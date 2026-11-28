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
        if (targetGrade != equipment.Grade) RequireResolvableGrade(equipment, targetGrade);
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
        if (targetGrade == equipment.Grade) return copy;
        RequireResolvableGrade(equipment, targetGrade);

        EquipmentGradeAdjustmentDefinition adjustment = equipment.GradeAdjustments.SingleOrDefault(x => x.Grade == targetGrade);
        if (adjustment != null)
        {
            equipment.MaxDurability = checked(equipment.MaxDurability + adjustment.MaxDurabilityDelta);
            if (string.IsNullOrEmpty(equipment.WeaponPowerFormula))
                equipment.WeaponPowerBonus = checked(equipment.WeaponPowerBonus + adjustment.WeaponPowerDelta);
            else if (adjustment.WeaponPowerDelta != 0)
                equipment.WeaponPowerFormula = "(" + equipment.WeaponPowerFormula + ")" +
                    adjustment.WeaponPowerDelta.ToString("+0;-0", CultureInfo.InvariantCulture);
            foreach (var change in adjustment.Replacements)
            {
                var parameters = ReplacementTarget(equipment, change.Rule).Parameters;
                parameters[parameters.Count - 1] = change.Amount.ToString(CultureInfo.InvariantCulture);
            }
            foreach (var modifier in adjustment.Modifiers)
                equipment.EffectGroups.Add(new EquipmentEffectGroup { Effects = { modifier } });
        }
        equipment.VariantGrade = targetGrade;
        equipment.Grade = targetGrade;
        equipment.GradeAdjustments.Clear();
        EquipmentTextConverter.Build(copy);
        return copy;
    }

    internal static EffectContent ReplacementTarget(EquipmentDefinition equipment, string rule)
    {
        var targets = equipment.EffectGroups.SelectMany(g => g.Effects)
            .Where(e => e.Type == EffectContentType.RuleModifier && e.Parameters != null && e.Parameters.Count > 1 &&
                e.Parameters[0] == rule).Take(2).ToArray();
        if (targets.Length != 1) throw new InvalidOperationException("グレード補正の置換対象が一意ではありません：" + rule);
        return targets[0];
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
        // The standard table lists adjustments for 1, 2, 4 and 5. Its omitted grade 3 has no adjustment.
        if (targetGrade == 3 && new[] { 1, 2, 4, 5 }.All(g => equipment.GradeAdjustments.Any(x => x.Grade == g))) return;
        if (!equipment.GradeAdjustments.Any(x => x.Grade == targetGrade))
            throw new InvalidOperationException("☆" + targetGrade + "のグレード補正がありません：" +
                equipment.Name);
    }
}
