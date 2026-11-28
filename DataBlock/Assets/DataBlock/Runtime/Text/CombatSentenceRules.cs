public static partial class SkillTextConverter
{
    private const string ElementSetPattern = @"[炎火水電冷氷風土地光闇雷](?:かつ[炎火水電冷氷風土地光闇雷])*";
    private const string OtherAttackScopePattern = @"全ての敵キャラクター|自身を除く全てのエンティティ";
    private const string AttackScopePattern = @"対象|" + OtherAttackScopePattern;
    private const string CategoryListPattern = @"[^〈〉《》、,。\s]+(?:,\s*[^〈〉《》、,。\s]+)*";

    private static void RegisterCombatActionRules(RuleSection rules)
    {
        rules.Add(EffectContentType.AttackChosenCharacters, AttackParts.Power, "キャラクターを{0}体選択して{1}の威力で攻撃する。", RulePositivePattern, ArithmeticPattern);
        rules.Add(EffectContentType.RepeatAttackByActivationResult, "発動ロールの結果に応じて対象を連続攻撃する。");
        rules.Add(EffectContentType.SelfElementDamageAndPersistentStates, "さらに自身は{0}の{1}属性のダメージを受ける、永続的に《{2}》かつ《{3}》状態になる。", ArithmeticPattern, ElementSetPattern, RuleNamePattern, RuleNamePattern);
        rules.Add(EffectContentType.RandomCharacterAttack, AttackParts.Power, "ランダムなキャラクターに{0}の威力で攻撃する。", ArithmeticPattern);
        rules.Add(EffectContentType.RepeatedRandomResourceDamage, "{0}回連続ランダムなキャラクターに{1}の{2}ダメージを与える。（重複する）", RulePositivePattern, ArithmeticPattern, RuleResourcePattern);
        rules.Add(EffectContentType.RemoveTargetFromParty, "対象をパーティーから離脱させ、新しいパーティとして扱う。");
        rules.Add(EffectContentType.LeavePartyAndLoseProgress, "対象はパーティーを外れ、進行ロールが-{0}される。", RulePositivePattern);
        rules.Add(EffectContentType.ReduceMaxResourcesExceptCategories, "〈{0}〉を除くランダムなキャラクターの最大{1}と{2}を最大値の{3}%減少させる。", CategoryListPattern, RuleResourcePattern, RuleResourcePattern, RulePositivePattern);
        rules.Add(EffectContentType.RequireEnemyDeclaration, "敵キャラクターは〈{0}〉または〈{1}〉を宣言しなければいけない。", RuleNamePattern, RuleNamePattern);
        rules.Add(EffectContentType.GiveDeclarerStacksOnSuccess, "成功した時、宣言者は《{0}》スタックを{1}獲得する。", RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.NoEffect, "何もなし。");
        rules.Add(EffectContentType.RecoverPerTargetWhenResourceLow, "この効果の対象に{0}が{1}以下のキャラクターが含まれる場合、対象1体につき自身の{2}と{3}を{4}回復する。", RuleResourcePattern, @"-?[0-9]+", RuleResourcePattern, RuleResourcePattern, ArithmeticPattern);
        rules.Add(EffectContentType.ScaleResourceDamageAtTargetThreshold, "対象の{0}が{1}以下の場合、この攻撃による{2}ダメージは×{3}倍される。", RuleResourcePattern, @"-?[0-9]+", RuleResourcePattern, RuleFactorPattern);
        rules.Add(EffectContentType.RecoverWhenEffectCausesState, "この効果によって対象が《{0}》状態になった時、自身の{1}を{2}回復する。", RuleNamePattern, RuleResourcePattern, ArithmeticPattern);
        rules.Add(EffectContentType.CombinedPowerAttack, AttackParts.Power | AttackParts.AttributePower, "{0}を{1}の威力と{2}の{3}属性威力で攻撃する。", AttackScopePattern, ArithmeticPattern, ArithmeticPattern, ElementSetPattern);
        rules.Add(EffectContentType.AttackIncomingAttacker, AttackParts.Power, "自身を攻撃した対象を{0}の威力で攻撃する。", ArithmeticPattern);
        rules.Add(EffectContentType.IndividualTargetAttack, AttackParts.Power, "対象個別に{0}の威力で攻撃する。", ArithmeticPattern);
        rules.Add(EffectContentType.RepeatedPowerAttack, AttackParts.Power, "対象を{0}回連続で{1}の威力で攻撃する。", RulePositivePattern, ArithmeticPattern);
        rules.Add(EffectContentType.RequireCounterCheck, "この効果に対してカウンター効果を宣言する場合、追加で{0} 目標値{1}を達成する必要がある。", ArithmeticPattern, ArithmeticPattern);
        rules.Add(EffectContentType.DamageActivationTargetPool, "発動対象に{0}の{1}ダメージを与える。", ArithmeticPattern, @"(?:釣り)?[A-Z]+");
        rules.Add(EffectContentType.SummonEnemy, "モンスター《{0}》を{1}体召喚する。", RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.SetRollOnInflictingState, "この攻撃によって対象を《{0}》状態にした場合、{1}ロールの結果は必ず《{2}》となる。", RuleNamePattern, RuleNamePattern, RuleNamePattern);
        rules.Add(EffectContentType.PreventAttackEffects, "この攻撃に対して、あらゆる効果を適用することはできない。");
        rules.Add(EffectContentType.NextAllyCategoryPower, "味方全員の次の{0}回の〈{1}〉による威力を×{2}倍にする。", RulePositivePattern, RuleNamePattern, RuleFactorPattern);
        rules.Add(EffectContentType.ApplyTimedStateAllEnemies, "全ての敵を{0}ターンの間《{1}》状態にする。", RulePositivePattern, RuleNamePattern);
        rules.Add(EffectContentType.AttackDefenseFactor, "この攻撃による被ダメージは防御点を×{0}倍して計算する。", RuleFactorPattern);
        rules.Add(EffectContentType.CancelSelfSelection, "自身への選択を無効にする。");
        rules.Add(EffectContentType.ReviveTargetWithResourcePercent, "対象1体の《{0}》状態を解除、最大{1}の{2}%{3}回復して蘇生する。", RuleNamePattern, RuleResourcePattern, RulePositivePattern, RuleResourcePattern);
        rules.Add(EffectContentType.AreaProgressRollDelta, "この戦闘エリア内でのプレイヤーキャラクターの次の{0}回の進行ロールは{1}されて扱われる。", RulePositivePattern, RuleSignedPattern);
        rules.Add(EffectContentType.RemoveRandomEnemyAppliedCategory, "対象の敵から付与されたランダムな〈{0}〉状態を解除する。", RuleNamePattern);
        rules.Add(EffectContentType.ResourcePowerAttack, AttackParts.Power, "対象を{0}の{1}威力で攻撃する。", ArithmeticPattern, RuleResourcePattern);
        rules.Add(EffectContentType.DelayedElementAttack, AttackParts.Power | AttackParts.AttributePower, "{0}ターン後、対象を{1}の{2}属性威力で攻撃する。", RulePositivePattern, ArithmeticPattern, ElementSetPattern);
        rules.Add(EffectContentType.FailTargetCategory, "対象の〈{0}〉発動を失敗にする。", RuleNamePattern);
        rules.Add(EffectContentType.AttackWithTargetMaxPercent, AttackParts.Power, "対象の{0}最大値の{1}%の威力で対象を攻撃する。", RuleResourcePattern, RulePositivePattern);
        rules.Add(EffectContentType.CancelIncomingActive, "自身へのアクティブ効果を無効にする。");
        rules.Add(EffectContentType.MoveTargetToOwnGroup, "対象を自身の接近グループへ移動させる。");
        rules.Add(EffectContentType.ReduceIncomingCategoryDamage, "自身への〈{0}〉による被ダメージを{1}軽減する。", RuleNamePattern, ArithmeticPattern);
        rules.Add(EffectContentType.RepeatedResourceDamage, "{0}回連続で対象へ{1}の{2}ダメージを与える。", RulePositivePattern, ArithmeticPattern, RuleResourcePattern);
        rules.Add(EffectContentType.AwardBattleTitle, "戦闘中の全てのキャラクターは称号《{0}》を獲得する。", RuleNamePattern);
        rules.Add(EffectContentType.CancelCategories, "〈{0}〉を無効にする。", CategoryListPattern);
        rules.Add(EffectContentType.ReviveNamedCharacter, "《{0}》は復活する。", RuleNamePattern);
        rules.Add(EffectContentType.AllyUsesSkill, "味方の《{0}》は《{1}》を発動する。", RuleNamePattern, RuleNamePattern);
        rules.Add(EffectContentType.InterceptActiveEffect, "対象の代わりにアクティブ効果を受ける。");
        rules.Add(EffectContentType.DefeatSelfOnInjuryRoll, "この攻撃によって{0}ロールを行った場合、自身は{1}0になり《{2}》状態となる。", RuleNamePattern, RuleResourcePattern, RuleNamePattern);
        rules.Add(EffectContentType.HealFractionOfDamage, "与ダメージの×{0}倍の{1}を回復する。", RuleFactorPattern, RuleResourcePattern);
        rules.Add(EffectContentType.EngageTargetWithAllAllies, "対象は味方キャラクター全てと同じ接近グループの《接近》状態になる。");
        rules.Add(EffectContentType.StealRandomItemBySize, "対象からサイズ{0}のアイテムを{1}つランダムで奪う。", @"小|中|大", RulePositivePattern);
        rules.Add(EffectContentType.NextTargetTurnResourceDelta, "対象は次の自身のターン開始時、{0}を{1}する。", RuleResourcePattern, RuleSignedPattern);
        rules.Add(EffectContentType.ElementAttackWithScope, AttackParts.Power | AttackParts.AttributePower, "{0}を{1}の{2}属性威力で攻撃する。", OtherAttackScopePattern, ArithmeticPattern, ElementSetPattern);
        rules.Add(EffectContentType.NegateIncomingCategory, "自身への〈{0}〉による効果を無効にする。", RuleNamePattern);
        rules.Add(EffectContentType.NextNamedSkillPower, "次の《{0}》の威力が×{1}倍される。", RuleNamePattern, RuleFactorPattern);
        rules.Add(EffectContentType.PreventResponsesForTargetState, "対象が《{0}》状態の時、この宣言に対して〈{1}〉と〈{2}〉は宣言できない。", RuleNamePattern, RuleNamePattern, RuleNamePattern);
        rules.Add(EffectContentType.PurgeOwnStates, "自身に付与された状態を全て解除する。");
        rules.Add(EffectContentType.RetaliateWithElement, AttackParts.Power | AttackParts.AttributePower, "自身へスキルを発動した対象を{0}の{1}属性威力で攻撃する。", ArithmeticPattern, ElementSetPattern);
        rules.Add(EffectContentType.DisarmRespondingWeapon, "このスキルに対して〈{0}〉を発動する場合、攻撃に選択した武器の耐久は0となり装備を解除する。", RuleNamePattern);
        rules.Add(EffectContentType.TimedTargetStateAtCheck, "達成値が{0}以上なら対象を{1}ターンの間《{2}》状態にする。", RulePositivePattern, RulePositivePattern, RuleNamePattern);
        rules.Add(EffectContentType.TargetStateAtCheck, "達成値が{0}以上の時、対象は《{1}》状態になる。", RulePositivePattern, RuleNamePattern);
        rules.Add(EffectContentType.PowerFactorAtTargetState, "対象が《{0}》状態なら威力は×{1}倍される。", RuleNamePattern, RuleFactorPattern);
        rules.Add(EffectContentType.SummonAndEngageCaster, "《{0}》を{1}体召喚して発動者と《接近》状態になる。", RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.ExitNextTurn, "次のターン開始時に自身は戦闘から離脱する。");
        rules.Add(EffectContentType.ResponseResourceCostDelta, "この攻撃に対してのカウンター効果を宣言する場合、消費リソースの{0}は{1}される。", RuleResourcePattern, RuleSignedPattern);
        rules.Add(EffectContentType.MixedPowerPoolDamage, AttackParts.Power | AttackParts.AttributePower, "{0}へ{1}の威力と{2}の{3}属性威力の{4}ダメージを与える。", @"発動対象|対象", ArithmeticPattern, ArithmeticPattern, ElementSetPattern, RuleResourcePattern);
        rules.Add(EffectContentType.RecoverAtEachTurnStart, "毎ターン開始時、自身の{0}を{1}回復する。", RuleResourcePattern, ArithmeticPattern);
        rules.Add(EffectContentType.SetResourceAtTurnStart, "自身はターン開始時に{0}が{1}になる。", RuleResourcePattern, ArithmeticPattern);
        rules.Add(EffectContentType.RemoveSelfNamedState, "自身の《{0}》状態は解除される。", RuleNamePattern);
        rules.Add(EffectContentType.EngageNamedCharacter, "自身は《{0}》と《接近》になる。", RuleNamePattern);
    }

    private static void RegisterCombatStateRules(RuleSection rules)
    {
        rules.Add(EffectContentType.TurnStartPoolDamage, "自身のターン開始時、{0}の{1}ダメージを受ける。", ArithmeticPattern, RuleResourcePattern);
        rules.Add(EffectContentType.ProhibitActiveExceptCategory, "〈{0}〉を除くアクティブ効果を持つスキルは使用不可能になる。", RuleNamePattern);
        rules.Add(EffectContentType.RandomOtherCharacterForCategory, "〈{0}〉の対象を自身を除く選択可能なキャラクターからランダムに決定する。", RuleNamePattern);
        rules.Add(EffectContentType.ClearStacksAfterEffect, "効果適用後このスタックは0になる。");
        rules.Add(EffectContentType.ProhibitAllActions, "自身は行動を宣言する事が出来ない。");
        rules.Add(EffectContentType.TurnStartResourceGainDelta, "ターン開始時に得る{0}は{1}減少する。", RuleResourcePattern, RuleSignedPattern);
        rules.Add(EffectContentType.PowerOffsetForBothCategories, "自身は〈{0}〉かつ〈{1}〉による威力は{2}される。", RuleNamePattern, RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.SelectedTargetMixedAttackAtTurnStart, "自身のターン開始時、付与時の選択対象を{0}の威力と{1}の{2}属性威力で攻撃する。", ArithmeticPattern, ArithmeticPattern, ElementSetPattern);
        rules.Add(EffectContentType.RetaliateWithElement, "自身へスキルを発動した対象を{0}の{1}属性威力で攻撃する。", ArithmeticPattern, ElementSetPattern);
        rules.Add(EffectContentType.ReduceIncomingCategoryDamage, "自身への〈{0}〉による被ダメージを{1}軽減する。", RuleNamePattern, ArithmeticPattern);
        rules.Add(EffectContentType.ProhibitCategoryDeclarations, "自身は〈{0}〉を宣言できなくなる。", CategoryListPattern);
        rules.Add(EffectContentType.PermanentlyProhibitCategories, "自身は永続的に〈{0}〉を宣言できなくなる。", CategoryListPattern);
        rules.Add(EffectContentType.DefeatAfterStateTurns, "状態付与から{0}ターン後自身の{1}は0となり、《{2}》状態となる。", RulePositivePattern, RuleResourcePattern, RuleNamePattern);
        rules.Add(EffectContentType.TurnStartDamageAtStacks, "スタックが{0}以上の時、ターン開始時{1}のダメージを受ける。", RulePositivePattern, ArithmeticPattern);
        rules.Add(EffectContentType.RandomSelectableEnemy, "自身は敵を対象選択する時、選択可能な全ての敵をランダムに選択する必要がある。");
        rules.Add(EffectContentType.BlockEquipmentSlotReference, "自身は装備部位が{0}または{1}のアイテムを参照することができなくなる。", @"[^〈〉《》【】、,。\s]+?", @"[^〈〉《》【】、,。\s]+?");
        rules.Add(EffectContentType.PreventCategoryTargeting, "自身はあらゆる対象から〈{0}〉の対象にならない。", RuleNamePattern);
        rules.Add(EffectContentType.SetOwnDefense, "自身の防御点は{0}になる。", ArithmeticPattern);
        rules.Add(EffectContentType.ProhibitEffectDeclaration, "自身は効果を宣言できなくなる。");
        rules.Add(EffectContentType.CategoryResultOffset, "自身の〈{0}〉の達成値は{1}される。", RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.EndOnGainingState, "この効果は自身が《{0}》状態になった時、解除される。", RuleNamePattern);
        rules.Add(EffectContentType.TurnStartResourceOffset, "自身のターン開始時{0}は{1}される。", RuleResourcePattern, RuleSignedPattern);
        rules.Add(EffectContentType.NamedSkillPowerFormulaFactor, "自身の《{0}》による威力は×{1}倍される。", RuleNamePattern, ArithmeticPattern);
        rules.Add(EffectContentType.EndWhenDamaged, "この状態はダメージを受けると解除される。");
    }

    private static void RegisterCombatOutcomeRules(RuleSection rules)
    {
        rules.Add(EffectContentType.ThresholdDeltaForTurn, "自身はこのターン、{0}になるダイスが{1}される。", @"クリティカル|ファンブル", RuleSignedPattern);
        rules.Add(EffectContentType.NegateOwnStates, "自身の《{0}》と《{1}》を無効にする。", RuleNamePattern, RuleNamePattern);
        rules.Add(EffectContentType.SwapResourcesWithTarget, "自身と対象の{0}と{1}を入れ替える。", RuleResourcePattern, RuleResourcePattern);
    }

    private static string NormalizeCombatEffect(string text)
    {
        string source = text;
        string clause = RegexReplace(text, @"^(?:さらに|更に)[、]?\s*", "");
        text = clause;
        text = RegexReplace(text, @"^(?:対象)?(?=[0-9]+(?:d[0-9]+)?[+*])(?=.+の(?:[^。]*属性)?威力で攻撃する。?$)", "対象を");
        text = RegexReplace(text, @"^対象は永続的に《([^》]+)》状態になる。?$", "対象を永続的に《$1》状態にする。");
        text = RegexReplace(text, @"^対象を([1-9][0-9]*)回連続(?=[0-9(\[])", "対象を$1回連続で");
        text = RegexReplace(text, @"^対象(?:1体)?を威力(" + ArithmeticPattern + @")の威力で攻撃する。?$", "対象を$1の威力で攻撃する。");
        text = RegexReplace(text, @"^対象を(" + ArithmeticPattern + @")の(" + ElementSetPattern + @")属性威力と(" + ArithmeticPattern + @")の威力で攻撃する。?$",
            "対象を$3の威力と$1の$2属性威力で攻撃する。");
        text = RegexReplace(text, @"^対象1体を(?=[0-9(].*属性威力で攻撃)", "対象を");
        if (!text.Contains("武器威力"))
        {
            text = RegexReplace(text, @"(?<=[火水冷氷土風電雷光闇])属性かつ(?=[火水冷氷土風電雷光闇]属性)", "かつ");
            text = RegexReplace(text, @"^対象を([0-9d.*+()\[\]/-]+)(?:の)?威力\+([0-9d.*+()\[\]/-]+)([火水冷氷土風電雷光闇])属性威力", "対象を$1の威力と$2の$3属性威力");
            text = RegexReplace(text, @"^対象を([0-9d.*+()\[\]/-]+)と(?=[0-9].*属性威力)", "対象を$1の威力と");
            text = RegexReplace(text, @"([0-9])([火水冷氷土風電雷光闇](?:かつ[火水冷氷土風電雷光闇])*属性威力)", "$1の$2");
        }
        text = RegexReplace(text, @"^(全ての敵キャラクター|自身を除く、?全てのエンティティ)に(?=[0-9].*属性威力)", "$1を");
        text = RegexReplace(text, @"^自身を除く、全てのエンティティを", "自身を除く全てのエンティティを");
        text = RegexReplace(text, @"属性威力で攻撃。?$", "属性威力で攻撃する。");
        text = RegexReplace(text, @"^対象を([0-9d.*+()/\-]+)で攻撃する。?$", "対象を$1の威力で攻撃する。");
        text = RegexReplace(text, @"^対象に(?=[0-9].*の威力で攻撃する)", "対象を");
        text = RegexReplace(text, @"^([0-9][0-9d.*+()/\-]*の威力で攻撃する。?)$", "対象を$1");
        text = RegexReplace(text, @"^攻撃対象に([0-9]+)回連続([0-9d.*+()/\-]+)の攻撃を行う。?$", "対象を$1回連続で$2の威力で攻撃する。");
        text = RegexReplace(text, @"^この攻撃による被ダメージは防御点を×([0-9]+),([0-9]+)倍して計算する。?$", "この攻撃による被ダメージは防御点を×$1.$2倍して計算する。");
        text = RegexReplace(text, @"^対象を(.+?の[A-Z]+ダメージを与える。?)$", "対象に$1");
        text = RegexReplace(text, @"^与えた([A-Z]+)ダメージ分、自身の([A-Z]+)を回復する。?$", "与えた$1ダメージの1倍の値の$2を回復する。");
        text = RegexReplace(text, @"^モンスター《([^》]+)》×([0-9]+)を召喚する。?$", "モンスター《$1》を$2体召喚する。");
        text = RegexReplace(text, @"^モンスター《([^》]+)》を召喚する。?$", "モンスター《$1》を1体召喚する。");
        text = RegexReplace(text, @"^《([^》]+)》を([0-9]+)体召喚する。?$", "モンスター《$1》を$2体召喚する。");
        text = RegexReplace(text, @"^〈([^〉]+)〉を除くアクティブ効果を持つスキルは以外は使用不可能になる。?$", "〈$1〉を除くアクティブ効果を持つスキルは使用不可能になる。");
        text = RegexReplace(text, @"^〈([^〉]+)〉の自身を除く選択可能なキャラクターからランダムに決定する。?$", "〈$1〉の対象を自身を除く選択可能なキャラクターからランダムに決定する。");
        text = RegexReplace(text, @"^自身(?:への|の|に)〈([^〉]+)〉(?:からの|による)被ダメージ[をは](.+?)軽減(?:される|する)。?$", "自身への〈$1〉による被ダメージを$2軽減する。");
        text = RegexReplace(text, @"^自身は〈([^〉]+)〉(?:及び|および)〈([^〉]+)〉が宣言できなくなる。?$", "自身は〈$1, $2〉を宣言できなくなる。");
        text = RegexReplace(text, @"^このスキルに対してカウンターを発動する事は出来ない。?$", "この効果はカウンター効果の対象にならない。");
        text = RegexReplace(text, @"^この攻撃は防御点による影響を受けない。?$", "この攻撃は対象の防御点の影響を受けない。");
        text = RegexReplace(text, @"^対象と同じ接近グループの《接近》状態となる。?$", "自身と対象は同じ接近グループの《接近》状態になる。");
        text = RegexReplace(text, @"^自身の《接近状態》は解除される。?$", "自身の《接近》状態を解除する。");
        text = RegexReplace(text, @"^(対象)の《([^》]+)》状態は解除される。?$", "$1の《$2》状態を解除する。");
        text = RegexReplace(text, @"^(対象|自身)への《([^》]+)》状態(?:は解除される|を解除する)。?$", "$1の《$2》状態を解除する。");
        text = RegexReplace(text, @"^対象の〈([^〉]+)〉および〈([^〉]+)〉を無効にする。?$", "対象の〈$1〉及び〈$2〉を無効にする。");
        text = RegexReplace(text, @"^対象へ〈([^〉]+)〉を無効にする。?$", "〈$1〉を無効にする。");
        text = RegexReplace(text, @"^この状態は戦闘終了時に解除される。?$", "戦闘終了時、この状態は解除される。");
        text = RegexReplace(text, @"^自身の次の([0-9]+回の〈[^〉]+〉による威力は)", "次の$1");
        text = RegexReplace(text, @"^自身は《([^》]+)》〈([^〉]+)〉状態になる。?$", "自身は《$1》〈$2〉：状態になる。");
        text = RegexReplace(text, @"^([0-9]+)ターンの間、自身は《([^》]+)》状態になる。?$", "自身は$1ターンの間《$2》状態になる。");
        text = RegexReplace(text, @"^対象(?:1体)?を([0-9d.*+()/\-]+)威力で攻撃する。?$", "対象を$1の威力で攻撃する。");
        text = RegexReplace(text, @"^対象(?:1体)?を威力([0-9d.*+()/\-]+)の(.+?属性威力)([0-9d.*+()/\-]+)で攻撃する。?$", "対象を$1の威力と$3の$2で攻撃する。");
        text = RegexReplace(text, @"^(?:この攻撃によるダメージは防御点を無視する|この攻撃は(?:対象の)?防御点(?:の影響を受けない|を無視する))。?$", "この攻撃は対象の防御点の影響を受けない。");
        text = RegexReplace(text, @"^この攻撃は対象の防御点の半分を無視する。?$", "この攻撃による被ダメージは防御点を×0.5倍して計算する。");
        text = RegexReplace(text, @"^自身は〈([^〉]+)〉による効果を受けない。?$", "自身への〈$1〉による効果を無効にする。");
        text = RegexReplace(text, @"^自身も〈([^〉]+)〉を宣言不可能。?$", "自身は〈$1〉を宣言できなくなる。");
        text = RegexReplace(text, @"^対象(?:へ|に)《([^》]+)》を付与する。?$", "対象に《$1》状態を付与する。");
        text = RegexReplace(text, @"^対象を永続的に《([^》]+)》状態を付与する", "対象を永続的に《$1》状態にする");
        text = RegexReplace(text, @"^対象を《([^》]+?)状態にする》[。]?$", "対象を《$1》状態にする。");
        text = RegexReplace(text, @"^自身の〈([^〉]+)〉状態を解除する。?$", "自身に付与されている〈$1〉状態を全て解除する。");
        text = RegexReplace(text, @"^自身は([A-Z]+)を回復する事が出来ない。?$", "自身は$1を回復することができなくなる。");
        text = RegexReplace(text, @"^自身が与えたダメージ分、自身の([A-Z]+)を回復する。?$", "与ダメージの×1倍の$1を回復する。");
        text = RegexReplace(text, @"^対象と同じ接近グループの《接近》状態になる。?$", "自身と対象は同じ接近グループの《接近》状態になる。");
        text = RegexReplace(text, @"^自身のHP及び([A-Z]+)ダメージを(.+?)軽減する。?$", "自身の被ダメージ及び$1ダメージを$2軽減する。");
        text = RegexReplace(text, @"^この攻撃に対してカウンター効果を発動できない。?$", "この効果はカウンター効果の対象にならない。");
        text = RegexReplace(text, @"対象の([A-Z]+)を(-[0-9]+)減少する。?$", "対象の$1を$2変化させる。");
        text = RegexReplace(text, @"^毎ターン開始時の([A-Z]+)は([0-9]+)になる。?$", "自身はターン開始時に$1が$2になる。");
        return text == clause ? source : text;
    }
}
