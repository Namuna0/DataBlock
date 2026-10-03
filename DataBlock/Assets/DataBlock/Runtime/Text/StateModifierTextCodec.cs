using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static string EvasionStateModifierText(List<TriggerDefinition> triggers, OverrideContent content)
    {
        if (content == null || content.Type != OverrideContentType.AddEvasionResult || triggers.Count != 1 || triggers[0].Conditions.And.Count != 1 || triggers[0].Conditions.And[0].Type != ConditionType.ActionCategory) return null;
        string category = Args(triggers[0].Conditions.And[0].Parameters, 1, "ActionCategory")[0];
        Require(Matches(triggers, Trigger(TriggerTiming.EvasionResult, Condition(ConditionType.ActionCategory, category))));
        string amount = Args(content.Parameters, 1, "AddEvasionResult")[0]; Require(M(amount, @"^-[0-9]+$").Success);
        return "〈" + category + "〉に対する自身の〈回避〉の達成値を" + amount + "減少させる。";
    }

    private static string TimedAndCategoryStateModifierText(List<TriggerDefinition> t, OverrideContent content)
    {
        string rule = SemanticRuleOverrideText(new OverrideDefinition { Type = EffectType.Passive, Triggers = t }, content, true);
        if (rule != null) return rule;
        if (content == null || t == null) return null;
        if (content.Type == OverrideContentType.MultiplyResourceCost && Matches(t, StateSkillCostTriggers().ToArray()))
        {
            var p = Args(content.Parameters, 3, "MultiplyResourceCost"); Require(p[0] == "Self");
            return "スキルによる" + p[1] + "の消費量が×" + p[2] + "倍される。";
        }
        if (content.Type == OverrideContentType.MultiplyPowerByStacks)
        {
            var p = VariableArgs(content.Parameters, 3, "MultiplyPowerByStacks"); Require(p[0] == "Self" && Matches(t, CategoryPowerTriggers(p[1]).ToArray()));
            foreach (string factor in p.Skip(2)) PositiveModifierFactor(factor);
            return "自身の〈" + p[1] + "〉による威力がスタックに応じて" + string.Join("→", p.Skip(2).Select(x => x + "倍")) + "と変化する。";
        }
        if (t.Count != 1 || !ValidTrigger(t[0]) || t[0].Conditions.Or.Count != 0) return null;
        var c = t[0].Conditions.And;
        if (content.Type == OverrideContentType.MultiplyDamageTaken && c.Count == 1 && c[0].Type == ConditionType.ActionCategory)
        {
            var p = Args(content.Parameters, 2, "MultiplyDamageTaken"); Require(p[0] == "Self" && t[0].Timing == TriggerTiming.IncomingDamage);
            return "自身が〈" + Args(c[0].Parameters, 1, "ActionCategory")[0] + "〉によって受けるダメージを×" + p[1] + "倍する。";
        }
        if (c.Count != 2 || c[1].Type != ConditionType.ActionCategory) return null;
        string category = Args(c[1].Parameters, 1, "ActionCategory")[0];
        if (content.Type == OverrideContentType.MultiplyActionResult && Matches(t, Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, category))))
            return "自身の〈" + category + "〉の達成値が×" + Args(content.Parameters, 1, "MultiplyActionResult")[0] + "倍される。";
        if (content.Type == OverrideContentType.AddTargetValue)
        {
            string value = Signed(Args(content.Parameters, 1, "AddTargetValue")[0]);
            if (Matches(t, Trigger(TriggerTiming.TargetValue, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, category))))
                return "自身の〈" + category + "〉による行為判定の目標値は" + value + "加算される。";
            if (Matches(t, Trigger(TriggerTiming.TargetValue, Condition(ConditionType.ActionTarget, "Self"), Condition(ConditionType.ActionCategory, category))))
                return "自身への〈" + category + "〉による命中ロールの目標値が" + value + "される。";
        }
        return null;
    }

    private static string BasicStateModifierText(OverrideDefinition effect, OverrideContent content)
    {
        var t = effect.Triggers; string[] p;
        if (content.Type == OverrideContentType.MultiplyPowerByStacks)
        {
            p = VariableArgs(content.Parameters, 3, "MultiplyPowerByStacks"); Require(effect.Type == EffectType.Passive && p[0] == "Self" && Matches(t, CategoryPowerTriggers(p[1]).ToArray()));
            foreach (string value in p.Skip(2)) { double number; Require(double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number) && number > 0 && !double.IsInfinity(number)); }
            return "自身の〈" + p[1] + "〉による威力がスタックに応じて" + string.Join("→", p.Skip(2).Select(x => x + "倍")) + "と増加する。";
        }
        if (content.Type == OverrideContentType.MultiplyDamageTaken && content.Parameters != null && content.Parameters.Count == 3)
        {
            p = Args(content.Parameters, 3, "MultiplyDamageTaken"); Require(p[0] == "Self" && Matches(t, Trigger(TriggerTiming.IncomingDamage))); return "被ダメージが×" + p[1] + "倍増加する。（最大" + p[2] + "倍まで）";
        }
        if (content.Type == OverrideContentType.ModifyStat && Matches(t, Trigger(TriggerTiming.Always)))
        {
            p = Args(content.Parameters, 4, "ModifyStat"); Require(p[0] == "Self");
            if (p[2] == "Multiply" && p[1].EndsWith("最大値")) return p[1] + "が×" + p[3] + "倍増加。";
            if (p[2] == "Add" && p[1].EndsWith("点")) return p[1] + Signed(p[3]) + "。";
        }
        return null;
    }

    private static string AttributeStateModifierText(List<TriggerDefinition> triggers, OverrideContent content)
    {
        if (content == null || triggers == null || triggers.Count != 1 || !ValidTrigger(triggers[0])) return null;
        if (content.Type != OverrideContentType.MultiplyAttackComponent) return null;
        var c = triggers[0].Conditions.And;
        if (c.Count != 3 || c[1].Type != ConditionType.ActionCategory || c[2].Type != ConditionType.HasAttributeBonusPower) return null;
        string category = Args(c[1].Parameters, 1, "ActionCategory")[0];
        Require(Matches(triggers, Trigger(TriggerTiming.ElementalPower, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, category), Condition(ConditionType.HasAttributeBonusPower))));
        string[] p = Args(content.Parameters, 2, "MultiplyAttackComponent"); Require(p[0] == "AttributePower"); PositiveModifierFactor(p[1]);
        return "自身の〈" + category + "〉による攻撃に属性Bによる威力がある場合、属性威力を×" + p[1] + "倍する。";
    }

    private static string DamageRecoveryStateModifierText(List<TriggerDefinition> t, OverrideContent content)
    {
        if (content == null || content.Type != OverrideContentType.AddActionResult || !Matches(t, Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self")))) return null;
        return "自身のあらゆる行為判定の達成値は" + Signed(Args(content.Parameters, 1, "AddActionResult")[0]) + "加算される。";
    }

    private static string DefenseStateModifierText(List<TriggerDefinition> triggers, OverrideContent content)
    {
        if (content == null || content.Type != OverrideContentType.IgnoreDefenseForCategories) return null;
        var p = Args(content.Parameters, 3, "IgnoreDefenseForCategories"); Require(p[0] == "Self" && Matches(triggers, Trigger(TriggerTiming.Always)));
        return "自身が〈" + p[1] + "〉かつ〈" + p[2] + "〉を発動する際、対象の防御点は無視される。";
    }

}
