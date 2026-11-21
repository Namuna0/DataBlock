using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static bool IsAppliedStateSpike(OverrideContentType type)
    {
        return type == OverrideContentType.SetAppliedStateDuration || type == OverrideContentType.SetStateDamageMultiplier || type == OverrideContentType.SetOnAppliedStateRecovery;
    }

    private static void AddModifierSpike(SkillBody skill, EffectType type, string value, params string[] rule)
    {
        var content = Change(OverrideContentType.SetModifier, new[] { "Rule" }.Concat(rule).Concat(new[] { value }).ToArray());
        ValidateModifierRuleTarget(skill, content);
        AddOverride(skill.Overrides, type, NoTriggers(), content);
    }

    private static IEnumerable<string[]> ModifierSpikeTargets(SkillBody skill, EffectType baseType)
    {
        return skill.Overrides.Where(e => e?.Type == baseType && e.Contents != null)
            .SelectMany(e => e.Contents.Select(c => ModifierRuleSelector(e, c)))
            .Where(r => r != null);
    }

    private static (string Subject, bool Multiplier) ModifierSpikeField(string[] rule)
    {
        switch (rule[0])
        {
            case "ActionStats":
            case "WeaponActivation": return ("達成値上昇", true);
            case "EnvironmentCheck":
            case "CategoryCheck":
            case "TimedWeaponActivation":
            case "EarlyBattleActivation": return ("達成値上昇", false);
            case "CraftCategories": return ("目標値の減少", false);
            case "AcquisitionCost": return ("消費決意", rule[1] == "Multiply");
            case "DailyGathering": return ("採取回数上限", false);
            case "EarlyBattleDamage": return ("被ダメージ", true);
            case "ResourceMaximum": return (rule[1] + "最大値", true);
            case "SalePrice": return ("売値増加", true);
            case "CategoryPower": return ("威力", true);
            case "CategoryHealing": return ("リソースの回復量", true);
            default: throw new InvalidOperationException("未対応の補正対象です：" + rule[0]);
        }
    }

    private static IEnumerable<string[]> MatchingSpikeTargets(SkillBody skill, EffectType baseType, (string Subject, bool Multiplier) field)
    {
        return ModifierSpikeTargets(skill, baseType).Where(rule => ModifierSpikeField(rule) == field).Take(2);
    }

    private static void ReplaceModifierSpike(SkillBody skill, EffectType type, string value, string kind)
    {
        var rules = ModifierSpikeTargets(skill, EffectType.Passive).Where(rule => rule[0] == kind).Take(2).ToList();
        if (rules.Count != 1) throw new InvalidOperationException("変更対象の基本補正を一意に特定できません：" + kind);
        AddModifierSpike(skill, type, value, rules[0]);
    }

    private static bool ReadModifierSpike(SkillBody skill, EffectType type, string s)
    {
        Match m = M(s, @"^この(?:パッシブ|アクティブ)効果の補正を「(.+)」に変更する。?$");
        if (m.Success)
        {
            // 明示形は同種の基本補正が複数ある場合にも変更先を特定できる。
            var temporary = new SkillBody();
            EffectType baseType = s.StartsWith("このアクティブ", StringComparison.Ordinal) ? EffectType.Active : EffectType.Passive;
            ReadSkillLine(temporary, baseType, m.Groups[1].Value);
            Require(temporary.Effects.Count == 0 && temporary.Overrides.Count == 1 && temporary.Overrides[0].Contents.Count == 1);
            var content = temporary.Overrides[0].Contents[0];
            string[] rule = ModifierRuleSelector(temporary.Overrides[0], content);
            Require(rule != null);
            AddModifierSpike(skill, type, content.Parameters.Last(), rule); return true;
        }
        m = M(s, @"^この発動ロールによる目標値は(.+?)に変化する。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetActivationRollTarget, m.Groups[1].Value)); return true;
        }
        m = M(s, @"^このアクティブ効果による目標値は([+-][0-9]+)される。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.AddTargetValue, m.Groups[1].Value)); return true;
        }
        m = M(s, @"^このカウンター効果による\[([^\]]+)\]の達成値は×?(.+?)倍される。?$");
        if (m.Success) { AddModifierSpike(skill, type, m.Groups[2].Value, "RerollResult", m.Groups[1].Value); return true; }
        m = M(s, @"^このパッシブ効果による達成値が([+-][0-9]+)、被ダメージは×(.+?)倍に変化する。?$");
        if (m.Success)
        {
            ReplaceModifierSpike(skill, type, m.Groups[1].Value, "EarlyBattleActivation");
            ReplaceModifierSpike(skill, type, m.Groups[2].Value, "EarlyBattleDamage"); return true;
        }
        m = M(s, @"^このパッシブ効果による威力とリソースの回復量上昇は×?(.+?)倍に変化する。?$");
        if (m.Success)
        {
            ReplaceModifierSpike(skill, type, m.Groups[1].Value, "CategoryPower");
            ReplaceModifierSpike(skill, type, m.Groups[1].Value, "CategoryHealing"); return true;
        }
        m = M(s, @"^この(パッシブ|アクティブ|カウンター)効果による(.+?)は(×?)([^、,。]+?)(倍?)に変化する。?$");
        if (!m.Success) return false;
        string heading = m.Groups[1].Value, subject = m.Groups[2].Value, value = m.Groups[4].Value;
        if (heading == "カウンター" && subject != "消費決意") return false;
        EffectType scope = heading == "アクティブ" ? EffectType.Active : EffectType.Passive;
        bool multiplier = m.Groups[3].Length != 0 || m.Groups[5].Length != 0;
        if (subject == "達成値の上昇") subject = "達成値上昇";
        if (subject == "威力上昇") { subject = "威力"; multiplier = true; }
        var targets = MatchingSpikeTargets(skill, scope, (subject, multiplier)).ToList();
        if (targets.Count > 1) throw new InvalidOperationException("変更対象の基本補正を一意に特定できません：" + subject);
        if (targets.Count == 1) { AddModifierSpike(skill, type, value, targets[0]); return true; }
        if (scope == EffectType.Passive && subject == "達成値上昇" && !multiplier && (value.StartsWith("+") || value.StartsWith("-")))
            return TryReadPassiveSpike(skill, type, "このパッシブ効果による達成値増加は" + value + "に変化する。");
        return false;
    }

    private static bool IsSpikeStage(EffectType type) { return type == EffectType.SecondSpike || type == EffectType.ThirdSpike; }

    private static bool IsStateReferenceSpike(OverrideContentType type)
    {
        return type == OverrideContentType.SetCooldown || type == OverrideContentType.SetStackAmount ||
            type == OverrideContentType.SetStateControlLimit || type == OverrideContentType.SetConditionalStatePower;
    }

}
