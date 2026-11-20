using System;
using System.Collections.Generic;
using UnityEngine;

public enum CostKind
{
    Resource = 0,
    ItemCategory = 1,
    ItemName = 2,
    NamedStack,
    CategoryStack,
    SelectedItem,
    Currency,
    SelectedCategoryItem
}
public enum CostQuantity
{
    Fixed,
    All,
    Chosen,
    LeaveRemaining,
    Formula,
    DistinctNameRange
}
[Serializable]
public class ResourceCost
{
    public CostKind Kind;
    public string Resource = "";
    public CostQuantity Quantity;
    [Min(0)] public int Amount;
    [Min(0)] public int Maximum;
    public string Formula = "";
}
public enum TriggerTiming
{
    Always = 0,
    AttackPower = 1,
    EvasionResult = 2,
    StateRemoved = 3,
    AfterWeaponAttack = 4,
    AfterSkillResolution = 5,
    AfterActivationRoll = 6,
    BeforeEffectActivation = 7,
    StateReapplied = 8,
    TargetValue = 9,
    ResourceCost = 10,

    IncomingDamage = 11,
    SkillValue = 12,
    ActionResult = 13,
    RandomResult = 14,
    ElementalPower = 15,
    TurnStart = 16,
    CharacterIncapacitated = 17,

    ResourceChanged = 18,
    ActionActivated = 19,
    StateStackChanged = 20,
    DamageDealt = 21,
    DamageReceived = 22,
    ActionDeclared = 23,
    BattleEnd = 24,
    ResourceRecovery = 25, // when a resource restoration amount is determined
    AttackHit = 26, // this skill's attack hit its target (not merely a successful roll)
    ActionReceived = 27 // after an incoming action resolves on the holder, including zero damage
}
[Serializable]
public class TriggerDefinition
{
    public TriggerTiming Timing;
    public ConditionSet Conditions = new ConditionSet();
}
[Serializable]
public class DiceRollDefinition
{
    [Tooltip("式全体を評価する回数。0なら発動ロールなし。1d100の1とは別です。")]
    [Min(0)] public int Count;
    public string Formula = "";
    public string Target = "";
    // -1: no fixed result. Used only by an automatic-success roll.
    public int FixedResult = -1;
    public bool UseSummonerResult;
    public List<string> SourceItemCategories = new List<string>();
    public int RepresentativeCount;
}
public enum EffectType
{
    None = 0,
    Active = 1,
    Counter = 2,
    Passive = 3,
    Roleplay = 4,
    Critical = 5,
    Fumble = 6,
    Declaration = 7,
    SecondSpike = 8,
    ThirdSpike = 9,
    Success = 10,
    Failure = 11
}
[Serializable]
public class EffectDefinition
{
    public EffectType Type;
    // 空なら追加トリガーなし。複数指定時は、いずれかのトリガーが成立した時。
    public List<TriggerDefinition> Triggers = new List<TriggerDefinition>();
    public List<EffectContent> Contents = new List<EffectContent>();
}
public enum EffectContentType
{
    None = 0,
    RestoreResource = 1,
    ApplyState = 2,
    ConsumeResource = 3,
    JoinMeleeGroup = 4,
    StateAlias = 5,
    RemoveState = 6,
    WeaponAttack = 7,
    ApplyConsumedItemActive = 8,
    GainStack = 9, // actor, state, amount/formula, optional maximum stacks
    Damage = 10,
    ApplyStateInMeleeGroup = 11,
    InvalidateIncomingAction = 12,
    InvalidateTriggeredEffect = 13,
    SkillAttack = 14, // actor, power | actor, PowerAndAttribute, power, attribute power | actor, IgnoreOwnStateCategory, category, power | ElementalWeapon form
    ModifyResource = 15,
    InvalidateAction = 16,
    ReduceDamage = 17,
    ReapplyEffects = 18,
    RollDice = 19,
    Summon = 20,
    // actor, amount, multi-attribute policy, rule key, then attribute/state pairs.
    GainMappedStateStacks = 21,
    // amount
    AddAreaGathering = 22,
    RoleplayDescription = 23, // Narrative permission, not executable combat text.
    ProhibitAcquisition = 24, // actor, timing, skill names...
    OptionalCategoryExclusion = 25, // actor, category, PerOccurrence
    RestoreFromDamage = 26, // actor, resource, dealt-damage multiplier
    ConsumeStacksForDamage = 27, // actor, state, All, multiplier, ThisAttack
    PlaceTrap = 28, // local Summons definition name
    RemoveSummon = 29, // Self
    PreventSelection = 30, // actor, All
    ProhibitEffect = 31, // actor, effect kind
    PreventStateApplication = 32, // actor, Except, state names...
    RerollActivation = 33, // Self, replacement ability bonus, TriggeringRoll
    SetResourceValue = 34, // actor, resource, value
    SkipRoll = 35, // actor, roll kind, Optional, ThisResolution
    LimitAcquisition = 36, // Self, RaceSelection, skill category, maximum (0 prohibits)
    RaceAlias = 37, // Self, additional race category
    RerollGathering = 38, // skill name, Day, maximum uses
    EquipSlotSubstitution = 39, // Weapon, BothHands, OneHand
    TransformAtStacks = 40, // Self, stack state, Equals, threshold, HP, MP, resulting state (after this gain)
    CyclingStateStacks = 41, // Self, action category, state, state category, gain, threshold, resource, restore, reset
    DrainResource = 42, // AllEnemies, resource, positive reduction formula, Self, ActualTotal, AllowOverflow
    CreateMeleeGroup = 43, // Self, Target, state name; create a new group
    PreventMealPenalties = 44, // Self, MealAndMealSet, ResourceAndStatDecrease
    GrantCreationChoice = 45, // LifePath, count, candidate names...
    GrantRaceTrait = 46, // race category, count
    CharacterRule = 47, // character-rule kind followed by semantic parameters; see CharacterRuleTextCodec
    OptionalInvalidateAction = 48, // Self, action category, AtMost, achievement value
    UseStateDefinitionAtStacks = 49, // GreaterThan, threshold, definition name, ReplaceEffects, KeepIdentityAndStacks
    LeaveBattle = 50, // Self
    PreventRollAtResource = 51, // Self, resource, AtMost, threshold, roll name
    CategoryImmunity = 52, // Self, categories... (any)
    ProhibitCategoryTarget = 53, // Self, category
    ConvertResourceCost = 54, // Self, from resource, to resource, All
    AddQuestReward = 55, // Self, resource, amount
    ApplySelectedSkillState = 56, // SelectedSkill, state, turns
    ProhibitSelectedSkill = 57, // Self, SelectedSkill
    ResourceDamage = 58, // Target, resource, formula
    RestoreFromResourceDamage = 59, // Self, source resource, restored resource, multiplier, ThisResolution
    GrantActionControl = 60, // Applier, Self, Turn, limit, AllyDuringAction
    DelayedStateAttack = 61, // Target, delay turns, power, attribute power, SnapshotOnApply, RemoveAfterAttack
    TurnStartStateStacks = 62, // Self, required state, gained state, amount, AllCharacters
    EnvironmentImmunity = 63, // Self, environment names...
    RestoreOnAppliedState = 64, // Self, action category, Target, applied state, resource, MaximumPercent, percent
    ChooseCheckOutcome = 65, // Target, Critical, Fumble, Optional
    GrantItem = 66, // Self, item name, count, rank formula
    RemoveStatesByOrigin = 67, // Self, Roll, roll name
    DeclareSelectedSkill = 68, // Self, Target, ACT, 0, Optional, power factor (other costs/conditions retained)
    SwapWeapon = 69, // Self, SelectedWeapon, Battle, AllowUnarmed
    RequireBattleAreaCategory = 70, // required destination area category
    RestrictBattleTurnDeclarations = 71, // turn, allowed category A, allowed category B (OR)
    SuppressTraitEffects = 72, // trait name
    DefineOwnAttribute = 73, // no parameters: highest base attribute; ties remain unresolved
    CounterFumbleState = 74, // incoming category, state name, turns
    OptionalCategoryImmunity = 75, // incoming category (Self, optional)
    OwnAttributeHitState = 76, // own attribute, state, turns (Target, on this attack hit)
    OwnAttributeHitRemoval = 77, // own attribute, removed state (Target, on this attack hit)
    MissingOwnAttributePower = 78, // base power; omit attribute power and attribute branches if absent
    RandomAttackDeclaration = 79, // no parameters: random own attack, random legal targets including Self
    RandomTargetSelection = 80, // count; random legal targets excluding Self
    PreventResourceRecovery = 81, // resource (Self)
    ProhibitActionCategory = 82, // category (Self, prevent declaration/activation)
    ExtraCountersPerSkill = 83, // battle uses, declarations per triggering skill, same-target declarations
    NextDeclarationExtraTargets = 84, // uses, excluded required state, minimum targets, optional extra targets
    AdvanceDestinySpike = 85, // stages; optional use of an existing higher stage
    PreventSelectionOutsideMelee = 86, // incoming category
    ApplierTurnAttack = 87, // power; Applier attacks state holder on Applier's turn start, current Applier stats
    ExecuteAfterAttackDamage = 88, // HP maximum percent threshold, resulting HP, applied state (Target)
    ProhibitDeclarationCategory = 89, // category (Self, declaration only)
    ReviveAfterBattle = 90, // required/removed own state, HP; only if allies were not wiped out
    AbsentResource = 91, // resource does not exist (not zero)
    StateImmunity = 92, // state name (Self)
    StateOnIncomingCritical = 93, // incoming action category, applied state; activation-roll critical
    EnvironmentPermanentState = 94, // environment name, maintained state without a turn limit
    SuppressSourceDamageAndState = 95, // incoming category, source trait, state (only that trait's penalties)
    SuppressSourceEnvironmentState = 96, // environment, source trait, state
    HitRaceState = 97, // allowed target races (comma separated, OR), state, turns; this attack hit
    WeaponHitRaceState = 98, // weapon category, allowed target races (OR), state, turns
    OptionalStackOnSelected = 99, // state, amount; give to the selecting actor, optional each selection
    OptionalRecoveryIfStackExists = 100, // state, resource, amount; own turn start, any character with >=1 stack
    AdditionalRaceSkills = 101, // additional count, from currently selectable race skills
    IncomingCategoriesState = 102, // incoming categories (comma separated, OR), state, turns (Self)
    DeferMissingResourceCost = 103, // resource; this skill's Active only, pay available now, remainder next own turn start
    RemoveAllStacks = 104, // own stack state, after this skill's attack (not its power/cost calculation)
    TimedTargetStacks = 105, // state, count, duration turns; only these newly granted stacks expire
    UseStateBoundCharacter = 106, // state; player must be present, ignore state effects for use, state cannot be removed
    SetTargetResourceAndStates = 107, // resource, value, state A, state B (both)
    SucceedFailedActivationRoll = 108, // no parameters; triggering normally failed roll becomes success, not Critical
    UnmodifiableMaximumResourceDamage = 109, // target resource, maximum percent; no power/damage increases or reductions from defense/effects/states
    ConsumeStacks = 110, // Self, local state, ReceivedActionPower (not post-mitigation damage)
    ApplyPermanentLinkedStates = 111, // Self state, selected character's state; retain this pair's partner
    RemoveLinkedStates = 112, // Self state, partner state; resolve partner before clearing either instance
    UsePartnerMeleeGroup = 113, // engagement state name; inherit its check and the partner's group
    UsePartnerStat = 114, // stat name; read the linked partner's current value
    SharePartnerAreaEligibility = 115, // holder qualifies when partner meets the area's movement conditions
    FollowPartnerBattleExit = 116, // holder also leaves when partner leaves the battle
    GrantSelectedRecipeProduct = 117,
    ActivateSelectedRecipe = 118,
    OfferSelectedRecipeEffect = 119,
    UseRecipeProductOnTarget = 120,
    ResolveDurabilityByRoll = 121,
    NoRollEffect = 122,
    AcquireSkillInCategory = 123,
    PreventMealStatPenalties = 124,
    IgnoreEnvironmentInOwnState = 125,
    AcquireOtherRaceTraits = 126,
    CannotUseItemCategories = 127,
    IgnoreTraitItemRestriction = 128,
    EquippedWeaponEquivalent = 129,
    GrantCalculatedDefense = 130,
    OptionalEncounterExit = 131,
    ClearTargetStacks = 132,
    ReceiveActionForTarget = 133,
    UnequipSelectedCategory = 134,
    StealSelectedPossessions = 135,
    ApplyPermanentTargetState = 136,
    SelectedConsumableActive = 137,
    ClearOwnStateAndCategory = 138,
    CancelIncomingActive = 139,
    ApplyStateToTargetGroup = 140,
    ApplyTimedStateToTargetGroup = 141,
    PartyEnvironmentImmunities = 142,
    HPHealBySkillValue = 143,
    OptionalStackExchange = 144,
    RemovePartyStateCategory = 145,
    GroupInNewMelee = 146,
    ReviveWithMaximumRatio = 147,
    DespawnAfterOwnAction = 148,
    CleanseTargetStateCategories = 149,
    NextTargetDieMultiplier = 150,
    ConditionalSelfRestore = 151,
    NextCategoryCostAndResult = 152,
    NextCategoryResultAndPower = 153,
    NextCategoryRollTargetDelta = 154,
    NextCategoryActivationDelta = 155,
    ChangeSelectedItemDurability = 156,
    RequireDestinationArea = 157,
    OptionalRaceCategory = 158,
    CriticalHitGrantsState = 159,
    RequireEligibleRaceSkill = 160,
    IgnoreGearOutsideSlots = 161,
    OptionalSubstituteRollStat = 162,
    ImmuneToTwoCategories = 163,
    OptionalEncounterSpendItemForState = 164,
    SpendResourcePerDeclaration = 165,
    BattleStartStacks = 166,
    WaiveTriggerSkillCost = 167,
    InvalidateStateApplication = 168,
    RedirectTargetToSelf = 169,
    CancelItemActive = 170,
    CancelDurabilityRoll = 171,
    RemoveOwnStateCategories = 172,
    RerollTargetActivation = 173,
    RedirectCategoryTarget = 174,
    InvalidateTargetEffect = 175,
    InvalidateTargetAction = 176,
    InstallerGroupWeaponAttack = 177,
    GroupSkillValueAttack = 178,
    GroupPowerAttack = 179,
    PurgeInjuryRollStates = 180,
    StealAdditionalItem = 181,
    MultiplySummonHPAndPower = 182,
    ReturnStacksSpentInBattle = 183,
    ResetTargetResourceByFormula = 184,
    SetTargetMeleeGroup = 185,
    HealSanityWithSkillValue = 186,
    HealPowerWithSkillValue = 187,
    DealTwoResourceDamage = 188,
    GrantCategoryItems = 189,
    InspectAreaProgressRolls = 190,
    AdditionalAreaProgressRolls = 191,
    ApplyStateUntilAreaMove = 192,
    GainPermanentTargetStacks = 193,
    GrantSelfStacksPerTarget = 194,
    ProhibitInvalidationExceptCategory = 195,
    ElementAttackPower = 196,
    ApplyStateToAllOthers = 197,
    NextAreaMoneyFactor = 198,
    NextCategoryTargetFactor = 199,
    BattleRejoinEnemyTurn = 200,
    AttackPerSuccessfulRoll = 201,
    AttackIfAllRollsSucceed = 202,
    NextOwnTurnResource = 203,
    OpposedTargetCheck = 204,
    RemoveBuffsAffectingStat = 205,
    SubjectResourceLossFloorZero = 206,
    NextCategoryDiceBonus = 207,
    ImmuneToTargetingAndState = 208,
    UseGrantedWeaponDefinition = 209,
    EquipArmorWithApplierDefense = 210,
    MayCancelAnyTargeting = 211,
    ExpireOnCategoryDeclaration = 213,
    RestrictDeclaredCategories = 214,
    NegateEffectsOnGroupExceptWeapons = 215,
    NegateSameGroupEffectsExceptWeapons = 216,
    ReceiveDelayedEnemyTurnAttack = 217,
    BlockRevivalAndSetResource = 218,
    UseStateAsWeapon = 219,
    ProtectIncomingCategoriesFromInvalidation = 220,
    OwnTurnElementDamage = 221,
    WaiveMeleeBelowCost = 222,
    DelayedSkillValueAttack = 223,
    GainStacksOnAreaMove = 224,
    ForbidDeclarationsAboveResourceCap = 225,
    UseApplierPossessions = 226,
    PreventEnemyStateApplications = 227,
    CannotBeSelectedByActive = 228,
    RecoveryOnAttackBySkillValue = 229,
    DeclareSelectedSkillAsOwn = 230,
    ApplyDespiteTargetStates = 231,
    ApplyRaceConditionalState = 232,
    GainTimedStateAfterRemove = 233,
    StackGainPerSpentResource = 234,
    NegateCategoryOnRollMargin = 235,
    ReceiveIncomingPowerShare = 236,
    TimedStateWithCategory = 237,
    MaySwitchBareHanded = 238,
    SwapEquippedSlots = 239,
    GainStackOnOtherTarget = 240,
    ExpireOnApplierSkill = 242,
    ExpireAfterOwnSkillOrItem = 243,
    SpendStacksPerUsedItem = 244,
    ExpireOnApplierSuccess = 245,
    ConsumeStacksAfterEffect = 246,
    ExpireAfterEffect = 247,
    ExpireOnMeleeOrSelectedState = 248,
    CannotDeclareActive = 249,
    RollOnDeclaration = 250,
    GainStateNextTurn = 251,
    RemoveSelectedStateCount = 252,
    ScaleResourceOnApplication = 254,
    SpendResourceEachTurn = 255,
    ScaleResourceOnRemoval = 256,
    RacialAbilityPercentByOrdinal = 257,
    JoinMeleeAndSecondState = 258,
    RepeatAttackScaledWeapon = 259,
    RepeatAttackRolledCount = 260,
    AttackForEachSuccessfulActivation = 261,
    MultiplySelectedBarrierStacks = 262,
    CurrentAreaProgressFactor = 263,
    TriggerAttackIfMelee = 265,
    ReduceDamageAndResourceDamage = 266,
    TargetStateCheckFactor = 267,
    DamagePercentTargetHP = 268,
    PreventAllTargetSelection = 269,
    IgnoreIncomingEnemyEffects = 270,
    TransferPreventedEffectsAtExpiry = 271,
    NegateIncomingCategory = 272,
    ApplierControlsDeclarations = 273,
    AttackAllEnemiesOnOwnTurn = 274,
    HealOtherCharacterOnOwnTurn = 275,
    IncrementalPaymentBeforeActivation = 276,
    ImmediateNextAction = 277,
    ChooseWhetherEncounterEnemy = 278,
    CasterNamedTitle = 279,
    CriticalDoesNotEnsureSuccess = 280,
    MinimumRollAlliesFirst = 281,
    DropResourceAndApplyTimedState = 282,
    TransferMeleeToCaster = 283,
    MayEmptyGearSlot = 284,
    ZeroHPForCategory = 285,
    ApplyPermanentOwnState = 286,
    ExcludeCategoryForEnemyEffects = 287,
    RemoveTargetStateCategory = 288,
    RemoveForOwnStacks = 289,
    SetReferenceActionResult = 290,
    SumRollsAsResult = 291,
    SelfResourceDamage = 292,
    CancelDeclarationAndCounter = 293,
    InstallerWeaponAttack = 294,
    EqualizeTargetResources = 295,
    TransferSelectedStateStacks = 296,
    TakeTwoActionsForTarget = 297,
    IgnoreStackCostOutsideBattle = 298,
    DualDamageReduction = 299,
    NextTurnCategorizedStateAndRecovery = 300,
    PlaceTargetInMeleeState = 301,
    GrantPermanentStateStacks = 302,
    TwoPermanentOwnStates = 303,
    WaiveCategoryCost = 304,
    ApplyTargetConsumableCategory = 305,
    MakeGrantedBuffPermanent = 306,
    EarnRecipeProductByCategory = 307,
    EarnRecipeProductByEitherCategory = 308,
    DisableCounterAgainstEffect = 309,
    BonusElementAndResourceDamage = 310,
    NextSkillBonusElementAndDamage = 311,
    NamedWeaponBonusAttack = 312,
    NamedWeaponDefensePenetration = 313,
    NamedWeaponAccuracyPower = 314,
    OptionalReviveByMaxResource = 315,
    RemoveStateFullyRecover = 316,
    HolderPerformsOrderedChecks = 317,
    RestrictionOnNegatingState = 318,
    NegateTriggeringAction = 319,
    TargetStateOnCheckFailure = 320,
    ApplyFormulaTargetStacks = 321,
    RemoveAllTargetCategory = 322,
    FailDeclarationOnRollAtMost = 323,
    NegateTargetResponseAndResolve = 324,
    NoCriticalFumbleForStat = 325,
    InitialBattleTargetState = 326,
    RollTargetDrop = 327,
    ScaleWeaponPower = 328,
    UnmodifiableDamage = 329,
    DisarmTargetCategory = 330,
    ActivateNamedEntity = 331,
    RemoveTwoTargetCategories = 333,
    GrantOwnCategorizedState = 334,
    MirrorEnemyResourceLossByStacks = 335,
    DeclareForTargetIgnoringState = 336,
    ElementWeaponResourceDamage = 337,
    SpreadAtStackCount = 339,
    WeaponResourceDamage = 340,
    StateGroupPowerAttack = 341,
    ScaledElementWeaponAttack = 342,
    InstallerWeaponResourceDamage = 343,
    ChoosePetAction = 344,
    StacksPerActivation = 345,
    NextStatDiceBonus = 346,
    ReturnCategoryToCaster = 347,
    GuaranteedStateAlternative = 348,
    ReduceTwoResourcesPercent = 349,
    ExpireAtTurnEnd = 350,
    RedirectBuffOrAuraToCaster = 351,
    JoinDestinationMeleeState = 352,
    ApplyWeaponPowerStacks = 353,
    ClearInjuryAfterHPThreshold = 354,
    CapAtResourceMaximum = 355,
    ClearRollStatesAtThreshold = 356,
    AdditionalRaceCategory = 357,
    PrioritizedMainPhaseAction = 358,
    DamageToApplier = 359,
    SelectedCategoryWeaponPower = 360,
    CreateItemFromSkillName = 361,
    RepeatedSelectedWeaponPower = 362,
    DailyLimitOnReceivingRecovery = 363,
    WaiveItemActivationAndCosts = 364,
    ApplyRollOutcome = 365,
    CraftNamedCategoryFromOutcome = 366,
    ProhibitStateCategoryDeclarations = 367,
    DeclarationCostsPaidByHolder = 368,
    RollStatusCleanup = 369,
    ExpireOnApplierNamedSkillSuccess = 370,
    MustIncludeApplier = 372,
    ExpireOnCategoryActivation = 373,
    CannotDeclareSkills = 374,
    AllowNoStateEffect = 375,
    ReplaceEveryAbilityBonus = 376,
    CategoryImmunityToPosition = 377,
    IncreasedStacksAfterLoss = 378,
    PermanentStateAtStacks = 379,
    ReceiveAttackAtRemoval = 380,
    CannotRespondWithTwoCategories = 381,
    DamageAndStateOverStacks = 382,
    MayRefuseGroupMelee = 383,
    TurnStartResourceDelta = 384,
    SnapshotRecipientsAndPower = 385,
    ExpireAfterTriggeredAttack = 387,
    OwnTurnResourceRecovery = 388,
    EquipmentCategoryMasteryList = 389,
    ProhibitTwoDeclarations = 390,
    WearerSharesRewards = 391,
    WearerSharesArea = 392,
    AreaUnavailableWithoutWielder = 393,
    IgnoreDefenseForTurnAttack = 394,
    SignedCategoryCost = 396,
    InitialEquipmentPairState = 397,
    SelectTriggeredInjuryRoll = 398,
    TemporarilyWaiveTraitLimits = 399,
    PartyPrepareBeforeProgressBattle = 400,
    DeferEffectPayment = 401,
    AlliedTurnInitiativeTie = 402,
    RemoveSummonAfterAction = 404,
    NextCategoryAttackAtPowerRatio = 405,
    SetTargetResponseThreshold = 406,
    ResolveCostByResultRange = 407,
    ResolveCostByResultAtLeast = 408,
    TransformSelectedIngredients = 409,
    CraftWithTwoElementsBySize = 410,
    CraftFromAnyItemCategory = 411,
    CraftFromItemCategory = 412,
    CraftFreeItem = 413,
    CraftFromNamedItem = 414,
    CraftGradeShift = 415,
    EnsureCraftRollSuccess = 416,
    FailAllCraftRolls = 417,
    AppraiseForQuestReward = 418,
    AwardBonusResolveForGrade = 419,
    DeferLowResourceState = 420,
    ApplyDeferredStateOnHitOrTurn = 421,
    RemoveSelfNamedState = 422,
    SetTwoResourcesAndStatesAtBattleEnd = 423,
    SetTwoResourcesAndStates = 424,
    IgnoreCategoryEffects = 425,
    AttackWithWeaponSlot = 426,
    FollowUpWithWeaponSlot = 427,
    StackDamageOnTurnAfterApplication = 428,
    RemoveWhenEitherWeaponUnequipped = 429,
    CancelTargetActionCategory = 430,
    ComputeReduction = 431,
    ReduceAndReflectIncomingCategory = 432,
    ReduceReflectedDamage = 433,
    UseSelectedWeaponProperties = 434,
    SummonFromCard = 435,
    OptionalNextPowerReroll = 436,
    DiscardOriginalPowerRoll = 437,
    UseStateWeaponForCategory = 438,
    ReleaseToChooseEffect = 439,
    RevealFutureByGM = 440,
    ChooseNextRollResult = 441,
    ResourceLossOnStateRemoval = 442,
    SnapshotPowerOnApply = 443,
    CapStacksRemovedPerEffect = 444,
    ReviveWithRecoveryAndDamage = 445,
    ScaledDualElementAttack = 446,
    EncounterAllEnemiesAttack = 447,
    GainStacksOnCategoryFumble = 448,
    PermanentCategorizedState = 450,
    ChangeWeaponAndAttack = 451,
    InvalidateCategory = 452,
    RemoveTargetStatesByCount = 453,
    InvalidateCategoryNearThreshold = 454,
    RemoveNamedSelfState = 455,
    AttackWithSummonerSkillValue = 456,
    RemovePetAfterAction = 457,
    NextCategoriesFailureAsFumbleAndDamage = 458,
    PersistUntilAreaCategoryChange = 459,
    RetargetExceptCounterUser = 460,
    ChooseTitle = 461,
    TitleOption = 462,
    ApplyStateByChosenElement = 463,
    ElementStateChoice = 464,
    ProhibitSkillEffectApplication = 465,
    ResourceAfterDelay = 466,
    LoseResourcesOnActiveExceptRace = 467,
    RemoveOnSuccessfulCategoryRoll = 468,
    MeleeGroupElementWeaponAttack = 469,
    GainDefenseFormula = 470,
    DamageIncomingCategoryUser = 471,
    ActionBonusFromStack = 472,
    RemoveAfterActionRoll = 473,
    ScaledSkillValueWithElementDamage = 474,
    GrantDamageAsStacks = 475,
    BlockDeclarationsByStackThreshold = 476,
    ProhibitCategoriesOnStateTurn = 477,
    PreventResourceLossThisTurn = 478,
    RepeatSummonerSkillOnSameTargets = 479,
    ForbidCategoryDeclaration = 480,
    CategoryPairWeaponStackGrant = 481,
    CategoryPairWeaponAttack = 482,
    ModifyTwoResources = 483,
    ForbidCombatExit = 484,
    ForbidDisengagement = 485,
    TurnResourceAndStatRoll = 486,
    LoseTwoResourcesAtBattleEnd = 487,
    MaxResourceDamageEachTurn = 488,
    PartyActsLast = 489,
    PartyLastAndLoseOpeningResource = 490,
    RecoverSelfFlat = 491,
    PreventEquippedWeaponsSelection = 492,
    AttackOriginalTargetNextTurn = 493,
    EntityStateImmunity = 494,
    RerollWithReplacementStat = 495,
    StackTierCategoryRoll = 496,
    StackTierCategoryCost = 497,
    StackTierAttributeOffset = 498,
    StackTierCostAndAttribute = 499,
    StackTierActWithEmptyResource = 500,
    StackTierCooldown = 501,
    StackTierPowerAndHealing = 502,
    StackTierTwoResourceCost = 503,
    StackTierPayAlternateResource = 504,
    ResetStacksOutsideCategory = 505,
    AddOptionalCraftCategory = 506,
    CraftCategoryByIngredientVariety = 507,
    CraftCategoryByNamedIngredient = 508,
    CraftCategoryByItemAndVariety = 509,
    DetermineCraftRank = 510,
    CraftRankByIngredientTotal = 511,
    FatalRollBonusItem = 512,
    AlternateElementRollSuccess = 513,
    WholePartyExit = 514,
    ShareCategoryUntilProgress = 515,
    RenameModifiedEquipment = 516,
    ReplacePriorModifications = 517,
    MakeWeaponUntradeable = 518,
    RollEquipmentSlots = 519,
    RollEquipmentEffect = 520,
    RollOutcomeDice = 521,
    RolledWeaponPowerOffset = 522,
    RolledWeaponCheckOffset = 523,
    RolledDurabilityOffset = 524,
    ChooseDiceOutcome = 525,
    DestroyEquipmentCategory = 526,
    ExpireAfterResolution = 527,
    ExpireAfterApplication = 528,
    ExpireWhenUsingCategory = 529,
    ApplyTimedNamedStates = 530,
    DisengageAffectedTarget = 531,
    AttackElementPowerFormula = 532,
    ModifyOutputCount = 533,
    RollEquipmentSlotResult = 534,
    OrderedRollForRewards = 535,
    MatchConsecutiveDice = 536,
    AwardAllOverlappingMatches = 537,
    RollMatchStacksWithNextReset = 538,
    RepeatSequenceCurrency = 539,
    StraightSequenceCurrencyAndStacks = 540,
    StraightSequenceRewards = 541,
    AlternatingSequenceStacks = 542,
    ExactSequenceStackProductCurrency = 543,
    ExactSequenceRewards = 544,
    ConditionalTripleStackCurrency = 545,
    StateWhileStackThreshold = 546,
    LootRollOnStackGain = 547,
    ExactSequenceTitle = 548,
    RepeatActiveEffect = 549,
    RollNamedRewardTable = 550,
    RewardTableItemRange = 551,
    EquipStateWeaponFormula = 552,
    GroupedElementWeaponAttack = 553
}
// 数値・判定・消費などへの変更。通常の効果内容には入れません。
public enum OverrideContentType
{
    None = 0,
    SetSkillValue = 1,
    MultiplyPower = 2,
    AddEvasionResult = 3,
    SetResourceCost = 4,
    SetCooldown = 5,
    AddResourceCost = 6,
    AddTargetValue = 7,
    SetCritical = 8,
    AddStateDuration = 9,

    MultiplyDamageTaken = 10, // actor, multiplier, optional maximum multiplier
    PreventCounterDamage = 11,
    AddActionResult = 12,
    SetDamageReduction = 13,

    // Component/rule names live in Parameters to avoid one enum value per
    // element, response type, or future attack component.
    SetAttackComponent = 14, // Power|AttributePower, replacement formula
    MultiplyAttackComponent = 15,
    SetAttackRule = 16,
    MultiplyActionResult = 17,

    // actor, stat name, Add|Multiply, value.
    ModifyStat = 18,
    // actor, resource, factor. The action selector belongs to Triggers.
    MultiplyResourceCost = 19,
    // actor, resource, mapped-stack rule key, interval, reduction, selection policy.
    ReduceResourceCostPerMappedStacks = 20,
    // Base modifier kind, selector fields, replacement value.
    SetModifier = 21,
    // Replacement formula for a skill activation roll. The base target is retained.
    SetActivationRollFormula = 22,
    MultiplyResourceDamage = 23, // actor, resource, multiplier, Optional
    SetStackAmount = 24, // actor, state, amount
    MultiplyPowerByStacks = 25, // actor, action category, multipliers for stacks 1..N
    AddTimedActionResult = 26, // Self, CurrentTurn, WeaponPower, weapon category, action category, value
    ModifyAcquisitionCost = 27, // Self, resource, Add|Multiply, AllSkills|Class|SkillCategory, selector, value
    MultiplySalePrice = 28, // NPC, ExceptCategory, category, factor
    MultiplyRecovery = 29, // factor; action category belongs to Triggers
    SetActivationRollTarget = 30, // replacement target for this skill's activation roll
    SetRollRange = 31, // Action, Fumble|Critical, lower inclusive, upper inclusive
    MultiplyDamageReduction = 32, // factor applied to the counter's reduction amount
    MultiplyPowerByTurnActivations = 33, // action category, first ordinal, factors..., maximum (reset each turn)
    StateTurnRecovery = 34, // state name, resource, amount, AnyTurn (spike replacement, not cumulative)
    AttributeWeaknessDamage = 35, // Self, HighestDisadvantageAttributeBonus
    PreventCounterTarget = 36, // ThisEffect
    SetStateControlLimit = 37, // state, Turn, limit
    ConditionalStatePower = 38, // action category, own state, target state, shared melee state, multiplier
    ConditionalStateCost = 39, // same first four selectors, resource, minimum base cost, delta
    SetConditionalStatePower = 40, // replacement multiplier for ConditionalStatePower
    IgnoreDefenseForCategories = 41, // Self, categories... (all)
    MultiplyStateAttackPower = 42, // local state name, factor (captured when applying the state)
    SetAppliedStateDuration = 43, // Target, state, replacement turns
    SetStateDamageMultiplier = 44, // local state, Self, replacement damage multiplier
    SetOnAppliedStateRecovery = 45, // resource, replacement maximum percent
    CriticalCategoryMultiplier = 46, // action category, Power|ActionResult, factor, Stack
    ItemEffectDuration = 47, // Self, AllTargets, added turns
    ItemCapacity = 48, // size, maximum count
    OptionalAreaProgressReduction = 49, // default reduction, area category, replacement reduction, Optional
    MultiplyDeclaredSkillPower = 50, // factor for the selected skill declaration
    CappedStackPower = 51, // action category, stack multiplier formula, maximum multiplier
    AreaStatMultiplier = 52, // present area category, stat, multiplier
    AreaActionResult = 53, // present area category, check stat, added result
    ApplierAttributePower = 54, // action category A AND B, added attribute power (current Applier stats)
    RaceCheckResult = 55, // target race categories (comma separated, OR), check stat, added result
    OwnAttributeBonus = 56, // added race attribute bonus percentage points
    OwnAttributeWeaknessMultiplier = 57, // incoming weak attribute power multiplier formula
    NamedSkillRollResult = 58, // skill name, added activation roll result
    NextActionPower = 59, // matching uses, action category, multiplier
    PowerByAlliedStateTypes = 60, // action category, multiplier formula; distinct Target states applied by Self/allies
    ThisSkillCriticalRange = 61, // inclusive lower and upper activation dice bounds
    CriticalThresholdDelta = 62, // signed delta to this skill's critical threshold
    ItemUseCostDelta = 63, // item category, resource, signed delta
    PhaseDamageMultiplier = 64, // phase A OR B, incoming damage multiplier
    PowerAgainstState = 65, // target state before this attack, multiplier (this skill only)
    WeaponPowerAgainstState = 66, // weapon category, target state before this attack, multiplier
    IncomingCategoriesDamageMultiplier = 67, // incoming categories (comma separated, OR), multiplier
    EquippedSkillLimit = 68, // maximum equipped skill count formula (not acquired skill count)
    ThisSkillStackCost = 69, // own stack state, resource, reduction per stack, minimum cost
    ThisSkillStackPower = 70, // own stack state, minimum stacks, multiplier; before stack removal
    OptionalSpikeState = 71, // resource A, resource B, cost each, granted own state, battle use limit; unlocked by spike stage
    AllBonusStatsMultiplier = 72, // all ability bonuses B, multiplier (not base ability values)
    SetIncomingReductionRatio = 73,
    IgnoreCharacterStatesForPower = 74,
    MultiplyCategoryActivationResult = 75,
    MultiplyCategoryCheckResult = 76,
    SetCategoryAcquisitionCost = 77,
    RepeatedCategoryTurnCost = 78,
    ReduceIncomingFromRace = 79,
    PhaseCheckMultiplier = 80,
    IgnoreAmountOfDefense = 81,
    EquippedBattleStartingResource = 82,
    AddExcessDefensePenetration = 83,
    SharedMeleeStatePower = 84,
    AttackPowerFormulaFactor = 85,
    StateTargetPower = 86,
    NamedSkillResultMultiplier = 87,
    SetRoleplayDescription = 88,
    OwnStateCategoryResult = 89,
    FailedDefenseIncomingMultiplier = 90,
    RacialAbilityPercent = 91,
    RacialElementPercent = 92,
    CategoryActivationTargetDelta = 93,
    CategoryIncomingReduction = 94,
    ResourceIncomingReduction = 95,
    InjuryRollThreshold = 96,
    EmptySlotDefensePerStack = 97,
    ReplaceSourceDamageWithElement = 98,
    IncomingSkillTargetDelta = 99,
    CategoryDamageTakenFactor = 100,
    CombinedCategoryPower = 101,
    CategorySpikeRankIncrease = 102,
    SetFixedBonusStat = 103,
    StackCapFromPossessedItems = 104,
    ReduceCostPerSpentItem = 105,
    CounterActivationTargetDelta = 106,
    TwoCategoryAttributePower = 107,
    ReplaceTwoStatsFromThird = 108,
    CheckTargetDeltaByStat = 109,
    IncomingElementCategoryResult = 110,
    OutgoingPowerFactor = 111,
    PerTurnNamedSkillCostReplacement = 112,
    CategoryIncomingDamage = 113,
    CounterResultByCategory = 114,
    CategoryTargetFactor = 115,
    TwoCategoryTargetFactor = 116,
    IncomingStackCountMultiplier = 117,
    SingleElementDamageFromStat = 118,
    EitherElementDamageFromStat = 119,
    FailedDefenseOutgoingDamage = 120,
    RecoveryItemDuration = 121,
    SeveralStatChecksFactor = 122,
    CategoryListCheckAddition = 123,
    StateBonusAddition = 124,
    StateStatFactor = 125,
    FixedStatValue = 126,
    IncomingDamageThisTurn = 127,
    TargetStateStackFactor = 128,
    CategoryPairWeaponPower = 129,
    CounterTargetFactor = 130,
    CounterResourceCostDelta = 131,
    AlliesFlatDefense = 132,
    CategoryLearnCostReduction = 133,
    ClassCategoryLearnCostFactor = 134,
    OpposedResultFromRollTotal = 135,
    ReplaceStacksIfSlotEmpty = 136,
    NextDoubleCategoryPowerCost = 137,
    MountedRangedRollBonuses = 138,
    CounterCategoryPower = 139,
    ProhibitResponseTarget = 140,
    StandbySelfCheckFactor = 141,
    AmazingRollBonus = 142,
    TriggerCategoryResultDelta = 143,
    CounterToIncomingCategoryFactor = 144,
    DropRollFactor = 145,
    IncomingWithSelectedWeapons = 146,
    SelectedWeaponsTargetAndPower = 147,
    CheckDeltaExceptCategory = 148,
    RequireAdditionalSuccessfulResponses = 149,
    TargetResultFactor = 150,
    DeclarationResourceCostFactor = 151,
    PowerAtHalfTargetHP = 152,
    NonCharacterPowerFactor = 153,
    OptionalIncomingStatResultFactor = 154,
    TwoSourcesIncomingReduction = 155,
    ElementDamageTakenFactor = 156,
    SetGrantedStackCount = 157,
    ReplaceStatBonusIncrease = 158,
    StandbyTargetCheckFactor = 159,
    AddPowerFormula = 160,
    TargetStateCheckFactor = 161,
    ResultWhenLackingState = 162,
    OrderedActionResultDelta = 163,
    NoSpikeChange = 165,
    IncreaseRacialBonusByPosition = 166,
    LearnNamedSkillAtRank = 167,
    SetAdditionalRacialSkillCount = 168,
    SetPartyDefenseBonus = 169,
    SetResourceDamageAtRank = 170,
    SetActivationResultFactor = 171,
    SetInjuryResourceThreshold = 172,
    SetStatePowerIncrease = 173,
    SetWeaponCategoryPower = 174,
    SetTwoResourceCosts = 175,
    SetEntityActivationRoll = 176,
    SetActiveResourceRecovery = 177,
    SetNamedStateDamage = 178,
    SetDefenseIgnoredAtRank = 180,
    MaxResourceMultiplier = 182,
    IncomingNamedSkillFactor = 183,
    OutgoingCategoryDamage = 184,
    MeleeOrCategoryIncomingDamage = 185,
    ActionCategoryActivationTargetFactor = 186,
    IncomingFromApplier = 187,
    SameMeleeCategoryPower = 188,
    BattleOpeningActionAndPower = 189,
    BattleUntilEndIncomingFactor = 190,
    StackCostAndTurnLoss = 191,
    HPThresholdBlocksAction = 192,
    CombinedResourceThresholdBlock = 193,
    SelectedDefenseResourceFactor = 194,
    StackScaleNamedSkill = 195,
    CheckByAbilityFactor = 196,
    CheckByCategoryFactor = 197,
    BonusesInNamedEnvironment = 198,
    TakeCategoryPairDamage = 199,
    RollByCategoryActivation = 200,
    StatFlatBonus = 201,
    PartyBattleEndResourceRecovery = 202,
    TwoWeaponsArmorPiercing = 203,
    ArmorSetMaxResource = 204,
    EffectAgainstRaceByWeapon = 205,
    ExtraRewardsOnAmazing = 206,
    SetTargetResponseThreshold = 207,
    HealMaxResourcePair = 208,
    CraftQuantityRankBonus = 209,
    RepeatCategoryPowerProgression = 210,
    SetRepeatPowerProgression = 211,
    StackMaxResourceProgression = 212,
    StackPowerProgression = 213,
    EmptySlotStackFormula = 214,
    SetStateBattleEndStackRecovery = 215,
    DefenseFromDefenseFactor = 217,
    EitherSelectedWeaponCategoryPower = 218,
    EncounterStatRollFactor = 219,
    ReplaceResourceLoss = 220,
    SetItemCostAndStackGain = 221,
    ScaleDamageReductionByVariable = 222,
    ReducedCategoryCost = 223,
    SetActivationRollBonus = 224,
    SetStateResourceLossRatio = 225,
    NextStatResultPenalty = 226,
    SetDamageAndResourcesPercent = 227,
    CategoryCostOffset = 228,
    CategorySelfDamageScale = 229,
    ReplaceStackQuantity = 230,
    StackGrantMultiplier = 231,
    IncomingCategoryTargetRollOffset = 232,
    QualifyNamedSkill = 233,
    ElementBonusScale = 234,
    AdvanceActionWithCap = 235,
    AllyBuffFactor = 236,
    SetResourceCostFixed = 237,
    TargetThresholdOffset = 238,
    CounterRollUseLowest = 239,
    CategoryStackAndRecoveryScale = 240,
    AreaTargetMultiplier = 241,
    SetDelayedStateSkillValue = 242,
    StatRollFactorForAction = 243,
    ReplacePassiveStatBonus = 244,
    CategoryAchievementScale = 245
}
[Serializable]
public class OverrideDefinition
{
    public EffectType Type;
    // 空なら効果種別に従う。複数トリガーはORであり、同じイベントには1回だけ適用します。
    public List<TriggerDefinition> Triggers = new List<TriggerDefinition>();
    public List<OverrideContent> Contents = new List<OverrideContent>();
}
[Serializable]
public class OverrideContent
{
    public OverrideContentType Type;
    public List<string> Parameters = new List<string>();
}
[Serializable]
public class EffectContent
{
    public EffectContentType Type;
    public List<string> Parameters = new List<string>();
}
