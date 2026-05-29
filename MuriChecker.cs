using System.Diagnostics;
using System.Reflection;
using System.Text;
using MelonLoader;

namespace Starrah.VM_MaiMuri;

public static class MuriChecker
{
    private const string PythonExecutable = "python";
    private const int RunTimeoutMs = 30_000;

    private static int _running;

    private static string _cachedCliPath;
    private static string _cachedWorkingDir;

    public delegate void ReportHandler(bool success, string report, string stderr);
    public static event ReportHandler OnReport;

    public static bool TryCheckAsync(string maidata)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return false;

        Task.Run(async () =>
        {
            try
            {
                await RunCheckerAsync(maidata);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Muri check failed: {e}");
                try { OnReport?.Invoke(false, null, e.ToString()); } catch { }
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        });
        return true;
    }

    private static async Task RunCheckerAsync(string maidata)
    {
        var cliPath = ResolveCliPath();
        if (cliPath == null)
        {
            MelonLogger.Warning($"MaiMuriDX cli.py not found next to mod assembly. Expected at: {GetExpectedCliPath()}");
            try { OnReport?.Invoke(false, null, "cli.py not found"); } catch { }
            return;
        }

        var tempInput = Path.Combine(Path.GetTempPath(), $"vm_muri_in_{Guid.NewGuid():N}.txt");
        var tempOutput = Path.Combine(Path.GetTempPath(), $"vm_muri_out_{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllText(tempInput, maidata, new UTF8Encoding(false));

            var psi = new ProcessStartInfo
            {
                FileName = PythonExecutable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
                WorkingDirectory = _cachedWorkingDir,
            };
            psi.ArgumentList.Add(cliPath);
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add(tempInput);
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(tempOutput);
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            psi.EnvironmentVariables["PYTHONUTF8"] = "1";

            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

            var exitTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            p.Exited += (_, _) => exitTcs.TrySetResult(true);

            if (!p.Start())
            {
                MelonLogger.Error("Failed to start python process.");
                try { OnReport?.Invoke(false, null, "Process.Start returned false"); } catch { }
                return;
            }

            var stdoutTask = p.StandardOutput.ReadToEndAsync();
            var stderrTask = p.StandardError.ReadToEndAsync();

            var timeoutTask = Task.Delay(RunTimeoutMs);
            var completed = await Task.WhenAny(exitTcs.Task, timeoutTask);
            if (completed == timeoutTask)
            {
                MelonLogger.Warning($"MaiMuriDX timed out after {RunTimeoutMs}ms; killing process.");
                try { p.Kill(); } catch { }
                try { OnReport?.Invoke(false, null, "Timeout"); } catch { }
                return;
            }

            var stderr = await stderrTask;
            _ = await stdoutTask;

            if (p.ExitCode != 0)
            {
                MelonLogger.Warning($"MaiMuriDX exited with code {p.ExitCode}.\nstderr: {stderr}");
                try { OnReport?.Invoke(false, null, stderr); } catch { }
                return;
            }

            var report = File.Exists(tempOutput)
                ? File.ReadAllText(tempOutput, Encoding.UTF8)
                : string.Empty;

            MelonLogger.Msg($"MaiMuriDX report:\n{report}");
            try { OnReport?.Invoke(true, report, stderr); } catch { }
        }
        finally
        {
            try { if (File.Exists(tempInput)) File.Delete(tempInput); } catch { }
            try { if (File.Exists(tempOutput)) File.Delete(tempOutput); } catch { }
        }
    }

    private static string ResolveCliPath()
    {
        if (_cachedCliPath != null) return _cachedCliPath;

        var modDir = GetModDirectory();
        if (modDir == null) return null;

        var cli = Path.Combine(modDir, "MaiMuriDX", "cli.py");
        if (!File.Exists(cli)) return null;

        _cachedCliPath = cli;
        _cachedWorkingDir = Path.GetDirectoryName(cli);
        return _cachedCliPath;
    }

    private static string GetExpectedCliPath()
    {
        var modDir = GetModDirectory() ?? "<unknown>";
        return Path.Combine(modDir, "MaiMuriDX", "cli.py");
    }

    private static string GetModDirectory()
    {
        try
        {
            var loc = Assembly.GetExecutingAssembly().Location;
            return string.IsNullOrEmpty(loc) ? null : Path.GetDirectoryName(loc);
        }
        catch
        {
            return null;
        }
    }
}
