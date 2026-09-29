using System;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static bool ReadSpiritSkill(SkillBody skill, EffectType type, string s)
    {
        if (ReadSpiritModifier(skill, type, s)) return true;
        Match m;
        m = M(s, @"^自身は([A-Z]+が[0-9]+以下になった時、.*)$");
        if (m.Success && s.Contains("状態になる")) { ReadSkillLine(skill, type, m.Groups[1].Value); return true; }
        if (s.TrimEnd('。') == "自身は戦闘を離脱する")
        { ExtendedEffect(skill, type, Content(EffectContentType.LeaveBattle, "Self")); return true; }
        m = M(s, @"^自身は([A-Z]+)が([0-9]+)以下になった時、(.+?)ロールを行う必要がない。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.PreventRollAtResource, "Self", m.Groups[1].Value, "AtMost", m.Groups[2].Value, m.Groups[3].Value)); return true; }
        m = M(s, @"^自身はあらゆる〈([^〉]+)〉からの効果を受けない。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.CategoryImmunity, "Self", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身は((?:〈[^〉]+〉)(?:及び〈[^〉]+〉)*)による効果を受ける事が出来ない。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.CategoryImmunity, new[] { "Self" }.Concat(AllMatches(m.Groups[1].Value, @"〈([^〉]+)〉").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray())); return true; }
        m = M(s, @"^自身は〈([^〉]+)〉を対象にすることはできない。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.ProhibitCategoryTarget, "Self", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身の([A-Z]+)の消費は全て([A-Z]+)の消費に置き換えられる。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.ConvertResourceCost, "Self", m.Groups[1].Value, m.Groups[2].Value, "All")); return true; }
        m = M(s, @"^クエストによって獲得する(.+?)が\+([0-9]+)される。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.AddQuestReward, "Self", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^対象スキルを([0-9]+)ターンの間《([^》]+)》状態にする。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.ApplySelectedSkillState, "SelectedSkill", m.Groups[2].Value, m.Groups[1].Value)); return true; }
        m = M(s, @"^対象に(.+?)の([A-Z]+)ダメージを与える。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.ResourceDamage, "Target", m.Groups[2].Value, m.Groups[1].Value.Replace("×", "*"))); return true; }
        m = M(s, @"^与えた([A-Z]+)ダメージの値の([A-Z]+)と、([0-9.]+)倍の値の([A-Z]+)を回復する。?$");
        if (m.Success)
        {
            ExtendedEffect(skill, type, Content(EffectContentType.RestoreFromResourceDamage, "Self", m.Groups[1].Value, m.Groups[2].Value, "1", "ThisResolution"));
            ExtendedEffect(skill, type, Content(EffectContentType.RestoreFromResourceDamage, "Self", m.Groups[1].Value, m.Groups[4].Value, m.Groups[3].Value, "ThisResolution")); return true;
        }
        m = M(s, @"^与えた([A-Z]+)ダメージの([0-9.]+)倍の値の([A-Z]+)を回復する。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.RestoreFromResourceDamage, "Self", m.Groups[1].Value, m.Groups[3].Value, m.Groups[2].Value, "ThisResolution")); return true; }
        m = M(s, @"^《([^》]+)》状態のキャラクター全てに自身のターン開始時《([^》]+)》スタックを([0-9]+)付与する。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.TurnStartStateStacks, "Self", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, "AllCharacters")); return true; }
        m = M(s, @"^対象に《([^》]+)》スタックを([0-9]+)付与する。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.GainStack, "Target", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^対象の([A-Z]+)を(.+?)の効果量で回復する。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.RestoreResource, "Target", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^自身の《([^》]+)》状態を解除する。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.RemoveState, "Self", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身の([A-Z]+最大値)及び([A-Z]+最大値)は×([0-9.]+)倍される。?$");
        if (m.Success)
        { ReadSkillLine(skill, type, m.Groups[1].Value + "が×" + m.Groups[3].Value + "倍される。"); ReadSkillLine(skill, type, m.Groups[2].Value + "が×" + m.Groups[3].Value + "倍される。"); return true; }
        return false;
    }

    private static string SpiritContentText(EffectContent content)
    {
        if (content == null) return null;
        string[] p;
        switch (content.Type)
        {
            case EffectContentType.RemoveState:
                if (content.Parameters.Count != 2 || content.Parameters[0] != "Self") return null;
                return "自身の《" + content.Parameters[1] + "》状態を解除する。";
            case EffectContentType.LeaveBattle:
                p = Args(content.Parameters, 1, "LeaveBattle"); Require(p[0] == "Self"); return "自身は戦闘を離脱する。";
            case EffectContentType.PreventRollAtResource:
                p = Args(content.Parameters, 5, "PreventRollAtResource"); Require(p[0] == "Self" && p[2] == "AtMost"); Number(p[3], 0, "閾値");
                return "自身は" + p[1] + "が" + p[3] + "以下になった時、" + p[4] + "ロールを行う必要がない。";
            case EffectContentType.CategoryImmunity:
                p = VariableArgs(content.Parameters, 2, "CategoryImmunity"); Require(p[0] == "Self");
                return "自身は" + string.Join("及び", p.Skip(1).Select(x => "〈" + x + "〉")) + "による効果を受ける事が出来ない。";
            case EffectContentType.ProhibitCategoryTarget:
                p = Args(content.Parameters, 2, "ProhibitCategoryTarget"); Require(p[0] == "Self"); return "自身は〈" + p[1] + "〉を対象にすることはできない。";
            case EffectContentType.ConvertResourceCost:
                p = Args(content.Parameters, 4, "ConvertResourceCost"); Require(p[0] == "Self" && p[3] == "All" && p[1] != p[2]);
                return "自身の" + p[1] + "の消費は全て" + p[2] + "の消費に置き換えられる。";
            case EffectContentType.AddQuestReward:
                p = Args(content.Parameters, 3, "AddQuestReward"); Require(p[0] == "Self"); Number(p[2], 1, "報酬加算"); return "クエストによって獲得する" + p[1] + "が+" + p[2] + "される。";
            case EffectContentType.ApplySelectedSkillState:
                p = Args(content.Parameters, 3, "ApplySelectedSkillState"); Require(p[0] == "SelectedSkill"); Number(p[2], 1, "持続ターン"); return "対象スキルを" + p[2] + "ターンの間《" + p[1] + "》状態にする。";
            case EffectContentType.ResourceDamage:
                p = Args(content.Parameters, 3, "ResourceDamage"); Require(p[0] == "Target"); return "対象に" + p[2] + "の" + p[1] + "ダメージを与える。";
            case EffectContentType.RestoreFromResourceDamage:
                p = Args(content.Parameters, 5, "RestoreFromResourceDamage"); Require(p[0] == "Self" && p[4] == "ThisResolution"); PositiveHumanFactor(p[3]);
                return "与えた" + p[1] + "ダメージの" + p[3] + "倍の値の" + p[2] + "を回復する。";
            case EffectContentType.TurnStartStateStacks:
                p = Args(content.Parameters, 5, "TurnStartStateStacks"); Require(p[0] == "Self" && p[4] == "AllCharacters"); Number(p[3], 1, "スタック数");
                return "《" + p[1] + "》状態のキャラクター全てに自身のターン開始時《" + p[2] + "》スタックを" + p[3] + "付与する。";
            default: return null;
        }
    }
}
