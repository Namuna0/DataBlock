using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

internal static partial class CommonEffectTextCodec
{
    // Only these linked operations inherit a trigger across a line break.
    private static readonly Dictionary<(string Trigger, string Operation), string> Continuations =
        new Dictionary<(string, string), string>
        {
            { ("ItemConsumedAsCost", "RepeatActive"), "RefundConsumedItems" },
            { ("ItemConsumedAsCost", "OwnState"), "RefundConsumedItems" },
            { ("ItemMeleeAction", "DamageFactor"), "TargetEntersTimedState" },
            { ("WeaponMultiHit", "ExtraElementAttack"), "CannotReduceDamage" },
            { ("Always", "OptionalNamedRoll"), "GainResourceOnSuccess" }
        };

    internal static bool TryReadContinuation(List<EffectContent> effects, string text)
    {
        if (effects == null || effects.Count != 1 || effects[0] == null) return false;
        var type = effects[0].Type;
        if (type == EffectContentType.GrantItem || type == EffectContentType.DeclareSelectedSkill)
            return SkillTextConverter.TryReadItemEffectContinuation(effects, text);
        if (type != EffectContentType.ConditionalRule) return false;
        var p = effects[0].Parameters;
        if (p == null || p.Count < 2) return false;
        if (!Continuations.TryGetValue((p[0], p[1]), out var continuation)) return false;
        var extra = OperationsByKey[continuation].Read(text);
        if (extra == null) return false;
        if (p[1] == "OptionalNamedRoll")
        {
            // The result belongs to this die roll at the start of the turn.
            effects[0] = Effect(EffectContentType.ConditionalRule,
                "SuccessfulTurnStartCheck", "GainResource", p[2], p[3], extra[0], extra[1]);
            return true;
        }
        var triggerArguments = p.Skip(2).Take(TriggersByKey[p[0]].ParameterCount);
        effects.Add(Effect(EffectContentType.ConditionalRule,
            new[] { p[0], continuation }.Concat(triggerArguments).Concat(extra).ToArray()));
        return true;
    }

    internal static string WriteGroup(List<EffectContent> effects)
    {
        if (effects == null || effects.Count == 0) return null;
        if (effects.All(e => e?.Type == EffectContentType.SelectedWeaponRule))
            return SelectedPrefix + string.Join("、", effects.Select(RuleText)) + "。";
        if (effects.Count != 2 || effects.Any(e => e?.Type != EffectContentType.ConditionalRule)) return null;
        var first = effects[0].Parameters;
        var second = effects[1].Parameters;
        if (first == null || second == null || first.Count < 2 || second.Count < 2)
            throw new InvalidOperationException("条件付き効果の条件と操作を指定してください。");
        if (first[0] == "Always" || first[0] != second[0] ||
            !Continuations.TryGetValue((first[0], first[1]), out var continuation) ||
            continuation != second[1]) return null;
        int count = TriggersByKey[first[0]].ParameterCount;
        if (!first.Skip(2).Take(count).SequenceEqual(second.Skip(2).Take(count))) return null;
        return ConditionalText(effects[0]) + "\n" +
            OperationsByKey[continuation].Write(second.Skip(2 + count).ToList()) + "。";
    }

    internal static bool TryRead(string text, out List<EffectContent> contents)
    {
        if (TryReadRuleContents(text, out contents, includeActions: true)) return true;
        return !string.IsNullOrWhiteSpace(text) && SkillTextConverter.TryReadCommonSkillEffect(text, out contents);
    }

    internal static bool TryReadRuleContents(string text, out List<EffectContent> contents, bool includeActions = false)
    {
        contents = new List<EffectContent>();
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim().Replace("この武器による威力には、", "この武器による威力は");
        Match sharedStat = M(text, @"^自身の\[?([一-龠ァ-ヶー]+B)\]?と\[?([一-龠ァ-ヶー]+B)\]?(?:は|が)([+-]" + MathFormula + @")される。?$");
        if (sharedStat.Success)
            text = sharedStat.Groups[1].Value + sharedStat.Groups[3].Value + ", " +
                sharedStat.Groups[2].Value + sharedStat.Groups[3].Value;
        if (TryReadAtom(text, out var atom))
        {
            contents.Add(atom);
            return true;
        }
        if (TryReadWeaponClauses(text, contents)) return true;
        contents.Clear();
        List<string> parts = Split(text, ",");
        if (parts.Count == 1) parts = Split(text, "、");
        if (parts.Count < 2) return false;
        foreach (string part in parts)
        {
            if (TryReadAtom(part, out atom)) contents.Add(atom);
            else if (includeActions && SkillTextConverter.TryReadCommonSkillEffect(part, out var actions)) contents.AddRange(actions);
            else
            {
                contents.Clear();
                return false;
            }
        }
        return true;
    }

    private static bool TryReadAtom(string text, out EffectContent effect)
    {
        return TryReadAbilityCheckMultiplier(text, out effect) || TryReadModifier(text, out effect) ||
            TryReadRuleSentence(text, out effect) || TryReadStatModifier(text, out effect);
    }

    internal static bool TryReadRuleSentence(string text, out EffectContent effect)
    {
        return TryReadRule(text, EffectContentType.RuleModifier, out effect) || TryReadConditional(text, out effect);
    }

    internal static bool TryReadStatModifier(string source, out EffectContent effect)
    {
        string text = source.Trim().TrimEnd('。', '.');
        const string subject = @"(?:(?<actor>自身|対象|付与者)の)?";
        const string stat = @"(?:\[(?<stat>[^\]]+)\]|(?<stat>[^+\-×*/()〈〉《》【】\[\]。、,\s]+?)|(?<stat>〈[^〉]+〉所持数))";
        Match match = M(text, "^" + subject + stat + @"(?:は|が)?(?<value>[+-]" + MathFormula + @")(?:加算される|される)?$");
        string operation = "Add";
        if (!match.Success)
        {
            match = M(text, "^" + subject + stat + @"(?:は|が)?[×x](?<value>" + MathFormula + @")(?:倍)?される$");
            if (!match.Success)
            {
                effect = null;
                return false;
            }
            operation = "Multiply";
        }
        string actor = match.Groups["actor"].Success ? ActorKey(match.Groups["actor"].Value) : "Self";
        effect = Effect(EffectContentType.ModifyStat, actor, match.Groups["stat"].Value, operation, match.Groups["value"].Value);
        return true;
    }

    private static bool TryReadModifier(string source, out EffectContent effect)
    {
        string text = source.Trim();
        Match match = M(text,
            @"^この武器の選択を条件にスキルを発動する場合[、,]\s*〈([^〉]+)〉の達成値[×x](" + Factor + @")倍かつ威力は[×x](" + Factor + @")倍される[。.]?$");
        if (match.Success)
        {
            effect = Effect(EffectContentType.ModifySelectedWeaponAction,
                match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
            return true;
        }

        match = M(text, @"^(自身)の〈([^〉]+)〉による被ダメージは[×x](" + Factor + @")倍される[。.]?$");
        if (match.Success)
        {
            effect = Effect(EffectContentType.MultiplyIncomingDamage,
                ActorKey(match.Groups[1].Value), match.Groups[2].Value, match.Groups[3].Value);
            return true;
        }

        match = M(text, @"^(自身)は(.+?)フェーズの行動で〈([^〉]+)〉の使用を宣言する事が出来る[。.]?$");
        if (match.Success)
        {
            string phase = match.Groups[2].Value == "エンカウント"
                ? "Encounter"
                : Need(match.Groups[2].Value, "フェーズ");
            effect = Effect(EffectContentType.AllowItemUse,
                ActorKey(match.Groups[1].Value), phase, match.Groups[3].Value);
            return true;
        }

        if (TryReadCategoryPower(text, out effect)) return true;

        match = M(text, @"^(自身)が〈([^〉]+)〉を発動した時[、,]\s*《([^》]+)》状態を([0-9]+)スタック得る[。.]?$");
        if (match.Success)
        {
            effect = Effect(EffectContentType.GainStateStacksOnAction,
                ActorKey(match.Groups[1].Value), match.Groups[2].Value, match.Groups[3].Value,
                Number(match.Groups[4].Value, 1, "獲得スタック数").ToString(CultureInfo.InvariantCulture));
            return true;
        }

        effect = null;
        return false;
    }

    internal static bool TryReadCategoryPower(string text, out EffectContent effect)
    {
        Match match = M(text, @"^(?:自身の)?〈([^〉]+)〉(?:による|の)威力(?:が|は)[×x](" + Factor + @")倍される[。.]?$");
        effect = match.Success ? Effect(EffectContentType.MultiplyActionPower, "Self", match.Groups[1].Value, match.Groups[2].Value) : null;
        return effect != null;
    }

    internal static bool TryReadAbilityCheckMultiplier(string text, out EffectContent effect)
    {
        const string stat = @"\[(?<stat>[^\[\]、,。]+)\]";
        var match = M(text, "^" + stat + "(?:(?:および|及び)" + stat +
            @")*による行為判定(?:を行う際[、,]\s*達成値が|の達成値[はが])×?(?<factor>" + MathFormula + @")倍される。?$");
        effect = match.Success ? Effect(EffectContentType.StatRollFactorForAction, match.Groups["stat"].Captures.Cast<Capture>()
            .Select(c => c.Value).Concat(new[] { match.Groups["factor"].Value }).ToArray()) : null;
        return effect != null;
    }

    internal static string AbilityCheckMultiplierText(IEnumerable<string> stats, string factor, bool state = false)
    {
        var names = stats.Select(stat => "[" + Token(stat, "判定能力値", "[]、,。") + "]").ToList();
        if (names.Count == 0 || !M(Need(factor, "達成値倍率"), "^" + MathFormula + "$").Success)
            throw new InvalidOperationException("行為判定の能力値と倍率を指定してください。");
        return string.Join(state ? "及び" : "および", names) +
            (state ? "による行為判定の達成値は×" : "による行為判定を行う際、達成値が×") + factor + "倍される。";
    }

    internal static string Write(EffectContent effect)
    {
        return WriteRuleContent(effect) ?? SkillTextConverter.WriteCommonSkillEffect(effect);
    }

    internal static string WriteRuleContent(EffectContent effect)
    {
        if (effect == null) throw new InvalidOperationException("効果がnullです。");
        string[] parameters;
        switch (effect.Type)
        {
            case EffectContentType.StatRollFactorForAction:
                var values = effect.Parameters;
                if (values == null || values.Count < 2) throw new InvalidOperationException("行為判定の能力値と倍率を指定してください。");
                return AbilityCheckMultiplierText(values.Take(values.Count - 1), values.Last());
            case EffectContentType.CheckByAbilityFactor:
                return ClausesByKey["StatResultFactor"].Write(effect.Parameters) + "される。";
            case EffectContentType.SelectedWeaponRule: return SelectedPrefix + RuleText(effect) + "。";
            case EffectContentType.RuleModifier: return RuleText(effect);
            case EffectContentType.ConditionalRule: return ConditionalText(effect);
            case EffectContentType.ModifyStat:
                parameters = Args(effect.Parameters, 4, "ModifyStat");
                Token(parameters[1], "能力値名", "[],、+-×*/()");
                if (!M(parameters[3], "^" + MathFormula + "$").Success)
                    throw new InvalidOperationException("能力値補正の数値または式が不正です：" + parameters[3]);
                if (parameters[2] == "Add")
                    return (parameters[0] == "Self" ? "" : Actor(parameters[0]) + "の") +
                        parameters[1] + Signed(parameters[3]);
                if (parameters[2] == "Multiply")
                    return Actor(parameters[0]) + "の" + parameters[1] + "は×" + parameters[3] + "される。";
                throw new InvalidOperationException("ModifyStatの演算はAdd / Multiplyで指定してください。");
            case EffectContentType.ModifySelectedWeaponAction:
                parameters = Args(effect.Parameters, 3, "ModifySelectedWeaponAction");
                Token(parameters[0], "行動カテゴリー", "〈〉,、");
                PositiveDecimal(parameters[1], "達成値倍率");
                PositiveDecimal(parameters[2], "威力倍率");
                return "この武器の選択を条件にスキルを発動する場合、〈" + parameters[0] +
                    "〉の達成値×" + parameters[1] + "倍かつ威力は×" + parameters[2] + "倍される。";
            case EffectContentType.MultiplyIncomingDamage:
                parameters = Args(effect.Parameters, 3, "MultiplyIncomingDamage");
                RequireSelf(parameters[0], "MultiplyIncomingDamage");
                Token(parameters[1], "行動カテゴリー", "〈〉,、");
                PositiveDecimal(parameters[2], "被ダメージ倍率");
                return Actor(parameters[0]) + "の〈" + parameters[1] + "〉による被ダメージは×" +
                    parameters[2] + "倍される。";
            case EffectContentType.AllowItemUse:
                parameters = Args(effect.Parameters, 3, "AllowItemUse");
                RequireSelf(parameters[0], "AllowItemUse");
                if (parameters[1] != "Encounter")
                    throw new InvalidOperationException("AllowItemUseのフェーズはEncounterです。");
                Token(parameters[2], "アイテムカテゴリー", "〈〉,、");
                return Actor(parameters[0]) + "はエンカウントフェーズの行動で〈" +
                    parameters[2] + "〉の使用を宣言する事が出来る。";
            case EffectContentType.MultiplyActionPower:
                parameters = Args(effect.Parameters, 3, "MultiplyActionPower");
                RequireSelf(parameters[0], "MultiplyActionPower");
                Token(parameters[1], "行動カテゴリー", "〈〉,、");
                PositiveDecimal(parameters[2], "威力倍率");
                return Actor(parameters[0]) + "の〈" + parameters[1] + "〉による威力が×" +
                    parameters[2] + "倍される。";
            case EffectContentType.GainStateStacksOnAction:
                parameters = Args(effect.Parameters, 4, "GainStateStacksOnAction");
                RequireSelf(parameters[0], "GainStateStacksOnAction");
                Token(parameters[1], "行動カテゴリー", "〈〉,、");
                Token(parameters[2], "状態名", "《》：:");
                return Actor(parameters[0]) + "が〈" + parameters[1] + "〉を発動した時、《" +
                    parameters[2] + "》状態を" +
                    Number(parameters[3], 1, "獲得スタック数").ToString(CultureInfo.InvariantCulture) + "スタック得る。";
            default:
                return null;
        }
    }

    internal static string StatAdditionText(EffectContent effect)
    {
        string[] parameters = Args(effect.Parameters, 4, "ModifyStat");
        if (parameters[0] != "Self" || parameters[2] != "Add")
            throw new InvalidOperationException("能力値加算はSelf / Addで指定してください。");
        return parameters[1] + Signed(parameters[3]);
    }

    internal static EffectContent Effect(EffectContentType type, params string[] parameters)
    {
        return new EffectContent { Type = type, Parameters = parameters.ToList() };
    }

    private static string Actor(string actor)
    {
        if (actor == "Self") return "自身";
        if (actor == "Target") return "対象";
        if (actor == "Applier") return "付与者";
        throw new InvalidOperationException("効果の人物はSelf / Target / Applierで指定してください。");
    }

    internal static string ActorKey(string actor)
    {
        if (actor == "自身") return "Self";
        if (actor == "対象") return "Target";
        if (actor == "付与者") return "Applier";
        throw new InvalidOperationException("効果の人物を読み取れません：" + actor);
    }

    internal static string Token(string value, string label, string forbiddenCharacters)
    {
        string result = Need(value, label);
        if (result.IndexOfAny(forbiddenCharacters.ToCharArray()) >= 0)
            throw new InvalidOperationException(label + "に構文区切り文字「" + forbiddenCharacters + "」は使用できません。");
        return result;
    }

    internal static void RequireSelf(string actor, string label)
    {
        if (actor != "Self") throw new InvalidOperationException(label + "の対象はSelfです。");
    }

    private static void PositiveDecimal(string value, string label)
    {
        if (!M(Need(value, label), "^" + Factor + "$").Success)
            throw new InvalidOperationException(label + "は0より大きい数値にしてください。");
    }

    internal static Match M(string text, string pattern)
    {
        return DataBlockText.GetRegex(pattern).Match(text);
    }

    internal static string RegexReplace(string text, string pattern, string replacement, RegexOptions options = RegexOptions.None)
    {
        return DataBlockText.GetRegex(pattern, options).Replace(text, replacement);
    }

    internal static string Need(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException(label + "が空です。");
        if (value.Contains("\n") || value.Contains("\r")) throw new InvalidOperationException(label + "に改行は使用できません。");
        return value.Trim();
    }

    internal static string[] Args(List<string> values, int count, string label)
    {
        if (values == null || values.Count != count)
            throw new InvalidOperationException(label + "のパラメーターは" + count + "個です。");
        return values.Select(x => Need(x, label + "のパラメーター")).ToArray();
    }

    internal static int Number(string value, int minimum, string label)
    {
        int result;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) || result < minimum)
            throw new InvalidOperationException(label + "は" + minimum + "以上の整数にしてください。");
        return result;
    }

    internal static string Signed(string value)
    {
        value = Need(value, "加減算値");
        if (!value.StartsWith("+", StringComparison.Ordinal) && !value.StartsWith("-", StringComparison.Ordinal))
            value = "+" + value;
        if (value.Length < 2) throw new InvalidOperationException("加減算値に数値または式が必要です。");
        return value;
    }

    internal static List<string> Split(string text, string delimiter)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if ("([〈《（［".IndexOf(c) >= 0) depth++;
            if (")]〉》）］".IndexOf(c) >= 0) depth--;
            if (depth < 0) throw new InvalidOperationException("括弧の対応が不正です：" + text);
            if (depth == 0 && i + delimiter.Length <= text.Length &&
                string.CompareOrdinal(text, i, delimiter, 0, delimiter.Length) == 0)
            {
                string part = text.Substring(start, i - start).Trim();
                if (part.Length == 0)
                    throw new InvalidOperationException("区切り文字の前後に空の項目があります：" + text);
                result.Add(part);
                i += delimiter.Length - 1;
                start = i + 1;
            }
        }
        if (depth != 0) throw new InvalidOperationException("括弧が閉じていません：" + text);
        string last = text.Substring(start).Trim();
        if (last.Length == 0 && result.Count > 0)
            throw new InvalidOperationException("区切り文字の前後に空の項目があります：" + text);
        if (last.Length > 0) result.Add(last);
        return result;
    }
}
