using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static bool ReadBeastSecondState(StateDefinition state, string text)
    {
        string s = RegexReplace(text.Trim(), @"^さらに[、]?\s*", "");
        if (ReadBeastSecondRule(null, state, EffectType.Passive, s)) return true;
        var m = M(s, @"^(?:自身は)?((?:〈[^〉]+〉)(?:(?:および|及び)〈[^〉]+〉)*)(?:による効果を発動できなくなる|が使用不可能になります)。?$");
        if (m.Success)
        {
            foreach (Match cat in AllMatches(m.Groups[1].Value, @"〈([^〉]+)〉"))
            foreach (string category in Split(cat.Groups[1].Value, ","))
                AddStateEffect(state, NoTriggers(), Content(EffectContentType.ProhibitActionCategory, category));
            return true;
        }
        m = M(s, @"^(?:自身の)?((?:〈[^〉]+〉)(?:(?:および|及び)〈[^〉]+〉)*)の達成値が×([0-9.]+)倍される。?$");
        if (m.Success)
        {
            foreach (Match category in AllMatches(m.Groups[1].Value, @"〈([^〉]+)〉"))
                AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, category.Groups[1].Value))), Change(OverrideContentType.MultiplyActionResult, m.Groups[2].Value));
            return true;
        }
        m = M(s, @"^自身の〈([^〉]+)〉による威力(?:は|が)スタックに応じて(.+?)と(?:減少してゆく|変化する)。?$");
        if (m.Success)
        {
            var values = m.Groups[2].Value.Split('→').Select(x => x.Trim().TrimStart('×').TrimEnd('倍')).ToArray();
            AddOverride(state.Overrides, EffectType.Passive, CategoryPowerTriggers(m.Groups[1].Value), Change(OverrideContentType.MultiplyPowerByStacks, new[] { "Self", m.Groups[1].Value }.Concat(values).ToArray())); return true;
        }
        m = M(s, @"^スキルによる([A-Z]+)の消費量が×(.+?)倍される。?$");
        if (m.Success)
        { AddOverride(state.Overrides, EffectType.Passive, StateSkillCostTriggers(), Change(OverrideContentType.MultiplyResourceCost, "Self", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^(毎ターン開始時|自身のターン開始時)、自身は([A-Z]+)を(.+?)(回復|消費)する。?$");
        if (m.Success)
        {
            AddStateEffect(state, On(Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, m.Groups[1].Value == "毎ターン開始時" ? "Any" : "Self"))), Content(m.Groups[4].Value == "回復" ? EffectContentType.RestoreResource : EffectContentType.ConsumeResource, "Self", m.Groups[2].Value, m.Groups[3].Value.Replace("×", "*"))); return true;
        }
        m = M(s, @"^自身が〈([^〉]+)〉によって受けるダメージを×([0-9.]+)倍する。?$");
        if (m.Success)
        { AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.ActionCategory, m.Groups[1].Value))), Change(OverrideContentType.MultiplyDamageTaken, "Self", m.Groups[2].Value)); return true; }
        m = M(s, @"^自身の〈([^〉]+)〉による行為判定の目標値は([+-][0-9]+)加算される。?$");
        if (m.Success)
        { AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.TargetValue, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, m.Groups[1].Value))), Change(OverrideContentType.AddTargetValue, m.Groups[2].Value)); return true; }
        m = M(s, @"^自身への〈([^〉]+)〉による命中ロールの目標値が([+-][0-9]+)される。?$");
        if (m.Success)
        { AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.TargetValue, Condition(ConditionType.ActionTarget, "Self"), Condition(ConditionType.ActionCategory, m.Groups[1].Value))), Change(OverrideContentType.AddTargetValue, m.Groups[2].Value)); return true; }
        m = M(s, @"^自身への〈([^〉]+)〉を無効(?:に)?する。?$");
        if (!m.Success) m = M(s, @"^自身はあらゆる〈([^〉]+)〉からの効果を受けない。?$");
        if (m.Success) { AddStateEffect(state, NoTriggers(), Content(EffectContentType.CategoryImmunity, "Self", m.Groups[1].Value)); return true; }
        m = M(s, @"^(?:自身または対象|付与者または自身)の《([^》]+)》状態が解除されたとき、(?:《([^》]+)》|この状態)も解除される。?$");
        if (m.Success)
        {
            Require(!m.Groups[2].Success || m.Groups[2].Value == state.Name);
            AddStateEffect(state, new List<TriggerDefinition>
            {
                Trigger(TriggerTiming.StateRemoved, Condition(ConditionType.RelatedStateRemoved, "Applier", m.Groups[1].Value)),
                Trigger(TriggerTiming.StateRemoved, Condition(ConditionType.RelatedStateRemoved, "Self", m.Groups[1].Value))
            }, Content(EffectContentType.RemoveState, "Self", state.Name)); return true;
        }
        return false;
    }
    private static List<TriggerDefinition> StateSkillCostTriggers()
    { return On(Trigger(TriggerTiming.ResourceCost, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionOrigin, "Skill"))); }

    private static string BeastSecondStateContentText(StateDefinition state, List<TriggerDefinition> t, EffectContent content)
    {
        if (content == null) return null;
        string rule = BeastSecondRuleContentText(content, true);
        if (rule != null) { Require(Matches(t, Trigger(TriggerTiming.Always))); return rule; }
        if (content.Type == EffectContentType.CategoryImmunity)
        {
            var p = Args(content.Parameters, 2, "CategoryImmunity"); Require(p[0] == "Self" && Matches(t, Trigger(TriggerTiming.Always)));
            return "自身への〈" + p[1] + "〉を無効にする。";
        }
        if (content.Type == EffectContentType.RestoreResource || content.Type == EffectContentType.ConsumeResource)
        {
            bool any = Matches(t, Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Any")));
            bool own = Matches(t, Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Self")));
            if (any || own)
            {
                var p = Args(content.Parameters, 3, content.Type.ToString()); Require(p[0] == "Self");
                return (any ? "毎ターン開始時" : "自身のターン開始時") + "、自身は" + p[1] + "を" + p[2] + (content.Type == EffectContentType.RestoreResource ? "回復" : "消費") + "する。";
            }
        }
        if (content.Type == EffectContentType.RemoveState && t != null && t.Count == 2 && t.All(x => ValidTrigger(x) && x.Conditions.And.Count == 1 && x.Conditions.And[0].Type == ConditionType.RelatedStateRemoved))
        {
            var p = Args(content.Parameters, 2, "RemoveState"); Require(p[0] == "Self" && p[1] == state.Name);
            string relation = Args(t[0].Conditions.And[0].Parameters, 2, "RelatedStateRemoved")[1];
            Require(Matches(t, Trigger(TriggerTiming.StateRemoved, Condition(ConditionType.RelatedStateRemoved, "Applier", relation)), Trigger(TriggerTiming.StateRemoved, Condition(ConditionType.RelatedStateRemoved, "Self", relation))));
            return "付与者または自身の《" + relation + "》状態が解除されたとき、この状態も解除される。";
        }
        return null;
    }

    private static string BeastSecondStateOverrideText(List<TriggerDefinition> t, OverrideContent content)
    {
        string rule = BeastSecondRuleOverrideText(new OverrideDefinition { Type = EffectType.Passive, Triggers = t }, content, true);
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
            foreach (string factor in p.Skip(2)) PositiveHumanFactor(factor);
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
}
