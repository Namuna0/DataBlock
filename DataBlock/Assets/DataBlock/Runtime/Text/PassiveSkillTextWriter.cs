using System;
using System.Collections.Generic;
using System.Linq;

public static partial class SkillTextConverter
{
    private static bool TryPassiveContentText(EffectContent content, out string text)
    {
        text = null;
        if (content.Type != EffectContentType.GainMappedStateStacks) return false;
        string[] p = VariableArgs(content.Parameters, 6, "GainMappedStateStacks");
        if ((p.Length - 4) % 2 != 0) throw new InvalidOperationException("属性対応は属性名と状態名の組にしてください。");
        if (p[0] != "Self" || p[2] != EachAttributePolicy) throw new InvalidOperationException("属性スタック取得はSelf / Eachで指定してください。");
        Number(p[1], 1, "獲得スタック数"); Need(p[3], "属性スタック参照名");
        string[] attributes = p.Skip(4).Where((x, i) => i % 2 == 0).ToArray();
        string[] states = p.Skip(4).Where((x, i) => i % 2 == 1).ToArray();
        if (attributes.Distinct(StringComparer.Ordinal).Count() != attributes.Length || states.Distinct(StringComparer.Ordinal).Count() != states.Length)
            throw new InvalidOperationException("属性対応の属性名または状態名が重複しています。");
        var pairs = new List<string>();
        for (int i = 4; i < p.Length; i += 2) pairs.Add(p[i] + "属性：《" + p[i + 1] + "》");
        text = "属性に応じた状態を" + p[1] + "スタック得る。\n" + string.Join(", ", pairs) +
               "\n※複数属性の場合はそれぞれ1種類ずつ。";
        return true;
    }

    private static bool TryPassiveTriggerText(EffectType type, List<TriggerDefinition> triggers, out string text)
    {
        text = null;
        if (type != EffectType.Passive || triggers == null || triggers.Count != 1 || !ValidTrigger(triggers[0])) return false;
        string category;
        if (TryGetCategoryTrigger(triggers, TriggerTiming.ActionActivated, out category))
        {
            text = "自身が〈" + category + "〉を発動するたびに";
            return true;
        }
        TriggerDefinition trigger = triggers[0];
        if (trigger.Timing == TriggerTiming.ResourceChanged && trigger.Conditions.Or.Count == 0 &&
            trigger.Conditions.And.Count == 1 && trigger.Conditions.And[0].Type == ConditionType.ResourceValue)
        {
            string[] p = Args(trigger.Conditions.And[0].Parameters, 4, "ResourceValue");
            if (p[0] != "Self" || p[2] != "AtMost") throw new InvalidOperationException("資源閾値はSelf / AtMostで指定してください。");
            text = p[1] + "が" + p[3] + "以下になった時、";
            return true;
        }
        if (trigger.Timing == TriggerTiming.StateStackChanged && trigger.Conditions.Or.Count == 0 &&
            trigger.Conditions.And.Count == 1 && trigger.Conditions.And[0].Type == ConditionType.MappedStateStackInterval)
        {
            string[] p = Args(trigger.Conditions.And[0].Parameters, 4, "MappedStateStackInterval");
            if (p[0] != "Self" || p[3] != OneAttributePolicy) throw new InvalidOperationException("属性スタック参照はSelf / Oneで指定してください。");
            Number(p[2], 1, "回復間隔");
            text = "さらにスタックが" + p[2] + "つ溜まるごとに";
            return true;
        }
        return false;
    }

    private static bool TryPassiveOverrideText(OverrideDefinition effect, OverrideContent content, out string text)
    {
        text = null;
        string[] p;
        string category;
        switch (content.Type)
        {
            case OverrideContentType.ModifyStat:
                p = Args(content.Parameters, 4, "ModifyStat");
                if (effect.Type != EffectType.Passive || effect.Triggers.Count != 0) return false;
                string statPrefix = p[0] == "Self" ? "" : Actor(p[0]) + "の";
                if (p[2] == "Multiply") text = statPrefix + p[1] + "が×" + p[3] + "倍される。";
                else if (p[2] == "Add") text = statPrefix + p[1] + Signed(p[3]);
                else throw new InvalidOperationException("能力値補正はAdd / Multiplyで指定してください。");
                return true;
            case OverrideContentType.MultiplyPower:
                if (effect.Type == EffectType.Passive && TryGetCategoryTrigger(effect.Triggers, TriggerTiming.AttackPower, out category))
                {
                    p = Args(content.Parameters, 1, "MultiplyPower");
                    text = "〈" + category + "〉の威力が×" + p[0] + "倍される。";
                    return true;
                }
                return false;
            case OverrideContentType.MultiplyResourceCost:
                p = Args(content.Parameters, 3, "MultiplyResourceCost");
                if (effect.Type != EffectType.Passive || p[0] != "Self" || p[1] != "MP" ||
                    !TryGetCategoryTrigger(effect.Triggers, TriggerTiming.ResourceCost, out category)) return false;
                text = "〈" + category + "〉のマナ消費が×" + p[2] + "倍になる。";
                return true;
            case OverrideContentType.ReduceResourceCostPerMappedStacks:
                p = Args(content.Parameters, 6, "ReduceResourceCostPerMappedStacks");
                if (effect.Type != EffectType.Passive || p[0] != "Self" || p[5] != OneAttributePolicy ||
                    !TryGetCategoryTrigger(effect.Triggers, TriggerTiming.ResourceCost, out category)) return false;
                Number(p[3], 1, "軽減間隔"); Number(p[4], 1, "軽減量");
                text = "効果〈" + p[2] + "〉：自身の〈" + category + "〉による消費" + p[1] +
                       "はその属性の[属性に応じたスタック]×" + p[3] + "につき" + p[4] + "減少する。";
                return true;
            case OverrideContentType.AddActionResult:
                if (effect.Type == EffectType.Passive && TryGetActionStatTrigger(effect.Triggers, out category))
                {
                    p = Args(content.Parameters, 1, "AddActionResult");
                    text = "[" + category + "]による行為判定を行う際、達成値が" + Signed(p[0]) + "される。";
                    return true;
                }
                return false;
            case OverrideContentType.SetModifier:
                if (effect.Type != EffectType.SecondSpike && effect.Type != EffectType.ThirdSpike) return false;
                if (effect.Triggers.Count != 0) return false;
                p = VariableArgs(content.Parameters, 4, "SetModifier");
                switch (p[0])
                {
                    case PowerModifier:
                        if (p.Length != 4 || p[1] != "ActionCategory") break;
                        text = "このパッシブ効果による〈" + p[2] + "〉の威力倍率は" + p[3] + "倍に変化する。"; return true;
                    case ResourceCostModifier:
                        if (p.Length != 5 || p[1] != "ActionCategory" || p[3] != "MP") break;
                        text = "このパッシブ効果による〈" + p[2] + "〉のマナ消費倍率は×" + p[4] + "倍に変化する。"; return true;
                    case StatModifier:
                        if (p.Length != 5 || p[3] != "Add") break;
                        text = "このパッシブ効果による" + (p[1] == "Self" ? "" : Actor(p[1]) + "の") +
                               p[2] + "増加は" + Signed(p[4]) + "に変化する。"; return true;
                    case ActionResultModifier:
                        if (p.Length != 4 || p[1] != "ActionStat") break;
                        text = "このパッシブ効果による達成値増加は" + Signed(p[3]) + "に変化する。"; return true;
                }
                throw new InvalidOperationException("未対応の補正置換です：" + string.Join(" / ", p));
            default:
                return false;
        }
    }

    private static string AppendPassiveEffectNotes(EffectDefinition effect, string text)
    {
        if (IsMappedStackRestore(effect, null)) return text + "\n※複数属性の場合はどちらか片方の種類を参照。";
        return text;
    }

}
