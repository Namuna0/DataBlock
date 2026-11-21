using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>装備の価値を通貨額で示すか、取引不可とするかを表す。</summary>
public enum EquipmentValueKind
{
    /// <summary>装備の価値を金額と通貨名で表す。</summary>
    Currency = 0,
    /// <summary>取引できない装備として扱う。</summary>
    Untradeable = 1
}

[Serializable]
public sealed class EquipmentValueDefinition
{
    public EquipmentValueKind Kind;
    [Min(0)] public int Amount;
    public string Currency = "";
}

/// <summary>キャラクターが装備を身に着けるために満たす条件の種類を表す。</summary>
public enum EquipmentRequirementType
{
    /// <summary>装備者のレベルが指定値以上であることを要求する。</summary>
    MinimumLevel = 0
}

[Serializable]
public sealed class EquipmentRequirementDefinition
{
    public EquipmentRequirementType Type;
    public List<string> Parameters = new List<string>();
}

/// <summary>装備による能力値の変化、行為の補正、使用許可、状態付与などの効果を表す。</summary>
public enum EquipmentEffectType
{
    // actor, stat, Add|Multiply, value
    /// <summary>装備者の指定能力値に数値を加算するか、倍率を掛ける。</summary>
    ModifyStat = 0,
    // action category, result factor, power factor
    /// <summary>この武器を選んで行う指定カテゴリーの行為の達成値と威力に、それぞれ倍率を掛ける。</summary>
    ModifySelectedWeaponAction = 1,
    // actor, action category, factor
    /// <summary>指定カテゴリーの行為によって装備者が受けるダメージに倍率を掛ける。</summary>
    MultiplyIncomingDamage = 2,
    // actor, encounter phase, item category
    /// <summary>指定エンカウントフェーズに指定カテゴリーのアイテムの使用を宣言できる。</summary>
    AllowItemUse = 3,
    // actor, action category, factor
    /// <summary>装備者が行う指定カテゴリーの行為の威力に倍率を掛ける。</summary>
    MultiplyActionPower = 4,
    // actor, action category, state, amount
    /// <summary>装備者が指定カテゴリーの行為を発動した時に、指定状態のスタックを得る。</summary>
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
