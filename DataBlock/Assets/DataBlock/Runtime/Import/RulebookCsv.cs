using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// Coordinates describe the source cell. They are not persistent skill catalog IDs.
public sealed class RulebookCell
{
    public int Row { get; }
    public int Column { get; }
    public string Name { get; }
    public object Data { get; }
    public string Error { get; }

    public string ExclusionReason { get; }

    internal RulebookCell(int row, int column, string name, object data, string error, string exclusionReason = null)
    {
        Row = row;
        Column = column;
        Name = name;
        Data = data;
        Error = error;
        ExclusionReason = exclusionReason;
    }

    public string BuildText()
    {
        if (Data is ItemTextData item) return ItemTextConverter.Build(item);
        if (Data is EnemyTextData enemy) return EnemyTextConverter.Build(enemy);
        if (Data is SkillTextData skill) return SkillTextConverter.Build(skill);
        if (Data is CharacterProfileData profile) return SkillTextConverter.BuildProfile(profile);
        if (Data is SiegeWeaponData siege) return SkillTextConverter.BuildSiegeWeapon(siege);
        if (Data is EquipmentTextData equipment) return EquipmentTextConverter.Build(equipment);
        throw new InvalidOperationException(Error ?? ExclusionReason);
    }
}

public static class RulebookCsv
{
    private static readonly HashSet<string> IncludedNames = new HashSet<string>(StringComparer.Ordinal)
    { "無機の身体", "砲撃マスタリー", "キャノン砲", "カルヴァリン砲", "考古学" };

    // Read one CSV record at a time. Embedded quotes, commas and newlines belong to the cell.
    // The editor requests a yield for skipped fields too, so a sparse sheet can be read in bounded batches.
    public static IEnumerable<RulebookCell> Read(TextReader reader, bool yieldSkippedFields = false,
        Action<RulebookCell> onExcluded = null)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        int row = 1, column = 1;
        var field = new StringBuilder();
        bool quoted = false, closed = false;
        if (reader.Peek() == '\uFEFF') reader.Read();
        while (true)
        {
            int ch = reader.Read();
            if (quoted)
            {
                if (ch < 0) throw new FormatException(row + "行" + column + "列の引用符が閉じていません。");
                if (ch != '"') field.Append((char)ch);
                else if (reader.Peek() == '"') { reader.Read(); field.Append('"'); }
                else { quoted = false; closed = true; }
                continue;
            }
            if (ch == ',' || ch == '\r' || ch == '\n' || ch < 0)
            {
                var cell = ParseCell(field.ToString(), row, column, onExcluded);
                if (cell != null || yieldSkippedFields) yield return cell;
                if (ch < 0) yield break;
                if (ch == ',') column++;
                else
                {
                    if (ch == '\r' && reader.Peek() == '\n') reader.Read();
                    row++;
                    column = 1;
                }
                field.Clear();
                closed = false;
            }
            else if (ch == '"' && field.Length == 0 && !closed) quoted = true;
            else if (closed || ch == '"') throw new FormatException(row + "行" + column + "列の引用符が不正です。");
            else field.Append((char)ch);
        }
    }

    private static RulebookCell ParseCell(string source, int row, int column, Action<RulebookCell> onExcluded)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        if (source.IndexOf('\n') < 0 && source.IndexOf('\r') < 0 && source.Contains("【レアリティ】"))
            source = EquipmentTextConverter.PrepareInput(source);
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(s => s.Trim()).SkipWhile(s => s.Length == 0 || DataBlockText.OpeningFence(DataBlockText.NormalizeFenceLine(s)).Success)
            .TakeWhile(s => !s.StartsWith("――――――――", StringComparison.Ordinal)).ToArray();
        if (lines.Length == 0) return null;
        bool prize = lines[0].StartsWith("-", StringComparison.Ordinal) && lines.Any(line => line.StartsWith("【景品ロール】", StringComparison.Ordinal));
        var title = DataBlockText.GetRegex(@"^《([^《》]+)》(☆[1-9][0-9]*)?$").Match(lines[0]);
        RulebookCell Exclude(string label, string reason)
        {
            onExcluded?.Invoke(new RulebookCell(row, column, label, null, null, reason));
            return null;
        }
        if (!title.Success && !prize)
        {
            if (lines[0].StartsWith("《", StringComparison.Ordinal) && lines.Skip(1).Any(s => s.StartsWith("【")))
                return Exclude(lines[0], "先頭の《名称》が空欄、または形式が不正です。");
            return null;
        }
        string name = prize ? lines[0].Trim('-').Trim() : title.Groups[1].Value;
        var body = lines.Skip(1).Where(s => s.Length > 0);
        string categories = body.FirstOrDefault() ?? "";
        bool hasCategories = categories.StartsWith("〈", StringComparison.Ordinal);
        var metadata = body.Skip(1).TakeWhile(s => s.StartsWith("【", StringComparison.Ordinal) &&
            !s.Contains("効果】") && !s.StartsWith("【行動】") && !s.StartsWith("【特性】") && !s.StartsWith("【説明】"));
        bool enemy = hasCategories && metadata.Any(s => s.StartsWith("【危険度】", StringComparison.Ordinal));
        string filterText = enemy ? categories : source;
        if ((filterText.Contains("秘匿") || filterText.Contains("兵器")) && !IncludedNames.Contains(name))
            return Exclude(name, "「秘匿」または「兵器」の除外条件に該当します。");
        bool profile = lines.Any(s => s.StartsWith("【種族能力値ボーナス】") || s.StartsWith("【クラス能力値】")) ||
                       categories == "〈種族〉" || categories.StartsWith("〈種族,");
        bool siege = categories == "〈兵器〉" || categories.StartsWith("〈兵器,");
        bool equipment = hasCategories && (lines.Any(line => line.StartsWith("【装備部位】") || line.StartsWith("【装備効果】") || line.StartsWith("【武器威力】")) ||
            DataBlockText.GetRegex(@"(?:^〈|[,、]\s*)(?:装備アイテム|投擲武器|ファミリアカード)(?=[,、〉])").IsMatch(categories));
        bool itemRecord = hasCategories && lines.Any(line => line.StartsWith("【レアリティ】", StringComparison.Ordinal));
        if (!equipment && !itemRecord && title.Groups[2].Success)
            return Exclude(name, "グレード指定に対応する装備またはアイテムの項目がありません。");
        if (!prize && !enemy && !equipment && !itemRecord)
        {
            string reason = SkillDraftReason(lines, profile || siege);
            if (reason != null) return Exclude(name, reason);
        }
        try
        {
            object data;
            if (prize) data = SkillTextConverter.Parse(source);
            else if (enemy) data = EnemyTextConverter.Parse(source);
            else if (profile) data = SkillTextConverter.ParseProfile(source);
            else if (siege) data = SkillTextConverter.ParseSiegeWeapon(source);
            else if (equipment)
            {
                if (lines.Any(DataBlockText.GetRegex(@"^(?:【[^】]+】\s*)?[?？]{3,}[。.]?$").IsMatch))
                    return Exclude(name, "未確定を示す疑問符が装備本文に残っています。");
                var item = EquipmentTextConverter.Parse(source, csvCell: true);
                var definition = item.Equipment;
                bool noEffect = string.IsNullOrWhiteSpace(definition.Flavor) && string.IsNullOrEmpty(definition.WeaponPowerFormula) &&
                    definition.WeaponModes.Count == 0 && definition.EffectGroups.Count == 0 && definition.Skills.Count == 0 &&
                    definition.States.Count == 0 && definition.GradeAdjustments.Count == 0;
                if (noEffect && lines.Any(s => s == "【装備効果】" || s == "【設置効果】" || s == "【パッシブ効果】"))
                    return Exclude(name, "装備の効果欄が空欄です。");
                if (noEffect && definition.EquipSlots.Count == 0 && definition.EquipRequirements.Count == 0 &&
                    definition.Value.Kind == EquipmentValueKind.Unspecified && definition.MaxDurability == 0 &&
                    definition.RequiredSpace == 0 && definition.Grade == 0 && string.IsNullOrEmpty(definition.SpecialGrade))
                    return Exclude(name, "装備の基本見出しだけで、装備部位・価値・効果などの記載がありません。");
                data = item;
            }
            else if (itemRecord)
            {
                Action<string, string> skipped = null;
                if (onExcluded != null) skipped = (choice, reason) => Exclude(name + " / " + choice, reason);
                data = ItemTextConverter.Parse(source, skipped);
            }
            else data = SkillTextConverter.Parse(source);
            return new RulebookCell(row, column, name, data, null);
        }
        catch (Exception e) when (e is InvalidOperationException || e is FormatException || e is RegexMatchTimeoutException || e is OverflowException)
        {
            return new RulebookCell(row, column, name, null, e.Message);
        }
    }

    private static string SkillDraftReason(string[] lines, bool hasDefinition)
    {
        if (!hasDefinition && !lines.Any(s => s.StartsWith("【") && (s.IndexOf("効果】", StringComparison.Ordinal) >= 0 || s.StartsWith("【説明】"))))
            return "スキルの効果または説明が未記入です。";
        // An unfinished row may have a title and valid headings. A missing target,
        // an empty element branch or a dangling conditional still makes it a draft.
        if (lines.Any(s => s == "この" || (s.StartsWith("【発動ロール】") && s.EndsWith("目標値")) ||
            (s.StartsWith("- ") && s.TrimEnd('：', ':').Length == 3 && "火水風電冷土光闇".Contains(s.Substring(2, 1))))) return "目標値・属性分岐・効果の記述が未完成です。";
        if (lines.Any(s => s == "【説明】下記の効果を一つ選んで実行する。") && !lines.Any(s => s.StartsWith("●"))) return "選択式の説明に対応する選択肢がありません。";
        string last = lines.LastOrDefault(s => s.Length > 0 && !s.StartsWith("```"));
        if (last != null && (last.EndsWith("を受けた時") || last.EndsWith("を受けた時、"))) return "発動条件に続く効果が未記入です。";
        return null;
    }
}
