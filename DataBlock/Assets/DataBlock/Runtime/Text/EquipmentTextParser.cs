using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class EquipmentTextConverter
{
    private static EquipmentTextData ParseOne(string source)
    {
        string[] lines = source.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
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
        bool foundEffects = false;
        while (index <= end)
        {
            SkipEmpty(lines, ref index, end);
            if (index > end) break;
            string line = SyntaxLine(lines[index]);
            Match header = M(line, @"^【([^】]+)】(.*)$");
            if (!header.Success) throw LineError(index, lines[index], "装備項目は【名前】値の形式です。");
            string key = Need(header.Groups[1].Value, "装備項目名");
            string body = header.Groups[2].Value.Trim();
            if (!headers.Add(key)) throw LineError(index, lines[index], key + "が重複しています。");
            try
            {
                switch (key)
                {
                    case "レアリティ":
                        equipment.Rarity = Need(body, "レアリティ");
                        break;
                    case "グレード":
                        if (!M(body, @"^☆+$").Success)
                            throw new InvalidOperationException("グレードは☆のみで指定してください。");
                        if (body.Length > MaximumEquipmentGrade)
                            throw new InvalidOperationException("グレードは" + MaximumEquipmentGrade +
                                "以下にしてください。");
                        equipment.Grade = body.Length;
                        break;
                    case "価値":
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
                    case "武器威力":
                        equipment.WeaponPowerFormula = Need(body, "武器威力");
                        break;
                    case "装備効果":
                        foundEffects = true;
                        if (body.Length > 0) ReadEquipmentEffectLine(equipment, body);
                        index++;
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
        foreach (string required in new[] { "レアリティ", "グレード", "価値", "装備部位", "サイズ", "耐久最大値", "装備効果" })
            if (!headers.Contains(required)) throw new InvalidOperationException("【" + required + "】がありません。");
        if (!foundEffects) throw new InvalidOperationException("【装備効果】がありません。");

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
            if (M(line, @"^―{8,}$").Success)
            {
                equipment.Flavor = string.Join("\n", lines.Skip(index + 1).Take(end - index)
                    .Select(x => x.TrimEnd())).TrimEnd();
                break;
            }

            if (line == "【グレード補正】")
            {
                if (skillsStarted)
                    throw LineError(index, original, "【グレード補正】は装備スキルより前に指定してください。");
                if (gradeAdjustmentsStarted)
                    throw LineError(index, original, "【グレード補正】が重複しています。");
                gradeAdjustmentsStarted = true;
                gradeAdjustmentStartCount = equipment.GradeAdjustments.Count;
                index++;
                continue;
            }

            Match skillHeader = M(line, @"^●\s*スキル[：:]\s*《([^《》]+)》$");
            if (skillHeader.Success)
            {
                skillsStarted = true;
                string skillName = Need(skillHeader.Groups[1].Value, "装備スキル名");
                int next = index + 1;
                while (next <= end)
                {
                    string candidate = SyntaxLine(lines[next]);
                    if (M(candidate, @"^―{8,}$").Success ||
                        M(candidate, @"^●\s*スキル[：:]\s*《([^《》]+)》$").Success) break;
                    next++;
                }
                string body = string.Join("\n", lines.Skip(index + 1).Take(next - index - 1));
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
                if (skillsStarted)
                    throw new InvalidOperationException("装備効果と状態は装備スキルより前に指定してください。");
                if (gradeAdjustmentsStarted)
                    ReadEquipmentGradeAdjustment(equipment, line);
                else if (!TryReadEquipmentState(equipment, line))
                    ReadEquipmentEffectLine(equipment, line);
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
        equipment.MaxDurability = Number(parts[0], 1, "耐久最大値");

        for (int i = 1; i < parts.Count; i++)
        {
            EquipmentEffectDefinition modifier;
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
        Match value = M(body, @"^([0-9]+)([^0-9\s].*)$");
        if (!value.Success) throw new InvalidOperationException("価値は432ジルダまたは取引不可の形式です。");
        equipment.Value = new EquipmentValueDefinition
        {
            Kind = EquipmentValueKind.Currency,
            Amount = Number(value.Groups[1].Value, 0, "価値"),
            Currency = Need(value.Groups[2].Value, "通貨名")
        };
    }

    private static void ReadEquipmentRequirements(EquipmentDefinition equipment, string body)
    {
        string normalized = body.Replace("、", ",");
        foreach (string part in Split(normalized, ","))
        {
            Match level = M(part, @"^([0-9]+)レベル以上$");
            if (!level.Success) throw new InvalidOperationException("未対応の装備条件です：" + part);
            equipment.EquipRequirements.Add(new EquipmentRequirementDefinition
            {
                Type = EquipmentRequirementType.MinimumLevel,
                Parameters = new List<string>
                {
                    Number(level.Groups[1].Value, 1, "必要レベル").ToString(CultureInfo.InvariantCulture)
                }
            });
        }
        if (equipment.EquipRequirements.Count == 0) throw new InvalidOperationException("装備条件が空です。");
    }

    private static void SkipEmpty(string[] lines, ref int index, int end)
    {
        while (index <= end && string.IsNullOrWhiteSpace(lines[index])) index++;
    }
}
