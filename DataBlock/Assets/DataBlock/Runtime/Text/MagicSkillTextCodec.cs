using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static string[] PrepareMagicLines(string[] lines)
    {
        bool flavor = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (M(lines[i].Trim(), @"^―{8,}$").Success) flavor = true;
            if (flavor) continue;
            lines[i] = lines[i].Replace("（エルフ魔法〉", "〈エルフ魔法〉").Replace("ここのパッシブ", "このパッシブ");
            if (i + 1 < lines.Length && ((lines[i].Contains("複数の〈") && lines[i].TrimEnd().EndsWith("を発動させた場合、", StringComparison.Ordinal)) ||
                (lines[i].Contains("次のライフパスを一つ選択") && lines[i + 1].TrimStart().StartsWith("《", StringComparison.Ordinal))))
            { lines[i] += lines[i + 1].Trim(); lines[i + 1] = ""; }
        }
        return lines;
    }

    private static bool ReadMagicSkill(SkillBody skill, EffectType type, string body)
    {
        string s = RegexReplace(body.Trim(), @"^さらに(?:追加で|、)?\s*", "");
        Match m;
        if (ReadOutsiderRule(skill, type, s)) return true;
        if (ReadMagicModifier(skill, type, s)) return true;
        // Equivalent spellings are handed to the established grammar.
        m = M(s, @"^([A-Z]+最大値)が([0-9.]+)倍される。?$");
        if (m.Success) { ReadSkillLine(skill, type, m.Groups[1].Value + "が×" + m.Groups[2].Value + "倍される。"); return true; }
        m = M(s, @"^\[([^\]]+)\]による行為判定の達成値が×(.+?)倍される。?$");
        if (m.Success) { ReadSkillLine(skill, type, "[" + m.Groups[1].Value + "]による行為判定を行う際、達成値が×" + m.Groups[2].Value + "倍される。"); return true; }
        m = M(s, @"^一日の採取回数上限が([+-][0-9]+)増加する[、。]?$" );
        if (m.Success) { ReadSkillLine(skill, type, "一日の採取回数上限が" + m.Groups[1].Value + "追加される。"); return true; }
        m = M(s, @"^自身の([A-Z]+最大値)は(-[0-9]+)減少する。?$");
        if (m.Success) { ReadSkillLine(skill, type, m.Groups[1].Value + m.Groups[2].Value); return true; }
        m = M(s, @"^([A-Z]+)を([0-9]+)回復する。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.RestoreResource, "Self", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^〈([^〉]+)〉かつ〈([^〉]+)〉の発動時にHPは(-[0-9]+)される。?$");
        if (m.Success)
        {
            ExtendedEffect(skill, type, Content(EffectContentType.ModifyResource, "Self", "HP", m.Groups[3].Value), Trigger(TriggerTiming.ActionActivated, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, m.Groups[1].Value), Condition(ConditionType.ActionCategory, m.Groups[2].Value))); return true;
        }
        m = M(s, @"^自身が〈([^〉]+)〉かつ〈([^〉]+)〉を発動した時、自身のHPを(-[0-9]+)変化させる。?$");
        if (m.Success) return ReadMagicSkill(skill, type, "〈" + m.Groups[1].Value + "〉かつ〈" + m.Groups[2].Value + "〉の発動時にHPは" + m.Groups[3].Value + "される。");
        m = M(s, @"^《([^》]+)》状態を([0-9]+)スタック得る。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.GainStack, "Self", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^スタックが([0-9]+)になった時、HPとMPが全回復して《([^》]+)》状態になる。?$");
        if (m.Success)
        {
            var gain = skill.Effects.SelectMany(e => e.Contents).LastOrDefault(c => c.Type == EffectContentType.GainStack);
            Require(gain != null && gain.Parameters[0] == "Self");
            ExtendedEffect(skill, type, Content(EffectContentType.TransformAtStacks, "Self", gain.Parameters[1], "Equals", m.Groups[1].Value, "HP", "MP", m.Groups[2].Value)); return true;
        }
        m = M(s, @"^〈([^〉]+)〉を発動するたびに《([^》]+)》〈([^〉]+)〉状態を([0-9]+)スタック得る。?$");
        if (m.Success)
        {
            ExtendedEffect(skill, type, Content(EffectContentType.CyclingStateStacks, "Self", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, "Unspecified", "ACT", "Unspecified", "0")); return true;
        }
        m = M(s, @"^([0-9]+)スタック得た時、([A-Z]+)は\+?([0-9]+)回復して、スタックは([0-9]+)になる。?$");
        if (m.Success)
        {
            var cycle = skill.Effects.SelectMany(e => e.Contents).LastOrDefault(c => c.Type == EffectContentType.CyclingStateStacks);
            Require(cycle != null); cycle.Parameters[5] = m.Groups[1].Value; cycle.Parameters[6] = m.Groups[2].Value; cycle.Parameters[7] = m.Groups[3].Value; cycle.Parameters[8] = m.Groups[4].Value; return true;
        }
        m = M(s, @"^《([^》]+)》による(?:の)?採取の結果を一日([0-9]+)回までやり直すことが出来る。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.RerollGathering, m.Groups[1].Value, "Day", m.Groups[2].Value)); return true; }
        if (s.TrimEnd('。') == "装備部位が両手である武器を片手で装備する事が出来る") { ExtendedEffect(skill, type, Content(EffectContentType.EquipSlotSubstitution, "Weapon", "BothHands", "OneHand")); return true; }
        m = M(s, @"^対象に《([^》]+)》状態をスタック([0-9]+)与える。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.GainStack, "Target", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^対象の現在の《([^》]+)》状態は解除され[、。]?$" );
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.RemoveState, "Target", m.Groups[1].Value)); return true; }
        m = M(s, @"^対象の《([^》]+)》状態を解除する。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.RemoveState, "Target", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身と対象は新しい接近グループの《([^》]+)》状態になる。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.CreateMeleeGroup, "Self", "Target", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身を除く、自身と同じ接近グループのキャラクター全てに([0-9]+)ターンの間《([^》]+)》状態を付与する。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.ApplyState, "SameMeleeExceptSelf", m.Groups[2].Value, m.Groups[1].Value)); return true; }
        m = M(s, @"^自身を対象にした達成値([0-9]+)以下の〈([^〉]+)〉を無効にしても良い。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.OptionalInvalidateAction, "Self", m.Groups[2].Value, "AtMost", m.Groups[1].Value)); return true; }
        m = M(s, @"^すべての敵の([A-Z]+)を-(.+?)減少させる。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.DrainResource, "AllEnemies", m.Groups[1].Value, m.Groups[2].Value, "Self", "Unspecified", "Unspecified")); return true; }
        if (s == "その合計値のMPを回復する。" || s == "この回復効果はMPの最大値を超過する事が出来る。")
        {
            var drain = skill.Effects.SelectMany(e => e.Contents).LastOrDefault(c => c.Type == EffectContentType.DrainResource);
            Require(drain != null && drain.Parameters[1] == "MP");
            if (s.StartsWith("その", StringComparison.Ordinal)) drain.Parameters[4] = "ActualTotal"; else drain.Parameters[5] = "AllowOverflow";
            return true;
        }
        if (s.TrimEnd('。') == "食事及び食事セット効果による, リソース減少及びステータス減少を受けない") { ExtendedEffect(skill, type, Content(EffectContentType.PreventMealPenalties, "Self", "MealAndMealSet", "ResourceAndStatDecrease")); return true; }
        m = M(s, @"^キャラクター作成時、自身は次のライフパスを一つ選択して追加で習得する。((?:《[^》]+》)(?:,\s*《[^》]+》)*)$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.GrantCreationChoice, new[] { "LifePath", "1" }.Concat(AllMatches(m.Groups[1].Value, @"《([^》]+)》").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray())); return true; }
        m = M(s, @"^〈([^〉]+)〉の種族特性を一つ選んで追加で習得する。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.GrantRaceTrait, m.Groups[1].Value, "1")); return true; }
        if (s.TrimEnd('。') == "対象のカウンター効果を無効にする") { ExtendedEffect(skill, type, Content(EffectContentType.InvalidateTriggeredEffect, "Target", "Counter")); return true; }
        if (s.TrimEnd('。') == "発動ロールをやり直す事が出来る") { ExtendedEffect(skill, type, Content(EffectContentType.RerollActivation, "Self", "Original", "TriggeringRoll")); return true; }
        if (s != body.Trim()) { ReadSkillLine(skill, type, s); return true; }
        return false;
    }

    private static string MagicContentText(EffectContent content)
    {
        if (content == null) return null;
        string[] p;
        switch (content.Type)
        {
            case EffectContentType.RerollGathering:
                p = Args(content.Parameters, 3, "RerollGathering"); Require(p[1] == "Day"); Number(p[2], 1, "使用回数"); return "《" + p[0] + "》による採取の結果を一日" + p[2] + "回までやり直すことが出来る。";
            case EffectContentType.EquipSlotSubstitution:
                Require(Args(content.Parameters, 3, "EquipSlotSubstitution").SequenceEqual(new[] { "Weapon", "BothHands", "OneHand" })); return "装備部位が両手である武器を片手で装備する事が出来る。";
            case EffectContentType.TransformAtStacks:
                p = Args(content.Parameters, 7, "TransformAtStacks"); Require(p[0] == "Self" && p[2] == "Equals" && p[4] == "HP" && p[5] == "MP"); Number(p[3], 1, "必要スタック"); return "スタックが" + p[3] + "になった時、HPとMPが全回復して《" + p[6] + "》状態になる。";
            case EffectContentType.CyclingStateStacks:
                p = Args(content.Parameters, 9, "CyclingStateStacks"); Require(p[0] == "Self"); Number(p[4], 1, "スタック獲得数"); Number(p[5], 1, "閾値"); Number(p[7], 1, "回復量"); Number(p[8], 0, "リセット値");
                return "〈" + p[1] + "〉を発動するたびに《" + p[2] + "》〈" + p[3] + "〉状態を" + p[4] + "スタック得る。\n" + p[5] + "スタック得た時、" + p[6] + "は+" + p[7] + "回復して、スタックは" + p[8] + "になる。";
            case EffectContentType.DrainResource:
                p = Args(content.Parameters, 6, "DrainResource"); Require(p[0] == "AllEnemies" && p[1] == "MP" && p[3] == "Self" && p[4] == "ActualTotal" && p[5] == "AllowOverflow");
                return "すべての敵のMPを-" + p[2] + "減少させる。\nさらに、その合計値のMPを回復する。\nこの回復効果はMPの最大値を超過する事が出来る。";
            case EffectContentType.CreateMeleeGroup:
                p = Args(content.Parameters, 3, "CreateMeleeGroup"); Require(p[0] == "Self" && p[1] == "Target"); return "自身と対象は新しい接近グループの《" + p[2] + "》状態になる。";
            case EffectContentType.PreventMealPenalties:
                Require(Args(content.Parameters, 3, "PreventMealPenalties").SequenceEqual(new[] { "Self", "MealAndMealSet", "ResourceAndStatDecrease" })); return "食事及び食事セット効果による, リソース減少及びステータス減少を受けない。";
            case EffectContentType.GrantCreationChoice:
                p = VariableArgs(content.Parameters, 3, "GrantCreationChoice"); Require(p[0] == "LifePath" && p[1] == "1"); return "キャラクター作成時、自身は次のライフパスを一つ選択して追加で習得する。\n" + string.Join(", ", p.Skip(2).Select(x => "《" + x + "》"));
            case EffectContentType.GrantRaceTrait:
                p = Args(content.Parameters, 2, "GrantRaceTrait"); Require(p[1] == "1"); return "〈" + p[0] + "〉の種族特性を一つ選んで追加で習得する。";
            case EffectContentType.OptionalInvalidateAction:
                p = Args(content.Parameters, 4, "OptionalInvalidateAction"); Require(p[0] == "Self" && p[2] == "AtMost"); Number(p[3], 0, "達成値"); return "自身を対象にした達成値" + p[3] + "以下の〈" + p[1] + "〉を無効にしても良い。";
            case EffectContentType.RemoveState:
                if (content.Parameters.Count == 2 && content.Parameters[0] == "Target") return "対象の《" + content.Parameters[1] + "》状態を解除する。"; return null;
            case EffectContentType.ApplyState:
                if (content.Parameters.Count == 3 && content.Parameters[0] == "SameMeleeExceptSelf") return "自身を除く、自身と同じ接近グループのキャラクター全てに" + content.Parameters[2] + "ターンの間《" + content.Parameters[1] + "》状態を付与する。"; return null;
            case EffectContentType.InvalidateTriggeredEffect:
                if (content.Parameters.SequenceEqual(new[] { "Target", "Counter" })) return "対象のカウンター効果を無効にする。"; return null;
            case EffectContentType.RerollActivation:
                if (content.Parameters.SequenceEqual(new[] { "Self", "Original", "TriggeringRoll" })) return "発動ロールをやり直す事が出来る。"; return null;
            case EffectContentType.OutsiderRule: return OutsiderRuleText(content);
            default: return null;
        }
    }

    private static string MagicTriggerText(List<TriggerDefinition> triggers)
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
}
