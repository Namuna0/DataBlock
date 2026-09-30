using System;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static string[] PrepareBeastRaceLines(string[] lines)
    {
        bool flavor = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (M(lines[i].Trim(), @"^―{8,}$").Success) flavor = true;
            if (flavor) continue;
            lines[i] = lines[i].Replace("〈種族学スキル", "〈種族スキル");
            // Remove only the dangling multiplication operator in a power replacement.
            lines[i] = RegexReplace(lines[i], @"(このアクティブ効果による威力は[^\r\n]+)\*に変化する", "$1に変化する");
            if (i + 1 < lines.Length && lines[i].Contains("によって相手を《") && lines[i].TrimEnd().EndsWith("状態にした時、", StringComparison.Ordinal))
            { lines[i] += lines[i + 1].Trim(); lines[i + 1] = ""; }
        }
        return lines;
    }

    private static bool ReadBeastRaceConditions(ConditionSet set, string text)
    {
        Match m = M(text.Trim(), @"^戦闘中、自身が《([^》]+)》状態の時に一度だけ、あらゆるキャラクターの行為判定に対して(?:（〈([^〉]+)〉を受けた場合回数リセット）)?宣言可能。?$");
        if (m.Success)
        {
            set.And.Add(Condition(ConditionType.IncapacitatedCheckReaction, m.Groups[1].Value, "Battle", "1", "AnyCharacterAction", m.Groups[2].Success ? m.Groups[2].Value : "Unspecified")); return true;
        }
        m = M(text.Trim(), @"^〈([^〉]+)〉を受けた場合回数はリセットされる。?$");
        if (m.Success)
        {
            var reaction = set.And.SingleOrDefault(x => x.Type == ConditionType.IncapacitatedCheckReaction);
            Require(reaction != null && reaction.Parameters[4] == "Unspecified"); reaction.Parameters[4] = m.Groups[1].Value; return true;
        }
        m = M(text.Trim(), @"^自身が〈([^〉]+)〉を受けた時、その対象へ発動。?$");
        if (m.Success) { set.And.Add(Condition(ConditionType.ReactionTarget, m.Groups[1].Value, "Self", "Source")); return true; }
        if (text.Trim() == "所持している武器を1つ選択して宣言可能。または素手を選択。")
        { set.And.Add(Condition(ConditionType.SelectCarriedWeapon, "1", "AllowUnarmed")); return true; }
        return false;
    }

    private static ConditionEntry ReadBeastRaceCondition(string text)
    {
        text = text.TrimEnd('。');
        if (text == "種族選択時") return Condition(ConditionType.AtRaceSelection);
        Match m = M(text, @"^自身が《([^》]+)》状態ではない時$");
        if (m.Success) return Condition(ConditionType.WithoutOwnState, m.Groups[1].Value);
        m = M(text, @"^1日([0-9]+)度のみ$");
        if (m.Success) return Condition(ConditionType.ActivationLimit, "Day", m.Groups[1].Value);
        m = M(text, @"^([A-Z]+)が([0-9]+)以下になる〈([^〉]+)〉を受けた時(?:に)?$");
        if (m.Success) return Condition(ConditionType.LethalIncomingAction, m.Groups[1].Value, "AtMost", m.Groups[2].Value, m.Groups[3].Value);
        m = M(text, @"^消費([A-Z]+)が([0-9]+)以下かつ接近状態を条件としない自身の装備スキルを([0-9]+)つ選択$");
        if (m.Success) return Condition(ConditionType.SelectEquipmentSkill, m.Groups[3].Value, m.Groups[1].Value, "AtMost", m.Groups[2].Value, "NoMeleeRequirement");
        m = M(text, @"^自身へ〈([^〉]+)〉を発動した対象に対して$");
        if (m.Success) return Condition(ConditionType.ActionTowardsSelf, m.Groups[1].Value, "Source");
        return null;
    }

    private static string BeastRaceConditionText(ConditionEntry condition)
    {
        if (condition == null) return null;
        string[] p;
        switch (condition.Type)
        {
            case ConditionType.WithoutOwnState:
                return "自身が《" + Args(condition.Parameters, 1, "WithoutOwnState")[0] + "》状態ではない時";
            case ConditionType.AtRaceSelection: Args(condition.Parameters, 0, "AtRaceSelection"); return "種族選択時";
            case ConditionType.IncapacitatedCheckReaction:
                p = Args(condition.Parameters, 5, "IncapacitatedCheckReaction"); Require(p[1] == "Battle" && p[2] == "1" && p[3] == "AnyCharacterAction" && p[4] != "Unspecified");
                return "戦闘中、自身が《" + p[0] + "》状態の時に一度だけ、あらゆるキャラクターの行為判定に対して（〈" + p[4] + "〉を受けた場合回数リセット）";
            case ConditionType.LethalIncomingAction:
                p = Args(condition.Parameters, 4, "LethalIncomingAction"); Require(p[1] == "AtMost"); Number(p[2], 0, "閾値");
                return p[0] + "が" + p[2] + "以下になる〈" + p[3] + "〉を受けた時に";
            case ConditionType.SelectEquipmentSkill:
                p = Args(condition.Parameters, 5, "SelectEquipmentSkill"); Number(p[0], 1, "選択数"); Number(p[3], 0, "消費上限"); Require(p[2] == "AtMost" && p[4] == "NoMeleeRequirement");
                return "消費" + p[1] + "が" + p[3] + "以下かつ接近状態を条件としない自身の装備スキルを" + p[0] + "つ選択";
            case ConditionType.ActionTowardsSelf:
                p = Args(condition.Parameters, 2, "ActionTowardsSelf"); Require(p[1] == "Source"); return "自身へ〈" + p[0] + "〉を発動した対象に対して";
            case ConditionType.SelectCarriedWeapon:
                p = Args(condition.Parameters, 2, "SelectCarriedWeapon"); Require(p[0] == "1" && p[1] == "AllowUnarmed"); return "所持している武器を1つ選択して宣言可能。または素手を選択。";
            default: return null;
        }
    }
}
