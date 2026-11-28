using static CommonEffectTextCodec;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class EquipmentTextConverter
{
    private static EquipmentEffectGroup ReadEquipmentEffectParagraph(EquipmentDefinition owner, string[] lines,
        ref int index, int end, string line, string modeName = null)
    {
        string text = Unbullet(JoinEffectLines(lines, ref index, end, line));
        if (modeName != null)
            text = text.Replace(modeName + "を選択してスキルを発動する場合", "この武器の選択を条件にスキルを発動する場合");
        string name = "";
        Match named = M(text, @"^《([^《》]+)》[：:]\s*(?:《\1》[：:]\s*)?(.+)$");
        if (named.Success)
        {
            name = Need(named.Groups[1].Value, "装備効果名");
            text = named.Groups[2].Value.Trim();
        }

        if (!CommonEffectRollTable.TryRead(text, lines, ref index, end, definition => TryReadEquipmentState(owner, definition), out var contents))
        {
            if (!CommonEffectTextCodec.TryRead(text, out contents))
                throw new InvalidOperationException("未対応の装備効果です：" + text);
            if (index < end && TryReadContinuation(contents, SyntaxLine(lines[index + 1]))) index++;
        }
        return new EquipmentEffectGroup { Name = name, Effects = contents };
    }

    private static string EquipmentEffectText(EffectContent effect)
    {
        return CommonEffectTextCodec.Write(effect) ??
            throw new InvalidOperationException("未対応の装備効果です：" + effect.Type);
    }

    private static void ReadEquipmentGradeAdjustment(EquipmentDefinition equipment, string source)
    {
        Match match = M(source.Trim(), @"^-\s*☆([1-9][0-9]*)\s*[：:]\s*(.+)$");
        if (!match.Success)
            throw new InvalidOperationException("グレード補正は「- ☆1：耐久最大値-4, 防御点-2」の形式です。");

        var adjustment = new EquipmentGradeAdjustmentDefinition
        {
            Grade = Number(match.Groups[1].Value, 1, "補正対象グレード")
        };
        foreach (string part in Split(match.Groups[2].Value.Replace("、", ","), ","))
        {
            Match replacement = M(part, @"^この装備効果による(追加ダメージ|防御点無視)は([0-9]+)に変化する[。.]?$");
            if (replacement.Success)
            {
                adjustment.Replacements.Add(new EquipmentGradeRuleReplacement
                {
                    Rule = replacement.Groups[1].Value == "追加ダメージ" ? "ExtraDamageToTargetCategories" : "CategoryIgnoreDefense",
                    Amount = Number(replacement.Groups[2].Value, 0, "装備効果の置換値")
                });
                continue;
            }
            EffectContent modifier;
            if (part.StartsWith("【装備効果】", StringComparison.Ordinal) ||
                !TryReadStatModifier(part, out modifier))
            {
                string rule = part.StartsWith("【装備効果】", StringComparison.Ordinal) ? part.Substring("【装備効果】".Length) : part;
                if (!TryReadRule(rule, EffectContentType.RuleModifier, out modifier))
                    throw new InvalidOperationException("未対応のグレード補正です：" + part);
                adjustment.Modifiers.Add(modifier);
                continue;
            }

            if (modifier.Parameters[1] == "耐久最大値")
            {
                if (adjustment.MaxDurabilityDelta != 0)
                    throw new InvalidOperationException("耐久最大値のグレード補正が重複しています。");
                adjustment.MaxDurabilityDelta = SignedInteger(
                    modifier.Parameters[3], "耐久最大値のグレード補正");
            }
            else if (modifier.Parameters[1] == "武器威力")
            {
                if (adjustment.WeaponPowerDelta != 0) throw new InvalidOperationException("武器威力のグレード補正が重複しています。");
                adjustment.WeaponPowerDelta = SignedInteger(modifier.Parameters[3], "武器威力のグレード補正");
            }
            else
            {
                adjustment.Modifiers.Add(modifier);
            }
        }
        if (adjustment.MaxDurabilityDelta == 0 && adjustment.WeaponPowerDelta == 0 && adjustment.Modifiers.Count == 0 && adjustment.Replacements.Count == 0)
            throw new InvalidOperationException("グレード補正が空です。");
        equipment.GradeAdjustments.Add(adjustment);
    }

    private static bool TryReadEquipmentState(EquipmentDefinition equipment, string line)
    {
        int index = 0;
        return TryReadEquipmentState(equipment, new[] { line }, ref index, 0);
    }

    private static bool TryReadEquipmentState(EquipmentDefinition equipment, string[] lines, ref int index, int end)
    {
        if (!SkillTextConverter.TryStateHeader(SyntaxLine(lines[index]), out var state, out string body, allowShorthand: true)) return false;
        if (body.Length > 0 && !ReadBody(body, ref index)) return false;
        int next = index + 1;
        while (next <= end)
        {
            string line = SyntaxLine(lines[next]);
            if (!(line.StartsWith("・") || line.StartsWith("*") || line.StartsWith("さらに") ||
                line.StartsWith("この効果を解除") || line.StartsWith("スタックが偶数") ||
                line.StartsWith("●この状態") || line.StartsWith("適用後スタック"))) break;
            if (SkillTextConverter.TryStateHeader(line, out _, out _, allowShorthand: true) || !ReadBody(Unbullet(line), ref next)) break;
            index = next++;
        }
        if (state.Effects.Count + state.Overrides.Count == 0) return false;
        SkillTextConverter.NormalizeStateTrigger(state);
        equipment.States.Add(state);
        return true;

        bool ReadBody(string text, ref int row)
        {
            if (!CommonEffectRollTable.TryRead(text, lines, ref row, end, definition => TryReadEquipmentState(equipment, definition), out var effects))
                return SkillTextConverter.ReadStateText(state, text);
            state.Effects.Add(new EffectDefinition { Type = EffectType.Passive, Contents = effects });
            return true;
        }
    }

    private static string EquipmentEffectGroupText(EquipmentEffectGroup group)
    {
        if (group == null || group.Effects == null || group.Effects.Count == 0)
            throw new InvalidOperationException("装備効果グループが空です。");
        string prefix = string.IsNullOrEmpty(group.Name) ? "" : "《" + Need(group.Name, "装備効果名") + "》：";
        if (group.Effects[0]?.Type == EffectContentType.RollTable) return prefix + CommonEffectRollTable.Write(group.Effects);
        string shared = WriteGroup(group.Effects);
        if (shared != null) return prefix + shared;
        if (!string.IsNullOrEmpty(group.Name) && group.Effects.Count > 1 &&
            group.Effects.Any(e => e.Type != EffectContentType.ModifyStat))
            return prefix + "\n" + string.Join("\n", group.Effects.Select(EquipmentEffectText)) + "\n";
        return prefix + string.Join(", ", group.Effects.Select(EquipmentEffectText));
    }

    private static string EquipmentGradeAdjustmentText(EquipmentGradeAdjustmentDefinition adjustment)
    {
        if (adjustment == null || adjustment.Modifiers == null)
            throw new InvalidOperationException("グレード補正がnullです。");

        var parts = new List<string>();
        if (adjustment.MaxDurabilityDelta != 0)
            parts.Add("耐久最大値" + SignedIntegerText(adjustment.MaxDurabilityDelta));
        if (adjustment.WeaponPowerDelta != 0)
            parts.Add("武器威力" + SignedIntegerText(adjustment.WeaponPowerDelta));
        foreach (var modifier in adjustment.Modifiers)
            parts.Add(modifier.Type == EffectContentType.ModifyStat ? StatAdditionText(modifier) : "【装備効果】" + EquipmentEffectText(modifier));
        foreach (var change in adjustment.Replacements)
            parts.Add("この装備効果による" + (change.Rule == "CategoryIgnoreDefense" ? "防御点無視" : "追加ダメージ") +
                "は" + change.Amount.ToString(CultureInfo.InvariantCulture) + "に変化する。");
        if (parts.Count == 0) throw new InvalidOperationException("グレード補正が空です。");
        return "☆" + adjustment.Grade.ToString(CultureInfo.InvariantCulture) + "：" +
            string.Join(", ", parts);
    }

    private static string EquipmentStateText(StateDefinition state, HashSet<string> referencedStates = null)
    {
        var texts = SkillTextConverter.StateTexts(state, referencedStates);
        if (texts.Count == 0) throw new InvalidOperationException("状態の効果が空です：" + state.Name);
        return Token(state.Name, "状態名", "《》：:") + "効果" +
            (state.Categories.Count == 0 ? "" : CategoryText(state.Categories)) + "：" + string.Join("\n・", texts);
    }

    private static int SignedInteger(string value, string label)
    {
        string text = Need(value, label);
        int result;
        if ((text[0] != '+' && text[0] != '-') ||
            !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ||
            result == 0)
            throw new InvalidOperationException(label + "は0以外の符号付き整数にしてください。");
        return result;
    }

    private static string SignedIntegerText(int value)
    {
        return (value > 0 ? "+" : "") + value.ToString(CultureInfo.InvariantCulture);
    }
}
