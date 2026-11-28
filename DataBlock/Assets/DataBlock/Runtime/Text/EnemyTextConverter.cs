using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static partial class EnemyTextConverter
{
    private const string Separator = "――――――――――――――――";

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
        sb.AppendLine("```");
        sb.AppendLine("《" + Token(enemy.Name, "エネミー名", "《》") + "》");
        sb.AppendLine(CategoryText(enemy.Categories));
        sb.AppendLine("【危険度】" + Need(enemy.DangerLevel, "危険度"));
        foreach (EnemyStatDefinition stat in enemy.Stats)
            sb.AppendLine("【" + Need(stat.Name, "能力値名") + "】" + (stat.Formula ?? "").Trim());

        if (enemy.Traits.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("【特性】");
            foreach (var trait in enemy.Traits) sb.AppendLine(TraitBodyText(trait));
        }
        if (enemy.Actions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("【行動】");
            foreach (EnemyActionRule action in enemy.Actions) sb.AppendLine("●" + ActionText(action));
        }

        foreach (SkillTextData skill in enemy.Skills)
        {
            sb.AppendLine();
            sb.AppendLine(SkillBodyText(skill));
        }

        WriteDrops(sb, enemy);
        if (!string.IsNullOrWhiteSpace(enemy.Flavor))
        {
            if (enemy.LootKind == EnemyLootKind.Unspecified && enemy.Skills.Count > 0 &&
                string.IsNullOrWhiteSpace(enemy.Skills[enemy.Skills.Count - 1].Skill.Flavor)) sb.AppendLine(Separator);
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

        while (index < lines.Length)
        {
            SkipEmpty(lines, ref index);
            if (index >= lines.Length) break;
            string line = SyntaxLine(lines[index]);
            if (StartsDropTable(lines, index)) { ParseDrops(enemy, lines, ref index); continue; }
            if (IsSeparator(line))
            {
                enemy.Flavor = string.Join("\n", lines.Skip(index + 1)).TrimEnd();
                break;
            }
            if (line.StartsWith("【行動】", StringComparison.Ordinal))
            {
                var actions = new List<string>();
                string first = line.Substring("【行動】".Length).Trim();
                if (first.Length > 0) actions.Add(first);
                index++;
                while (index < lines.Length && !SectionBoundary(lines, index)) actions.Add(lines[index++]);
                enemy.Actions.AddRange(ParseActions(actions));
                continue;
            }
            if (TrySkillBoundary(lines, index))
            {
                int start = index++;
                bool inFlavor = false;
                while (index < lines.Length)
                {
                    string candidate = SyntaxLine(lines[index]);
                    if (StartsDropTable(lines, index) || (TrySkillBoundary(lines, index) && !IsEmbeddedSummon(lines, start, index))) break;
                    if (IsSeparator(candidate))
                    {
                        if (inFlavor) break;
                        inFlavor = true;
                    }
                    index++;
                }
                string skillSource = NestedSkillText(lines, start, index);
                try
                {
                    var skill = SkillTextConverter.Parse("```\n" + skillSource + "\n```");
                    // Embedded trap rules often give the removal trigger after
                    // the counter. The writer emits declaration triggers first.
                    foreach (var summon in skill.Summons)
                        summon.Effects = summon.Effects.OrderBy(effect => effect.Type == EffectType.Declaration ? 0 : 1).ToList();
                    enemy.Skills.Add(skill);
                }
                catch (InvalidOperationException ex) { throw LineError(start, lines[start], "行動スキルを解析できません：" + ex.Message, ex); }
                continue;
            }

            Match traitHeader = Match(line, @"^【(?:特性|パッシブ効果)】(.*)$");
            if (traitHeader.Success || line.StartsWith("《", StringComparison.Ordinal))
            {
                var traitLines = new List<string>();
                traitLines.Add(traitHeader.Success ? traitHeader.Groups[1].Value : line);
                int start = index++;
                while (index < lines.Length && !SectionBoundary(lines, index)) traitLines.Add(lines[index++]);
                try { AddTraits(enemy, traitLines); }
                catch (InvalidOperationException ex) { throw LineError(start, lines[start], ex.Message, ex); }
                continue;
            }
            Match header = Match(line, @"^【([^】]+)】(.*)$");
            if (!header.Success) throw LineError(index, lines[index], "能力値は【名前】値の形式です。");
            string key = Need(header.Groups[1].Value, "能力値名");
            string value = header.Groups[2].Value.Trim();
            if (key == "危険度")
            {
                if (enemy.DangerLevel.Length != 0) throw LineError(index, lines[index], "危険度が重複しています。");
                enemy.DangerLevel = Need(value, key);
            }
            else enemy.Stats.Add(new EnemyStatDefinition { Name = key, Formula = value });
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
        if (enemy.Categories == null || enemy.Categories.Count == 0 || enemy.Stats == null || enemy.Actions == null || enemy.Skills == null || enemy.Traits == null || enemy.Drops == null)
            throw new InvalidOperationException("カテゴリー・能力値・行動・スキル・ドロップを初期化してください。");
        CategoryText(enemy.Categories);
        if (enemy.Stats.Any(x => x == null)) throw new InvalidOperationException("能力値にnullがあります。");
        string[] statNames = enemy.Stats.Select(x => Token(x.Name, "能力値名", "【】")).ToArray();
        if (statNames.Distinct(StringComparer.Ordinal).Count() != statNames.Length || statNames.Contains("危険度", StringComparer.Ordinal))
            throw new InvalidOperationException("能力値が重複しているか、予約名の「危険度」が使われています。");
        foreach (EnemyStatDefinition stat in enemy.Stats)
            if (!string.IsNullOrEmpty(stat.Formula)) Need(stat.Formula, stat.Name + "の値");
        if (enemy.Actions.Any(x => x == null)) throw new InvalidOperationException("行動がnullです。");
        foreach (var action in enemy.Actions) ActionText(action);
        if (enemy.Skills.Concat(enemy.Traits).Any(x => x == null || x.Skill == null)) throw new InvalidOperationException("特性または行動スキルがnullです。");
        string[] skillNames = enemy.Skills.Select(x => Token(x.Skill.Name, "行動スキル名", "《》")).ToArray();
        var skillNameSet = new HashSet<string>(skillNames, StringComparer.Ordinal);
        if (skillNameSet.Count != skillNames.Length) throw new InvalidOperationException("行動スキル名が重複しています。");

        foreach (SkillTextData skill in enemy.Skills.Concat(enemy.Traits))
        {
            if (skill.Summons == null) continue;
            foreach (SummonedEntityDefinition summon in skill.Summons.Where(x => x != null))
            {
                string summonName = Token(summon.Name, "召喚名", "《》");
                if (skillNameSet.Contains(summonName))
                    throw new InvalidOperationException("行動スキル名と召喚名は重複できません：" + summonName);
            }
        }
        ValidateDrops(enemy);
        return enemy;
    }

    private static string SkillBodyText(SkillTextData skill)
    {
        string[] lines = SkillTextConverter.Build(skill).Replace("\r\n", "\n").Split('\n');
        if (lines.Length < 3 || !lines[0].StartsWith("```", StringComparison.Ordinal) || lines[lines.Length - 1] != "```")
            throw new InvalidOperationException("行動スキルのコードフェンスが不正です。");
        return string.Join("\n", lines.Skip(1).Take(lines.Length - 2));
    }

    private static List<string> InputBlocks(string source) =>
        DataBlockText.ReadBlocks(DataBlockText.NormalizeBreaks(source), "エネミー", keepFences: false,
            openingPattern: @"^(?:\|\s*)?""?(?<ticks>`{3,})(?:[A-Za-z][A-Za-z0-9_.+-]*)?""?\s*\|?$");

    private static bool TryNameHeader(string line, out string name)
    {
        Match match = Match(line, @"^《([^《》]+)》$");
        name = match.Success ? match.Groups[1].Value.Trim() : "";
        return match.Success;
    }

    private static string NestedSkillText(string[] lines, int start, int end)
    {
        // A trap or pet may follow the parent skill's flavor in the source.
        // Place its structured fields before the shared skill flavor delimiter.
        int separator = Array.FindIndex(lines, start + 1, end - start - 1, line => IsSeparator(SyntaxLine(line)));
        if (separator >= 0)
        {
            for (int i = separator + 1; i < end - 1; i++)
                if (TryNameHeader(SyntaxLine(lines[i]), out _) && IsEmbeddedSummon(lines, start, i))
                    return string.Join("\n", lines.Skip(start).Take(separator - start)
                        .Concat(lines.Skip(i).Take(end - i)).Concat(lines.Skip(separator).Take(i - separator))).TrimEnd();
        }
        return string.Join("\n", lines, start, end - start).TrimEnd();
    }

    private static bool IsEmbeddedSummon(string[] lines, int start, int index)
    {
        int next = index + 1;
        SkipEmpty(lines, ref next);
        if (next >= lines.Length || !IsSummonCategory(SyntaxLine(lines[next]))) return false;
        string header = SyntaxLine(lines[index]);
        for (int i = start + 1; i < index; i++)
            if (lines[i].Contains(header) && (lines[i].Contains("召喚") || lines[i].Contains("設置"))) return true;
        return false;
    }

    private static bool IsSummonCategory(string line) => Match(line, @"^〈(?:[^〉]*[,、]\s*)?(?:罠|ペット)(?:\s*[,、〉])").Success;

    private static bool IsSeparator(string line) => Match(line, @"^―{8,}$").Success;

    private static bool SectionBoundary(string[] lines, int index)
    {
        string line = SyntaxLine(lines[index]);
        return IsSeparator(line) || StartsDropTable(lines, index) || TrySkillBoundary(lines, index) ||
            line.StartsWith("【行動】", StringComparison.Ordinal) || line.StartsWith("【特性】", StringComparison.Ordinal) ||
            line.StartsWith("【パッシブ効果】", StringComparison.Ordinal);
    }

    private static bool TrySkillBoundary(string[] lines, int index)
    {
        if (!TryNameHeader(SyntaxLine(lines[index]), out _)) return false;
        int next = index + 1;
        SkipEmpty(lines, ref next);
        if (next >= lines.Length) return false;
        string first = SyntaxLine(lines[next]);
        return SkillTextConverter.IsSkillBodyStart(first, next + 1 < lines.Length ? SyntaxLine(lines[next + 1]) : "");
    }

    public static IEnumerable<string> ReferenceWarnings(EnemyDefinition enemy)
    {
        var references = new HashSet<string>(enemy.Actions.SelectMany(ActionSkillNames), StringComparer.Ordinal);
        var definitions = new HashSet<string>(enemy.Skills.Select(x => x.Skill.Name), StringComparer.Ordinal);
        foreach (string name in references.Except(definitions).OrderBy(x => x, StringComparer.Ordinal))
            yield return "このエネミーに行動スキル定義がありません：《" + name + "》";
        foreach (var action in enemy.Actions)
            foreach (var item in new EnemyActionStep[] { action }.Concat(action.Branches).Where(step => step.UseItem))
                references.Add(item.SkillName);
        foreach (string name in definitions.Except(references).OrderBy(x => x, StringComparer.Ordinal))
            yield return "行動欄から直接参照されていないスキル定義です：《" + name + "》";
        foreach (var stat in enemy.Stats.Where(x => string.IsNullOrWhiteSpace(x.Formula)))
            yield return "能力値が未記入です：【" + stat.Name + "】";
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

    private static Match Match(string text, string pattern, RegexOptions options = RegexOptions.None) { return DataBlockText.GetRegex(pattern, options).Match(text); }
    private static MatchCollection Matches(string text, string pattern, RegexOptions options = RegexOptions.None) { return DataBlockText.GetRegex(pattern, options).Matches(text); }
    private static string Replace(string text, string pattern, string replacement, RegexOptions options = RegexOptions.None) { return DataBlockText.GetRegex(pattern, options).Replace(text, replacement); }
}
