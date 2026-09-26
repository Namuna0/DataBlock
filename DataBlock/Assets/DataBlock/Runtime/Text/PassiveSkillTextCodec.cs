using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// 種族・職業を問わず使える常時補正、資源閾値、属性別スタックの文型。
public static partial class SkillTextConverter
{
    private const string Unspecified = "Unspecified";
    private const string EachAttributePolicy = "Each";
    private const string OneAttributePolicy = "One";
    private const string PowerModifier = "MultiplyPower";
    private const string ResourceCostModifier = "MultiplyResourceCost";
    private const string StatModifier = "ModifyStat";
    private const string ActionResultModifier = "AddActionResult";

    private static List<TriggerDefinition> CategoryPowerTriggers(string category)
    {
        return On(Trigger(TriggerTiming.AttackPower,
            Condition(ConditionType.ActionSource, "Self"),
            Condition(ConditionType.ActionCategory, category)));
    }

    private static List<TriggerDefinition> CategoryResourceCostTriggers(string category)
    {
        return On(Trigger(TriggerTiming.ResourceCost,
            Condition(ConditionType.ActionSource, "Self"),
            Condition(ConditionType.ActionCategory, category)));
    }

    private static List<TriggerDefinition> CategoryActivationTriggers(string category)
    {
        return On(Trigger(TriggerTiming.ActionActivated,
            Condition(ConditionType.ActionSource, "Self"),
            Condition(ConditionType.ActionCategory, category)));
    }

    private static List<TriggerDefinition> ActionResultTriggers(string stat)
    {
        return On(Trigger(TriggerTiming.ActionResult,
            Condition(ConditionType.ActionSource, "Self"),
            Condition(ConditionType.ActionStat, stat)));
    }

    private static bool TryReadPassiveSpike(SkillBody skill, EffectType type, string body)
    {
        if (type != EffectType.SecondSpike && type != EffectType.ThirdSpike) return false;

        Match m = M(body, @"^このパッシブ効果による威力上昇は(.+?)倍[、,]\s*(?:〈([^〉]+)〉|([^〈〉]+?))のマナ消費が×(.+?)倍に変化する。?$");
        if (m.Success)
        {
            string category = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value.Trim();
            AddOverride(skill.Overrides, type, NoTriggers(),
                Change(OverrideContentType.SetModifier, PowerModifier, "ActionCategory", category, m.Groups[1].Value),
                Change(OverrideContentType.SetModifier, ResourceCostModifier, "ActionCategory", category, "MP", m.Groups[4].Value));
            return true;
        }

        m = M(body, @"^このパッシブ効果による〈([^〉]+)〉の威力倍率は(.+?)倍に変化する。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetModifier,
                PowerModifier, "ActionCategory", m.Groups[1].Value, m.Groups[2].Value));
            return true;
        }

        m = M(body, @"^このパッシブ効果による〈([^〉]+)〉のマナ消費倍率は×?(.+?)倍に変化する。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetModifier,
                ResourceCostModifier, "ActionCategory", m.Groups[1].Value, "MP", m.Groups[2].Value));
            return true;
        }

        m = M(body, @"^このパッシブ効果による(?:\[([^\]]+)\]の)?達成値増加は([+-].+?)に変化する。?$");
        if (m.Success)
        {
            string stat = m.Groups[1].Success ? m.Groups[1].Value : SingleActionResultStat(skill);
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetModifier,
                ActionResultModifier, "ActionStat", stat, Signed(m.Groups[2].Value)));
            return true;
        }

        m = M(body, @"^このパッシブ効果による(?:(自身|対象|付与者)の)?(.+?)増加は([+-].+?)に変化する。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetModifier,
                StatModifier, m.Groups[1].Success ? ActorKey(m.Groups[1].Value) : "Self",
                m.Groups[2].Value, "Add", Signed(m.Groups[3].Value)));
            return true;
        }
        return false;
    }

    private static bool TryReadPassiveSkillLine(SkillBody skill, EffectType type, string body)
    {
        if (type != EffectType.Passive) return false;

        Match m = M(body, @"^自身が〈([^〉]+)〉を発動するたびに属性に応じた状態を([0-9]+)スタック得る。?$");
        if (m.Success)
        {
            Number(m.Groups[2].Value, 1, "獲得スタック数");
            skill.Effects.Add(new EffectDefinition
            {
                Type = type,
                Triggers = CategoryActivationTriggers(m.Groups[1].Value),
                Contents = new List<EffectContent>
                {
                    Content(EffectContentType.GainMappedStateStacks, "Self", m.Groups[2].Value, Unspecified, "")
                }
            });
            return true;
        }

        List<string> mappings;
        if (TryReadAttributeStateMappings(body, out mappings))
        {
            EffectContent gain = LastMappedStackGain(skill);
            var attributes = new HashSet<string>(gain.Parameters.Skip(4).Where((x, i) => i % 2 == 0), StringComparer.Ordinal);
            var states = new HashSet<string>(gain.Parameters.Skip(4).Where((x, i) => i % 2 == 1), StringComparer.Ordinal);
            for (int i = 0; i < mappings.Count; i += 2)
            {
                if (!attributes.Add(mappings[i])) throw new InvalidOperationException("属性対応が重複しています：" + mappings[i]);
                if (!states.Add(mappings[i + 1])) throw new InvalidOperationException("対応状態が重複しています：" + mappings[i + 1]);
                gain.Parameters.Add(mappings[i]);
                gain.Parameters.Add(mappings[i + 1]);
            }
            return true;
        }

        if (body.TrimEnd('。') == "※複数属性の場合はそれぞれ1種類ずつ")
        {
            EffectContent gain = LastMappedStackGain(skill);
            gain.Parameters[2] = EachAttributePolicy;
            return true;
        }

        m = M(body, @"^効果〈([^〉]+)〉[：:]自身の〈([^〉]+)〉による消費([A-Z]+)はその属性の\[属性に応じたスタック\]×([0-9]+)につき([0-9]+)減少する。?$");
        if (m.Success)
        {
            Number(m.Groups[4].Value, 1, "軽減間隔");
            Number(m.Groups[5].Value, 1, "軽減量");
            EffectDefinition gainEffect = NextMappedStackGainEffect(skill, m.Groups[2].Value);
            string category = m.Groups[2].Value;
            EffectContent gain = gainEffect.Contents.Single(x => x.Type == EffectContentType.GainMappedStateStacks);
            gain.Parameters[3] = m.Groups[1].Value;
            AddOverride(skill.Overrides, type, CategoryResourceCostTriggers(category),
                Change(OverrideContentType.ReduceResourceCostPerMappedStacks,
                    "Self", m.Groups[3].Value, m.Groups[1].Value, m.Groups[4].Value, m.Groups[5].Value, Unspecified));
            return true;
        }

        m = M(body, @"^(?:さらに)?スタックが([0-9]+)つ溜まるごとに(?:自身は)?([A-Z]+)(?:が|を)([0-9]+)回復する。?$");
        if (m.Success)
        {
            Number(m.Groups[1].Value, 1, "回復間隔");
            Number(m.Groups[3].Value, 1, "回復量");
            OverrideContent costRule = NextMappedStackCostRule(skill);
            string[] cost = Args(costRule.Parameters, 6, "ReduceResourceCostPerMappedStacks");
            skill.Effects.Add(new EffectDefinition
            {
                Type = type,
                Triggers = On(Trigger(TriggerTiming.StateStackChanged,
                    Condition(ConditionType.MappedStateStackInterval, "Self", cost[2], m.Groups[1].Value, Unspecified))),
                Contents = new List<EffectContent> { Content(EffectContentType.RestoreResource, "Self", m.Groups[2].Value, m.Groups[3].Value) }
            });
            return true;
        }

        if (body.TrimEnd('。') == "※複数属性の場合はどちらか片方の種類を参照")
        {
            EffectDefinition restore = skill.Effects.LastOrDefault(x => IsMappedStackRestore(x, null) &&
                x.Triggers[0].Conditions.And[0].Parameters[3] == Unspecified);
            if (restore == null) throw new InvalidOperationException("参照方法の前にスタック回復効果を指定してください。");
            string rule = restore.Triggers[0].Conditions.And[0].Parameters[1];
            var costRules = skill.Overrides.Where(x => x.Type == EffectType.Passive).SelectMany(x => x.Contents)
                .Where(x => x.Type == OverrideContentType.ReduceResourceCostPerMappedStacks &&
                    x.Parameters != null && x.Parameters.Count == 6 && x.Parameters[2] == rule).ToList();
            if (costRules.Count != 1) throw new InvalidOperationException("回復が参照する消費軽減を1件指定してください：" + rule);
            costRules[0].Parameters[5] = OneAttributePolicy;
            restore.Triggers[0].Conditions.And[0].Parameters[3] = OneAttributePolicy;
            return true;
        }

        m = M(body, @"^([A-Z]+)が(.+?)以下になった時[、,]\s*(?:自身は)?《([^》]+)》状態になる。?$");
        if (m.Success)
        {
            skill.Effects.Add(new EffectDefinition
            {
                Type = type,
                Triggers = On(Trigger(TriggerTiming.ResourceChanged,
                    Condition(ConditionType.ResourceValue, "Self", m.Groups[1].Value, "AtMost", m.Groups[2].Value))),
                Contents = new List<EffectContent> { Content(EffectContentType.ApplyState, "Self", m.Groups[3].Value) }
            });
            return true;
        }

        m = M(body, @"^(?:(自身|対象|付与者)の)?(.+?最大値)(?:が|は)×(.+?)倍される。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.ModifyStat,
                m.Groups[1].Success ? ActorKey(m.Groups[1].Value) : "Self",
                m.Groups[2].Value, "Multiply", m.Groups[3].Value));
            return true;
        }

        List<string> modifierParts = Split(body, ",");
        if (modifierParts.Count == 1 && body.Contains("、")) modifierParts = Split(body, "、");
        var modifiers = new List<OverrideContent>();
        foreach (string part in modifierParts)
        {
            m = M(part, @"^(?:(自身|対象|付与者)の)?(.+?(?:最大値|点))([+-].+?)。?$");
            if (!m.Success) { modifiers.Clear(); break; }
            modifiers.Add(Change(OverrideContentType.ModifyStat,
                m.Groups[1].Success ? ActorKey(m.Groups[1].Value) : "Self",
                m.Groups[2].Value, "Add", Signed(m.Groups[3].Value)));
        }
        if (modifiers.Count > 0)
        {
            AddOverride(skill.Overrides, type, NoTriggers(), modifiers.ToArray());
            return true;
        }

        m = M(body, @"^〈([^〉]+)〉の威力(?:が|は)×(.+?)倍される。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type, CategoryPowerTriggers(m.Groups[1].Value),
                Change(OverrideContentType.MultiplyPower, m.Groups[2].Value));
            return true;
        }

        m = M(body, @"^(?:〈([^〉]+)〉|([^〈〉]+?))のマナ消費(?:が|は)×(.+?)倍になる。?$");
        if (m.Success)
        {
            string category = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value.Trim();
            AddOverride(skill.Overrides, type, CategoryResourceCostTriggers(category),
                Change(OverrideContentType.MultiplyResourceCost, "Self", "MP", m.Groups[3].Value));
            return true;
        }

        m = M(body, @"^\[([^\]]+)\]による行為判定を行う際[、,]\s*達成値(?:が|は)([+-].+?)される。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type, ActionResultTriggers(m.Groups[1].Value),
                Change(OverrideContentType.AddActionResult, Signed(m.Groups[2].Value)));
            return true;
        }
        return false;
    }

    private static bool TryReadAttributeStateMappings(string text, out List<string> mappings)
    {
        mappings = new List<string>();
        if (!M(text, @"^(?:[^,、\s]+属性：《[^》]+》)(?:\s*[,、]\s*[^,、\s]+属性：《[^》]+》)*。?$").Success) return false;
        foreach (Match match in AllMatches(text, @"([^,、\s]+)属性：《([^》]+)》"))
        {
            mappings.Add(match.Groups[1].Value);
            mappings.Add(match.Groups[2].Value);
        }
        return mappings.Count > 0;
    }

    private static EffectDefinition LastMappedStackGainEffect(SkillBody skill)
    {
        EffectDefinition result = skill.Effects.LastOrDefault(x => x.Type == EffectType.Passive &&
            x.Contents.Any(c => c.Type == EffectContentType.GainMappedStateStacks));
        if (result == null) throw new InvalidOperationException("属性対応の前に属性スタック取得効果を指定してください。");
        return result;
    }

    private static EffectContent LastMappedStackGain(SkillBody skill)
    {
        return LastMappedStackGainEffect(skill).Contents.Last(x => x.Type == EffectContentType.GainMappedStateStacks);
    }

    private static EffectDefinition NextMappedStackGainEffect(SkillBody skill, string category)
    {
        foreach (EffectDefinition effect in skill.Effects.Where(x => x.Type == EffectType.Passive &&
                     x.Contents.Count(c => c.Type == EffectContentType.GainMappedStateStacks) == 1))
        {
            string triggerCategory;
            EffectContent gain = effect.Contents.Single(x => x.Type == EffectContentType.GainMappedStateStacks);
            if (gain.Parameters == null || gain.Parameters.Count < 6 || (gain.Parameters.Count - 4) % 2 != 0)
                throw new InvalidOperationException("GainMappedStateStacksの属性対応が不正です。");
            Need(gain.Parameters[0], "GainMappedStateStacks");
            Need(gain.Parameters[1], "GainMappedStateStacks");
            Need(gain.Parameters[2], "GainMappedStateStacks");
            for (int i = 4; i < gain.Parameters.Count; i++) Need(gain.Parameters[i], "GainMappedStateStacks");
            if (string.IsNullOrEmpty(gain.Parameters[3]) &&
                TryGetCategoryTrigger(effect.Triggers, TriggerTiming.ActionActivated, out triggerCategory) &&
                triggerCategory == category) return effect;
        }
        throw new InvalidOperationException("同じ行動カテゴリーの未接続な属性スタック取得を指定してください：" + category);
    }

    private static OverrideContent NextMappedStackCostRule(SkillBody skill)
    {
        foreach (OverrideContent content in skill.Overrides.Where(x => x.Type == EffectType.Passive).SelectMany(x => x.Contents)
                     .Where(x => x.Type == OverrideContentType.ReduceResourceCostPerMappedStacks))
        {
            string[] p = Args(content.Parameters, 6, "ReduceResourceCostPerMappedStacks");
            if (!skill.Effects.Any(x => IsMappedStackRestore(x, p[2]))) return content;
        }
        throw new InvalidOperationException("スタック回復の前に未接続の消費軽減を指定してください。");
    }

    private static string SingleActionResultStat(SkillBody skill)
    {
        var stats = new List<string>();
        foreach (OverrideDefinition effect in skill.Overrides.Where(x => x.Type == EffectType.Passive &&
                     x.Contents.Any(c => c.Type == OverrideContentType.AddActionResult)))
        {
            string stat;
            if (TryGetActionStatTrigger(effect.Triggers, out stat)) stats.Add(stat);
        }
        if (stats.Count != 1) throw new InvalidOperationException("達成値スパイクの対象となる基本補正を1件指定してください。");
        return stats[0];
    }

    private static bool TryGetCategoryTrigger(List<TriggerDefinition> triggers, TriggerTiming timing, out string category)
    {
        category = null;
        if (triggers == null || triggers.Count != 1 || !ValidTrigger(triggers[0])) return false;
        TriggerDefinition trigger = triggers[0];
        if (trigger.Timing != timing || trigger.Conditions.Or.Count != 0 || trigger.Conditions.And.Count != 2) return false;
        ConditionEntry source = trigger.Conditions.And.SingleOrDefault(x => x.Type == ConditionType.ActionSource);
        ConditionEntry actionCategory = trigger.Conditions.And.SingleOrDefault(x => x.Type == ConditionType.ActionCategory);
        if (source == null || actionCategory == null || !Args(source.Parameters, 1, "ActionSource").SequenceEqual(new[] { "Self" })) return false;
        category = Args(actionCategory.Parameters, 1, "ActionCategory")[0];
        return true;
    }

    private static bool TryGetActionStatTrigger(List<TriggerDefinition> triggers, out string stat)
    {
        stat = null;
        if (triggers == null || triggers.Count != 1 || !ValidTrigger(triggers[0])) return false;
        TriggerDefinition trigger = triggers[0];
        if (trigger.Timing != TriggerTiming.ActionResult || trigger.Conditions.Or.Count != 0 || trigger.Conditions.And.Count != 2) return false;
        ConditionEntry source = trigger.Conditions.And.SingleOrDefault(x => x.Type == ConditionType.ActionSource);
        ConditionEntry actionStat = trigger.Conditions.And.SingleOrDefault(x => x.Type == ConditionType.ActionStat);
        if (source == null || actionStat == null || !Args(source.Parameters, 1, "ActionSource").SequenceEqual(new[] { "Self" })) return false;
        stat = Args(actionStat.Parameters, 1, "ActionStat")[0];
        return true;
    }

    private static bool IsMappedStackRestore(EffectDefinition effect, string rule)
    {
        if (effect == null || effect.Type != EffectType.Passive || effect.Triggers == null || effect.Triggers.Count != 1 ||
            effect.Contents == null || effect.Contents.Count != 1 || effect.Contents[0].Type != EffectContentType.RestoreResource) return false;
        TriggerDefinition trigger = effect.Triggers[0];
        if (!ValidTrigger(trigger) || trigger.Timing != TriggerTiming.StateStackChanged || trigger.Conditions.Or.Count != 0 ||
            trigger.Conditions.And.Count != 1 || trigger.Conditions.And[0].Type != ConditionType.MappedStateStackInterval) return false;
        string[] p = Args(trigger.Conditions.And[0].Parameters, 4, "MappedStateStackInterval");
        return rule == null || p[1] == rule;
    }
}
