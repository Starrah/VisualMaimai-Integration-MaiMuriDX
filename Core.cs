using System.Diagnostics;
using System.Reflection;
using System.Text;
using EditorScene.Check;
using Global.Chart;
using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(Starrah.VM_MaiMuri.Core), "VisualMaimai-Integration-MaiMuriDX", "2.0.0", "Starrah")]
[assembly: MelonGame("CH3COOOH", "Visual Maimai")]

namespace Starrah.VM_MaiMuri;

public class Core : MelonMod
{
    private const string PythonExecutable = "python";
    private const int RunTimeoutMs = 5000;
    private const int DebounceMs = 0;

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
        HarmonyInstance.PatchAll(GetType());
        MelonLogger.Msg("MaiMuriDX集成模块已加载");
    }

    [HarmonyPatch(typeof(ChartWatcher), nameof(ChartWatcher.Check))]
    [HarmonyPostfix]
    private static void RunMaiMuriDX(NotesData chart, ref Dictionary<TimeData, List<CheckResult>> results, ref bool __result)
    {
        Process process = null;
        try
        {
            // 等一小会再开始检查。（这期间VM内置的逻辑可能会把这个线程整个销毁掉，也就不触发python调用了）
            // 不然高速连点的情况下会反复fork进程又立即销毁。
#pragma warning disable CS0162 // 检测到不可到达的代码
            if (DebounceMs > 0) Thread.Sleep(DebounceMs);
#pragma warning restore CS0162 // 检测到不可到达的代码

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
            __result = results.Count == 0;
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

    [HarmonyPatch(typeof(CheckResult), nameof(CheckResult.ToString))]
    [HarmonyPrefix]
    private static bool CheckResultToString(CheckResult __instance, ref string __result)
    {
        if (__instance.Code < 100) return true; // 非MaiMuriDX，而是VM原生的无理
        __result = __instance.Type switch
        {
            ResultType.Bad => $"<color=#FF6767>[MaiMuriDX] {__instance.Info}</color>",
            ResultType.Warning => $"<color=#FDFF45>[MaiMuriDX] {__instance.Info}</color>",
            _ => throw new ArgumentOutOfRangeException(),
        };
        return false;
    }
}
