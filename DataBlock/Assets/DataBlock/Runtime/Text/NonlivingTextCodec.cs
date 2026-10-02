using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static string[] PrepareNonlivingLines(string[] source)
    {
        bool flavor = false;
        bool moonRecovery = source.Any(x => x.Trim() == "《月影の呪縛》") && source.Any(x => x.Contains("《回復阻害》状態"));
        var result = new List<string>();
        foreach (string raw in source)
        {
            string line = raw;
            if (M(line.Trim(), @"^―{8,}$").Success) flavor = true;
            if (flavor) { result.Add(raw); continue; }
            line = line.Replace("さらに、", "さらに");
            if (moonRecovery) line = RegexReplace(line, @"^回復無効効果〈エンチャント〉", "回復阻害効果〈エンチャント〉");
            line = RegexReplace(line, @"^深紅の剣〈顕現〉：", "深紅の剣効果〈顕現〉：");
            line = RegexReplace(line, @"^((?:【パッシブ効果】)?)([A-Z]+が[0-9]+以下になった時、.+?ロールを行う必要がない。)$", "$1自身は$2");
            line = RegexReplace(line, @"自身は([A-Z]+)が存在せず《([^》]+)》状態にならない。", "自身は$1が存在しない。\n自身は《$2》状態にならない。");
            line = line.Replace("自身のHPとMPを回復する事が出来なくなる。", "自身はHPを回復することができなくなる。\n自身はMPを回復することができなくなる。");
            line = RegexReplace(line, @"自身は《([^》]+)》による効果を受けない。", "特性《$1》による効果をすべて無効にする。");
            line = RegexReplace(line, @"環境〈([^〉]+)〉および〈([^〉]+)〉による環境効果を受けない。", "環境《$1》および《$2》による環境効果を受けない。");
            line = RegexReplace(line, @"自身は自身のターン開始時([0-9]+)ダメージを受ける。", "自身は自身のターン開始時に$1のダメージを受ける。");
            line = RegexReplace(line, @"自身の\s*((?:\[[^\]]+\])(?:及び\[[^\]]+\])*)による達成値が([0-9.]+)倍される。", "$1による行為判定の達成値が×$2倍される。");
            line = RegexReplace(line, @"(【宣言条件】)自身が《([^》]+)》を受けた時、その対象へ発動。?$", "$1自身が〈$2〉を受けた時、その対象へ宣言可能。");
            line = RegexReplace(line, @"自身への〈([^〉]+)〉を全て無効にする。", "自身への〈$1〉を無効にする。");
            // These two penalties have the same OR selector, but remain separate data.
            line = RegexReplace(line, @"自身は〈([^〉]+)〉および〈([^〉]+)〉を受けた時、被ダメージが×([0-9.]+)倍され、さらに([0-9]+)ターンの間《([^》]+)》状態となる。",
                "自身が〈$1, $2〉のいずれかを受けた時、被ダメージが×$3倍される。\n自身が〈$1, $2〉のいずれかを受けた時、自身は$4ターンの間《$5》状態になる。");
            result.AddRange(line.Split('\n'));
        }
        return result.ToArray();
    }

    private static ConditionEntry ReadNonlivingCondition(string text)
    {
        var m = M(text.TrimEnd('。'), @"^自身が《([^》]+)》状態になった時$");
        if (m.Success) return Condition(ConditionType.OwnStateApplied, m.Groups[1].Value);
        m = M(text.TrimEnd('。'), @"^《([^》]+)》状態(?:の)?キャラクターを([0-9]+)体まで選択$");
        if (m.Success) return Condition(ConditionType.SelectCharactersWithState, m.Groups[1].Value, m.Groups[2].Value, "UpTo");
        return null;
    }

    private static string NonlivingRaceList(string text)
    { return string.Join(", ", AllMatches(text, @"《([^》]+)》").Cast<Match>().Select(x => x.Groups[1].Value)); }

    private static List<TriggerDefinition> ThisAttackDamageTriggers()
    { return On(Trigger(TriggerTiming.DamageDealt, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionOrigin, "ThisSkill"))); }

    private static bool ReadNonlivingSkill(SkillBody skill, EffectType type, string text)
    {
        string s = RegexReplace(text.Trim(), @"^さらに[、]?\s*", "");
        var m = M(s, @"^戦闘中の敵と味方全てのキャラクター(?:に対して|を)(.+?)の威力で攻撃する。?$");
        if (m.Success)
        { Require(type == EffectType.Counter); ExtendedEffect(skill, type, Content(EffectContentType.SkillAttack, "AllBattleCharacters", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身のターン開始時、(?:自身の([A-Z]+)は(.+?)回復する|自身は([A-Z]+)を(.+?)回復する)。?$");
        if (m.Success)
        {
            Require(type == EffectType.Passive);
            ExtendedEffect(skill, type, Content(EffectContentType.RestoreResource, "Self", m.Groups[1].Success ? m.Groups[1].Value : m.Groups[3].Value, m.Groups[2].Success ? m.Groups[2].Value : m.Groups[4].Value), Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Self"))); return true;
        }
        m = M(s, @"^(?:この)?攻撃が命中した時、対象が((?:《[^》]+》)(?:,\s*《[^》]+》)*)のいずれかなら、《([^》]+)》状態を([0-9]+)ターン付与する。?$");
        if (m.Success)
        { Require(type == EffectType.Active); ExtendedEffect(skill, type, Content(EffectContentType.HitRaceState, NonlivingRaceList(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value)); return true; }
        m = M(s, @"^既に対象が《([^》]+)》状態の時、威力は×([0-9.]+)倍される。?$");
        if (m.Success)
        { Require(type == EffectType.Active); AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.PowerAgainstState, m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^(?:この)?攻撃が命中した時、対象に《([^》]+)》状態を([0-9]+)ターン付与する。?$");
        if (m.Success)
        { Require(type == EffectType.Active); ExtendedEffect(skill, type, Content(EffectContentType.ApplyState, "Target", m.Groups[1].Value, m.Groups[2].Value), Trigger(TriggerTiming.AttackHit)); return true; }
        m = M(s, @"^(?:対象に|この攻撃で)与えたダメージの×([0-9.]+)倍の([A-Z]+)(?:と([A-Z]+))?を回復する。?$");
        if (m.Success)
        {
            Require(type == EffectType.Active);
            foreach (string resource in new[] { m.Groups[2].Value, m.Groups[3].Value }.Where(x => x.Length > 0))
                ExtendedEffect(skill, type, Content(EffectContentType.RestoreFromDamage, "Self", resource, m.Groups[1].Value), ThisAttackDamageTriggers().ToArray());
            return true;
        }
        m = M(s, @"^自身が与える([A-Z]+)ダメージは×([0-9.]+)倍される。?$");
        if (m.Success)
        { Require(type == EffectType.Passive); AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.DamageDealt, Condition(ConditionType.ActionSource, "Self"))), Change(OverrideContentType.MultiplyResourceDamage, "Self", m.Groups[1].Value, m.Groups[2].Value, "Automatic")); return true; }
        return false;
    }

    private static string NonlivingContentText(EffectContent content)
    {
        if (content == null || content.Type != EffectContentType.RestoreFromDamage) return null;
        var p = Args(content.Parameters, 3, "RestoreFromDamage"); Require(p[0] == "Self"); PositiveHumanFactor(p[2]);
        return "この攻撃で与えたダメージの×" + p[2] + "倍の" + p[1] + "を回復する。";
    }
    private static string NonlivingTriggerText(EffectType type, List<TriggerDefinition> t)
    {
        if (Matches(t, ThisAttackDamageTriggers().ToArray())) { Require(type == EffectType.Active); return ""; }
        if (Matches(t, Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Self"))))
        { Require(type == EffectType.Passive); return "自身のターン開始時、"; }
        return null;
    }
    private static string NonlivingOverrideText(OverrideDefinition effect, OverrideContent content)
    {
        if (content == null || content.Type != OverrideContentType.MultiplyResourceDamage || effect == null ||
            !Matches(effect.Triggers, Trigger(TriggerTiming.DamageDealt, Condition(ConditionType.ActionSource, "Self")))) return null;
        var p = Args(content.Parameters, 4, "MultiplyResourceDamage"); Require(effect.Type == EffectType.Passive && p[0] == "Self" && p[3] == "Automatic"); PositiveHumanFactor(p[2]);
        return "自身が与える" + p[1] + "ダメージは×" + p[2] + "倍される。";
    }

    private static bool ReadNonlivingState(StateDefinition state, string s)
    {
        var m = M(s, @"^自身の〈([^〉]+)〉による武器攻撃が命中した時、対象が((?:《[^》]+》)(?:,\s*《[^》]+》)*)のいずれかなら、《([^》]+)》状態を([0-9]+)ターン付与する。?$");
        if (m.Success)
        { AddStateEffect(state, NoTriggers(), Content(EffectContentType.WeaponHitRaceState, m.Groups[1].Value, NonlivingRaceList(m.Groups[2].Value), m.Groups[3].Value, m.Groups[4].Value)); return true; }
        m = M(s, @"^攻撃が命中した時、対象が((?:《[^》]+》)(?:,\s*《[^》]+》)*)のいずれかなら、《([^》]+)》状態を([0-9]+)ターン付与する。?$");
        if (m.Success)
        {
            // The source repeats the immediately preceding weapon effect. Do not
            // turn its explanation into a second, unrestricted state application.
            var prior = state.Effects.LastOrDefault();
            Require(prior != null && prior.Contents.Count == 1 && prior.Contents[0].Type == EffectContentType.WeaponHitRaceState &&
                prior.Contents[0].Parameters.Skip(1).SequenceEqual(new[] { NonlivingRaceList(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value }));
            return true;
        }
        m = M(s, @"^既に対象が《([^》]+)》状態の時、威力は×([0-9.]+)倍される。?$");
        if (m.Success)
        {
            var weapons = state.Effects.SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.WeaponHitRaceState).ToList();
            Require(weapons.Count == 1);
            AddOverride(state.Overrides, EffectType.Passive, NoTriggers(), Change(OverrideContentType.WeaponPowerAgainstState, weapons[0].Parameters[0], m.Groups[1].Value, m.Groups[2].Value)); return true;
        }
        m = M(s, @"^自身のあらゆる行為判定の達成値は(.+?)減少する。?$");
        if (m.Success)
        { AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"))), Change(OverrideContentType.AddActionResult, "-(" + m.Groups[1].Value + ")")); return true; }
        m = M(s, @"^自身のあらゆる行為判定の達成値は([+-].+?)加算される。?$");
        if (m.Success)
        { AddOverride(state.Overrides, EffectType.Passive, On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"))), Change(OverrideContentType.AddActionResult, m.Groups[1].Value)); return true; }
        return false;
    }
    private static string NonlivingStateOverrideText(List<TriggerDefinition> t, OverrideContent content)
    {
        if (content == null || content.Type != OverrideContentType.AddActionResult || !Matches(t, Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self")))) return null;
        return "自身のあらゆる行為判定の達成値は" + Signed(Args(content.Parameters, 1, "AddActionResult")[0]) + "加算される。";
    }

    private static void ValidateNonlivingSkill(SkillBody skill)
    {
        var attacks = skill.Effects.Where(e => e.Type == EffectType.Active && e.Triggers.Count == 0).SelectMany(e => e.Contents)
            .Where(c => (c.Type == EffectContentType.SkillAttack || c.Type == EffectContentType.WeaponAttack) && c.Parameters[0] == "Target").ToList();
        foreach (var effect in skill.Effects)
        foreach (var content in effect.Contents)
        {
            if (content.Type == EffectContentType.HitRaceState || content.Type == EffectContentType.RestoreFromDamage)
            {
                Require(attacks.Count == 1 && skill.Effects.TakeWhile(e => e != effect).SelectMany(e => e.Contents).Contains(attacks[0]));
                if (content.Type == EffectContentType.RestoreFromDamage) Require(effect.Type == EffectType.Active && Matches(effect.Triggers, ThisAttackDamageTriggers().ToArray()));
            }
            if (content.Type == EffectContentType.SkillAttack && content.Parameters[0] == "AllBattleCharacters")
                Require(effect.Type == EffectType.Counter && effect.Triggers.Count == 0);
        }
        foreach (var content in skill.Overrides.SelectMany(e => e.Contents).Where(c => c.Type == OverrideContentType.PowerAgainstState))
            Require(attacks.Count == 1);
    }
}
