using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static bool ReadMagicModifier(SkillBody skill, EffectType type, string s)
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
        if (m.Success && spike) { HumanSpikeFor(skill, type, m.Groups[1].Value, "CategoryPower"); return true; }
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
        if (m.Success && spike) { HumanSpike(skill, type, m.Groups[1].Value, "RerollResult", "Original"); return true; }
        return false;
    }

    private static string MagicOverrideText(OverrideDefinition effect, OverrideContent content)
    {
        if (effect == null || content == null || effect.Triggers == null) return null;
        var t = effect.Triggers; string[] p;
        if (content.Type == OverrideContentType.SetModifier && content.Parameters != null && content.Parameters.Count == 4 && content.Parameters.Take(3).SequenceEqual(new[] { "Rule", "RerollResult", "Original" }))
        { Require(t.Count == 0 && (effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike)); PositiveHumanFactor(content.Parameters[3]); return "このカウンター効果による達成値は" + content.Parameters[3] + "倍される。"; }
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
        { p = Args(content.Parameters, 1, "MultiplyDamageReduction"); Require(t.Count == 0 && effect.Type == EffectType.Critical); PositiveHumanFactor(p[0]); return "被ダメージの軽減量を×" + p[0] + "倍する。"; }
        if (content.Type == OverrideContentType.MultiplyPowerByTurnActivations)
        { p = VariableArgs(content.Parameters, 4, "MultiplyPowerByTurnActivations"); Require(effect.Type == EffectType.Passive && t.Count == 0); Number(p[1], 1, "開始回数"); foreach (string factor in p.Skip(2)) PositiveHumanFactor(factor); return "1ターンの内に複数の〈" + p[0] + "〉を発動させた場合、" + p[1] + "回目から威力は" + string.Join("→", p.Skip(2).Take(p.Length - 3).Select(x => "×" + x + "倍")) + "と上昇してゆく。（" + p.Last() + "倍まで）"; }
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
}
