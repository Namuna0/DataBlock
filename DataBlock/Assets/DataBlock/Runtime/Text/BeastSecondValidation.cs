using System;
using System.Linq;

public static partial class SkillTextConverter
{
    private static void ValidateBeastSecondSkill(SkillBody skill)
    {
        var attacks = skill.Effects.Where(e => e.Type == EffectType.Active && e.Triggers.Count == 0)
            .SelectMany(e => e.Contents).Where(c => c.Type == EffectContentType.SkillAttack || c.Type == EffectContentType.WeaponAttack).ToList();
        foreach (var effect in skill.Effects)
        foreach (var content in effect.Contents)
        {
            var rule = BeastSecondRules.FirstOrDefault(r => !r.State && r.Content != EffectContentType.None && r.Content == content.Type);
            if (rule != null) Require(effect.Type == rule.Section && effect.Triggers.Count == 0);
            bool hit = effect.Triggers.Any(t => t.Timing == TriggerTiming.AttackHit);
            bool branch = content.Type == EffectContentType.OwnAttributeHitState || content.Type == EffectContentType.OwnAttributeHitRemoval;
            if (hit || branch || content.Type == EffectContentType.ExecuteAfterAttackDamage || content.Type == EffectContentType.MissingOwnAttributePower)
            {
                Require(attacks.Count == 1 && attacks[0].Parameters[0] == "Target");
                Require(skill.Effects.TakeWhile(e => e != effect).SelectMany(e => e.Contents).Contains(attacks[0]));
                if (hit) Require(content.Type == EffectContentType.ApplyState && content.Parameters[0] == "Target");
            }
            if (content.Type == EffectContentType.MissingOwnAttributePower)
                Require(attacks[0].Type == EffectContentType.SkillAttack && attacks[0].Parameters.Count == 4 && attacks[0].Parameters[1] == "PowerAndAttribute" && attacks[0].Parameters[2] == content.Parameters[0]);
        }
        var branches = skill.Effects.SelectMany(e => e.Contents).Where(c => c.Type == EffectContentType.OwnAttributeHitState || c.Type == EffectContentType.OwnAttributeHitRemoval).ToList();
        Require(!branches.GroupBy(c => c.Parameters[0]).Any(g => g.Count() > 1));
        foreach (var content in skill.Overrides.SelectMany(e => e.Contents))
        {
            if (content.Type == OverrideContentType.ThisSkillCriticalRange || content.Type == OverrideContentType.CriticalThresholdDelta)
                Require(skill.Roll.Count > 0 && skill.Roll.Formula != "自動成功");
            if (content.Type == OverrideContentType.OwnAttributeBonus || content.Type == OverrideContentType.OwnAttributeWeaknessMultiplier)
                Require(skill.Effects.SelectMany(e => e.Contents).Count(c => c.Type == EffectContentType.DefineOwnAttribute) == 1);
        }
        if (skill.DeclarationConditions.And.Any(c => c.Type == ConditionType.AutomaticEnemyAction))
            Require(skill.DeclarationConditions.And.Count == 1 && skill.DeclarationConditions.Or.Count == 0);
    }
}
