using System;
using System.Collections.Generic;
using System.Linq;

public static partial class SkillTextConverter
{
    private static readonly SkillTextRule[] ConditionRules =
    {
        new SkillTextRule(ConditionType.StatCheckFumbled, "{0}Bによる行為判定をファンブルした時", RuleNamePattern),
        new SkillTextRule(ConditionType.SeasonalHarvest, "季節が{0}及び{1}の〈{2}〉で収穫を行う時", @"[春夏秋冬]", @"[春夏秋冬]", RuleNamePattern),
        new SkillTextRule(ConditionType.SelectDangerCategory, "【危険度】☆{0}以下の〈{1}〉を選択", RulePositivePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.SelectOwnedEquipmentSlots, "所持している{0}かつ{1}の装備を{2}つ選択", @"[^〈〉《》【】、,。\s]+", @"[^〈〉《》【】、,。\s]+", RulePositivePattern),
        new SkillTextRule(ConditionType.TargetedByCategoryRequirement, "自身が〈{0}〉または〈{1}〉を条件として選択対象にされた時", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.OwnBattleCheckFailed, "戦闘中に自身の行為判定が失敗した時"),
        new SkillTextRule(ConditionType.OwnActivatedCategory, "自身が発動した〈{0}〉に対して", RuleNamePattern),
        new SkillTextRule(ConditionType.NpcSkillActivation, "NPCキャラクターのスキル発動時"),
        new SkillTextRule(ConditionType.OwnCategoryRollSuccess, "自身の〈{0}〉による発動ロールが成功した時", RuleNamePattern),
        new SkillTextRule(ConditionType.OwnCategoryActivation, "自身が〈{0}〉を発動する時", RuleNamePattern),
        new SkillTextRule(ConditionType.CategoryUsingThisWeapon, "この武器の選択を条件に〈{0}〉を発動する時", RuleNamePattern),
        new SkillTextRule(ConditionType.HitWithThisWeapon, "この武器による自身の〈{0}〉が命中した時", RuleNamePattern),
        new SkillTextRule(ConditionType.ApplyCategoryToCharacter, "自身がキャラクターに〈{0}〉を付与した時", RuleNamePattern),
        new SkillTextRule(ConditionType.RepeatedWeaponDamageOwnTurn, "自身のターン中、この武器によって{0}回以上同一のキャラクターに対してダメージを与えた時", RulePositivePattern),
        new SkillTextRule(ConditionType.ApplyStateToEnemy, "自身が敵キャラクターに対して状態を付与した時"),
        new SkillTextRule(ConditionType.AnyResourceFractionAtMost, "何れかのキャラクターの{0}が最大値の{1}%以下になった時", RuleResourcePattern, @"(?:100|[1-9]?[0-9])"),
        new SkillTextRule(ConditionType.MeleeStateTargetOfAction, "自身と同じ接近グループの《{0}》状態のキャラクターが〈{1}〉の対象になった時", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.BattleTurnNumber, "戦闘開始{0}ターン目", RulePositivePattern),
        new SkillTextRule(ConditionType.IncomingActiveDamage, "自身がアクティブ効果によってダメージを受けた時"),
        new SkillTextRule(ConditionType.SelectCharacterStateByCategory, "キャラクターの状態〈{0}〉を{1}つ選択", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.IncomingCategoryOnSelf, "自身に対して発動した〈{0}〉へ", RuleNamePattern),
        new SkillTextRule(ConditionType.SummonerSelectedTarget, "発動者が選択した対象へ発動する"),
        new SkillTextRule(ConditionType.TargetOfCategory, "自身が〈{0}〉の対象になった時", RuleNamePattern),
        new SkillTextRule(ConditionType.LockWeaponForBattle, "戦闘中同じ武器を選択し続ける必要がある"),
        new SkillTextRule(ConditionType.AutoEnemyOnDeclaration, "〈{0}〉を宣言した敵キャラクターを対象に自動発動", RuleNamePattern),
        new SkillTextRule(ConditionType.EquipmentSlotCount, "対象の装備部位が{0}か所の場合", RulePositivePattern),
        new SkillTextRule(ConditionType.NextUsesMustPickRandomEnemy, "次の{0}回スキル、選択可能な敵が{1}体以上居る場合、ランダムに敵を対象に選択しなくてはならない", RulePositivePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.SelectAlliedStates, "味方キャラクターに付与された状態を{0}つ選択", RulePositivePattern),
        new SkillTextRule(ConditionType.EndBattleInState, "自身が《{0}》状態になり戦闘を終えた後", RuleNamePattern),
        new SkillTextRule(ConditionType.AutoEnemyExceptTwoCategories, "〈{0}〉を宣言した〈{1}〉でも〈{2}〉でもない敵キャラクターを対象に自動発動", RuleNamePattern, RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.SelectMeleeExceptCategory, "自身と同じ接近グループの〈{0}〉ではないキャラクターを{1}体選択", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.TargetIncomingSource, "自身への〈{0}〉発動者へ", RuleNamePattern),
        new SkillTextRule(ConditionType.IncomingCategoryOrElementalAction, "自身が〈{0}〉、または{1}属性威力を含む〈{2}〉を受けた時", RuleNamePattern, @"[炎火水電冷風土光闇雷]", RuleNamePattern),
        new SkillTextRule(ConditionType.KnowAnyNamedSkills, "《{0}》の内いずれかを習得", RuleNamePattern),
        new SkillTextRule(ConditionType.OwnRaceNotName, "《{0}》ではないこと", RuleNamePattern),
        new SkillTextRule(ConditionType.AutoRandomEnemyOwnTurn, "自身のターン開始時ランダムな敵キャラクターを対象に自動発動"),
        new SkillTextRule(ConditionType.SelectAnyWeaponAndMelee, "装備している任意の武器と自身と同じ接近グループのキャラクターを{0}体選択", RulePositivePattern),
        new SkillTextRule(ConditionType.ExcludeItemsUsedBySkill, "{0}度でも《{1}》によって宣言されているアイテムは選択できない", RulePositivePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.TournamentChampion, "第{0}回{1}トーナメント優勝", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.SelectInCategory, "〈{0}〉を{1}つ選択", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.PartyJoinTurnEnemy, "自身がパーティーに合流したターンに敵キャラクターを{0}体選択", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectInstalledEntities, "設置されている任意の数の《{0}》を対象に", RuleNamePattern),
        new SkillTextRule(ConditionType.SelectTwoCategoriesUpTo, "〈{0}〉及び〈{1}〉を{2}つまで選択", RuleNamePattern, RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.RaceCategoryOrName, "自身の種族が〈{0}〉のいずれか、または《{1}》であること", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.AreaSecurityAtMost, "治安が{0}以下のエリアで", RuleNamePattern),
        new SkillTextRule(ConditionType.WhenSummoned, "召喚時"),
        new SkillTextRule(ConditionType.PetTurn, "自身が〈{0}〉及び所持している〈{1}〉のターンに", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.StartAlliedTurn, "味方のターン開始時"),
        new SkillTextRule(ConditionType.MoveDestinationCharacter, "自由な移動先対象キャラクターを{0}体選択", RulePositivePattern),
        new SkillTextRule(ConditionType.DistinctAlliedRange, "味方キャラクターを{0}体以上、{1}体まで選択（重複不可）", RulePositivePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.AutoWhenSummonerUsesSkill, "召喚者がスキルを発動した時に自動発動"),
        new SkillTextRule(ConditionType.RoleplayOrder, "ロールプレイによって行動を指定された時"),
        new SkillTextRule(ConditionType.AutoEnemyNotMeleeWithTarget, "対象と《{0}》状態ではない敵キャラクターの宣言に対して自動発動", RuleNamePattern),
        new SkillTextRule(ConditionType.SelectMeleeAttackTargetUnspecified, "自身と同じ接近グループの攻撃対象キャラクターを選択"),
        new SkillTextRule(ConditionType.EquipByClassDescription, "《{0}》クラス記載", RuleNamePattern),
        new SkillTextRule(ConditionType.AutomaticAcquisition, "自動習得"),
        new SkillTextRule(ConditionType.SpendBeyondCurrent, "自身の宣言したスキルの消費{0}が現在の{1}を上回っている時", RuleResourcePattern, RuleResourcePattern),
        new SkillTextRule(ConditionType.CategoryCostBeyondCurrent, "自身の宣言した〈{0}〉の消費{1}が現在の{2}を上回っている時", RuleNamePattern, RuleResourcePattern, RuleResourcePattern),
        new SkillTextRule(ConditionType.TargetedByMeleeCharacter, "自身と同じ接近グループのキャラクターから選択対象になった時"),
        new SkillTextRule(ConditionType.EquippedNamedItem, "《{0}》の装備", RuleNamePattern),
        new SkillTextRule(ConditionType.SelectedSkillUsable, "選択スキルの宣言条件と必要リソースを満たすこと"),
        new SkillTextRule(ConditionType.WearerUsesAsWeapon, "自身を装備しているキャラクターが自身を武器として選択して〈{0}〉を宣言した時", RuleNamePattern),
        new SkillTextRule(ConditionType.OutsideAreaCategory, "エリアが〈{0}〉ではない", RuleNamePattern),
        new SkillTextRule(ConditionType.DeclareWithWeaponCategories, "〈{0}〉を選択してスキルを宣言する時", RuleNamePattern),
        new SkillTextRule(ConditionType.PartyLimitPerBattle, "このスキルは戦闘中パーティー全体で{0}回のみ使用できる", RulePositivePattern),
        new SkillTextRule(ConditionType.InAreaCategory, "〈{0}〉エリアで", RuleNamePattern),
        new SkillTextRule(ConditionType.SelectAllMeleeEnemies, "自身と同じ接近グループの敵全てを選択"),
        new SkillTextRule(ConditionType.SelectOptionalOwnedGear, "所持している装備を{0}つ選択または無しを選択", RulePositivePattern),
        new SkillTextRule(ConditionType.OwnRaceIsOneOf, "自身の種族が《{0}》のいずれかであること", RuleNamePattern),
        new SkillTextRule(ConditionType.SelectEnemyCount, "敵キャラクターを{0}体選択", RulePositivePattern),
        new SkillTextRule(ConditionType.DuringBattle, "戦闘中"),
        new SkillTextRule(ConditionType.MinimumOwnResource, "自身の{0}が{1}以上の時", RuleResourcePattern, @"[0-9]+"),
        new SkillTextRule(ConditionType.OwnCategoryCountered, "自身の宣言した〈{0}〉に対してカウンターを発動された時", RuleNamePattern),
        new SkillTextRule(ConditionType.AutoEnemyFromEitherAction, "〈{0}〉または〈{1}〉を発動した敵キャラクターを対象に自動発動", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.AutoEnemyFromAnyAction, "〈{0}〉いずれかを発動した敵キャラクターを対象に自動発動", RuleNamePattern),
        new SkillTextRule(ConditionType.AutoCharacterFromAction, "〈{0}〉を発動したキャラクターに対して自動発動", RuleNamePattern),
        new SkillTextRule(ConditionType.SingleSkillFromCategory, "〈{0}〉を複数習得することはできない", RuleNamePattern),
        new SkillTextRule(ConditionType.LearnFromBook, "書物《{0}》から習得可能", RuleNamePattern),
        new SkillTextRule(ConditionType.SelectElements, "属性を{0}つ選択", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectMinimumAllies, "味方キャラクターを{0}体以上", RulePositivePattern),
        new SkillTextRule(ConditionType.EitherElementMinimum, "{0}属性または{1}属性{2}以上", RuleNamePattern, RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.SelectAlliesUpTo, "味方キャラクターを{0}体まで選択", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectOtherAlliesUpTo, "自身を除く、味方キャラクターを{0}体まで選択", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectWithoutStateCategory, "〈{0}〉状態ではないキャラクターを{1}体選択", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.SelectCharacterStateOfCategories, "キャラクターに付与されている〈{0}〉及び〈{1}〉{2}つを選択", RuleNamePattern, RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.AnyoneReceivesBoth, "キャラクターが〈{0}〉及び〈{1}〉を受けた時", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.FromStateRecipient, "《{0}》状態の対象から", RuleNamePattern),
        new SkillTextRule(ConditionType.OneTimeLimit, "{0}度だけ", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectPriorRoleplayAcrossAreas, "別エリアの過去に{0}度でもRPをしたことのあるキャラクターを{1}体選択", RulePositivePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.NamedCharacterLevel, "キャラクター{0}がレベル{1}到達", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.LearnByClassDescription, "クラス《{0}》の説明の通りに習得可能", RuleNamePattern),
        new SkillTextRule(ConditionType.ChooseBySharedAndSelectedRace, "{0}と選択種族の説明の通りに選択可能", RuleNamePattern),
        new SkillTextRule(ConditionType.ChooseByRaceDescription, "{0}の説明の通りに選択可能", RuleNamePattern),
        new SkillTextRule(ConditionType.ChosenTrait, "《{0}》選択時", RuleNamePattern),
        new SkillTextRule(ConditionType.HasRaceCategory, "自身の種族が〈{0}〉であること", RuleNamePattern),
        new SkillTextRule(ConditionType.LacksRaceCategory, "自身が〈{0}〉以外である事", RuleNamePattern),
        new SkillTextRule(ConditionType.HasSkill, "《{0}》の習得", RuleNamePattern),
        new SkillTextRule(ConditionType.KnowsLifePath, "ライフパス《{0}》の習得", RuleNamePattern),
        new SkillTextRule(ConditionType.AcquireWithLifePath, "ライフパス《{0}》選択時に効果に従って習得可能", RuleNamePattern),
        new SkillTextRule(ConditionType.KnowsClass, "クラス《{0}》の習得", RuleNamePattern),
        new SkillTextRule(ConditionType.HasTitle, "称号《{0}》の習得", RuleNamePattern),
        new SkillTextRule(ConditionType.PossessesItem, "《{0}》を所有している事", RuleNamePattern),
        new SkillTextRule(ConditionType.PossessesCategory, "〈{0}〉を所持している事", RuleNamePattern),
        new SkillTextRule(ConditionType.SelectOwnedCategory, "所持している任意の〈{0}〉アイテムを{1}つ指定", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.SelectOwnedEitherCategory, "所持している任意の〈{0}〉または〈{1}〉アイテムを{2}つ指定", RuleNamePattern, RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.SelectNamedEquipment, "装備している《{0}》を{1}つ選択", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.EquippedCount, "〈{0}〉を{1}つ装備", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.EquippedAnyCategories, "〈{0}〉のいずれかを装備", RuleNamePattern),
        new SkillTextRule(ConditionType.EquippedEitherCategory, "〈{0}〉または〈{1}〉を装備", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.SelectPairedEquipment, "装備している〈{0}〉と〈{1}〉を選択", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.BothHandsFree, "両手が素手"),
        new SkillTextRule(ConditionType.StandbyPhase, "スタンバイフェーズ"),
        new SkillTextRule(ConditionType.InstalledHousing, "この〈{0}〉がハウジングとして設置されている事", RuleNamePattern),
        new SkillTextRule(ConditionType.JointDeclarers, "{0}人で宣言する事", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectRestrictedOwnedItem, "〈{0}〉を除く、任意のキャラクターの所有している〈{1}〉を{2}つ選択", RuleNamePattern, RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.SelectCharacterItem, "任意のキャラクターの所有している〈{0}〉アイテムを{1}つ選択", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.SelectOwnedItem, "自身の所持品を{0}つ選択", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectOwnStates, "自身の状態を{0}つ選択", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectCharacterStates, "キャラクター{0}体の状態を{1}つ選択", RulePositivePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.SelectAllAllies, "味方キャラクターを全体を選択"),
        new SkillTextRule(ConditionType.SelectAllies, "味方キャラクターを{0}体選択", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectDistinctAlliesUpTo, "味方キャラクターを{0}体まで選択（重複不可）", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectOtherAlliesRange, "自身を除く味方キャラクターを{0}体以上{1}体まで選択（重複不可）", RulePositivePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.MinimumAlliesIncludingSelf, "自身を含む味方が{0}体以上の時", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectMeleeStateCharacter, "自身と同じ接近グループの《{0}》状態キャラクターを{1}体選択", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.SelectStateCharactersDistinct, "《{0}》状態キャラクターを{1}体まで選択（重複不可）", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.SelectMeleeAttackTarget, "自身と同じ接近グループの攻撃対象キャラクターを{0}体選択", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectDistinctMeleeAttackTargets, "自身と同じ接近グループの攻撃対象キャラクターを{0}体まで選択（重複不可）", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectStateReceiver, "《{0}》状態の対象に対して", RuleNamePattern),
        new SkillTextRule(ConditionType.SelectWearer, "自身を装備しているキャラクターを対象に"),
        new SkillTextRule(ConditionType.SelectOwnStateRecipient, "自身から《{0}》を付与されたキャラクターを対象に", RuleNamePattern),
        new SkillTextRule(ConditionType.AreaUseLimit, "エリアに付き{0}回だけ", RulePositivePattern),
        new SkillTextRule(ConditionType.AfterBattleTurn, "戦闘開始から{0}ターン目以降", RulePositivePattern),
        new SkillTextRule(ConditionType.HighestAttributeMinimum, "自身の何れかの属性値が{0}を以上", RulePositivePattern),
        new SkillTextRule(ConditionType.PastStackSpend, "戦闘中《{0}》スタックを{1}以上消費している場合", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.OwnSkillActivated, "自身が《{0}》を発動した時", RuleNamePattern),
        new SkillTextRule(ConditionType.AfterOwnCategoryDeclared, "自身が〈{0}〉を宣言した直後", RuleNamePattern),
        new SkillTextRule(ConditionType.CounterToCategory, "自身の〈{0}〉に対してカウンターを発動された時", RuleNamePattern),
        new SkillTextRule(ConditionType.CounterCategoryToCategory, "自身の〈{0}〉に対して〈{1}〉を発動された時", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.OwnActionInvalidated, "自身の〈{0}〉が対象の〈{1}〉によって無効化された時", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.OwnRollTiming, "自身が{0}ロールを行う時", RuleNamePattern),
        new SkillTextRule(ConditionType.ReceivedState, "自身が状態を受けた時"),
        new SkillTextRule(ConditionType.StateFromRoll, "自身に{0}ロールによる状態が付与された時", RuleNamePattern),
        new SkillTextRule(ConditionType.StateBuffOnStat, "自身の[{0}]を変化させるバフを受けている時", RuleNamePattern),
        new SkillTextRule(ConditionType.ReceivesAnyOfTwoActions, "〈{0}〉または〈{1}〉を受けた対象に対して", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.SelectActionReceiverCount, "〈{0}〉を受けたキャラクターを{1}体選択", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.ActionActivatedByCharacter, "キャラクターが〈{0}〉を発動した時", RuleNamePattern),
        new SkillTextRule(ConditionType.OtherCharacterTargeted, "自身以外のキャラクターが〈{0}〉の対象になった時", RuleNamePattern),
        new SkillTextRule(ConditionType.AnyActivationSucceeded, "キャラクターが発動ロールに成功した時"),
        new SkillTextRule(ConditionType.SelectItemUser, "アイテムを使用したキャラクターを{0}体選択", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectAttackedStateHolder, "〈{0}〉を受けた《{1}》状態の対象に対して", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.SelectTwoHandEquipment, "装備している装備部位が両手の〈{0}〉を{1}つ選択", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.SelectDeclaredBattleSkill, "戦闘中に宣言されたあらゆるスキルを選択"),
        new SkillTextRule(ConditionType.SpendChosenResource, "{0}を任意の分消費して", RuleResourcePattern),
        new SkillTextRule(ConditionType.SpendChosenStacks, "《{0}》スタックを任意の分消費して", RuleNamePattern),
        new SkillTextRule(ConditionType.CostByLearnedCategoryCount, "決意[習得済の〈{0}〉の数+{1}]×{2}消費", RuleNamePattern, @"[0-9]+", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectedPetSkillReaction, "自身が〈{0}〉及び所持している〈{1}〉のスキル宣言時", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.MinimumHelpingMembers, "{0}人以上のパーティーメンバーにRPとして作業を手伝って貰う必要がある", RulePositivePattern),
        new SkillTextRule(ConditionType.SourceEvent, "{0}{1}より", RuleNamePattern, @"[0-9]{4}"),
        new SkillTextRule(ConditionType.SelectInventoryCategory, "所持している〈{0}〉を指定", RuleNamePattern),
        new SkillTextRule(ConditionType.OwnStatePresent, "自身が《{0}》状態", RuleNamePattern),
        new SkillTextRule(ConditionType.PossessesAnySkillCategory, "任意の〈{0}〉を習得しているキャラクターを選択", RuleNamePattern),
        new SkillTextRule(ConditionType.NormalFailureInCategory, "〈{0}〉のよる発動ロールに通常失敗した時", RuleNamePattern),
        new SkillTextRule(ConditionType.OnCraftDeclaration, "アイテムを生産する〈{0}〉を宣言した時", RuleNamePattern),
        new SkillTextRule(ConditionType.SelectNamedSkillSource, "{0}の発動時", RuleNamePattern),
        new SkillTextRule(ConditionType.ChoosePartyInBattle, "戦闘中のプレイヤーパーティーを選択して発動可能"),
        new SkillTextRule(ConditionType.OpponentTurnEndTarget, "敵のターン終了直前にキャラクターを{0}体選択", RulePositivePattern),
        new SkillTextRule(ConditionType.SourceSkillSpike, "《{0}》{1}rd以上", RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.SelectActionAndStateUser, "〈{0}〉及びバフ付与を受けた対象に対して", RuleNamePattern),
        new SkillTextRule(ConditionType.SelectOwnedCategoryAnyAndWeapon, "所持している任意の数の〈{0}〉及び〈{1}〉を選択", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.KnownSkillAndOtherCategory, "《{0}》とそれ以外の《{1}》を{2}つ習得している事", RuleNamePattern, RuleNamePattern, RulePositivePattern),
        new SkillTextRule(ConditionType.SelectTargetAndElement, "任意の対象{0}体と属性B指定", RulePositivePattern),
        new SkillTextRule(ConditionType.AlliedActionTarget, "〈{0}〉の選択対象になった味方キャラクターに対して", RuleNamePattern),
        new SkillTextRule(ConditionType.IsStateCategoryTarget, "〈{0}〉状態のキャラクターに対して", RuleNamePattern),
        new SkillTextRule(ConditionType.RandomEnemyWithRepeats, "敵キャラクターからランダムに{0}体まで選択（重複する）", RulePositivePattern),
        new SkillTextRule(ConditionType.SelectCharacterUsingAction, "〈{0}〉を発動したキャラクターに対して", RuleNamePattern),
        new SkillTextRule(ConditionType.ChooseOwnedPetAction, "自身が〈{0}〉及び所持している〈{1}〉を選択", RuleNamePattern, RuleNamePattern),
        new SkillTextRule(ConditionType.IncomingStateApplication, "対象が状態付与を含む効果を受けた時、発動可能"),
    };
    private static readonly Dictionary<ConditionType, SkillTextRule> ConditionRulesByKind = ConditionRules.ToDictionary(r => r.ConditionKind);

    private static ConditionEntry ReadConditionRule(string text)
    {
        foreach (var rule in ConditionRules)
        {
            var args = rule.Read(text.Trim().TrimEnd('、'));
            if (args != null) return Condition(rule.ConditionKind, args);
        }
        return null;
    }

    private static string NormalizeConditionPhrasing(string text)
    {
        text = RegexReplace(text.Trim(), @"[,、]?\s*宣言可能。?\s*(?=$|[（(,、]|または)", "");
        text = RegexReplace(text, @"(選択|指定)して(?=$|[。、,（(）)]|または| または )", "$1");
        text = RegexReplace(text, @"時に(?=$|[。、,）)]| または )", "時");
        text = RegexReplace(text, @"選択。\s*(?=[（(]重複)", "選択");
        text = RegexReplace(text, @"所持している〈([^〉]+)〉を([0-9]+)つ選択(?:して)?", "所持している任意の〈$1〉アイテムを$2つ指定");
        text = text.Replace("（戦闘中同じ武器を選択し続ける必要がある。）", "戦闘中同じ武器を選択し続ける必要がある");
        text = text.Trim().Replace("宣言する事、対象を", "宣言する事, キャラクターを")
            .Replace("宣言する事, 対象を", "宣言する事, キャラクターを")
            .Replace("自身以外のキャラクターを", "自身を除くキャラクターを")
            .Replace("〉いずれかを装備", "〉のいずれかを装備")
            .Replace("〉の装備", "〉を装備").Replace("　の習得", "の習得");
        text = RegexReplace(text, @"キャラクター([^《》、,]+?)が([0-9]+)レベル到達", "キャラクター$1がレベル$2到達");
        text = RegexReplace(text, @"(^|, )対象(?:キャラクター)?を(?=[0-9]+体)", "$1キャラクターを");
        text = RegexReplace(text, @"(〈[^〉]+〉を除く、任意のキャラクターの所有している〈[^〉]+〉)アイテムを", "$1を");
        text = RegexReplace(text, @"〈([^〉]+)〉を装備している事。?$", "〈$1〉を装備");
        text = RegexReplace(text, @"(^|, )自身が〈([^〉]+)〉である(?:こと|事)。?(?=$|[,、])", "$1自身の種族が〈$2〉であること");
        text = RegexReplace(text, @"^同じ接近グループの", "自身と同じ接近グループの");
        text = RegexReplace(text, @"([0-9]+体(?:まで)?選択)して発動可能。?", "$1");
        text = RegexReplace(text, @"1日([0-9]+)回(?:まで)?。?(?=$|[,、])", "1日$1回まで");
        text = RegexReplace(text, @"戦闘中([0-9]+)度だけ[。、](?=\s*(?:キャラクター|自身|$))", "戦闘中$1度だけ, ");
        text = RegexReplace(text, @"〈([^〉]+)〉と〈([^〉]+)〉を1つ選択", "〈$1〉と〈$2〉を選択");
        text = RegexReplace(text, @"1日([0-9]+)度(?:まで|だけ)", "1日$1回まで");
        text = text.Replace("味方全体を対象に", "味方キャラクターを全体を選択");
        text = RegexReplace(text, @"種族が《([^《》、]+(?:》、《[^《》、]+)+)》のいずれか。?", "自身の種族が《$1》のいずれかであること");
        text = text.Replace("》、《", ", ");
        var namedSkills = M(text, @"((?:《[^《》]+》,\s*)+《[^《》]+》)の内いずれかを習得");
        if (namedSkills.Success)
        {
            string choices = string.Join(", ", AllMatches(namedSkills.Groups[1].Value, @"《([^》]+)》").Cast<System.Text.RegularExpressions.Match>().Select(x => x.Groups[1].Value));
            text = text.Replace(namedSkills.Value, "《" + choices + "》の内いずれかを習得");
        }
        var largeCost = M(text, @"決意([0-9]+)万([0-9]{1,4})消費");
        if (largeCost.Success)
        {
            long cost = (long)Number(largeCost.Groups[1].Value, 1, "万") * 10000 + Number(largeCost.Groups[2].Value, 0, "端数");
            text = text.Replace(largeCost.Value, "決意" + cost + "消費");
        }
        text = RegexReplace(text, @"決意消費([0-9]+)", "決意$1消費");
        text = RegexReplace(text, @"ライフパス《([^》]+)》を習得。?", "ライフパス《$1》の習得");
        text = RegexReplace(text, @"《([^》]+)》を習得している事。?", "《$1》の習得");
        text = RegexReplace(text, @"スタンバイフェーズ(?:時|に)(?=\s*[,、。]|$)", "スタンバイフェーズ");
        text = RegexReplace(text, @"戦闘中([0-9]+)回だけ", "戦闘中$1回まで");
        text = RegexReplace(text, @"1日([0-9]+)回(?:だけ|のみ)", "1日$1回まで");
        text = RegexReplace(text, @"戦闘中に自身の行為判定が失敗した時[、,]\s*([0-9]+)回だけ", "戦闘中に自身の行為判定が失敗した時, 戦闘中$1回まで");
        text = RegexReplace(text, @"〈([^〉]+)〉が含まれるエリアで", "〈$1〉エリアで");
        text = RegexReplace(text, @"自身が〈([^〉]+)〉を宣言した時", "自身が〈$1〉を宣言した直後");
        text = RegexReplace(text, @"(最大値の[0-9]+%以下になった時)、発動可能", "$1");
        return text.Replace("である事。", "であること").Replace("片手が素手。", "片手が素手").TrimEnd('。');
    }
}
