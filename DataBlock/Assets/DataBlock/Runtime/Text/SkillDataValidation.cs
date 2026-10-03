using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static void CompleteSelectedSkillModifiers(SkillTextData data)
    {
        foreach (var skill in new[] { data.Skill }.Concat(data.Skill.Choices))
        {
            if (!skill.Effects.SelectMany(x => x.Contents).Any(x => x.Type == EffectContentType.DeclareSelectedSkill)) continue;
            Require(!skill.Effects.SelectMany(x => x.Contents).Any(x => x.Type == EffectContentType.SkillAttack || x.Type == EffectContentType.WeaponAttack));
            foreach (var content in skill.Overrides.Where(x => x.Type == EffectType.Critical && x.Triggers.Count == 0).SelectMany(x => x.Contents).Where(x => x.Type == OverrideContentType.MultiplyPower))
                content.Type = OverrideContentType.MultiplyDeclaredSkillPower;
        }
    }

    private static void ValidateAppliedStateModifiers(SkillTextData data)
    {
        foreach (var skill in new[] { data.Skill }.Concat(data.Skill.Choices))
        foreach (var change in skill.Overrides.SelectMany(x => x.Contents).Where(x => x.Type == OverrideContentType.SetStateDamageMultiplier))
        {
            var p = Args(change.Parameters, 3, "SetStateDamageMultiplier");
            StateDefinition state = data.States.SingleOrDefault(x => x.Name == p[0]);
            Require(state != null && state.Overrides.Count == 1 && state.Overrides[0].Contents.Count == 1);
            var value = state.Overrides[0].Contents[0];
            Require(value.Type == OverrideContentType.MultiplyDamageTaken && Args(value.Parameters, 2, "MultiplyDamageTaken")[0] == p[1]);
            Require(Matches(StateTriggers(state, state.Overrides[0].Triggers), Trigger(TriggerTiming.IncomingDamage)));
            Require(skill.Effects.SelectMany(x => x.Contents).Any(x => x.Type == EffectContentType.ApplyState && x.Parameters[1] == p[0]));
        }
    }

    private static void CompleteAreaProgressModifiers(SkillTextData data)
    {
        foreach (var skill in new[] { data.Skill }.Concat(data.Skill.Choices).Concat(data.Summons))
        foreach (var content in skill.Overrides.SelectMany(x => x.Contents).Where(x => x.Type == OverrideContentType.OptionalAreaProgressReduction))
        {
            if (content.Parameters.Count == 4 && content.Parameters[1] == "Unspecified" && content.Parameters[2] == "Unspecified")
            { content.Parameters[1] = "Any"; content.Parameters[2] = content.Parameters[0]; }
        }
    }

    private static void ValidateOptionalSpikeStateReferences(SkillTextData data)
    {
        foreach (var skill in new[] { data.Skill }.Concat(data.Skill.Choices).Concat(data.Summons))
        foreach (var content in skill.Overrides.Where(e => e != null && e.Contents != null).SelectMany(e => e.Contents).Where(c => c != null && c.Type == OverrideContentType.OptionalSpikeState))
        {
            var p = Args(content.Parameters, 5, "OptionalSpikeState");
            Require(data.States.Count(s => s.Name == p[3]) == 1);
        }
    }

    private static void ValidateStackStateReferences(SkillTextData data)
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

    private static void CompleteStackStateReferences(SkillTextData data)
    {
        for (int i = 0; i < data.States.Count; i++)
        foreach (var content in data.States[i].Effects.SelectMany(e => e.Contents).Where(c => c.Type == EffectContentType.UseStateDefinitionAtStacks))
        {
            if (content.Parameters[2] != "NextDefinition") continue;
            Require(i + 1 < data.States.Count); content.Parameters[2] = data.States[i + 1].Name;
        }
    }

    private static void ValidateStateSkillReferences(SkillTextData data)
    {
        foreach (var skill in new[] { data.Skill }.Concat(data.Skill.Choices))
        {
            foreach (var content in skill.Overrides.SelectMany(x => x.Contents).Where(x => x.Type == OverrideContentType.MultiplyStateAttackPower))
            {
                var p = Args(content.Parameters, 2, "MultiplyStateAttackPower");
                Require(data.States.Any(x => x.Name == p[0] && x.Effects.SelectMany(e => e.Contents).Any(c => c.Type == EffectContentType.DelayedStateAttack)));
                Require(skill.Effects.SelectMany(x => x.Contents).Any(x => x.Type == EffectContentType.ApplyState && x.Parameters[1] == p[0]));
            }
            foreach (var content in skill.Effects.SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.ApplySelectedSkillState))
                Require(data.States.Any(x => x.Name == content.Parameters[1] && x.Effects.SelectMany(e => e.Contents).Any(c => c.Type == EffectContentType.ProhibitSelectedSkill)));
            foreach (var content in skill.Overrides.SelectMany(x => x.Contents).Where(x => x.Type == OverrideContentType.SetStateControlLimit))
            {
                Require(data.States.Any(x => x.Name == content.Parameters[0] && x.Effects.SelectMany(e => e.Contents).Count(c => c.Type == EffectContentType.GrantActionControl) == 1));
                Require(skill.Effects.SelectMany(x => x.Contents).Any(x => x.Type == EffectContentType.ApplyState && x.Parameters[1] == content.Parameters[0]));
            }
        }
        foreach (var state in data.States.Where(x => x.Effects.SelectMany(e => e.Contents).Any(c => c.Type == EffectContentType.DelayedStateAttack)))
            Require(new[] { data.Skill }.Concat(data.Skill.Choices).Any(s => s.Effects.SelectMany(e => e.Contents).Any(c => c.Type == EffectContentType.ApplyState && c.Parameters[0] == "Self" && c.Parameters[1] == state.Name)));
    }

    private static void CompleteDelayedAttackModifiers(SkillTextData data)
    {
        foreach (var skill in new[] { data.Skill }.Concat(data.Skill.Choices))
        {
            var names = skill.Effects.Where(e => e.Type == EffectType.Active).SelectMany(e => e.Contents)
                .Where(c => c.Type == EffectContentType.ApplyState && data.States.Any(s => s.Name == c.Parameters[1] && s.Effects.SelectMany(e => e.Contents).Any(x => x.Type == EffectContentType.DelayedStateAttack)))
                .Select(c => c.Parameters[1]).ToList();
            if (names.Count == 0) continue;
            foreach (var content in skill.Overrides.Where(x => x.Type == EffectType.Critical && x.Triggers.Count == 0).SelectMany(x => x.Contents).Where(x => x.Type == OverrideContentType.MultiplyPower))
            {
                Require(names.Count == 1 && !skill.Effects.SelectMany(x => x.Contents).Any(x => x.Type == EffectContentType.SkillAttack || x.Type == EffectContentType.WeaponAttack));
                string factor = Args(content.Parameters, 1, "MultiplyPower")[0];
                content.Type = OverrideContentType.MultiplyStateAttackPower;
                content.Parameters = new List<string> { names[0], factor };
            }
        }
    }

}
