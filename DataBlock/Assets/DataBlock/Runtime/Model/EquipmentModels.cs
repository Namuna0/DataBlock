using System;
using System.Collections.Generic;
using UnityEngine;

public enum EquipmentValueKind
{
    Currency = 0,
    Untradeable = 1
}

[Serializable]
public sealed class EquipmentValueDefinition
{
    public EquipmentValueKind Kind;
    [Min(0)] public int Amount;
    public string Currency = "";
}

public enum EquipmentRequirementType
{
    MinimumLevel = 0
}

[Serializable]
public sealed class EquipmentRequirementDefinition
{
    public EquipmentRequirementType Type;
    public List<string> Parameters = new List<string>();
}

public enum EquipmentEffectType
{
    // actor, stat, Add|Multiply, value
    ModifyStat = 0,
    // action category, result factor, power factor
    ModifySelectedWeaponAction = 1,
    // actor, action category, factor
    MultiplyIncomingDamage = 2,
    // actor, encounter phase, item category
    AllowItemUse = 3,
    // actor, action category, factor
    MultiplyActionPower = 4,
    // actor, action category, state, amount
    GainStateStacksOnAction = 5
}

[Serializable]
public sealed class EquipmentEffectDefinition
{
    public EquipmentEffectType Type;
    public List<string> Parameters = new List<string>();
}

[Serializable]
public sealed class EquipmentEffectGroup
{
    // Empty for an ordinary equipment-effect line; otherwise written as 《Name》：.
    public string Name = "";
    public List<EquipmentEffectDefinition> Effects = new List<EquipmentEffectDefinition>();
}

[Serializable]
public sealed class EquipmentStateDefinition
{
    public string Name = "";
    public List<string> Categories = new List<string>();
    public List<EquipmentEffectDefinition> Modifiers = new List<EquipmentEffectDefinition>();
}

[Serializable]
public sealed class EquipmentGradeAdjustmentDefinition
{
    [Min(1)] public int Grade = 1;
    public int MaxDurabilityDelta;
    public List<EquipmentEffectDefinition> Modifiers = new List<EquipmentEffectDefinition>();
}

[Serializable]
public sealed class EquipmentDefinition
{
    public string Name = "";
    public List<string> Categories = new List<string>();
    public string Rarity = "";
    [Min(1)] public int Grade = 1;
    // Nonzero only for a concrete non-base variant written as 《Name》☆N.
    [Min(0)] public int VariantGrade;
    public EquipmentValueDefinition Value = new EquipmentValueDefinition();
    public List<string> EquipSlots = new List<string>();
    public List<EquipmentRequirementDefinition> EquipRequirements = new List<EquipmentRequirementDefinition>();
    public string Size = "";
    [Min(1)] public int MaxDurability = 1;
    public string WeaponPowerFormula = "";
    public List<EquipmentEffectGroup> EffectGroups = new List<EquipmentEffectGroup>();
    public List<EquipmentStateDefinition> States = new List<EquipmentStateDefinition>();
    public List<EquipmentGradeAdjustmentDefinition> GradeAdjustments =
        new List<EquipmentGradeAdjustmentDefinition>();
    // The equipment root stays independent; granted skills reuse the shared skill grammar.
    public List<SkillTextData> Skills = new List<SkillTextData>();
    [TextArea(3, 10)] public string Flavor = "";
}

[Serializable]
public sealed class EquipmentTextData
{
    public EquipmentDefinition Equipment = new EquipmentDefinition();
}
