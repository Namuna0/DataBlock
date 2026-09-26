using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class EquipmentTextConverter
{
    private static void ReadEquipmentEffectLine(EquipmentDefinition equipment, string source)
    {
        string text = Unbullet(source);
        string name = "";
        Match named = M(text, @"^《([^《》]+)》[：:](.+)$");
        if (named.Success)
        {
            name = Need(named.Groups[1].Value, "装備効果名");
            text = named.Groups[2].Value.Trim();
        }

        var group = new EquipmentEffectGroup { Name = name };
        EquipmentEffectDefinition special;
        if (TryReadSpecialEquipmentEffect(text, out special))
        {
            group.Effects.Add(special);
        }
        else
        {
            foreach (string part in Split(text.Replace("、", ","), ","))
            {
                EquipmentEffectDefinition modifier;
                if (!TryReadStatModifier(part, out modifier))
                    throw new InvalidOperationException("未対応の装備効果です：" + part);
                group.Effects.Add(modifier);
            }
        }
        if (group.Effects.Count == 0) throw new InvalidOperationException("装備効果が空です。");
        equipment.EffectGroups.Add(group);
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
            EquipmentEffectDefinition modifier;
            if (!TryReadStatModifier(part, out modifier))
                throw new InvalidOperationException("未対応のグレード補正です：" + part);

            if (modifier.Parameters[1] == "耐久最大値")
            {
                if (adjustment.MaxDurabilityDelta != 0)
                    throw new InvalidOperationException("耐久最大値のグレード補正が重複しています。");
                adjustment.MaxDurabilityDelta = SignedInteger(
                    modifier.Parameters[3], "耐久最大値のグレード補正");
            }
            else
            {
                adjustment.Modifiers.Add(modifier);
            }
        }
        if (adjustment.MaxDurabilityDelta == 0 && adjustment.Modifiers.Count == 0)
            throw new InvalidOperationException("グレード補正が空です。");
        equipment.GradeAdjustments.Add(adjustment);
    }

    private static bool TryReadStatModifier(string source, out EquipmentEffectDefinition effect)
    {
        string text = source.Trim().TrimEnd('。', '.');
        Match match = M(text, @"^自身の\[([^\]]+)\](?:は|が)([+-].+?)される$");
        if (!match.Success) match = M(text, @"^自身の(.+?)(?:は|が)([+-].+?)される$");
        if (!match.Success) match = M(text, @"^([^+\-]+?)([+-].+)$");
        if (!match.Success)
        {
            effect = null;
            return false;
        }
        effect = Effect(EquipmentEffectType.ModifyStat, "Self",
            Need(match.Groups[1].Value, "能力値名"), "Add", Signed(match.Groups[2].Value));
        return true;
    }

    private static bool TryReadSpecialEquipmentEffect(string source, out EquipmentEffectDefinition effect)
    {
        string text = source.Trim();
        Match match = M(text,
            @"^この武器の選択を条件にスキルを発動する場合[、,]\s*〈([^〉]+)〉の達成値[×x](.+?)倍かつ威力は[×x](.+?)倍される[。.]?$");
        if (match.Success)
        {
            effect = Effect(EquipmentEffectType.ModifySelectedWeaponAction,
                match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
            return true;
        }

        match = M(text, @"^(自身)の〈([^〉]+)〉による被ダメージは[×x](.+?)倍される[。.]?$");
        if (match.Success)
        {
            effect = Effect(EquipmentEffectType.MultiplyIncomingDamage,
                ActorKey(match.Groups[1].Value), match.Groups[2].Value, match.Groups[3].Value);
            return true;
        }

        match = M(text, @"^(自身)は(.+?)フェーズの行動で〈([^〉]+)〉の使用を宣言する事が出来る[。.]?$");
        if (match.Success)
        {
            string phase = match.Groups[2].Value == "エンカウント"
                ? "Encounter"
                : Need(match.Groups[2].Value, "フェーズ");
            effect = Effect(EquipmentEffectType.AllowItemUse,
                ActorKey(match.Groups[1].Value), phase, match.Groups[3].Value);
            return true;
        }

        match = M(text, @"^(自身)の〈([^〉]+)〉による威力が[×x](.+?)倍される[。.]?$");
        if (match.Success)
        {
            effect = Effect(EquipmentEffectType.MultiplyActionPower,
                ActorKey(match.Groups[1].Value), match.Groups[2].Value, match.Groups[3].Value);
            return true;
        }

        match = M(text, @"^(自身)が〈([^〉]+)〉を発動した時[、,]\s*《([^》]+)》状態を([0-9]+)スタック得る[。.]?$");
        if (match.Success)
        {
            effect = Effect(EquipmentEffectType.GainStateStacksOnAction,
                ActorKey(match.Groups[1].Value), match.Groups[2].Value, match.Groups[3].Value,
                Number(match.Groups[4].Value, 1, "獲得スタック数").ToString(CultureInfo.InvariantCulture));
            return true;
        }

        effect = null;
        return false;
    }

    private static bool TryReadEquipmentState(EquipmentDefinition equipment, string source)
    {
        string text = Unbullet(source);
        string name;
        List<string> categories;
        string body;
        Match canonical = M(text, @"^([^《【●].*?)効果((?:〈[^〉]+〉)+)[：:](.+)$");
        if (canonical.Success)
        {
            name = Need(canonical.Groups[1].Value, "状態名");
            categories = Categories(canonical.Groups[2].Value);
            body = canonical.Groups[3].Value.Trim();
        }
        else
        {
            Match shorthand = M(text, @"^([^《【●][^：:]+)[：:](.+)$");
            if (!shorthand.Success) return false;
            name = Need(shorthand.Groups[1].Value, "状態名");
            categories = new List<string> { "スタック" };
            body = shorthand.Groups[2].Value.Trim();
        }

        Match modifier = M(body, @"^(自身)の(.+?)は[×x](.+?)される[。.]?$");
        if (!modifier.Success) return false;
        var state = new EquipmentStateDefinition { Name = name, Categories = categories };
        state.Modifiers.Add(Effect(EquipmentEffectType.ModifyStat,
            ActorKey(modifier.Groups[1].Value), modifier.Groups[2].Value, "Multiply", modifier.Groups[3].Value));
        equipment.States.Add(state);
        return true;
    }

    private static string EquipmentEffectGroupText(EquipmentEffectGroup group)
    {
        if (group == null || group.Effects == null || group.Effects.Count == 0)
            throw new InvalidOperationException("装備効果グループが空です。");
        string body;
        if (group.Effects.All(x => x != null && x.Type == EquipmentEffectType.ModifyStat &&
            x.Parameters != null && x.Parameters.Count == 4 && x.Parameters[2] == "Add"))
        {
            body = string.Join(", ", group.Effects.Select(StatAdditionText));
        }
        else
        {
            if (group.Effects.Count != 1)
                throw new InvalidOperationException("能力値加算以外の装備効果は1グループ1件です。");
            body = EquipmentEffectText(group.Effects[0]);
        }
        return string.IsNullOrWhiteSpace(group.Name)
            ? body
            : "《" + Need(group.Name, "装備効果名") + "》：" + body;
    }

    private static string EquipmentGradeAdjustmentText(EquipmentGradeAdjustmentDefinition adjustment)
    {
        if (adjustment == null || adjustment.Modifiers == null)
            throw new InvalidOperationException("グレード補正がnullです。");

        var parts = new List<string>();
        if (adjustment.MaxDurabilityDelta != 0)
            parts.Add("耐久最大値" + SignedIntegerText(adjustment.MaxDurabilityDelta));
        parts.AddRange(adjustment.Modifiers.Select(StatAdditionText));
        if (parts.Count == 0) throw new InvalidOperationException("グレード補正が空です。");
        return "☆" + adjustment.Grade.ToString(CultureInfo.InvariantCulture) + "：" +
            string.Join(", ", parts);
    }

    private static string EquipmentEffectText(EquipmentEffectDefinition effect)
    {
        if (effect == null) throw new InvalidOperationException("装備効果がnullです。");
        string[] parameters;
        switch (effect.Type)
        {
            case EquipmentEffectType.ModifyStat:
                parameters = Args(effect.Parameters, 4, "ModifyStat");
                if (parameters[2] == "Add") return StatAdditionText(effect);
                if (parameters[2] == "Multiply")
                    return Actor(parameters[0]) + "の" + parameters[1] + "は×" + parameters[3] + "される。";
                throw new InvalidOperationException("ModifyStatの演算はAdd / Multiplyで指定してください。");
            case EquipmentEffectType.ModifySelectedWeaponAction:
                parameters = Args(effect.Parameters, 3, "ModifySelectedWeaponAction");
                return "この武器の選択を条件にスキルを発動する場合、〈" + parameters[0] +
                    "〉の達成値×" + parameters[1] + "倍かつ威力は×" + parameters[2] + "倍される。";
            case EquipmentEffectType.MultiplyIncomingDamage:
                parameters = Args(effect.Parameters, 3, "MultiplyIncomingDamage");
                return Actor(parameters[0]) + "の〈" + parameters[1] + "〉による被ダメージは×" +
                    parameters[2] + "倍される。";
            case EquipmentEffectType.AllowItemUse:
                parameters = Args(effect.Parameters, 3, "AllowItemUse");
                string phase = parameters[1] == "Encounter" ? "エンカウント" : parameters[1];
                return Actor(parameters[0]) + "は" + phase + "フェーズの行動で〈" +
                    parameters[2] + "〉の使用を宣言する事が出来る。";
            case EquipmentEffectType.MultiplyActionPower:
                parameters = Args(effect.Parameters, 3, "MultiplyActionPower");
                return Actor(parameters[0]) + "の〈" + parameters[1] + "〉による威力が×" +
                    parameters[2] + "倍される。";
            case EquipmentEffectType.GainStateStacksOnAction:
                parameters = Args(effect.Parameters, 4, "GainStateStacksOnAction");
                return Actor(parameters[0]) + "が〈" + parameters[1] + "〉を発動した時、《" +
                    parameters[2] + "》状態を" +
                    Number(parameters[3], 1, "獲得スタック数").ToString(CultureInfo.InvariantCulture) + "スタック得る。";
            default:
                throw new InvalidOperationException("未対応の装備効果です：" + effect.Type);
        }
    }

    private static string StatAdditionText(EquipmentEffectDefinition effect)
    {
        string[] parameters = Args(effect.Parameters, 4, "ModifyStat");
        if (parameters[0] != "Self" || parameters[2] != "Add")
            throw new InvalidOperationException("装備の能力値加算はSelf / Addで指定してください。");
        return parameters[1] + Signed(parameters[3]);
    }

    private static string EquipmentStateText(EquipmentStateDefinition state)
    {
        if (state == null || state.Modifiers == null || state.Modifiers.Count != 1)
            throw new InvalidOperationException("装備状態の補正は1件必要です。");
        return Need(state.Name, "状態名") + "効果" + CategoryText(state.Categories) + "：" +
            EquipmentEffectText(state.Modifiers[0]);
    }

    private static EquipmentEffectDefinition Effect(EquipmentEffectType type, params string[] parameters)
    {
        return new EquipmentEffectDefinition { Type = type, Parameters = parameters.ToList() };
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

    private static string Actor(string actor)
    {
        if (actor == "Self") return "自身";
        throw new InvalidOperationException("装備効果の人物はSelfで指定してください。");
    }

    private static string ActorKey(string actor)
    {
        if (actor == "自身") return "Self";
        throw new InvalidOperationException("装備効果の人物を読み取れません：" + actor);
    }
}
