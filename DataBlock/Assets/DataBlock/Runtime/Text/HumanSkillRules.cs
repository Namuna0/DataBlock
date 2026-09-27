using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public static partial class SkillTextConverter
{
    // Rule selectors identify a modifier by meaning, never by skill name or list index.
    // SetModifier: Rule, selector, selector arguments..., replacement value.
    private static OverrideDefinition HumanRule(string[] rule, string value)
    {
        Need(value, "補正値");
        var result = new OverrideDefinition { Type = EffectType.Passive };
        OverrideContent content;
        switch (rule[0])
        {
            case "WeaponActivation":
                RuleLength(rule, 2);
                result.Triggers = SkillWeaponPowerTriggers(rule[1]);
                result.Triggers[0].Timing = TriggerTiming.ActionResult;
                result.Triggers[0].Conditions.And.Add(Condition(ConditionType.CheckContext, "Activation"));
                content = Change(OverrideContentType.MultiplyActionResult, value); break;
            case "ActionStats":
                Require(rule.Length >= 2);
                result.Triggers = On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionStat, rule.Skip(1).ToArray())));
                content = Change(OverrideContentType.MultiplyActionResult, value); break;
            case "EnvironmentCheck":
                RuleLength(rule, 1);
                result.Triggers = On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.CheckContext, "Environment")));
                content = Change(OverrideContentType.AddActionResult, Signed(value)); break;
            case "CraftCategories":
                Require(rule.Length >= 2);
                result.Triggers = On(Trigger(TriggerTiming.TargetValue, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.CheckContext, new[] { "Crafting" }.Concat(rule.Skip(1)).ToArray())));
                content = Change(OverrideContentType.AddTargetValue, Signed(value)); break;
            case "TimedWeaponActivation":
                RuleLength(rule, 3); result.Type = EffectType.Active;
                content = Change(OverrideContentType.AddTimedActionResult, "Self", "CurrentTurn", "WeaponPower", rule[1], rule[2], Signed(value)); break;
            case "AcquisitionCost":
                RuleLength(rule, 4);
                Require(rule[1] == "Add" || rule[1] == "Multiply");
                Require(rule[2] == "AllSkills" || rule[2] == "Class" || rule[2] == "SkillCategory");
                if (rule[2] == "AllSkills") Require(rule[3] == "All");
                content = Change(OverrideContentType.ModifyAcquisitionCost, "Self", "決意", rule[1], rule[2], rule[3], rule[1] == "Add" ? Signed(value) : value); break;
            case "DailyGathering":
                RuleLength(rule, 1);
                content = Change(OverrideContentType.ModifyStat, "Self", "一日の採取回数上限", "Add", Signed(value)); break;
            case "EarlyBattleActivation":
                RuleLength(rule, 3); Number(rule[1], 1, "持続ターン");
                result.Triggers = On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionStat, rule[2]), Condition(ConditionType.CheckContext, "Activation"), Condition(ConditionType.BattleTurnRange, "1", rule[1])));
                content = Change(OverrideContentType.AddActionResult, Signed(value)); break;
            case "EarlyBattleDamage":
                RuleLength(rule, 2); Number(rule[1], 1, "持続ターン");
                result.Triggers = On(Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.BattleTurnRange, "1", rule[1])));
                content = Change(OverrideContentType.MultiplyDamageTaken, "Self", value); break;
            case "ResourceMaximum":
                RuleLength(rule, 2);
                content = Change(OverrideContentType.ModifyStat, "Self", rule[1] + "最大値", "Multiply", value); break;
            case "SalePrice":
                RuleLength(rule, 2);
                content = Change(OverrideContentType.MultiplySalePrice, "NPC", "ExceptCategory", rule[1], value); break;
            case "CategoryPower":
                RuleLength(rule, 2); result.Triggers = CategoryPowerTriggers(rule[1]);
                content = Change(OverrideContentType.MultiplyPower, value); break;
            case "CategoryHealing":
                RuleLength(rule, 2);
                result.Triggers = On(Trigger(TriggerTiming.ResourceRecovery, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, rule[1]), Condition(ConditionType.EffectKind, "RestoreResource")));
                content = Change(OverrideContentType.MultiplyRecovery, value); break;
            case "CategoryCheck":
                RuleLength(rule, 2);
                result.Triggers = On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, rule[1])));
                content = Change(OverrideContentType.AddActionResult, Signed(value)); break;
            default: throw new InvalidOperationException("未対応の補正対象です：" + rule[0]);
        }
        decimal numeric;
        if (!decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out numeric))
            throw new InvalidOperationException("補正値には数値を指定してください。");
        if ((content.Type == OverrideContentType.MultiplyActionResult || content.Type == OverrideContentType.MultiplyDamageTaken ||
             content.Type == OverrideContentType.MultiplyPower || content.Type == OverrideContentType.MultiplyRecovery ||
             content.Type == OverrideContentType.MultiplySalePrice || rule[0] == "ResourceMaximum" ||
             (rule[0] == "AcquisitionCost" && rule[1] == "Multiply")) && numeric <= 0)
            throw new InvalidOperationException("倍率は0より大きい値を指定してください。");
        result.Contents.Add(content);
        return result;
    }

    private static void RuleLength(string[] rule, int length)
    {
        Require(rule.Length == length);
        foreach (string part in rule) Need(part, "補正対象");
    }

    private static string[] HumanRuleOf(OverrideDefinition effect, OverrideContent content)
    {
        if (effect == null || content == null || content.Parameters == null || effect.Triggers == null ||
            effect.Triggers.Any(t => !ValidTrigger(t))) return null;
        var p = content.Parameters;
        var conditions = effect.Triggers.SelectMany(t => t.Conditions.And).ToList();
        Func<ConditionType, string[]> get = kind => conditions.FirstOrDefault(c => c.Type == kind)?.Parameters?.ToArray();
        string[] stat = get(ConditionType.ActionStat), context = get(ConditionType.CheckContext),
            turn = get(ConditionType.BattleTurnRange), category = get(ConditionType.ActionCategory), weapon = get(ConditionType.WeaponCategory);
        string[] rule = null;
        switch (content.Type)
        {
            case OverrideContentType.MultiplyActionResult:
                if (weapon != null && weapon.Length == 1) rule = new[] { "WeaponActivation", weapon[0] };
                else if (stat != null) rule = new[] { "ActionStats" }.Concat(stat).ToArray();
                break;
            case OverrideContentType.AddActionResult:
                if (turn != null && turn.Length == 2 && stat != null && stat.Length == 1) rule = new[] { "EarlyBattleActivation", turn[1], stat[0] };
                else if (context != null && context.SequenceEqual(new[] { "Environment" })) rule = new[] { "EnvironmentCheck" };
                else if (category != null && category.Length == 1) rule = new[] { "CategoryCheck", category[0] };
                break;
            case OverrideContentType.AddTargetValue:
                if (context != null && context.Length >= 2 && context[0] == "Crafting") rule = new[] { "CraftCategories" }.Concat(context.Skip(1)).ToArray();
                break;
            case OverrideContentType.AddTimedActionResult:
                if (p.Count == 6) rule = new[] { "TimedWeaponActivation", p[3], p[4] }; break;
            case OverrideContentType.ModifyAcquisitionCost:
                if (p.Count == 6) rule = new[] { "AcquisitionCost", p[2], p[3], p[4] }; break;
            case OverrideContentType.ModifyStat:
                if (p.Count == 4 && p[1] == "一日の採取回数上限" && p[2] == "Add") rule = new[] { "DailyGathering" };
                else if (p.Count == 4 && p[1].EndsWith("最大値", StringComparison.Ordinal) && p[2] == "Multiply") rule = new[] { "ResourceMaximum", p[1].Substring(0, p[1].Length - 3) };
                break;
            case OverrideContentType.MultiplyDamageTaken:
                if (turn != null && turn.Length == 2) rule = new[] { "EarlyBattleDamage", turn[1] }; break;
            case OverrideContentType.MultiplySalePrice:
                if (p.Count == 4) rule = new[] { "SalePrice", p[2] }; break;
            case OverrideContentType.MultiplyPower:
                if (category != null && category.Length == 1) rule = new[] { "CategoryPower", category[0] }; break;
            case OverrideContentType.MultiplyRecovery:
                if (category != null && category.Length == 1) rule = new[] { "CategoryHealing", category[0] }; break;
        }
        if (rule == null || p.Count == 0) return null;
        OverrideDefinition expected;
        try { expected = HumanRule(rule, p.Last()); }
        catch (InvalidOperationException) { return null; } // Other codecs also support formula-valued modifiers.
        return effect.Type == expected.Type && Matches(effect.Triggers, expected.Triggers.ToArray()) &&
            content.Type == expected.Contents[0].Type && p.SequenceEqual(expected.Contents[0].Parameters) ? rule : null;
    }

    private static string HumanRuleText(string[] r, string value)
    {
        HumanRule(r, value); // Validate both the selector and value before rendering.
        switch (r[0])
        {
            case "WeaponActivation": return "スキルによって〈" + r[1] + "〉で武器攻撃を行う時、発動ロール達成値が×" + value + "倍される。";
            case "ActionStats": return string.Join("および", r.Skip(1).Select(s => "[" + s + "]")) + "による行為判定を行う際、達成値が×" + value + "倍される。";
            case "EnvironmentCheck": return "環境による行為判定を行う際、達成値が" + Signed(value) + "される。";
            case "CraftCategories": return "〈" + string.Join(", ", r.Skip(1)) + "〉の装備を制作するとき、目標値が" + Signed(value) + "される。";
            case "TimedWeaponActivation": return "このターン中、〈" + r[1] + "〉の武器威力を参照する〈" + r[2] + "〉の発動ロールの達成値が" + Signed(value) + "される。";
            case "AcquisitionCost":
                string subject = r[2] == "AllSkills" ? "スキル習得に必要な消費決意" : (r[2] == "Class" ? "クラス《" + r[3] + "》" : "〈" + r[3] + "〉") + "の習得決意";
                return subject + "が" + (r[1] == "Multiply" ? "×" + value + "倍" : Signed(value)) + "される。";
            case "DailyGathering": return "一日の採取回数上限が" + Signed(value) + "追加される。";
            case "EarlyBattleActivation": return "戦闘開始から" + r[1] + "ターンの間、[" + r[2] + "]による発動ロールの達成値が" + Signed(value) + "される。";
            case "EarlyBattleDamage": return "戦闘開始から" + r[1] + "ターンの間、自身の被ダメージは×" + value + "倍される。";
            case "ResourceMaximum": return r[1] + "最大値が×" + value + "倍される。";
            case "SalePrice": return "NPCへの〈" + r[1] + "〉を除くアイテムの売値が×" + value + "倍される。";
            case "CategoryPower": return "〈" + r[1] + "〉の威力が×" + value + "倍される。";
            case "CategoryHealing": return "〈" + r[1] + "〉によるリソースの回復量が×" + value + "倍される。";
            case "CategoryCheck": return "〈" + r[1] + "〉による行為判定の達成値が" + Signed(value) + "される。";
            default: throw new InvalidOperationException("未対応の補正対象です。");
        }
    }

    private static void AddHumanRule(SkillBody skill, EffectType type, string value, params string[] rule)
    {
        var effect = HumanRule(rule, value);
        Require(effect.Type == type);
        skill.Overrides.Add(effect);
    }

    private static string HumanOverrideText(OverrideDefinition effect, OverrideContent content)
    {
        if (content == null || effect == null) return null;
        if (content.Type == OverrideContentType.SetActivationRollTarget)
        {
            Require((effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike) && effect.Triggers.Count == 0);
            return "この発動ロールによる目標値は" + Args(content.Parameters, 1, "SetActivationRollTarget")[0] + "に変化する。";
        }
        if (content.Type == OverrideContentType.SetModifier && content.Parameters != null && content.Parameters.Count > 0 && content.Parameters[0] == "Rule")
        {
            Require((effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike) && effect.Triggers.Count == 0);
            var p = VariableArgs(content.Parameters, 3, "SetModifier/Rule");
            string[] rule = p.Skip(1).Take(p.Length - 2).ToArray();
            if (rule[0] == "RerollResult")
            {
                RuleLength(rule, 2); PositiveHumanFactor(p.Last());
                return "このカウンター効果による[" + rule[1] + "]の達成値は" + p.Last() + "倍される。";
            }
            var original = HumanRule(rule, p.Last());
            return "この" + (original.Type == EffectType.Active ? "アクティブ" : "パッシブ") + "効果の補正を「" + HumanRuleText(rule, p.Last()) + "」に変更する。";
        }
        string[] scope = HumanRuleOf(effect, content);
        return scope == null ? null : HumanRuleText(scope, content.Parameters.Last());
    }

    private static void PositiveHumanFactor(string value)
    {
        decimal factor;
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out factor) || factor <= 0)
            throw new InvalidOperationException("倍率は0より大きい数値にしてください。");
    }

    private static bool ValidateHumanModifierTarget(SkillBody skill, OverrideContent content)
    {
        if (content.Parameters == null || content.Parameters.Count == 0 || content.Parameters[0] != "Rule") return false;
        string[] p = VariableArgs(content.Parameters, 3, "SetModifier/Rule");
        string[] rule = p.Skip(1).Take(p.Length - 2).ToArray();
        if (rule[0] == "RerollResult")
        {
            RuleLength(rule, 2); PositiveHumanFactor(p.Last());
            Require(skill.Effects.Where(e => e.Type == EffectType.Counter && e.Triggers.Count == 0).SelectMany(e => e.Contents)
                .Count(c => c.Type == EffectContentType.RerollActivation && c.Parameters.SequenceEqual(new[] { "Self", rule[1], "TriggeringRoll" })) == 1);
            return true;
        }
        HumanRule(rule, p.Last());
        int count = skill.Overrides.Where(e => e.Type == EffectType.Passive || e.Type == EffectType.Active)
            .Sum(e => e.Contents.Count(c => { string[] candidate = HumanRuleOf(e, c); return candidate != null && candidate.SequenceEqual(rule); }));
        if (count != 1) throw new InvalidOperationException("スパイクの対象となる基本補正を1件指定してください：" + string.Join(" / ", rule));
        return true;
    }
}
