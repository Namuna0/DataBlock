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
    ResourceRecovery = 25 // when a resource restoration amount is determined
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
    UseStateDefinitionAtStacks = 49 // GreaterThan, threshold, definition name, ReplaceEffects, KeepIdentityAndStacks
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
    StateTurnRecovery = 34 // state name, resource, amount, AnyTurn (spike replacement, not cumulative)
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
