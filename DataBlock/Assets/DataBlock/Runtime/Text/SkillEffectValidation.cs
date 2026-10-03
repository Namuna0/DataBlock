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
        bool hasUnconditionalAttributeAttack = HasSingleUnconditional(skill, activeAttacks.Where(x =>
            IsElementalWeaponAttack(x) || (x.Type == EffectContentType.SkillAttack &&
            x.Parameters.Count == 4 && x.Parameters[1] == "PowerAndAttribute")).ToList(), EffectType.Active);
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
                if (spike && content.Type != OverrideContentType.OptionalSpikeState && !IsAppliedStateSpike(content.Type) && !IsStateReferenceSpike(content.Type) && content.Type != OverrideContentType.SetSkillValue && content.Type != OverrideContentType.SetDamageReduction && content.Type != OverrideContentType.SetAttackComponent && content.Type != OverrideContentType.SetModifier && content.Type != OverrideContentType.SetActivationRollFormula && content.Type != OverrideContentType.SetActivationRollTarget && content.Type != OverrideContentType.SetRollRange && content.Type != OverrideContentType.StateTurnRecovery)
                    throw new InvalidOperationException("スパイクはスキル値・攻撃構成要素・被ダメージ軽減値・発動ロールの変更に対応します。");
                if (content.Type == OverrideContentType.SetActivationRollFormula &&
                    (skill.Roll.Count <= 0 || skill.Roll.Formula == "自動成功"))
                    throw new InvalidOperationException("発動ロールのスパイク変更には通常の発動ロールが必要です。");
                if (content.Type == OverrideContentType.SetSkillValue && !hasUnconditionalSkillValueAttack)
                    throw new InvalidOperationException("スキル値の上書き先となるアクティブ攻撃を無条件で1件だけ指定してください。");
                if (content.Type == OverrideContentType.SetAttackComponent || content.Type == OverrideContentType.MultiplyAttackComponent)
                {
                    string[] p = Args(content.Parameters, 2, content.Type.ToString());
                    if (p[0] != "AttributePower" && p[0] != "Power") throw new InvalidOperationException("攻撃構成要素はPower / AttributePowerです。");
                    if (!(p[0] == "Power" ? hasUnconditionalActiveAttack : hasUnconditionalAttributeAttack))
                        throw new InvalidOperationException("威力の変更先となる対応アクティブ攻撃を無条件で1件だけ指定してください。");
                }
                if (content.Type == OverrideContentType.SetAttackRule && !hasUnconditionalActiveAttack && !content.Parameters.SequenceEqual(new[] { "Response", "回避", "Prohibit", "ThisSkill" }))
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
                    string costCategory;
                    bool passiveCategoryCost = effect.Type == EffectType.Passive && TryGetCategoryTrigger(effect.Triggers, TriggerTiming.ResourceCost, out costCategory);
                    if (p[3] == "Automatic" && !passiveCategoryCost && (effect.Type != EffectType.Critical || !hasUnconditionalActiveAttack)) throw new InvalidOperationException("自動消費補正には攻撃のCriticalまたはカテゴリー指定Passiveが必要です。");
                    if (p[3] != "Optional" && p[3] != "Automatic") throw new InvalidOperationException("消費補正の適用方法はOptional / Automaticです。");
                }
            }
        }

        ValidatePassiveExtensions(skill);
        ValidateBasicEffectTargets(skill);
        ValidateModifierSpikes(skill);
        ValidateStackAndCategoryEffects(skill);
        ValidateSelectedSkillAndResourceEffects(skill);
        ValidateEquipmentAndItemEffects(skill);
        ValidateTimedAttackEffects(skill);
        ValidateRaceAndRecoveryEffects(skill);
        ValidateDeferredCostAndTimedStacks(skill);
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
        var entries = skill.Overrides.Where(x => x.Type == stage).SelectMany(x => x.Contents.Select(c => new { Definition = x, Content = c }))
            .Where(x => x.Content.Type != OverrideContentType.OptionalSpikeState && x.Content.Type != OverrideContentType.SetRollRange && x.Content.Type != OverrideContentType.StateTurnRecovery && !IsStateReferenceSpike(x.Content.Type) && !IsAppliedStateSpike(x.Content.Type)).ToList();
        if (entries.Count == 0) return;
        var rollEntries = entries.Where(x => x.Content.Type == OverrideContentType.SetActivationRollFormula || x.Content.Type == OverrideContentType.SetActivationRollTarget).ToList();
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
        if (components.Count > 0 && (components.Any(x => x.Definition.Triggers.Count != 0) || components.GroupBy(x => x.Content.Parameters[0]).Any(x => x.Count() > 1))) throw new InvalidOperationException("各スパイク段階の属性威力は無条件で1件指定してください。");
    }

    private static void ValidateEquipmentAndItemEffects(SkillBody skill)
    {
        foreach (var effect in skill.Effects)
        foreach (var content in effect.Contents)
        {
            if (content.Type != EffectContentType.EnvironmentImmunity && content.Type != EffectContentType.RestoreOnAppliedState &&
                content.Type != EffectContentType.ChooseCheckOutcome && content.Type != EffectContentType.GrantItem &&
                content.Type != EffectContentType.RemoveStatesByOrigin && content.Type != EffectContentType.DeclareSelectedSkill &&
                content.Type != EffectContentType.SwapWeapon) continue;
            Require(effect.Triggers.Count == 0);
            EffectType expected = content.Type == EffectContentType.EnvironmentImmunity || content.Type == EffectContentType.RestoreOnAppliedState ? EffectType.Passive :
                content.Type == EffectContentType.GrantItem || content.Type == EffectContentType.SwapWeapon ? EffectType.Active : EffectType.Counter;
            Require(effect.Type == expected);
            if (content.Type == EffectContentType.ChooseCheckOutcome)
                Require(skill.DeclarationConditions.And.Count(x => x.Type == ConditionType.IncapacitatedCheckReaction) == 1);
            if (content.Type == EffectContentType.DeclareSelectedSkill)
            {
                Require(skill.DeclarationConditions.And.Count(x => x.Type == ConditionType.SelectEquipmentSkill && x.Parameters[0] == "1") == 1);
                Require(skill.DeclarationConditions.And.Count(x => x.Type == ConditionType.ActionTowardsSelf) == 1);
            }
            if (content.Type == EffectContentType.SwapWeapon)
                Require(skill.DeclarationConditions.And.Count(x => x.Type == ConditionType.SelectCarriedWeapon) == 1);
        }
        foreach (var effect in skill.Overrides)
        foreach (var content in effect.Contents)
        {
            if (content.Type == OverrideContentType.SetAppliedStateDuration)
                Require(skill.Effects.Where(x => x.Type == EffectType.Active && x.Triggers.Count == 0).SelectMany(x => x.Contents).Count(x => x.Type == EffectContentType.ApplyState && x.Parameters.Count == 3 && x.Parameters[0] == content.Parameters[0] && x.Parameters[1] == content.Parameters[1]) == 1);
            if (content.Type == OverrideContentType.SetOnAppliedStateRecovery)
                Require(skill.Effects.Where(x => x.Type == EffectType.Passive).SelectMany(x => x.Contents).Count(x => x.Type == EffectContentType.RestoreOnAppliedState && x.Parameters[4] == content.Parameters[0]) == 1);
            if (content.Type == OverrideContentType.MultiplyDeclaredSkillPower)
                Require(skill.Effects.Where(x => x.Type == EffectType.Counter && x.Triggers.Count == 0).SelectMany(x => x.Contents).Count(x => x.Type == EffectContentType.DeclareSelectedSkill) == 1);
        }
        foreach (var stage in new[] { EffectType.SecondSpike, EffectType.ThirdSpike })
            Require(!skill.Overrides.Where(x => x.Type == stage).SelectMany(x => x.Contents).Where(x => IsAppliedStateSpike(x.Type)).GroupBy(x => x.Type + ":" + x.Parameters[0]).Any(x => x.Count() > 1));
    }

    private static void ValidateTimedAttackEffects(SkillBody skill)
    {
        var attacks = skill.Effects.Where(e => e.Type == EffectType.Active && e.Triggers.Count == 0)
            .SelectMany(e => e.Contents).Where(c => c.Type == EffectContentType.SkillAttack || c.Type == EffectContentType.WeaponAttack).ToList();
        foreach (var effect in skill.Effects)
        foreach (var content in effect.Contents)
        {
            var rule = SemanticTextRules.FirstOrDefault(r => !r.State && r.Content != EffectContentType.None && r.Content == content.Type);
            if (rule != null) Require(effect.Type == rule.Section && effect.Triggers.Count == 0);
            bool hit = effect.Triggers.Any(t => t.Timing == TriggerTiming.AttackHit);
            bool branch = content.Type == EffectContentType.OwnAttributeHitState || content.Type == EffectContentType.OwnAttributeHitRemoval;
            if (hit || branch || content.Type == EffectContentType.ExecuteAfterAttackDamage || content.Type == EffectContentType.MissingOwnAttributePower)
            {
                Require(attacks.Count == 1 && attacks[0].Parameters[0] == "Target");
                Require(skill.Effects.TakeWhile(e => e != effect).SelectMany(e => e.Contents).Contains(attacks[0]));
                if (hit) Require(content.Type == EffectContentType.ApplyState && content.Parameters[0] == "Target");
            }
            if (content.Type == EffectContentType.MissingOwnAttributePower)
                Require(attacks[0].Type == EffectContentType.SkillAttack && attacks[0].Parameters.Count == 4 && attacks[0].Parameters[1] == "PowerAndAttribute" && attacks[0].Parameters[2] == content.Parameters[0]);
        }
        var branches = skill.Effects.SelectMany(e => e.Contents).Where(c => c.Type == EffectContentType.OwnAttributeHitState || c.Type == EffectContentType.OwnAttributeHitRemoval).ToList();
        Require(!branches.GroupBy(c => c.Parameters[0]).Any(g => g.Count() > 1));
        foreach (var content in skill.Overrides.SelectMany(e => e.Contents))
        {
            if (content.Type == OverrideContentType.ThisSkillCriticalRange || content.Type == OverrideContentType.CriticalThresholdDelta)
                Require(skill.Roll.Count > 0 && skill.Roll.Formula != "自動成功");
            if (content.Type == OverrideContentType.OwnAttributeBonus || content.Type == OverrideContentType.OwnAttributeWeaknessMultiplier)
                Require(skill.Effects.SelectMany(e => e.Contents).Count(c => c.Type == EffectContentType.DefineOwnAttribute) == 1);
        }
        if (skill.DeclarationConditions.And.Any(c => c.Type == ConditionType.AutomaticEnemyAction))
            Require(skill.DeclarationConditions.And.Count == 1 && skill.DeclarationConditions.Or.Count == 0);
    }

    private static void ValidateBasicEffectTargets(SkillBody skill)
    {
        var stacks = skill.Effects.Where(e => (e.Type == EffectType.Active || e.Type == EffectType.Counter) && e.Triggers.Count == 0)
            .SelectMany(e => e.Contents).Where(c => c.Type == EffectContentType.GainStack).ToList();
        var stackOverrides = skill.Overrides.SelectMany(e => e.Contents
            .Where(c => c.Type == OverrideContentType.SetStackAmount).Select(c => new { Stage = e.Type, Content = c })).ToList();
        foreach (var change in stackOverrides)
        {
            string[] p = Args(change.Content.Parameters, 3, "SetStackAmount");
            if (stacks.Count(c => c.Parameters[0] == p[0] && c.Parameters[1] == p[1]) != 1)
                throw new InvalidOperationException("スタック数変更には同じ対象・状態への無条件の付与を1件指定してください。");
        }
        if (stackOverrides.GroupBy(c => c.Stage + "\u001f" + c.Content.Parameters[0] + "\u001f" + c.Content.Parameters[1]).Any(g => g.Count() > 1))
            throw new InvalidOperationException("スタック数変更が重複しています。");
        foreach (var effect in skill.Effects)
        foreach (var content in effect.Contents)
        {
            if (content.Type == EffectContentType.RoleplayDescription && (effect.Type != EffectType.Roleplay || effect.Triggers.Count != 0))
                throw new InvalidOperationException("説明文はロールプレイ効果にのみ使用できます。");
            if (content.Type == EffectContentType.ConsumeStacksForDamage)
            {
                if (effect.Type != EffectType.Active || effect.Triggers.Count != 0 ||
                    skill.Effects.Where(e => e.Type == EffectType.Active && e.Triggers.Count == 0).SelectMany(e => e.Contents)
                    .Count(c => (c.Type == EffectContentType.SkillAttack || c.Type == EffectContentType.WeaponAttack) && c.Parameters[0] == content.Parameters[0]) != 1)
                    throw new InvalidOperationException("スタック消費による被ダメージ補正には同じ対象のアクティブ攻撃を1件指定してください。");
            }
            if (content.Type == EffectContentType.RemoveSummon &&
                (!(skill is SummonedEntityDefinition) || !skill.Categories.Contains("罠") || effect.Type != EffectType.Declaration || !Matches(effect.Triggers, Trigger(TriggerTiming.AfterSkillResolution))))
                throw new InvalidOperationException("罠除去は罠の宣言効果・スキル処理後に指定してください。");
            if (content.Type == EffectContentType.ProhibitAcquisition && content.Parameters.Skip(2).Distinct(StringComparer.Ordinal).Count() != content.Parameters.Count - 2)
                throw new InvalidOperationException("習得不能スキルが重複しています。");
        }
    }

    private static void ValidateDeferredCostAndTimedStacks(SkillBody skill)
    {
        var attacks = skill.Effects.Where(e => e.Type == EffectType.Active && e.Triggers.Count == 0)
            .SelectMany(e => e.Contents).Where(c => c.Type == EffectContentType.SkillAttack || c.Type == EffectContentType.WeaponAttack).ToList();
        foreach (var effect in skill.Effects)
        foreach (var content in effect.Contents)
        {
            if (content.Type == EffectContentType.RemoveAllStacks)
                Require(attacks.Count == 1 && skill.Effects.TakeWhile(e => e != effect).SelectMany(e => e.Contents).Contains(attacks[0]));
            if (content.Type == EffectContentType.DeferMissingResourceCost)
            {
                Require(skill.Costs.Count(c => c.Kind == CostKind.Resource && c.Resource == content.Parameters[0] && c.Amount > 0) == 1);
                Require(skill.Effects.Any(e => e.Type == EffectType.Active));
            }
            if (content.Type == EffectContentType.SucceedFailedActivationRoll)
                Require(skill.DeclarationConditions.And.Any(c => c.Type == ConditionType.RollResult && c.Parameters.SequenceEqual(new[] { "Activation", "NormalFailure" })));
        }
        var costs = skill.Overrides.SelectMany(e => e.Contents).Where(c => c.Type == OverrideContentType.ThisSkillStackCost).ToList();
        Require(!costs.GroupBy(c => c.Parameters[1]).Any(g => g.Count() > 1));
        foreach (var cost in costs)
            Require(skill.Costs.Count(c => c.Kind == CostKind.Resource && c.Resource == cost.Parameters[1] && c.Amount >= Number(cost.Parameters[3], 1, "最低消費")) == 1);
        foreach (var content in skill.Overrides.SelectMany(e => e.Contents).Where(c => c.Type == OverrideContentType.ThisSkillStackPower))
            Require(attacks.Count == 1);
        foreach (var stage in new[] { EffectType.SecondSpike, EffectType.ThirdSpike })
            Require(skill.Overrides.Where(e => e.Type == stage).SelectMany(e => e.Contents).Count(c => c.Type == OverrideContentType.OptionalSpikeState) <= 1);
    }

    private static bool ValidateModifierRuleTarget(SkillBody skill, OverrideContent content)
    {
        if (content.Parameters == null || content.Parameters.Count == 0 || content.Parameters[0] != "Rule") return false;
        string[] p = VariableArgs(content.Parameters, 3, "SetModifier/Rule");
        string[] rule = p.Skip(1).Take(p.Length - 2).ToArray();
        if (rule[0] == "RerollResult")
        {
            RuleLength(rule, 2); PositiveModifierFactor(p.Last());
            Require(skill.Effects.Where(e => e.Type == EffectType.Counter && e.Triggers.Count == 0).SelectMany(e => e.Contents)
                .Count(c => c.Type == EffectContentType.RerollActivation && c.Parameters.SequenceEqual(new[] { "Self", rule[1], "TriggeringRoll" })) == 1);
            return true;
        }
        CreateModifierRule(rule, p.Last());
        int count = skill.Overrides.Where(e => e.Type == EffectType.Passive || e.Type == EffectType.Active)
            .Sum(e => e.Contents.Count(c => { string[] candidate = ModifierRuleSelector(e, c); return candidate != null && candidate.SequenceEqual(rule); }));
        if (count != 1) throw new InvalidOperationException("スパイクの対象となる基本補正を1件指定してください：" + string.Join(" / ", rule));
        return true;
    }

    private static void ValidateModifierSpikes(SkillBody skill)
    {
        foreach (var effect in skill.Effects)
        foreach (var content in effect.Contents)
        {
            if (content.Type == EffectContentType.RerollActivation)
            {
                Require(effect.Type == EffectType.Counter && effect.Triggers.Count == 0);
                Require(skill.DeclarationConditions.And.Any(c => c.Type == ConditionType.RollResult && c.Parameters.SequenceEqual(new[] { "Activation", "NormalFailure" })));
            }
            if (content.Type == EffectContentType.SetResourceValue || content.Type == EffectContentType.SkipRoll)
                Require(effect.Type == EffectType.Counter && effect.Triggers.Count == 0);
            if (content.Type == EffectContentType.SkipRoll)
                Require(skill.Effects.Where(e => e.Type == EffectType.Counter && e.Triggers.Count == 0).SelectMany(e => e.Contents).Count(c => c.Type == EffectContentType.SetResourceValue) == 1);
            if (content.Type == EffectContentType.LimitAcquisition || content.Type == EffectContentType.RaceAlias)
                Require(effect.Type == EffectType.Passive && effect.Triggers.Count == 0);
        }
        foreach (var effect in skill.Overrides)
        foreach (var content in effect.Contents)
        {
            if (content.Type == OverrideContentType.SetActivationRollTarget)
                Require(skill.Roll.Count > 0 && skill.Roll.Formula != "自動成功" && !string.IsNullOrWhiteSpace(skill.Roll.Target));
        }
    }

    private static void ValidateStackAndCategoryEffects(SkillBody skill)
    {
        foreach (var effect in skill.Effects)
        foreach (var c in effect.Contents)
        {
            if (c.Type == EffectContentType.TransformAtStacks)
            {
                Require(effect.Type == EffectType.Active && effect.Triggers.Count == 0);
                Require(skill.Effects.TakeWhile(e => e != effect).SelectMany(e => e.Contents).Count(x => x.Type == EffectContentType.GainStack && x.Parameters[0] == "Self" && x.Parameters[1] == c.Parameters[1]) == 1);
            }
            if (c.Type == EffectContentType.CharacterRule || c.Type == EffectContentType.CyclingStateStacks || c.Type == EffectContentType.RerollGathering || c.Type == EffectContentType.EquipSlotSubstitution || c.Type == EffectContentType.PreventMealPenalties || c.Type == EffectContentType.GrantCreationChoice || c.Type == EffectContentType.GrantRaceTrait || c.Type == EffectContentType.OptionalInvalidateAction)
                Require(effect.Type == EffectType.Passive && effect.Triggers.Count == 0);
            if (c.Type == EffectContentType.DrainResource || c.Type == EffectContentType.CreateMeleeGroup)
                Require(effect.Type == EffectType.Active && effect.Triggers.Count == 0);
            if (c.Type == EffectContentType.InvalidateTriggeredEffect && c.Parameters.SequenceEqual(new[] { "Target", "Counter" }))
                Require(effect.Type == EffectType.Counter && skill.DeclarationConditions.And.Any(x => x.Type == ConditionType.CounterToOwnActive));
        }
        foreach (var effect in skill.Overrides)
        foreach (var c in effect.Contents)
        {
            if (c.Type == OverrideContentType.MultiplyDamageReduction)
                Require(skill.Effects.Where(e => e.Type == EffectType.Counter && e.Triggers.Count == 0).SelectMany(e => e.Contents).Count(x => x.Type == EffectContentType.ReduceDamage) == 1);
            if (c.Type == OverrideContentType.SetRollRange && effect.Type != EffectType.Passive)
                Require(skill.Overrides.Where(e => e.Type == EffectType.Passive).SelectMany(e => e.Contents).Count(x => x.Type == OverrideContentType.SetRollRange && x.Parameters[1] == c.Parameters[1]) == 1);
            if (c.Type == OverrideContentType.StateTurnRecovery)
                Require(skill.Effects.SelectMany(e => e.Contents).Any(x => x.Type == EffectContentType.TransformAtStacks && x.Parameters[6] == c.Parameters[0]));
        }
        foreach (var stage in new[] { EffectType.Passive, EffectType.SecondSpike, EffectType.ThirdSpike })
        {
            var special = skill.Overrides.Where(e => e.Type == stage).SelectMany(e => e.Contents)
                .Where(c => c.Type == OverrideContentType.SetRollRange || c.Type == OverrideContentType.StateTurnRecovery);
            Require(!special.GroupBy(c => c.Type + ":" + (c.Type == OverrideContentType.SetRollRange ? c.Parameters[1] : c.Parameters[0] + ":" + c.Parameters[1])).Any(g => g.Count() > 1));
        }
    }

    private static void ValidateRaceAndRecoveryEffects(SkillBody skill)
    {
        var attacks = skill.Effects.Where(e => e.Type == EffectType.Active && e.Triggers.Count == 0).SelectMany(e => e.Contents)
            .Where(c => (c.Type == EffectContentType.SkillAttack || c.Type == EffectContentType.WeaponAttack) && c.Parameters[0] == "Target").ToList();
        foreach (var effect in skill.Effects)
        foreach (var content in effect.Contents)
        {
            if (content.Type == EffectContentType.HitRaceState || content.Type == EffectContentType.RestoreFromDamage)
            {
                Require(attacks.Count == 1 && skill.Effects.TakeWhile(e => e != effect).SelectMany(e => e.Contents).Contains(attacks[0]));
                if (content.Type == EffectContentType.RestoreFromDamage) Require(effect.Type == EffectType.Active && Matches(effect.Triggers, ThisAttackDamageTriggers().ToArray()));
            }
            if (content.Type == EffectContentType.SkillAttack && content.Parameters[0] == "AllBattleCharacters")
                Require(effect.Type == EffectType.Counter && effect.Triggers.Count == 0);
        }
        foreach (var content in skill.Overrides.SelectMany(e => e.Contents).Where(c => c.Type == OverrideContentType.PowerAgainstState))
            Require(attacks.Count == 1);
    }

    private static void ValidateSelectedSkillAndResourceEffects(SkillBody skill)
    {
        foreach (var effect in skill.Effects)
        foreach (var content in effect.Contents)
        {
            if (content.Type == EffectContentType.LeaveBattle || content.Type == EffectContentType.PreventRollAtResource ||
                content.Type == EffectContentType.CategoryImmunity || content.Type == EffectContentType.ProhibitCategoryTarget ||
                content.Type == EffectContentType.ConvertResourceCost || content.Type == EffectContentType.AddQuestReward ||
                content.Type == EffectContentType.ApplySelectedSkillState || content.Type == EffectContentType.ProhibitSelectedSkill ||
                content.Type == EffectContentType.ResourceDamage || content.Type == EffectContentType.RestoreFromResourceDamage ||
                content.Type == EffectContentType.GrantActionControl || content.Type == EffectContentType.DelayedStateAttack ||
                content.Type == EffectContentType.TurnStartStateStacks)
            {
                Require(effect.Triggers.Count == 0);
                bool active = content.Type == EffectContentType.LeaveBattle || content.Type == EffectContentType.ApplySelectedSkillState || content.Type == EffectContentType.ResourceDamage || content.Type == EffectContentType.RestoreFromResourceDamage;
                Require(effect.Type == (active ? EffectType.Active : EffectType.Passive));
                Require(content.Type != EffectContentType.GrantActionControl && content.Type != EffectContentType.DelayedStateAttack && content.Type != EffectContentType.ProhibitSelectedSkill);
            }
            if (content.Type == EffectContentType.ApplySelectedSkillState)
                Require(skill.DeclarationConditions.And.Count(x => x.Type == ConditionType.SelectCharacterSkill) == 1);
            if (content.Type == EffectContentType.RestoreFromResourceDamage)
                Require(skill.Effects.Where(x => x.Type == effect.Type).SelectMany(x => x.Contents).Count(x => x.Type == EffectContentType.ResourceDamage && x.Parameters[1] == content.Parameters[1]) == 1);
        }
        foreach (var effect in skill.Overrides)
        foreach (var content in effect.Contents)
        {
            if (content.Type == OverrideContentType.SetConditionalStatePower)
                Require(skill.Overrides.Where(x => x.Type == EffectType.Passive).SelectMany(x => x.Contents).Count(x => x.Type == OverrideContentType.ConditionalStatePower) == 1);
            if (content.Type == OverrideContentType.SetCooldown && IsSpikeStage(effect.Type)) Require(skill.CooldownTurns > 0);
            if (content.Type == OverrideContentType.MultiplyRecovery && effect.Type == EffectType.Critical && effect.Triggers.Count == 0)
                Require(skill.Effects.Where(x => x.Type == EffectType.Active).SelectMany(x => x.Contents).Count(x => x.Type == EffectContentType.RestoreResource) == 1);
        }
        foreach (var stage in new[] { EffectType.SecondSpike, EffectType.ThirdSpike })
            Require(!skill.Overrides.Where(x => x.Type == stage).SelectMany(x => x.Contents).Where(x => IsStateReferenceSpike(x.Type)).GroupBy(x => x.Type).Any(x => x.Count() > 1));
    }

}
