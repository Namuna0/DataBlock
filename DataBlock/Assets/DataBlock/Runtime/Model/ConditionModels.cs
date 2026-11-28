using System;
using System.Collections.Generic;

[Serializable]
public class ConditionSet
{
    // 全And条件 AND (Orが空、またはOr条件のいずれか)。空の集合は追加制約なし。
    public List<ConditionEntry> And = new List<ConditionEntry>();
    public List<ConditionEntry> Or = new List<ConditionEntry>();
}
/// <summary>習得・宣言・効果適用などの条件や対象の選び方を表す。人数、対象の状態、しきい値などは条件の引数で指定する。</summary>
public enum ConditionType
{
    /// <summary>指定リソースの最大値が必要量以上である。</summary>
    MinimumResourceCapacity = 400,
    /// <summary>対象の生年月日と出身地の知識を必要とする。</summary>
    KnowTargetBirthAndOrigin = 401,
    /// <summary>条件に指定した称号を選ぶ。</summary>
    SelectOwnedTitle = 402,
    /// <summary>宣言者の現在地が町である。</summary>
    InTownArea = 403,
    /// <summary>条件の種類が未指定であることを表す。</summary>
    None = 0,
    /// <summary>種族選択時に任意で習得できる。</summary>
    OptionalAtRaceSelection = 1,
    /// <summary>指定人数のキャラクターを選ぶ。人数の上限と重複の可否も指定できる。</summary>
    SelectCharacters = 2,
    /// <summary>行為を行ったのが自身、付与者、味方などのうち誰かを条件とする。</summary>
    ActionSource = 3,
    /// <summary>行為が指定カテゴリーに属することを条件とする。</summary>
    ActionCategory = 4,
    /// <summary>指定した相手との状態が解除された時に成立する。</summary>
    RelatedStateRemoved = 5,
    /// <summary>指定量のリソースを消費することを要求する。</summary>
    SpendResource = 6,
    /// <summary>装備中の指定カテゴリーの武器を指定数選ぶ。</summary>
    SelectEquippedWeapon = 7,
    /// <summary>自身と同じ接近グループから指定人数のキャラクターを選ぶ。</summary>
    SelectMeleeCharacters = 8,
    /// <summary>消費するアイテムを指定カテゴリーから指定数選ぶ。</summary>
    SelectConsumedItem = 9,
    /// <summary>選んだアイテム自体の宣言条件を満たすことを要求する。</summary>
    SelectedItemConditionsMet = 10,
    /// <summary>攻撃で指定の状態を付与したことを条件とする。</summary>
    AttackAppliedState = 11,
    /// <summary>自身が受けている指定カテゴリーの状態から指定数を選ぶ。</summary>
    SelectOwnState = 12,
    /// <summary>自身と指定の接近関係にあるキャラクターから、指定カテゴリーの行為を受けた時に成立する。</summary>
    ReceiveFromCharacter = 13,
    /// <summary>指定カテゴリーの装備を身に着けていることを要求する。</summary>
    EquippedCategory = 14,
    /// <summary>使用した武器のカテゴリーを条件とする。</summary>
    WeaponCategory = 15,
    /// <summary>その攻撃が武器攻撃であることを要求する。</summary>
    IsWeaponAttack = 16,
    /// <summary>発動ロールの成功を条件とする。</summary>
    RollSucceeded = 17,
    /// <summary>そのスキルが対象にダメージを与えなかった時に成立する。</summary>
    SkillDealtNoDamage = 18,
    /// <summary>戦闘中にその効果を適用できる回数を制限する。</summary>
    BattleEffectLimit = 19,
    /// <summary>自身がその効果に関わっていることを条件とする。</summary>
    EffectInvolvesSelf = 20,
    /// <summary>同じ接近グループにいることを要求する。</summary>
    RequiresSameMeleeGroup = 21,
    /// <summary>指定したカテゴリーの武器を条件の対象から除く。</summary>
    ExcludedWeaponCategories = 22,
    /// <summary>自身が指定の状態であることを要求する。</summary>
    OwnState = 23,

    /// <summary>複数の候補カテゴリーのいずれかに属する装備中の武器を指定数選ぶ。</summary>
    SelectEquippedWeaponFromCategories = 24,
    /// <summary>指定カテゴリーの行為の発動者または受け手と、自身との関係を条件とする。</summary>
    ActionTarget = 25,
    /// <summary>アクティブやカウンターなど、発動した効果の種類を条件とする。</summary>
    EffectKind = 26,
    /// <summary>スキルによる行為かどうかなど、行為の発生元を条件とする。</summary>
    ActionOrigin = 27,
    /// <summary>指定の能力値が必要値以上であることを要求する。</summary>
    MinimumStat = 28,
    /// <summary>ターンまたは日ごとの使用回数に上限を設ける。</summary>
    ActivationLimit = 29,
    /// <summary>敵または味方のターン終了時など、宣言できる時点を指定する。</summary>
    DeclarationTiming = 30,
    /// <summary>毎ターン開始時にランダムな敵を対象として自動発動する。</summary>
    AutomaticActivation = 31,
    /// <summary>発動ロールの通常失敗や、指定した追加ロールの出目など、判定の結果を条件とする。</summary>
    RollResult = 32,
    /// <summary>現在のリソース量を最大値に対する割合で判定する。</summary>
    ResourceRatio = 33,
    /// <summary>行為判定に使った能力値を条件とする。</summary>
    ActionStat = 34,
    /// <summary>誰のターンであるかを条件とする。</summary>
    TurnOwner = 35,

    /// <summary>種族選択時に自動で習得する。</summary>
    AutomaticAtRaceSelection = 36,
    /// <summary>指定したリソースの現在値をしきい値と比較する。</summary>
    ResourceValue = 37,
    /// <summary>属性に対応する状態のスタックが指定数たまるごとに成立する。</summary>
    MappedStateStackInterval = 38,
    /// <summary>装備してから指定日数以上が経過していることを要求する。</summary>
    MinimumEquippedDays = 39,
    /// <summary>指定キャラクターがパーティーにいることを要求する。</summary>
    InParty = 40, // actor
    /// <summary>指定キャラクターが食事効果を受けているかを判定する。</summary>
    HasMealEffect = 41, // actor, True|False
    /// <summary>現在が指定したターンであることを要求する。</summary>
    TurnNumber = 42, // 1-based turn number
    /// <summary>指定カテゴリーの行為をきっかけに、発動者や受け手を反応の対象とする。</summary>
    ReactionTarget = 43, // category, Self, Source | category, Ally, Receiver, Automatic
    /// <summary>発動・環境・製作など、行為判定を行う場面を条件とする。</summary>
    CheckContext = 44, // Activation | Environment | Crafting, equipment categories...
    /// <summary>戦闘のターン数が指定した開始ターンから終了ターンまでの範囲にある時に成立する。</summary>
    BattleTurnRange = 45, // first turn, last turn (inclusive)
    /// <summary>自身が指定カテゴリーのいずれかに属する状態を受けていることを要求する。</summary>
    OwnStateCategories = 46, // Any, categories... (not individual state names)
    /// <summary>発動したスキルを名前で指定する。</summary>
    ActionName = 47, // named skill, not a category
    /// <summary>攻撃に属性ボーナスに由来する威力が含まれていることを要求する。</summary>
    HasAttributeBonusPower = 48, // attribute-bonus-derived power exists on this attack
    /// <summary>自身のアクティブ効果に対してカウンターが行われた時に成立する。</summary>
    CounterToOwnActive = 49, // the triggering counter responds to this actor's active effect
    /// <summary>指定したエンカウントフェーズであることを要求する。</summary>
    EncounterPhase = 50,
    /// <summary>指定人数のキャラクターからアクティブ効果を持つスキルを指定数選ぶ。</summary>
    SelectCharacterSkill = 51, // character count, effect kind, skill count
    /// <summary>指定した種族カテゴリーを除いて対象を選ぶ。</summary>
    ExcludeTargetRaces = 52, // character count, excluded race categories...
    /// <summary>指定の状態スタックが必要数以上あるキャラクターを選ぶ。</summary>
    TargetStackMinimum = 53, // state, minimum, character count
    /// <summary>キャラクター以外も含めて、任意の対象を指定数選ぶ。</summary>
    SelectAnyTarget = 54, // count (including non-character roleplay targets)
    /// <summary>自身が指定の状態を受けていないことを要求する。</summary>
    WithoutOwnState = 55, // state
    /// <summary>戦闘中に指定状態の自身が行為判定へ反応できる回数と、その回数を戻す条件を定める。</summary>
    IncapacitatedCheckReaction = 56, // state, Battle, limit, AnyCharacterAction, reset action category
    /// <summary>種族を選択する時点で習得する。</summary>
    AtRaceSelection = 57, // unspecified optional/automatic acquisition
    /// <summary>指定カテゴリーの行為を受けるとリソースが指定値以下になる場合に成立する。</summary>
    LethalIncomingAction = 58, // resource, AtMost, threshold, action category
    /// <summary>消費リソースの上限と接近条件に合う装備スキルを指定数選ぶ。</summary>
    SelectEquipmentSkill = 59, // count, resource, AtMost, maximum cost, NoMeleeRequirement
    /// <summary>自身に向けて指定カテゴリーの行為を行った相手を対象とする。</summary>
    ActionTowardsSelf = 60, // action category, Source
    /// <summary>携帯している武器から選ぶ。指定に応じて素手も候補に含む。</summary>
    SelectCarriedWeapon = 61, // count, AllowUnarmed
    /// <summary>エリアが指定カテゴリーに属していないことを要求する。</summary>
    AreaWithoutCategory = 62, // excluded area category
    /// <summary>指定状態のキャラクターを、指定人数またはその人数を上限として選ぶ。</summary>
    SelectCharactersWithState = 63, // state, count, optional Exact|UpTo (omitted = Exact)
    /// <summary>敵が指定カテゴリーの行為を行った時、その敵を対象に自動発動する。</summary>
    AutomaticEnemyAction = 64, // triggering action category; target its enemy source
    /// <summary>自身に指定の状態が付与された時に成立する。</summary>
    OwnStateApplied = 65, // declaration on becoming this state, not while remaining in it
    /// <summary>自身を除いて指定人数のキャラクターを選ぶ。</summary>
    SelectCharactersExceptSelf = 66, // exact count
    /// <summary>指定の種族カテゴリーに属し、指定の特性を習得しているキャラクターを選ぶ。</summary>
    SelectRaceWithTrait = 67, // race category, acquired trait name, exact count
    /// <summary>自身の片手が空いていて素手であることを要求する。</summary>
    HasFreeHand = 68, // Self has at least one unequipped hand
    /// <summary>指定した名前のキャラクターが必要レベルに達した時に成立する。</summary>
    NamedCharacterLevel = 69,
    /// <summary>指定クラスの説明に従って習得できる。</summary>
    LearnByClassDescription = 70,
    /// <summary>共通の説明と選択した種族の説明に従って選択できる。</summary>
    ChooseBySharedAndSelectedRace = 71,
    /// <summary>指定された種族の説明に従って選択できる。</summary>
    ChooseByRaceDescription = 72,
    /// <summary>指定した特性を選んだ時に成立する。</summary>
    ChosenTrait = 73,
    /// <summary>自身の種族が指定カテゴリーに含まれることを要求する。</summary>
    HasRaceCategory = 74,
    /// <summary>自身の種族が指定カテゴリーに含まれないことを要求する。</summary>
    LacksRaceCategory = 75,
    /// <summary>指定スキルを習得済みであることを要求する。</summary>
    HasSkill = 76,
    /// <summary>指定ライフパスを習得済みであることを要求する。</summary>
    KnowsLifePath = 77,
    /// <summary>指定ライフパスを選択した時、その効果に従って習得できる。</summary>
    AcquireWithLifePath = 78,
    /// <summary>指定クラスを習得済みであることを要求する。</summary>
    KnowsClass = 79,
    /// <summary>指定の称号を取得していることを要求する。</summary>
    HasTitle = 80,
    /// <summary>指定名のアイテムを所有していることを要求する。</summary>
    PossessesItem = 81,
    /// <summary>指定カテゴリーのアイテムを所持していることを要求する。</summary>
    PossessesCategory = 82,
    /// <summary>自身の所持品から指定カテゴリーのアイテムを指定数選ぶ。</summary>
    SelectOwnedCategory = 83,
    /// <summary>自身の所持品から二つの指定カテゴリーのいずれかに属するアイテムを指定数選ぶ。</summary>
    SelectOwnedEitherCategory = 84,
    /// <summary>装備中の指定名のアイテムを指定数選ぶ。</summary>
    SelectNamedEquipment = 85,
    /// <summary>指定カテゴリーのアイテムを指定数装備していることを要求する。</summary>
    EquippedCount = 86,
    /// <summary>候補に挙げたカテゴリーのいずれかを装備していることを要求する。</summary>
    EquippedAnyCategories = 87,
    /// <summary>二つの指定カテゴリーのいずれかを装備していることを要求する。</summary>
    EquippedEitherCategory = 88,
    /// <summary>指定した二つのカテゴリーの装備を組み合わせて選ぶ。</summary>
    SelectPairedEquipment = 89,
    /// <summary>自身の両手が素手であることを要求する。</summary>
    BothHandsFree = 90,
    /// <summary>スタンバイフェーズ中であることを要求する。</summary>
    StandbyPhase = 91,
    /// <summary>指定カテゴリーのアイテムがハウジングとして設置されていることを要求する。</summary>
    InstalledHousing = 92,
    /// <summary>指定人数で共同宣言することを要求する。</summary>
    JointDeclarers = 93,
    /// <summary>任意のキャラクターの所持品から除外カテゴリーを除き、指定カテゴリーの品を選ぶ。</summary>
    SelectRestrictedOwnedItem = 94,
    /// <summary>任意のキャラクターが持つ指定カテゴリーの品を指定数選ぶ。</summary>
    SelectCharacterItem = 95,
    /// <summary>自身の所持品から指定数のアイテムを選ぶ。</summary>
    SelectOwnedItem = 96,
    /// <summary>自身に付与されている状態を指定数選ぶ。</summary>
    SelectOwnStates = 97,
    /// <summary>指定人数のキャラクターに付与されている状態を指定数選ぶ。</summary>
    SelectCharacterStates = 98,
    /// <summary>味方キャラクター全員を選ぶ。</summary>
    SelectAllAllies = 99,
    /// <summary>指定人数の味方キャラクターを選ぶ。</summary>
    SelectAllies = 100,
    /// <summary>上限人数まで重複せずに味方キャラクターを選ぶ。</summary>
    SelectDistinctAlliesUpTo = 101,
    /// <summary>自身を除く味方から、下限・上限の範囲で重複せずに選ぶ。</summary>
    SelectOtherAlliesRange = 102,
    /// <summary>自身を含む味方の人数が必要数以上の時に成立する。</summary>
    MinimumAlliesIncludingSelf = 103,
    /// <summary>同じ接近グループから指定状態のキャラクターを指定人数選ぶ。</summary>
    SelectMeleeStateCharacter = 104,
    /// <summary>指定状態のキャラクターを上限人数まで重複せずに選ぶ。</summary>
    SelectStateCharactersDistinct = 105,
    /// <summary>同じ接近グループにいる攻撃対象を指定人数選ぶ。</summary>
    SelectMeleeAttackTarget = 106,
    /// <summary>同じ接近グループにいる攻撃対象を上限人数まで重複せずに選ぶ。</summary>
    SelectDistinctMeleeAttackTargets = 107,
    /// <summary>指定状態を受けている相手を対象にする。</summary>
    SelectStateReceiver = 108,
    /// <summary>この装備を身に着けているキャラクターを対象にする。</summary>
    SelectWearer = 109,
    /// <summary>自身が指定状態を付与した相手を対象にする。</summary>
    SelectOwnStateRecipient = 110,
    /// <summary>エリアごとの使用回数を制限する。</summary>
    AreaUseLimit = 111,
    /// <summary>戦闘開始から数えて指定ターン以降であることを要求する。</summary>
    AfterBattleTurn = 112,
    /// <summary>自身の属性値のうち少なくとも一つが必要値以上であることを要求する。</summary>
    HighestAttributeMinimum = 113,
    /// <summary>この戦闘で指定状態のスタックを必要数以上消費済みであることを要求する。</summary>
    PastStackSpend = 114,
    /// <summary>自身が指定名のスキルを発動した時に成立する。</summary>
    OwnSkillActivated = 115,
    /// <summary>自身が指定カテゴリーの行為を宣言した直後に成立する。</summary>
    AfterOwnCategoryDeclared = 116,
    /// <summary>自身の指定カテゴリーの行為にカウンターを受けた時に成立する。</summary>
    CounterToCategory = 117,
    /// <summary>自身の指定カテゴリーの行為に対し、指定カテゴリーの行為が発動された時に成立する。</summary>
    CounterCategoryToCategory = 118,
    /// <summary>自身の行為が対象の指定カテゴリーの行為によって無効化された時に成立する。</summary>
    OwnActionInvalidated = 119,
    /// <summary>自身が指定の種類のロールを行う時に成立する。</summary>
    OwnRollTiming = 120,
    /// <summary>自身が状態を付与された時に成立する。</summary>
    ReceivedState = 121,
    /// <summary>自身に指定のロールによる状態が付与された時に成立する。</summary>
    StateFromRoll = 122,
    /// <summary>指定能力値を変化させるバフを自身が受けている時に成立する。</summary>
    StateBuffOnStat = 123,
    /// <summary>二つの指定カテゴリーのいずれかの行為を受けた相手を対象にする。</summary>
    ReceivesAnyOfTwoActions = 124,
    /// <summary>指定カテゴリーの行為を受けたキャラクターを指定人数選ぶ。</summary>
    SelectActionReceiverCount = 125,
    /// <summary>キャラクターが指定カテゴリーの行為を発動した時に成立する。</summary>
    ActionActivatedByCharacter = 126,
    /// <summary>自身以外のキャラクターが指定カテゴリーの行為の対象になった時に成立する。</summary>
    OtherCharacterTargeted = 127,
    /// <summary>いずれかのキャラクターが発動ロールに成功した時に成立する。</summary>
    AnyActivationSucceeded = 128,
    /// <summary>アイテムを使用したキャラクターを指定人数選ぶ。</summary>
    SelectItemUser = 129,
    /// <summary>指定カテゴリーの行為を受けた相手で、かつ指定状態の相手を対象にする。</summary>
    SelectAttackedStateHolder = 130,
    /// <summary>装備部位が両手である装備中のアイテムを指定カテゴリーから選ぶ。</summary>
    SelectTwoHandEquipment = 131,
    /// <summary>戦闘中に宣言されたスキルを選ぶ。</summary>
    SelectDeclaredBattleSkill = 132,
    /// <summary>指定リソースを任意の量だけ消費する。</summary>
    SpendChosenResource = 133,
    /// <summary>指定状態のスタックを任意の数だけ消費する。</summary>
    SpendChosenStacks = 134,
    /// <summary>指定カテゴリーの習得済みスキル数に応じて、習得に必要な決意を算出する。</summary>
    CostByLearnedCategoryCount = 135,
    /// <summary>自身が召喚・所持しているペットのスキル宣言時に成立する。</summary>
    SelectedPetSkillReaction = 136,
    /// <summary>ロールプレイで指定人数以上のパーティーメンバーの手伝いを必要とする。</summary>
    MinimumHelpingMembers = 137,
    /// <summary>習得元などの出典となるイベントを指定する。</summary>
    SourceEvent = 138,
    /// <summary>所持品から指定カテゴリーのアイテムを指定する。</summary>
    SelectInventoryCategory = 139,
    /// <summary>自身に指定名の状態が付与されていることを要求する。</summary>
    OwnStatePresent = 140,
    /// <summary>指定カテゴリーのスキルを一つでも習得しているキャラクターを選ぶ。</summary>
    PossessesAnySkillCategory = 141,
    /// <summary>指定カテゴリーの発動ロールが通常失敗になった時に成立する。</summary>
    NormalFailureInCategory = 142,
    /// <summary>アイテムを生産する指定カテゴリーの行為を宣言した時に成立する。</summary>
    OnCraftDeclaration = 143,
    /// <summary>指定されたスキルなどの発動時に成立する。</summary>
    SelectNamedSkillSource = 144,
    /// <summary>戦闘中のプレイヤーパーティーを選択する。</summary>
    ChoosePartyInBattle = 145,
    /// <summary>敵のターン終了直前に指定人数のキャラクターを選ぶ。</summary>
    OpponentTurnEndTarget = 146,
    /// <summary>指定スキルが必要なスパイク段階以上であることを要求する。</summary>
    SourceSkillSpike = 147,
    /// <summary>指定カテゴリーの行為とバフ付与の両方を受けた相手を対象にする。</summary>
    SelectActionAndStateUser = 148,
    /// <summary>所持している指定カテゴリーの品を任意の数選び、併せて指定カテゴリーの武器を選ぶ。</summary>
    SelectOwnedCategoryAnyAndWeapon = 149,
    /// <summary>指定スキルと、それ以外に指定数の該当スキルを習得していることを要求する。</summary>
    KnownSkillAndOtherCategory = 150,
    /// <summary>任意の対象を指定人数選び、参照する属性ボーナスも指定する。</summary>
    SelectTargetAndElement = 151,
    /// <summary>指定カテゴリーの行為の対象に選ばれた味方を対象にする。</summary>
    AlliedActionTarget = 152,
    /// <summary>指定カテゴリーの状態を受けているキャラクターを対象にする。</summary>
    IsStateCategoryTarget = 153,
    /// <summary>敵を上限人数までランダムに選び、同じ敵の重複を許す。</summary>
    RandomEnemyWithRepeats = 154,
    /// <summary>指定カテゴリーの行為を発動したキャラクターを対象にする。</summary>
    SelectCharacterUsingAction = 155,
    /// <summary>自身が召喚・所持しているペットを選ぶ。</summary>
    ChooseOwnedPetAction = 156,
    /// <summary>対象が状態付与を含む効果を受けた時に成立する。</summary>
    IncomingStateApplication = 157,
    /// <summary>装備条件を指定クラスの記載に従って扱う。</summary>
    EquipByClassDescription = 158,
    /// <summary>条件を満たした際に自動で習得する。</summary>
    AutomaticAcquisition = 159,
    /// <summary>宣言したスキルの消費量が自身の現在のリソース量を超える時に成立する。</summary>
    SpendBeyondCurrent = 160,
    /// <summary>指定カテゴリーの行為の消費量が自身の現在のリソース量を超える時に成立する。</summary>
    CategoryCostBeyondCurrent = 161,
    /// <summary>同じ接近グループのキャラクターに自身が対象として選ばれた時に成立する。</summary>
    TargetedByMeleeCharacter = 162,
    /// <summary>指定した名前のアイテムを装備していることを要求する。</summary>
    EquippedNamedItem = 163,
    /// <summary>選択スキルの宣言条件とリソースの両方を満たすことを要求する。</summary>
    SelectedSkillUsable = 164,
    /// <summary>装備者がこのアイテムを武器に選び、指定カテゴリーの行為を宣言した時に成立する。</summary>
    WearerUsesAsWeapon = 165,
    /// <summary>現在のエリアが指定カテゴリーの外にあることを要求する。</summary>
    OutsideAreaCategory = 166,
    /// <summary>指定カテゴリーの武器を選んでスキルを宣言する時に成立する。</summary>
    DeclareWithWeaponCategories = 167,
    /// <summary>戦闘中にパーティー全体で共有する使用回数の上限を定める。</summary>
    PartyLimitPerBattle = 168,
    /// <summary>現在のエリアが指定カテゴリーに属することを要求する。</summary>
    InAreaCategory = 169,
    /// <summary>同じ接近グループの敵全員を選ぶ。</summary>
    SelectAllMeleeEnemies = 170,
    /// <summary>所持している装備を指定数選ぶか、何も選ばずに宣言する。</summary>
    SelectOptionalOwnedGear = 171,
    /// <summary>自身の種族が候補のいずれかに該当することを要求する。</summary>
    OwnRaceIsOneOf = 172,
    /// <summary>指定人数の敵キャラクターを選ぶ。</summary>
    SelectEnemyCount = 173,
    /// <summary>戦闘中であることを要求する。</summary>
    DuringBattle = 174,
    /// <summary>自身の指定リソースが必要量以上の時に成立する。</summary>
    MinimumOwnResource = 175,
    /// <summary>自身が宣言した指定カテゴリーの行為にカウンターを受けた時に成立する。</summary>
    OwnCategoryCountered = 176,
    /// <summary>二つの指定カテゴリーのいずれかを発動した敵に対して自動発動する。</summary>
    AutoEnemyFromEitherAction = 177,
    /// <summary>指定カテゴリーのいずれかを発動した敵に対して自動発動する。</summary>
    AutoEnemyFromAnyAction = 178,
    /// <summary>指定カテゴリーの行為を発動したキャラクターに対して自動発動する。</summary>
    AutoCharacterFromAction = 179,
    /// <summary>指定カテゴリーから習得できるスキルを一つに制限する。</summary>
    SingleSkillFromCategory = 180,
    /// <summary>指定された書物から習得できる。</summary>
    LearnFromBook = 181,
    /// <summary>指定数の属性を選ぶ。</summary>
    SelectElements = 182,
    /// <summary>指定人数以上の味方キャラクターを必要とする。</summary>
    SelectMinimumAllies = 183,
    /// <summary>二つの属性値のうち少なくとも一方が必要値以上であることを要求する。</summary>
    EitherElementMinimum = 184,
    /// <summary>指定人数を上限に味方キャラクターを選ぶ。</summary>
    SelectAlliesUpTo = 185,
    /// <summary>自身を除く味方キャラクターを上限人数まで選ぶ。</summary>
    SelectOtherAlliesUpTo = 186,
    /// <summary>指定カテゴリーの状態を受けていないキャラクターを指定人数選ぶ。</summary>
    SelectWithoutStateCategory = 187,
    /// <summary>キャラクターに付与された二つの指定カテゴリーの状態から指定数を選ぶ。</summary>
    SelectCharacterStateOfCategories = 188,
    /// <summary>キャラクターが二つの指定カテゴリーの行為をともに受けた時に成立する。</summary>
    AnyoneReceivesBoth = 189,
    /// <summary>指定状態の相手を行為の発生元とする。</summary>
    FromStateRecipient = 190,
    /// <summary>使用できる通算回数を指定回数に制限する。</summary>
    OneTimeLimit = 191,
    /// <summary>別エリアにいる、過去に必要回数以上ロールプレイをした相手を選ぶ。</summary>
    SelectPriorRoleplayAcrossAreas = 192,
    /// <summary>以降の指定回数のスキルでは、選択可能な敵が必要数いれば敵をランダムに選ぶ。</summary>
    NextUsesMustPickRandomEnemy = 193,
    /// <summary>味方キャラクターに付与された状態を指定数選ぶ。</summary>
    SelectAlliedStates = 194,
    /// <summary>自身が指定状態になって戦闘を終えた後に成立する。</summary>
    EndBattleInState = 195,
    /// <summary>指定の行為を宣言した敵のうち、二つの除外カテゴリーのどちらにも該当しない敵に自動発動する。</summary>
    AutoEnemyExceptTwoCategories = 196,
    /// <summary>同じ接近グループから指定カテゴリーを除いてキャラクターを選ぶ。</summary>
    SelectMeleeExceptCategory = 197,
    /// <summary>自身に向けて指定カテゴリーの行為を発動した相手を対象にする。</summary>
    TargetIncomingSource = 198,
    /// <summary>自身が指定の行為を受けるか、指定属性の威力を含む別の指定行為を受けた時に成立する。</summary>
    IncomingCategoryOrElementalAction = 199,
    /// <summary>候補に挙げた名前のスキルをいずれか一つ習得していることを要求する。</summary>
    KnowAnyNamedSkills = 200,
    /// <summary>自身の種族名が指定名と異なることを要求する。</summary>
    OwnRaceNotName = 201,
    /// <summary>自身のターン開始時にランダムな敵を対象として自動発動する。</summary>
    AutoRandomEnemyOwnTurn = 202,
    /// <summary>装備中の任意の武器と、同じ接近グループのキャラクターを選ぶ。</summary>
    SelectAnyWeaponAndMelee = 203,
    /// <summary>指定スキルによる宣言回数が基準に達したアイテムを選択候補から外す。</summary>
    ExcludeItemsUsedBySkill = 204,
    /// <summary>指定した回・名称のトーナメントで優勝していることを要求する。</summary>
    TournamentChampion = 205,
    /// <summary>指定カテゴリーの対象を指定数選ぶ。</summary>
    SelectInCategory = 206,
    /// <summary>自身がパーティーに合流したターンに指定人数の敵を選ぶ。</summary>
    PartyJoinTurnEnemy = 207,
    /// <summary>設置済みの指定名のものを任意の数だけ対象にする。</summary>
    SelectInstalledEntities = 208,
    /// <summary>二つの指定カテゴリーから上限数まで選ぶ。</summary>
    SelectTwoCategoriesUpTo = 209,
    /// <summary>自身の種族が候補カテゴリーのいずれかに属するか、指定名の種族であることを要求する。</summary>
    RaceCategoryOrName = 210,
    /// <summary>エリアの治安が指定値以下であることを要求する。</summary>
    AreaSecurityAtMost = 211,
    /// <summary>召喚された時に成立する。</summary>
    WhenSummoned = 212,
    /// <summary>自身が召喚・所持しているペットのターンに成立する。</summary>
    PetTurn = 213,
    /// <summary>味方のターン開始時に成立する。</summary>
    StartAlliedTurn = 214,
    /// <summary>移動先とするキャラクターを自由に指定人数選ぶ。</summary>
    MoveDestinationCharacter = 215,
    /// <summary>味方を下限・上限の範囲で重複せずに選ぶ。</summary>
    DistinctAlliedRange = 216,
    /// <summary>召喚者がスキルを発動した時に自動発動する。</summary>
    AutoWhenSummonerUsesSkill = 217,
    /// <summary>ロールプレイによって行動を指定された時に成立する。</summary>
    RoleplayOrder = 218,
    /// <summary>対象と指定の状態関係にない敵の宣言に対して自動発動する。</summary>
    AutoEnemyNotMeleeWithTarget = 219,
    /// <summary>同じ接近グループから攻撃対象を選ぶ。人数は指定しない。</summary>
    SelectMeleeAttackTargetUnspecified = 220,
    /// <summary>同じ接近グループの指定状態のキャラクターが、指定カテゴリーの行為の対象になった時に成立する。</summary>
    MeleeStateTargetOfAction = 221,
    /// <summary>戦闘開始から数えて指定ターン目であることを要求する。</summary>
    BattleTurnNumber = 222,
    /// <summary>自身がアクティブ効果によるダメージを受けた時に成立する。</summary>
    IncomingActiveDamage = 223,
    /// <summary>キャラクターが受けている指定カテゴリーの状態を指定数選ぶ。</summary>
    SelectCharacterStateByCategory = 224,
    /// <summary>自身に対して発動した指定カテゴリーの行為を対象にする。</summary>
    IncomingCategoryOnSelf = 225,
    /// <summary>発動者が選んだ対象を引き継いで発動する。</summary>
    SummonerSelectedTarget = 226,
    /// <summary>自身が指定カテゴリーの行為の対象になった時に成立する。</summary>
    TargetOfCategory = 227,
    /// <summary>戦闘中は同じ武器を選び続けることを要求する。</summary>
    LockWeaponForBattle = 228,
    /// <summary>対象の装備部位の数が指定数であることを要求する。</summary>
    EquipmentSlotCount = 229,
    /// <summary>指定カテゴリーの行為を宣言した敵に対して自動発動する。</summary>
    AutoEnemyOnDeclaration = 230,
    /// <summary>危険度上限とカテゴリーを満たす対象を選ぶ。</summary>
    SelectDangerCategory = 231,
    /// <summary>両方の装備部位に該当する所持装備を指定数選ぶ。</summary>
    SelectOwnedEquipmentSlots = 232,
    /// <summary>いずれかの指定カテゴリーを条件として自身が選択された時に成立する。</summary>
    TargetedByCategoryRequirement = 233,
    /// <summary>戦闘中に自身が行為判定に失敗した時に成立する。</summary>
    OwnBattleCheckFailed = 234,
    /// <summary>自身が発動した指定カテゴリーの行為に反応する。</summary>
    OwnActivatedCategory = 235,
    /// <summary>NPCがスキルを発動した時に成立する。</summary>
    NpcSkillActivation = 236,
    /// <summary>指定カテゴリーの自身の発動ロールが成功すると成立する。</summary>
    OwnCategoryRollSuccess = 237,
    /// <summary>自身が指定カテゴリーの行為を発動する時に成立する。</summary>
    OwnCategoryActivation = 238,
    /// <summary>この武器を選択して指定カテゴリーの行為を発動する時に成立する。</summary>
    CategoryUsingThisWeapon = 239,
    /// <summary>この武器を使った指定カテゴリーの行為が命中した時に成立する。</summary>
    HitWithThisWeapon = 240,
    /// <summary>自身がキャラクターに指定カテゴリーの状態を付与した時に成立する。</summary>
    ApplyCategoryToCharacter = 241,
    /// <summary>自身のターンに同じ対象へこの武器で指定回数以上ダメージを与える。</summary>
    RepeatedWeaponDamageOwnTurn = 242,
    /// <summary>自身が敵に状態を付与した時に成立する。</summary>
    ApplyStateToEnemy = 243,
    /// <summary>誰かの指定リソースが最大値に対する指定割合以下になった時に成立する。</summary>
    AnyResourceFractionAtMost = 244,
    /// <summary>特定の能力値を参照する行為判定でファンブルが出た時に成立する。</summary>
    StatCheckFumbled = 245,
    /// <summary>指定した季節のいずれかで、指定エリアにおいて収穫を行う時に成立する。</summary>
    SeasonalHarvest = 246,
    /// <summary>《指定名》を指定数体選択。</summary>
    SelectNamedCharacter = 450,
    /// <summary>〈指定名〉いずれかのキャラクターを指定数体選択。</summary>
    SelectCharacterOfCategories = 451,
    /// <summary>《指定名》を所持しているキャラクターを指定数体選択。</summary>
    SelectNamedItemHolder = 452,
    /// <summary>《指定名》とのソロ戦闘で指定数ターン経過。</summary>
    SoloBattleTurnElapsed = 453,
    /// <summary>《指定名》との戦闘でダメージを与える。</summary>
    DamageNamedCombatant = 454,
    /// <summary>《指定名》との戦闘に単独勝利。</summary>
    WinSoloAgainstNamed = 455,
    /// <summary>称号〈指定名〉未取得。</summary>
    LacksTitle = 456,
    /// <summary>エリア《指定名》。</summary>
    AtNamedArea = 457,
    /// <summary>特殊行動《指定名》成功後。</summary>
    SucceededSpecialAction = 458,
    /// <summary>キャラクターが指定名内の〈指定名〉に居る事。</summary>
    WithinRealmArea = 459,
    /// <summary>パーティーメンバーが戦闘エリアに居る事。</summary>
    PartyInBattleArea = 460,
    /// <summary>戦闘中使用不可能。</summary>
    OutsideBattleUse = 461,
    /// <summary>レシピ指定名によって《指定名》を得る時。</summary>
    RecipeProducedNamedItem = 462,
    /// <summary>指定名の養牧に通常失敗した時。</summary>
    NormalFailureHusbandry = 463,
    /// <summary>自身または騎乗対象が〈指定名〉の対象になった時。</summary>
    RiderOrMountTargeted = 464,
    /// <summary>耐久指定数以上。</summary>
    CurrentDurabilityAtLeast = 465,
    /// <summary>自身の装備している〈指定名〉を1つ選択。</summary>
    SelectedEquippedCategory = 466,
    /// <summary>自身が消費リソースに《指定名》を含む《指定名》レシピを発動した時。</summary>
    RecipeWithNamedMaterial = 467,
    /// <summary>自身が港のある〈指定名〉に居る事。</summary>
    AtHarborArea = 468,
    /// <summary>港の行き先から〈指定名〉エリアを1つ選択。</summary>
    SelectHarborDestination = 469,
    /// <summary>設置者が自身と接近グループではないキャラクターから〈指定名〉の対象となった時、自動発動。</summary>
    InstallerTargetedOutsideMelee = 470,
    /// <summary>適正レベル指定数のクエストを達成した時。</summary>
    QuestCompletedAtLevel = 471,
    /// <summary>適正レベル指定数～指定数のクエストを達成した時。</summary>
    QuestCompletedLevelRange = 472,
    /// <summary>〈指定名〉により〈指定名〉アイテムを生産する時。</summary>
    CraftCategoryWithTool = 473,
    /// <summary>《指定名》を消費して〈指定名〉によりアイテムを生産する時。</summary>
    CraftWithIngredientAndTool = 474,
    /// <summary>《指定名》を消費して《指定名》アイテムを生産する時。</summary>
    CraftCategoryWithIngredient = 475,
    /// <summary>キャラクターを無作為に指定数選ぶ。</summary>
    RandomCharacters = 476,
    /// <summary>重複を許さずキャラクターを無作為に指定数選ぶ。</summary>
    DistinctRandomCharacters = 477,
    /// <summary>選択できる敵から重複を許さず指定数を無作為に選ぶ。</summary>
    DistinctRandomEnemies = 478,
    /// <summary>パーティー全員を対象にする。</summary>
    SelectParty = 479,
    /// <summary>指定カテゴリーを含むパーティーではそのカテゴリーから指定数を無作為に選ぶ。</summary>
    SelectRandomCategoryInParty = 480,
    /// <summary>直前の条件に合う対象がいない場合にキャラクターを無作為に指定数選ぶ。</summary>
    FallbackRandomCharacters = 481,
    /// <summary>対象が任意の判定に成功した場合に発動を失敗にする。</summary>
    OptionalCheckCancelsActivation = 482,
}
[Serializable]
public class ConditionEntry
{
    public ConditionType Type;
    public List<string> Parameters = new List<string>();
}
