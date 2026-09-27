using System;
using System.Linq;

public static partial class SkillTextConverter
{
    private static void ValidateExtendedSkill(SkillBody skill)
    {
        var stacks = skill.Effects.Where(e => (e.Type == EffectType.Active || e.Type == EffectType.Counter) && e.Triggers.Count == 0)
            .SelectMany(e => e.Contents).Where(c => c.Type == EffectContentType.GainStack).ToList();
        var stackOverrides = skill.Overrides.SelectMany(e => e.Contents).Where(c => c.Type == OverrideContentType.SetStackAmount).ToList();
        foreach (var change in stackOverrides)
        {
            string[] p = Args(change.Parameters, 3, "SetStackAmount");
            if (stacks.Count(c => c.Parameters[0] == p[0] && c.Parameters[1] == p[1]) != 1)
                throw new InvalidOperationException("スタック数変更には同じ対象・状態への無条件の付与を1件指定してください。");
        }
        if (stackOverrides.GroupBy(c => c.Parameters[0] + "\u001f" + c.Parameters[1]).Any(g => g.Count() > 1))
            throw new InvalidOperationException("スタック数変更が重複しています。");
        foreach (var effect in skill.Effects)
        foreach (var content in effect.Contents)
        {
            if (content.Type == EffectContentType.RoleplayDescription && (effect.Type != EffectType.Roleplay || effect.Triggers.Count != 0))
                throw new InvalidOperationException("説明文はロールプレイ効果にのみ使用できます。");
            if (content.Type == EffectContentType.ConsumeStacksForDamage)
            {
                if (effect.Type != EffectType.Active || effect.Triggers.Count != 0 ||
                    skill.Effects.Where(e => e.Type == EffectType.Active && e.Triggers.Count == 0).SelectMany(e => e.Contents)
                    .Count(c => (c.Type == EffectContentType.SkillAttack || c.Type == EffectContentType.WeaponAttack) && c.Parameters[0] == content.Parameters[0]) != 1)
                    throw new InvalidOperationException("スタック消費による被ダメージ補正には同じ対象のアクティブ攻撃を1件指定してください。");
            }
            if (content.Type == EffectContentType.RemoveSummon &&
                (!(skill is SummonedEntityDefinition) || !skill.Categories.Contains("罠") || effect.Type != EffectType.Declaration || !Matches(effect.Triggers, Trigger(TriggerTiming.AfterSkillResolution))))
                throw new InvalidOperationException("罠除去は罠の宣言効果・スキル処理後に指定してください。");
            if (content.Type == EffectContentType.ProhibitAcquisition && content.Parameters.Skip(2).Distinct(StringComparer.Ordinal).Count() != content.Parameters.Count - 2)
                throw new InvalidOperationException("習得不能スキルが重複しています。");
        }
    }
}
