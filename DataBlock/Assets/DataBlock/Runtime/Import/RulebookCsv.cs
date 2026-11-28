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

    internal RulebookCell(int row, int column, string name, object data, string error)
    {
        Row = row;
        Column = column;
        Name = name;
        Data = data;
        Error = error;
    }

    public string BuildText()
    {
        if (Data is EnemyTextData enemy) return EnemyTextConverter.Build(enemy);
        if (Data is SkillTextData skill) return SkillTextConverter.Build(skill);
        if (Data is CharacterProfileData profile) return SkillTextConverter.BuildProfile(profile);
        if (Data is SiegeWeaponData siege) return SkillTextConverter.BuildSiegeWeapon(siege);
        if (Data is EquipmentTextData equipment) return EquipmentTextConverter.Build(equipment);
        throw new InvalidOperationException(Error);
    }
}

public static class RulebookCsv
{
    private static readonly HashSet<string> IncludedNames = new HashSet<string>(StringComparer.Ordinal)
    { "無機の身体", "砲撃マスタリー", "キャノン砲", "カルヴァリン砲", "考古学" };

    // Read one CSV record at a time. Embedded quotes, commas and newlines belong to the cell.
    // The editor requests a yield for skipped fields too, so a sparse sheet can be read in bounded batches.
    public static IEnumerable<RulebookCell> Read(TextReader reader, bool yieldSkippedFields = false)
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
                var cell = ParseCell(field.ToString(), row, column);
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

    private static RulebookCell ParseCell(string source, int row, int column)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        if (source.IndexOf('\n') < 0 && source.IndexOf('\r') < 0 && source.Contains("【レアリティ】"))
            source = EquipmentTextConverter.PrepareInput(source);
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(s => s.Trim()).SkipWhile(s => s.Length == 0 || DataBlockText.OpeningFence(DataBlockText.NormalizeFenceLine(s)).Success)
            .TakeWhile(s => !s.StartsWith("――――――――", StringComparison.Ordinal)).ToArray();
        if (lines.Length == 0 || !lines[0].StartsWith("《", StringComparison.Ordinal)) return null;
        var title = DataBlockText.GetRegex(@"^《([^《》]+)》(☆[1-9][0-9]*)?$").Match(lines[0]);
        if (!title.Success) return null;
        string name = title.Groups[1].Value;
        bool enemy = lines.Skip(1).FirstOrDefault(s => s.StartsWith("【", StringComparison.Ordinal))
            ?.StartsWith("【危険度】", StringComparison.Ordinal) == true;
        string filterText = enemy ? lines.Skip(1).FirstOrDefault(s => s.StartsWith("〈", StringComparison.Ordinal)) ?? "" : source;
        if ((filterText.Contains("秘匿") || filterText.Contains("兵器")) && !IncludedNames.Contains(name)) return null;
        bool profile = lines.Any(s => s.StartsWith("【種族能力値ボーナス】") || s.StartsWith("【クラス能力値】")) ||
                       lines.Skip(1).Take(1).Any(s => s == "〈種族〉" || s.StartsWith("〈種族,"));
        bool siege = lines.Skip(1).Take(1).Any(s => s == "〈兵器〉" || s.StartsWith("〈兵器,"));
        bool equipment = !siege && !profile && lines.Skip(1).FirstOrDefault()?.StartsWith("〈", StringComparison.Ordinal) == true
            && (lines.Any(line => line.StartsWith("【装備部位】") || line.StartsWith("【装備効果】")) ||
                (lines.Any(line => line.StartsWith("【レアリティ】")) && lines.Any(line => line.StartsWith("【サイズ】"))));
        if (!equipment && title.Groups[2].Success) return null;
        if (!enemy && !equipment && IsSkillDraft(lines, profile || siege)) return null;
        try
        {
            object data;
            if (enemy) data = EnemyTextConverter.Parse(source);
            else if (profile) data = SkillTextConverter.ParseProfile(source);
            else if (siege) data = SkillTextConverter.ParseSiegeWeapon(source);
            else if (equipment)
            {
                if (lines.Any(DataBlockText.GetRegex(@"^(?:【[^】]+】\s*)?[?？]{3,}[。.]?$").IsMatch)) return null;
                var item = EquipmentTextConverter.Parse(source, csvCell: true);
                var definition = item.Equipment;
                if (string.IsNullOrWhiteSpace(definition.Flavor) && string.IsNullOrEmpty(definition.WeaponPowerFormula) &&
                    definition.WeaponModes.Count == 0 && definition.EffectGroups.Count == 0 && definition.Skills.Count == 0 &&
                    definition.States.Count == 0 && definition.GradeAdjustments.Count == 0) return null;
                data = item;
            }
            else data = SkillTextConverter.Parse(source);
            return new RulebookCell(row, column, name, data, null);
        }
        catch (Exception e) when (e is InvalidOperationException || e is FormatException || e is RegexMatchTimeoutException || e is OverflowException)
        {
            return new RulebookCell(row, column, name, null, e.Message);
        }
    }

    private static bool IsSkillDraft(string[] lines, bool hasDefinition)
    {
        if (!hasDefinition && !lines.Any(s => s.StartsWith("【") && (s.IndexOf("効果】", StringComparison.Ordinal) >= 0 || s.StartsWith("【説明】")))) return true;
        // An unfinished row may have a title and valid headings. A missing target,
        // an empty element branch or a dangling conditional still makes it a draft.
        if (lines.Any(s => s == "この" || (s.StartsWith("【発動ロール】") && s.EndsWith("目標値")) ||
            (s.StartsWith("- ") && s.TrimEnd('：', ':').Length == 3 && "火水風電冷土光闇".Contains(s.Substring(2, 1))))) return true;
        if (lines.Any(s => s == "【説明】下記の効果を一つ選んで実行する。") && !lines.Any(s => s.StartsWith("●"))) return true;
        string last = lines.LastOrDefault(s => s.Length > 0 && !s.StartsWith("```"));
        if (last != null && (last.EndsWith("を受けた時") || last.EndsWith("を受けた時、"))) return true;
        return false;
    }
}
