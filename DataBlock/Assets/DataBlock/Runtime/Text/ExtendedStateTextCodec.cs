using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static bool ReadExtendedState(StateDefinition state, string s)
    {
        s = RegexReplace(s, @"^さらに\s*", "");
        Match m = M(s, @"^自身が〈([^〉]+)〉によるダメージを与えた時、与えたダメージの(.+?)倍の値の([A-Z]+)を回復する。?$");
        if (m.Success) { AddStateEffect(state, On(Trigger(TriggerTiming.DamageDealt, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, m.Groups[1].Value))), Content(EffectContentType.RestoreFromDamage, "Self", m.Groups[3].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^自身の〈([^〉]+)〉によって相手を《([^》]+)》状態にした時、《([^》]+)》スタックを([0-9]+)獲得する。?$");
        if (m.Success) { AddStateEffect(state, On(Trigger(TriggerTiming.DamageDealt, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, m.Groups[1].Value), Condition(ConditionType.AttackAppliedState, "Target", m.Groups[2].Value))), Content(EffectContentType.GainStack, "Self", m.Groups[3].Value, m.Groups[4].Value)); return true; }
        m = M(s, @"^自身の〈([^〉]+)〉による威力がスタックに応じて(.+?)と増加する。?（戦闘終了まで）$");
        if (m.Success)
        {
            string[] values = m.Groups[2].Value.Split('→').Select(x => x.Trim().TrimEnd('倍')).ToArray();
            AddOverride(state.Overrides, EffectType.Passive, CategoryPowerTriggers(m.Groups[1].Value), Change(OverrideContentType.MultiplyPowerByStacks, new[] { "Self", m.Groups[1].Value }.Concat(values).ToArray()));
            AddStateEffect(state, On(Trigger(TriggerTiming.BattleEnd)), Content(EffectContentType.RemoveState, "Self", state.Name)); return true;
        }
        m = M(s, @"^被ダメージが×(.+?)倍増加する。?（最大(.+?)倍まで）$");
        if (m.Success) { AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.IncomingDamage)), Change(OverrideContentType.MultiplyDamageTaken, "Self", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        if (s.TrimEnd('。') == "自身はあらゆる効果の選択対象にならない") { AddStateEffect(state, NoTriggers(), Content(EffectContentType.PreventSelection, "Self", "All")); return true; }
        m = M(s, @"^自身がダメージを受ける、もしくは((?:〈[^〉]+〉)(?:及び〈[^〉]+〉)*)を宣言するとこの状態は解除される。?$");
        if (m.Success)
        {
            var triggers = On(Trigger(TriggerTiming.DamageReceived, Condition(ConditionType.ActionSource, "Self")));
            triggers.AddRange(AllMatches(m.Groups[1].Value, @"〈([^〉]+)〉").Cast<Match>().Select(x => Trigger(TriggerTiming.ActionDeclared, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, x.Groups[1].Value))));
            AddStateEffect(state, triggers, Content(EffectContentType.RemoveState, "Self", state.Name)); return true;
        }
        m = M(s, @"^(.+?最大値)が×(.+?)倍増加,\s*自身のターン開始時([A-Z]+)(.+?)回復,\s*(.+?点)([+-].+)$");
        if (m.Success)
        {
            AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(OverrideContentType.ModifyStat, "Self", m.Groups[1].Value, "Multiply", m.Groups[2].Value), Change(OverrideContentType.ModifyStat, "Self", m.Groups[5].Value, "Add", Signed(m.Groups[6].Value)));
            AddStateEffect(state, On(Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Self"))), Content(EffectContentType.RestoreResource, "Self", m.Groups[3].Value, m.Groups[4].Value)); return true;
        }
        m = M(s, @"^自身のターン開始時([A-Z]+)(.+?)回復。?$");
        if (m.Success) { AddStateEffect(state, On(Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Self"))), Content(EffectContentType.RestoreResource, "Self", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^(.+?最大値)が×(.+?)倍増加。?$");
        if (m.Success) { AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(OverrideContentType.ModifyStat, "Self", m.Groups[1].Value, "Multiply", m.Groups[2].Value)); return true; }
        m = M(s, @"^(.+?点)([+-].+?)。?$");
        if (m.Success) { AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(OverrideContentType.ModifyStat, "Self", m.Groups[1].Value, "Add", Signed(m.Groups[2].Value))); return true; }
        if (s.TrimEnd('。') == "自身はカウンター効果を発動できなくなる") { AddStateEffect(state, NoTriggers(), Content(EffectContentType.ProhibitEffect, "Self", "Counter")); return true; }
        m = M(s, @"^自身の〈([^〉]+)〉による威力(?:が|は)×?(.+?)倍される。?$");
        if (m.Success) { AddOverride(state.Overrides, EffectType.Passive, CategoryPowerTriggers(m.Groups[1].Value), Change(OverrideContentType.MultiplyPower, m.Groups[2].Value)); return true; }
        m = M(s, @"^自身の被ダメージ(?:が|は)×?(.+?)倍される。?$");
        if (m.Success) { AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.IncomingDamage)), Change(OverrideContentType.MultiplyDamageTaken, "Self", m.Groups[1].Value)); return true; }
        m = M(s, @"^《([^》]+)》を除くあらゆる状態を付与されない。?$");
        if (m.Success) { AddStateEffect(state, NoTriggers(), Content(EffectContentType.PreventStateApplication, "Self", "Except", m.Groups[1].Value)); return true; }
        if (s == "戦闘終了時、この状態は解除される。") { AddStateEffect(state, On(Trigger(TriggerTiming.BattleEnd)), Content(EffectContentType.RemoveState, "Self", state.Name)); return true; }
        // Canonical stack scaling and expiration are separate serialized entries.
        m = M(s, @"^自身の〈([^〉]+)〉による威力がスタックに応じて(.+?)と増加する。?$");
        if (m.Success) { AddOverride(state.Overrides, EffectType.Passive, CategoryPowerTriggers(m.Groups[1].Value), Change(OverrideContentType.MultiplyPowerByStacks, new[] { "Self", m.Groups[1].Value }.Concat(m.Groups[2].Value.Split('→').Select(x => x.Trim().TrimEnd('倍'))).ToArray())); return true; }
        return false;
    }
    private static string ExtendedStateOverrideText(OverrideDefinition effect, OverrideContent content)
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
    private static string ExtendedStateContentText(StateDefinition state, List<TriggerDefinition> t, EffectContent content)
    {
        if (content == null || t == null) return null;
        string[] p;
        if (content.Type == EffectContentType.RestoreFromDamage)
        {
            p = Args(content.Parameters, 3, "RestoreFromDamage"); string category = StateDamageCategory(t, false); Require(p[0] == "Self");
            return "自身が〈" + category + "〉によるダメージを与えた時、与えたダメージの" + p[2] + "倍の値の" + p[1] + "を回復する。";
        }
        if (content.Type == EffectContentType.GainStack && content.Parameters != null && content.Parameters.Count == 3)
        {
            p = Args(content.Parameters, 3, "GainStack"); string category = StateDamageCategory(t, true); Require(p[0] == "Self"); Number(p[2], 1, "スタック数");
            string[] condition = Args(t[0].Conditions.And[2].Parameters, 2, "AttackAppliedState"); Require(condition[0] == "Target");
            return "自身の〈" + category + "〉によって相手を《" + condition[1] + "》状態にした時、《" + p[1] + "》スタックを" + p[2] + "獲得する。";
        }
        if (content.Type == EffectContentType.PreventSelection) { Require(Args(content.Parameters, 2, "PreventSelection").SequenceEqual(new[] { "Self", "All" }) && Matches(t, Trigger(TriggerTiming.Always))); return "自身はあらゆる効果の選択対象にならない。"; }
        if (content.Type == EffectContentType.ProhibitEffect) { Require(Args(content.Parameters, 2, "ProhibitEffect").SequenceEqual(new[] { "Self", "Counter" }) && Matches(t, Trigger(TriggerTiming.Always))); return "自身はカウンター効果を発動できなくなる。"; }
        if (content.Type == EffectContentType.PreventStateApplication) { p = Args(content.Parameters, 3, "PreventStateApplication"); Require(p[0] == "Self" && p[1] == "Except" && Matches(t, Trigger(TriggerTiming.Always))); return "《" + p[2] + "》を除くあらゆる状態を付与されない。"; }
        if (content.Type == EffectContentType.RestoreResource && Matches(t, Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Self")))) { p = Args(content.Parameters, 3, "RestoreResource"); Require(p[0] == "Self"); return "自身のターン開始時" + p[1] + p[2] + "回復。"; }
        if (content.Type == EffectContentType.RemoveState)
        {
            p = Args(content.Parameters, 2, "RemoveState"); Require(p[0] == "Self" && p[1] == state.Name);
            if (Matches(t, Trigger(TriggerTiming.BattleEnd))) return "戦闘終了時、この状態は解除される。";
            if (t.Count >= 2 && HasTrigger(t[0], TriggerTiming.DamageReceived, Condition(ConditionType.ActionSource, "Self")))
            {
                var categories = new List<string>();
                foreach (var trigger in t.Skip(1))
                {
                    Require(ValidTrigger(trigger) && trigger.Conditions.And.Count == 2 && trigger.Conditions.And[1].Type == ConditionType.ActionCategory);
                    string category = Args(trigger.Conditions.And[1].Parameters, 1, "ActionCategory")[0];
                    Require(HasTrigger(trigger, TriggerTiming.ActionDeclared, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, category))); categories.Add(category);
                }
                return "自身がダメージを受ける、もしくは" + string.Join("及び", categories.Select(x => "〈" + x + "〉")) + "を宣言するとこの状態は解除される。";
            }
        }
        return null;
    }
    private static string StateDamageCategory(List<TriggerDefinition> t, bool applied)
    {
        Require(t.Count == 1 && ValidTrigger(t[0]) && t[0].Timing == TriggerTiming.DamageDealt && t[0].Conditions.Or.Count == 0);
        var c = t[0].Conditions.And; Require(c.Count == (applied ? 3 : 2) && c[0].Type == ConditionType.ActionSource && c[1].Type == ConditionType.ActionCategory);
        Require(Args(c[0].Parameters, 1, "ActionSource")[0] == "Self");
        if (applied) Require(c[2].Type == ConditionType.AttackAppliedState);
        return Args(c[1].Parameters, 1, "ActionCategory")[0];
    }
}
