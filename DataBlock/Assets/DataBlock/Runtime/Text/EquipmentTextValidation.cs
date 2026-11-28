using static CommonEffectTextCodec;
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
        Token(equipment.Name, "装備名", "《》");
        Need(equipment.Rarity, "レアリティ");
        Need(equipment.Size, "サイズ");
        if (equipment.Grade < 0 || equipment.Grade > MaximumEquipmentGrade)
            throw new InvalidOperationException("グレードは1～" + MaximumEquipmentGrade + "にしてください。");
        if (!string.IsNullOrEmpty(equipment.SpecialGrade) &&
            (equipment.Grade != 0 || !M(equipment.SpecialGrade, @"^★+☆*$").Success || equipment.SpecialGrade.Length > MaximumEquipmentGrade))
            throw new InvalidOperationException("特殊グレードの星表記が不正です。");
        if (equipment.RequiredSpace < 0) throw new InvalidOperationException("必要敷地は0以上にしてください。");
        if (equipment.VariantGrade < 0 || equipment.VariantGrade > MaximumEquipmentGrade)
            throw new InvalidOperationException("装備名のグレードは0～" +
                MaximumEquipmentGrade + "にしてください。");
        if (equipment.MaxDurability < -1) throw new InvalidOperationException("耐久最大値は正の整数、∞、または未指定にしてください。");
        if (!string.IsNullOrEmpty(equipment.WeaponPowerName))
        {
            Token(equipment.WeaponPowerName, "武器威力名", "《》：:");
            Need(equipment.WeaponPowerFormula, "名前付き武器威力の式");
        }
        if (!string.IsNullOrEmpty(equipment.WeaponPowerFormula)) Need(equipment.WeaponPowerFormula, "武器威力");
        if (!string.IsNullOrEmpty(equipment.Flavor) &&
            equipment.Flavor.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
                .Any(x => M(DataBlockText.NormalizeFenceLine(x), @"^(?<ticks>`{3,})""?\s*\|?$").Success))
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
        if (equipment.WeaponModes == null || equipment.WeaponModes.Count == 1)
            throw new InvalidOperationException("武器の選択肢を使用する場合は2つ以上指定してください。");
        var modes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mode in equipment.WeaponModes)
        {
            if (mode == null || mode.EffectGroups == null || !modes.Add(Token(mode.Name, "武器の選択肢", "●【】")))
                throw new InvalidOperationException("武器の選択肢が重複または未設定です。");
            Need(mode.WeaponPowerFormula, "選択した武器の威力");
            referencedStates.UnionWith(ValidateEquipmentEffectGroups(mode.EffectGroups));
        }
        var ruleStates = equipment.EffectGroups.Concat(equipment.WeaponModes.SelectMany(m => m.EffectGroups))
            .SelectMany(g => g.Effects).Concat(equipment.States.SelectMany(state => state.Effects.Concat(state.Overrides)).SelectMany(e => e.Contents))
            .SelectMany(StateReferences);
        ValidateEquipmentStates(equipment.States, referencedStates, ruleStates);
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
            string value = Token(category, "カテゴリー", "〈〉,、");
            if (!seen.Add(value)) throw new InvalidOperationException("カテゴリーが重複しています：" + value);
        }
        if (!seen.Overlaps(ItemCategories)) throw new InvalidOperationException("アイテムカテゴリーが必要です。");
    }

    private static void ValidateEquipmentSlots(List<string> slots)
    {
        if (slots == null) throw new InvalidOperationException("装備部位がありません。");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string slot in slots)
        {
            string value = Token(slot, "装備部位", ",、");
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
                if (value.Quantity < 1) throw new InvalidOperationException("取引数量は1以上にしてください。");
                if (!string.IsNullOrEmpty(value.Currency) &&
                    !M(value.Currency, @"^[^0-9\s,、【】]+$").Success)
                    throw new InvalidOperationException("通貨名が不正です。");
                return;
            case EquipmentValueKind.Untradeable:
            case EquipmentValueKind.Unspecified:
                if (value.Quantity != 1 || value.Amount != 0 || !string.IsNullOrEmpty(value.Currency))
                    throw new InvalidOperationException("取引不可の装備には金額と通貨を指定できません。");
                return;
            default:
                throw new InvalidOperationException("未対応の価値種別です：" + value.Kind);
        }
    }

    private static void ValidateEquipmentRequirements(List<EquipmentRequirementDefinition> requirements)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (EquipmentRequirementDefinition requirement in requirements)
        {
            string key = requirement.Type.ToString();
            switch (requirement.Type)
            {
                case EquipmentRequirementType.MinimumLevel:
                    Number(Args(requirement.Parameters, 1, "MinimumLevel")[0], 1, "必要レベル");
                    break;
                case EquipmentRequirementType.MinimumStat:
                case EquipmentRequirementType.MaximumStat:
                    string[] stat = Args(requirement.Parameters, 2, "装備条件の能力値");
                    if (!M(stat[0], @"^[一-龯ぁ-んァ-ヶA-Za-z]+$").Success)
                        throw new InvalidOperationException("装備条件の能力値名が不正です。");
                    Number(stat[1], 0, "能力値の閾値");
                    key += ":" + stat[0];
                    break;
                case EquipmentRequirementType.LearnedTitle:
                case EquipmentRequirementType.LearnedSkill:
                    key += ":" + Token(Args(requirement.Parameters, 1, "習得条件")[0], "習得名", "《》");
                    break;
                default:
                    throw new InvalidOperationException("未対応の装備条件です：" + requirement.Type);
            }
            if (!seen.Add(key)) throw new InvalidOperationException("装備条件が重複しています：" + key);
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
            if (!grades.Add(adjustment.Grade))
                throw new InvalidOperationException("グレード補正が重複しています：☆" + adjustment.Grade);
            if (adjustment.Modifiers == null)
                throw new InvalidOperationException("グレード補正の能力値補正を初期化してください：☆" +
                    adjustment.Grade);
            if (adjustment.Modifiers.Any(x => x == null))
                throw new InvalidOperationException("グレード補正にnullがあります：☆" + adjustment.Grade);
            if (adjustment.Replacements == null) throw new InvalidOperationException("グレードの置換値を初期化してください。");
            if (adjustment.MaxDurabilityDelta == 0 && adjustment.WeaponPowerDelta == 0 && adjustment.Modifiers.Count == 0 && adjustment.Replacements.Count == 0)
                throw new InvalidOperationException("グレード補正が空です：☆" + adjustment.Grade);

            if (equipment.Grade == 0 || (adjustment.MaxDurabilityDelta != 0 && equipment.MaxDurability < 1))
                throw new InvalidOperationException("グレード補正に必要な基準グレードまたは耐久最大値がありません。");
            long effectiveDurability = (long)equipment.MaxDurability + adjustment.MaxDurabilityDelta;
            if (adjustment.MaxDurabilityDelta != 0 && (effectiveDurability < 1 || effectiveDurability > int.MaxValue))
                throw new InvalidOperationException("グレード補正後の耐久最大値は1以上の整数にしてください：☆" +
                    adjustment.Grade);

            var changedRules = new HashSet<string>(StringComparer.Ordinal);
            foreach (var change in adjustment.Replacements)
            {
                if (change == null || change.Amount < 0 ||
                    (change.Rule != "ExtraDamageToTargetCategories" && change.Rule != "CategoryIgnoreDefense") || !changedRules.Add(change.Rule))
                    throw new InvalidOperationException("グレード補正の置換対象または値が不正です。");
                EquipmentGradeResolver.ReplacementTarget(equipment, change.Rule);
            }

            var stats = new HashSet<string>(StringComparer.Ordinal);
            foreach (EffectContent modifier in adjustment.Modifiers)
            {
                if (modifier.Type == EffectContentType.RuleModifier || modifier.Type == EffectContentType.CheckByAbilityFactor)
                {
                    ValidateEquipmentEffect(modifier, new HashSet<string>());
                    continue;
                }
                if (modifier.Type != EffectContentType.ModifyStat)
                    throw new InvalidOperationException("グレード補正の効果種別が不正です：☆" + adjustment.Grade);
                string[] p = Args(modifier.Parameters, 4, "グレード補正のModifyStat");
                RequireSelf(p[0], "グレード補正のModifyStat");
                string stat = Token(p[1], "グレード補正の能力値名", "[],、+-");
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
            if (!Enum.IsDefined(typeof(EquipmentEffectSection), group.Section))
                throw new InvalidOperationException("効果の適用区分が不正です。");
            if (!string.IsNullOrEmpty(group.Name))
            {
                string name = Token(group.Name, "装備効果名", "《》：:");
                if (!names.Add(name)) throw new InvalidOperationException("装備効果名が重複しています：" + name);
            }
            if (group.Effects == null || group.Effects.Count == 0)
                throw new InvalidOperationException("装備効果グループの効果が空です：" + group.Name);
            if (group.Effects.Any(x => x == null))
                throw new InvalidOperationException("装備効果にnullがあります：" + group.Name);
            if (group.Effects[0].Type == EffectContentType.RollTable)
            {
                CommonEffectRollTable.Write(group.Effects, referencedStates);
                continue;
            }
            if (group.Effects.Any(x => x.Type == EffectContentType.SelectedWeaponRule) &&
                !group.Effects.All(x => x.Type == EffectContentType.SelectedWeaponRule))
                throw new InvalidOperationException("同じグループの武器選択条件を統一してください：" + group.Name);
            foreach (EffectContent effect in group.Effects)
                ValidateEquipmentEffect(effect, referencedStates);
        }
        return referencedStates;
    }

    private static void ValidateEquipmentEffect(EffectContent effect, HashSet<string> referencedStates)
    {
        EquipmentEffectText(effect);
        if (effect.Type == EffectContentType.GainStateStacksOnAction)
            referencedStates.Add(effect.Parameters[2]);
    }

    private static IEnumerable<string> StateReferences(EffectContent effect)
    {
        if (effect.Type == EffectContentType.ApplyState || effect.Type == EffectContentType.GainStack)
            yield return effect.Parameters[1];
        foreach (string name in ReferencedRuleStates(effect)) yield return name;
    }

    private static void ValidateEquipmentStates(List<StateDefinition> states,
        HashSet<string> referencedStates, IEnumerable<string> ruleStates)
    {
        var definitions = new HashSet<string>(StringComparer.Ordinal);
        foreach (StateDefinition state in states)
        {
            string name = Token(state.Name, "状態名", "《》：:");
            if (!definitions.Add(name)) throw new InvalidOperationException("状態名が重複しています：" + name);
            if (state.Categories == null) throw new InvalidOperationException("状態のカテゴリーがnullです：" + name);
            EquipmentStateText(state, referencedStates);
        }

        foreach (string reference in referencedStates)
            if (!definitions.Contains(reference))
                throw new InvalidOperationException("参照された状態の定義がありません：" + reference);
        referencedStates.UnionWith(ruleStates);
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
            string name = Token(skill.Skill.Name, "装備内スキル名", "《》：:");
            if (!names.Add(name)) throw new InvalidOperationException("装備内スキル名が重複しています：" + name);
            if (!string.IsNullOrWhiteSpace(skill.Skill.Flavor))
                throw new InvalidOperationException("装備内スキルには個別フレーバーを指定できません：" + name);
            string built = SkillTextConverter.Build(skill);
            if (built.Replace("\r\n", "\n").Split('\n').Skip(2)
                .Any(x => M(SyntaxLine(x), @"^●\s*スキル[：:]\s*《([^《》]+)》$").Success))
                throw new InvalidOperationException("装備内の子スキル名に予約済みの「スキル：《...》」形式は使用できません：" + name);
        }
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
