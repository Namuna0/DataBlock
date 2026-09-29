using System;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static bool SpiritSpike(EffectType type) { return type == EffectType.SecondSpike || type == EffectType.ThirdSpike; }

    private static bool ReadSpiritModifier(SkillBody skill, EffectType type, string s)
    {
        Match m;
        m = M(s, @"^《([^》]+)》の遅延攻撃の威力は×([0-9.]+)倍される。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.MultiplyStateAttackPower, m.Groups[1].Value, m.Groups[2].Value)); return true; }
        if (s == "属性威力による自身の被ダメージは×[最も高いその属性の不利属性B]倍される。")
        { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.AttributeWeaknessDamage, "Self", "HighestDisadvantageAttributeBonus")); return true; }
        if (s.TrimEnd('。') == "この効果はカウンター効果の対象にならない")
        { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.PreventCounterTarget, "ThisEffect")); return true; }
        m = M(s, @"^〈([^〉]+)〉による自身の被ダメージが×([0-9.]+)倍される。?$");
        if (m.Success)
        { AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.ActionCategory, m.Groups[1].Value))), Change(OverrideContentType.MultiplyDamageTaken, "Self", m.Groups[2].Value)); return true; }
        m = M(s, @"^自身の〈([^〉]+)〉による発動ロールを行う際、ダイスが([0-9]+)[-～]([0-9]+)の時にクリティカルとなる。?$");
        if (m.Success)
        { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetRollRange, "Activation", "Critical", m.Groups[2].Value, m.Groups[3].Value, m.Groups[1].Value)); return true; }
        m = M(s, @"^効果量([0-9.]+)倍。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.MultiplyRecovery, m.Groups[1].Value)); return true; }
        m = M(s, @"^このクールタイムは([0-9]+)ターンに変化する。?$");
        if (m.Success && SpiritSpike(type)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetCooldown, m.Groups[1].Value)); return true; }
        m = M(s, @"^この発動ロールによる目標値は([0-9]+)に変化する。?$");
        if (m.Success && SpiritSpike(type)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetActivationRollTarget, m.Groups[1].Value)); return true; }
        m = M(s, @"^このアクティブ効果による《([^》]+)》スタックの付与は([0-9]+)に変化する。?$");
        if (m.Success && SpiritSpike(type)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetStackAmount, "Target", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^《([^》]+)》を付与されたキャラクターの代わりに行動を([0-9]+)回までに宣言に変化する。?$");
        if (m.Success && SpiritSpike(type)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetStateControlLimit, m.Groups[1].Value, "Turn", m.Groups[2].Value)); return true; }
        m = M(s, @"^自身が〈([^〉]+)〉を発動する際、自身が《([^》]+)》状態、かつ対象が《([^》]+)》状態、かつ自身と対象が《([^》]+)》状態の時、自身の〈([^〉]+)〉(?:に)?よる威力は×([0-9.]+)倍,\s*消費([A-Z]+)が([0-9]+)以上のスキルの消費([A-Z]+)は(-[0-9]+)される。?$");
        if (m.Success)
        {
            Require(m.Groups[1].Value == m.Groups[5].Value && m.Groups[7].Value == m.Groups[9].Value);
            AddOverride(skill.Overrides, type, NoTriggers(),
                Change(OverrideContentType.ConditionalStatePower, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[6].Value),
                Change(OverrideContentType.ConditionalStateCost, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[7].Value, m.Groups[8].Value, m.Groups[10].Value)); return true;
        }
        // Canonical output writes each independent modifier with its own selector.
        m = M(s, @"^自身が〈([^〉]+)〉を発動する際、自身が《([^》]+)》状態、かつ対象が《([^》]+)》状態、かつ自身と対象が《([^》]+)》状態の時、威力は×([0-9.]+)倍される。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.ConditionalStatePower, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value)); return true; }
        m = M(s, @"^自身が〈([^〉]+)〉を発動する際、自身が《([^》]+)》状態、かつ対象が《([^》]+)》状態、かつ自身と対象が《([^》]+)》状態の時、消費([A-Z]+)が([0-9]+)以上のスキルの消費\5は(-[0-9]+)される。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.ConditionalStateCost, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value, m.Groups[6].Value, m.Groups[7].Value)); return true; }
        m = M(s, @"^このパッシブ効果による威力上昇は([0-9.]+)倍に変化する。?$");
        if (m.Success && SpiritSpike(type) && skill.Overrides.SelectMany(x => x.Contents).Any(x => x.Type == OverrideContentType.ConditionalStatePower))
        { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetConditionalStatePower, m.Groups[1].Value)); return true; }
        return false;
    }

    private static string SpiritOverrideText(OverrideDefinition effect, OverrideContent content)
    {
        if (effect == null || content == null) return null;
        var t = effect.Triggers;
        string[] p;
        if (content.Type == OverrideContentType.MultiplyDamageTaken && t.Count == 1 && t[0].Conditions.And.Count == 1 && t[0].Conditions.And[0].Type == ConditionType.ActionCategory)
        {
            p = Args(content.Parameters, 2, "MultiplyDamageTaken"); string cat = Args(t[0].Conditions.And[0].Parameters, 1, "ActionCategory")[0];
            Require(effect.Type == EffectType.Passive && p[0] == "Self" && Matches(t, Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.ActionCategory, cat)))); PositiveHumanFactor(p[1]);
            return "〈" + cat + "〉による自身の被ダメージが×" + p[1] + "倍される。";
        }
        if (content.Type == OverrideContentType.SetRollRange && content.Parameters.Count == 5)
        {
            p = Args(content.Parameters, 5, "SetRollRange"); Require(t.Count == 0 && effect.Type == EffectType.Passive && p[0] == "Activation" && p[1] == "Critical");
            int lower = Number(p[2], 1, "下限"); Require(Number(p[3], lower, "上限") <= 100);
            return "自身の〈" + p[4] + "〉による発動ロールを行う際、ダイスが" + p[2] + "-" + p[3] + "の時にクリティカルとなる。";
        }
        if (content.Type == OverrideContentType.MultiplyRecovery && effect.Type == EffectType.Critical && t.Count == 0)
        { p = Args(content.Parameters, 1, "MultiplyRecovery"); PositiveHumanFactor(p[0]); return "効果量" + p[0] + "倍"; }
        if (SpiritSpike(effect.Type) && t.Count == 0)
        {
            if (content.Type == OverrideContentType.SetCooldown)
            { p = Args(content.Parameters, 1, "SetCooldown"); Number(p[0], 0, "クールタイム"); return "このクールタイムは" + p[0] + "ターンに変化する。"; }
            if (content.Type == OverrideContentType.SetStackAmount)
            { p = Args(content.Parameters, 3, "SetStackAmount"); Require(p[0] == "Target"); Number(p[2], 1, "スタック数"); return "このアクティブ効果による《" + p[1] + "》スタックの付与は" + p[2] + "に変化する。"; }
        }
        switch (content.Type)
        {
            case OverrideContentType.MultiplyStateAttackPower:
                p = Args(content.Parameters, 2, "MultiplyStateAttackPower"); Require(effect.Type == EffectType.Critical && t.Count == 0); PositiveHumanFactor(p[1]);
                return "《" + p[0] + "》の遅延攻撃の威力は×" + p[1] + "倍される。";
            case OverrideContentType.AttributeWeaknessDamage:
                p = Args(content.Parameters, 2, "AttributeWeaknessDamage"); Require(effect.Type == EffectType.Passive && t.Count == 0 && p.SequenceEqual(new[] { "Self", "HighestDisadvantageAttributeBonus" }));
                return "属性威力による自身の被ダメージは×[最も高いその属性の不利属性B]倍される。";
            case OverrideContentType.PreventCounterTarget:
                p = Args(content.Parameters, 1, "PreventCounterTarget"); Require(effect.Type == EffectType.Active && t.Count == 0 && p[0] == "ThisEffect"); return "この効果はカウンター効果の対象にならない。";
            case OverrideContentType.SetStateControlLimit:
                p = Args(content.Parameters, 3, "SetStateControlLimit"); Require(SpiritSpike(effect.Type) && t.Count == 0 && p[1] == "Turn"); Number(p[2], 1, "行動回数");
                return "《" + p[0] + "》を付与されたキャラクターの代わりに行動を" + p[2] + "回までに宣言に変化する。";
            case OverrideContentType.ConditionalStatePower:
            case OverrideContentType.ConditionalStateCost:
                bool power = content.Type == OverrideContentType.ConditionalStatePower;
                p = Args(content.Parameters, power ? 5 : 7, content.Type.ToString()); Require(effect.Type == EffectType.Passive && t.Count == 0);
                if (power) PositiveHumanFactor(p[4]); else { Number(p[5], 1, "消費下限"); Require(M(p[6], @"^-[0-9]+$").Success); }
                return "自身が〈" + p[0] + "〉を発動する際、自身が《" + p[1] + "》状態、かつ対象が《" + p[2] + "》状態、かつ自身と対象が《" + p[3] + "》状態の時、" +
                    (power ? "威力は×" + p[4] + "倍される。" : "消費" + p[4] + "が" + p[5] + "以上のスキルの消費" + p[4] + "は" + p[6] + "される。");
            case OverrideContentType.SetConditionalStatePower:
                p = Args(content.Parameters, 1, "SetConditionalStatePower"); Require(SpiritSpike(effect.Type) && t.Count == 0); PositiveHumanFactor(p[0]); return "このパッシブ効果による威力上昇は" + p[0] + "倍に変化する。";
            default: return null;
        }
    }
}
