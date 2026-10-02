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
    MinimumEquippedDays = 39,
    InParty = 40, // actor
    HasMealEffect = 41, // actor, True|False
    TurnNumber = 42, // 1-based turn number
    ReactionTarget = 43, // category, Self, Source | category, Ally, Receiver, Automatic
    CheckContext = 44, // Activation | Environment | Crafting, equipment categories...
    BattleTurnRange = 45, // first turn, last turn (inclusive)
    OwnStateCategories = 46, // Any, categories... (not individual state names)
    ActionName = 47, // named skill, not a category
    HasAttributeBonusPower = 48, // attribute-bonus-derived power exists on this attack
    CounterToOwnActive = 49, // the triggering counter responds to this actor's active effect
    EncounterPhase = 50,
    SelectCharacterSkill = 51, // character count, effect kind, skill count
    ExcludeTargetRaces = 52, // character count, excluded race categories...
    TargetStackMinimum = 53, // state, minimum, character count
    SelectAnyTarget = 54, // count (including non-character roleplay targets)
    WithoutOwnState = 55, // state
    IncapacitatedCheckReaction = 56, // state, Battle, limit, AnyCharacterAction, reset action category
    AtRaceSelection = 57, // unspecified optional/automatic acquisition
    LethalIncomingAction = 58, // resource, AtMost, threshold, action category
    SelectEquipmentSkill = 59, // count, resource, AtMost, maximum cost, NoMeleeRequirement
    ActionTowardsSelf = 60, // action category, Source
    SelectCarriedWeapon = 61, // count, AllowUnarmed
    AreaWithoutCategory = 62, // excluded area category
    SelectCharactersWithState = 63, // state, count, optional Exact|UpTo (omitted = Exact)
    AutomaticEnemyAction = 64, // triggering action category; target its enemy source
    OwnStateApplied = 65 // declaration on becoming this state, not while remaining in it
}
[Serializable]
public class ConditionEntry
{
    public ConditionType Type;
    public List<string> Parameters = new List<string>();
}
