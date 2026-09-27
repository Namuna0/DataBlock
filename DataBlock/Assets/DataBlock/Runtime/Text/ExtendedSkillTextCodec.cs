using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// Additional grammar is semantic and independent of skill/state display names.
public static partial class SkillTextConverter
{
    private static void ExtendedEffect(SkillBody skill, EffectType type, EffectContent content, params TriggerDefinition[] triggers)
    {
        skill.Effects.Add(new EffectDefinition { Type = type, Contents = new List<EffectContent> { content }, Triggers = triggers.ToList() });
    }
    private static bool ReadReactionCondition(ConditionSet set, string text)
    {
        Match m = M(text, @"^自身が[《〈]([^》〉]+)[》〉]を受けた時、その対象へ(?:宣言可能。)?$");
        if (m.Success) { set.And.Add(Condition(ConditionType.ReactionTarget, m.Groups[1].Value, "Self", "Source")); return true; }
        m = M(text, @"^〈([^〉]+)〉の対象になった味方キャラクターを対象に自動発動。?$");
        if (m.Success) { set.And.Add(Condition(ConditionType.ReactionTarget, m.Groups[1].Value, "Ally", "Receiver", "Automatic")); return true; }
        return false;
    }
    private static string ReactionConditionText(ConditionEntry item)
    {
        string[] p = VariableArgs(item.Parameters, 3, "ReactionTarget");
        if (p.SequenceEqual(new[] { p[0], "Self", "Source" })) return "自身が〈" + p[0] + "〉を受けた時、その対象へ";
        if (p.SequenceEqual(new[] { p[0], "Ally", "Receiver", "Automatic" })) return "〈" + p[0] + "〉の対象になった味方キャラクターを対象に自動発動";
        throw new InvalidOperationException("反応対象の指定が不正です。");
    }
    private static bool ReadExtendedSkill(SkillBody skill, EffectType type, string text)
    {
        string s = RegexReplace(text.Trim(), @"^さらに[、]?\s*", "");
        if (type == EffectType.Roleplay)
        {
            ExtendedEffect(skill, type, Content(EffectContentType.RoleplayDescription, Need(s, "ロールプレイ効果"))); return true;
        }
        Match m = M(s, @"^キャラクター作成時に((?:《[^》]+》)(?:及び《[^》]+》)*)習得不能。?$");
        if (m.Success && type == EffectType.Passive)
        {
            ExtendedEffect(skill, type, Content(EffectContentType.ProhibitAcquisition, new[] { "Self", "CharacterCreation" }.Concat(AllMatches(m.Groups[1].Value, @"《([^》]+)》").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray())); return true;
        }
        m = M(s, @"^自身がパーティーを組んでいる時、自身を除く味方の受ける([A-Z]+)ダメージを×(.+?)倍しても良い。?$");
        if (m.Success && type == EffectType.Passive)
        {
            AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.InParty, "Self"))), Change(OverrideContentType.MultiplyResourceDamage, "AllAlliesExceptSelf", m.Groups[1].Value, m.Groups[2].Value, "Optional")); return true;
        }
        m = M(s, @"^自身を〈([^〉]+)〉ではない物として扱うかその都度任意で選択可能。?$");
        if (m.Success && type == EffectType.Passive) { ExtendedEffect(skill, type, Content(EffectContentType.OptionalCategoryExclusion, "Self", m.Groups[1].Value, "PerOccurrence")); return true; }
        m = M(s, @"^自身が食事セット効果を受けていない時、([0-9]+)ターン目に(?:自身は)?《([^》]+)》状態になる。?$");
        if (m.Success && type == EffectType.Passive)
        {
            ExtendedEffect(skill, type, Content(EffectContentType.ApplyState, "Self", m.Groups[2].Value), Trigger(TriggerTiming.TurnStart, Condition(ConditionType.HasMealEffect, "Self", "False"), Condition(ConditionType.TurnNumber, m.Groups[1].Value))); return true;
        }
        m = M(s, @"^自身を除く味方全員の\[([^\]]+)\]による行為判定の達成値が\+(.+?)される。?$");
        if (m.Success && type == EffectType.Passive)
        {
            AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "AllAlliesExceptSelf"), Condition(ConditionType.ActionStat, m.Groups[1].Value))), Change(OverrideContentType.AddActionResult, m.Groups[2].Value)); return true;
        }
        m = M(s, @"^(自身|対象)へ付与する《([^》]+)》スタックの付与が([0-9]+)に変化する。?$");
        if (m.Success && type == EffectType.Critical) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetStackAmount, ActorKey(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value)); return true; }
        m = M(s, @"^このアクティブ効果による威力は(.+?)(?:属性威力は(.+?))?に変化する。?$");
        if (m.Success && (type == EffectType.SecondSpike || type == EffectType.ThirdSpike))
        {
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetAttackComponent, "Power", m.Groups[1].Value));
            if (m.Groups[2].Success) AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetAttackComponent, "AttributePower", m.Groups[2].Value)); return true;
        }
        m = M(s, @"^このアクティブ効果による属性威力は(.+?)に変化する。?$");
        if (m.Success && (type == EffectType.SecondSpike || type == EffectType.ThirdSpike)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetAttackComponent, "AttributePower", m.Groups[1].Value)); return true; }
        // Sentences following a timed state application are separate effects.
        if (s.Contains("。さらに")) { foreach (string part in s.Split(new[] { "。さらに" }, StringSplitOptions.None)) ReadSkillLine(skill, type, part.TrimEnd('。') + "。"); return true; }
        EffectContent content = ReadExtendedContent(s);
        if (content != null && IsOrdinary(type))
        {
            if (content.Type == EffectContentType.RemoveSummon)
            {
                Require(type == EffectType.Declaration && skill is SummonedEntityDefinition);
                ExtendedEffect(skill, type, content, Trigger(TriggerTiming.AfterSkillResolution));
            }
            else ExtendedEffect(skill, type, content);
            return true;
        }
        return false;
    }
    private static EffectContent ReadExtendedContent(string s)
    {
        Match m = M(s, @"^(自身|対象)(?:に|は)《([^》]+)》スタックを([0-9]+)(?:付与する|獲得する)。?$");
        if (m.Success) return Content(EffectContentType.GainStack, ActorKey(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value);
        m = M(s, @"^対象を(.+?)の威力\+(.+?)の属性威力で攻撃する。?$");
        if (m.Success) return Content(EffectContentType.SkillAttack, "Target", "PowerAndAttribute", m.Groups[1].Value, m.Groups[2].Value);
        m = M(s, @"^対象の《([^》]+)》スタックをすべて消費して、被ダメージを×(.+?)倍増加する。?$");
        if (m.Success) return Content(EffectContentType.ConsumeStacksForDamage, "Target", m.Groups[1].Value, "All", m.Groups[2].Value, "ThisAttack");
        m = M(s, @"^《([^》]+)》罠を設置する。?$");
        if (m.Success) return Content(EffectContentType.PlaceTrap, m.Groups[1].Value);
        if (s.TrimEnd('。') == "スキルの処理後にこの罠は除去される") return Content(EffectContentType.RemoveSummon, "Self");
        m = M(s, @"^HP(.+?)回復。?$");
        if (m.Success) return Content(EffectContentType.RestoreResource, "Self", "HP", m.Groups[1].Value);
        return null;
    }
    private static string ExtendedContentText(EffectContent content)
    {
        if (content == null) return null;
        string[] p;
        switch (content.Type)
        {
            case EffectContentType.RoleplayDescription: return Args(content.Parameters, 1, "RoleplayDescription")[0];
            case EffectContentType.ProhibitAcquisition:
                p = VariableArgs(content.Parameters, 3, "ProhibitAcquisition"); Require(p[0] == "Self" && p[1] == "CharacterCreation");
                return "キャラクター作成時に" + string.Join("及び", p.Skip(2).Select(x => "《" + x + "》")) + "習得不能。";
            case EffectContentType.OptionalCategoryExclusion:
                p = Args(content.Parameters, 3, "OptionalCategoryExclusion"); Require(p[0] == "Self" && p[2] == "PerOccurrence"); return "自身を〈" + p[1] + "〉ではない物として扱うかその都度任意で選択可能。";
            case EffectContentType.GainStack:
                if (content.Parameters == null || content.Parameters.Count != 3) return null;
                p = Args(content.Parameters, 3, "GainStack"); Number(p[2], 1, "スタック数"); return Actor(p[0]) + "に《" + p[1] + "》スタックを" + p[2] + "付与する。";
            case EffectContentType.SkillAttack:
                if (content.Parameters == null || content.Parameters.Count != 4 || content.Parameters[1] != "PowerAndAttribute") return null;
                p = Args(content.Parameters, 4, "SkillAttack"); Require(p[0] == "Target"); return "対象を" + p[2] + "の威力+" + p[3] + "の属性威力で攻撃する。";
            case EffectContentType.ConsumeStacksForDamage:
                p = Args(content.Parameters, 5, "ConsumeStacksForDamage"); Require(p[0] == "Target" && p[2] == "All" && p[4] == "ThisAttack"); return "対象の《" + p[1] + "》スタックをすべて消費して、被ダメージを×" + p[3] + "倍増加する。";
            case EffectContentType.PlaceTrap: return "《" + Args(content.Parameters, 1, "PlaceTrap")[0] + "》罠を設置する。";
            case EffectContentType.RemoveSummon: Require(Args(content.Parameters, 1, "RemoveSummon")[0] == "Self"); return "スキルの処理後にこの罠は除去される。";
        }
        return null;
    }
    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("効果の対象・条件・パラメーターの組み合わせが不正です。"); }
    private static string ExtendedTriggerText(List<TriggerDefinition> triggers)
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
    private static string ExtendedOverrideText(OverrideDefinition effect, OverrideContent content)
    {
        if (effect == null || content == null || effect.Triggers == null) return null;
        var t = effect.Triggers; string[] p;
        if (content.Type == OverrideContentType.MultiplyResourceDamage)
        {
            p = Args(content.Parameters, 4, "MultiplyResourceDamage"); Require(effect.Type == EffectType.Passive && p[0] == "AllAlliesExceptSelf" && p[3] == "Optional" && Matches(t, Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.InParty, "Self"))));
            return "自身がパーティーを組んでいる時、自身を除く味方の受ける" + p[1] + "ダメージを×" + p[2] + "倍しても良い。";
        }
        if (content.Type == OverrideContentType.SetStackAmount)
        {
            p = Args(content.Parameters, 3, "SetStackAmount"); Require(effect.Type == EffectType.Critical && t.Count == 0); Number(p[2], 1, "スタック数"); return Actor(p[0]) + "へ付与する《" + p[1] + "》スタックの付与が" + p[2] + "に変化する。";
        }
        if (content.Type == OverrideContentType.SetAttackComponent && content.Parameters != null && content.Parameters.Count == 2 && content.Parameters[0] == "Power")
        {
            p = Args(content.Parameters, 2, "SetAttackComponent"); Require(t.Count == 0 && (effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike)); return "このアクティブ効果による威力は" + p[1] + "に変化する。";
        }
        if (content.Type == OverrideContentType.AddActionResult && t.Count == 1 && t[0].Conditions.And.Count == 2)
        {
            var c = t[0].Conditions.And;
            if (c[0].Type == ConditionType.ActionSource && c[0].Parameters.SequenceEqual(new[] { "AllAlliesExceptSelf" }) && c[1].Type == ConditionType.ActionStat)
            {
                Require(effect.Type == EffectType.Passive && t[0].Timing == TriggerTiming.ActionResult && t[0].Conditions.Or.Count == 0);
                return "自身を除く味方全員の[" + Args(c[1].Parameters, 1, "ActionStat")[0] + "]による行為判定の達成値が" + Signed(Args(content.Parameters, 1, "AddActionResult")[0]) + "される。";
            }
        }
        return ExtendedStateOverrideText(effect, content);
    }
}

