using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    // Each grammar maps to a distinct semantic enum. Parameters contain selectors,
    // numbers or formulas, never an uninterpreted combat sentence or a skill-name key.
    private sealed class SkillTextRule
    {
        public readonly EffectContentType Content;
        public readonly OverrideContentType Modifier;
        public readonly bool State;
        public readonly EffectType Section;
        public readonly string Template;
        public readonly string Pattern;
        public readonly int Count;

        public SkillTextRule(EffectContentType content, bool state, EffectType section, string template, params string[] parameters)
            : this(content, OverrideContentType.None, state, section, template, parameters) { }
        public SkillTextRule(OverrideContentType modifier, bool state, EffectType section, string template, params string[] parameters)
            : this(EffectContentType.None, modifier, state, section, template, parameters) { }
        private SkillTextRule(EffectContentType content, OverrideContentType modifier, bool state, EffectType section, string template, string[] parameters)
        {
            Content = content; Modifier = modifier; State = state; Section = section; Template = template; Count = parameters.Length;
            string pattern = Regex.Escape(template.TrimEnd('。'));
            for (int i = 0; i < parameters.Length; i++)
                pattern = pattern.Replace(Regex.Escape("{" + i + "}"), "(?<p" + i + ">" + parameters[i] + ")");
            Pattern = "^" + pattern + "。?$";
        }
        public string Write(List<string> parameters)
        {
            string[] p = Args(parameters, Count, Content != EffectContentType.None ? Content.ToString() : Modifier.ToString());
            string result = string.Format(CultureInfo.InvariantCulture, Template, p.Cast<object>().ToArray());
            Match match = M(result, Pattern);
            Require(match.Success && Enumerable.Range(0, Count).All(i => match.Groups["p" + i].Value == p[i]));
            if (Modifier == OverrideContentType.ThisSkillCriticalRange)
                Require(Number(p[0], 1, "下限") <= Number(p[1], 1, "上限") && Number(p[1], 1, "上限") <= 100);
            if (Content == EffectContentType.ExecuteAfterAttackDamage) Require(Number(p[0], 0, "HP割合") <= 100);
            if (Content == EffectContentType.ExtraCountersPerSkill) Require(Number(p[2], 1, "同一対象回数") <= Number(p[1], 1, "宣言回数"));
            if (Content == EffectContentType.UnmodifiableMaximumResourceDamage) Require(Number(p[1], 1, "最大値割合") <= 100);
            return result;
        }
    }

    private const string B2Name = @"[^〈〉《》\r\n。]+?";
    private const string B2Formula = @"[^〈〉《》\r\n。]+?";
    private const string B2Positive = @"[1-9][0-9]*";
    private const string B2Factor = @"(?:[1-9][0-9]*(?:\.[0-9]+)?|0\.[0-9]*[1-9][0-9]*)";
    private const string B2Resource = @"[A-Z]+";
    private const string B2Signed = @"[+-][0-9]+";

    private static readonly SkillTextRule[] BeastSecondRules =
    {
        new SkillTextRule(EffectContentType.RequireBattleAreaCategory, false, EffectType.Passive,
            "エリアに〈{0}〉が含まれない戦闘エリアへ移動する事が出来ない。", B2Name),
        new SkillTextRule(EffectContentType.RestrictBattleTurnDeclarations, false, EffectType.Passive,
            "戦闘開始{0}ターン目は〈{1}〉または〈{2}〉を含むスキルのみ宣言可能。", B2Positive, B2Name, B2Name),
        new SkillTextRule(EffectContentType.SuppressTraitEffects, false, EffectType.Passive,
            "特性《{0}》による効果をすべて無効にする。", B2Name),
        new SkillTextRule(EffectContentType.DefineOwnAttribute, false, EffectType.Passive,
            "自身の最も高い基礎属性値と同じ属性を「自属性」とする。"),
        new SkillTextRule(EffectContentType.CounterFumbleState, false, EffectType.Passive,
            "自身は〈{0}〉に対するカウンターの発動ロールにファンブルした時、{2}ターンの間《{1}》状態になる。", B2Name, B2Name, B2Positive),
        new SkillTextRule(EffectContentType.OptionalCategoryImmunity, false, EffectType.Passive,
            "自身への〈{0}〉による効果を無効に出来る。", B2Name),
        new SkillTextRule(EffectContentType.OwnAttributeHitState, false, EffectType.Active,
            "この攻撃が命中した時、自属性が{0}属性なら対象に《{1}》状態を{2}ターン付与する。", "火|水|風|電|冷|土", B2Name, B2Positive),
        new SkillTextRule(EffectContentType.OwnAttributeHitRemoval, false, EffectType.Active,
            "この攻撃が命中した時、自属性が{0}属性なら対象の《{1}》状態を解除する。", "火|水|風|電|冷|土", B2Name),
        new SkillTextRule(EffectContentType.MissingOwnAttributePower, false, EffectType.Active,
            "自属性が存在しない時、属性威力と自属性の追加効果を適用せず、{0}のみの威力となる。", B2Formula),
        new SkillTextRule(EffectContentType.RandomAttackDeclaration, true, EffectType.Passive,
            "自身の攻撃スキルをランダムに一つ選ぶ、その後自身を含む選択可能な対象をランダムに選択して発動する。"),
        new SkillTextRule(EffectContentType.RandomTargetSelection, true, EffectType.Passive,
            "自身は対象を選択する際、自身を除く選択可能な対象全てからランダムに{0}体決定する必要があります。", B2Positive),
        new SkillTextRule(EffectContentType.PreventResourceRecovery, true, EffectType.Passive,
            "自身は{0}を回復することができなくなる。", B2Resource),
        new SkillTextRule(EffectContentType.ProhibitActionCategory, true, EffectType.Passive,
            "自身は〈{0}〉による効果を発動できなくなる。", B2Name),
        new SkillTextRule(EffectContentType.ProhibitActionCategory, false, EffectType.Passive,
            "自身は〈{0}〉による効果を発動できなくなる。", B2Name),
        new SkillTextRule(EffectContentType.ProhibitDeclarationCategory, false, EffectType.Passive,
            "自身は〈{0}〉を宣言する事が出来ない。", B2Name),
        new SkillTextRule(EffectContentType.ExtraCountersPerSkill, false, EffectType.Passive,
            "戦闘中{0}度だけ、自身は1つのスキルに対して{1}回カウンター効果を宣言する事が出来る。この宣言は同一対象へ{2}回発動可能。", B2Positive, B2Positive, B2Positive),
        new SkillTextRule(EffectContentType.NextDeclarationExtraTargets, false, EffectType.Active,
            "次の{0}回の宣言時に、《{1}》状態を必要としない{2}体以上のキャラクターを選択するスキルは追加で任意のキャラクターを{3}体選択に加えてもよい。", B2Positive, B2Name, B2Positive, B2Positive),
        new SkillTextRule(EffectContentType.AdvanceDestinySpike, true, EffectType.Passive,
            "自身が宣言するあらゆるスキルは現在より{0}段階高いディスティニースパイクの効果を適用できる。", B2Positive),
        new SkillTextRule(EffectContentType.PreventSelectionOutsideMelee, true, EffectType.Passive,
            "自身と同じ接近グループ以外からの〈{0}〉の選択対象にならなくなる。", B2Name),
        new SkillTextRule(EffectContentType.ApplierTurnAttack, true, EffectType.Passive,
            "付与者のターン開始時に付与者がこの状態の対象を{0}の威力で攻撃する。（付与者の現在のステータスを参照）", B2Formula),
        new SkillTextRule(EffectContentType.ExecuteAfterAttackDamage, false, EffectType.Active,
            "この攻撃によるダメージ処理の後、対象のHPがHP最大値の{0}%以下なら対象のHPを{1}にして《{2}》状態にする。", @"[0-9]+", @"[0-9]+", B2Name),

        new SkillTextRule(OverrideContentType.CappedStackPower, true, EffectType.Passive,
            "自身の〈{0}〉による威力は×{1}倍されてゆく。（最大×{2}倍）", B2Name, B2Formula, B2Factor),
        new SkillTextRule(OverrideContentType.AreaStatMultiplier, false, EffectType.Passive,
            "エリアに〈{0}〉が含まれている時、[{1}]が×{2}倍される。", B2Name, @"[^\[\]\r\n]+", B2Factor),
        new SkillTextRule(OverrideContentType.AreaActionResult, false, EffectType.Passive,
            "エリアに〈{0}〉が含まれている時、[{1}]による行為判定の達成値は{2}される。", B2Name, @"[^\[\]\r\n]+", B2Signed),
        new SkillTextRule(OverrideContentType.ApplierAttributePower, true, EffectType.Passive,
            "自身は〈{0}〉かつ〈{1}〉に{2}の属性威力が追加される。（加算値は威力の算出時に付与したキャラクターのステータスを参照する）", B2Name, B2Name, B2Formula),
        new SkillTextRule(OverrideContentType.RaceCheckResult, false, EffectType.Passive,
            "キャラクター〈{0}〉に対する[{1}]による行為判定を行う際、達成値が{2}される。", B2Name, @"[^\[\]\r\n]+", B2Signed),
        new SkillTextRule(OverrideContentType.OwnAttributeBonus, false, EffectType.Passive,
            "自属性の種族属性ボーナスは+{0}%加算される。", B2Positive),
        new SkillTextRule(OverrideContentType.OwnAttributeWeaknessMultiplier, false, EffectType.Passive,
            "その弱点の属性威力による被ダメージは×{0}倍される。", B2Formula),
        new SkillTextRule(OverrideContentType.NamedSkillRollResult, false, EffectType.Passive,
            "スキル《{0}》による発動ロールの達成値が{1}される。", B2Name, B2Signed),
        new SkillTextRule(OverrideContentType.NextActionPower, false, EffectType.Active,
            "次の{0}回、自身の〈{1}〉による威力が{2}倍される。", B2Positive, B2Name, B2Factor),
        new SkillTextRule(OverrideContentType.PowerByAlliedStateTypes, false, EffectType.Passive,
            "自身が〈{0}〉を発動する際、対象に自身を含む味方によって付与されてる状態の数に応じて威力は×{1}倍される。", B2Name, B2Formula),
        new SkillTextRule(OverrideContentType.ThisSkillCriticalRange, false, EffectType.Active,
            "このスキルによる発動ロールは、ダイスが{0}～{1}の時にクリティカルとなる。", B2Positive, B2Positive),
        new SkillTextRule(OverrideContentType.CriticalThresholdDelta, false, EffectType.Active,
            "このスキルによるクリティカル目は{0}される。", B2Signed),
        new SkillTextRule(OverrideContentType.ItemUseCostDelta, false, EffectType.Passive,
            "〈{0}〉を使用する際、消費する{1}は{2}される。", B2Name, B2Resource, B2Signed),
        new SkillTextRule(OverrideContentType.PhaseDamageMultiplier, false, EffectType.Passive,
            "{0}フェーズ及び{1}フェーズ、自身が受ける被ダメージは×{2}倍される。", B2Name, B2Name, B2Factor)
    };

    private static bool ReadBeastSecondRule(SkillBody skill, StateDefinition state, EffectType type, string text)
    {
        foreach (SkillTextRule rule in SemanticRules().Where(x => x.State == (state != null)))
        {
            Match m = M(text, rule.Pattern);
            if (!m.Success) continue;
            Require(type == rule.Section);
            string[] p = Enumerable.Range(0, rule.Count).Select(i => m.Groups["p" + i].Value).ToArray();
            rule.Write(p.ToList());
            if (rule.Content != EffectContentType.None)
            {
                if (state != null) AddStateEffect(state, NoTriggers(), Content(rule.Content, p));
                else ExtendedEffect(skill, type, Content(rule.Content, p));
            }
            else AddOverride(state != null ? state.Overrides : skill.Overrides, type, NoTriggers(), Change(rule.Modifier, p));
            return true;
        }
        return false;
    }
    private static string BeastSecondRuleContentText(EffectContent content, bool state)
    {
        if (content == null) return null;
        SkillTextRule rule = SemanticRules().FirstOrDefault(x => x.State == state && x.Content != EffectContentType.None && x.Content == content.Type);
        return rule == null ? null : rule.Write(content.Parameters);
    }
    private static string BeastSecondRuleOverrideText(OverrideDefinition effect, OverrideContent content, bool state)
    {
        if (content == null || effect == null) return null;
        SkillTextRule rule = SemanticRules().FirstOrDefault(x => x.State == state && x.Modifier != OverrideContentType.None && x.Modifier == content.Type);
        if (rule == null) return null;
        Require(effect.Type == rule.Section && (state ? Matches(effect.Triggers, Trigger(TriggerTiming.Always)) : effect.Triggers.Count == 0));
        return rule.Write(content.Parameters);
    }
    private static IEnumerable<SkillTextRule> SemanticRules() { return BeastSecondRules.Concat(NonlivingRules).Concat(GodRules); }
}
