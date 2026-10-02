using System.Linq;

public static partial class SkillTextConverter
{
    private static void ValidateGodSkill(SkillBody skill)
    {
        var attacks = skill.Effects.Where(e => e.Type == EffectType.Active && e.Triggers.Count == 0)
            .SelectMany(e => e.Contents).Where(c => c.Type == EffectContentType.SkillAttack || c.Type == EffectContentType.WeaponAttack).ToList();
        foreach (var effect in skill.Effects)
        foreach (var content in effect.Contents)
        {
            if (content.Type == EffectContentType.RemoveAllStacks)
                Require(attacks.Count == 1 && skill.Effects.TakeWhile(e => e != effect).SelectMany(e => e.Contents).Contains(attacks[0]));
            if (content.Type == EffectContentType.DeferMissingResourceCost)
            {
                Require(skill.Costs.Count(c => c.Kind == CostKind.Resource && c.Resource == content.Parameters[0] && c.Amount > 0) == 1);
                Require(skill.Effects.Any(e => e.Type == EffectType.Active));
            }
            if (content.Type == EffectContentType.SucceedFailedActivationRoll)
                Require(skill.DeclarationConditions.And.Any(c => c.Type == ConditionType.RollResult && c.Parameters.SequenceEqual(new[] { "Activation", "NormalFailure" })));
        }
        var costs = skill.Overrides.SelectMany(e => e.Contents).Where(c => c.Type == OverrideContentType.ThisSkillStackCost).ToList();
        Require(!costs.GroupBy(c => c.Parameters[1]).Any(g => g.Count() > 1));
        foreach (var cost in costs)
            Require(skill.Costs.Count(c => c.Kind == CostKind.Resource && c.Resource == cost.Parameters[1] && c.Amount >= Number(cost.Parameters[3], 1, "最低消費")) == 1);
        foreach (var content in skill.Overrides.SelectMany(e => e.Contents).Where(c => c.Type == OverrideContentType.ThisSkillStackPower))
            Require(attacks.Count == 1);
        foreach (var stage in new[] { EffectType.SecondSpike, EffectType.ThirdSpike })
            Require(skill.Overrides.Where(e => e.Type == stage).SelectMany(e => e.Contents).Count(c => c.Type == OverrideContentType.OptionalSpikeState) <= 1);
    }

    private static void ValidateGodData(SkillTextData data)
    {
        foreach (var skill in new[] { data.Skill }.Concat(data.Skill.Choices).Concat(data.Summons))
        foreach (var content in skill.Overrides.Where(e => e != null && e.Contents != null).SelectMany(e => e.Contents).Where(c => c != null && c.Type == OverrideContentType.OptionalSpikeState))
        {
            var p = Args(content.Parameters, 5, "OptionalSpikeState");
            Require(data.States.Count(s => s.Name == p[3]) == 1);
        }
    }
}
