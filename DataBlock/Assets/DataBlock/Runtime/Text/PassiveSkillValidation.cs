using System;
using System.Collections.Generic;
using System.Linq;

public static partial class SkillTextConverter
{
    private static void ValidatePassiveExtensions(SkillBody skill)
    {
        var gains = skill.Effects.Where(x => x.Type == EffectType.Passive)
            .SelectMany(x => x.Contents.Select(c => new { Definition = x, Content = c }))
            .Where(x => x.Content.Type == EffectContentType.GainMappedStateStacks).ToList();
        var costs = skill.Overrides.Where(x => x.Type == EffectType.Passive)
            .SelectMany(x => x.Contents.Select(c => new { Definition = x, Content = c }))
            .Where(x => x.Content.Type == OverrideContentType.ReduceResourceCostPerMappedStacks).ToList();
        var restores = skill.Effects.Where(x => IsMappedStackRestore(x, null)).ToList();
        if (gains.Count + costs.Count + restores.Count == 0) return;

        foreach (var gain in gains) TryPassiveContentText(gain.Content, out _);
        string[] rules = gains.Select(x => VariableArgs(x.Content.Parameters, 6, "GainMappedStateStacks")[3]).ToArray();
        if (rules.Distinct(StringComparer.Ordinal).Count() != rules.Length) throw new InvalidOperationException("属性スタック参照名が重複しています。");
        if (gains.Count != costs.Count || costs.Count != restores.Count) throw new InvalidOperationException("属性スタックには取得・消費軽減・回復を1件ずつ指定してください。");
        string[] costRules = costs.Select(x => Args(x.Content.Parameters, 6, "ReduceResourceCostPerMappedStacks")[2]).ToArray();
        string[] restoreRules = restores.Select(x => Args(x.Triggers[0].Conditions.And[0].Parameters, 4, "MappedStateStackInterval")[1]).ToArray();
        if (costRules.Distinct(StringComparer.Ordinal).Count() != costRules.Length ||
            restoreRules.Distinct(StringComparer.Ordinal).Count() != restoreRules.Length ||
            !new HashSet<string>(rules, StringComparer.Ordinal).SetEquals(costRules) ||
            !new HashSet<string>(rules, StringComparer.Ordinal).SetEquals(restoreRules))
            throw new InvalidOperationException("属性スタックの取得・消費軽減・回復は参照名ごとに1件ずつ指定してください。");

        foreach (var cost in costs)
        {
            string[] p = Args(cost.Content.Parameters, 6, "ReduceResourceCostPerMappedStacks");
            string category;
            if (p[0] != "Self" || p[5] != OneAttributePolicy || !TryGetCategoryTrigger(cost.Definition.Triggers, TriggerTiming.ResourceCost, out category))
                throw new InvalidOperationException("属性スタック消費軽減の対象・参照方法・トリガーが不正です。");
            var matchingGains = gains.Where(x => x.Content.Parameters[3] == p[2]).ToList();
            if (matchingGains.Count != 1) throw new InvalidOperationException("消費軽減が参照する属性スタック取得を1件指定してください：" + p[2]);
            string gainCategory;
            if (!TryGetCategoryTrigger(matchingGains[0].Definition.Triggers, TriggerTiming.ActionActivated, out gainCategory) || gainCategory != category)
                throw new InvalidOperationException("属性スタック取得と消費軽減の行動カテゴリーが一致しません。");
            var matchingRestores = restores.Where(x => IsMappedStackRestore(x, p[2])).ToList();
            if (matchingRestores.Count != 1) throw new InvalidOperationException("消費軽減が参照するスタック回復を1件指定してください：" + p[2]);
            string[] interval = Args(matchingRestores[0].Triggers[0].Conditions.And[0].Parameters, 4, "MappedStateStackInterval");
            if (interval[0] != "Self" || interval[2] != p[3] || interval[3] != OneAttributePolicy)
                throw new InvalidOperationException("消費軽減と回復のスタック間隔または参照方法が一致しません。");
            string[] recovery = Args(matchingRestores[0].Contents[0].Parameters, 3, "RestoreResource");
            if (recovery[0] != "Self") throw new InvalidOperationException("属性スタックによる回復対象はSelfです。");
            Number(recovery[2], 1, "回復量");
        }
    }

    private static string SetModifierKey(OverrideContent content)
    {
        string[] p = VariableArgs(content.Parameters, 4, "SetModifier");
        return string.Join("\u001f", p.Take(p.Length - 1));
    }

    private static void ValidateSetModifierTarget(SkillBody skill, OverrideContent content)
    {
        string[] p = VariableArgs(content.Parameters, 4, "SetModifier");
        int count = 0;
        switch (p[0])
        {
            case PowerModifier:
                if (p.Length != 4 || p[1] != "ActionCategory") throw new InvalidOperationException("MultiplyPower置換の指定が不正です。");
                count = skill.Overrides.Where(x =>
                    {
                        string category;
                        return x.Type == EffectType.Passive && TryGetCategoryTrigger(x.Triggers, TriggerTiming.AttackPower, out category) && category == p[2];
                    })
                    .Sum(x => x.Contents.Count(c => c.Type == OverrideContentType.MultiplyPower));
                break;
            case ResourceCostModifier:
                if (p.Length != 5 || p[1] != "ActionCategory") throw new InvalidOperationException("MultiplyResourceCost置換の指定が不正です。");
                count = skill.Overrides.Where(x =>
                    {
                        string category;
                        return x.Type == EffectType.Passive && TryGetCategoryTrigger(x.Triggers, TriggerTiming.ResourceCost, out category) && category == p[2];
                    })
                    .Sum(x => x.Contents.Count(c => c.Type == OverrideContentType.MultiplyResourceCost &&
                        c.Parameters != null && c.Parameters.Count == 3 && c.Parameters[0] == "Self" && c.Parameters[1] == p[3]));
                break;
            case StatModifier:
                if (p.Length != 5) throw new InvalidOperationException("ModifyStat置換の指定が不正です。");
                count = skill.Overrides.Where(x => x.Type == EffectType.Passive).SelectMany(x => x.Contents)
                    .Count(x => x.Type == OverrideContentType.ModifyStat && x.Parameters != null && x.Parameters.Count == 4 && x.Parameters.Take(3).SequenceEqual(p.Skip(1).Take(3)));
                break;
            case ActionResultModifier:
                if (p.Length != 4 || p[1] != "ActionStat") throw new InvalidOperationException("AddActionResult置換の指定が不正です。");
                count = skill.Overrides.Where(x =>
                    {
                        string stat;
                        return x.Type == EffectType.Passive && TryGetActionStatTrigger(x.Triggers, out stat) && stat == p[2];
                    })
                    .Sum(x => x.Contents.Count(c => c.Type == OverrideContentType.AddActionResult));
                break;
            default:
                throw new InvalidOperationException("未対応の補正置換対象です：" + p[0]);
        }
        if (count != 1) throw new InvalidOperationException("補正置換の対象となる基本補正を1件指定してください：" + p[0]);
    }
}
