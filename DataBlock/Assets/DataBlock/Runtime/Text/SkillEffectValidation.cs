using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static bool IsElementalWeaponAttack(EffectContent content)
    {
        return content != null && content.Type == EffectContentType.SkillAttack && content.Parameters != null &&
               content.Parameters.Count >= 6 && content.Parameters[1] == "ElementalWeapon";
    }

    private static void ValidateSkillEffects(SkillBody skill)
    {
        if (skill.Effects == null || skill.Overrides == null) throw new InvalidOperationException("EffectsまたはOverridesがnullです。");
        if (skill.Effects.Count == 0 && skill.Overrides.Count == 0) return;

        var activeAttacks = new List<EffectContent>();
        var skillValueAttacks = new List<EffectContent>();
        var elementalAttacks = new List<EffectContent>();
        var counterReductions = new List<EffectContent>();
        foreach (EffectDefinition effect in skill.Effects)
        {
            if (effect == null || !IsOrdinary(effect.Type) || effect.Triggers == null || effect.Contents == null || effect.Contents.Count == 0) throw new InvalidOperationException("通常効果の種別・内容を指定してください。");
            SkillTriggerText(effect.Type, effect.Triggers);
            foreach (EffectContent content in effect.Contents)
            {
                SkillContentText(content);
                if (effect.Type == EffectType.Active && (content.Type == EffectContentType.WeaponAttack || content.Type == EffectContentType.SkillAttack)) activeAttacks.Add(content);
                if (effect.Type == EffectType.Active && (content.Type == EffectContentType.WeaponAttack || IsElementalWeaponAttack(content))) skillValueAttacks.Add(content);
                if (effect.Type == EffectType.Active && IsElementalWeaponAttack(content)) elementalAttacks.Add(content);
                if (effect.Type == EffectType.Counter && content.Type == EffectContentType.ReduceDamage) counterReductions.Add(content);
            }
        }

        bool hasUnconditionalActiveAttack = HasSingleUnconditional(skill, activeAttacks, EffectType.Active);
        bool hasUnconditionalSkillValueAttack = HasSingleUnconditional(skill, skillValueAttacks, EffectType.Active);
        bool hasUnconditionalElementalAttack = HasSingleUnconditional(skill, elementalAttacks, EffectType.Active);
        bool hasUnconditionalCounterReduction = HasSingleUnconditional(skill, counterReductions, EffectType.Counter);

        bool seenWeaponAttack = false;
        List<EffectContent> activeWeaponAttacks = skillValueAttacks.Where(x => x.Type == EffectContentType.WeaponAttack).ToList();
        foreach (EffectDefinition effect in skill.Effects.Where(x => x.Type == EffectType.Active))
        {
            if (effect.Triggers.Any(x => x.Timing == TriggerTiming.AfterWeaponAttack))
            {
                if (activeWeaponAttacks.Count != 1 || !seenWeaponAttack) throw new InvalidOperationException("「この攻撃」の前に同じActive内の無条件武器攻撃を1件指定してください。");
                ConditionEntry applied = effect.Triggers.Single().Conditions.And.Single();
                if (activeWeaponAttacks[0].Parameters[0] != applied.Parameters[0]) throw new InvalidOperationException("攻撃対象と攻撃後条件の対象が一致しません。");
            }
            if (effect.Triggers.Count == 0 && effect.Contents.Any(x => x.Type == EffectContentType.WeaponAttack)) seenWeaponAttack = true;
        }

        ValidateConsumedItems(skill);
        ValidateRandomRolls(skill);
        foreach (OverrideDefinition effect in skill.Overrides)
        {
            if (effect == null || effect.Type == EffectType.None || !DisplayOrder.Contains(effect.Type) || effect.Triggers == null || effect.Contents == null || effect.Contents.Count == 0) throw new InvalidOperationException("上書き効果の種別と内容を指定してください。");
            foreach (OverrideContent content in effect.Contents)
            {
                OverrideText(effect, content);
                bool spike = effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike;
                if (spike && content.Type != OverrideContentType.SetSkillValue && content.Type != OverrideContentType.SetDamageReduction && content.Type != OverrideContentType.SetAttackComponent && content.Type != OverrideContentType.SetModifier && content.Type != OverrideContentType.SetActivationRollFormula)
                    throw new InvalidOperationException("スパイクはスキル値・攻撃構成要素・被ダメージ軽減値・発動ロールの変更に対応します。");
                if (content.Type == OverrideContentType.SetActivationRollFormula &&
                    (skill.Roll.Count <= 0 || skill.Roll.Formula == "自動成功"))
                    throw new InvalidOperationException("発動ロールのスパイク変更には通常の発動ロールが必要です。");
                if (content.Type == OverrideContentType.SetSkillValue && !hasUnconditionalSkillValueAttack)
                    throw new InvalidOperationException("スキル値の上書き先となるアクティブ攻撃を無条件で1件だけ指定してください。");
                if (content.Type == OverrideContentType.SetAttackComponent || content.Type == OverrideContentType.MultiplyAttackComponent)
                {
                    string[] p = Args(content.Parameters, 2, content.Type.ToString());
                    if (p[0] != "AttributePower") throw new InvalidOperationException("今回の攻撃構成要素はAttributePowerです。");
                    if (!hasUnconditionalElementalAttack) throw new InvalidOperationException("属性威力の変更先となる複合属性攻撃を無条件で1件だけ指定してください。");
                }
                if (content.Type == OverrideContentType.SetAttackRule && !hasUnconditionalActiveAttack)
                    throw new InvalidOperationException("攻撃規則の適用先となるアクティブ攻撃を無条件で1件だけ指定してください。");
                if (content.Type == OverrideContentType.SetDamageReduction && !hasUnconditionalCounterReduction)
                    throw new InvalidOperationException("軽減値の上書き先となるカウンターの被ダメージ軽減を無条件で1件だけ指定してください。");
                if (content.Type == OverrideContentType.SetCritical && !hasUnconditionalSkillValueAttack)
                    throw new InvalidOperationException("確定クリティカルの対象となるアクティブ武器攻撃を無条件で1件だけ指定してください。");
                if (content.Type == OverrideContentType.SetCritical && (effect.Type != EffectType.Active || skill.Roll.Count == 0)) throw new InvalidOperationException("確定クリティカルは発動ロールのあるActiveに指定してください。");
                if (content.Type == OverrideContentType.PreventCounterDamage && !hasUnconditionalActiveAttack)
                    throw new InvalidOperationException("カウンターダメージ禁止の対象となるアクティブ攻撃を無条件で1件だけ指定してください。");
                if (content.Type == OverrideContentType.AddStateDuration && (effect.Type != EffectType.Active || skill.DeclarationConditions.And.Count(x => x.Type == ConditionType.SelectOwnState && x.Parameters.Count >= 2 && x.Parameters[0] == "1") != 1)) throw new InvalidOperationException("持続ターン変更にはActiveとSelectOwnState宣言条件が必要です。");
                if (content.Type == OverrideContentType.AddTargetValue && effect.Type != EffectType.Passive) throw new InvalidOperationException("今回の目標値補正はPassiveに指定してください。");
                if (content.Type == OverrideContentType.AddResourceCost)
                {
                    string[] p = Args(content.Parameters, 4, "AddResourceCost");
                    if (p[3] == "Optional" && effect.Type != EffectType.Passive) throw new InvalidOperationException("任意消費補正はPassiveに指定してください。");
                    if (p[3] == "Automatic" && (effect.Type != EffectType.Critical || !hasUnconditionalActiveAttack)) throw new InvalidOperationException("自動消費補正は攻撃を持つスキルのCriticalに指定してください。");
                    if (p[3] != "Optional" && p[3] != "Automatic") throw new InvalidOperationException("消費補正の適用方法はOptional / Automaticです。");
                }
            }
        }

        ValidatePassiveExtensions(skill);
        foreach (EffectType stage in new[] { EffectType.SecondSpike, EffectType.ThirdSpike }) ValidateSpikeStage(skill, stage);

        int activeConditionalValueCount = skill.Overrides.Where(x => x.Type == EffectType.Active).Sum(x => x.Contents.Count(c => c.Type == OverrideContentType.SetSkillValue));
        if (activeConditionalValueCount > 1) throw new InvalidOperationException("優先順位が曖昧になるため、アクティブ効果の状態別スキル値は1件までです。");
    }

    private static bool HasSingleUnconditional(SkillBody skill, List<EffectContent> candidates, EffectType type)
    {
        return candidates.Count == 1 && skill.Effects.Any(x => x.Type == type && x.Triggers.Count == 0 && x.Contents.Contains(candidates[0]));
    }

    private static void ValidateConsumedItems(SkillBody skill)
    {
        foreach (EffectContent content in skill.Effects.SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.ApplyConsumedItemActive))
        {
            string category = content.Parameters[0];
            var selected = skill.DeclarationConditions.And.Where(x => x.Type == ConditionType.SelectConsumedItem).ToList();
            if (selected.Count != 1 || selected[0].Parameters[0] != category || selected[0].Parameters[1] != "1" || !skill.DeclarationConditions.And.Any(x => x.Type == ConditionType.SelectedItemConditionsMet) || skill.Costs.Count(x => x.Kind == CostKind.ItemCategory && x.Resource == category && x.Amount == 1) != 1)
                throw new InvalidOperationException("消費アイテム効果には、同じカテゴリーの1個選択・宣言条件確認・アイテム×1消費が必要です。");
        }
    }

    private static void ValidateRandomRolls(SkillBody skill)
    {
        var rolls = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (EffectContent content in skill.Effects.SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.RollDice))
        {
            string[] p = Args(content.Parameters, 2, "RollDice"); Number(p[0], 1, "追加ロール番号");
            if (rolls.ContainsKey(p[0])) throw new InvalidOperationException("追加ロール番号が重複しています：" + p[0]);
            rolls.Add(p[0], p[1]);
        }
        foreach (EffectDefinition effect in skill.Effects.Where(x => x.Triggers.Count == 1 && x.Triggers[0].Timing == TriggerTiming.RandomResult))
        {
            ConditionEntry condition = effect.Triggers[0].Conditions.And.Single();
            string[] p = Args(condition.Parameters, 2, "RollResult");
            string formula;
            if (!rolls.TryGetValue(p[0], out formula)) throw new InvalidOperationException("対応する追加ロールがありません：" + p[0]);
            int result = Number(p[1], 1, "出目");
            Match dice = M(formula, @"^1d([0-9]+)$");
            if (dice.Success)
            {
                int sides = Number(dice.Groups[1].Value, 1, "ダイス面数");
                if (result > sides) throw new InvalidOperationException("1d" + sides + "の出目は1～" + sides + "で指定してください。");
            }
        }
    }

    private static void ValidateSpikeStage(SkillBody skill, EffectType stage)
    {
        var entries = skill.Overrides.Where(x => x.Type == stage).SelectMany(x => x.Contents.Select(c => new { Definition = x, Content = c })).ToList();
        if (entries.Count == 0) return;
        var rollEntries = entries.Where(x => x.Content.Type == OverrideContentType.SetActivationRollFormula).ToList();
        if (rollEntries.Count > 0)
        {
            if (entries.Count != 1 || rollEntries[0].Definition.Triggers.Count != 0)
                throw new InvalidOperationException("発動ロールのスパイク変更は各段階に無条件で1件指定してください。");
            return;
        }
        var modifierEntries = entries.Where(x => x.Content.Type == OverrideContentType.SetModifier).ToList();
        if (modifierEntries.Count > 0)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in modifierEntries)
            {
                if (entry.Definition.Triggers.Count != 0) throw new InvalidOperationException("パッシブ補正スパイクには条件を指定できません。");
                if (!keys.Add(SetModifierKey(entry.Content))) throw new InvalidOperationException("同じ補正に対するスパイク変更が重複しています。");
                ValidateSetModifierTarget(skill, entry.Content);
            }
            entries = entries.Where(x => x.Content.Type != OverrideContentType.SetModifier).ToList();
            if (entries.Count == 0) return;
        }
        int reductions = entries.Count(x => x.Content.Type == OverrideContentType.SetDamageReduction);
        if (reductions > 0)
        {
            if (entries.Count != 1 || entries[0].Definition.Triggers.Count != 0) throw new InvalidOperationException("軽減値スパイクは各段階に無条件で1件指定してください。");
            return;
        }
        if (entries.Any(x => x.Content.Type != OverrideContentType.SetSkillValue && x.Content.Type != OverrideContentType.SetAttackComponent))
            throw new InvalidOperationException("攻撃スパイクにはスキル値または攻撃構成要素を指定してください。");
        var skillValues = entries.Where(x => x.Content.Type == OverrideContentType.SetSkillValue).ToList();
        if (skillValues.Count > 0 && skillValues.Count(x => x.Definition.Triggers.Count == 0) != 1) throw new InvalidOperationException("各スパイク段階には無条件の基準スキル値を1件指定してください。");
        if (skillValues.Count(x => x.Definition.Triggers.Count > 0) > 1) throw new InvalidOperationException("優先順位が曖昧になるため、各スパイク段階の状態別スキル値は1件までです。");
        var components = entries.Where(x => x.Content.Type == OverrideContentType.SetAttackComponent).ToList();
        if (components.Count > 0 && (components.Count != 1 || components[0].Definition.Triggers.Count != 0)) throw new InvalidOperationException("各スパイク段階の属性威力は無条件で1件指定してください。");
    }
}
