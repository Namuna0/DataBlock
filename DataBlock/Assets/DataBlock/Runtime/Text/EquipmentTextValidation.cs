using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public static partial class EquipmentTextConverter
{
    private static EquipmentDefinition Validate(EquipmentTextData data)
    {
        if (data == null || data.Equipment == null)
            throw new InvalidOperationException("装備データがnullです。");

        EquipmentDefinition equipment = data.Equipment;
        EquipmentToken(equipment.Name, "装備名", "《》");
        Need(equipment.Rarity, "レアリティ");
        Need(equipment.Size, "サイズ");
        if (equipment.Grade < 1 || equipment.Grade > MaximumEquipmentGrade)
            throw new InvalidOperationException("グレードは1～" + MaximumEquipmentGrade + "にしてください。");
        if (equipment.VariantGrade < 0 || equipment.VariantGrade > MaximumEquipmentGrade)
            throw new InvalidOperationException("装備名のグレードは0～" +
                MaximumEquipmentGrade + "にしてください。");
        if (equipment.MaxDurability < 1) throw new InvalidOperationException("耐久最大値は1以上にしてください。");
        if (!string.IsNullOrEmpty(equipment.WeaponPowerFormula)) Need(equipment.WeaponPowerFormula, "武器威力");
        if (!string.IsNullOrEmpty(equipment.Flavor) &&
            equipment.Flavor.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
                .Any(x => M(NormalizeFenceLine(x), @"^(?<ticks>`{3,})""?\s*\|?$").Success))
            throw new InvalidOperationException("装備フレーバーに単独のコードフェンス行は使用できません。");

        ValidateEquipmentCategories(equipment.Categories);
        ValidateEquipmentSlots(equipment.EquipSlots);
        ValidateEquipmentValue(equipment.Value);

        if (equipment.EquipRequirements == null || equipment.EffectGroups == null ||
            equipment.States == null || equipment.GradeAdjustments == null || equipment.Skills == null)
            throw new InvalidOperationException("装備条件・装備効果・状態・グレード補正・装備内スキルを初期化してください。");
        if (equipment.EquipRequirements.Any(x => x == null) || equipment.EffectGroups.Any(x => x == null) ||
            equipment.States.Any(x => x == null) || equipment.GradeAdjustments.Any(x => x == null) ||
            equipment.Skills.Any(x => x == null))
            throw new InvalidOperationException("装備条件・装備効果・状態・グレード補正・装備内スキルにnullがあります。");
        if (equipment.VariantGrade > 0 &&
            (equipment.VariantGrade != equipment.Grade || equipment.GradeAdjustments.Count > 0))
            throw new InvalidOperationException(
                "名前末尾のグレードは、補正適用済みで補正表を持たない装備のグレードと一致させてください。");
        ValidateEquipmentRequirements(equipment.EquipRequirements);
        ValidateEquipmentGradeAdjustments(equipment);
        HashSet<string> referencedStates = ValidateEquipmentEffectGroups(equipment.EffectGroups);
        ValidateEquipmentStates(equipment.States, referencedStates);
        ValidateEquipmentSkills(equipment.Skills);
        return equipment;
    }

    private static void ValidateEquipmentCategories(List<string> categories)
    {
        if (categories == null || categories.Count == 0)
            throw new InvalidOperationException("装備カテゴリーがありません。");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string category in categories)
        {
            string value = EquipmentToken(category, "カテゴリー", "〈〉,、");
            if (!seen.Add(value)) throw new InvalidOperationException("カテゴリーが重複しています：" + value);
        }
        if (!seen.Contains(EquipmentCategory))
            throw new InvalidOperationException("装備には〈" + EquipmentCategory + "〉カテゴリーが必要です。");
    }

    private static void ValidateEquipmentSlots(List<string> slots)
    {
        if (slots == null || slots.Count == 0) throw new InvalidOperationException("装備部位がありません。");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string slot in slots)
        {
            string value = EquipmentToken(slot, "装備部位", ",、");
            if (!seen.Add(value)) throw new InvalidOperationException("装備部位が重複しています：" + value);
        }
    }

    private static void ValidateEquipmentValue(EquipmentValueDefinition value)
    {
        if (value == null) throw new InvalidOperationException("価値がnullです。");
        switch (value.Kind)
        {
            case EquipmentValueKind.Currency:
                if (value.Amount < 0) throw new InvalidOperationException("価値は0以上にしてください。");
                EquipmentToken(value.Currency, "通貨名", "【】,、");
                return;
            case EquipmentValueKind.Untradeable:
                if (value.Amount != 0 || !string.IsNullOrEmpty(value.Currency))
                    throw new InvalidOperationException("取引不可の装備には金額と通貨を指定できません。");
                return;
            default:
                throw new InvalidOperationException("未対応の価値種別です：" + value.Kind);
        }
    }

    private static void ValidateEquipmentRequirements(List<EquipmentRequirementDefinition> requirements)
    {
        var seen = new HashSet<EquipmentRequirementType>();
        foreach (EquipmentRequirementDefinition requirement in requirements)
        {
            if (!seen.Add(requirement.Type))
                throw new InvalidOperationException("装備条件が重複しています：" + requirement.Type);
            switch (requirement.Type)
            {
                case EquipmentRequirementType.MinimumLevel:
                    Number(Args(requirement.Parameters, 1, "MinimumLevel")[0], 1, "必要レベル");
                    break;
                default:
                    throw new InvalidOperationException("未対応の装備条件です：" + requirement.Type);
            }
        }
    }

    private static void ValidateEquipmentGradeAdjustments(EquipmentDefinition equipment)
    {
        var grades = new HashSet<int>();
        foreach (EquipmentGradeAdjustmentDefinition adjustment in equipment.GradeAdjustments)
        {
            if (adjustment.Grade < 1 || adjustment.Grade > MaximumEquipmentGrade)
                throw new InvalidOperationException("補正対象グレードは1～" +
                    MaximumEquipmentGrade + "にしてください。");
            if (adjustment.Grade == equipment.Grade)
                throw new InvalidOperationException("基準グレードにはグレード補正を指定できません：☆" +
                    adjustment.Grade);
            if (!grades.Add(adjustment.Grade))
                throw new InvalidOperationException("グレード補正が重複しています：☆" + adjustment.Grade);
            if (adjustment.Modifiers == null)
                throw new InvalidOperationException("グレード補正の能力値補正を初期化してください：☆" +
                    adjustment.Grade);
            if (adjustment.Modifiers.Any(x => x == null))
                throw new InvalidOperationException("グレード補正にnullがあります：☆" + adjustment.Grade);
            if (adjustment.MaxDurabilityDelta == 0 && adjustment.Modifiers.Count == 0)
                throw new InvalidOperationException("グレード補正が空です：☆" + adjustment.Grade);

            long effectiveDurability = (long)equipment.MaxDurability + adjustment.MaxDurabilityDelta;
            if (effectiveDurability < 1 || effectiveDurability > int.MaxValue)
                throw new InvalidOperationException("グレード補正後の耐久最大値は1以上の整数にしてください：☆" +
                    adjustment.Grade);

            var stats = new HashSet<string>(StringComparer.Ordinal);
            foreach (EquipmentEffectDefinition modifier in adjustment.Modifiers)
            {
                if (modifier.Type != EquipmentEffectType.ModifyStat)
                    throw new InvalidOperationException("グレード補正には能力値加算だけを指定できます：☆" +
                        adjustment.Grade);
                string[] p = Args(modifier.Parameters, 4, "グレード補正のModifyStat");
                RequireSelf(p[0], "グレード補正のModifyStat");
                string stat = EquipmentToken(p[1], "グレード補正の能力値名", "[],、+-");
                if (stat == "耐久最大値")
                    throw new InvalidOperationException("耐久最大値はMaxDurabilityDeltaへ指定してください：☆" +
                        adjustment.Grade);
                if (!stats.Add(stat))
                    throw new InvalidOperationException("同じ能力値のグレード補正が重複しています：☆" +
                        adjustment.Grade + " " + stat);
                if (p[2] != "Add")
                    throw new InvalidOperationException("グレード補正のModifyStat演算はAddです：☆" +
                        adjustment.Grade);
                NonZeroSignedDecimal(p[3], "グレード補正値");
            }
        }
    }

    private static HashSet<string> ValidateEquipmentEffectGroups(List<EquipmentEffectGroup> groups)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var referencedStates = new HashSet<string>(StringComparer.Ordinal);
        foreach (EquipmentEffectGroup group in groups)
        {
            if (!string.IsNullOrEmpty(group.Name))
            {
                string name = EquipmentToken(group.Name, "装備効果名", "《》：:");
                if (!names.Add(name)) throw new InvalidOperationException("装備効果名が重複しています：" + name);
            }
            if (group.Effects == null || group.Effects.Count == 0)
                throw new InvalidOperationException("装備効果グループの効果が空です：" + group.Name);
            if (group.Effects.Any(x => x == null))
                throw new InvalidOperationException("装備効果にnullがあります：" + group.Name);
            if (group.Effects.Count > 1 && group.Effects.Any(x => x.Type != EquipmentEffectType.ModifyStat))
                throw new InvalidOperationException("能力値変更以外の装備効果は1グループ1件です：" + group.Name);
            foreach (EquipmentEffectDefinition effect in group.Effects)
                ValidateEquipmentEffect(effect, referencedStates);
        }
        return referencedStates;
    }

    private static void ValidateEquipmentEffect(
        EquipmentEffectDefinition effect,
        HashSet<string> referencedStates)
    {
        string[] p;
        switch (effect.Type)
        {
            case EquipmentEffectType.ModifyStat:
                p = Args(effect.Parameters, 4, "ModifyStat");
                RequireSelf(p[0], "ModifyStat");
                EquipmentToken(p[1], "能力値名", "[],、+-");
                if (p[2] != "Add")
                    throw new InvalidOperationException("装備本体のModifyStat演算はAddです。");
                string modifierValue = Need(p[3], "ModifyStatの値");
                List<string> commaParts = Split(modifierValue, ",");
                List<string> japaneseCommaParts = Split(modifierValue, "、");
                if (commaParts.Count != 1 || commaParts[0] != modifierValue ||
                    japaneseCommaParts.Count != 1 || japaneseCommaParts[0] != modifierValue)
                    throw new InvalidOperationException("ModifyStatの値に括弧外のカンマは使用できません。");
                break;
            case EquipmentEffectType.ModifySelectedWeaponAction:
                p = Args(effect.Parameters, 3, "ModifySelectedWeaponAction");
                EquipmentToken(p[0], "行動カテゴリー", "〈〉,、");
                PositiveDecimal(p[1], "達成値倍率");
                PositiveDecimal(p[2], "威力倍率");
                break;
            case EquipmentEffectType.MultiplyIncomingDamage:
                p = Args(effect.Parameters, 3, "MultiplyIncomingDamage");
                RequireSelf(p[0], "MultiplyIncomingDamage");
                EquipmentToken(p[1], "行動カテゴリー", "〈〉,、");
                PositiveDecimal(p[2], "被ダメージ倍率");
                break;
            case EquipmentEffectType.AllowItemUse:
                p = Args(effect.Parameters, 3, "AllowItemUse");
                RequireSelf(p[0], "AllowItemUse");
                if (p[1] != "Encounter")
                    throw new InvalidOperationException("AllowItemUseのフェーズはEncounterです。");
                EquipmentToken(p[2], "アイテムカテゴリー", "〈〉,、");
                break;
            case EquipmentEffectType.MultiplyActionPower:
                p = Args(effect.Parameters, 3, "MultiplyActionPower");
                RequireSelf(p[0], "MultiplyActionPower");
                EquipmentToken(p[1], "行動カテゴリー", "〈〉,、");
                PositiveDecimal(p[2], "威力倍率");
                break;
            case EquipmentEffectType.GainStateStacksOnAction:
                p = Args(effect.Parameters, 4, "GainStateStacksOnAction");
                RequireSelf(p[0], "GainStateStacksOnAction");
                EquipmentToken(p[1], "行動カテゴリー", "〈〉,、");
                string stateName = EquipmentToken(p[2], "状態名", "《》：:");
                Number(p[3], 1, "獲得スタック数");
                referencedStates.Add(stateName);
                break;
            default:
                throw new InvalidOperationException("未対応の装備効果です：" + effect.Type);
        }
    }

    private static void ValidateEquipmentStates(
        List<EquipmentStateDefinition> states,
        HashSet<string> referencedStates)
    {
        var definitions = new HashSet<string>(StringComparer.Ordinal);
        foreach (EquipmentStateDefinition state in states)
        {
            string name = EquipmentToken(state.Name, "状態名", "《》：:");
            if (!definitions.Add(name)) throw new InvalidOperationException("状態名が重複しています：" + name);
            if (state.Categories == null || state.Categories.Count != 1 || state.Categories[0] != "スタック")
                throw new InvalidOperationException("装備のスタック状態カテゴリーは「スタック」を1件指定してください：" + name);
            if (state.Modifiers == null || state.Modifiers.Count != 1 || state.Modifiers[0] == null)
                throw new InvalidOperationException("スタック状態の補正は1件必要です：" + name);
            foreach (EquipmentEffectDefinition modifier in state.Modifiers)
            {
                if (modifier.Type != EquipmentEffectType.ModifyStat)
                    throw new InvalidOperationException("スタック状態にはModifyStatだけを指定できます：" + name);
                string[] p = Args(modifier.Parameters, 4, "状態のModifyStat");
                RequireSelf(p[0], "状態のModifyStat");
                EquipmentToken(p[1], "能力値名", "[],、+-");
                if (p[2] != "Multiply")
                    throw new InvalidOperationException("スタック状態のModifyStatはMultiplyにしてください：" + name);
                if (!Need(p[3], "スタック倍率式").Contains("[スタック]"))
                    throw new InvalidOperationException("スタック状態の倍率式には[スタック]が必要です：" + name);
            }
        }

        foreach (string reference in referencedStates)
            if (!definitions.Contains(reference))
                throw new InvalidOperationException("参照された状態の定義がありません：" + reference);
        foreach (string definition in definitions)
            if (!referencedStates.Contains(definition))
                throw new InvalidOperationException("装備効果から参照されていない状態です：" + definition);
    }

    private static void ValidateEquipmentSkills(List<SkillTextData> skills)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (SkillTextData skill in skills)
        {
            if (skill == null || skill.Skill == null)
                throw new InvalidOperationException("装備内スキルがnullです。");
            string name = EquipmentToken(skill.Skill.Name, "装備内スキル名", "《》：:");
            if (!names.Add(name)) throw new InvalidOperationException("装備内スキル名が重複しています：" + name);
            if (!string.IsNullOrWhiteSpace(skill.Skill.Flavor))
                throw new InvalidOperationException("装備内スキルには個別フレーバーを指定できません：" + name);
            string built = SkillTextConverter.Build(skill);
            if (built.Replace("\r\n", "\n").Split('\n').Skip(2)
                .Any(x => M(SyntaxLine(x), @"^●\s*スキル[：:]\s*《([^《》]+)》$").Success))
                throw new InvalidOperationException("装備内の子スキル名に予約済みの「スキル：《...》」形式は使用できません：" + name);
        }
    }

    private static string EquipmentToken(string value, string label, string forbiddenCharacters)
    {
        string result = Need(value, label);
        if (result.IndexOfAny(forbiddenCharacters.ToCharArray()) >= 0)
            throw new InvalidOperationException(label + "に構文区切り文字「" + forbiddenCharacters + "」は使用できません。");
        return result;
    }

    private static void RequireSelf(string actor, string label)
    {
        if (actor != "Self") throw new InvalidOperationException(label + "の対象はSelfです。");
    }

    private static void PositiveDecimal(string value, string label)
    {
        decimal number;
        if (!decimal.TryParse(Need(value, label), NumberStyles.Float, CultureInfo.InvariantCulture, out number) || number <= 0)
            throw new InvalidOperationException(label + "は0より大きい数値にしてください。");
    }

    private static void NonZeroSignedDecimal(string value, string label)
    {
        string text = Need(value, label);
        decimal number;
        if ((text[0] != '+' && text[0] != '-') ||
            !decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
            number == 0)
            throw new InvalidOperationException(label + "は0以外の符号付き数値にしてください。");
    }
}
