using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static string[] PrepareSpiritLines(string[] source)
    {
        var lines = source.ToList();
        int separator = lines.FindIndex(x => M(x.Trim(), @"^―{8,}$").Success);
        if (separator < 0) separator = lines.Count;
        // The supplied Oni block repeats its identical introduction before the choices.
        int first = lines.FindIndex(x => x.Trim() == "《鬼神楽》");
        if (first >= 0)
        {
            int repeated = lines.FindIndex(first + 1, x => x.Trim() == "《鬼神楽》");
            if (repeated >= 0 && repeated < separator)
            {
                string[] intro = lines.Skip(first).Take(repeated - first).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
                int count = intro.Length;
                if (lines.Skip(repeated).Take(count).Select(x => x.Trim()).SequenceEqual(intro))
                { lines.RemoveRange(repeated, count); separator -= count; }
            }
        }
        // Two independently rolled uses must not overwrite the same Roll field.
        if (lines.Any(x => x.Trim() == "《壁抜け》") && !lines.Any(x => x.TrimStart().StartsWith("●", StringComparison.Ordinal)))
        {
            int costs = lines.FindIndex(x => x.StartsWith("【消費リソース】", StringComparison.Ordinal));
            int second = lines.FindIndex(x => x.StartsWith("【宣言条件】任意の対象", StringComparison.Ordinal));
            if (costs >= 0 && second > costs && second < separator)
            {
                string cost = lines[costs];
                lines.InsertRange(second, new[] { "●ロールプレイ", cost });
                lines.InsertRange(costs, new[] { "【説明】" + Description, "●戦闘" });
                separator += 4;
            }
        }
        for (int i = 0; i < separator; i++)
        {
            lines[i] = lines[i].Replace("》態になる", "》状態になる").Replace("SANダメ―ジ", "SANダメージ");
            // Unwrapped, canonical lines and flavor are deliberately left intact.
            if (i + 1 < separator && (lines[i].TrimEnd().EndsWith("発動ロールを行う際、", StringComparison.Ordinal) ||
                lines[i].TrimEnd().EndsWith("を発動する際、", StringComparison.Ordinal) ||
                (lines[i].Contains("かつ自身と対象が") && lines[i].TrimEnd().EndsWith("状態の時、", StringComparison.Ordinal))))
            { lines[i] += lines[i + 1].Trim(); lines.RemoveAt(i + 1); separator--; i--; continue; }
            // This recovery belongs to the skill, not to the preceding state definition.
            if (lines[i].Trim() == "さらにHP150回復") lines[i] = "【アクティブ効果】HP150回復";
        }
        return lines.ToArray();
    }

    private static ConditionEntry ReadSpiritCondition(string text)
    {
        text = text.TrimEnd('。');
        if (text == "エンカウントフェーズ") return Condition(ConditionType.EncounterPhase);
        Match m = M(text, @"^キャラクター([0-9]+)体のアクティブ効果を持つスキルを([0-9]+)つ選択$");
        if (m.Success) return Condition(ConditionType.SelectCharacterSkill, m.Groups[1].Value, "Active", m.Groups[2].Value);
        m = M(text, @"^任意の対象を([0-9]+)体指定(?:して)?$");
        if (m.Success) return Condition(ConditionType.SelectAnyTarget, m.Groups[1].Value);
        m = M(text, @"^((?:《[^》]+》)(?:及び《[^》]+》)*)を除くキャラクターを([0-9]+)体選択$");
        if (m.Success) return Condition(ConditionType.ExcludeTargetRaces, new[] { m.Groups[2].Value }.Concat(AllMatches(m.Groups[1].Value, @"《([^》]+)》").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray());
        m = M(text, @"^《([^》]+)》スタックが([0-9]+)以上のキャラクターを([0-9]+)体選択$");
        if (m.Success) return Condition(ConditionType.TargetStackMinimum, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
        return null;
    }

    private static string SpiritConditionText(ConditionEntry item)
    {
        if (item == null) return null;
        string[] p;
        switch (item.Type)
        {
            case ConditionType.EncounterPhase: Args(item.Parameters, 0, "EncounterPhase"); return "エンカウントフェーズ";
            case ConditionType.SelectCharacterSkill:
                p = Args(item.Parameters, 3, "SelectCharacterSkill"); Number(p[0], 1, "人数"); Number(p[2], 1, "スキル数"); Require(p[1] == "Active");
                return "キャラクター" + p[0] + "体のアクティブ効果を持つスキルを" + p[2] + "つ選択";
            case ConditionType.SelectAnyTarget:
                p = Args(item.Parameters, 1, "SelectAnyTarget"); Number(p[0], 1, "対象数"); return "任意の対象を" + p[0] + "体指定して";
            case ConditionType.ExcludeTargetRaces:
                p = VariableArgs(item.Parameters, 2, "ExcludeTargetRaces"); Number(p[0], 1, "人数");
                return string.Join("及び", p.Skip(1).Select(x => "《" + x + "》")) + "を除くキャラクターを" + p[0] + "体選択";
            case ConditionType.TargetStackMinimum:
                p = Args(item.Parameters, 3, "TargetStackMinimum"); Number(p[1], 1, "スタック下限"); Number(p[2], 1, "人数");
                return "《" + p[0] + "》スタックが" + p[1] + "以上のキャラクターを" + p[2] + "体選択";
            default: return null;
        }
    }
}
