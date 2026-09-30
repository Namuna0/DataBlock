using System;
using System.Collections.Generic;
using System.Linq;

public static partial class SkillTextConverter
{
    private static bool ReadBeastRaceState(StateDefinition state, string text)
    {
        var m = M(text, @"^〈([^〉]+)〉に対する自身の〈回避〉の達成値を(-[0-9]+)減少させる。?$");
        if (m.Success)
        {
            AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.EvasionResult, Condition(ConditionType.ActionCategory, m.Groups[1].Value))), Change(OverrideContentType.AddEvasionResult, m.Groups[2].Value)); return true;
        }
        if (M(text, @"^自身の被ダメージは×[0-9.]+倍されます。?$").Success)
            return ReadStateLine(state, text.Replace("されます", "される"));
        return false;
    }

    private static string BeastRaceStateOverrideText(List<TriggerDefinition> triggers, OverrideContent content)
    {
        if (content == null || content.Type != OverrideContentType.AddEvasionResult || triggers.Count != 1 || triggers[0].Conditions.And.Count != 1 || triggers[0].Conditions.And[0].Type != ConditionType.ActionCategory) return null;
        string category = Args(triggers[0].Conditions.And[0].Parameters, 1, "ActionCategory")[0];
        Require(Matches(triggers, Trigger(TriggerTiming.EvasionResult, Condition(ConditionType.ActionCategory, category))));
        string amount = Args(content.Parameters, 1, "AddEvasionResult")[0]; Require(M(amount, @"^-[0-9]+$").Success);
        return "〈" + category + "〉に対する自身の〈回避〉の達成値を" + amount + "減少させる。";
    }

    private static bool IsBeastRaceSpike(OverrideContentType type)
    {
        return type == OverrideContentType.SetAppliedStateDuration || type == OverrideContentType.SetStateDamageMultiplier || type == OverrideContentType.SetOnAppliedStateRecovery;
    }

    private static void CompleteBeastRaceData(SkillTextData data)
    {
        foreach (var skill in new[] { data.Skill }.Concat(data.Skill.Choices))
        {
            if (!skill.Effects.SelectMany(x => x.Contents).Any(x => x.Type == EffectContentType.DeclareSelectedSkill)) continue;
            Require(!skill.Effects.SelectMany(x => x.Contents).Any(x => x.Type == EffectContentType.SkillAttack || x.Type == EffectContentType.WeaponAttack));
            foreach (var content in skill.Overrides.Where(x => x.Type == EffectType.Critical && x.Triggers.Count == 0).SelectMany(x => x.Contents).Where(x => x.Type == OverrideContentType.MultiplyPower))
                content.Type = OverrideContentType.MultiplyDeclaredSkillPower;
        }
    }

    private static void ValidateBeastRaceSkill(SkillBody skill)
    {
        foreach (var effect in skill.Effects)
        foreach (var content in effect.Contents)
        {
            if ((int)content.Type < 63 || (int)content.Type > 69) continue;
            Require(effect.Triggers.Count == 0);
            EffectType expected = content.Type == EffectContentType.EnvironmentImmunity || content.Type == EffectContentType.RestoreOnAppliedState ? EffectType.Passive :
                content.Type == EffectContentType.GrantItem || content.Type == EffectContentType.SwapWeapon ? EffectType.Active : EffectType.Counter;
            Require(effect.Type == expected);
            if (content.Type == EffectContentType.ChooseCheckOutcome)
                Require(skill.DeclarationConditions.And.Count(x => x.Type == ConditionType.IncapacitatedCheckReaction) == 1);
            if (content.Type == EffectContentType.DeclareSelectedSkill)
            {
                Require(skill.DeclarationConditions.And.Count(x => x.Type == ConditionType.SelectEquipmentSkill && x.Parameters[0] == "1") == 1);
                Require(skill.DeclarationConditions.And.Count(x => x.Type == ConditionType.ActionTowardsSelf) == 1);
            }
            if (content.Type == EffectContentType.SwapWeapon)
                Require(skill.DeclarationConditions.And.Count(x => x.Type == ConditionType.SelectCarriedWeapon) == 1);
        }
        foreach (var effect in skill.Overrides)
        foreach (var content in effect.Contents)
        {
            if (content.Type == OverrideContentType.SetAppliedStateDuration)
                Require(skill.Effects.Where(x => x.Type == EffectType.Active && x.Triggers.Count == 0).SelectMany(x => x.Contents).Count(x => x.Type == EffectContentType.ApplyState && x.Parameters.Count == 3 && x.Parameters[0] == content.Parameters[0] && x.Parameters[1] == content.Parameters[1]) == 1);
            if (content.Type == OverrideContentType.SetOnAppliedStateRecovery)
                Require(skill.Effects.Where(x => x.Type == EffectType.Passive).SelectMany(x => x.Contents).Count(x => x.Type == EffectContentType.RestoreOnAppliedState && x.Parameters[4] == content.Parameters[0]) == 1);
            if (content.Type == OverrideContentType.MultiplyDeclaredSkillPower)
                Require(skill.Effects.Where(x => x.Type == EffectType.Counter && x.Triggers.Count == 0).SelectMany(x => x.Contents).Count(x => x.Type == EffectContentType.DeclareSelectedSkill) == 1);
        }
        foreach (var stage in new[] { EffectType.SecondSpike, EffectType.ThirdSpike })
            Require(!skill.Overrides.Where(x => x.Type == stage).SelectMany(x => x.Contents).Where(x => IsBeastRaceSpike(x.Type)).GroupBy(x => x.Type + ":" + x.Parameters[0]).Any(x => x.Count() > 1));
    }

    private static void ValidateBeastRaceData(SkillTextData data)
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
}
