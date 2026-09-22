using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
public static partial class SkillTextConverter
{
    private static void AddStateEffect(StateDefinition state, List<TriggerDefinition> triggers, params EffectContent[] contents)
    {
        state.Effects.Add(new EffectDefinition { Type = EffectType.Passive, Triggers = triggers, Contents = contents.ToList() });
    }
    private static bool ReadStateLine(StateDefinition state, string text)
    {
        text = Unbullet(text).TrimStart('*', '＊').Trim();
        Match m = M(text, @"^自身は毎ターン開始時《([^》]+)》状態になる。?$");
        if (m.Success)
        {
            AddStateEffect(state, On(Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Any"))), Content(EffectContentType.ApplyState, "Self", m.Groups[1].Value)); return true;
        }
        m = M(text, @"^自身は自身のターン開始時に(.+?)のダメージを受ける。?$");
        if (m.Success)
        {
            AddStateEffect(state, On(Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Self"))), Content(EffectContentType.Damage, "Self", m.Groups[1].Value)); return true;
        }
        m = M(text, @"^((?:\[[^\]]+\])(?:及び\[[^\]]+\])*)による行為判定の達成値(?:が|は)×?(.+?)倍される。?$");
        if (m.Success)
        {
            string[] stats = AllMatches(m.Groups[1].Value, @"\[([^\]]+)\]").Cast<Match>().Select(x => x.Groups[1].Value).ToArray();
            AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionStat, stats))), Change(OverrideContentType.MultiplyActionResult, m.Groups[2].Value)); return true;
        }
        m = M(text, @"^この状態は付与者が戦闘不能になった時[、,]\s*解除される。?$");
        if (m.Success)
        {
            AddStateEffect(state, On(Trigger(TriggerTiming.CharacterIncapacitated, Condition(ConditionType.ActionSource, "Applier"))), Content(EffectContentType.RemoveState, "Self", state.Name)); return true;
        }
        m = M(text, @"^(自身|対象|付与者)の被ダメ(?:ー|―)?ジ(?:が|は)×?(.+?)倍される。?$");
        if (m.Success)
        {
            if (state.Effects.Count + state.Overrides.Count != 0) throw new InvalidOperationException("この状態の共通トリガーは既に設定されています。");
            state.Trigger = Trigger(TriggerTiming.IncomingDamage);
            AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(OverrideContentType.MultiplyDamageTaken, ActorKey(m.Groups[1].Value), m.Groups[2].Value)); return true;
        }
        m = M(text, @"^(自身|対象|付与者)のカウンター効果による達成値(?:が|は)([+-].+?)される。?$");
        if (m.Success)
        {
            if (state.Effects.Count + state.Overrides.Count != 0) throw new InvalidOperationException("この状態の共通トリガーは既に設定されています。");
            state.Trigger = Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, ActorKey(m.Groups[1].Value)), Condition(ConditionType.EffectKind, "Counter"));
            AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(OverrideContentType.AddActionResult, Signed(m.Groups[2].Value))); return true;
        }
        m = M(text, @"^(自身|対象|付与者)の〈([^〉]+)〉による威力(?:が|は)×(.+?)倍される。?$");
        if (m.Success)
        {
            if (state.Effects.Count + state.Overrides.Count != 0) throw new InvalidOperationException("この状態の共通トリガーは既に設定されています。");
            state.Trigger = Trigger(TriggerTiming.AttackPower, Condition(ConditionType.ActionSource, ActorKey(m.Groups[1].Value)), Condition(ConditionType.ActionCategory, m.Groups[2].Value));
            AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(OverrideContentType.MultiplyPower, m.Groups[3].Value)); return true;
        }
        m = M(text, @"^(自身|対象|付与者)からの〈([^〉]+)〉の回避達成値(?:が|は)([+-].+?)される。?$");
        if (m.Success)
        {
            if (state.Effects.Count + state.Overrides.Count != 0) throw new InvalidOperationException("この状態の共通トリガーは既に設定されています。");
            state.Trigger = Trigger(TriggerTiming.EvasionResult, Condition(ConditionType.ActionSource, ActorKey(m.Groups[1].Value)), Condition(ConditionType.ActionCategory, m.Groups[2].Value));
            AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(OverrideContentType.AddEvasionResult, Signed(m.Groups[3].Value))); return true;
        }
        m = M(text, @"^この効果は自身からは《([^》]+)》状態として扱う事が出来るが、対象からは《([^》]+)》状態として扱う事が出来ない。?$");
        if (m.Success)
        {
            AddStateEffect(state, NoTriggers(), Content(EffectContentType.StateAlias, "Self", m.Groups[1].Value, "Allow"));
            AddStateEffect(state, NoTriggers(), Content(EffectContentType.StateAlias, "Target", m.Groups[2].Value, "Deny")); return true;
        }
        m = M(text, @"^(自身|対象|付与者)はこの状態を《([^》]+)》状態として扱(える|えない)。?$");
        if (m.Success) { AddStateEffect(state, NoTriggers(), Content(EffectContentType.StateAlias, ActorKey(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value == "える" ? "Allow" : "Deny")); return true; }
        m = M(text, @"^(自身|対象|付与者)との(?:《([^》]+)》|([^《》]+?))状態が解除された時、この状態も解除される。?$");
        if (m.Success)
        {
            AddStateEffect(state, On(Trigger(TriggerTiming.StateRemoved, Condition(ConditionType.RelatedStateRemoved, ActorKey(m.Groups[1].Value), m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value))), Content(EffectContentType.RemoveState, "Self", state.Name)); return true;
        }
        m = M(text, @"^〈([^〉]+)〉及び〈([^〉]+)〉を除(?:く|き)、自身または自身を対象にする、?同じ接近グループを条件とする効果の発動(?:を無効される|は無効になる)。?$");
        if (m.Success)
        {
            AddStateEffect(state, On(Trigger(TriggerTiming.BeforeEffectActivation, Condition(ConditionType.EffectInvolvesSelf), Condition(ConditionType.RequiresSameMeleeGroup), Condition(ConditionType.ExcludedWeaponCategories, m.Groups[1].Value, m.Groups[2].Value))), Content(EffectContentType.InvalidateTriggeredEffect)); return true;
        }
        m = M(text, @"^(?:この効果は)?自身の《([^》]+)》状態が解除される、または付与されなおした時、(?:この状態も)?解除される。?$");
        if (m.Success)
        {
            AddStateEffect(state, new List<TriggerDefinition> { Trigger(TriggerTiming.StateRemoved, Condition(ConditionType.OwnState, m.Groups[1].Value)), Trigger(TriggerTiming.StateReapplied, Condition(ConditionType.OwnState, m.Groups[1].Value)) }, Content(EffectContentType.RemoveState, "Self", state.Name)); return true;
        }
        return false;
    }
    private static bool ReadStateText(StateDefinition state, string text)
    {
        if (ReadStateLine(state, text)) return true;
        string[] sentences = text.Split(new[] { '。' }, StringSplitOptions.RemoveEmptyEntries);
        if (sentences.Length <= 1) return false;
        foreach (string sentence in sentences)
            if (!ReadStateLine(state, sentence.Trim() + "。")) return false;
        return true;
    }
    private static List<TriggerDefinition> StateTriggers(StateDefinition state, List<TriggerDefinition> triggers)
    {
        if (state.Trigger == null || !ValidConditions(state.Trigger.Conditions) || triggers == null) throw new InvalidOperationException("状態のトリガーが不正です。");
        if (triggers.Count > 0)
        {
            if (state.Trigger.Timing != TriggerTiming.Always || !Empty(state.Trigger.Conditions)) throw new InvalidOperationException("今回の状態文型は共通トリガーと個別トリガーの同時指定に対応しません。");
            return triggers;
        }
        return On(state.Trigger);
    }
    private static List<string> StateTexts(StateDefinition state)
    {
        if (state.Effects == null || state.Overrides == null) throw new InvalidOperationException("状態のEffectsまたはOverridesがnullです。");
        var texts = new List<string>();
        foreach (EffectDefinition effect in state.Effects)
        {
            if (effect == null || effect.Type != EffectType.Passive || effect.Contents == null || effect.Contents.Count == 0) throw new InvalidOperationException("状態の通常効果は、内容のあるPassiveにしてください。");
            foreach (EffectContent content in effect.Contents) texts.Add(StateContentText(state, StateTriggers(state, effect.Triggers), content));
        }
        foreach (OverrideDefinition effect in state.Overrides)
        {
            if (effect == null || effect.Type != EffectType.Passive || effect.Contents == null || effect.Contents.Count == 0) throw new InvalidOperationException("状態の上書き効果は、内容のあるPassiveにしてください。");
            foreach (OverrideContent content in effect.Contents) texts.Add(StateOverrideText(StateTriggers(state, effect.Triggers), content));
        }
        return texts;
    }
    private static string StateOverrideText(List<TriggerDefinition> triggers, OverrideContent content)
    {
        if (triggers.Count != 1 || content == null || !ValidTrigger(triggers[0])) throw new InvalidOperationException("状態の数値上書きにはトリガーを1個指定してください。");
        TriggerDefinition t = triggers[0];
        var c = t.Conditions;
        if (t.Timing == TriggerTiming.IncomingDamage && Empty(c) && content.Type == OverrideContentType.MultiplyDamageTaken)
        {
            string[] p = Args(content.Parameters, 2, "MultiplyDamageTaken");
            return Actor(p[0]) + "の被ダメージは" + p[1] + "倍される。";
        }
        if (t.Timing == TriggerTiming.ActionResult && c.Or.Count == 0 && c.And.Count == 2 && c.And.Count(x => x.Type == ConditionType.ActionSource) == 1 && c.And.Count(x => x.Type == ConditionType.EffectKind) == 1 && content.Type == OverrideContentType.AddActionResult)
        {
            string source = Args(c.And.First(x => x.Type == ConditionType.ActionSource).Parameters, 1, "ActionSource")[0];
            string kind = Args(c.And.First(x => x.Type == ConditionType.EffectKind).Parameters, 1, "EffectKind")[0];
            if (kind == "Counter") return Actor(source) + "のカウンター効果による達成値は" + Signed(Args(content.Parameters, 1, "AddActionResult")[0]) + "される。";
        }
        if (t.Timing == TriggerTiming.ActionResult && c.Or.Count == 0 && c.And.Count == 1 && c.And[0].Type == ConditionType.ActionStat && content.Type == OverrideContentType.MultiplyActionResult)
        {
            string[] stats = VariableArgs(c.And[0].Parameters, 1, "ActionStat");
            string factor = Args(content.Parameters, 1, "MultiplyActionResult")[0];
            return string.Join("及び", stats.Select(x => "[" + x + "]")) + "による行為判定の達成値は×" + factor + "倍される。";
        }
        if (c.Or.Count == 0 && c.And.Count == 2 && c.And.Count(x => x.Type == ConditionType.ActionSource) == 1 && c.And.Count(x => x.Type == ConditionType.ActionCategory) == 1)
        {
            string source = Args(c.And.First(x => x.Type == ConditionType.ActionSource).Parameters, 1, "ActionSource")[0];
            string category = Args(c.And.First(x => x.Type == ConditionType.ActionCategory).Parameters, 1, "ActionCategory")[0];
            string value = Args(content.Parameters, 1, content.Type.ToString())[0];
            if (t.Timing == TriggerTiming.AttackPower && content.Type == OverrideContentType.MultiplyPower) return Actor(source) + "の〈" + category + "〉による威力は×" + value + "倍される。";
            if (t.Timing == TriggerTiming.EvasionResult && content.Type == OverrideContentType.AddEvasionResult) return Actor(source) + "からの〈" + category + "〉の回避達成値は" + Signed(value) + "される。";
        }
        throw new InvalidOperationException("未対応の状態の上書きトリガーと内容です。");
    }
    private static string StateContentText(StateDefinition state, List<TriggerDefinition> triggers, EffectContent content)
    {
        if (triggers == null || content == null || triggers.Any(x => !ValidTrigger(x))) throw new InvalidOperationException("状態のトリガーまたは内容が不正です。");
        if (content.Type == EffectContentType.StateAlias && Matches(triggers, Trigger(TriggerTiming.Always)))
        {
            string[] p = Args(content.Parameters, 3, "StateAlias");
            if (p[2] != "Allow" && p[2] != "Deny") throw new InvalidOperationException("状態の扱いはAllow / Denyです。");
            return Actor(p[0]) + "はこの状態を《" + p[1] + "》状態として扱" + (p[2] == "Allow" ? "える。" : "えない。");
        }
        if (content.Type == EffectContentType.ApplyState && triggers.Count == 1 && triggers[0].Timing == TriggerTiming.TurnStart)
        {
            string[] p = Args(content.Parameters, 2, "ApplyState");
            string[] owner = triggers[0].Conditions.And.Count == 1 && triggers[0].Conditions.And[0].Type == ConditionType.TurnOwner ? Args(triggers[0].Conditions.And[0].Parameters, 1, "TurnOwner") : new string[0];
            if (p[0] == "Self" && owner.Length == 1 && owner[0] == "Any" && triggers[0].Conditions.Or.Count == 0)
                return "自身は毎ターン開始時《" + p[1] + "》状態になる。";
        }
        if (content.Type == EffectContentType.Damage && triggers.Count == 1 && triggers[0].Timing == TriggerTiming.TurnStart)
        {
            string[] p = Args(content.Parameters, 2, "Damage");
            string[] owner = triggers[0].Conditions.And.Count == 1 && triggers[0].Conditions.And[0].Type == ConditionType.TurnOwner ? Args(triggers[0].Conditions.And[0].Parameters, 1, "TurnOwner") : new string[0];
            if (p[0] == "Self" && owner.Length == 1 && owner[0] == "Self" && triggers[0].Conditions.Or.Count == 0)
                return "自身は自身のターン開始時に" + p[1] + "のダメージを受ける。";
        }
        if (content.Type == EffectContentType.InvalidateTriggeredEffect && triggers.Count == 1)
        {
            Args(content.Parameters, 0, "InvalidateTriggeredEffect");
            var c = triggers[0].Conditions;
            if (c.Or.Count == 0 && c.And.Count == 3 && c.And.Count(x => x.Type == ConditionType.ExcludedWeaponCategories) == 1)
            {
                string[] cats = Args(c.And.First(x => x.Type == ConditionType.ExcludedWeaponCategories).Parameters, 2, "ExcludedWeaponCategories");
                if (HasTrigger(triggers[0], TriggerTiming.BeforeEffectActivation, Condition(ConditionType.EffectInvolvesSelf), Condition(ConditionType.RequiresSameMeleeGroup), Condition(ConditionType.ExcludedWeaponCategories, cats)))
                    return CategoryAlternatives(cats, "及び") + "を除き、自身または自身を対象にする、同じ接近グループを条件とする効果の発動は無効になる。";
            }
        }
        if (content.Type == EffectContentType.RemoveState)
        {
            string[] p = Args(content.Parameters, 2, "RemoveState");
            if (p[0] != "Self" || p[1] != state.Name) throw new InvalidOperationException("解除はSelfと自分の状態名を指定してください。");
            if (triggers.Count == 1 && triggers[0].Conditions.And.Count == 1 && triggers[0].Conditions.And[0].Type == ConditionType.RelatedStateRemoved)
            {
                string[] c = Args(triggers[0].Conditions.And[0].Parameters, 2, "RelatedStateRemoved");
                if (HasTrigger(triggers[0], TriggerTiming.StateRemoved, Condition(ConditionType.RelatedStateRemoved, c))) return Actor(c[0]) + "との《" + c[1] + "》状態が解除された時、この状態も解除される。";
            }
            if (triggers.Count == 2 && triggers[0].Conditions.And.Count == 1 && triggers[0].Conditions.And[0].Type == ConditionType.OwnState)
            {
                string[] c = Args(triggers[0].Conditions.And[0].Parameters, 1, "OwnState");
                if (Matches(triggers, Trigger(TriggerTiming.StateRemoved, Condition(ConditionType.OwnState, c)), Trigger(TriggerTiming.StateReapplied, Condition(ConditionType.OwnState, c))))
                    return "自身の《" + c[0] + "》状態が解除される、または付与されなおした時、この状態も解除される。";
            }
            if (triggers.Count == 1 && HasTrigger(triggers[0], TriggerTiming.CharacterIncapacitated, Condition(ConditionType.ActionSource, "Applier")))
                return "この状態は付与者が戦闘不能になった時、解除される。";
        }
        throw new InvalidOperationException("未対応の状態のトリガーと効果内容です：" + content.Type);
    }
}
