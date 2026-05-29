using System.Diagnostics;
using System.Reflection;
using System.Text;
using EditorScene.Chart;
using EditorScene.Check;
using Global.Chart;
using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(Starrah.VM_MaiMuri.Core), "VisualMaimai-Integration-MaiMuriDX", "2.0.0", "Starrah")]
[assembly: MelonGame("CH3COOOH", "Visual Maimai")]

namespace Starrah.VM_MaiMuri;

[HarmonyPatch]
public class Core : MelonMod
{
    private const string PythonExecutable = "python";
    private const int RunTimeoutMs = 30000;
    private const int DebounceMs = 300;

    private static string cliPath;

    public override void OnInitializeMelon()
    {
        var expected = GetMaiMuriDXPath();
        if (expected == null || !File.Exists(expected))
        {
            MelonLogger.Error($"未找到有效的MaiMuriDX程序，无法启用本Mod的功能。请确保按照文档所述正确的配置了MaiMuriDX。（MaiMuriDX的cli.py应当位于：{expected ?? "<unknown>"}）");
            return;
        }
        cliPath = expected;
        HarmonyInstance.PatchAll();
        MelonLogger.Msg("MaiMuriDX集成模块已加载");
    }

    [HarmonyPatch(typeof(ChartWatcher), nameof(ChartWatcher.Check))]
    [HarmonyPostfix]
    private static void Postfix(NotesData chart, ref Dictionary<TimeData, List<CheckResult>> results)
    {
        Process process = null;
        try
        {
            // 等一小会再开始检查。（这期间VM内置的逻辑可能会把这个线程整个销毁掉，也就不触发python调用了）
            // 不然高速连点的情况下会反复fork进程又立即销毁。
            Thread.Sleep(DebounceMs);

            string simaiNotes;
            try
            {
                simaiNotes = ChartExporter.ExportNote(chart, false);
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"Failed to export simai notes: {e}");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = PythonExecutable,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), // 没有BOM
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(cliPath)!,
            };
            psi.ArgumentList.Add(cliPath);
            psi.ArgumentList.Add("--first");
            psi.ArgumentList.Add(OperationManager.Chart.offset.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            psi.EnvironmentVariables["PYTHONUTF8"] = "1";

            process = new Process { StartInfo = psi };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            process.StandardInput.Write(simaiNotes);
            process.StandardInput.Close();

            if (!process.WaitForExit(RunTimeoutMs))
            {
                try { process.Kill(); } catch { /*ignored*/ }
                MelonLogger.Warning($"MaiMuriDX timed out after {RunTimeoutMs}ms.");
                return;
            }
            process.WaitForExit(); // WaitForExit(timeout) doesn't wait for async output handlers to finish; the parameterless overload does.
            if (process.ExitCode != 0)
            {
                MelonLogger.Warning($"MaiMuriDX exited with code {process.ExitCode}.\nstderr: {stderr}");
                return;
            }

            var report = stdout.ToString();
            var parsed = MaiMuriReportParser.Parse(report);
            MaiMuriReportParser.MergeIntoResults(parsed, chart.bpmList, results);
        }
        catch (ThreadAbortException) { /* VM主动发起的进程abort。因此直接忽略即可 */ }
        finally
        {
            try { if (process != null && !process.HasExited) process.Kill(); } catch { /*ignored*/ }
            try { process?.Dispose(); } catch { /*ignored*/ }
        }
    }

    private static string GetMaiMuriDXPath()
    {
        try
        {
            var loc = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(loc)) return null;
            return Path.Combine(Path.GetDirectoryName(loc)!, "MaiMuriDX", "cli.py");
        }
        catch
        {
            return null;
        }
    }
}
