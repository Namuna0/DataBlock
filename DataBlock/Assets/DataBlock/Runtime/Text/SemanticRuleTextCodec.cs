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

    private const string RuleNamePattern = @"[^〈〉《》\r\n。]+?";
    private const string RuleFormulaPattern = @"[^〈〉《》\r\n。]+?";
    private const string RulePositivePattern = @"[1-9][0-9]*";
    private const string RuleFactorPattern = @"(?:[1-9][0-9]*(?:\.[0-9]+)?|0\.[0-9]*[1-9][0-9]*)";
    private const string RuleResourcePattern = @"[A-Z]+";
    private const string RuleSignedPattern = @"[+-][0-9]+";

    private static readonly SkillTextRule[] SemanticTextRules =
    {
        new SkillTextRule(EffectContentType.RequireBattleAreaCategory, false, EffectType.Passive,
            "エリアに〈{0}〉が含まれない戦闘エリアへ移動する事が出来ない。", RuleNamePattern),
        new SkillTextRule(EffectContentType.RestrictBattleTurnDeclarations, false, EffectType.Passive,
            "戦闘開始{0}ターン目は〈{1}〉または〈{2}〉を含むスキルのみ宣言可能。", RulePositivePattern, RuleNamePattern, RuleNamePattern),
        new SkillTextRule(EffectContentType.SuppressTraitEffects, false, EffectType.Passive,
            "特性《{0}》による効果をすべて無効にする。", RuleNamePattern),
        new SkillTextRule(EffectContentType.DefineOwnAttribute, false, EffectType.Passive,
            "自身の最も高い基礎属性値と同じ属性を「自属性」とする。"),
        new SkillTextRule(EffectContentType.CounterFumbleState, false, EffectType.Passive,
            "自身は〈{0}〉に対するカウンターの発動ロールにファンブルした時、{2}ターンの間《{1}》状態になる。", RuleNamePattern, RuleNamePattern, RulePositivePattern),
        new SkillTextRule(EffectContentType.OptionalCategoryImmunity, false, EffectType.Passive,
            "自身への〈{0}〉による効果を無効に出来る。", RuleNamePattern),
        new SkillTextRule(EffectContentType.OwnAttributeHitState, false, EffectType.Active,
            "この攻撃が命中した時、自属性が{0}属性なら対象に《{1}》状態を{2}ターン付与する。", "火|水|風|電|冷|土", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(EffectContentType.OwnAttributeHitRemoval, false, EffectType.Active,
            "この攻撃が命中した時、自属性が{0}属性なら対象の《{1}》状態を解除する。", "火|水|風|電|冷|土", RuleNamePattern),
        new SkillTextRule(EffectContentType.MissingOwnAttributePower, false, EffectType.Active,
            "自属性が存在しない時、属性威力と自属性の追加効果を適用せず、{0}のみの威力となる。", RuleFormulaPattern),
        new SkillTextRule(EffectContentType.RandomAttackDeclaration, true, EffectType.Passive,
            "自身の攻撃スキルをランダムに一つ選ぶ、その後自身を含む選択可能な対象をランダムに選択して発動する。"),
        new SkillTextRule(EffectContentType.RandomTargetSelection, true, EffectType.Passive,
            "自身は対象を選択する際、自身を除く選択可能な対象全てからランダムに{0}体決定する必要があります。", RulePositivePattern),
        new SkillTextRule(EffectContentType.PreventResourceRecovery, true, EffectType.Passive,
            "自身は{0}を回復することができなくなる。", RuleResourcePattern),
        new SkillTextRule(EffectContentType.ProhibitActionCategory, true, EffectType.Passive,
            "自身は〈{0}〉による効果を発動できなくなる。", RuleNamePattern),
        new SkillTextRule(EffectContentType.ProhibitActionCategory, false, EffectType.Passive,
            "自身は〈{0}〉による効果を発動できなくなる。", RuleNamePattern),
        new SkillTextRule(EffectContentType.ProhibitDeclarationCategory, false, EffectType.Passive,
            "自身は〈{0}〉を宣言する事が出来ない。", RuleNamePattern),
        new SkillTextRule(EffectContentType.ExtraCountersPerSkill, false, EffectType.Passive,
            "戦闘中{0}度だけ、自身は1つのスキルに対して{1}回カウンター効果を宣言する事が出来る。この宣言は同一対象へ{2}回発動可能。", RulePositivePattern, RulePositivePattern, RulePositivePattern),
        new SkillTextRule(EffectContentType.NextDeclarationExtraTargets, false, EffectType.Active,
            "次の{0}回の宣言時に、《{1}》状態を必要としない{2}体以上のキャラクターを選択するスキルは追加で任意のキャラクターを{3}体選択に加えてもよい。", RulePositivePattern, RuleNamePattern, RulePositivePattern, RulePositivePattern),
        new SkillTextRule(EffectContentType.AdvanceDestinySpike, true, EffectType.Passive,
            "自身が宣言するあらゆるスキルは現在より{0}段階高いディスティニースパイクの効果を適用できる。", RulePositivePattern),
        new SkillTextRule(EffectContentType.PreventSelectionOutsideMelee, true, EffectType.Passive,
            "自身と同じ接近グループ以外からの〈{0}〉の選択対象にならなくなる。", RuleNamePattern),
        new SkillTextRule(EffectContentType.ApplierTurnAttack, true, EffectType.Passive,
            "付与者のターン開始時に付与者がこの状態の対象を{0}の威力で攻撃する。（付与者の現在のステータスを参照）", RuleFormulaPattern),
        new SkillTextRule(EffectContentType.ExecuteAfterAttackDamage, false, EffectType.Active,
            "この攻撃によるダメージ処理の後、対象のHPがHP最大値の{0}%以下なら対象のHPを{1}にして《{2}》状態にする。", @"[0-9]+", @"[0-9]+", RuleNamePattern),

        new SkillTextRule(OverrideContentType.CappedStackPower, true, EffectType.Passive,
            "自身の〈{0}〉による威力は×{1}倍されてゆく。（最大×{2}倍）", RuleNamePattern, RuleFormulaPattern, RuleFactorPattern),
        new SkillTextRule(OverrideContentType.AreaStatMultiplier, false, EffectType.Passive,
            "エリアに〈{0}〉が含まれている時、[{1}]が×{2}倍される。", RuleNamePattern, @"[^\[\]\r\n]+", RuleFactorPattern),
        new SkillTextRule(OverrideContentType.AreaActionResult, false, EffectType.Passive,
            "エリアに〈{0}〉が含まれている時、[{1}]による行為判定の達成値は{2}される。", RuleNamePattern, @"[^\[\]\r\n]+", RuleSignedPattern),
        new SkillTextRule(OverrideContentType.ApplierAttributePower, true, EffectType.Passive,
            "自身は〈{0}〉かつ〈{1}〉に{2}の属性威力が追加される。（加算値は威力の算出時に付与したキャラクターのステータスを参照する）", RuleNamePattern, RuleNamePattern, RuleFormulaPattern),
        new SkillTextRule(OverrideContentType.RaceCheckResult, false, EffectType.Passive,
            "キャラクター〈{0}〉に対する[{1}]による行為判定を行う際、達成値が{2}される。", RuleNamePattern, @"[^\[\]\r\n]+", RuleSignedPattern),
        new SkillTextRule(OverrideContentType.OwnAttributeBonus, false, EffectType.Passive,
            "自属性の種族属性ボーナスは+{0}%加算される。", RulePositivePattern),
        new SkillTextRule(OverrideContentType.OwnAttributeWeaknessMultiplier, false, EffectType.Passive,
            "その弱点の属性威力による被ダメージは×{0}倍される。", RuleFormulaPattern),
        new SkillTextRule(OverrideContentType.NamedSkillRollResult, false, EffectType.Passive,
            "スキル《{0}》による発動ロールの達成値が{1}される。", RuleNamePattern, RuleSignedPattern),
        new SkillTextRule(OverrideContentType.NextActionPower, false, EffectType.Active,
            "次の{0}回、自身の〈{1}〉による威力が{2}倍される。", RulePositivePattern, RuleNamePattern, RuleFactorPattern),
        new SkillTextRule(OverrideContentType.PowerByAlliedStateTypes, false, EffectType.Passive,
            "自身が〈{0}〉を発動する際、対象に自身を含む味方によって付与されてる状態の数に応じて威力は×{1}倍される。", RuleNamePattern, RuleFormulaPattern),
        new SkillTextRule(OverrideContentType.ThisSkillCriticalRange, false, EffectType.Active,
            "このスキルによる発動ロールは、ダイスが{0}～{1}の時にクリティカルとなる。", RulePositivePattern, RulePositivePattern),
        new SkillTextRule(OverrideContentType.CriticalThresholdDelta, false, EffectType.Active,
            "このスキルによるクリティカル目は{0}される。", RuleSignedPattern),
        new SkillTextRule(OverrideContentType.ItemUseCostDelta, false, EffectType.Passive,
            "〈{0}〉を使用する際、消費する{1}は{2}される。", RuleNamePattern, RuleResourcePattern, RuleSignedPattern),
        new SkillTextRule(OverrideContentType.PhaseDamageMultiplier, false, EffectType.Passive,
            "{0}フェーズ及び{1}フェーズ、自身が受ける被ダメージは×{2}倍される。", RuleNamePattern, RuleNamePattern, RuleFactorPattern),

        new SkillTextRule(EffectContentType.ReviveAfterBattle, false, EffectType.Passive,
            "戦闘終了後、味方が全滅していなければ、自身は《{0}》状態を解除してHP{1}の状態で復活する。", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(EffectContentType.AbsentResource, false, EffectType.Passive,
            "自身は{0}が存在しない。", RuleResourcePattern),
        new SkillTextRule(EffectContentType.StateImmunity, false, EffectType.Passive,
            "自身は《{0}》状態にならない。", RuleNamePattern),
        new SkillTextRule(EffectContentType.StateOnIncomingCritical, false, EffectType.Passive,
            "自身は発動ロールクリティカルによる〈{0}〉を受けた時《{1}》状態になる。", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(EffectContentType.EnvironmentPermanentState, false, EffectType.Passive,
            "自身は環境に《{0}》を含む場合、永続的に《{1}》状態となる。", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(EffectContentType.SuppressSourceDamageAndState, false, EffectType.Passive,
            "〈{0}〉によるダメージを受けた時、《{1}》による被ダメ上昇と《{2}》状態を受けなくなる。", RuleNamePattern, RuleNamePattern, RuleNamePattern),
        new SkillTextRule(EffectContentType.SuppressSourceEnvironmentState, false, EffectType.Passive,
            "環境に《{0}》を含む時、《{1}》による《{2}》状態を受けなくなる。", RuleNamePattern, RuleNamePattern, RuleNamePattern),
        new SkillTextRule(EffectContentType.HitRaceState, false, EffectType.Active,
            "この攻撃が命中した時、対象の種族が〈{0}〉のいずれかなら、《{1}》状態を{2}ターン付与する。", RuleNamePattern, RuleNamePattern, RulePositivePattern),
        new SkillTextRule(EffectContentType.WeaponHitRaceState, true, EffectType.Passive,
            "自身の〈{0}〉による武器攻撃が命中した時、対象の種族が〈{1}〉のいずれかなら、《{2}》状態を{3}ターン付与する。", RuleNamePattern, RuleNamePattern, RuleNamePattern, RulePositivePattern),
        new SkillTextRule(EffectContentType.OptionalStackOnSelected, false, EffectType.Passive,
            "自身が選択対象になる時、相手に《{0}》スタックを{1}付与しても良い。", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(EffectContentType.OptionalRecoveryIfStackExists, false, EffectType.Passive,
            "自身のターン開始時、《{0}》スタックが付与されているキャラクターが1体でもいれば{1}を{2}回復しても良い。", RuleNamePattern, RuleResourcePattern, RuleFormulaPattern),
        new SkillTextRule(EffectContentType.AdditionalRaceSkills, false, EffectType.Passive,
            "選択可能な種族スキルを追加で{0}つ習得する事が出来る。", RulePositivePattern),
        new SkillTextRule(EffectContentType.IncomingCategoriesState, false, EffectType.Passive,
            "自身が〈{0}〉のいずれかを受けた時、自身は{2}ターンの間《{1}》状態になる。", RuleNamePattern, RuleNamePattern, RulePositivePattern),
        new SkillTextRule(OverrideContentType.PowerAgainstState, false, EffectType.Active,
            "この攻撃の前から対象が《{0}》状態の時、威力は×{1}倍される。", RuleNamePattern, RuleFactorPattern),
        new SkillTextRule(OverrideContentType.WeaponPowerAgainstState, true, EffectType.Passive,
            "自身の〈{0}〉による武器攻撃の前から対象が《{1}》状態の時、その攻撃の威力は×{2}倍される。", RuleNamePattern, RuleNamePattern, RuleFactorPattern),
        new SkillTextRule(OverrideContentType.IncomingCategoriesDamageMultiplier, false, EffectType.Passive,
            "自身が〈{0}〉のいずれかを受けた時、被ダメージが×{1}倍される。", RuleNamePattern, RuleFactorPattern),

        new SkillTextRule(OverrideContentType.EquippedSkillLimit, false, EffectType.Passive,
            "自身が装備できるスキルは{0}個までとなる。", RuleFormulaPattern),
        new SkillTextRule(EffectContentType.DeferMissingResourceCost, false, EffectType.Passive,
            "このスキルの消費リソースの{0}が足りない時、可能な限り支払い、不足分を自身の次のターン開始時に消費する事でアクティブ効果を発動できる。", RuleResourcePattern),
        new SkillTextRule(OverrideContentType.ThisSkillStackCost, false, EffectType.Passive,
            "このスキルの消費{1}は自身の《{0}》スタック1つにつき-{2}される。（最低消費{3}）", RuleNamePattern, RuleResourcePattern, RulePositivePattern, RulePositivePattern),
        new SkillTextRule(OverrideContentType.ThisSkillStackPower, false, EffectType.Passive,
            "自身の《{0}》スタックが{1}以上の場合、このスキルの威力は{2}倍される。", RuleNamePattern, RulePositivePattern, RuleFactorPattern),
        new SkillTextRule(EffectContentType.RemoveAllStacks, false, EffectType.Active,
            "このスキルの攻撃後、自身の《{0}》スタックは全て除去される。", RuleNamePattern),
        new SkillTextRule(EffectContentType.TimedTargetStacks, false, EffectType.Active,
            "対象は{2}ターンの間《{0}》スタックを{1}得る。", RuleNamePattern, RulePositivePattern, RulePositivePattern),
        new SkillTextRule(EffectContentType.UseStateBoundCharacter, false, EffectType.Passive,
            "《{0}》状態のキャラクターは、そのプレイヤーがその場にいる場合、この状態の効果を無視して使用できる。ただし、この状態は解除できない。", RuleNamePattern),
        new SkillTextRule(EffectContentType.SetTargetResourceAndStates, false, EffectType.Active,
            "対象の{0}を{1}にして《{2}》及び《{3}》状態にする。", RuleResourcePattern, @"[0-9]+", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(EffectContentType.SucceedFailedActivationRoll, false, EffectType.Counter,
            "発動ロールを自動成功にする。"),
        new SkillTextRule(EffectContentType.UnmodifiableMaximumResourceDamage, false, EffectType.Active,
            "対象へ対象の{0}最大値の{1}%のダメージを与える。（この攻撃による威力及び被ダメージは、防御点、効果、状態によって増加も軽減もされない）", RuleResourcePattern, RulePositivePattern),
        new SkillTextRule(OverrideContentType.AllBonusStatsMultiplier, true, EffectType.Passive,
            "自身のすべての能力値Bは×{0}倍される。", RuleFactorPattern)
    };

    private static bool ReadSemanticRule(SkillBody skill, StateDefinition state, EffectType type, string text)
    {
        foreach (SkillTextRule rule in SemanticTextRules.Where(x => x.State == (state != null)))
        {
            Match m = M(text, rule.Pattern);
            if (!m.Success) continue;
            Require(type == rule.Section);
            string[] p = Enumerable.Range(0, rule.Count).Select(i => m.Groups["p" + i].Value).ToArray();
            rule.Write(p.ToList());
            if (rule.Content != EffectContentType.None)
            {
                if (state != null) AddStateEffect(state, NoTriggers(), Content(rule.Content, p));
                else AddSkillEffect(skill, type, Content(rule.Content, p));
            }
            else AddOverride(state != null ? state.Overrides : skill.Overrides, type, NoTriggers(), Change(rule.Modifier, p));
            return true;
        }
        return false;
    }
    private static string SemanticRuleContentText(EffectContent content, bool state)
    {
        if (content == null) return null;
        SkillTextRule rule = SemanticTextRules.FirstOrDefault(x => x.State == state && x.Content != EffectContentType.None && x.Content == content.Type);
        return rule == null ? null : rule.Write(content.Parameters);
    }
    private static string SemanticRuleOverrideText(OverrideDefinition effect, OverrideContent content, bool state)
    {
        if (content == null || effect == null) return null;
        SkillTextRule rule = SemanticTextRules.FirstOrDefault(x => x.State == state && x.Modifier != OverrideContentType.None && x.Modifier == content.Type);
        if (rule == null) return null;
        Require(effect.Type == rule.Section && (state ? Matches(effect.Triggers, Trigger(TriggerTiming.Always)) : effect.Triggers.Count == 0));
        return rule.Write(content.Parameters);
    }
}
