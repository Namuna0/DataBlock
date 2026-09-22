using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static partial class EnemyTextConverter
{
    private const string Separator = "――――――――――――――――";
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly ConcurrentDictionary<EnemyRegexKey, Regex> RegexCache = new ConcurrentDictionary<EnemyRegexKey, Regex>();

    public static EnemyTextData Parse(string source)
    {
        List<string> blocks = InputBlocks(source);
        if (blocks.Count != 1) throw new InvalidOperationException(blocks.Count + "件のエネミーがあります。シリアライズセットは1件ずつ行ってください。構文統一はまとめて実行できます。");
        EnemyTextData data = ParseOne(blocks[0]);
        Build(data);
        return data;
    }

    public static string Normalize(string source)
    {
        return string.Join("\n\n", InputBlocks(source).Select(x => Build(ParseOne(x))));
    }

    public static string Build(EnemyTextData data)
    {
        EnemyDefinition enemy = Validate(data);
        var sb = new StringBuilder();
        sb.AppendLine("```none");
        sb.AppendLine("《" + Token(enemy.Name, "エネミー名", "《》") + "》");
        sb.AppendLine(CategoryText(enemy.Categories));
        sb.AppendLine("【危険度】" + Need(enemy.DangerLevel, "危険度"));
        foreach (EnemyStatDefinition stat in enemy.Stats)
            sb.AppendLine("【" + Need(stat.Name, "能力値名") + "】" + Need(stat.Formula, stat.Name + "の値"));

        sb.AppendLine();
        sb.AppendLine("【行動】");
        foreach (EnemyActionRule action in enemy.Actions) sb.AppendLine("●" + ActionText(action));

        foreach (SkillTextData skill in enemy.Skills)
        {
            sb.AppendLine();
            sb.AppendLine(SkillBodyText(skill));
        }

        sb.AppendLine();
        sb.AppendLine("【ドロップロール】" + Need(enemy.DropRoll, "ドロップロール"));
        foreach (EnemyDropDefinition drop in OrderedDrops(enemy)) sb.AppendLine(DropText(drop));
        if (!string.IsNullOrWhiteSpace(enemy.Flavor))
        {
            sb.AppendLine(Separator);
            sb.AppendLine(enemy.Flavor.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd());
        }
        sb.Append("```");
        return sb.ToString().Replace("\r\n", "\n");
    }

    private static EnemyTextData ParseOne(string source)
    {
        string[] lines = source.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        int index = 0;
        SkipEmpty(lines, ref index);
        if (index >= lines.Length) throw new InvalidOperationException("エネミーテキストが空です。");

        Match name = Match(SyntaxLine(lines[index]), @"^《([^《》]+)》$");
        if (!name.Success) throw LineError(index, lines[index], "先頭に《エネミー名》を指定してください。");
        var data = new EnemyTextData();
        EnemyDefinition enemy = data.Enemy;
        enemy.Name = Need(name.Groups[1].Value, "エネミー名");
        index++;

        SkipEmpty(lines, ref index);
        if (index >= lines.Length) throw new InvalidOperationException("エネミーのカテゴリーがありません。");
        try { enemy.Categories = ParseCategories(SyntaxLine(lines[index])); }
        catch (InvalidOperationException exception) { throw LineError(index, lines[index], exception.Message, exception); }
        index++;

        bool actionHeader = false;
        while (index < lines.Length)
        {
            SkipEmpty(lines, ref index);
            if (index >= lines.Length) break;
            string line = SyntaxLine(lines[index]);
            if (line == "【行動】") { actionHeader = true; index++; break; }
            Match header = Match(line, @"^【([^】]+)】(.+)$");
            if (!header.Success) throw LineError(index, lines[index], "能力値は【名前】値の形式です。");
            string key = Need(header.Groups[1].Value, "能力値名");
            string value = Need(header.Groups[2].Value, key + "の値");
            if (key == "危険度")
            {
                if (enemy.DangerLevel.Length != 0) throw LineError(index, lines[index], "危険度が重複しています。");
                enemy.DangerLevel = value;
            }
            else
            {
                if (enemy.Stats.Any(x => x.Name == key)) throw LineError(index, lines[index], "能力値が重複しています：" + key);
                enemy.Stats.Add(new EnemyStatDefinition { Name = key, Formula = value });
            }
            index++;
        }
        if (!actionHeader) throw new InvalidOperationException("【行動】がありません。");

        while (index < lines.Length)
        {
            SkipEmpty(lines, ref index);
            if (index >= lines.Length) break;
            string line = SyntaxLine(lines[index]);
            if (TryNameHeader(line, out _)) break;
            if (!line.StartsWith("●", StringComparison.Ordinal)) throw LineError(index, lines[index], "行動は●から始めてください。");
            try { enemy.Actions.Add(ParseAction(line.Substring(1).Trim())); }
            catch (InvalidOperationException exception) { throw LineError(index, lines[index], exception.Message, exception); }
            index++;
        }
        if (enemy.Actions.Count == 0) throw new InvalidOperationException("行動がありません。");

        var referencedNames = new HashSet<string>(enemy.Actions.Select(x => x.SkillName), StringComparer.Ordinal);
        var definedNames = new HashSet<string>(StringComparer.Ordinal);
        Match dropHeader = Match("", "$a");
        while (index < lines.Length)
        {
            SkipEmpty(lines, ref index);
            if (index >= lines.Length) break;
            string line = SyntaxLine(lines[index]);
            dropHeader = Match(line, @"^【ドロップロール】(.+)$");
            if (dropHeader.Success) break;
            string skillName;
            if (!TrySkillBoundary(lines, index, referencedNames, out skillName))
                throw LineError(index, lines[index], "行動から参照されていないスキル定義です。");

            int next = index + 1;
            while (next < lines.Length)
            {
                string candidate = SyntaxLine(lines[next]);
                if (Match(candidate, @"^【ドロップロール】(.+)$").Success) break;
                string nextName;
                if (TrySkillBoundary(lines, next, referencedNames, out nextName)) break;
                next++;
            }

            string skillSource = string.Join("\n", lines.Skip(index).Take(next - index)).Trim();
            SkillTextData skill;
            try { skill = SkillTextConverter.Parse("```\n" + skillSource + "\n```"); }
            catch (InvalidOperationException exception) { throw LineError(index, lines[index], "行動スキルを解析できません：" + exception.Message, exception); }
            if (!definedNames.Add(skill.Skill.Name)) throw LineError(index, lines[index], "行動スキル定義が重複しています：" + skill.Skill.Name);
            enemy.Skills.Add(skill);
            index = next;
        }

        if (!dropHeader.Success)
        {
            if (index < lines.Length) dropHeader = Match(SyntaxLine(lines[index]), @"^【ドロップロール】(.+)$");
            if (!dropHeader.Success) throw new InvalidOperationException("【ドロップロール】がありません。");
        }
        enemy.DropRoll = Need(dropHeader.Groups[1].Value, "ドロップロール");
        index++;

        while (index < lines.Length)
        {
            string line = SyntaxLine(lines[index]);
            if (line.Length == 0) { index++; continue; }
            if (Match(line, @"^―{8,}$").Success)
            {
                index++;
                enemy.Flavor = string.Join("\n", lines.Skip(index)).TrimEnd();
                index = lines.Length;
                break;
            }
            try { enemy.Drops.Add(ParseDrop(line)); }
            catch (InvalidOperationException exception) { throw LineError(index, lines[index], exception.Message, exception); }
            index++;
        }
        return data;
    }

    private static EnemyDefinition Validate(EnemyTextData data)
    {
        if (data == null || data.Enemy == null) throw new InvalidOperationException("エネミーデータがnullです。");
        EnemyDefinition enemy = data.Enemy;
        Need(enemy.Name, "エネミー名");
        Need(enemy.DangerLevel, "危険度");
        if (enemy.Categories == null || enemy.Categories.Count == 0 || enemy.Stats == null || enemy.Actions == null || enemy.Skills == null || enemy.Drops == null)
            throw new InvalidOperationException("カテゴリー・能力値・行動・スキル・ドロップを初期化してください。");
        CategoryText(enemy.Categories);
        if (enemy.Stats.Any(x => x == null)) throw new InvalidOperationException("能力値にnullがあります。");
        string[] statNames = enemy.Stats.Select(x => Token(x.Name, "能力値名", "【】")).ToArray();
        if (statNames.Distinct(StringComparer.Ordinal).Count() != statNames.Length || statNames.Contains("危険度", StringComparer.Ordinal))
            throw new InvalidOperationException("能力値名が重複しているか、予約名の「危険度」が使われています。");
        foreach (EnemyStatDefinition stat in enemy.Stats) Need(stat.Formula, Token(stat.Name, "能力値名", "【】") + "の値");
        if (enemy.Actions.Count == 0 || enemy.Actions.Any(x => x == null)) throw new InvalidOperationException("行動が空またはnullです。");
        if (enemy.Skills.Count == 0 || enemy.Skills.Any(x => x == null || x.Skill == null)) throw new InvalidOperationException("行動スキルが空またはnullです。");
        if (enemy.Skills.Any(x => x.Skill.Categories == null || x.Skill.Categories.Count == 0))
            throw new InvalidOperationException("行動スキルにはカテゴリーが1件以上必要です。");

        var references = new HashSet<string>(enemy.Actions.Select(x => { ActionText(x); return Need(x.SkillName, "行動スキル名"); }), StringComparer.Ordinal);
        string[] skillNames = enemy.Skills.Select(x => Token(x.Skill.Name, "行動スキル名", "《》")).ToArray();
        var skillNameSet = new HashSet<string>(skillNames, StringComparer.Ordinal);
        if (skillNameSet.Count != skillNames.Length) throw new InvalidOperationException("行動スキル名が重複しています。");
        foreach (string reference in references)
            if (!skillNameSet.Contains(reference)) throw new InvalidOperationException("行動スキル定義がありません：" + reference);
        foreach (string skillName in skillNames)
            if (!references.Contains(skillName)) throw new InvalidOperationException("行動から参照されていないスキル定義です：" + skillName);

        var topLevelNames = skillNameSet;
        foreach (SkillTextData skill in enemy.Skills)
        {
            if (skill.Summons == null) continue;
            foreach (SummonedEntityDefinition summon in skill.Summons.Where(x => x != null))
            {
                string summonName = Token(summon.Name, "召喚名", "《》");
                if (topLevelNames.Contains(summonName))
                    throw new InvalidOperationException("行動スキル名と召喚名は重複できません：" + summonName);
            }
        }
        Need(enemy.DropRoll, "ドロップロール");
        OrderedDrops(enemy);
        return enemy;
    }

    private static string SkillBodyText(SkillTextData skill)
    {
        string[] lines = SkillTextConverter.Build(skill).Replace("\r\n", "\n").Split('\n');
        if (lines.Length < 3 || !lines[0].StartsWith("```", StringComparison.Ordinal) || lines[lines.Length - 1] != "```")
            throw new InvalidOperationException("行動スキルのコードフェンスが不正です。");
        return string.Join("\n", lines.Skip(1).Take(lines.Length - 2));
    }

    private static List<string> InputBlocks(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new InvalidOperationException("入力テキストが空です。");
        string text = source.Replace("\r\n", "\n").Replace("\r", "\n");
        text = Replace(text, @"\n?[ \t]*<br\s*/?>[ \t]*\n?", "\n", RegexOptions.IgnoreCase);
        string[] lines = text.Split('\n');
        bool fenced = lines.Any(x => OpeningFence(NormalizeFenceLine(x)).Success);
        if (!fenced)
        {
            string plain = text.Trim();
            if (plain.Length >= 2 && plain.StartsWith("|", StringComparison.Ordinal) && plain.EndsWith("|", StringComparison.Ordinal)) plain = plain.Substring(1, plain.Length - 2).Trim();
            return new List<string> { plain };
        }

        var blocks = new List<string>();
        var body = new List<string>();
        bool inside = false;
        bool quoted = false;
        int openingLength = 0;
        foreach (string raw in lines)
        {
            string line = NormalizeFenceLine(raw);
            if (!inside)
            {
                if (line.Length == 0 || line == "|" || Match(line, @"^\|\s*:?-{3,}:?\s*\|$").Success) continue;
                Match opening = OpeningFence(line);
                if (!opening.Success)
                {
                    if (line.StartsWith("《", StringComparison.Ordinal) || line.StartsWith("【", StringComparison.Ordinal))
                        throw new InvalidOperationException("エネミーの行がコードブロック外にあります：" + raw);
                    continue;
                }
                quoted = line.Contains("\"");
                openingLength = opening.Groups["ticks"].Value.Length;
                inside = true;
                body.Clear();
                continue;
            }
            Match closing = Match(line, @"^(?<ticks>`{3,})""?\s*\|?$");
            if (closing.Success && closing.Groups["ticks"].Value.Length >= openingLength)
            {
                string block = string.Join("\n", body);
                if (quoted) block = block.Replace("\"\"", "\"");
                blocks.Add(block);
                inside = false;
                continue;
            }
            body.Add(raw);
        }
        if (inside) throw new InvalidOperationException("末尾のコードフェンスがありません。");
        if (blocks.Count == 0) throw new InvalidOperationException("エネミーのコードブロックがありません。");
        return blocks;
    }

    private static Match OpeningFence(string line)
    {
        return Match(line, @"^(?:\|\s*)?""?(?<ticks>`{3,})(?:none|text|txt|markdown|md)?""?\s*\|?$");
    }

    private static string NormalizeFenceLine(string raw)
    {
        string line = raw.Trim();
        if (!line.Contains("\\`")) return line;
        string candidate = line.Replace("\\`", "`");
        return OpeningFence(candidate).Success || Match(candidate, @"^`{3,}""?\s*\|?$").Success ? candidate : line;
    }

    private static bool TryNameHeader(string line, out string name)
    {
        Match match = Match(line, @"^《([^《》]+)》$");
        name = match.Success ? match.Groups[1].Value : "";
        return match.Success;
    }

    private static bool TrySkillBoundary(string[] lines, int index, HashSet<string> referencedNames, out string name)
    {
        name = "";
        if (index < 0 || index >= lines.Length || !TryNameHeader(SyntaxLine(lines[index]), out name) || !referencedNames.Contains(name))
            return false;
        int next = index + 1;
        SkipEmpty(lines, ref next);
        return next < lines.Length && Match(SyntaxLine(lines[next]), @"^(?:〈[^〉]+〉)+$").Success;
    }

    private static List<string> ParseCategories(string text)
    {
        if (!Match(text, @"^(?:〈[^〉]+〉)+$").Success) throw new InvalidOperationException("カテゴリーの形式が不正です：" + text);
        List<string> values = Matches(text, @"〈([^〉]+)〉").Cast<Match>().SelectMany(x => x.Groups[1].Value.Split(',')).Select(x => Need(x, "カテゴリー")).ToList();
        if (values.Count == 0 || values.Distinct(StringComparer.Ordinal).Count() != values.Count) throw new InvalidOperationException("カテゴリーが空または重複しています。");
        return values;
    }

    private static string CategoryText(List<string> values)
    {
        if (values == null || values.Count == 0) throw new InvalidOperationException("カテゴリーがありません。");
        string[] categories = values.Select(x => Token(x, "カテゴリー", "〈〉,")).ToArray();
        if (categories.Distinct(StringComparer.Ordinal).Count() != categories.Length) throw new InvalidOperationException("カテゴリーが重複しています。");
        return "〈" + string.Join(", ", categories) + "〉";
    }

    private static string SyntaxLine(string line) { return line.Trim().Replace("\\*", "*"); }
    private static void SkipEmpty(string[] lines, ref int index) { while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index])) index++; }
    private static InvalidOperationException LineError(int index, string line, string message, Exception inner = null)
    {
        return new InvalidOperationException((index + 1) + "行目：" + message + "\n原文：" + line, inner);
    }

    private static string Need(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException(label + "が空です。");
        if (value.Contains("\n") || value.Contains("\r")) throw new InvalidOperationException(label + "に改行は使用できません。");
        return value.Trim();
    }

    private static string Token(string value, string label, string forbiddenCharacters)
    {
        string token = Need(value, label);
        if (token.IndexOfAny(forbiddenCharacters.ToCharArray()) >= 0)
            throw new InvalidOperationException(label + "に構文区切り文字「" + forbiddenCharacters + "」は使用できません。");
        return token;
    }

    private static int Number(string value, int minimum, string label)
    {
        int number;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) || number < minimum)
            throw new InvalidOperationException(label + "は" + minimum + "以上の整数にしてください。");
        return number;
    }

    private static Match Match(string text, string pattern, RegexOptions options = RegexOptions.None) { return GetRegex(pattern, options).Match(text); }
    private static MatchCollection Matches(string text, string pattern, RegexOptions options = RegexOptions.None) { return GetRegex(pattern, options).Matches(text); }
    private static string Replace(string text, string pattern, string replacement, RegexOptions options = RegexOptions.None) { return GetRegex(pattern, options).Replace(text, replacement); }
    private static Regex GetRegex(string pattern, RegexOptions options)
    {
        options |= RegexOptions.CultureInvariant;
        return RegexCache.GetOrAdd(new EnemyRegexKey(pattern, options), x => new Regex(x.Pattern, x.Options, RegexTimeout));
    }

    private readonly struct EnemyRegexKey : IEquatable<EnemyRegexKey>
    {
        public readonly string Pattern;
        public readonly RegexOptions Options;
        public EnemyRegexKey(string pattern, RegexOptions options) { Pattern = pattern; Options = options; }
        public bool Equals(EnemyRegexKey other) { return Options == other.Options && string.Equals(Pattern, other.Pattern, StringComparison.Ordinal); }
        public override bool Equals(object value) { return value is EnemyRegexKey && Equals((EnemyRegexKey)value); }
        public override int GetHashCode()
        {
            unchecked { return ((Pattern == null ? 0 : StringComparer.Ordinal.GetHashCode(Pattern)) * 397) ^ (int)Options; }
        }
    }
}
