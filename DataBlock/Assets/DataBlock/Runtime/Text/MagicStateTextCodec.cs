using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static bool ReadMagicState(StateDefinition state, string s)
    {
        Match m = M(s, @"^自身のあらゆる〈([^〉]+)〉の威力が×([0-9.]+)倍される。?$");
        if (m.Success) { AddOverride(state.Overrides, EffectType.Passive, CategoryPowerTriggers(m.Groups[1].Value), Change(OverrideContentType.MultiplyPower, m.Groups[2].Value)); return true; }
        m = M(s, @"^対象の〈([^〉]+)〉の威力を×([0-9.]+)倍する。さらにその攻撃に属性Bによる威力ある場合、属性威力を×([0-9.]+)倍する。?$");
        if (m.Success)
        {
            AddOverride(state.Overrides, EffectType.Passive, CategoryPowerTriggers(m.Groups[1].Value), Change(OverrideContentType.MultiplyPower, m.Groups[2].Value));
            AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.ElementalPower, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, m.Groups[1].Value), Condition(ConditionType.HasAttributeBonusPower))), Change(OverrideContentType.MultiplyAttackComponent, "AttributePower", m.Groups[3].Value)); return true;
        }
        m = M(s, @"^自身の〈([^〉]+)〉による攻撃に属性Bによる威力がある場合、属性威力を×([0-9.]+)倍する。?$");
        if (m.Success)
        { AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.ElementalPower, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, m.Groups[1].Value), Condition(ConditionType.HasAttributeBonusPower))), Change(OverrideContentType.MultiplyAttackComponent, "AttributePower", m.Groups[2].Value)); return true; }
        m = M(s, @"^この状態のスタックが([0-9]+)を超えた時、(?:以下の効果|《([^》]+)》の効果)に変化する。?$");
        if (m.Success)
        { AddStateEffect(state, NoTriggers(), Content(EffectContentType.UseStateDefinitionAtStacks, "GreaterThan", m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : "NextDefinition", "ReplaceEffects", "KeepIdentityAndStacks")); return true; }
        return false;
    }

    private static string MagicStateOverrideText(List<TriggerDefinition> triggers, OverrideContent content)
    {
        if (content == null || triggers == null || triggers.Count != 1 || !ValidTrigger(triggers[0])) return null;
        if (content.Type != OverrideContentType.MultiplyAttackComponent) return null;
        var c = triggers[0].Conditions.And;
        if (c.Count != 3 || c[1].Type != ConditionType.ActionCategory || c[2].Type != ConditionType.HasAttributeBonusPower) return null;
        string category = Args(c[1].Parameters, 1, "ActionCategory")[0];
        Require(Matches(triggers, Trigger(TriggerTiming.ElementalPower, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, category), Condition(ConditionType.HasAttributeBonusPower))));
        string[] p = Args(content.Parameters, 2, "MultiplyAttackComponent"); Require(p[0] == "AttributePower"); PositiveHumanFactor(p[1]);
        return "自身の〈" + category + "〉による攻撃に属性Bによる威力がある場合、属性威力を×" + p[1] + "倍する。";
    }

    private static string MagicStateContentText(StateDefinition state, List<TriggerDefinition> triggers, EffectContent content)
    {
        if (content == null || content.Type != EffectContentType.UseStateDefinitionAtStacks) return null;
        string[] p = Args(content.Parameters, 5, "UseStateDefinitionAtStacks");
        Require(Matches(triggers, Trigger(TriggerTiming.Always)) && p[0] == "GreaterThan" && p[3] == "ReplaceEffects" && p[4] == "KeepIdentityAndStacks");
        Number(p[1], 0, "スタック閾値"); Require(p[2] != "NextDefinition" && p[2] != state.Name);
        return "この状態のスタックが" + p[1] + "を超えた時、《" + p[2] + "》の効果に変化する。";
    }

    private static void CompleteMagicStates(SkillTextData data)
    {
        for (int i = 0; i < data.States.Count; i++)
        foreach (var content in data.States[i].Effects.SelectMany(e => e.Contents).Where(c => c.Type == EffectContentType.UseStateDefinitionAtStacks))
        {
            if (content.Parameters[2] != "NextDefinition") continue;
            Require(i + 1 < data.States.Count); content.Parameters[2] = data.States[i + 1].Name;
        }
    }
}
