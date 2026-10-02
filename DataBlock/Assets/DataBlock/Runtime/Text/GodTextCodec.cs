using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static string RepairGodFence(string source)
    {
        var lines = source.Split('\n');
        var result = new List<string>();
        bool inside = false;
        foreach (string line in lines)
        {
            string fence = NormalizeFenceLine(line);
            if (!inside && line.Trim() == "《制約の降臨》")
            { result.Add("```"); inside = true; }
            result.Add(line);
            if (OpeningFence(fence).Success) inside = !inside;
        }
        // A plain single block without any fence must stay plain.
        if (!source.Contains("```")) return source;
        return string.Join("\n", result);
    }

    private static string[] PrepareGodLines(string[] source)
    {
        var result = new List<string>();
        bool flavor = false;
        bool minimumOne = source.Any(x => x.Trim() == "※消費ACT及びMPは0以下にはならない。");
        bool costRule = source.Any(x => x.Contains("このスキルによる消費ACT及び消費MPは自身の《"));
        for (int i = 0; i < source.Length; i++)
        {
            string line = source[i];
            if (M(line.Trim(), @"^―{8,}$").Success) flavor = true;
            if (flavor) { result.Add(line); continue; }
            if (line.Trim() == "※消費ACT及びMPは0以下にはならない。") { Require(costRule); continue; }
            if (i + 1 < source.Length && (line.TrimEnd().EndsWith("が足りない時、", StringComparison.Ordinal) || line.TrimEnd().EndsWith("その場にいる場合、", StringComparison.Ordinal)))
                line += source[++i].Trim();
            if (line.Contains("状態の効果を無視してそのキャラクターを使用する事が出来る。") && i + 1 < source.Length && source[i + 1].TrimStart().StartsWith("ただし《", StringComparison.Ordinal))
                line += source[++i].Trim();
            line = RegexReplace(line, @"このスキルの消費リソースの([A-Z]+)が足りない時、可能な限り\1を支払い不足分を次のターン開始時に消費する事でアクティブ効果を発動できる。",
                "このスキルの消費リソースの$1が足りない時、可能な限り支払い、不足分を自身の次のターン開始時に消費する事でアクティブ効果を発動できる。");
            if (line.Contains("このスキルによる消費ACT及び消費MPは自身の《"))
            {
                Require(minimumOne);
                line = RegexReplace(line, @"このスキルによる消費ACT及び消費MPは自身の《([^》]+)》スタック1つにつき-([0-9]+)される。",
                    "このスキルの消費ACTは自身の《$1》スタック1つにつき-$2される。（最低消費1）\nこのスキルの消費MPは自身の《$1》スタック1つにつき-$2される。（最低消費1）");
            }
            line = RegexReplace(line, @"さらに《([^》]+)》スタックが([0-9]+)以上の場合、このスキルの威力は([0-9.]+)倍される。", "自身の《$1》スタックが$2以上の場合、このスキルの威力は$3倍される。");
            line = RegexReplace(line, @"その後、自身の《([^》]+)》スタックは全て除去される。", "このスキルの攻撃後、自身の《$1》スタックは全て除去される。");
            line = RegexReplace(line, @"自身の〈([^〉]+)〉による行為判定の目標値は([+-][0-9]+)加算され、自身のターン開始時に([A-Z]+)-([0-9]+)及び([A-Z]+)-([0-9]+)。",
                "自身の〈$1〉による行為判定の目標値は$2加算される。\n自身のターン開始時、自身は$3を$4消費する。\n自身のターン開始時、自身は$5を$6消費する。");
            line = RegexReplace(line, @"《([^》]+)》状態のキャラクターのプレイヤーがその場にいる場合、《\1》状態の効果を無視してそのキャラクターを使用する事が出来る。ただし《\1》状態は解除できない。",
                "《$1》状態のキャラクターは、そのプレイヤーがその場にいる場合、この状態の効果を無視して使用できる。ただし、この状態は解除できない。");
            if (i + 1 < source.Length && M(line, @"対象へ対象の[A-Z]+最大値の[0-9]+%のダメージを与える。$").Success &&
                source[i + 1].Trim() == "この攻撃による威力及び被ダメージは、防御点、効果、状態によって増加も軽減もされない。")
            {
                line += "（この攻撃による威力及び被ダメージは、防御点、効果、状態によって増加も軽減もされない）";
                i++;
            }
            result.AddRange(line.Split('\n'));
        }
        return result.ToArray();
    }

    private static ConditionEntry ReadGodCondition(string text)
    {
        var m = M(text, @"^自身を除くキャラクターを([0-9]+)体選択$");
        if (m.Success) return Condition(ConditionType.SelectCharactersExceptSelf, m.Groups[1].Value);
        m = M(text, @"^特性《([^》]+)》を習得している(.+?)キャラクターを([0-9]+)体選択$");
        if (m.Success) return Condition(ConditionType.SelectRaceWithTrait, m.Groups[2].Value, m.Groups[1].Value, m.Groups[3].Value);
        return null;
    }
    private static string GodConditionText(ConditionEntry item)
    {
        if (item == null) return null;
        if (item.Type == ConditionType.SelectCharactersExceptSelf)
            return "自身を除くキャラクターを" + Number(Args(item.Parameters, 1, "SelectCharactersExceptSelf")[0], 1, "人数") + "体選択";
        if (item.Type == ConditionType.SelectRaceWithTrait)
        {
            var p = Args(item.Parameters, 3, "SelectRaceWithTrait"); Number(p[2], 1, "人数");
            return "特性《" + p[1] + "》を習得している" + p[0] + "キャラクターを" + p[2] + "体選択";
        }
        return null;
    }
    private static bool ReadGodSkill(SkillBody skill, EffectType type, string text)
    {
        if (type == EffectType.Passive && text.TrimEnd('。') == "無し") return true;
        var m = M(text, @"^自身は(.+?)の威力で攻撃する。?$");
        if (m.Success)
        {
            Require(type == EffectType.Active && skill.DeclarationConditions.And.Any(x => x.Type == ConditionType.SelectCharacters && x.Parameters.SequenceEqual(new[] { "1", "Exact", "Default" })));
            ExtendedEffect(skill, type, Content(EffectContentType.SkillAttack, "Target", m.Groups[1].Value)); return true;
        }
        m = M(text, @"^戦闘中([0-9]+)度だけ、(.+?)及び(.+?)を([0-9]+)消費する事で、自身に《([^》]+)》状態を付与しても良い。?$");
        if (m.Success)
        {
            Require(SpiritSpike(type));
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.OptionalSpikeState, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value, m.Groups[1].Value)); return true;
        }
        m = M(text, @"^自身が行為判定を行う際、達成値が([+-][0-9]+)される。?$");
        if (m.Success)
        { Require(type == EffectType.Passive); AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"))), Change(OverrideContentType.AddActionResult, m.Groups[1].Value)); return true; }
        m = M(text, @"^((?:〈[^〉]+〉)(?:及び〈[^〉]+〉)*)の達成値が([+-][0-9]+)される。?$");
        if (m.Success)
        {
            Require(type == EffectType.Passive);
            var triggers = AllMatches(m.Groups[1].Value, @"〈([^〉]+)〉").Cast<Match>().Select(x => Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, x.Groups[1].Value))).ToList();
            AddOverride(skill.Overrides, type, triggers, Change(OverrideContentType.AddActionResult, m.Groups[2].Value)); return true;
        }
        return false;
    }
    private static bool ReadGodState(StateDefinition state, string text)
    {
        var m = M(text, @"^あらゆる行為判定の達成値が([+-][0-9]+)上昇する。?$");
        if (!m.Success) return false;
        AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"))), Change(OverrideContentType.AddActionResult, m.Groups[1].Value)); return true;
    }
    private static string GodOverrideText(OverrideDefinition effect, OverrideContent content)
    {
        if (content == null || effect == null) return null;
        if (content.Type == OverrideContentType.OptionalSpikeState)
        {
            var p = Args(content.Parameters, 5, "OptionalSpikeState"); Require(SpiritSpike(effect.Type) && effect.Triggers.Count == 0 && p[0] != p[1]); Number(p[2], 1, "各消費量"); Number(p[4], 1, "戦闘回数");
            return "戦闘中" + p[4] + "度だけ、" + p[0] + "及び" + p[1] + "を" + p[2] + "消費する事で、自身に《" + p[3] + "》状態を付与しても良い。";
        }
        if (content.Type != OverrideContentType.AddActionResult || effect.Type != EffectType.Passive) return null;
        string amount = Signed(Args(content.Parameters, 1, "AddActionResult")[0]);
        var t = effect.Triggers;
        if (Matches(t, Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"))))
            return "自身が行為判定を行う際、達成値が" + amount + "される。";
        if (t == null || t.Count == 0 || t.Any(x => !ValidTrigger(x) || x.Conditions.And.Count != 2 || x.Conditions.And[1].Type != ConditionType.ActionCategory)) return null;
        var categories = t.Select(x => Args(x.Conditions.And[1].Parameters, 1, "ActionCategory")[0]).ToArray();
        if (!Matches(t, categories.Select(x => Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, x))).ToArray())) return null;
        return CategoryAlternatives(categories, "及び") + "の達成値が" + amount + "される。";
    }
}
