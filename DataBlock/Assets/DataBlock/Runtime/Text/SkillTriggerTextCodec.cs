using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static string TimedAttackTriggerText(EffectType type, List<TriggerDefinition> t)
    {
        if (Matches(t, Trigger(TriggerTiming.AttackHit))) { Require(type == EffectType.Active); return "この攻撃が命中した時、"; }
        if (Matches(t, Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Any"))))
        { Require(type == EffectType.Passive); return "毎ターン開始時、"; }
        if (t == null || t.Count != 1 || !ValidTrigger(t[0]) || t[0].Conditions.Or.Count != 0) return null;
        var c = t[0].Conditions.And;
        if (t[0].Timing == TriggerTiming.TurnStart && c.Count == 1 && c[0].Type == ConditionType.TurnNumber)
        { Require(type == EffectType.Passive); string turn = Args(c[0].Parameters, 1, "TurnNumber")[0]; Number(turn, 1, "ターン"); return "戦闘開始" + turn + "ターン目、"; }
        if (t[0].Timing == TriggerTiming.IncomingDamage && c.Count == 2 && c.All(x => x.Type == ConditionType.ActionCategory))
        { Require(type == EffectType.Passive); return "自身が受ける〈" + Args(c[0].Parameters, 1, "ActionCategory")[0] + "〉かつ〈" + Args(c[1].Parameters, 1, "ActionCategory")[0] + "〉によるダメージに対して、"; }
        return null;
    }

    private static string BasicEffectTriggerText(List<TriggerDefinition> triggers)
    {
        if (triggers == null || triggers.Count != 1 || !ValidTrigger(triggers[0])) return null;
        var t = triggers[0]; var c = t.Conditions.And;
        if (HasTrigger(t, TriggerTiming.AfterSkillResolution)) return "";
        if (t.Timing == TriggerTiming.TurnStart && c.Count == 2 && c[0].Type == ConditionType.HasMealEffect && c[1].Type == ConditionType.TurnNumber)
        {
            Require(t.Conditions.Or.Count == 0 && Args(c[0].Parameters, 2, "HasMealEffect").SequenceEqual(new[] { "Self", "False" }));
            string turn = Args(c[1].Parameters, 1, "TurnNumber")[0]; Number(turn, 1, "ターン");
            return "自身が食事セット効果を受けていない時、" + turn + "ターン目に";
        }
        return null;
    }

    private static string CategoryActivationTriggerText(List<TriggerDefinition> triggers)
    {
        if (triggers == null || triggers.Count != 1 || !ValidTrigger(triggers[0])) return null;
        var t = triggers[0]; var c = t.Conditions.And;
        if (t.Timing == TriggerTiming.ActionActivated && c.Count == 3 && c[0].Type == ConditionType.ActionSource && c[1].Type == ConditionType.ActionCategory && c[2].Type == ConditionType.ActionCategory)
        {
            Require(c[0].Parameters.SequenceEqual(new[] { "Self" }) && t.Conditions.Or.Count == 0);
            return "自身が〈" + Args(c[1].Parameters, 1, "ActionCategory")[0] + "〉かつ〈" + Args(c[2].Parameters, 1, "ActionCategory")[0] + "〉を発動した時、";
        }
        return null;
    }

    private static List<TriggerDefinition> ThisAttackDamageTriggers()
    { return On(Trigger(TriggerTiming.DamageDealt, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionOrigin, "ThisSkill"))); }

    private static string DamageRecoveryTriggerText(EffectType type, List<TriggerDefinition> t)
    {
        if (Matches(t, ThisAttackDamageTriggers().ToArray())) { Require(type == EffectType.Active); return ""; }
        if (Matches(t, Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Self"))))
        { Require(type == EffectType.Passive); return "自身のターン開始時、"; }
        return null;
    }

}
