using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static ConditionEntry ReadHumanCondition(string text)
    {
        Match m = M(text, @"^戦闘中([0-9]+)(?:度だけ|回まで)$");
        if (m.Success) return Condition(ConditionType.ActivationLimit, "Battle", m.Groups[1].Value);
        if (text == "発動ロールに通常失敗した時に") return Condition(ConditionType.RollResult, "Activation", "NormalFailure");
        m = M(text, @"^([A-Z]+)が(.+?)以下になった時に$");
        if (m.Success) return Condition(ConditionType.ResourceValue, "Self", m.Groups[1].Value, "AtMost", m.Groups[2].Value);
        m = M(text, @"^自身に〈([^〉]+)〉状態が付与されている時[、,]?$" );
        if (m.Success) return Condition(ConditionType.OwnStateCategories, new[] { "Any" }.Concat(Split(m.Groups[1].Value, ",")).ToArray());
        return null;
    }

    private static string HumanConditionText(ConditionEntry condition)
    {
        if (condition == null) return null;
        string[] p;
        if (condition.Type == ConditionType.ActivationLimit && condition.Parameters != null && condition.Parameters.Count == 2 && condition.Parameters[0] == "Battle")
            return "戦闘中" + Number(condition.Parameters[1], 1, "発動回数") + "回まで";
        if (condition.Type == ConditionType.RollResult)
        {
            p = Args(condition.Parameters, 2, "RollResult"); Require(p.SequenceEqual(new[] { "Activation", "NormalFailure" }));
            return "発動ロールに通常失敗した時に";
        }
        if (condition.Type == ConditionType.ResourceValue)
        {
            p = Args(condition.Parameters, 4, "ResourceValue"); Require(p[0] == "Self" && p[2] == "AtMost");
            return p[1] + "が" + p[3] + "以下になった時に";
        }
        if (condition.Type == ConditionType.OwnStateCategories)
        {
            p = VariableArgs(condition.Parameters, 2, "OwnStateCategories"); Require(p[0] == "Any");
            return "自身に〈" + string.Join(", ", p.Skip(1)) + "〉状態が付与されている時";
        }
        return null;
    }

    private static bool ReadHumanSkill(SkillBody skill, EffectType type, string body)
    {
        string s = body.Trim();
        bool spike = type == EffectType.SecondSpike || type == EffectType.ThirdSpike;
        if (spike && s.TrimEnd('。') == "無し") return true;
        if (spike && ReadHumanSpike(skill, type, s)) return true;
        if (spike) return false;
        Match m = M(s, @"^能力値ボーナスを\[?([^\[\]]+?B)\]?に置きかえて発動ロールをやり直す。?$");
        if (m.Success && type == EffectType.Counter)
        {
            ExtendedEffect(skill, type, Content(EffectContentType.RerollActivation, "Self", m.Groups[1].Value, "TriggeringRoll")); return true;
        }
        m = M(s, @"^HP([0-9]+)の状態で耐える。?$");
        if (m.Success && type == EffectType.Counter)
        {
            ExtendedEffect(skill, type, Content(EffectContentType.SetResourceValue, "Self", "HP", m.Groups[1].Value)); return true;
        }
        m = M(s, @"^この時、(.+?)ロールは行わなくてもよい。?$");
        if (m.Success && type == EffectType.Counter)
        {
            ExtendedEffect(skill, type, Content(EffectContentType.SkipRoll, "Self", m.Groups[1].Value, "Optional", "ThisResolution")); return true;
        }
        m = M(s, @"^自身に付与されている〈([^〉]+)〉状態を全て解除する。?$");
        if (m.Success && type == EffectType.Active)
        {
            ExtendedEffect(skill, type, Content(EffectContentType.RemoveState, new[] { "Self", "Categories", "All" }.Concat(Split(m.Groups[1].Value, ",")).ToArray())); return true;
        }
        if (type != EffectType.Passive && type != EffectType.Active) return false;

        m = M(s, @"^スキルによって〈([^〉]+)〉で武器攻撃を行う時、発動ロール達成値が×(.+?)倍される。?$");
        if (m.Success) { AddHumanRule(skill, type, m.Groups[2].Value, "WeaponActivation", m.Groups[1].Value); return true; }
        m = M(s, @"^((?:\[[^\]]+\])(?:(?:および|及び)\[[^\]]+\])*)による行為判定を行う際、達成値が×(.+?)倍される。?$");
        if (m.Success)
        {
            AddHumanRule(skill, type, m.Groups[2].Value, new[] { "ActionStats" }.Concat(AllMatches(m.Groups[1].Value, @"\[([^\]]+)\]").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray()); return true;
        }
        m = M(s, @"^環境による行為判定を行う際、達成値が([+-].+?)される。?$");
        if (m.Success) { AddHumanRule(skill, type, m.Groups[1].Value, "EnvironmentCheck"); return true; }
        m = M(s, @"^〈([^〉]+)〉(?:上記の装備|の装備)を制作するとき、目標値が([+-].+?)される。?$");
        if (m.Success) { AddHumanRule(skill, type, m.Groups[2].Value, new[] { "CraftCategories" }.Concat(Split(m.Groups[1].Value, ",")).ToArray()); return true; }
        m = M(s, @"^このターン中、〈([^〉]+)〉の武器威力を参照する〈([^〉]+)〉の発動ロールの達成値が([+-].+?)される。?$");
        if (m.Success) { AddHumanRule(skill, type, m.Groups[3].Value, "TimedWeaponActivation", m.Groups[1].Value, m.Groups[2].Value); return true; }
        m = M(s, @"^スキル習得に必要な消費決意が([+-].+?)される。?$");
        if (m.Success) { AddHumanRule(skill, type, m.Groups[1].Value, "AcquisitionCost", "Add", "AllSkills", "All"); return true; }
        m = M(s, @"^一日の採取回数上限が([+-].+?)追加される。?$");
        if (m.Success) { AddHumanRule(skill, type, m.Groups[1].Value, "DailyGathering"); return true; }
        m = M(s, @"^戦闘開始から([0-9]+)ターンの間、\[([^\]]+)\]による発動ロールの達成値が([+-][0-9]+)(?:される。?|および自身の被ダメージは×(.+?)倍され(?:ます|る)。?)$");
        if (m.Success)
        {
            AddHumanRule(skill, type, m.Groups[3].Value, "EarlyBattleActivation", m.Groups[1].Value, m.Groups[2].Value);
            if (m.Groups[4].Success) AddHumanRule(skill, type, m.Groups[4].Value, "EarlyBattleDamage", m.Groups[1].Value);
            return true;
        }
        m = M(s, @"^戦闘開始から([0-9]+)ターンの間、自身の被ダメージは×(.+?)倍される。?$");
        if (m.Success) { AddHumanRule(skill, type, m.Groups[2].Value, "EarlyBattleDamage", m.Groups[1].Value); return true; }
        m = M(s, @"^([A-Z]+)最大値が×(.+?)倍される。?$");
        if (m.Success) { AddHumanRule(skill, type, m.Groups[2].Value, "ResourceMaximum", m.Groups[1].Value); return true; }
        m = M(s, @"^NPCへの〈([^〉]+)〉を除くアイテムの売値が×(.+?)倍される。?$");
        if (m.Success) { AddHumanRule(skill, type, m.Groups[2].Value, "SalePrice", m.Groups[1].Value); return true; }
        m = M(s, @"^〈([^〉]+)〉による[、,]?\s*威力とリソースの回復量が×(.+?)倍される。?$");
        if (m.Success)
        {
            AddHumanRule(skill, type, m.Groups[2].Value, "CategoryPower", m.Groups[1].Value);
            AddHumanRule(skill, type, m.Groups[2].Value, "CategoryHealing", m.Groups[1].Value); return true;
        }
        m = M(s, @"^〈([^〉]+)〉によるリソースの回復量が×(.+?)倍される。?$");
        if (m.Success) { AddHumanRule(skill, type, m.Groups[2].Value, "CategoryHealing", m.Groups[1].Value); return true; }
        m = M(s, @"^〈([^〉]+)〉による(?:の)?行為判定(?:の)?達成値が([+-].+?)される。?$");
        if (m.Success) { AddHumanRule(skill, type, m.Groups[2].Value, "CategoryCheck", m.Groups[1].Value); return true; }
        m = M(s, @"^クラス《([^》]+)》(?:及び|および)〈([^〉]+)〉の習得決意が×(.+?)倍される。?$");
        if (m.Success)
        {
            AddHumanRule(skill, type, m.Groups[3].Value, "AcquisitionCost", "Multiply", "Class", m.Groups[1].Value);
            AddHumanRule(skill, type, m.Groups[3].Value, "AcquisitionCost", "Multiply", "SkillCategory", m.Groups[2].Value); return true;
        }
        m = M(s, @"^(?:クラス《([^》]+)》|〈([^〉]+)〉)の習得決意が×(.+?)倍される。?$");
        if (m.Success) { AddHumanRule(skill, type, m.Groups[3].Value, "AcquisitionCost", "Multiply", m.Groups[1].Success ? "Class" : "SkillCategory", m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value); return true; }
        if (type != EffectType.Passive) return false;
        m = M(s, @"^(?:〈([^〉]+)〉|([^〈〉]+?))を(一|[0-9]+)つまでしか選択する事が出来ない。?$");
        if (m.Success)
        {
            ExtendedEffect(skill, type, Content(EffectContentType.LimitAcquisition, "Self", "RaceSelection", m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value, m.Groups[3].Value == "一" ? "1" : m.Groups[3].Value)); return true;
        }
        m = M(s, @"^※(?:〈([^〉]+)〉|([^〈〉]+?))は選択不可能。?$");
        if (m.Success)
        {
            ExtendedEffect(skill, type, Content(EffectContentType.LimitAcquisition, "Self", "RaceSelection", m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value, "0")); return true;
        }
        m = M(s, @"^あなたの種族は〈([^〉]+)〉としても扱われる。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.RaceAlias, "Self", m.Groups[1].Value)); return true; }
        return false;
    }

    private static string HumanContentText(EffectContent content)
    {
        if (content == null) return null;
        string[] p;
        switch (content.Type)
        {
            case EffectContentType.RerollActivation:
                p = Args(content.Parameters, 3, "RerollActivation"); Require(p[0] == "Self" && p[2] == "TriggeringRoll");
                return "能力値ボーナスを" + p[1] + "に置きかえて発動ロールをやり直す。";
            case EffectContentType.SetResourceValue:
                p = Args(content.Parameters, 3, "SetResourceValue"); Require(p[0] == "Self" && p[1] == "HP"); Number(p[2], 1, "耐えるHP");
                return "HP" + p[2] + "の状態で耐える。";
            case EffectContentType.SkipRoll:
                p = Args(content.Parameters, 4, "SkipRoll"); Require(p[0] == "Self" && p[2] == "Optional" && p[3] == "ThisResolution");
                return "この時、" + p[1] + "ロールは行わなくてもよい。";
            case EffectContentType.RemoveState:
                if (content.Parameters == null || content.Parameters.Count < 4 || content.Parameters[1] != "Categories") return null;
                p = VariableArgs(content.Parameters, 4, "RemoveState/Categories"); Require(p[0] == "Self" && p[2] == "All");
                return "自身に付与されている〈" + string.Join(", ", p.Skip(3)) + "〉状態を全て解除する。";
            case EffectContentType.LimitAcquisition:
                p = Args(content.Parameters, 4, "LimitAcquisition"); Require(p[0] == "Self" && p[1] == "RaceSelection");
                return Number(p[3], 0, "選択上限") == 0 ? "※〈" + p[2] + "〉は選択不可能。" : "〈" + p[2] + "〉を" + p[3] + "つまでしか選択する事が出来ない。";
            case EffectContentType.RaceAlias:
                p = Args(content.Parameters, 2, "RaceAlias"); Require(p[0] == "Self");
                return "あなたの種族は〈" + p[1] + "〉としても扱われる。";
            default: return null;
        }
    }
}
