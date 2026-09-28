using System;
using System.Linq;

public static partial class SkillTextConverter
{
    private static void ValidateMagicSkill(SkillBody skill)
    {
        foreach (var effect in skill.Effects)
        foreach (var c in effect.Contents)
        {
            if (c.Type == EffectContentType.TransformAtStacks)
            {
                Require(effect.Type == EffectType.Active && effect.Triggers.Count == 0);
                Require(skill.Effects.TakeWhile(e => e != effect).SelectMany(e => e.Contents).Count(x => x.Type == EffectContentType.GainStack && x.Parameters[0] == "Self" && x.Parameters[1] == c.Parameters[1]) == 1);
            }
            if (c.Type == EffectContentType.OutsiderRule || c.Type == EffectContentType.CyclingStateStacks || c.Type == EffectContentType.RerollGathering || c.Type == EffectContentType.EquipSlotSubstitution || c.Type == EffectContentType.PreventMealPenalties || c.Type == EffectContentType.GrantCreationChoice || c.Type == EffectContentType.GrantRaceTrait || c.Type == EffectContentType.OptionalInvalidateAction)
                Require(effect.Type == EffectType.Passive && effect.Triggers.Count == 0);
            if (c.Type == EffectContentType.DrainResource || c.Type == EffectContentType.CreateMeleeGroup)
                Require(effect.Type == EffectType.Active && effect.Triggers.Count == 0);
            if (c.Type == EffectContentType.InvalidateTriggeredEffect && c.Parameters.SequenceEqual(new[] { "Target", "Counter" }))
                Require(effect.Type == EffectType.Counter && skill.DeclarationConditions.And.Any(x => x.Type == ConditionType.CounterToOwnActive));
        }
        foreach (var effect in skill.Overrides)
        foreach (var c in effect.Contents)
        {
            if (c.Type == OverrideContentType.MultiplyDamageReduction)
                Require(skill.Effects.Where(e => e.Type == EffectType.Counter && e.Triggers.Count == 0).SelectMany(e => e.Contents).Count(x => x.Type == EffectContentType.ReduceDamage) == 1);
            if (c.Type == OverrideContentType.SetRollRange && effect.Type != EffectType.Passive)
                Require(skill.Overrides.Where(e => e.Type == EffectType.Passive).SelectMany(e => e.Contents).Count(x => x.Type == OverrideContentType.SetRollRange && x.Parameters[1] == c.Parameters[1]) == 1);
            if (c.Type == OverrideContentType.StateTurnRecovery)
                Require(skill.Effects.SelectMany(e => e.Contents).Any(x => x.Type == EffectContentType.TransformAtStacks && x.Parameters[6] == c.Parameters[0]));
        }
        foreach (var stage in new[] { EffectType.Passive, EffectType.SecondSpike, EffectType.ThirdSpike })
        {
            var special = skill.Overrides.Where(e => e.Type == stage).SelectMany(e => e.Contents)
                .Where(c => c.Type == OverrideContentType.SetRollRange || c.Type == OverrideContentType.StateTurnRecovery);
            Require(!special.GroupBy(c => c.Type + ":" + (c.Type == OverrideContentType.SetRollRange ? c.Parameters[1] : c.Parameters[0] + ":" + c.Parameters[1])).Any(g => g.Count() > 1));
        }
    }

    private static void ValidateMagicStates(SkillTextData data)
    {
        foreach (var state in data.States)
        foreach (var content in state.Effects.SelectMany(e => e.Contents).Where(c => c.Type == EffectContentType.UseStateDefinitionAtStacks))
        {
            var p = Args(content.Parameters, 5, "UseStateDefinitionAtStacks");
            var target = data.States.SingleOrDefault(s => s.Name == p[2]);
            Require(target != null && target != state);
            Require(!target.Effects.SelectMany(e => e.Contents).Any(c => c.Type == EffectContentType.UseStateDefinitionAtStacks));
        }
        foreach (var content in data.Skill.Effects.SelectMany(e => e.Contents).Where(c => c.Type == EffectContentType.TransformAtStacks))
            Require(data.States.Any(s => s.Name == content.Parameters[6]));
    }
}
