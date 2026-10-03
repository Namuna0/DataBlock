using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static bool ReadEquipmentAndItemModifier(SkillBody skill, EffectType type, string s)
    {
        Match m = M(s, @"^戦闘開始の([0-9]+)ターン目に発動した場合、スキル値は([0-9]+)に上昇する。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.SkillValue, Condition(ConditionType.TurnNumber, m.Groups[1].Value))), Change(OverrideContentType.SetSkillValue, m.Groups[2].Value)); return true; }
        m = M(s, @"^このアクティブ効果による《([^》]+)》付与は([0-9]+)ターンに変化する。?$");
        if (m.Success && IsSpikeStage(type)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetAppliedStateDuration, "Target", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^(.+?)効果による自身の被ダメージ軽減が×([0-9.]+)倍に変化する。?$");
        if (m.Success && IsSpikeStage(type)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetStateDamageMultiplier, m.Groups[1].Value, "Self", m.Groups[2].Value)); return true; }
        m = M(s, @"^このパッシブ効果による([A-Z]+)は\1最大値の([0-9]+)%に変化する。?$");
        if (m.Success && IsSpikeStage(type)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetOnAppliedStateRecovery, m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^自身の〈([^〉]+)〉がクリティカルした時、(威力|達成値)は×([0-9.]+)倍される。（他効果と重複する）$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.CriticalCategoryMultiplier, m.Groups[1].Value, m.Groups[2].Value == "威力" ? "Power" : "ActionResult", m.Groups[3].Value, "Stack")); return true; }
        m = M(s, @"^そのクリティカル時の達成値は([0-9.]+)倍される。?$");
        if (m.Success)
        {
            var power = skill.Overrides.Where(x => x.Type == type).SelectMany(x => x.Contents).LastOrDefault(x => x.Type == OverrideContentType.CriticalCategoryMultiplier && x.Parameters[1] == "Power");
            Require(power != null); AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.CriticalCategoryMultiplier, power.Parameters[0], "ActionResult", m.Groups[1].Value, "Stack")); return true;
        }
        m = M(s, @"^自身が使用した持続効果を持つアイテムの効果ターンが\+([0-9]+)される。（他キャラクターに対しても有効）$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.ItemEffectDuration, "Self", "AllTargets", m.Groups[1].Value)); return true; }
        m = M(s, @"^サイズ(.+?)のアイテムを最大([0-9]+)個まで、サイズ(.+?)のアイテムを最大([0-9]+)個まで所持する事が出来ます。?$");
        if (m.Success)
        { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.ItemCapacity, m.Groups[1].Value, m.Groups[2].Value), Change(OverrideContentType.ItemCapacity, m.Groups[3].Value, m.Groups[4].Value)); return true; }
        m = M(s, @"^サイズ(.+?)のアイテムを最大([0-9]+)個まで所持する事が出来ます。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.ItemCapacity, m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^エリアの進行ロールの回数を-([0-9]+)しても良い。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.OptionalAreaProgressReduction, m.Groups[1].Value, "Unspecified", "Unspecified", "Optional")); return true; }
        m = M(s, @"^エリアに〈([^〉]+)〉を含む場合、エリアの進行ロールの減少回数が-([0-9]+)に変化する。?$");
        if (m.Success)
        {
            var reduction = skill.Overrides.Where(x => x.Type == type).SelectMany(x => x.Contents).LastOrDefault(x => x.Type == OverrideContentType.OptionalAreaProgressReduction);
            Require(reduction != null && reduction.Parameters[1] == "Unspecified"); reduction.Parameters[1] = m.Groups[1].Value; reduction.Parameters[2] = m.Groups[2].Value; return true;
        }
        m = M(s, @"^選択して宣言するスキルの威力は×([0-9.]+)倍される。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.MultiplyDeclaredSkillPower, m.Groups[1].Value)); return true; }
        return false;
    }

    private static string EquipmentAndItemModifierText(OverrideDefinition effect, OverrideContent content)
    {
        if (effect == null || content == null) return null;
        var t = effect.Triggers;
        string[] p;
        if (content.Type == OverrideContentType.SetSkillValue && effect.Type == EffectType.Active && t.Count == 1 && t[0].Conditions.And.Count == 1 && t[0].Conditions.And[0].Type == ConditionType.TurnNumber)
        {
            string turn = Args(t[0].Conditions.And[0].Parameters, 1, "TurnNumber")[0]; Number(turn, 1, "ターン"); Require(Matches(t, Trigger(TriggerTiming.SkillValue, Condition(ConditionType.TurnNumber, turn))));
            return "戦闘開始の" + turn + "ターン目に発動した場合、スキル値は" + Args(content.Parameters, 1, "SetSkillValue")[0] + "に上昇する。";
        }
        if (content.Type != OverrideContentType.SetAppliedStateDuration && content.Type != OverrideContentType.SetStateDamageMultiplier &&
            content.Type != OverrideContentType.SetOnAppliedStateRecovery && content.Type != OverrideContentType.CriticalCategoryMultiplier &&
            content.Type != OverrideContentType.ItemEffectDuration && content.Type != OverrideContentType.ItemCapacity &&
            content.Type != OverrideContentType.OptionalAreaProgressReduction && content.Type != OverrideContentType.MultiplyDeclaredSkillPower) return null;
        Require(t.Count == 0);
        switch (content.Type)
        {
            case OverrideContentType.SetAppliedStateDuration:
                p = Args(content.Parameters, 3, "SetAppliedStateDuration"); Require(IsSpikeStage(effect.Type) && p[0] == "Target"); Number(p[2], 1, "持続ターン");
                return "このアクティブ効果による《" + p[1] + "》付与は" + p[2] + "ターンに変化する。";
            case OverrideContentType.SetStateDamageMultiplier:
                p = Args(content.Parameters, 3, "SetStateDamageMultiplier"); Require(IsSpikeStage(effect.Type) && p[1] == "Self"); PositiveModifierFactor(p[2]);
                return p[0] + "効果による自身の被ダメージ軽減が×" + p[2] + "倍に変化する。";
            case OverrideContentType.SetOnAppliedStateRecovery:
                p = Args(content.Parameters, 2, "SetOnAppliedStateRecovery"); Require(IsSpikeStage(effect.Type)); Number(p[1], 1, "回復率");
                return "このパッシブ効果による" + p[0] + "は" + p[0] + "最大値の" + p[1] + "%に変化する。";
            case OverrideContentType.CriticalCategoryMultiplier:
                p = Args(content.Parameters, 4, "CriticalCategoryMultiplier"); Require(effect.Type == EffectType.Passive && (p[1] == "Power" || p[1] == "ActionResult") && p[3] == "Stack"); PositiveModifierFactor(p[2]);
                return "自身の〈" + p[0] + "〉がクリティカルした時、" + (p[1] == "Power" ? "威力" : "達成値") + "は×" + p[2] + "倍される。（他効果と重複する）";
            case OverrideContentType.ItemEffectDuration:
                p = Args(content.Parameters, 3, "ItemEffectDuration"); Require(effect.Type == EffectType.Passive && p[0] == "Self" && p[1] == "AllTargets"); Number(p[2], 1, "追加ターン");
                return "自身が使用した持続効果を持つアイテムの効果ターンが+" + p[2] + "される。（他キャラクターに対しても有効）";
            case OverrideContentType.ItemCapacity:
                p = Args(content.Parameters, 2, "ItemCapacity"); Require(effect.Type == EffectType.Passive); Number(p[1], 1, "所持上限");
                return "サイズ" + p[0] + "のアイテムを最大" + p[1] + "個まで所持する事が出来ます。";
            case OverrideContentType.OptionalAreaProgressReduction:
                p = Args(content.Parameters, 4, "OptionalAreaProgressReduction"); Require(effect.Type == EffectType.Passive && p[1] != "Unspecified" && p[3] == "Optional"); Number(p[0], 0, "通常軽減回数"); Number(p[2], 1, "条件付き軽減回数");
                if (p[1] == "Any") { Require(p[0] == p[2]); return "エリアの進行ロールの回数を-" + p[0] + "しても良い。"; }
                if (p[0] == "0") return "エリアに〈" + p[1] + "〉を含む場合、エリアの進行ロールの回数を-" + p[2] + "しても良い。";
                return "エリアの進行ロールの回数を-" + p[0] + "しても良い。\nエリアに〈" + p[1] + "〉を含む場合、エリアの進行ロールの減少回数が-" + p[2] + "に変化する。";
            case OverrideContentType.MultiplyDeclaredSkillPower:
                p = Args(content.Parameters, 1, "MultiplyDeclaredSkillPower"); Require(effect.Type == EffectType.Critical); PositiveModifierFactor(p[0]);
                return "選択して宣言するスキルの威力は×" + p[0] + "倍される。";
            default: return null;
        }
    }

    private static string TimedAttackModifierText(OverrideDefinition effect, OverrideContent content)
    {
        string rule = SemanticRuleOverrideText(effect, content, false);
        if (rule != null) return rule;
        if (content == null || effect == null) return null;
        var t = effect.Triggers;
        if (content.Type == OverrideContentType.MultiplyPower)
        {
            if (effect.Type == EffectType.Passive && Matches(t, Trigger(TriggerTiming.AttackPower, Condition(ConditionType.ActionSource, "Self"))))
                return "自身のあらゆる威力は×" + Args(content.Parameters, 1, "MultiplyPower")[0] + "倍される。";
            if (effect.Type == EffectType.Active && t.Count == 1 && ValidTrigger(t[0]) && t[0].Conditions.And.Count == 1 && t[0].Conditions.And[0].Type == ConditionType.TurnNumber)
            {
                string turn = Args(t[0].Conditions.And[0].Parameters, 1, "TurnNumber")[0]; Number(turn, 1, "ターン");
                Require(Matches(t, Trigger(TriggerTiming.AttackPower, Condition(ConditionType.TurnNumber, turn))));
                return "戦闘開始の" + turn + "ターン目に発動した場合、威力は×" + Args(content.Parameters, 1, "MultiplyPower")[0] + "倍される。";
            }
        }
        return null;
    }

    private static string BasicModifierText(OverrideDefinition effect, OverrideContent content)
    {
        if (effect == null || content == null || effect.Triggers == null) return null;
        var t = effect.Triggers; string[] p;
        if (content.Type == OverrideContentType.MultiplyResourceDamage)
        {
            p = Args(content.Parameters, 4, "MultiplyResourceDamage"); Require(effect.Type == EffectType.Passive && p[0] == "AllAlliesExceptSelf" && p[3] == "Optional" && Matches(t, Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.InParty, "Self"))));
            return "自身がパーティーを組んでいる時、自身を除く味方の受ける" + p[1] + "ダメージを×" + p[2] + "倍しても良い。";
        }
        if (content.Type == OverrideContentType.SetStackAmount)
        {
            p = Args(content.Parameters, 3, "SetStackAmount"); Require(effect.Type == EffectType.Critical && t.Count == 0); Number(p[2], 1, "スタック数"); return Actor(p[0]) + "へ付与する《" + p[1] + "》スタックの付与が" + p[2] + "に変化する。";
        }
        if (content.Type == OverrideContentType.SetAttackComponent && content.Parameters != null && content.Parameters.Count == 2 && content.Parameters[0] == "Power")
        {
            p = Args(content.Parameters, 2, "SetAttackComponent"); Require(t.Count == 0 && (effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike)); return "このアクティブ効果による威力は" + p[1] + "に変化する。";
        }
        if (content.Type == OverrideContentType.AddActionResult && t.Count == 1 && t[0].Conditions.And.Count == 2)
        {
            var c = t[0].Conditions.And;
            if (c[0].Type == ConditionType.ActionSource && c[0].Parameters.SequenceEqual(new[] { "AllAlliesExceptSelf" }) && c[1].Type == ConditionType.ActionStat)
            {
                Require(effect.Type == EffectType.Passive && t[0].Timing == TriggerTiming.ActionResult && t[0].Conditions.Or.Count == 0);
                return "自身を除く味方全員の[" + Args(c[1].Parameters, 1, "ActionStat")[0] + "]による行為判定の達成値が" + Signed(Args(content.Parameters, 1, "AddActionResult")[0]) + "される。";
            }
        }
        return BasicStateModifierText(effect, content);
    }

    private static string ActionResultModifierText(OverrideDefinition effect, OverrideContent content)
    {
        if (content == null || effect == null) return null;
        if (content.Type == OverrideContentType.OptionalSpikeState)
        {
            var p = Args(content.Parameters, 5, "OptionalSpikeState"); Require(IsSpikeStage(effect.Type) && effect.Triggers.Count == 0 && p[0] != p[1]); Number(p[2], 1, "各消費量"); Number(p[4], 1, "戦闘回数");
            return "戦闘中" + p[4] + "度だけ、" + p[0] + "及び" + p[1] + "を" + p[2] + "消費する事で、自身に《" + p[3] + "》状態を付与しても良い。";
        }
        if (content.Type != OverrideContentType.AddActionResult || effect.Type != EffectType.Passive) return null;
        string amount = Signed(Args(content.Parameters, 1, "AddActionResult")[0]);
        var t = effect.Triggers;
        if (Matches(t, Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"))))
            return "自身が行為判定を行う際、達成値が" + amount + "される。";
        if (t == null || t.Count == 0 || t.Any(x => !ValidTrigger(x) || x.Conditions.And.Count != 2 || x.Conditions.And[1].Type != ConditionType.ActionCategory)) return null;
        var categories = t.Select(x => Args(x.Conditions.And[1].Parameters, 1, "ActionCategory")[0]).ToArray();
        if (!Matches(t, categories.Select(x => Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, x))).ToArray())) return null;
        return CategoryAlternatives(categories, "及び") + "の達成値が" + amount + "される。";
    }

    private static OverrideDefinition CreateModifierRule(string[] rule, string value)
    {
        Need(value, "補正値");
        var result = new OverrideDefinition { Type = EffectType.Passive };
        OverrideContent content;
        switch (rule[0])
        {
            case "WeaponActivation":
                RuleLength(rule, 2);
                result.Triggers = SkillWeaponPowerTriggers(rule[1]);
                result.Triggers[0].Timing = TriggerTiming.ActionResult;
                result.Triggers[0].Conditions.And.Add(Condition(ConditionType.CheckContext, "Activation"));
                content = Change(OverrideContentType.MultiplyActionResult, value); break;
            case "ActionStats":
                Require(rule.Length >= 2);
                result.Triggers = On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionStat, rule.Skip(1).ToArray())));
                content = Change(OverrideContentType.MultiplyActionResult, value); break;
            case "EnvironmentCheck":
                RuleLength(rule, 1);
                result.Triggers = On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.CheckContext, "Environment")));
                content = Change(OverrideContentType.AddActionResult, Signed(value)); break;
            case "CraftCategories":
                Require(rule.Length >= 2);
                result.Triggers = On(Trigger(TriggerTiming.TargetValue, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.CheckContext, new[] { "Crafting" }.Concat(rule.Skip(1)).ToArray())));
                content = Change(OverrideContentType.AddTargetValue, Signed(value)); break;
            case "TimedWeaponActivation":
                RuleLength(rule, 3); result.Type = EffectType.Active;
                content = Change(OverrideContentType.AddTimedActionResult, "Self", "CurrentTurn", "WeaponPower", rule[1], rule[2], Signed(value)); break;
            case "AcquisitionCost":
                RuleLength(rule, 4);
                Require(rule[1] == "Add" || rule[1] == "Multiply");
                Require(rule[2] == "AllSkills" || rule[2] == "Class" || rule[2] == "SkillCategory");
                if (rule[2] == "AllSkills") Require(rule[3] == "All");
                content = Change(OverrideContentType.ModifyAcquisitionCost, "Self", "決意", rule[1], rule[2], rule[3], rule[1] == "Add" ? Signed(value) : value); break;
            case "DailyGathering":
                RuleLength(rule, 1);
                content = Change(OverrideContentType.ModifyStat, "Self", "一日の採取回数上限", "Add", Signed(value)); break;
            case "EarlyBattleActivation":
                RuleLength(rule, 3); Number(rule[1], 1, "持続ターン");
                result.Triggers = On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionStat, rule[2]), Condition(ConditionType.CheckContext, "Activation"), Condition(ConditionType.BattleTurnRange, "1", rule[1])));
                content = Change(OverrideContentType.AddActionResult, Signed(value)); break;
            case "EarlyBattleDamage":
                RuleLength(rule, 2); Number(rule[1], 1, "持続ターン");
                result.Triggers = On(Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.BattleTurnRange, "1", rule[1])));
                content = Change(OverrideContentType.MultiplyDamageTaken, "Self", value); break;
            case "ResourceMaximum":
                RuleLength(rule, 2);
                content = Change(OverrideContentType.ModifyStat, "Self", rule[1] + "最大値", "Multiply", value); break;
            case "SalePrice":
                RuleLength(rule, 2);
                content = Change(OverrideContentType.MultiplySalePrice, "NPC", "ExceptCategory", rule[1], value); break;
            case "CategoryPower":
                RuleLength(rule, 2); result.Triggers = CategoryPowerTriggers(rule[1]);
                content = Change(OverrideContentType.MultiplyPower, value); break;
            case "CategoryHealing":
                RuleLength(rule, 2);
                result.Triggers = On(Trigger(TriggerTiming.ResourceRecovery, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, rule[1]), Condition(ConditionType.EffectKind, "RestoreResource")));
                content = Change(OverrideContentType.MultiplyRecovery, value); break;
            case "CategoryCheck":
                RuleLength(rule, 2);
                result.Triggers = On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, rule[1])));
                content = Change(OverrideContentType.AddActionResult, Signed(value)); break;
            default: throw new InvalidOperationException("未対応の補正対象です：" + rule[0]);
        }
        decimal numeric;
        if (!decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out numeric))
            throw new InvalidOperationException("補正値には数値を指定してください。");
        if ((content.Type == OverrideContentType.MultiplyActionResult || content.Type == OverrideContentType.MultiplyDamageTaken ||
             content.Type == OverrideContentType.MultiplyPower || content.Type == OverrideContentType.MultiplyRecovery ||
             content.Type == OverrideContentType.MultiplySalePrice || rule[0] == "ResourceMaximum" ||
             (rule[0] == "AcquisitionCost" && rule[1] == "Multiply")) && numeric <= 0)
            throw new InvalidOperationException("倍率は0より大きい値を指定してください。");
        result.Contents.Add(content);
        return result;
    }

    private static void RuleLength(string[] rule, int length)
    {
        Require(rule.Length == length);
        foreach (string part in rule) Need(part, "補正対象");
    }

    private static string[] ModifierRuleSelector(OverrideDefinition effect, OverrideContent content)
    {
        if (effect == null || content == null || content.Parameters == null || effect.Triggers == null ||
            effect.Triggers.Any(t => !ValidTrigger(t))) return null;
        var p = content.Parameters;
        var conditions = effect.Triggers.SelectMany(t => t.Conditions.And).ToList();
        Func<ConditionType, string[]> get = kind => conditions.FirstOrDefault(c => c.Type == kind)?.Parameters?.ToArray();
        string[] stat = get(ConditionType.ActionStat), context = get(ConditionType.CheckContext),
            turn = get(ConditionType.BattleTurnRange), category = get(ConditionType.ActionCategory), weapon = get(ConditionType.WeaponCategory);
        string[] rule = null;
        switch (content.Type)
        {
            case OverrideContentType.MultiplyActionResult:
                if (weapon != null && weapon.Length == 1) rule = new[] { "WeaponActivation", weapon[0] };
                else if (stat != null) rule = new[] { "ActionStats" }.Concat(stat).ToArray();
                break;
            case OverrideContentType.AddActionResult:
                if (turn != null && turn.Length == 2 && stat != null && stat.Length == 1) rule = new[] { "EarlyBattleActivation", turn[1], stat[0] };
                else if (context != null && context.SequenceEqual(new[] { "Environment" })) rule = new[] { "EnvironmentCheck" };
                else if (category != null && category.Length == 1) rule = new[] { "CategoryCheck", category[0] };
                break;
            case OverrideContentType.AddTargetValue:
                if (context != null && context.Length >= 2 && context[0] == "Crafting") rule = new[] { "CraftCategories" }.Concat(context.Skip(1)).ToArray();
                break;
            case OverrideContentType.AddTimedActionResult:
                if (p.Count == 6) rule = new[] { "TimedWeaponActivation", p[3], p[4] }; break;
            case OverrideContentType.ModifyAcquisitionCost:
                if (p.Count == 6) rule = new[] { "AcquisitionCost", p[2], p[3], p[4] }; break;
            case OverrideContentType.ModifyStat:
                if (p.Count == 4 && p[1] == "一日の採取回数上限" && p[2] == "Add") rule = new[] { "DailyGathering" };
                else if (p.Count == 4 && p[1].EndsWith("最大値", StringComparison.Ordinal) && p[2] == "Multiply") rule = new[] { "ResourceMaximum", p[1].Substring(0, p[1].Length - 3) };
                break;
            case OverrideContentType.MultiplyDamageTaken:
                if (turn != null && turn.Length == 2) rule = new[] { "EarlyBattleDamage", turn[1] }; break;
            case OverrideContentType.MultiplySalePrice:
                if (p.Count == 4) rule = new[] { "SalePrice", p[2] }; break;
            case OverrideContentType.MultiplyPower:
                if (category != null && category.Length == 1) rule = new[] { "CategoryPower", category[0] }; break;
            case OverrideContentType.MultiplyRecovery:
                if (category != null && category.Length == 1) rule = new[] { "CategoryHealing", category[0] }; break;
        }
        if (rule == null || p.Count == 0) return null;
        OverrideDefinition expected;
        try { expected = CreateModifierRule(rule, p.Last()); }
        catch (InvalidOperationException) { return null; } // Other codecs also support formula-valued modifiers.
        return effect.Type == expected.Type && Matches(effect.Triggers, expected.Triggers.ToArray()) &&
            content.Type == expected.Contents[0].Type && p.SequenceEqual(expected.Contents[0].Parameters) ? rule : null;
    }

    private static string ModifierRuleText(string[] r, string value)
    {
        CreateModifierRule(r, value); // Validate both the selector and value before rendering.
        switch (r[0])
        {
            case "WeaponActivation": return "スキルによって〈" + r[1] + "〉で武器攻撃を行う時、発動ロール達成値が×" + value + "倍される。";
            case "ActionStats": return string.Join("および", r.Skip(1).Select(s => "[" + s + "]")) + "による行為判定を行う際、達成値が×" + value + "倍される。";
            case "EnvironmentCheck": return "環境による行為判定を行う際、達成値が" + Signed(value) + "される。";
            case "CraftCategories": return "〈" + string.Join(", ", r.Skip(1)) + "〉の装備を制作するとき、目標値が" + Signed(value) + "される。";
            case "TimedWeaponActivation": return "このターン中、〈" + r[1] + "〉の武器威力を参照する〈" + r[2] + "〉の発動ロールの達成値が" + Signed(value) + "される。";
            case "AcquisitionCost":
                string subject = r[2] == "AllSkills" ? "スキル習得に必要な消費決意" : (r[2] == "Class" ? "クラス《" + r[3] + "》" : "〈" + r[3] + "〉") + "の習得決意";
                return subject + "が" + (r[1] == "Multiply" ? "×" + value + "倍" : Signed(value)) + "される。";
            case "DailyGathering": return "一日の採取回数上限が" + Signed(value) + "追加される。";
            case "EarlyBattleActivation": return "戦闘開始から" + r[1] + "ターンの間、[" + r[2] + "]による発動ロールの達成値が" + Signed(value) + "される。";
            case "EarlyBattleDamage": return "戦闘開始から" + r[1] + "ターンの間、自身の被ダメージは×" + value + "倍される。";
            case "ResourceMaximum": return r[1] + "最大値が×" + value + "倍される。";
            case "SalePrice": return "NPCへの〈" + r[1] + "〉を除くアイテムの売値が×" + value + "倍される。";
            case "CategoryPower": return "〈" + r[1] + "〉の威力が×" + value + "倍される。";
            case "CategoryHealing": return "〈" + r[1] + "〉によるリソースの回復量が×" + value + "倍される。";
            case "CategoryCheck": return "〈" + r[1] + "〉による行為判定の達成値が" + Signed(value) + "される。";
            default: throw new InvalidOperationException("未対応の補正対象です。");
        }
    }

    private static void AddModifierRule(SkillBody skill, EffectType type, string value, params string[] rule)
    {
        var effect = CreateModifierRule(rule, value);
        Require(effect.Type == type);
        skill.Overrides.Add(effect);
    }

    private static string ModifierRuleOverrideText(OverrideDefinition effect, OverrideContent content)
    {
        if (content == null || effect == null) return null;
        if (content.Type == OverrideContentType.SetActivationRollTarget)
        {
            Require((effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike) && effect.Triggers.Count == 0);
            return "この発動ロールによる目標値は" + Args(content.Parameters, 1, "SetActivationRollTarget")[0] + "に変化する。";
        }
        if (content.Type == OverrideContentType.SetModifier && content.Parameters != null && content.Parameters.Count > 0 && content.Parameters[0] == "Rule")
        {
            Require((effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike) && effect.Triggers.Count == 0);
            var p = VariableArgs(content.Parameters, 3, "SetModifier/Rule");
            string[] rule = p.Skip(1).Take(p.Length - 2).ToArray();
            if (rule[0] == "RerollResult")
            {
                RuleLength(rule, 2); PositiveModifierFactor(p.Last());
                return "このカウンター効果による[" + rule[1] + "]の達成値は" + p.Last() + "倍される。";
            }
            var original = CreateModifierRule(rule, p.Last());
            return "この" + (original.Type == EffectType.Active ? "アクティブ" : "パッシブ") + "効果の補正を「" + ModifierRuleText(rule, p.Last()) + "」に変更する。";
        }
        string[] scope = ModifierRuleSelector(effect, content);
        return scope == null ? null : ModifierRuleText(scope, content.Parameters.Last());
    }

    private static void PositiveModifierFactor(string value)
    {
        decimal factor;
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out factor) || factor <= 0)
            throw new InvalidOperationException("倍率は0より大きい数値にしてください。");
    }

    private static bool ReadStackAndCategoryModifier(SkillBody skill, EffectType type, string s)
    {
        bool spike = type == EffectType.SecondSpike || type == EffectType.ThirdSpike;
        Match m = M(s, @"^行為判定を行う際、ダイスが([0-9]+)～([0-9]+)の時にファンブル、ダイスが([0-9]+)～([0-9]+)の時にクリティカルとなる。?$");
        if (m.Success && type == EffectType.Passive)
        {
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetRollRange, "Action", "Fumble", m.Groups[1].Value, m.Groups[2].Value), Change(OverrideContentType.SetRollRange, "Action", "Critical", m.Groups[3].Value, m.Groups[4].Value)); return true;
        }
        m = M(s, @"^(?:このパッシブ効果による)?(ファンブル|クリティカル)のダイス(?:は|を)([0-9]+)～([0-9]+)(?:に変化する|にする)。?$");
        if (m.Success && (spike || type == EffectType.Passive))
        { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetRollRange, "Action", m.Groups[1].Value == "ファンブル" ? "Fumble" : "Critical", m.Groups[2].Value, m.Groups[3].Value)); return true; }
        m = M(s, @"^このパッシブ効果による威力上昇は([0-9.]+)(?:倍)?に変化する。?$");
        if (m.Success && spike) { ReplaceModifierSpike(skill, type, m.Groups[1].Value, "CategoryPower"); return true; }
        m = M(s, @"^(.+?)状態の時、自身はターン開始時に([A-Z]+)を([0-9]+)得る。?$");
        if (m.Success && spike) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.StateTurnRecovery, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, "AnyTurn")); return true; }
        m = M(s, @"^被ダメージの軽減量を×([0-9.]+)倍する。?$");
        if (m.Success && type == EffectType.Critical) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.MultiplyDamageReduction, m.Groups[1].Value)); return true; }
        m = M(s, @"^対象からの被ダメージを×([0-9.]+)倍にする。?$");
        if (m.Success && type == EffectType.Counter) { AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.ActionSource, "Target"))), Change(OverrideContentType.MultiplyDamageTaken, "Self", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身への〈([^〉]+)〉の発動ロールの目標値が([+-][0-9]+)される。?$");
        if (m.Success && type == EffectType.Passive)
        { AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.TargetValue, Condition(ConditionType.ActionTarget, "Self"), Condition(ConditionType.ActionCategory, m.Groups[1].Value), Condition(ConditionType.CheckContext, "Activation"))), Change(OverrideContentType.AddTargetValue, m.Groups[2].Value)); return true; }
        m = M(s, @"^(?:自身の)?〈([^〉]+)〉(?:の発動ロール|による)の?目標値(?:が|は)([+-][0-9]+)される。?$");
        if (m.Success && type == EffectType.Passive)
        { AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.TargetValue, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, m.Groups[1].Value), Condition(ConditionType.CheckContext, "Activation"))), Change(OverrideContentType.AddTargetValue, m.Groups[2].Value)); return true; }
        m = M(s, @"^〈([^〉]+)〉による消費([A-Z]+)が(-[0-9]+)される。?$");
        if (m.Success && type == EffectType.Passive)
        { AddOverride(skill.Overrides, type, CategoryResourceCostTriggers(m.Groups[1].Value), Change(OverrideContentType.AddResourceCost, "Self", m.Groups[2].Value, m.Groups[3].Value, "Automatic")); return true; }
        m = M(s, @"^1ターンの内に複数の〈([^〉]+)〉を発動させた場合、([0-9]+)回目から威力は(.+?)と上昇してゆく。（([0-9.]+)倍まで）$");
        if (m.Success && type == EffectType.Passive)
        { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.MultiplyPowerByTurnActivations, new[] { m.Groups[1].Value, m.Groups[2].Value }.Concat(m.Groups[3].Value.Split('→').Select(x => x.TrimStart('×').TrimEnd('倍'))).Concat(new[] { m.Groups[4].Value }).ToArray())); return true; }
        m = M(s, @"^《([^》]+)》の達成値が([+-][0-9]+)される。?$");
        if (m.Success && type == EffectType.Passive)
        { AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionName, m.Groups[1].Value))), Change(OverrideContentType.AddActionResult, m.Groups[2].Value)); return true; }
        m = M(s, @"^〈([^〉]+)〉スキルの達成値が([+-][0-9]+)される。?$");
        if (m.Success) { ReadSkillLine(skill, type, "〈" + m.Groups[1].Value + "〉による行為判定の達成値が" + m.Groups[2].Value + "される。"); return true; }
        m = M(s, @"^自身の〈([^〉]+)〉による威力は×([0-9.]+)倍される。?$");
        if (m.Success) { ReadSkillLine(skill, type, "〈" + m.Groups[1].Value + "〉の威力が×" + m.Groups[2].Value + "倍される。"); return true; }
        if (s.TrimEnd('。') == "このスキルは〈回避〉する事が出来ない")
        { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetAttackRule, "Response", "回避", "Prohibit", "ThisSkill")); return true; }
        m = M(s, @"^このカウンター効果による達成値は([0-9.]+)倍される。?$");
        if (m.Success && spike) { AddModifierSpike(skill, type, m.Groups[1].Value, "RerollResult", "Original"); return true; }
        return false;
    }

    private static string StackAndCategoryModifierText(OverrideDefinition effect, OverrideContent content)
    {
        if (effect == null || content == null || effect.Triggers == null) return null;
        var t = effect.Triggers; string[] p;
        if (content.Type == OverrideContentType.SetModifier && content.Parameters != null && content.Parameters.Count == 4 && content.Parameters.Take(3).SequenceEqual(new[] { "Rule", "RerollResult", "Original" }))
        { Require(t.Count == 0 && (effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike)); PositiveModifierFactor(content.Parameters[3]); return "このカウンター効果による達成値は" + content.Parameters[3] + "倍される。"; }
        if (content.Type == OverrideContentType.SetRollRange)
        {
            p = Args(content.Parameters, 4, "SetRollRange"); Require(t.Count == 0 && p[0] == "Action" && (p[1] == "Fumble" || p[1] == "Critical"));
            int lo = Number(p[2], 1, "下限"), hi = Number(p[3], lo, "上限"); Require(hi <= 100);
            bool spike = effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike; Require(spike || effect.Type == EffectType.Passive);
            return (spike ? "このパッシブ効果による" : "") + (p[1] == "Fumble" ? "ファンブル" : "クリティカル") + "のダイスは" + p[2] + "～" + p[3] + (spike ? "に変化する。" : "にする。");
        }
        if (content.Type == OverrideContentType.StateTurnRecovery)
        { p = Args(content.Parameters, 4, "StateTurnRecovery"); Require(t.Count == 0 && p[3] == "AnyTurn" && (effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike)); Number(p[2], 1, "回復量"); return p[0] + "状態の時、自身はターン開始時に" + p[1] + "を" + p[2] + "得る。"; }
        if (content.Type == OverrideContentType.MultiplyDamageReduction)
        { p = Args(content.Parameters, 1, "MultiplyDamageReduction"); Require(t.Count == 0 && effect.Type == EffectType.Critical); PositiveModifierFactor(p[0]); return "被ダメージの軽減量を×" + p[0] + "倍する。"; }
        if (content.Type == OverrideContentType.MultiplyPowerByTurnActivations)
        { p = VariableArgs(content.Parameters, 4, "MultiplyPowerByTurnActivations"); Require(effect.Type == EffectType.Passive && t.Count == 0); Number(p[1], 1, "開始回数"); foreach (string factor in p.Skip(2)) PositiveModifierFactor(factor); return "1ターンの内に複数の〈" + p[0] + "〉を発動させた場合、" + p[1] + "回目から威力は" + string.Join("→", p.Skip(2).Take(p.Length - 3).Select(x => "×" + x + "倍")) + "と上昇してゆく。（" + p.Last() + "倍まで）"; }
        if (content.Type == OverrideContentType.MultiplyDamageTaken && effect.Type == EffectType.Counter && Matches(t, Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.ActionSource, "Target"))))
        { p = Args(content.Parameters, 2, "MultiplyDamageTaken"); Require(p[0] == "Self"); return "対象からの被ダメージを×" + p[1] + "倍にする。"; }
        if (content.Type == OverrideContentType.AddTargetValue && effect.Type == EffectType.Passive && t.Count == 1 && ValidTrigger(t[0]))
        {
            var c = t[0].Conditions.And;
            if (c.Count == 3 && (c[0].Type == ConditionType.ActionSource || c[0].Type == ConditionType.ActionTarget) && c[1].Type == ConditionType.ActionCategory && c[2].Type == ConditionType.CheckContext)
            {
                Require(t[0].Timing == TriggerTiming.TargetValue && t[0].Conditions.Or.Count == 0 && c[0].Parameters.SequenceEqual(new[] { "Self" }) && c[2].Parameters.SequenceEqual(new[] { "Activation" }));
                return (c[0].Type == ConditionType.ActionTarget ? "自身への" : "") + "〈" + Args(c[1].Parameters, 1, "ActionCategory")[0] + "〉の発動ロールの目標値が" + Signed(Args(content.Parameters, 1, "AddTargetValue")[0]) + "される。";
            }
        }
        string category;
        if (content.Type == OverrideContentType.AddResourceCost && effect.Type == EffectType.Passive && TryGetCategoryTrigger(t, TriggerTiming.ResourceCost, out category))
        { p = Args(content.Parameters, 4, "AddResourceCost"); Require(p[0] == "Self" && p[3] == "Automatic"); return "〈" + category + "〉による消費" + p[1] + "が" + Signed(p[2]) + "される。"; }
        if (content.Type == OverrideContentType.AddActionResult && effect.Type == EffectType.Passive && t.Count == 1 && ValidTrigger(t[0]))
        {
            var c = t[0].Conditions.And;
            if (c.Count == 2 && c[1].Type == ConditionType.ActionName)
            { string name = Args(c[1].Parameters, 1, "ActionName")[0]; Require(Matches(t, Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionName, name)))); return "《" + name + "》の達成値が" + Signed(Args(content.Parameters, 1, "AddActionResult")[0]) + "される。"; }
        }
        if (content.Type == OverrideContentType.SetAttackRule && content.Parameters.SequenceEqual(new[] { "Response", "回避", "Prohibit", "ThisSkill" }))
        { Require(effect.Type == EffectType.Active && t.Count == 0); return "このスキルは〈回避〉する事が出来ない。"; }
        return null;
    }

    private static string DamageRecoveryModifierText(OverrideDefinition effect, OverrideContent content)
    {
        if (content == null || content.Type != OverrideContentType.MultiplyResourceDamage || effect == null ||
            !Matches(effect.Triggers, Trigger(TriggerTiming.DamageDealt, Condition(ConditionType.ActionSource, "Self")))) return null;
        var p = Args(content.Parameters, 4, "MultiplyResourceDamage"); Require(effect.Type == EffectType.Passive && p[0] == "Self" && p[3] == "Automatic"); PositiveModifierFactor(p[2]);
        return "自身が与える" + p[1] + "ダメージは×" + p[2] + "倍される。";
    }

    private static bool ReadStateReferenceModifier(SkillBody skill, EffectType type, string s)
    {
        Match m;
        m = M(s, @"^《([^》]+)》の遅延攻撃の威力は×([0-9.]+)倍される。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.MultiplyStateAttackPower, m.Groups[1].Value, m.Groups[2].Value)); return true; }
        if (s == "属性威力による自身の被ダメージは×[最も高いその属性の不利属性B]倍される。")
        { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.AttributeWeaknessDamage, "Self", "HighestDisadvantageAttributeBonus")); return true; }
        if (s.TrimEnd('。') == "この効果はカウンター効果の対象にならない")
        { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.PreventCounterTarget, "ThisEffect")); return true; }
        m = M(s, @"^〈([^〉]+)〉による自身の被ダメージが×([0-9.]+)倍される。?$");
        if (m.Success)
        { AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.ActionCategory, m.Groups[1].Value))), Change(OverrideContentType.MultiplyDamageTaken, "Self", m.Groups[2].Value)); return true; }
        m = M(s, @"^自身の〈([^〉]+)〉による発動ロールを行う際、ダイスが([0-9]+)[-～]([0-9]+)の時にクリティカルとなる。?$");
        if (m.Success)
        { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetRollRange, "Activation", "Critical", m.Groups[2].Value, m.Groups[3].Value, m.Groups[1].Value)); return true; }
        m = M(s, @"^効果量([0-9.]+)倍。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.MultiplyRecovery, m.Groups[1].Value)); return true; }
        m = M(s, @"^このクールタイムは([0-9]+)ターンに変化する。?$");
        if (m.Success && IsSpikeStage(type)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetCooldown, m.Groups[1].Value)); return true; }
        m = M(s, @"^この発動ロールによる目標値は([0-9]+)に変化する。?$");
        if (m.Success && IsSpikeStage(type)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetActivationRollTarget, m.Groups[1].Value)); return true; }
        m = M(s, @"^このアクティブ効果による《([^》]+)》スタックの付与は([0-9]+)に変化する。?$");
        if (m.Success && IsSpikeStage(type)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetStackAmount, "Target", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^《([^》]+)》を付与されたキャラクターの代わりに行動を([0-9]+)回までに宣言に変化する。?$");
        if (m.Success && IsSpikeStage(type)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetStateControlLimit, m.Groups[1].Value, "Turn", m.Groups[2].Value)); return true; }
        m = M(s, @"^自身が〈([^〉]+)〉を発動する際、自身が《([^》]+)》状態、かつ対象が《([^》]+)》状態、かつ自身と対象が《([^》]+)》状態の時、自身の〈([^〉]+)〉(?:に)?よる威力は×([0-9.]+)倍,\s*消費([A-Z]+)が([0-9]+)以上のスキルの消費([A-Z]+)は(-[0-9]+)される。?$");
        if (m.Success)
        {
            Require(m.Groups[1].Value == m.Groups[5].Value && m.Groups[7].Value == m.Groups[9].Value);
            AddOverride(skill.Overrides, type, NoTriggers(),
                Change(OverrideContentType.ConditionalStatePower, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[6].Value),
                Change(OverrideContentType.ConditionalStateCost, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[7].Value, m.Groups[8].Value, m.Groups[10].Value)); return true;
        }
        // Canonical output writes each independent modifier with its own selector.
        m = M(s, @"^自身が〈([^〉]+)〉を発動する際、自身が《([^》]+)》状態、かつ対象が《([^》]+)》状態、かつ自身と対象が《([^》]+)》状態の時、威力は×([0-9.]+)倍される。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.ConditionalStatePower, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value)); return true; }
        m = M(s, @"^自身が〈([^〉]+)〉を発動する際、自身が《([^》]+)》状態、かつ対象が《([^》]+)》状態、かつ自身と対象が《([^》]+)》状態の時、消費([A-Z]+)が([0-9]+)以上のスキルの消費\5は(-[0-9]+)される。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.ConditionalStateCost, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value, m.Groups[6].Value, m.Groups[7].Value)); return true; }
        m = M(s, @"^このパッシブ効果による威力上昇は([0-9.]+)倍に変化する。?$");
        if (m.Success && IsSpikeStage(type) && skill.Overrides.SelectMany(x => x.Contents).Any(x => x.Type == OverrideContentType.ConditionalStatePower))
        { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetConditionalStatePower, m.Groups[1].Value)); return true; }
        return false;
    }

    private static string StateReferenceModifierText(OverrideDefinition effect, OverrideContent content)
    {
        if (effect == null || content == null) return null;
        var t = effect.Triggers;
        string[] p;
        if (content.Type == OverrideContentType.MultiplyDamageTaken && t.Count == 1 && t[0].Conditions.And.Count == 1 && t[0].Conditions.And[0].Type == ConditionType.ActionCategory)
        {
            p = Args(content.Parameters, 2, "MultiplyDamageTaken"); string cat = Args(t[0].Conditions.And[0].Parameters, 1, "ActionCategory")[0];
            Require(effect.Type == EffectType.Passive && p[0] == "Self" && Matches(t, Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.ActionCategory, cat)))); PositiveModifierFactor(p[1]);
            return "〈" + cat + "〉による自身の被ダメージが×" + p[1] + "倍される。";
        }
        if (content.Type == OverrideContentType.SetRollRange && content.Parameters.Count == 5)
        {
            p = Args(content.Parameters, 5, "SetRollRange"); Require(t.Count == 0 && effect.Type == EffectType.Passive && p[0] == "Activation" && p[1] == "Critical");
            int lower = Number(p[2], 1, "下限"); Require(Number(p[3], lower, "上限") <= 100);
            return "自身の〈" + p[4] + "〉による発動ロールを行う際、ダイスが" + p[2] + "-" + p[3] + "の時にクリティカルとなる。";
        }
        if (content.Type == OverrideContentType.MultiplyRecovery && effect.Type == EffectType.Critical && t.Count == 0)
        { p = Args(content.Parameters, 1, "MultiplyRecovery"); PositiveModifierFactor(p[0]); return "効果量" + p[0] + "倍"; }
        if (IsSpikeStage(effect.Type) && t.Count == 0)
        {
            if (content.Type == OverrideContentType.SetCooldown)
            { p = Args(content.Parameters, 1, "SetCooldown"); Number(p[0], 0, "クールタイム"); return "このクールタイムは" + p[0] + "ターンに変化する。"; }
            if (content.Type == OverrideContentType.SetStackAmount)
            { p = Args(content.Parameters, 3, "SetStackAmount"); Require(p[0] == "Target"); Number(p[2], 1, "スタック数"); return "このアクティブ効果による《" + p[1] + "》スタックの付与は" + p[2] + "に変化する。"; }
        }
        switch (content.Type)
        {
            case OverrideContentType.MultiplyStateAttackPower:
                p = Args(content.Parameters, 2, "MultiplyStateAttackPower"); Require(effect.Type == EffectType.Critical && t.Count == 0); PositiveModifierFactor(p[1]);
                return "《" + p[0] + "》の遅延攻撃の威力は×" + p[1] + "倍される。";
            case OverrideContentType.AttributeWeaknessDamage:
                p = Args(content.Parameters, 2, "AttributeWeaknessDamage"); Require(effect.Type == EffectType.Passive && t.Count == 0 && p.SequenceEqual(new[] { "Self", "HighestDisadvantageAttributeBonus" }));
                return "属性威力による自身の被ダメージは×[最も高いその属性の不利属性B]倍される。";
            case OverrideContentType.PreventCounterTarget:
                p = Args(content.Parameters, 1, "PreventCounterTarget"); Require(effect.Type == EffectType.Active && t.Count == 0 && p[0] == "ThisEffect"); return "この効果はカウンター効果の対象にならない。";
            case OverrideContentType.SetStateControlLimit:
                p = Args(content.Parameters, 3, "SetStateControlLimit"); Require(IsSpikeStage(effect.Type) && t.Count == 0 && p[1] == "Turn"); Number(p[2], 1, "行動回数");
                return "《" + p[0] + "》を付与されたキャラクターの代わりに行動を" + p[2] + "回までに宣言に変化する。";
            case OverrideContentType.ConditionalStatePower:
            case OverrideContentType.ConditionalStateCost:
                bool power = content.Type == OverrideContentType.ConditionalStatePower;
                p = Args(content.Parameters, power ? 5 : 7, content.Type.ToString()); Require(effect.Type == EffectType.Passive && t.Count == 0);
                if (power) PositiveModifierFactor(p[4]); else { Number(p[5], 1, "消費下限"); Require(M(p[6], @"^-[0-9]+$").Success); }
                return "自身が〈" + p[0] + "〉を発動する際、自身が《" + p[1] + "》状態、かつ対象が《" + p[2] + "》状態、かつ自身と対象が《" + p[3] + "》状態の時、" +
                    (power ? "威力は×" + p[4] + "倍される。" : "消費" + p[4] + "が" + p[5] + "以上のスキルの消費" + p[4] + "は" + p[6] + "される。");
            case OverrideContentType.SetConditionalStatePower:
                p = Args(content.Parameters, 1, "SetConditionalStatePower"); Require(IsSpikeStage(effect.Type) && t.Count == 0); PositiveModifierFactor(p[0]); return "このパッシブ効果による威力上昇は" + p[0] + "倍に変化する。";
            default: return null;
        }
    }

}
