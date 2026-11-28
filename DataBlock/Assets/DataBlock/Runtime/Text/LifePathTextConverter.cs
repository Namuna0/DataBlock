using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static class LifePathTextConverter
{
    private const string GroupHeader = "【ライフパス分類】";
    private const string StatHeader = "【ライフパス能力値】";
    private const string Second = "【セカンドスパイク】";
    private const string Third = "【サードスパイク】";
    private static readonly Regex SpikeStats = DataBlockText.GetRegex(
        @"^このライ[フプ]パス能力値(?:による|の)(.+?)に変化する(?:。に変化する)?。?(.*)$");
    private static readonly Regex StatAssignment = DataBlockText.GetRegex(@"^([^は、,。\s]+)は([+-]?[0-9]+)$");

    public static LifePathTextData Parse(string text)
    {
        var blocks = SkillTextConverter.InputBlocks(text, SplitPlain);
        if (blocks.Count != 1) throw new InvalidOperationException("ライフパスを1件指定してください。");
        return ParseBlock(blocks[0]);
    }

    public static string Normalize(string text) =>
        string.Join("\n\n", SkillTextConverter.InputBlocks(text, SplitPlain).Select(block => Build(ParseBlock(block))));

    private static List<string> SplitPlain(string text)
    {
        var lines = text.Split('\n');
        var blocks = new List<string>();
        int start = 0;
        for (int i = 1; i + 1 < lines.Length; i++)
        {
            if (!DataBlockText.GetRegex(@"^《[^《》]+》$").IsMatch(lines[i].Trim())) continue;
            int next = i + 1;
            while (next < lines.Length && string.IsNullOrWhiteSpace(lines[next])) next++;
            if (next == lines.Length || !DataBlockText.GetRegex(@"^〈(?:[^〉]+[,、]\s*)?ライフパス(?:[,、][^〉]+)?〉$").IsMatch(lines[next].Trim())) continue;
            blocks.Add(string.Join("\n", lines, start, i - start));
            start = i;
        }
        blocks.Add(string.Join("\n", lines, start, lines.Length - start));
        return blocks;
    }

    private static LifePathTextData ParseBlock(string block)
    {
        var data = new LifePathTextData();
        var body = new List<string>();
        bool hasStats = false, afterStats = false;
        var lines = block.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith("――――――――", StringComparison.Ordinal))
            {
                body.AddRange(lines.Skip(i));
                break;
            }
            if (afterStats && line.Length > 0)
            {
                // 能力値欄に直接続く共有効果文はライフパス本体のパッシブ。
                afterStats = false;
                if (!line.StartsWith("【", StringComparison.Ordinal) &&
                    CommonEffectTextCodec.TryRead(line.TrimStart('・'), out _))
                    body.Add("【パッシブ効果】");
            }
            if (line.StartsWith(GroupHeader, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(data.Group)) throw new InvalidOperationException("ライフパス分類が重複しています。");
                data.Group = ReadGroup(line.Substring(GroupHeader.Length).Trim());
                continue;
            }
            if (line.StartsWith(StatHeader, StringComparison.Ordinal))
            {
                if (hasStats) throw new InvalidOperationException("ライフパス能力値が重複しています。");
                hasStats = afterStats = true;
                string stats = line.Substring(StatHeader.Length).Trim().TrimEnd(',', '、');
                if (stats.Length == 0) continue;
                stats = DataBlockText.GetRegex(@"(?<=[0-9])\.(?=\s*[^\s0-9])").Replace(stats, ",");
                data.AbilityBonuses = SkillTextConverter.ProfileBonuses(stats, false);
                continue;
            }
            if (line.StartsWith("【パッシブスキル】", StringComparison.Ordinal))
                line = "【パッシブ効果】" + line.Substring("【パッシブスキル】".Length);
            if (line.StartsWith("【セカンドパイク】", StringComparison.Ordinal))
                line = Second + line.Substring("【セカンドパイク】".Length);
            string stage = line.StartsWith(Second, StringComparison.Ordinal) ? Second :
                line.StartsWith(Third, StringComparison.Ordinal) ? Third : null;
            if (stage != null)
            {
                string value = line.Substring(stage.Length).Trim();
                int at = i;
                while (value.Length == 0 && at + 1 < lines.Length)
                    value = lines[++at].Trim().TrimStart('・').Trim();
                var match = SpikeStats.Match(value);
                if (match.Success)
                {
                    var stats = stage == Second ? data.SecondSpikeBonuses : data.ThirdSpikeBonuses;
                    if (stats.Count > 0) throw new InvalidOperationException(stage + "ライフパス能力値が重複しています。");
                    ReadSpikeStats(stats, match.Groups[1].Value);
                    i = at;
                    body.Add(stage + match.Groups[2].Value.Trim());
                    continue;
                }
            }
            body.Add(line);
        }
        data.Definition = SkillTextConverter.Parse(string.Join("\n", body));
        RequireLifePath(data);
        return data;
    }

    private static void ReadSpikeStats(List<ProfileBonus> stats, string source)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (string part in DataBlockText.GetRegex(@"に?\s*[,、]\s*").Split(source))
        {
            Match pair = StatAssignment.Match(part.Trim());
            if (!pair.Success || !int.TryParse(pair.Groups[2].Value, NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out int amount))
                throw new InvalidOperationException("スパイクのライフパス能力値が不正です：" + part);
            foreach (string name in pair.Groups[1].Value.Split('と'))
            {
                if (name.Length == 0 || !names.Add(name)) throw new InvalidOperationException("能力値名が空欄または重複しています：" + part);
                stats.Add(new ProfileBonus { Stat = name, Amount = amount });
            }
        }
    }

    private static string ReadGroup(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() != value || value.IndexOfAny(new[] { '\r', '\n', '【', '】' }) >= 0)
            throw new InvalidOperationException("ライフパス分類を1行で指定してください。");
        return value;
    }

    private static void RequireLifePath(LifePathTextData data)
    {
        if (data?.Definition?.Skill?.Categories == null || !data.Definition.Skill.Categories.Contains("ライフパス"))
            throw new InvalidOperationException("〈ライフパス〉のデータを指定してください。");
    }

    private static string FormatStats(List<ProfileBonus> stats, bool spike)
    {
        if (stats == null || stats.Any(b => b == null || string.IsNullOrWhiteSpace(b.Stat) ||
            b.Stat.Any(c => char.IsWhiteSpace(c) || "+-%,、。とには【】".Contains(c))))
            throw new InvalidOperationException("ライフパス能力値の指定が不正です。");
        if (stats.Count == 0) return null;
        if (stats.Select(b => b.Stat).Distinct(StringComparer.Ordinal).Count() != stats.Count)
            throw new InvalidOperationException("ライフパス能力値が重複しています。");
        string Signed(int n) => n.ToString("+0;-0;0", CultureInfo.InvariantCulture);
        if (!spike)
            return string.Join(", ", stats.Select(b => b.Stat + (b.Amount == 0 ? "+0" : Signed(b.Amount))));

        var parts = new List<string>();
        for (int i = 0; i < stats.Count;)
        {
            int start = i++;
            while (i < stats.Count && stats[i].Amount == stats[start].Amount) i++;
            var names = stats.GetRange(start, i - start).Select(b => b.Stat);
            parts.Add(string.Join("と", names) + "は" + Signed(stats[start].Amount));
        }
        return "このライフパス能力値による" + string.Join("、", parts) + "に変化する。";
    }

    public static string Build(LifePathTextData data)
    {
        RequireLifePath(data);
        string second = FormatStats(data.SecondSpikeBonuses, true);
        string third = FormatStats(data.ThirdSpikeBonuses, true);
        var lines = SkillTextConverter.Build(data.Definition, true, second, third).Split('\n').ToList();
        var fields = new List<string>();
        if (!string.IsNullOrEmpty(data.Group)) fields.Add(GroupHeader + ReadGroup(data.Group));
        string stats = FormatStats(data.AbilityBonuses, false);
        if (stats != null) fields.Add(StatHeader + stats);
        lines.InsertRange(3, fields);
        return string.Join("\n", lines);
    }
}
