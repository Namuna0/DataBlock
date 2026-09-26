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
        builder.AppendLine("【グレード】" + new string('☆', equipment.Grade));
        builder.AppendLine("【価値】" + EquipmentValueText(equipment.Value));
        var slots = new List<string>();
        foreach (string slot in equipment.EquipSlots) slots.Add(Need(slot, "装備部位"));
        builder.AppendLine("【装備部位】" + string.Join(", ", slots));
        if (equipment.EquipRequirements.Count > 0)
            builder.AppendLine("【装備条件】" + EquipmentRequirementsText(equipment.EquipRequirements));
        builder.AppendLine("【サイズ】" + Need(equipment.Size, "サイズ"));
        builder.AppendLine("【耐久最大値】" + equipment.MaxDurability.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(equipment.WeaponPowerFormula))
            builder.AppendLine("【武器威力】" + Need(equipment.WeaponPowerFormula, "武器威力"));
        builder.AppendLine("【装備効果】");
        foreach (EquipmentEffectGroup group in equipment.EffectGroups)
            builder.AppendLine("・" + EquipmentEffectGroupText(group));
        foreach (EquipmentStateDefinition state in equipment.States)
            builder.AppendLine("・" + EquipmentStateText(state));

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
                return value.Amount.ToString(CultureInfo.InvariantCulture) + Need(value.Currency, "通貨名");
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
