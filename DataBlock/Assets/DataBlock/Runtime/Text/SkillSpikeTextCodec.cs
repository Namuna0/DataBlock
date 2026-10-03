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

    private static void ReplaceModifierSpike(SkillBody skill, EffectType type, string value, params string[] kinds)
    {
        var rules = skill.Overrides.Where(e => e.Type == EffectType.Passive || e.Type == EffectType.Active)
            .SelectMany(e => e.Contents.Select(c => ModifierRuleSelector(e, c)))
            .Where(r => r != null && kinds.Contains(r[0])).ToList();
        if (rules.Count != 1) throw new InvalidOperationException("変更対象の基本補正を一意に特定できません：" + string.Join(" / ", kinds));
        AddModifierSpike(skill, type, value, rules[0]);
    }

    private static bool ReadModifierSpike(SkillBody skill, EffectType type, string s)
    {
        Match m = M(s, @"^この(?:パッシブ|アクティブ)効果の補正を「(.+)」に変更する。?$");
        if (m.Success)
        {
            // Canonical text names the whole selector, so multiple modifiers remain unambiguous.
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
        m = M(s, @"^このカウンター効果による\[([^\]]+)\]の達成値は×?(.+?)倍される。?$");
        if (m.Success) { AddModifierSpike(skill, type, m.Groups[2].Value, "RerollResult", m.Groups[1].Value); return true; }
        m = M(s, @"^このパッシブ効果による達成値が([+-][0-9]+)、被ダメージは×(.+?)倍に変化する。?$");
        if (m.Success)
        {
            ReplaceModifierSpike(skill, type, m.Groups[1].Value, "EarlyBattleActivation");
            ReplaceModifierSpike(skill, type, m.Groups[2].Value, "EarlyBattleDamage"); return true;
        }
        m = M(s, @"^このパッシブ効果による達成値上昇は×(.+?)倍に変化する。?$");
        if (m.Success) { ReplaceModifierSpike(skill, type, m.Groups[1].Value, "ActionStats", "WeaponActivation"); return true; }
        m = M(s, @"^この(パッシブ|アクティブ)効果による達成値(?:の)?上昇は([+-].+?)に変化する。?$");
        if (m.Success)
        {
            // Both 上昇 and 増加 describe the same single-stat increase.
            var candidates = skill.Overrides.Where(e => e.Type == EffectType.Passive || e.Type == EffectType.Active)
                .SelectMany(e => e.Contents.Select(c => ModifierRuleSelector(e, c))).Where(r => r != null &&
                    new[] { "EnvironmentCheck", "CategoryCheck", "TimedWeaponActivation", "EarlyBattleActivation" }.Contains(r[0])).ToList();
            if (candidates.Count == 0 && m.Groups[1].Value == "パッシブ")
                return TryReadPassiveSpike(skill, type, "このパッシブ効果による達成値増加は" + m.Groups[2].Value + "に変化する。");
            ReplaceModifierSpike(skill, type, m.Groups[2].Value, m.Groups[1].Value == "アクティブ" ? new[] { "TimedWeaponActivation" } : new[] { "EnvironmentCheck", "CategoryCheck", "EarlyBattleActivation" }); return true;
        }
        m = M(s, @"^このパッシブ効果による目標値の減少は([+-].+?)に変化する。?$");
        if (m.Success) { ReplaceModifierSpike(skill, type, m.Groups[1].Value, "CraftCategories"); return true; }
        m = M(s, @"^この(?:パッシブ|カウンター)効果による消費決意は([+-].+?)に変化する。?$");
        if (m.Success)
        {
            // A counter heading here is accepted only when its unique target is the passive acquisition-cost modifier.
            ReplaceModifierSpike(skill, type, m.Groups[1].Value, "AcquisitionCost"); return true;
        }
        m = M(s, @"^このパッシブ効果による採取回数上限は([+-].+?)に変化する。?$");
        if (m.Success) { ReplaceModifierSpike(skill, type, m.Groups[1].Value, "DailyGathering"); return true; }
        m = M(s, @"^このパッシブ効果による([A-Z]+)最大値は×?(.+?)倍に変化する。?$");
        if (m.Success) { AddModifierSpike(skill, type, m.Groups[2].Value, "ResourceMaximum", m.Groups[1].Value); return true; }
        m = M(s, @"^このパッシブ効果による売値増加は×(.+?)倍に変化する。?$");
        if (m.Success) { ReplaceModifierSpike(skill, type, m.Groups[1].Value, "SalePrice"); return true; }
        m = M(s, @"^このパッシブ効果による威力とリソースの回復量上昇は×?(.+?)倍に変化する。?$");
        if (m.Success)
        {
            ReplaceModifierSpike(skill, type, m.Groups[1].Value, "CategoryPower");
            ReplaceModifierSpike(skill, type, m.Groups[1].Value, "CategoryHealing"); return true;
        }
        return false;
    }

    private static bool IsSpikeStage(EffectType type) { return type == EffectType.SecondSpike || type == EffectType.ThirdSpike; }

    private static bool IsStateReferenceSpike(OverrideContentType type)
    {
        return type == OverrideContentType.SetCooldown || type == OverrideContentType.SetStackAmount ||
            type == OverrideContentType.SetStateControlLimit || type == OverrideContentType.SetConditionalStatePower;
    }

}
