using System;
using System.Collections.Generic;
using UnityEngine;

public enum CostKind
{
    Resource = 0,
    ItemCategory = 1,
    ItemName = 2
}
[Serializable]
public class ResourceCost
{
    public CostKind Kind;
    public string Resource = "";
    [Min(0)] public int Amount;
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
    AttackHit = 26 // this skill's attack hit its target (not merely a successful roll)
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
    ThirdSpike = 9
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
    GainStack = 9, // actor, state, amount, optional maximum stacks
    Damage = 10,
    ApplyStateInMeleeGroup = 11,
    InvalidateIncomingAction = 12,
    InvalidateTriggeredEffect = 13,
    SkillAttack = 14, // actor, power | actor, PowerAndAttribute, power, attribute power | ElementalWeapon form
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
    OutsiderRule = 47, // dedicated rule key followed by typed parameters; see MAGIC_SKILL_SUPPORT.md
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
    UnmodifiableMaximumResourceDamage = 109 // target resource, maximum percent; no power/damage increases or reductions from defense/effects/states
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
    AllBonusStatsMultiplier = 72 // all ability bonuses B, multiplier (not base ability values)
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
