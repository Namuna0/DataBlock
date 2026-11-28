using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static void MergeChoiceStateDefinitions(SkillTextData data)
    {
        if (!data.States.GroupBy(x => x.Name).Any(x => x.Count() > 1)) return;
        var states = new List<StateDefinition>();
        foreach (StateDefinition state in data.States)
        {
            int index = states.FindIndex(x => x.Name == state.Name);
            if (index < 0) { states.Add(state); continue; }
            StateDefinition prior = states[index];
            if (!new HashSet<string>(prior.Categories, StringComparer.Ordinal).SetEquals(state.Categories))
                throw new InvalidOperationException("選択肢の状態カテゴリーが一致しません：" + state.Name);
            var priorTexts = StateTexts(prior);
            var texts = StateTexts(state);
            // A repeated reminder can omit details, but cannot alter or combine
            // conflicting definitions. Keep one whole, most complete definition.
            if (ContainsStateTexts(priorTexts, texts)) continue;
            if (ContainsStateTexts(texts, priorTexts)) { states[index] = state; continue; }
            throw new InvalidOperationException("選択肢の状態効果が一致しません：" + state.Name);
        }
        data.States = states;
    }

    private static bool ContainsStateTexts(List<string> definition, List<string> reminder)
    {
        return reminder.GroupBy(x => x).All(x => definition.Count(y => y == x.Key) >= x.Count());
    }

    private static void CompleteSelectedSkillModifiers(SkillTextData data)
    {
        foreach (var skill in new[] { data.Skill }.Concat(data.Skill.Choices))
        {
            if (!skill.Effects.SelectMany(x => x.Contents).Any(x => x.Type == EffectContentType.DeclareSelectedSkill)) continue;
            Require(!skill.Effects.SelectMany(x => x.Contents).Any(x => x.Type == EffectContentType.SkillAttack || x.Type == EffectContentType.WeaponAttack));
            foreach (var content in skill.Overrides.Where(x => x.Type == EffectType.Critical && x.Triggers.Count == 0).SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.MultiplyPower))
                content.Type = EffectContentType.MultiplyDeclaredSkillPower;
        }
    }

    private static void ValidateAppliedStateModifiers(SkillTextData data)
    {
        foreach (var skill in new[] { data.Skill }.Concat(data.Skill.Choices))
        foreach (var change in skill.Overrides.SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.SetStateDamageMultiplier))
        {
            var p = Args(change.Parameters, 3, "SetStateDamageMultiplier");
            StateDefinition state = data.States.SingleOrDefault(x => x.Name == p[0]);
            Require(state != null && state.Overrides.Count == 1 && state.Overrides[0].Contents.Count == 1);
            var value = state.Overrides[0].Contents[0];
            Require(value.Type == EffectContentType.MultiplyDamageTaken && Args(value.Parameters, 2, "MultiplyDamageTaken")[0] == p[1]);
            Require(Matches(StateTriggers(state, state.Overrides[0].Triggers), Trigger(TriggerTiming.IncomingDamage)));
            Require(skill.Effects.SelectMany(x => x.Contents).Any(x => x.Type == EffectContentType.ApplyState && x.Parameters[1] == p[0]));
        }
    }

    private static void CompleteAreaProgressModifiers(SkillTextData data)
    {
        foreach (var skill in new[] { data.Skill }.Concat(data.Skill.Choices).Concat(data.Summons))
        foreach (var content in skill.Overrides.SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.OptionalAreaProgressReduction))
        {
            if (content.Parameters.Count == 4 && content.Parameters[1] == "Unspecified" && content.Parameters[2] == "Unspecified")
            { content.Parameters[1] = "Any"; content.Parameters[2] = content.Parameters[0]; }
        }
    }

    private static void ValidateOptionalSpikeStateReferences(SkillTextData data)
    {
        foreach (var skill in new[] { data.Skill }.Concat(data.Skill.Choices).Concat(data.Summons))
        foreach (var content in skill.Overrides.Where(e => e != null && e.Contents != null).SelectMany(e => e.Contents).Where(c => c != null && c.Type == EffectContentType.OptionalSpikeState))
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
            foreach (var change in skill.Overrides.SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.SetDelayedStateSkillValue))
                Require(data.States.Any(s => s.Name == change.Parameters[0] && s.Effects.SelectMany(e => e.Contents).Any(c => c.Type == EffectContentType.ReceiveDelayedEnemyTurnAttack)) &&
                    skill.Effects.SelectMany(e => e.Contents).Any(c => c.Type == EffectContentType.ApplyState && c.Parameters[1] == change.Parameters[0]));
            foreach (var content in skill.Overrides.SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.MultiplyStateAttackPower))
            {
                var p = Args(content.Parameters, 2, "MultiplyStateAttackPower");
                Require(data.States.Any(x => x.Name == p[0] && x.Effects.SelectMany(e => e.Contents).Any(c => c.Type == EffectContentType.DelayedStateAttack)));
                Require(skill.Effects.SelectMany(x => x.Contents).Any(x => x.Type == EffectContentType.ApplyState && x.Parameters[1] == p[0]));
            }
            foreach (var content in skill.Effects.SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.ApplySelectedSkillState))
                Require(data.States.Any(x => x.Name == content.Parameters[1] && x.Effects.SelectMany(e => e.Contents).Any(c => c.Type == EffectContentType.ProhibitSelectedSkill)));
            foreach (var content in skill.Overrides.SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.SetStateControlLimit))
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
            var delayed = skill.Effects.Where(e => e.Type == EffectType.Active).SelectMany(e => e.Contents)
                .Where(c => c.Type == EffectContentType.ApplyState).Select(c => c.Parameters[1])
                .Where(n => data.States.Any(s => s.Name == n && s.Effects.SelectMany(e => e.Contents).Any(c => c.Type == EffectContentType.ReceiveDelayedEnemyTurnAttack))).Distinct().ToList();
            if (delayed.Count == 1 && !skill.Effects.SelectMany(e => e.Contents).Any(HasRuleSkillValue))
                foreach (var spike in skill.Overrides.Where(o => IsSpikeStage(o.Type) && o.Triggers.Count == 0).SelectMany(o => o.Contents).Where(c => c.Type == EffectContentType.SetSkillValue))
                {
                    spike.Type = EffectContentType.SetDelayedStateSkillValue;
                    spike.Parameters.Insert(0, delayed[0]);
                }
            var names = skill.Effects.Where(e => e.Type == EffectType.Active).SelectMany(e => e.Contents)
                .Where(c => c.Type == EffectContentType.ApplyState && data.States.Any(s => s.Name == c.Parameters[1] && s.Effects.SelectMany(e => e.Contents).Any(x => x.Type == EffectContentType.DelayedStateAttack)))
                .Select(c => c.Parameters[1]).ToList();
            if (names.Count == 0) continue;
            foreach (var content in skill.Overrides.Where(x => x.Type == EffectType.Critical && x.Triggers.Count == 0).SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.MultiplyPower))
            {
                Require(names.Count == 1 && !skill.Effects.SelectMany(x => x.Contents).Any(x => x.Type == EffectContentType.SkillAttack || x.Type == EffectContentType.WeaponAttack));
                string factor = Args(content.Parameters, 1, "MultiplyPower")[0];
                content.Type = EffectContentType.MultiplyStateAttackPower;
                content.Parameters = new List<string> { names[0], factor };
            }
        }
    }

}
