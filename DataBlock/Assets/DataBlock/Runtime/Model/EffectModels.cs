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
    CharacterIncapacitated = 17
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
    GainStack = 9,
    Damage = 10,
    ApplyStateInMeleeGroup = 11,
    InvalidateIncomingAction = 12,
    InvalidateTriggeredEffect = 13,
    SkillAttack = 14,
    ModifyResource = 15,
    InvalidateAction = 16,
    ReduceDamage = 17,
    ReapplyEffects = 18,
    RollDice = 19,
    Summon = 20
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

    MultiplyDamageTaken = 10,
    PreventCounterDamage = 11,
    AddActionResult = 12,
    SetDamageReduction = 13,

    // Component/rule names live in Parameters to avoid one enum value per
    // element, response type, or future attack component.
    SetAttackComponent = 14,
    MultiplyAttackComponent = 15,
    SetAttackRule = 16,
    MultiplyActionResult = 17
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
