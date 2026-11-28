using static CommonEffectTextCodec;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class EquipmentTextConverter
{
    private static EquipmentTextData ParseOne(string source)
    {
        string[] lines = EquipmentLines(source);
        int start = 0;
        int end = lines.Length - 1;
        while (start <= end && string.IsNullOrWhiteSpace(lines[start])) start++;
        while (end >= start && string.IsNullOrWhiteSpace(lines[end])) end--;
        if (start <= end && lines[start].Trim().StartsWith("```", StringComparison.Ordinal))
        {
            if (start == end || lines[end].Trim() != "```")
                throw new InvalidOperationException("末尾のコードフェンスがありません。");
            start++;
            end--;
        }

        int index = start;
        SkipEmpty(lines, ref index, end);
        if (index > end) throw new InvalidOperationException("装備テキストが空です。");
        Match name = M(SyntaxLine(lines[index]), @"^《([^《》]+)》(?:☆([1-9][0-9]*))?$");
        if (!name.Success) throw LineError(index, lines[index], "先頭に《装備名》を指定してください。");

        var data = new EquipmentTextData();
        EquipmentDefinition equipment = data.Equipment;
        equipment.Name = Need(name.Groups[1].Value, "装備名");
        int requestedGrade = name.Groups[2].Success
            ? Number(name.Groups[2].Value, 1, "装備名のグレード")
            : 0;
        index++;

        SkipEmpty(lines, ref index, end);
        if (index > end) throw new InvalidOperationException("装備カテゴリーがありません。");
        try
        {
            equipment.Categories = Categories(SyntaxLine(lines[index]));
        }
        catch (InvalidOperationException exception)
        {
            throw LineError(index, lines[index], exception.Message, exception);
        }
        index++;

        var headers = new HashSet<string>(StringComparer.Ordinal);
        while (index <= end)
        {
            SkipEmpty(lines, ref index, end);
            if (index > end) break;
            string line = SyntaxLine(lines[index]);
            if (IsFlavorSeparator(line) || IsSkillHeading(lines, index, end) || IsActionHeader(line) || line == "【グレード補正】") break;
            Match header = M(line, @"^【([^】]+)】(.*)$");
            if (!header.Success) throw LineError(index, lines[index], "装備項目は【名前】値の形式です。");
            string key = Need(header.Groups[1].Value, "装備項目名");
            string body = header.Groups[2].Value.Trim();
            if (!headers.Add(key))
            {
                if ((key == "サイズ" && body == equipment.Size) ||
                    (key == "耐久最大値" && body == equipment.MaxDurability.ToString(CultureInfo.InvariantCulture)))
                {
                    index++;
                    continue;
                }
                throw LineError(index, lines[index], key + "が重複しています。");
            }
            try
            {
                switch (key)
                {
                    case "レアリティ":
                        equipment.Rarity = Need(body, "レアリティ");
                        break;
                    case "グレード":
                        if (!M(body, @"^(?:☆+|★+☆*)$").Success || body.Length > MaximumEquipmentGrade)
                            throw new InvalidOperationException("グレードの星表記が不正です：" + body);
                        if (body[0] == '★') equipment.SpecialGrade = body;
                        else equipment.Grade = body.Length;
                        break;
                    case "価値":
                        if (body.Length == 0) throw new InvalidOperationException("【価値】の値が空欄です。");
                        ReadEquipmentValue(equipment, body);
                        break;
                    case "装備部位":
                        equipment.EquipSlots = Split(body.Replace("、", ","), ",");
                        if (equipment.EquipSlots.Count == 0)
                            throw new InvalidOperationException("装備部位が空です。");
                        break;
                    case "装備条件":
                        ReadEquipmentRequirements(equipment, body);
                        break;
                    case "サイズ":
                        equipment.Size = Need(body, "サイズ");
                        break;
                    case "耐久最大値":
                        ReadEquipmentMaxDurability(equipment, body);
                        break;
                    case "武器威力補正":
                        equipment.WeaponPowerBonus = SignedInteger(body, "武器威力補正");
                        break;
                    case "武器威力":
                        if (body.Length == 0)
                        {
                            int next = index + 1;
                            SkipEmpty(lines, ref next, end);
                            if (next <= end && M(SyntaxLine(lines[next]), @"^(?:《[^》]+》[：:]\s*)?\[").Success)
                            {
                                index = next;
                                body = SyntaxLine(lines[next]);
                            }
                        }
                        Match namedPower = M(body, @"^《([^《》]+)》[：:]\s*(.+)$");
                        if (namedPower.Success)
                        {
                            equipment.WeaponPowerName = namedPower.Groups[1].Value;
                            body = namedPower.Groups[2].Value;
                        }
                        equipment.WeaponPowerFormula = Need(body, "武器威力");
                        break;
                    case "必要敷地":
                        equipment.RequiredSpace = Number(body, 1, "必要敷地");
                        break;
                    case "パッシブ効果":
                    case "設置効果":
                    case "装備効果":
                        goto Effects;
                    default:
                        throw new InvalidOperationException("未対応の装備項目です：" + key);
                }
            }
            catch (InvalidOperationException exception)
            {
                throw LineError(index, lines[index], exception.Message, exception);
            }
            index++;
        }

    Effects:
        foreach (string required in new[] { "レアリティ", "サイズ" })
            if (!headers.Contains(required)) throw new InvalidOperationException("【" + required + "】がありません。");

        EquipmentEffectSection section = EquipmentEffectSection.Equipped;
        bool skillsStarted = false;
        bool gradeAdjustmentsStarted = false;
        int gradeAdjustmentStartCount = 0;
        while (index <= end)
        {
            string original = lines[index];
            string line = SyntaxLine(original);
            if (line.Length == 0)
            {
                index++;
                continue;
            }
            Match sectionHeader = M(line, @"^【(パッシブ効果|設置効果|装備効果)】(.*)$");
            if (sectionHeader.Success)
            {
                if (skillsStarted || gradeAdjustmentsStarted)
                    throw LineError(index, original, "装備の効果欄は装備スキル・グレード補正より前に指定してください。");
                string key = sectionHeader.Groups[1].Value;
                section = key == "パッシブ効果" ? EquipmentEffectSection.Passive :
                    key == "設置効果" ? EquipmentEffectSection.Installed : EquipmentEffectSection.Equipped;
                line = sectionHeader.Groups[2].Value.Trim();
                if (line.Length == 0) { index++; continue; }
                lines[index] = line;
            }
            if (IsFlavorSeparator(line))
            {
                equipment.Flavor = string.Join("\n", lines.Skip(index + 1).Take(end - index)
                    .Select(x => x.TrimEnd())).TrimEnd();
                break;
            }

            if (line == "【説明】武器を参照する時、以下から一つ選ぶ。")
            {
                index++;
                ReadWeaponModes(equipment, lines, ref index, end);
                continue;
            }

            if (line == "【グレード補正】")
            {
                if (gradeAdjustmentsStarted)
                    throw LineError(index, original, "【グレード補正】が重複しています。");
                gradeAdjustmentsStarted = true;
                gradeAdjustmentStartCount = equipment.GradeAdjustments.Count;
                index++;
                continue;
            }

            bool namedSkill = IsSkillHeading(lines, index, end);
            int followingLine = index + 1;
            SkipEmpty(lines, ref followingLine, end);
            bool taggedAction = M(line, @"^(?:〈[^〉]+〉)+$").Success && followingLine <= end &&
                IsActionHeader(SyntaxLine(lines[followingLine]));
            if (namedSkill || (!skillsStarted && (IsActionHeader(line) || taggedAction)))
            {
                skillsStarted = true;
                string skillName = namedSkill ? SkillHeadingName(line) : equipment.Name;
                int first = index + (namedSkill ? 1 : 0);
                int next = first + 1;
                while (next <= end)
                {
                    string candidate = SyntaxLine(lines[next]);
                    if (IsFlavorSeparator(candidate) || candidate == "【グレード補正】" ||
                        M(candidate, @"^●\s*スキル[：:]\s*《[^《》]+》$").Success) break;
                    next++;
                }
                string body = string.Join("\n", lines.Skip(first).Take(next - first)
                    .Select(x => RegexReplace(x, @"^○(?=\S)", "●")));
                string skillSource = "```\n《" + skillName + "》\n" + body.TrimEnd() + "\n```";
                try
                {
                    equipment.Skills.Add(SkillTextConverter.Parse(skillSource));
                }
                catch (InvalidOperationException exception)
                {
                    throw LineError(index, original, "装備スキルを解析できません：" + exception.Message, exception);
                }
                index = next;
                continue;
            }

            try
            {
                if (skillsStarted && !gradeAdjustmentsStarted)
                    throw new InvalidOperationException("装備効果と状態は装備スキルより前に指定してください。");
                if (gradeAdjustmentsStarted)
                    ReadEquipmentGradeAdjustment(equipment, line);
                else
                {
                    int firstGroup = equipment.EffectGroups.Count;
                    if (!TryReadEquipmentState(equipment, lines, ref index, end) &&
                        !TryReadNamedEffectBlock(equipment, lines, ref index, end, line))
                        equipment.EffectGroups.Add(ReadEquipmentEffectParagraph(equipment, lines, ref index, end, line));
                    for (int i = firstGroup; i < equipment.EffectGroups.Count; i++)
                        equipment.EffectGroups[i].Section = section;
                }
                index++;
            }
            catch (InvalidOperationException exception)
            {
                throw LineError(index, original, exception.Message, exception);
            }
        }
        if (gradeAdjustmentsStarted && equipment.GradeAdjustments.Count == gradeAdjustmentStartCount)
            throw new InvalidOperationException("【グレード補正】に補正行がありません。");
        if (requestedGrade > 0)
        {
            if (requestedGrade == equipment.Grade)
            {
                if (equipment.GradeAdjustments.Count > 0)
                    throw new InvalidOperationException("基準グレードの装備名には☆" +
                        requestedGrade + "を付けないでください。");
                equipment.VariantGrade = requestedGrade;
            }
            else
            {
                return EquipmentGradeResolver.Resolve(data, requestedGrade);
            }
        }
        return data;
    }

    private static void ReadEquipmentMaxDurability(EquipmentDefinition equipment, string body)
    {
        string normalized = body.Replace("、", ",").Trim();
        if (normalized.EndsWith(",", StringComparison.Ordinal))
            throw new InvalidOperationException("耐久最大値の末尾に空の補正があります。");

        List<string> parts = Split(normalized, ",");
        if (parts.Count == 0) throw new InvalidOperationException("耐久最大値が空です。");
        equipment.MaxDurability = parts[0] == "∞" ? -1 : Number(parts[0], 1, "耐久最大値");

        for (int i = 1; i < parts.Count; i++)
        {
            EffectContent modifier;
            if (!TryReadStatModifier(parts[i], out modifier))
                throw new InvalidOperationException("耐久最大値の後には能力値補正だけを指定できます：" + parts[i]);
            var group = new EquipmentEffectGroup();
            group.Effects.Add(modifier);
            equipment.EffectGroups.Add(group);
        }
    }

    private static void ReadEquipmentValue(EquipmentDefinition equipment, string body)
    {
        if (body == "取引不可")
        {
            equipment.Value = new EquipmentValueDefinition { Kind = EquipmentValueKind.Untradeable };
            return;
        }
        Match value = M(body, @"^(?:(?<quantity>[0-9]+)個で)?(?<amount>[0-9]{1,3}(?:,[0-9]{3})+|[0-9]+)(?<currency>[^\s0-9,、]*)$");
        if (!value.Success) throw new InvalidOperationException("価値は432ジルダまたは取引不可の形式です。");
        equipment.Value = new EquipmentValueDefinition
        {
            Kind = EquipmentValueKind.Currency,
            Amount = Number(value.Groups["amount"].Value.Replace(",", ""), 0, "価値"),
            Quantity = value.Groups["quantity"].Success ? Number(value.Groups["quantity"].Value, 1, "取引数量") : 1,
            Currency = value.Groups["currency"].Value
        };
    }

    private static void ReadEquipmentRequirements(EquipmentDefinition equipment, string body)
    {
        // A trailing comparator applies to the preceding comma-separated stats too:
        // 生命, 器用, 敏捷100以上 and 幸運100, 冷属性200以上.
        var parts = Split(body.Replace("、", ",").TrimEnd(','), ",");
        string sharedLimit = null;
        string sharedValue = null;
        var requirements = new List<EquipmentRequirementDefinition>();
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            string part = parts[i];
            Match level = M(part, @"^(?:([0-9]+)レベル以上|レベル([0-9]+)以上)$");
            Match learned = M(part, @"^(称号)?《([^》]+)》の習得$");
            var requirement = new EquipmentRequirementDefinition();
            if (level.Success || learned.Success)
            {
                sharedLimit = sharedValue = null;
                if (level.Success)
                {
                    requirement.Type = EquipmentRequirementType.MinimumLevel;
                    requirement.Parameters.Add(level.Groups[1].Value + level.Groups[2].Value);
                }
                else
                {
                    requirement.Type = learned.Groups[1].Success ? EquipmentRequirementType.LearnedTitle : EquipmentRequirementType.LearnedSkill;
                    requirement.Parameters.Add(learned.Groups[2].Value);
                }
            }
            else
            {
                Match stat = M(part, @"^([一-龯ぁ-んァ-ヶA-Za-z]+?)([0-9]+)?(以上|以下)?$");
                if (!stat.Success) throw new InvalidOperationException("未対応の装備条件です：" + part);
                if (stat.Groups[3].Success) sharedLimit = stat.Groups[3].Value;
                if (stat.Groups[2].Success) sharedValue = stat.Groups[2].Value;
                if (sharedLimit == null || sharedValue == null)
                    throw new InvalidOperationException("装備条件の能力値に閾値がありません：" + part);
                requirement.Type = sharedLimit == "以上" ? EquipmentRequirementType.MinimumStat : EquipmentRequirementType.MaximumStat;
                requirement.Parameters.Add(stat.Groups[1].Value);
                requirement.Parameters.Add(sharedValue);
            }
            requirements.Add(requirement);
        }
        if (requirements.Count == 0) throw new InvalidOperationException("装備条件が空です。");
        requirements.Reverse();
        equipment.EquipRequirements.AddRange(requirements);
    }

    private static void ReadWeaponModes(EquipmentDefinition equipment, string[] lines, ref int index, int end)
    {
        EquipmentWeaponMode mode = null;
        int count = equipment.WeaponModes.Count;
        while (index <= end)
        {
            string line = SyntaxLine(lines[index]);
            if (IsFlavorSeparator(line) || line == "【グレード補正】" || IsSkillHeading(lines, index, end)) break;
            if (line.StartsWith("●", StringComparison.Ordinal))
            {
                mode = new EquipmentWeaponMode { Name = Need(line.Substring(1), "武器の選択肢") };
                equipment.WeaponModes.Add(mode);
            }
            else if (line.Length > 0)
            {
                if (mode == null) throw LineError(index, line, "武器の選択肢に名前がありません。");
                if (line.StartsWith("【武器威力】", StringComparison.Ordinal))
                {
                    if (mode.WeaponPowerFormula.Length > 0) throw LineError(index, line, "武器威力が重複しています。");
                    mode.WeaponPowerFormula = Need(line.Substring("【武器威力】".Length), "選択した武器の威力");
                }
                else
                {
                    mode.EffectGroups.Add(ReadEquipmentEffectParagraph(equipment, lines, ref index, end, line, mode.Name));
                }
            }
            index++;
        }
        if (equipment.WeaponModes.Count - count < 2) throw new InvalidOperationException("武器の選択肢を2つ以上指定してください。");
    }

    private static bool IsFlavorSeparator(string line) => M(line, @"^―{8,}[。.]?$").Success;
    private static bool IsActionHeader(string line) => M(line, @"^【(?:消費リソース|宣言条件|発動ロール|アクティブ効果|カウンター効果|ロールプレイ効果)】").Success;
    private static string SkillHeadingName(string line)
    {
        Match explicitName = M(line, @"^●\s*スキル[：:]\s*《([^《》]+)》$");
        return explicitName.Success ? explicitName.Groups[1].Value : line.Substring(1).Trim();
    }

    private static bool IsSkillHeading(string[] lines, int index, int end)
    {
        string line = SyntaxLine(lines[index]);
        if (M(line, @"^●\s*スキル[：:]\s*《([^《》]+)》$").Success) return true;
        if (!M(line, @"^●[^●《【：:、。]+$").Success) return false;
        int next = index + 1;
        SkipEmpty(lines, ref next, end);
        return next <= end && IsActionHeader(SyntaxLine(lines[next]));
    }

    private static bool TryReadNamedEffectBlock(EquipmentDefinition equipment, string[] lines, ref int index, int end, string line)
    {
        Match name = M(Unbullet(line), @"^《([^《》]+)》[：:]$");
        if (!name.Success) return false;
        var group = new EquipmentEffectGroup { Name = name.Groups[1].Value };
        while (index < end)
        {
            string next = SyntaxLine(lines[index + 1]);
            if (next.Length == 0 || IsFlavorSeparator(next) || next.StartsWith("【", StringComparison.Ordinal) ||
                IsSkillHeading(lines, index + 1, end) ||
                M(Unbullet(next), @"^《[^》]+》[：:]|^.{1,80}効果〈[^〉]+〉[：:]").Success) break;
            index++;
            group.Effects.AddRange(ReadEquipmentEffectParagraph(equipment, lines, ref index, end, Unbullet(next)).Effects);
        }
        if (group.Effects.Count == 0) throw new InvalidOperationException("名前付き効果の本文がありません：" + group.Name);
        equipment.EffectGroups.Add(group);
        return true;
    }

    private static string JoinEffectLines(string[] lines, ref int index, int end, string text)
    {
        while (index < end)
        {
            bool named = M(Unbullet(text), @"^《[^《》]+》[：:]$").Success;
            string next = SyntaxLine(lines[index + 1]);
            bool trigger = M(text, @"(?:時|とき|際|場合|なら)[。.]$").Success;
            bool continued = M(text, @"(?:[,、]|によって|に応じて|して|することで|する事で)$").Success ||
                M(next, @"^によ(?:る|って)").Success ||
                (M(text, @"戦闘中[0-9]+度だけ$").Success && next.StartsWith("《", StringComparison.Ordinal)) ||
                (text.EndsWith("）", StringComparison.Ordinal) && M(next, @"^\[[^\]]+\]による").Success);
            if (!named && !trigger && !continued) break;
            if (next.Length == 0 || IsFlavorSeparator(next) || next.StartsWith("【") ||
                M(next, @"^(?:[・●⚫]\s*)?《[^》]+》[：:]|^[^、。]*効果〈[^〉]+〉[：:]").Success ||
                (!named && M(next, @"^[・●⚫-]").Success)) break;
            if (trigger) text = text.TrimEnd('。', '.') + "、";
            text += named ? Unbullet(next) : next;
            index++;
        }
        return text;
    }

    private static void SkipEmpty(string[] lines, ref int index, int end)
    {
        while (index <= end && string.IsNullOrWhiteSpace(lines[index])) index++;
    }
}
