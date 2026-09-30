using System;
using System.Collections.Generic;
using System.Linq;

public static partial class SkillTextConverter
{
    private static bool ReadBeastSecondSkill(SkillBody skill, EffectType type, string text)
    {
        string s = RegexReplace(text.Trim(), @"^(?:さらに|更に)[、]?\s*", "");
        if (ReadBeastSecondRule(skill, null, type, s)) return true;
        var m = M(s, @"^対象は《([^》]+)》スタックを([0-9]+)付与される。（最大([0-9]+)スタックまで）$");
        if (m.Success)
        { Require(type == EffectType.Active); ExtendedEffect(skill, type, Content(EffectContentType.GainStack, "Target", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)); return true; }
        m = M(s, @"^スタックの付与数が([0-9]+)に変化する。?$");
        if (m.Success)
        {
            Require(type == EffectType.Critical);
            var gains = skill.Effects.Where(x => x.Type == EffectType.Active && x.Triggers.Count == 0).SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.GainStack).ToList();
            Require(gains.Count == 1);
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetStackAmount, gains[0].Parameters[0], gains[0].Parameters[1], m.Groups[1].Value)); return true;
        }
        m = M(s, @"^自身は毎ターン《([^》]+)》スタックを([0-9]+)得る。?$");
        if (m.Success)
        { Require(type == EffectType.Passive); ExtendedEffect(skill, type, Content(EffectContentType.GainStack, "Self", m.Groups[1].Value, m.Groups[2].Value), Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Any"))); return true; }
        m = M(s, @"^毎ターン開始時、(.+)$");
        if (m.Success)
        {
            Require(type == EffectType.Passive);
            EffectContent content = ReadExtendedContent(m.Groups[1].Value) ?? ReadContent(m.Groups[1].Value);
            Require(content.Type == EffectContentType.GainStack);
            ExtendedEffect(skill, type, content, Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Any"))); return true;
        }
        m = M(s, @"^戦闘開始([0-9]+)ターン目、([A-Z]+)が([+-][0-9]+)される。?$");
        if (m.Success)
        { Require(type == EffectType.Passive); ExtendedEffect(skill, type, Content(EffectContentType.ModifyResource, "Self", m.Groups[2].Value, m.Groups[3].Value), Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnNumber, m.Groups[1].Value))); return true; }
        m = M(s, @"^戦闘開始([0-9]+)ターン目、(自身の[A-Z]+を[+-].+変化させる。)$");
        if (m.Success)
        { Require(type == EffectType.Passive); ExtendedEffect(skill, type, ReadContent(m.Groups[2].Value), Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnNumber, m.Groups[1].Value))); return true; }
        m = M(s, @"^(?:自身の)?防御点(?:は|が)×?([0-9.]+)倍される。?$");
        if (m.Success)
        { Require(type == EffectType.Passive); AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.ModifyStat, "Self", "防御点", "Multiply", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身のあらゆる威力は×([0-9.]+)倍される。?$");
        if (m.Success)
        { Require(type == EffectType.Passive); AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.AttackPower, Condition(ConditionType.ActionSource, "Self"))), Change(OverrideContentType.MultiplyPower, m.Groups[1].Value)); return true; }
        m = M(s, @"^戦闘開始の([0-9]+)ターン目に発動した場合、威力は×([0-9.]+)倍される。?$");
        if (m.Success)
        { Require(type == EffectType.Active); AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.AttackPower, Condition(ConditionType.TurnNumber, m.Groups[1].Value))), Change(OverrideContentType.MultiplyPower, m.Groups[2].Value)); return true; }
        m = M(s, @"^エリアに〈([^〉]+)〉を含む場合、エリアの進行ロールの回数を-([0-9]+)しても良い。?$");
        if (m.Success)
        { Require(type == EffectType.Passive); AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.OptionalAreaProgressReduction, "0", m.Groups[1].Value, m.Groups[2].Value, "Optional")); return true; }
        m = M(s, @"^自身が受ける〈([^〉]+)〉かつ〈([^〉]+)〉による被ダメージは(.+?)減少される。?$");
        if (m.Success)
        {
            Require(type == EffectType.Passive);
            ExtendedEffect(skill, type, Content(EffectContentType.ReduceDamage, "Self", m.Groups[3].Value), Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.ActionCategory, m.Groups[1].Value), Condition(ConditionType.ActionCategory, m.Groups[2].Value))); return true;
        }
        m = M(s, @"^自身が受ける〈([^〉]+)〉かつ〈([^〉]+)〉によるダメージに対して、(自身の被ダメージを.+軽減する。)$");
        if (m.Success)
        {
            Require(type == EffectType.Passive);
            ExtendedEffect(skill, type, ReadContent(m.Groups[3].Value), Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.ActionCategory, m.Groups[1].Value), Condition(ConditionType.ActionCategory, m.Groups[2].Value))); return true;
        }
        m = M(s, @"^この攻撃が命中した時、対象は([0-9]+)ターンの間((?:《[^》]+》)(?:と《[^》]+》)*)状態になる。?$");
        if (m.Success)
        {
            Require(type == EffectType.Active);
            foreach (System.Text.RegularExpressions.Match state in AllMatches(m.Groups[2].Value, @"《([^》]+)》"))
                ExtendedEffect(skill, type, Content(EffectContentType.ApplyState, "Target", state.Groups[1].Value, m.Groups[1].Value), Trigger(TriggerTiming.AttackHit));
            return true;
        }
        return false;
    }

    private static string BeastSecondTriggerText(EffectType type, List<TriggerDefinition> t)
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

    private static string BeastSecondOverrideText(OverrideDefinition effect, OverrideContent content)
    {
        string rule = BeastSecondRuleOverrideText(effect, content, false);
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

    private static void CompleteBeastSecondData(SkillTextData data)
    {
        foreach (var skill in new[] { data.Skill }.Concat(data.Skill.Choices).Concat(data.Summons))
        foreach (var content in skill.Overrides.SelectMany(x => x.Contents).Where(x => x.Type == OverrideContentType.OptionalAreaProgressReduction))
        {
            if (content.Parameters.Count == 4 && content.Parameters[1] == "Unspecified" && content.Parameters[2] == "Unspecified")
            { content.Parameters[1] = "Any"; content.Parameters[2] = content.Parameters[0]; }
        }
    }
}
