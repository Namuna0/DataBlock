using System;
using System.Globalization;
using System.Linq;

public static class EquipmentGradeResolver
{
    public static EquipmentTextData Resolve(EquipmentTextData source, int targetGrade)
    {
        EquipmentTextData copy = Clone(source);
        return Apply(copy, targetGrade);
    }

    public static EquipmentTextData Resolve(EquipmentTextData source, string referenceText)
    {
        EquipmentTextData copy = Clone(source);
        int targetGrade = ReadReferenceGrade(copy.Equipment, referenceText);
        return Apply(copy, targetGrade);
    }

    public static string FormatReference(EquipmentTextData source, int targetGrade)
    {
        EquipmentDefinition equipment = Clone(source).Equipment;
        RequireResolvableGrade(equipment, targetGrade);
        string reference = "《" + equipment.Name + "》";
        return targetGrade == equipment.Grade && equipment.VariantGrade == 0
            ? reference
            : reference + "☆" + targetGrade.ToString(CultureInfo.InvariantCulture);
    }

    private static EquipmentTextData Clone(EquipmentTextData source)
    {
        return EquipmentTextConverter.Parse(EquipmentTextConverter.Build(source));
    }

    private static EquipmentTextData Apply(EquipmentTextData copy, int targetGrade)
    {
        EquipmentDefinition equipment = copy.Equipment;
        RequireResolvableGrade(equipment, targetGrade);
        if (targetGrade == equipment.Grade)
        {
            equipment.GradeAdjustments.Clear();
            EquipmentTextConverter.Build(copy);
            return copy;
        }

        EquipmentGradeAdjustmentDefinition adjustment =
            equipment.GradeAdjustments.Single(x => x.Grade == targetGrade);
        equipment.MaxDurability = checked(equipment.MaxDurability + adjustment.MaxDurabilityDelta);
        if (adjustment.Modifiers.Count > 0)
        {
            var group = new EquipmentEffectGroup();
            foreach (EquipmentEffectDefinition modifier in adjustment.Modifiers)
            {
                group.Effects.Add(new EquipmentEffectDefinition
                {
                    Type = modifier.Type,
                    Parameters = modifier.Parameters.ToList()
                });
            }
            equipment.EffectGroups.Add(group);
        }
        equipment.Grade = targetGrade;
        equipment.VariantGrade = targetGrade;
        equipment.GradeAdjustments.Clear();
        EquipmentTextConverter.Build(copy);
        return copy;
    }

    private static int ReadReferenceGrade(EquipmentDefinition equipment, string referenceText)
    {
        if (string.IsNullOrWhiteSpace(referenceText))
            throw new InvalidOperationException("装備参照が空です。");

        string text = referenceText.Trim();
        string baseReference = "《" + equipment.Name + "》";
        if (text == baseReference) return equipment.Grade;

        string prefix = baseReference + "☆";
        if (!text.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("装備参照は《" + equipment.Name + "》または《" +
                equipment.Name + "》☆5の形式です。");

        string gradeText = text.Substring(prefix.Length);
        int grade;
        if (gradeText.Length == 0 || (gradeText.Length > 1 && gradeText[0] == '0') ||
            !int.TryParse(gradeText, NumberStyles.None, CultureInfo.InvariantCulture, out grade) ||
            grade < 1)
            throw new InvalidOperationException("装備参照のグレードは1以上の整数にしてください。");
        if (grade == equipment.Grade && equipment.VariantGrade == 0)
            throw new InvalidOperationException("基準グレードの装備参照には☆" + grade +
                "を付けないでください。");
        return grade;
    }

    private static void RequireResolvableGrade(EquipmentDefinition equipment, int targetGrade)
    {
        if (targetGrade < 1 || targetGrade > EquipmentTextConverter.MaximumEquipmentGrade)
            throw new InvalidOperationException("解決するグレードは1～" +
                EquipmentTextConverter.MaximumEquipmentGrade + "にしてください。");
        if (targetGrade == equipment.Grade) return;
        if (!equipment.GradeAdjustments.Any(x => x.Grade == targetGrade))
            throw new InvalidOperationException("☆" + targetGrade + "のグレード補正がありません：" +
                equipment.Name);
    }
}
