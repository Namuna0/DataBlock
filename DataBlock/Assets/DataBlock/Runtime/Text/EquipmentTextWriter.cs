using static CommonEffectTextCodec;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public static partial class EquipmentTextConverter
{
    public static string Build(EquipmentTextData data)
    {
        EquipmentDefinition equipment = Validate(data);
        var builder = new StringBuilder();
        builder.AppendLine("```");
        string gradeSuffix = equipment.VariantGrade > 0
            ? "☆" + equipment.VariantGrade.ToString(CultureInfo.InvariantCulture)
            : "";
        builder.AppendLine("《" + Need(equipment.Name, "装備名") + "》" + gradeSuffix);
        builder.AppendLine(CategoryText(equipment.Categories));
        builder.AppendLine("【レアリティ】" + Need(equipment.Rarity, "レアリティ"));
        if (equipment.Grade > 0 || !string.IsNullOrEmpty(equipment.SpecialGrade))
            builder.AppendLine("【グレード】" + (equipment.Grade > 0 ? new string('☆', equipment.Grade) : equipment.SpecialGrade));
        if (equipment.Value.Kind != EquipmentValueKind.Unspecified)
            builder.AppendLine("【価値】" + EquipmentValueText(equipment.Value));
        if (equipment.EquipSlots.Count > 0)
            builder.AppendLine("【装備部位】" + string.Join(", ", equipment.EquipSlots));
        if (equipment.EquipRequirements.Count > 0)
            builder.AppendLine("【装備条件】" + EquipmentRequirementsText(equipment.EquipRequirements));
        builder.AppendLine("【サイズ】" + Need(equipment.Size, "サイズ"));
        if (equipment.MaxDurability != 0)
            builder.AppendLine("【耐久最大値】" + (equipment.MaxDurability == -1 ? "∞" : equipment.MaxDurability.ToString(CultureInfo.InvariantCulture)));
        if (equipment.RequiredSpace > 0)
            builder.AppendLine("【必要敷地】" + equipment.RequiredSpace.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(equipment.WeaponPowerFormula))
        {
            string name = string.IsNullOrEmpty(equipment.WeaponPowerName) ? "" : "《" + equipment.WeaponPowerName + "》：";
            builder.AppendLine("【武器威力】" + name + Need(equipment.WeaponPowerFormula, "武器威力"));
        }
        if (equipment.WeaponPowerBonus != 0)
            builder.AppendLine("【武器威力補正】" + SignedIntegerText(equipment.WeaponPowerBonus));
        EquipmentEffectSection? section = null;
        foreach (EquipmentEffectGroup group in equipment.EffectGroups)
        {
            if (section != group.Section)
            {
                section = group.Section;
                builder.AppendLine(section == EquipmentEffectSection.Passive ? "【パッシブ効果】" :
                    section == EquipmentEffectSection.Installed ? "【設置効果】" : "【装備効果】");
            }
            builder.AppendLine("・" + EquipmentEffectGroupText(group));
        }
        if (section == null && equipment.States.Count > 0) builder.AppendLine("【装備効果】");
        foreach (StateDefinition state in equipment.States)
            builder.AppendLine("・" + EquipmentStateText(state));

        if (equipment.WeaponModes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("【説明】武器を参照する時、以下から一つ選ぶ。");
            foreach (var mode in equipment.WeaponModes)
            {
                builder.AppendLine("●" + mode.Name);
                builder.AppendLine("【武器威力】" + mode.WeaponPowerFormula);
                foreach (var group in mode.EffectGroups)
                    builder.AppendLine("・" + EquipmentEffectGroupText(group));
            }
        }

        if (equipment.GradeAdjustments.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("【グレード補正】");
            foreach (EquipmentGradeAdjustmentDefinition adjustment in
                equipment.GradeAdjustments.OrderBy(x => x.Grade))
                builder.AppendLine("- " + EquipmentGradeAdjustmentText(adjustment));
        }

        foreach (SkillTextData skill in equipment.Skills) WriteGrantedSkill(builder, skill);

        if (!string.IsNullOrWhiteSpace(equipment.Flavor))
        {
            builder.AppendLine(Separator);
            builder.AppendLine(equipment.Flavor.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd());
        }
        builder.Append("```");
        return builder.ToString().Replace("\r\n", "\n");
    }

    private static string EquipmentValueText(EquipmentValueDefinition value)
    {
        if (value == null) throw new InvalidOperationException("価値がnullです。");
        switch (value.Kind)
        {
            case EquipmentValueKind.Currency:
                string quantity = value.Quantity > 1 ? value.Quantity.ToString(CultureInfo.InvariantCulture) + "個で" : "";
                return quantity + value.Amount.ToString(CultureInfo.InvariantCulture) + value.Currency;
            case EquipmentValueKind.Untradeable:
                return "取引不可";
            default:
                throw new InvalidOperationException("未対応の価値種別です：" + value.Kind);
        }
    }

    private static string EquipmentRequirementsText(List<EquipmentRequirementDefinition> requirements)
    {
        var texts = new List<string>();
        foreach (EquipmentRequirementDefinition requirement in requirements)
        {
            if (requirement == null) throw new InvalidOperationException("装備条件がnullです。");
            switch (requirement.Type)
            {
                case EquipmentRequirementType.MinimumLevel:
                    string[] parameters = Args(requirement.Parameters, 1, "MinimumLevel");
                    texts.Add(Number(parameters[0], 1, "必要レベル").ToString(CultureInfo.InvariantCulture) + "レベル以上");
                    break;
                case EquipmentRequirementType.MinimumStat:
                case EquipmentRequirementType.MaximumStat:
                    string[] stat = Args(requirement.Parameters, 2, "能力値条件");
                    texts.Add(stat[0] + stat[1] + (requirement.Type == EquipmentRequirementType.MinimumStat ? "以上" : "以下"));
                    break;
                case EquipmentRequirementType.LearnedSkill:
                case EquipmentRequirementType.LearnedTitle:
                    string prefix = requirement.Type == EquipmentRequirementType.LearnedTitle ? "称号" : "";
                    texts.Add(prefix + "《" + Args(requirement.Parameters, 1, "習得条件")[0] + "》の習得");
                    break;
                default:
                    throw new InvalidOperationException("未対応の装備条件です：" + requirement.Type);
            }
        }
        return string.Join(", ", texts);
    }

    private static void WriteGrantedSkill(StringBuilder builder, SkillTextData skill)
    {
        string built = SkillTextConverter.Build(skill);
        string[] lines = built.Replace("\r\n", "\n").Split('\n');
        if (lines.Length < 3 || lines[0] != "```" || lines[lines.Length - 1] != "```")
            throw new InvalidOperationException("装備スキルの再構築結果が不正です。");
        string expectedName = "《" + Need(skill.Skill.Name, "装備スキル名") + "》";
        if (lines[1] != expectedName) throw new InvalidOperationException("装備スキル名の再構築結果が一致しません。");
        builder.AppendLine();
        builder.AppendLine("●スキル：" + expectedName);
        for (int i = 2; i < lines.Length - 1; i++) builder.AppendLine(lines[i]);
    }
}
