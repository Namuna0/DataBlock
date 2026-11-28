using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
public static partial class SkillTextConverter
{
    internal static bool TryStateHeader(string line, out StateDefinition state, out string body, bool allowShorthand = false)
    {
        string text = Unbullet(line);
        var header = M(text, @"^(?<name>[^・⚫【《：:〈〉]+?)効果(?<tags>(?:〈[^〉]+〉)*)[：:](?<body>.*)$");
        if (!header.Success && (allowShorthand || text.StartsWith("《", StringComparison.Ordinal)))
            header = M(text, @"^(?:《(?<name>[^》]+)》|(?<name>[^《【●：:〈〉]+?))(?:効果)?(?<tags>(?:〈[^〉]+〉)+|効果)(?:効果)?[：:](?<body>.*)$");
        if (header.Success && header.Groups["name"].Value != "効果")
        {
            state = new StateDefinition { Name = Need(header.Groups["name"].Value, "状態名") };
            string tags = header.Groups["tags"].Value;
            if (tags.Length > 0 && tags != "効果") state.Categories = Categories(tags);
            body = header.Groups["body"].Value.Trim();
            return true;
        }
        if (allowShorthand)
        {
            var shorthand = M(text, @"^([^《【●：:]+)[：:](自身の.+?は[×x].+?される[。.]?)$");
            if (shorthand.Success)
            {
                state = new StateDefinition { Name = Need(shorthand.Groups[1].Value, "状態名"), Categories = new List<string> { "スタック" } };
                body = shorthand.Groups[2].Value;
                return true;
            }
        }
        state = null;
        body = "";
        return false;
    }

    internal static void NormalizeStateTrigger(StateDefinition state)
    {
        if (state.Effects.Count != 0 || state.Overrides.Count != 1 ||
            state.Trigger.Timing != TriggerTiming.Always || !Empty(state.Trigger.Conditions)) return;
        var modifier = state.Overrides[0];
        if (modifier.Triggers.Count != 1) return;
        var trigger = modifier.Triggers[0];
        if (trigger.Timing != TriggerTiming.IncomingDamage && trigger.Timing != TriggerTiming.AttackPower) return;
        state.Trigger = trigger;
        modifier.Triggers.Clear();
    }

    private static void AddStateEffect(StateDefinition state, List<TriggerDefinition> triggers, params EffectContent[] contents)
    {
        state.Effects.Add(new EffectDefinition { Type = EffectType.Passive, Triggers = triggers, Contents = contents.ToList() });
    }
    private static bool ReadStateLine(StateDefinition state, string text, bool compound = false, bool allowSkillSentences = true)
    {
        string source = Unbullet(text);
        text = RegexReplace(source, @"^さらに[、]?\s*", "").TrimStart('*', '＊').Trim();
        text = RegexReplace(text, @"^再び付与者が《([^》]+)》発動する時", "再び付与者が《$1》を発動する時");
        text = RegexReplace(text, @"^自身の(〈[^〉]+〉)かつ(〈[^〉]+〉)による威力が×", "自身は$1かつ$2による威力は×");
        text = RegexReplace(text, @"^自身の(〈[^〉]+〉)及び(〈[^〉]+〉)による目標値は", "自身は$1と$2の目標値が");
        text = RegexReplace(text, @"^自身の([^〈〉\[\]]+B)が([+-][0-9.]+)される。?$", "自身の$1が$2加算される。");
        text = RegexReplace(text, @"^自身は(?=\[[^\]]+\]による行為判定の達成値が)", "");
        text = text.Replace("自身のが与える威力は", "自身が与える威力が");
        text = text.Replace("。。", "。").Replace("効果適用後この状態は解除される", "効果適用後この効果は解除される");
        text = RegexReplace(text, @"^自身の(?=\[[^\]]+\]による行為判定の達成値[はが])", "");
        text = RegexReplace(text, @"^自身の(〈[^〉]+〉)かつ(〈[^〉]+〉)による威力は", "自身は$1かつ$2による威力は");
        text = RegexReplace(text, @"^自身の([^,]+B, [^,]+B, [^,]+Bによる達成値は)([0-9.]+)倍", "自身の$1×$2倍");
        text = RegexReplace(text, @"^自身は(〈[^〉]+〉)を宣言する事はできない", "自身は$1を宣言する事は出来ない");
        if (compound)
        {
            if (ReadSemanticRule(null, state, EffectType.Passive, text) || ReadCompoundStatePower(state, text)) return true;
            if (!CommonEffectTextCodec.TryReadRuleContents(text, out var clauses)) return false;
            AddStateEffect(state, NoTriggers(), clauses.ToArray());
            return true;
        }
        if (ReadStateEffectStatement(state, text)) return true;
        Match m = M(text, @"^自身のターン開始時に対象を(.+?)の威力で攻撃する。?$");
        if (m.Success)
        {
            AddStateEffect(state, On(Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Self"))),
                Content(EffectContentType.SkillAttack, "Target", m.Groups[1].Value)); return true;
        }
        m = M(text, @"^自身は毎ターン開始時《([^》]+)》状態になる。?$");
        if (m.Success)
        {
            AddStateEffect(state, On(Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Any"))), Content(EffectContentType.ApplyState, "Self", m.Groups[1].Value)); return true;
        }
        m = M(text, @"^自身は自身のターン開始時に(.+?)のダメージを受ける。?$");
        if (m.Success)
        {
            AddStateEffect(state, On(Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Self"))), Content(EffectContentType.Damage, "Self", m.Groups[1].Value)); return true;
        }
        if (CommonEffectTextCodec.TryReadAbilityCheckMultiplier(text, out var checkMultiplier))
        {
            AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), checkMultiplier);
            return true;
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
            AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(EffectContentType.MultiplyDamageTaken, ActorKey(m.Groups[1].Value), m.Groups[2].Value)); return true;
        }
        m = M(text, @"^(自身|対象|付与者)のカウンター効果による達成値(?:が|は)([+-].+?)される。?$");
        if (m.Success)
        {
            if (state.Effects.Count + state.Overrides.Count != 0) throw new InvalidOperationException("この状態の共通トリガーは既に設定されています。");
            state.Trigger = Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, ActorKey(m.Groups[1].Value)), Condition(ConditionType.EffectKind, "Counter"));
            AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(EffectContentType.AddActionResult, Signed(m.Groups[2].Value))); return true;
        }
        m = M(text, @"^(自身|対象|付与者)の〈([^〉]+)〉による威力(?:が|は)×(.+?)倍される。?$");
        if (m.Success)
        {
            if (state.Effects.Count + state.Overrides.Count != 0) throw new InvalidOperationException("この状態の共通トリガーは既に設定されています。");
            state.Trigger = Trigger(TriggerTiming.AttackPower, Condition(ConditionType.ActionSource, ActorKey(m.Groups[1].Value)), Condition(ConditionType.ActionCategory, m.Groups[2].Value));
            AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(EffectContentType.MultiplyPower, m.Groups[3].Value)); return true;
        }
        m = M(text, @"^(自身|対象|付与者)からの〈([^〉]+)〉の回避達成値(?:が|は)([+-].+?)される。?$");
        if (m.Success)
        {
            if (state.Effects.Count + state.Overrides.Count != 0) throw new InvalidOperationException("この状態の共通トリガーは既に設定されています。");
            state.Trigger = Trigger(TriggerTiming.EvasionResult, Condition(ConditionType.ActionSource, ActorKey(m.Groups[1].Value)), Condition(ConditionType.ActionCategory, m.Groups[2].Value));
            AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(EffectContentType.AddEvasionResult, Signed(m.Groups[3].Value))); return true;
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
        if (CommonEffectTextCodec.TryReadContinuation(state.Effects.LastOrDefault()?.Contents, text)) return true;
        if (!allowSkillSentences && TryReadCommonSkillEffect(source, out _)) return false;
        if (!CommonEffectTextCodec.TryRead(source, out var contents)) return false;
        if (contents.All(content => content.Type == EffectContentType.ModifyStat))
            AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), contents.ToArray());
        else
            AddStateEffect(state, NoTriggers(), contents.ToArray());
        return true;
    }
    internal static bool ReadStateText(StateDefinition state, string text, SkillBody skill = null, EffectType[] sections = null, bool allowSkillSentences = true)
    {
        var sentences = text.TrimEnd('。').Contains("。") ? Split(text, "。") : null;
        if (sentences == null || sentences.Count < 2) return ReadStateLine(state, text, allowSkillSentences: allowSkillSentences);
        if (ReadStateLine(state, text, compound: true)) return true;
        bool roleplay = sections?.Contains(EffectType.Roleplay) == true;
        for (int i = 0; i < sentences.Count; i++)
        {
            string sentence = sentences[i];
            if (i + 1 < sentences.Count)
            {
                string pair = sentence + sentences[i + 1];
                if (ReadStateLine(state, pair, compound: true) ||
                    (sections?.Length == 1 && ReadSemanticRule(skill, null, sections[0], NormalizeEffectSentence(pair))))
                {
                    i++;
                    continue;
                }
            }
            // A sentence after an inline state can resume the enclosing skill.
            if (ReadStateLine(state, sentence, allowSkillSentences: false)) continue;
            if (roleplay)
            {
                ReadSkillLine(skill, EffectType.Roleplay, string.Concat(sentences.Skip(i)));
                return true;
            }
            if (sections == null || sections.Length == 0 || sentence.StartsWith("*") || sentence.StartsWith("＊"))
                throw new InvalidOperationException("未対応の状態効果です：" + sentence);
            foreach (EffectType section in sections) ReadSkillLine(skill, section, sentence);
        }
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
    internal static List<string> StateTexts(StateDefinition state, HashSet<string> referencedStates = null)
    {
        if (state.Effects == null || state.Overrides == null) throw new InvalidOperationException("状態のEffectsまたはOverridesがnullです。");
        var texts = new List<string>();
        foreach (EffectDefinition effect in state.Effects)
        {
            if (effect == null || effect.Type != EffectType.Passive || effect.Contents == null || effect.Contents.Count == 0) throw new InvalidOperationException("状態の通常効果は、内容のあるPassiveにしてください。");
            var triggers = StateTriggers(state, effect.Triggers);
            bool unconditional = Matches(triggers, Trigger(TriggerTiming.Always));
            string group = WriteCommonGroup(effect.Contents, unconditional, referencedStates);
            if (group != null && !unconditional)
                throw new InvalidOperationException("追加の発動条件を持つ状態効果を一つの文にはまとめられません。");
            if (group != null) texts.Add(group);
            else foreach (EffectContent content in effect.Contents) texts.Add(StateContentText(state, triggers, content));
        }
        foreach (EffectDefinition effect in state.Overrides)
        {
            if (effect == null || effect.Type != EffectType.Passive || effect.Contents == null || effect.Contents.Count == 0) throw new InvalidOperationException("状態の上書き効果は、内容のあるPassiveにしてください。");
            foreach (EffectContent content in effect.Contents) texts.Add(StateOverrideText(StateTriggers(state, effect.Triggers), content));
        }
        return texts;
    }
    private static string StateOverrideText(List<TriggerDefinition> triggers, EffectContent content)
    {
        string damageRecovery = DamageRecoveryStateModifierText(triggers, content);
        if (damageRecovery != null) return damageRecovery;
        string timedCategory = TimedAndCategoryStateModifierText(triggers, content);
        if (timedCategory != null) return timedCategory;
        string evasion = EvasionStateModifierText(triggers, content);
        if (evasion != null) return evasion;
        string defense = DefenseStateModifierText(triggers, content);
        if (defense != null) return defense;
        string attribute = AttributeStateModifierText(triggers, content);
        if (attribute != null) return attribute;
        string basic = BasicModifierText(new EffectDefinition { Type = EffectType.Passive, Triggers = triggers }, content);
        if (basic != null) return basic;
        if (triggers.Count != 1 || content == null || !ValidTrigger(triggers[0])) throw new InvalidOperationException("状態の数値上書きにはトリガーを1個指定してください。");
        TriggerDefinition t = triggers[0];
        var c = t.Conditions;
        if (t.Timing == TriggerTiming.IncomingDamage && Empty(c) && content.Type == EffectContentType.MultiplyDamageTaken)
        {
            string[] p = Args(content.Parameters, 2, "MultiplyDamageTaken");
            return Actor(p[0]) + "の被ダメージは" + p[1] + "倍される。";
        }
        if (t.Timing == TriggerTiming.ActionResult && c.Or.Count == 0 && c.And.Count == 2 && c.And.Count(x => x.Type == ConditionType.ActionSource) == 1 && c.And.Count(x => x.Type == ConditionType.EffectKind) == 1 && content.Type == EffectContentType.AddActionResult)
        {
            string source = Args(c.And.First(x => x.Type == ConditionType.ActionSource).Parameters, 1, "ActionSource")[0];
            string kind = Args(c.And.First(x => x.Type == ConditionType.EffectKind).Parameters, 1, "EffectKind")[0];
            if (kind == "Counter") return Actor(source) + "のカウンター効果による達成値は" + Signed(Args(content.Parameters, 1, "AddActionResult")[0]) + "される。";
        }
        if (content.Type == EffectContentType.StatRollFactorForAction && Matches(triggers, Trigger(TriggerTiming.Always)))
        {
            var operands = VariableArgs(content.Parameters, 2, "StatRollFactorForAction");
            return CommonEffectTextCodec.AbilityCheckMultiplierText(operands.Take(operands.Length - 1), operands.Last(), state: true);
        }
        if (c.Or.Count == 0 && c.And.Count == 2 && c.And.Count(x => x.Type == ConditionType.ActionSource) == 1 && c.And.Count(x => x.Type == ConditionType.ActionCategory) == 1)
        {
            string source = Args(c.And.First(x => x.Type == ConditionType.ActionSource).Parameters, 1, "ActionSource")[0];
            string category = Args(c.And.First(x => x.Type == ConditionType.ActionCategory).Parameters, 1, "ActionCategory")[0];
            string value = Args(content.Parameters, 1, content.Type.ToString())[0];
            if (t.Timing == TriggerTiming.AttackPower && content.Type == EffectContentType.MultiplyPower) return Actor(source) + "の〈" + category + "〉による威力は×" + value + "倍される。";
            if (t.Timing == TriggerTiming.EvasionResult && content.Type == EffectContentType.AddEvasionResult) return Actor(source) + "からの〈" + category + "〉の回避達成値は" + Signed(value) + "される。";
        }
        throw new InvalidOperationException("未対応の状態の上書きトリガーと内容です。");
    }
    private static string StateContentText(StateDefinition state, List<TriggerDefinition> triggers, EffectContent content)
    {
        string timedCategory = TimedAndCategoryStateContentText(state, triggers, content);
        if (timedCategory != null) return timedCategory;
        string skillControl = SkillControlStateContentText(triggers, content);
        if (skillControl != null) return skillControl;
        string stack = StackStateContentText(state, triggers, content);
        if (stack != null) return stack;
        string basic = BasicStateContentText(state, triggers, content);
        if (basic != null) return basic;
        if (triggers == null || content == null || triggers.Any(x => !ValidTrigger(x))) throw new InvalidOperationException("状態のトリガーまたは内容が不正です。");
        if (content.Type == EffectContentType.SkillAttack &&
            Matches(triggers, Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Self"))))
        {
            string[] p = Args(content.Parameters, 2, "SkillAttack");
            Require(p[0] == "Target");
            return "自身のターン開始時に対象を" + p[1] + "の威力で攻撃する。";
        }
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
        if (content.Type == EffectContentType.RemoveState && !Matches(triggers, Trigger(TriggerTiming.Always)))
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
        if (Matches(triggers, Trigger(TriggerTiming.Always)))
        {
            string shared = CommonEffectTextCodec.Write(content);
            if (shared != null && content.Type == EffectContentType.RuleModifier && shared.EndsWith("される", StringComparison.Ordinal))
                return shared + "。";
            if (shared != null) return shared;
        }
        throw new InvalidOperationException("未対応の状態のトリガーと効果内容です：" + content.Type);
    }
}
