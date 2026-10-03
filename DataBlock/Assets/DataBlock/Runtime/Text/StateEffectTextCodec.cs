using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static bool ReadEvasionStateEffects(StateDefinition state, string text)
    {
        var m = M(text, @"^〈([^〉]+)〉に対する自身の〈回避〉の達成値を(-[0-9]+)減少させる。?$");
        if (m.Success)
        {
            AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.EvasionResult, Condition(ConditionType.ActionCategory, m.Groups[1].Value))), Change(OverrideContentType.AddEvasionResult, m.Groups[2].Value)); return true;
        }
        if (M(text, @"^自身の被ダメージは×[0-9.]+倍されます。?$").Success)
            return ReadStateLine(state, text.Replace("されます", "される"));
        return false;
    }

    private static bool ReadTimedAndCategoryStateEffects(StateDefinition state, string text)
    {
        string s = RegexReplace(text.Trim(), @"^さらに[、]?\s*", "");
        if (ReadSemanticRule(null, state, EffectType.Passive, s)) return true;
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

    private static string TimedAndCategoryStateContentText(StateDefinition state, List<TriggerDefinition> t, EffectContent content)
    {
        if (content == null) return null;
        string rule = SemanticRuleContentText(content, true);
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

    private static bool ReadBasicStateEffects(StateDefinition state, string s)
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

    private static string BasicStateContentText(StateDefinition state, List<TriggerDefinition> t, EffectContent content)
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

    private static bool ReadActionResultStateEffects(StateDefinition state, string text)
    {
        var m = M(text, @"^あらゆる行為判定の達成値が([+-][0-9]+)上昇する。?$");
        if (!m.Success) return false;
        AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"))), Change(OverrideContentType.AddActionResult, m.Groups[1].Value)); return true;
    }

    private static bool ReadStackAndAttributeState(StateDefinition state, string s)
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

    private static string StackStateContentText(StateDefinition state, List<TriggerDefinition> triggers, EffectContent content)
    {
        if (content == null || content.Type != EffectContentType.UseStateDefinitionAtStacks) return null;
        string[] p = Args(content.Parameters, 5, "UseStateDefinitionAtStacks");
        Require(Matches(triggers, Trigger(TriggerTiming.Always)) && p[0] == "GreaterThan" && p[3] == "ReplaceEffects" && p[4] == "KeepIdentityAndStacks");
        Number(p[1], 0, "スタック閾値"); Require(p[2] != "NextDefinition" && p[2] != state.Name);
        return "この状態のスタックが" + p[1] + "を超えた時、《" + p[2] + "》の効果に変化する。";
    }

    private static bool ReadRecoveryAndResourceStateEffects(StateDefinition state, string s)
    {
        var m = M(s, @"^自身の〈([^〉]+)〉による武器攻撃が命中した時、対象が((?:《[^》]+》)(?:,\s*《[^》]+》)*)のいずれかなら、《([^》]+)》状態を([0-9]+)ターン付与する。?$");
        if (m.Success)
        { AddStateEffect(state, NoTriggers(), Content(EffectContentType.WeaponHitRaceState, m.Groups[1].Value, RaceNames(m.Groups[2].Value), m.Groups[3].Value, m.Groups[4].Value)); return true; }
        m = M(s, @"^攻撃が命中した時、対象が((?:《[^》]+》)(?:,\s*《[^》]+》)*)のいずれかなら、《([^》]+)》状態を([0-9]+)ターン付与する。?$");
        if (m.Success)
        {
            // The source repeats the immediately preceding weapon effect. Do not
            // turn its explanation into a second, unrestricted state application.
            var prior = state.Effects.LastOrDefault();
            Require(prior != null && prior.Contents.Count == 1 && prior.Contents[0].Type == EffectContentType.WeaponHitRaceState &&
                prior.Contents[0].Parameters.Skip(1).SequenceEqual(new[] { RaceNames(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value }));
            return true;
        }
        m = M(s, @"^既に対象が《([^》]+)》状態の時、威力は×([0-9.]+)倍される。?$");
        if (m.Success)
        {
            var weapons = state.Effects.SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.WeaponHitRaceState).ToList();
            Require(weapons.Count == 1);
            AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(OverrideContentType.WeaponPowerAgainstState, weapons[0].Parameters[0], m.Groups[1].Value, m.Groups[2].Value)); return true;
        }
        m = M(s, @"^自身のあらゆる行為判定の達成値は(.+?)減少する。?$");
        if (m.Success)
        { AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"))), Change(OverrideContentType.AddActionResult, "-(" + m.Groups[1].Value + ")")); return true; }
        m = M(s, @"^自身のあらゆる行為判定の達成値は([+-].+?)加算される。?$");
        if (m.Success)
        { AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"))), Change(OverrideContentType.AddActionResult, m.Groups[1].Value)); return true; }
        return false;
    }

    private static bool ReadSkillControlStateEffects(StateDefinition state, string s)
    {
        Match m;
        if (s.TrimEnd('。') == "自身は指定されたスキルを宣言する事が出来なくなる")
        { AddStateEffect(state, NoTriggers(), Content(EffectContentType.ProhibitSelectedSkill, "Self", "SelectedSkill")); return true; }
        m = M(s, @"^自身が〈([^〉]+)〉かつ〈([^〉]+)〉を発動する際、対象の防御点は無視される。?$");
        if (m.Success)
        { AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(OverrideContentType.IgnoreDefenseForCategories, "Self", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^付与者は、この状態が付与されたキャラクターの代わりに行動を毎ターン([0-9]+)回まで宣言できる。?$");
        if (m.Success)
        { AddStateEffect(state, NoTriggers(), Content(EffectContentType.GrantActionControl, "Applier", "Self", "Turn", m.Groups[1].Value, "Unspecified")); return true; }
        if (s.TrimEnd('。') == "その行動中は味方キャラクターとして扱われる")
        {
            var control = state.Effects.SelectMany(x => x.Contents).LastOrDefault(x => x.Type == EffectContentType.GrantActionControl);
            Require(control != null); control.Parameters[4] = "AllyDuringAction"; return true;
        }
        m = M(s, @"^付与されてから([0-9]+)ターン後、対象を(.+?)の威力\+(.+?)の属性威力で攻撃する。?$");
        if (m.Success)
        { AddStateEffect(state, NoTriggers(), Content(EffectContentType.DelayedStateAttack, "Target", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, "Unspecified", "Unspecified")); return true; }
        if (s == "（対象選択と威力の算出は付与の際に行われる）" || s.TrimEnd('。') == "効果による攻撃後、この状態は解除される")
        {
            var attack = state.Effects.SelectMany(x => x.Contents).LastOrDefault(x => x.Type == EffectContentType.DelayedStateAttack);
            Require(attack != null);
            if (s.StartsWith("（", StringComparison.Ordinal)) attack.Parameters[4] = "SnapshotOnApply"; else attack.Parameters[5] = "RemoveAfterAttack";
            return true;
        }
        return false;
    }

    private static string SkillControlStateContentText(List<TriggerDefinition> triggers, EffectContent content)
    {
        if (content == null) return null;
        if (content.Type != EffectContentType.ProhibitSelectedSkill && content.Type != EffectContentType.GrantActionControl && content.Type != EffectContentType.DelayedStateAttack) return null;
        Require(Matches(triggers, Trigger(TriggerTiming.Always)));
        string[] p;
        switch (content.Type)
        {
            case EffectContentType.ProhibitSelectedSkill:
                p = Args(content.Parameters, 2, "ProhibitSelectedSkill"); Require(p.SequenceEqual(new[] { "Self", "SelectedSkill" })); return "自身は指定されたスキルを宣言する事が出来なくなる。";
            case EffectContentType.GrantActionControl:
                p = Args(content.Parameters, 5, "GrantActionControl"); Require(p[0] == "Applier" && p[1] == "Self" && p[2] == "Turn" && p[4] == "AllyDuringAction"); Number(p[3], 1, "行動回数");
                return "付与者は、この状態が付与されたキャラクターの代わりに行動を毎ターン" + p[3] + "回まで宣言できる。\nその行動中は味方キャラクターとして扱われる。";
            case EffectContentType.DelayedStateAttack:
                p = Args(content.Parameters, 6, "DelayedStateAttack"); Require(p[0] == "Target" && p[4] == "SnapshotOnApply" && p[5] == "RemoveAfterAttack"); Number(p[1], 1, "遅延ターン");
                return "付与されてから" + p[1] + "ターン後、対象を" + p[2] + "の威力+" + p[3] + "の属性威力で攻撃する。\n（対象選択と威力の算出は付与の際に行われる）\n*効果による攻撃後、この状態は解除される。";
            default: return null;
        }
    }


    private static bool ReadStateEffectStatement(StateDefinition state, string text)
    {
        return ReadActionResultStateEffects(state, text) ||
               ReadRecoveryAndResourceStateEffects(state, text) ||
               ReadTimedAndCategoryStateEffects(state, text) ||
               ReadEvasionStateEffects(state, text) ||
               ReadSkillControlStateEffects(state, text) ||
               ReadStackAndAttributeState(state, text) ||
               ReadBasicStateEffects(state, text);
    }
}
