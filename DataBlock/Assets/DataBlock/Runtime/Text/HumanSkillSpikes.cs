using System;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static void HumanSpike(SkillBody skill, EffectType type, string value, params string[] rule)
    {
        var content = Change(OverrideContentType.SetModifier, new[] { "Rule" }.Concat(rule).Concat(new[] { value }).ToArray());
        ValidateHumanModifierTarget(skill, content);
        AddOverride(skill.Overrides, type, NoTriggers(), content);
    }

    private static void HumanSpikeFor(SkillBody skill, EffectType type, string value, params string[] kinds)
    {
        var rules = skill.Overrides.Where(e => e.Type == EffectType.Passive || e.Type == EffectType.Active)
            .SelectMany(e => e.Contents.Select(c => HumanRuleOf(e, c)))
            .Where(r => r != null && kinds.Contains(r[0])).ToList();
        if (rules.Count != 1) throw new InvalidOperationException("変更対象の基本補正を一意に特定できません：" + string.Join(" / ", kinds));
        HumanSpike(skill, type, value, rules[0]);
    }

    private static bool ReadHumanSpike(SkillBody skill, EffectType type, string s)
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
            string[] rule = HumanRuleOf(temporary.Overrides[0], content);
            Require(rule != null);
            HumanSpike(skill, type, content.Parameters.Last(), rule); return true;
        }
        m = M(s, @"^この発動ロールによる目標値は(.+?)に変化する。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetActivationRollTarget, m.Groups[1].Value)); return true;
        }
        m = M(s, @"^このカウンター効果による\[([^\]]+)\]の達成値は×?(.+?)倍される。?$");
        if (m.Success) { HumanSpike(skill, type, m.Groups[2].Value, "RerollResult", m.Groups[1].Value); return true; }
        m = M(s, @"^このパッシブ効果による達成値が([+-][0-9]+)、被ダメージは×(.+?)倍に変化する。?$");
        if (m.Success)
        {
            HumanSpikeFor(skill, type, m.Groups[1].Value, "EarlyBattleActivation");
            HumanSpikeFor(skill, type, m.Groups[2].Value, "EarlyBattleDamage"); return true;
        }
        m = M(s, @"^このパッシブ効果による達成値上昇は×(.+?)倍に変化する。?$");
        if (m.Success) { HumanSpikeFor(skill, type, m.Groups[1].Value, "ActionStats", "WeaponActivation"); return true; }
        m = M(s, @"^この(パッシブ|アクティブ)効果による達成値(?:の)?上昇は([+-].+?)に変化する。?$");
        if (m.Success)
        {
            // The human document uses 上昇; the existing single-stat grammar uses 増加.
            var candidates = skill.Overrides.Where(e => e.Type == EffectType.Passive || e.Type == EffectType.Active)
                .SelectMany(e => e.Contents.Select(c => HumanRuleOf(e, c))).Where(r => r != null &&
                    new[] { "EnvironmentCheck", "CategoryCheck", "TimedWeaponActivation", "EarlyBattleActivation" }.Contains(r[0])).ToList();
            if (candidates.Count == 0 && m.Groups[1].Value == "パッシブ")
                return TryReadPassiveSpike(skill, type, "このパッシブ効果による達成値増加は" + m.Groups[2].Value + "に変化する。");
            HumanSpikeFor(skill, type, m.Groups[2].Value, m.Groups[1].Value == "アクティブ" ? new[] { "TimedWeaponActivation" } : new[] { "EnvironmentCheck", "CategoryCheck", "EarlyBattleActivation" }); return true;
        }
        m = M(s, @"^このパッシブ効果による目標値の減少は([+-].+?)に変化する。?$");
        if (m.Success) { HumanSpikeFor(skill, type, m.Groups[1].Value, "CraftCategories"); return true; }
        m = M(s, @"^この(?:パッシブ|カウンター)効果による消費決意は([+-].+?)に変化する。?$");
        if (m.Success)
        {
            // A counter heading here is accepted only when its unique target is the passive acquisition-cost modifier.
            HumanSpikeFor(skill, type, m.Groups[1].Value, "AcquisitionCost"); return true;
        }
        m = M(s, @"^このパッシブ効果による採取回数上限は([+-].+?)に変化する。?$");
        if (m.Success) { HumanSpikeFor(skill, type, m.Groups[1].Value, "DailyGathering"); return true; }
        m = M(s, @"^このパッシブ効果による([A-Z]+)最大値は×?(.+?)倍に変化する。?$");
        if (m.Success) { HumanSpike(skill, type, m.Groups[2].Value, "ResourceMaximum", m.Groups[1].Value); return true; }
        m = M(s, @"^このパッシブ効果による売値増加は×(.+?)倍に変化する。?$");
        if (m.Success) { HumanSpikeFor(skill, type, m.Groups[1].Value, "SalePrice"); return true; }
        m = M(s, @"^このパッシブ効果による威力とリソースの回復量上昇は×?(.+?)倍に変化する。?$");
        if (m.Success)
        {
            HumanSpikeFor(skill, type, m.Groups[1].Value, "CategoryPower");
            HumanSpikeFor(skill, type, m.Groups[1].Value, "CategoryHealing"); return true;
        }
        return false;
    }

    private static void ValidateHumanSkill(SkillBody skill)
    {
        foreach (var effect in skill.Effects)
        foreach (var content in effect.Contents)
        {
            if (content.Type == EffectContentType.RerollActivation)
            {
                Require(effect.Type == EffectType.Counter && effect.Triggers.Count == 0);
                Require(skill.DeclarationConditions.And.Any(c => c.Type == ConditionType.RollResult && c.Parameters.SequenceEqual(new[] { "Activation", "NormalFailure" })));
            }
            if (content.Type == EffectContentType.SetResourceValue || content.Type == EffectContentType.SkipRoll)
                Require(effect.Type == EffectType.Counter && effect.Triggers.Count == 0);
            if (content.Type == EffectContentType.SkipRoll)
                Require(skill.Effects.Where(e => e.Type == EffectType.Counter && e.Triggers.Count == 0).SelectMany(e => e.Contents).Count(c => c.Type == EffectContentType.SetResourceValue) == 1);
            if (content.Type == EffectContentType.LimitAcquisition || content.Type == EffectContentType.RaceAlias)
                Require(effect.Type == EffectType.Passive && effect.Triggers.Count == 0);
        }
        foreach (var effect in skill.Overrides)
        foreach (var content in effect.Contents)
        {
            if (content.Type == OverrideContentType.SetActivationRollTarget)
                Require(skill.Roll.Count > 0 && skill.Roll.Formula != "自動成功" && !string.IsNullOrWhiteSpace(skill.Roll.Target));
        }
    }
}
