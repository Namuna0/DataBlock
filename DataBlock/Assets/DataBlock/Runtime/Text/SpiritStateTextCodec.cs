using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static bool ReadSpiritState(StateDefinition state, string s)
    {
        Match m;
        if (s.TrimEnd('。') == "自身は指定されたスキルを宣言する事が出来なくなる")
        { AddStateEffect(state, NoTriggers(), Content(EffectContentType.ProhibitSelectedSkill, "Self", "SelectedSkill")); return true; }
        m = M(s, @"^自身が〈([^〉]+)〉かつ〈([^〉]+)〉を発動する際、対象の防御点は無視される。?$");
        if (m.Success)
        { AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(OverrideContentType.IgnoreDefenseForCategories, "Self", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^付与者は、この状態が付与されたキャラクターの代わりに行動を毎ターン([0-9]+)回まで宣言できる。?$");
        if (m.Success)
        { AddStateEffect(state, NoTriggers(), Content(EffectContentType.GrantActionControl, "Applier", "Self", "Turn", m.Groups[1].Value, "Unspecified")); return true; }
        if (s.TrimEnd('。') == "その行動中は味方キャラクターとして扱われる")
        {
            var control = state.Effects.SelectMany(x => x.Contents).LastOrDefault(x => x.Type == EffectContentType.GrantActionControl);
            Require(control != null); control.Parameters[4] = "AllyDuringAction"; return true;
        }
        m = M(s, @"^付与されてから([0-9]+)ターン後、対象を(.+?)の威力\+(.+?)の属性威力で攻撃する。?$");
        if (m.Success)
        { AddStateEffect(state, NoTriggers(), Content(EffectContentType.DelayedStateAttack, "Target", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, "Unspecified", "Unspecified")); return true; }
        if (s == "（対象選択と威力の算出は付与の際に行われる）" || s.TrimEnd('。') == "効果による攻撃後、この状態は解除される")
        {
            var attack = state.Effects.SelectMany(x => x.Contents).LastOrDefault(x => x.Type == EffectContentType.DelayedStateAttack);
            Require(attack != null);
            if (s.StartsWith("（", StringComparison.Ordinal)) attack.Parameters[4] = "SnapshotOnApply"; else attack.Parameters[5] = "RemoveAfterAttack";
            return true;
        }
        return false;
    }

    private static string SpiritStateContentText(List<TriggerDefinition> triggers, EffectContent content)
    {
        if (content == null) return null;
        if (content.Type != EffectContentType.ProhibitSelectedSkill && content.Type != EffectContentType.GrantActionControl && content.Type != EffectContentType.DelayedStateAttack) return null;
        Require(Matches(triggers, Trigger(TriggerTiming.Always)));
        string[] p;
        switch (content.Type)
        {
            case EffectContentType.ProhibitSelectedSkill:
                p = Args(content.Parameters, 2, "ProhibitSelectedSkill"); Require(p.SequenceEqual(new[] { "Self", "SelectedSkill" })); return "自身は指定されたスキルを宣言する事が出来なくなる。";
            case EffectContentType.GrantActionControl:
                p = Args(content.Parameters, 5, "GrantActionControl"); Require(p[0] == "Applier" && p[1] == "Self" && p[2] == "Turn" && p[4] == "AllyDuringAction"); Number(p[3], 1, "行動回数");
                return "付与者は、この状態が付与されたキャラクターの代わりに行動を毎ターン" + p[3] + "回まで宣言できる。\nその行動中は味方キャラクターとして扱われる。";
            case EffectContentType.DelayedStateAttack:
                p = Args(content.Parameters, 6, "DelayedStateAttack"); Require(p[0] == "Target" && p[4] == "SnapshotOnApply" && p[5] == "RemoveAfterAttack"); Number(p[1], 1, "遅延ターン");
                return "付与されてから" + p[1] + "ターン後、対象を" + p[2] + "の威力+" + p[3] + "の属性威力で攻撃する。\n（対象選択と威力の算出は付与の際に行われる）\n*効果による攻撃後、この状態は解除される。";
            default: return null;
        }
    }

    private static string SpiritStateOverrideText(List<TriggerDefinition> triggers, OverrideContent content)
    {
        if (content == null || content.Type != OverrideContentType.IgnoreDefenseForCategories) return null;
        var p = Args(content.Parameters, 3, "IgnoreDefenseForCategories"); Require(p[0] == "Self" && Matches(triggers, Trigger(TriggerTiming.Always)));
        return "自身が〈" + p[1] + "〉かつ〈" + p[2] + "〉を発動する際、対象の防御点は無視される。";
    }

    private static bool IsSpiritSpikeContent(OverrideContentType type)
    {
        return type == OverrideContentType.SetCooldown || type == OverrideContentType.SetStackAmount ||
            type == OverrideContentType.SetStateControlLimit || type == OverrideContentType.SetConditionalStatePower;
    }

    private static void ValidateSpiritSkill(SkillBody skill)
    {
        foreach (var effect in skill.Effects)
        foreach (var content in effect.Contents)
        {
            if ((int)content.Type >= 50 && (int)content.Type <= 62)
            {
                Require(effect.Triggers.Count == 0);
                bool active = content.Type == EffectContentType.LeaveBattle || content.Type == EffectContentType.ApplySelectedSkillState || content.Type == EffectContentType.ResourceDamage || content.Type == EffectContentType.RestoreFromResourceDamage;
                Require(effect.Type == (active ? EffectType.Active : EffectType.Passive));
                Require(content.Type != EffectContentType.GrantActionControl && content.Type != EffectContentType.DelayedStateAttack && content.Type != EffectContentType.ProhibitSelectedSkill);
            }
            if (content.Type == EffectContentType.ApplySelectedSkillState)
                Require(skill.DeclarationConditions.And.Count(x => x.Type == ConditionType.SelectCharacterSkill) == 1);
            if (content.Type == EffectContentType.RestoreFromResourceDamage)
                Require(skill.Effects.Where(x => x.Type == effect.Type).SelectMany(x => x.Contents).Count(x => x.Type == EffectContentType.ResourceDamage && x.Parameters[1] == content.Parameters[1]) == 1);
        }
        foreach (var effect in skill.Overrides)
        foreach (var content in effect.Contents)
        {
            if (content.Type == OverrideContentType.SetConditionalStatePower)
                Require(skill.Overrides.Where(x => x.Type == EffectType.Passive).SelectMany(x => x.Contents).Count(x => x.Type == OverrideContentType.ConditionalStatePower) == 1);
            if (content.Type == OverrideContentType.SetCooldown && SpiritSpike(effect.Type)) Require(skill.CooldownTurns > 0);
            if (content.Type == OverrideContentType.MultiplyRecovery && effect.Type == EffectType.Critical && effect.Triggers.Count == 0)
                Require(skill.Effects.Where(x => x.Type == EffectType.Active).SelectMany(x => x.Contents).Count(x => x.Type == EffectContentType.RestoreResource) == 1);
        }
        foreach (var stage in new[] { EffectType.SecondSpike, EffectType.ThirdSpike })
            Require(!skill.Overrides.Where(x => x.Type == stage).SelectMany(x => x.Contents).Where(x => IsSpiritSpikeContent(x.Type)).GroupBy(x => x.Type).Any(x => x.Count() > 1));
    }

    private static void ValidateSpiritData(SkillTextData data)
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

    private static void CompleteSpiritData(SkillTextData data)
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
