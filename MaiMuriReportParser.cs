using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EditorScene.Check;
using Global.Chart;
using Settings.Managers;

namespace Starrah.VM_MaiMuri;

internal enum MuriKind { 内屏 = 100, 多押 = 101, 叠键 = 102, 外键 = 103, 撞尾 = 104 }

internal class ParsedMuri
{
    public float TimeSeconds;
    public int Combo;
    public MuriKind Kind;
    public string Info;
    public bool IsStatic;
}

internal static class MaiMuriReportParser
{
    private static readonly Regex TimestampRegex = new(
        @"^\[(\d{2}):(\d{2})F([\d.]+)\]\s*(.*)$",
        RegexOptions.Compiled);

    private static readonly Regex ComboRegex = new(
        @"(\d+)cb",
        RegexOptions.Compiled);

    private static readonly Regex MuriHeaderRegex = new(
        @"(内屏无理|多押无理|叠键无理|外键无理|撞尾无理)",
        RegexOptions.Compiled);

    private static readonly Regex LineColumnRegex = new(
        @"\(L\d+,C\d+\)",
        RegexOptions.Compiled);
    
    private static readonly Regex MultipleSpaceRegex = new(
        @"\s{2,}",
        RegexOptions.Compiled);
    
    private static readonly Regex CPRangeRegex = new(
        @"CP区间±\s*(\d+)\s*ms",
        RegexOptions.Compiled);

    public static List<ParsedMuri> Parse(string stdout)
    {
        var parsed = new List<ParsedMuri>();
        foreach (var raw in CollapseMessages(stdout))
        {
            if (ParseMessage(raw.Text, raw.IsStaticSection, out var item))
                parsed.Add(item);
        }
        return DeduplicateStaticDynamic(parsed);
    }

    // 显示等级的规则
    private static ResultType GetResultType(ParsedMuri item)
    {
        var result = ResultType.Bad;
        if (item.Kind == MuriKind.多押) result = ResultType.Warning; // 多押全是警告
        if (item.Info.Contains("可能") || item.Info.Contains("似乎")) result = ResultType.Warning; // 含有不确定词汇的，警告
        if (item.Kind is MuriKind.外键 or MuriKind.撞尾 && item.Info.Contains("x")) result = ResultType.Warning; // 保护套规避的外键或者撞尾无理，警告
        if (item.Kind == MuriKind.内屏)
        { // 很慢的星星构成的内无是警告
            var cpRangeMatch = CPRangeRegex.Match(item.Info);
            var cpRange = cpRangeMatch.Success ? int.Parse(cpRangeMatch.Groups[1].Value) : 0;
            if (cpRange >= 500 || (item.Info.Contains("w") && cpRange >= 335)) result = ResultType.Warning;
        }
        return result;
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

            var result = new CheckResult(GetResultType(item), (int)item.Kind, FormatInfoForUi(item.Info));
            if (!results.TryGetValue(time, out var list))
            {
                results[time] = new List<CheckResult> { result };
                continue;
            }

            if (!list.Exists(r => r.Code == result.Code))
                list.Add(result);
        }
    }

    /**
     * 1. 对叠键无理，如果同一个音符静态和动态都查到了，则只保留静态的结果，删除动态的结果。
     * 2. 对其他类型的无理，如果静态和动态都查到了，则字符串结果仅保留动态的检查结果，但时间戳以静态的为准。
     */
    private static List<ParsedMuri> DeduplicateStaticDynamic(List<ParsedMuri> items)
    {
        var result = new List<ParsedMuri>();
        var staticDict = new Dictionary<(int, MuriKind), ParsedMuri>();

        foreach (var item in items)
        {
            var key = (item.Combo, item.Kind);
            if (!item.IsStatic && item.Combo >= 0 && staticDict.ContainsKey(key))
            { // 对动态的无理，如果静态阶段已经被检查到过：
                if (item.Kind == MuriKind.叠键) continue; // 叠键无理，直接删除动态的结果，只留下静态结果
                else
                {
                    // ReSharper disable once NotAccessedVariable
                    staticDict.Remove(key, out var staticItem);
                    staticItem.IsStatic = false;
                    staticItem.Info = item.Info; // 其他类型的无理，字符串info替换为动态的结果，但时间戳仍以静态的为准
                    continue;
                }
            }
            
            result.Add(item);
            if (item.IsStatic && item.Combo >= 0)
            {
                staticDict.TryAdd(key, item);
                
                if (item.Kind == MuriKind.叠键)
                { // 叠键的情况，要把其他被叠的键的所有cb也加进去
                    foreach (var match in ComboRegex.Matches(item.Info).Skip(1))
                    {
                        var cbValue = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                        staticDict.TryAdd((cbValue, MuriKind.叠键), item);
                    }
                }
            }
        }
        return result;
    }

    private static TimeData SecondsToTimeData(BpmData bpm, float seconds)
    {
        var beatSplit = SettingsManager.CurrentSettings.beatSplit;
        var beatFloat = bpm.GetBeat(seconds);
        return new TimeData(beatSplit, (long)Math.Round(beatFloat / 4f * beatSplit)).Normalize();
    }

    private static int ParseAffectedCombo(string body)
    {
        var match = ComboRegex.Match(body);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
    }

    private static List<(string Text, bool IsStaticSection)> CollapseMessages(string stdout)
    {
        var messages = new List<(string Text, bool IsStaticSection)>();
        var current = new StringBuilder();
        var inStaticSection = false;

        foreach (var rawLine in stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0)
                continue;

            if (line.Contains("========== 静态检查 ==========", StringComparison.Ordinal))
            {
                inStaticSection = true;
                continue;
            }
            if (line.Contains("========== 动态检查 ==========", StringComparison.Ordinal))
            {
                inStaticSection = false;
                continue;
            }
            if (IsNoiseLine(line))
                continue;

            if (IsMessageStart(line))
            {
                if (current.Length > 0)
                    messages.Add((current.ToString(), inStaticSection));
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
            messages.Add((current.ToString(), inStaticSection));
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

    private static bool ParseMessage(string message, bool isStaticSection, out ParsedMuri item)
    {
        item = null;
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

        MuriKind kind;
        string info;
        switch (header.Groups[1].Value)
        {
            case "内屏无理":
                kind = MuriKind.内屏;
                info = TrimInnerScreenBody(body);
                break;
            case "多押无理":
                kind = MuriKind.多押;
                info = NormalizeMultiTouchBody(body);
                break;
            case "叠键无理":
                kind = MuriKind.叠键;
                info = body;
                break;
            case "外键无理":
                kind = MuriKind.外键;
                info = body;
                break;
            case "撞尾无理":
                kind = MuriKind.撞尾;
                info = body;
                break;
            default:
                return false;
        }

        item = new ParsedMuri
        {
            Combo = ParseAffectedCombo(body),
            TimeSeconds = timeSeconds,
            Info = info,
            Kind = kind,
            IsStatic = isStaticSection,
        };
        return true;
    }

    private static string FormatInfoForUi(string info)
    {
        info = LineColumnRegex.Replace(info, "");
        return MultipleSpaceRegex.Replace(info, " ").Trim();
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
