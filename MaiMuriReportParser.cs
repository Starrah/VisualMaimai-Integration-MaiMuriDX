using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EditorScene.Check;
using Global.Chart;
using MelonLoader;
using Settings.Managers;

namespace Starrah.VM_MaiMuri;

internal struct ParsedMuri
{
    public float TimeSeconds;
    public int Code;
    public string Info;
}

internal static class MaiMuriReportParser
{
    private static readonly Regex TimestampRegex = new(
        @"^\[(\d{2}):(\d{2})F([\d.]+)\]\s*(.*)$",
        RegexOptions.Compiled);

    private static readonly Regex MuriHeaderRegex = new(
        @"(内屏无理|多押无理|叠键无理|外键无理|撞尾无理)",
        RegexOptions.Compiled);

    // MaiMuriDX result codes (100+) — distinct from VM built-in 0..8.
    public const int Code_内屏 = 100;
    public const int Code_多押 = 101;
    public const int Code_叠键_Static = 102;
    public const int Code_外键 = 103;
    public const int Code_撞尾 = 104;
    public const int Code_叠键_Dynamic = 105;

    public static List<ParsedMuri> Parse(string stdout)
    {
        var messages = CollapseMessages(stdout);
        var parsed = new List<ParsedMuri>();
        foreach (var raw in messages)
        {
            if (ParseMessage(raw, out var item))
                parsed.Add(item);
        }
        return parsed;
    }

    public static void MergeIntoResults(
        IEnumerable<ParsedMuri> items,
        BpmData bpm,
        Dictionary<TimeData, List<CheckResult>> results)
    {
        foreach (var item in items)
        {
            var time = SecondsToTimeData(bpm, item.TimeSeconds);
            if (time.split == 0)
                continue;

            var result = new CheckResult(ResultType.Bad, item.Code, item.Info);
            if (!results.TryGetValue(time, out var list))
            {
                results[time] = new List<CheckResult> { result };
                continue;
            }

            if (!list.Exists(r => r.Code == result.Code))
                list.Add(result);
        }
    }

    /// <summary>
    /// Inverse of <see cref="BpmData.GetTime"/>, matching VM's ProgressPanel.GetAdsorptionTime.
    /// GetBeat returns the same float as (float)TimeData, i.e. 4 * beat / split.
    /// </summary>
    private static TimeData SecondsToTimeData(BpmData bpm, float seconds)
    {
        var beatSplit = SettingsManager.CurrentSettings.beatSplit;
        var beatFloat = bpm.GetBeat(seconds);
        MelonLogger.Msg($"{seconds}:{(long)Math.Round(beatFloat / 4f * beatSplit)}/{beatSplit}");
        return new TimeData(beatSplit, (long)Math.Round(beatFloat / 4f * beatSplit)).Normalize();
    }

    private static List<string> CollapseMessages(string stdout)
    {
        var messages = new List<string>();
        var current = new StringBuilder();

        foreach (var rawLine in stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0)
                continue;

            if (IsNoiseLine(line))
                continue;

            if (IsMessageStart(line))
            {
                if (current.Length > 0)
                    messages.Add(current.ToString());
                current.Clear();
                current.Append(line);
            }
            else if (current.Length > 0)
            {
                current.Append(' ');
                current.Append(line.Trim());
            }
        }

        if (current.Length > 0)
            messages.Add(current.ToString());
        return messages;
    }

    private static bool IsNoiseLine(string line)
    {
        return line.StartsWith("谱面加载完成", StringComparison.Ordinal)
               || line.StartsWith("==========", StringComparison.Ordinal)
               || line.StartsWith("检测完成", StringComparison.Ordinal)
               || line.StartsWith("L:", StringComparison.Ordinal);
    }

    private static bool IsMessageStart(string line) => TimestampRegex.IsMatch(line);

    private static bool ParseMessage(string message, out ParsedMuri item)
    {
        item = default;

        var ts = TimestampRegex.Match(message);
        if (!ts.Success)
            return false;

        var minutes = int.Parse(ts.Groups[1].Value, CultureInfo.InvariantCulture);
        var seconds = int.Parse(ts.Groups[2].Value, CultureInfo.InvariantCulture);
        var frames = float.Parse(ts.Groups[3].Value, CultureInfo.InvariantCulture);
        var timeSeconds = minutes * 60f + seconds + frames / 60f;
        var body = ts.Groups[4].Value;

        var header = MuriHeaderRegex.Match(body);
        if (!header.Success)
            return false;

        int code;
        string info;
        switch (header.Groups[1].Value)
        {
            case "内屏无理":
                code = Code_内屏;
                info = TrimInnerScreenBody(body);
                break;
            case "多押无理":
                code = Code_多押;
                info = NormalizeMultiTouchBody(body);
                break;
            case "叠键无理":
                code = body.Contains("重叠", StringComparison.Ordinal) ? Code_叠键_Static : Code_叠键_Dynamic;
                info = body;
                break;
            case "外键无理":
                code = Code_外键;
                info = body;
                break;
            case "撞尾无理":
                code = Code_撞尾;
                info = body;
                break;
            default:
                return false;
        }

        item = new ParsedMuri
        {
            TimeSeconds = timeSeconds,
            Code = code,
            Info = info
        };
        return true;
    }

    private static string TrimInnerScreenBody(string body)
    {
        const string trailer = "，相关判定区如下";
        var idx = body.IndexOf(trailer, StringComparison.Ordinal);
        if (idx >= 0)
            return body.Substring(0, idx);
        return body;
    }

    private static string NormalizeMultiTouchBody(string body)
    {
        var idx = body.IndexOf("多押无理", StringComparison.Ordinal);
        if (idx < 0)
            return body;

        return Regex.Replace(body.Substring(idx), @"\s+", " ").Trim();
    }
}
