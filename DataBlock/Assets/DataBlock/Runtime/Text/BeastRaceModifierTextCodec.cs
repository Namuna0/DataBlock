using System;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static bool ReadBeastRaceModifier(SkillBody skill, EffectType type, string s)
    {
        Match m = M(s, @"^戦闘開始の([0-9]+)ターン目に発動した場合、スキル値は([0-9]+)に上昇する。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.SkillValue, Condition(ConditionType.TurnNumber, m.Groups[1].Value))), Change(OverrideContentType.SetSkillValue, m.Groups[2].Value)); return true; }
        m = M(s, @"^このアクティブ効果による《([^》]+)》付与は([0-9]+)ターンに変化する。?$");
        if (m.Success && SpiritSpike(type)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetAppliedStateDuration, "Target", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^(.+?)効果による自身の被ダメージ軽減が×([0-9.]+)倍に変化する。?$");
        if (m.Success && SpiritSpike(type)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetStateDamageMultiplier, m.Groups[1].Value, "Self", m.Groups[2].Value)); return true; }
        m = M(s, @"^このパッシブ効果による([A-Z]+)は\1最大値の([0-9]+)%に変化する。?$");
        if (m.Success && SpiritSpike(type)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetOnAppliedStateRecovery, m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^自身の〈([^〉]+)〉がクリティカルした時、(威力|達成値)は×([0-9.]+)倍される。（他効果と重複する）$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.CriticalCategoryMultiplier, m.Groups[1].Value, m.Groups[2].Value == "威力" ? "Power" : "ActionResult", m.Groups[3].Value, "Stack")); return true; }
        m = M(s, @"^そのクリティカル時の達成値は([0-9.]+)倍される。?$");
        if (m.Success)
        {
            var power = skill.Overrides.Where(x => x.Type == type).SelectMany(x => x.Contents).LastOrDefault(x => x.Type == OverrideContentType.CriticalCategoryMultiplier && x.Parameters[1] == "Power");
            Require(power != null); AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.CriticalCategoryMultiplier, power.Parameters[0], "ActionResult", m.Groups[1].Value, "Stack")); return true;
        }
        m = M(s, @"^自身が使用した持続効果を持つアイテムの効果ターンが\+([0-9]+)される。（他キャラクターに対しても有効）$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.ItemEffectDuration, "Self", "AllTargets", m.Groups[1].Value)); return true; }
        m = M(s, @"^サイズ(.+?)のアイテムを最大([0-9]+)個まで、サイズ(.+?)のアイテムを最大([0-9]+)個まで所持する事が出来ます。?$");
        if (m.Success)
        { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.ItemCapacity, m.Groups[1].Value, m.Groups[2].Value), Change(OverrideContentType.ItemCapacity, m.Groups[3].Value, m.Groups[4].Value)); return true; }
        m = M(s, @"^サイズ(.+?)のアイテムを最大([0-9]+)個まで所持する事が出来ます。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.ItemCapacity, m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^エリアの進行ロールの回数を-([0-9]+)しても良い。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.OptionalAreaProgressReduction, m.Groups[1].Value, "Unspecified", "Unspecified", "Optional")); return true; }
        m = M(s, @"^エリアに〈([^〉]+)〉を含む場合、エリアの進行ロールの減少回数が-([0-9]+)に変化する。?$");
        if (m.Success)
        {
            var reduction = skill.Overrides.Where(x => x.Type == type).SelectMany(x => x.Contents).LastOrDefault(x => x.Type == OverrideContentType.OptionalAreaProgressReduction);
            Require(reduction != null && reduction.Parameters[1] == "Unspecified"); reduction.Parameters[1] = m.Groups[1].Value; reduction.Parameters[2] = m.Groups[2].Value; return true;
        }
        m = M(s, @"^選択して宣言するスキルの威力は×([0-9.]+)倍される。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.MultiplyDeclaredSkillPower, m.Groups[1].Value)); return true; }
        return false;
    }

    private static string BeastRaceOverrideText(OverrideDefinition effect, OverrideContent content)
    {
        if (effect == null || content == null) return null;
        var t = effect.Triggers;
        string[] p;
        if (content.Type == OverrideContentType.SetSkillValue && effect.Type == EffectType.Active && t.Count == 1 && t[0].Conditions.And.Count == 1 && t[0].Conditions.And[0].Type == ConditionType.TurnNumber)
        {
            string turn = Args(t[0].Conditions.And[0].Parameters, 1, "TurnNumber")[0]; Number(turn, 1, "ターン"); Require(Matches(t, Trigger(TriggerTiming.SkillValue, Condition(ConditionType.TurnNumber, turn))));
            return "戦闘開始の" + turn + "ターン目に発動した場合、スキル値は" + Args(content.Parameters, 1, "SetSkillValue")[0] + "に上昇する。";
        }
        if ((int)content.Type < 43 || (int)content.Type > 50) return null;
        Require(t.Count == 0);
        switch (content.Type)
        {
            case OverrideContentType.SetAppliedStateDuration:
                p = Args(content.Parameters, 3, "SetAppliedStateDuration"); Require(SpiritSpike(effect.Type) && p[0] == "Target"); Number(p[2], 1, "持続ターン");
                return "このアクティブ効果による《" + p[1] + "》付与は" + p[2] + "ターンに変化する。";
            case OverrideContentType.SetStateDamageMultiplier:
                p = Args(content.Parameters, 3, "SetStateDamageMultiplier"); Require(SpiritSpike(effect.Type) && p[1] == "Self"); PositiveHumanFactor(p[2]);
                return p[0] + "効果による自身の被ダメージ軽減が×" + p[2] + "倍に変化する。";
            case OverrideContentType.SetOnAppliedStateRecovery:
                p = Args(content.Parameters, 2, "SetOnAppliedStateRecovery"); Require(SpiritSpike(effect.Type)); Number(p[1], 1, "回復率");
                return "このパッシブ効果による" + p[0] + "は" + p[0] + "最大値の" + p[1] + "%に変化する。";
            case OverrideContentType.CriticalCategoryMultiplier:
                p = Args(content.Parameters, 4, "CriticalCategoryMultiplier"); Require(effect.Type == EffectType.Passive && (p[1] == "Power" || p[1] == "ActionResult") && p[3] == "Stack"); PositiveHumanFactor(p[2]);
                return "自身の〈" + p[0] + "〉がクリティカルした時、" + (p[1] == "Power" ? "威力" : "達成値") + "は×" + p[2] + "倍される。（他効果と重複する）";
            case OverrideContentType.ItemEffectDuration:
                p = Args(content.Parameters, 3, "ItemEffectDuration"); Require(effect.Type == EffectType.Passive && p[0] == "Self" && p[1] == "AllTargets"); Number(p[2], 1, "追加ターン");
                return "自身が使用した持続効果を持つアイテムの効果ターンが+" + p[2] + "される。（他キャラクターに対しても有効）";
            case OverrideContentType.ItemCapacity:
                p = Args(content.Parameters, 2, "ItemCapacity"); Require(effect.Type == EffectType.Passive); Number(p[1], 1, "所持上限");
                return "サイズ" + p[0] + "のアイテムを最大" + p[1] + "個まで所持する事が出来ます。";
            case OverrideContentType.OptionalAreaProgressReduction:
                p = Args(content.Parameters, 4, "OptionalAreaProgressReduction"); Require(effect.Type == EffectType.Passive && p[1] != "Unspecified" && p[3] == "Optional"); Number(p[0], 1, "通常軽減回数"); Number(p[2], 1, "条件付き軽減回数");
                return "エリアの進行ロールの回数を-" + p[0] + "しても良い。\nエリアに〈" + p[1] + "〉を含む場合、エリアの進行ロールの減少回数が-" + p[2] + "に変化する。";
            case OverrideContentType.MultiplyDeclaredSkillPower:
                p = Args(content.Parameters, 1, "MultiplyDeclaredSkillPower"); Require(effect.Type == EffectType.Critical); PositiveHumanFactor(p[0]);
                return "選択して宣言するスキルの威力は×" + p[0] + "倍される。";
            default: return null;
        }
    }
}
