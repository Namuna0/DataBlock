using System;
using System.Collections.Generic;

[Serializable]
public class ConditionSet
{
    // 全And条件 AND (Orが空、またはOr条件のいずれか)。空の集合は追加制約なし。
    public List<ConditionEntry> And = new List<ConditionEntry>();
    public List<ConditionEntry> Or = new List<ConditionEntry>();
}
public enum ConditionType
{
    None = 0,
    OptionalAtRaceSelection = 1,
    SelectCharacters = 2,
    ActionSource = 3,
    ActionCategory = 4,
    RelatedStateRemoved = 5,
    SpendResource = 6,
    SelectEquippedWeapon = 7,
    SelectMeleeCharacters = 8,
    SelectConsumedItem = 9,
    SelectedItemConditionsMet = 10,
    AttackAppliedState = 11,
    SelectOwnState = 12,
    ReceiveFromCharacter = 13,
    EquippedCategory = 14,
    WeaponCategory = 15,
    IsWeaponAttack = 16,
    RollSucceeded = 17,
    SkillDealtNoDamage = 18,
    BattleEffectLimit = 19,
    EffectInvolvesSelf = 20,
    RequiresSameMeleeGroup = 21,
    ExcludedWeaponCategories = 22,
    OwnState = 23,

    SelectEquippedWeaponFromCategories = 24,
    ActionTarget = 25,
    EffectKind = 26,
    ActionOrigin = 27,
    MinimumStat = 28,
    ActivationLimit = 29,
    DeclarationTiming = 30,
    AutomaticActivation = 31,
    RollResult = 32,
    ResourceRatio = 33,
    ActionStat = 34,
    TurnOwner = 35,

    AutomaticAtRaceSelection = 36,
    ResourceValue = 37,
    MappedStateStackInterval = 38,
    MinimumEquippedDays = 39
}
[Serializable]
public class ConditionEntry
{
    public ConditionType Type;
    public List<string> Parameters = new List<string>();
}
