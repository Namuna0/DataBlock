using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>装備の価値を通貨額で示すか、取引不可とするかを表す。</summary>
public enum EquipmentValueKind
{
    /// <summary>装備の価値を金額と通貨名で表す。</summary>
    Currency = 0,
    /// <summary>取引できない装備として扱う。</summary>
    Untradeable = 1,
    /// <summary>資料に価値の記載がない。</summary>
    Unspecified = 2
}

[Serializable]
public sealed class EquipmentValueDefinition
{
    public EquipmentValueKind Kind;
    [Min(0)] public int Amount;
    public string Currency = "";
    [Min(1)] public int Quantity = 1;
}

/// <summary>キャラクターが装備を身に着けるために満たす条件の種類を表す。</summary>
public enum EquipmentRequirementType
{
    /// <summary>装備者のレベルが指定値以上であることを要求する。</summary>
    MinimumLevel = 0,
    /// <summary>指定した能力値が下限以上であることを要求する。</summary>
    MinimumStat,
    /// <summary>指定した能力値が上限以下であることを要求する。</summary>
    MaximumStat,
    /// <summary>指定したスキルを習得していることを要求する。</summary>
    LearnedSkill,
    /// <summary>指定した称号を習得していることを要求する。</summary>
    LearnedTitle
}

[Serializable]
public sealed class EquipmentRequirementDefinition
{
    public EquipmentRequirementType Type;
    public List<string> Parameters = new List<string>();
}

[Serializable]
public sealed class EquipmentEffectGroup
{
    public EquipmentEffectSection Section;
    // Empty for an ordinary equipment-effect line; otherwise written as 《Name》：.
    public string Name = "";
    public List<EffectContent> Effects = new List<EffectContent>();
}

[Serializable]
public sealed class EquipmentGradeRuleReplacement
{
    // 対象効果の意味コードと、その効果で置き換える数値。
    public string Rule = "";
    public int Amount;
}

[Serializable]
public sealed class EquipmentGradeAdjustmentDefinition
{
    [Min(1)] public int Grade = 1;
    public int MaxDurabilityDelta;
    // 選択した武器の威力式への加減算。
    public int WeaponPowerDelta;
    public List<EquipmentGradeRuleReplacement> Replacements = new List<EquipmentGradeRuleReplacement>();
    public List<EffectContent> Modifiers = new List<EffectContent>();
}

[Serializable]
public sealed class EquipmentWeaponMode
{
    public string Name = "";
    public string WeaponPowerFormula = "";
    public List<EquipmentEffectGroup> EffectGroups = new List<EquipmentEffectGroup>();
}

/// <summary>効果が装備・所持・設置のどの欄から発生するかを表す。</summary>
public enum EquipmentEffectSection
{
    /// <summary>装備して適用する効果。</summary>
    Equipped,
    /// <summary>アイテムのパッシブ効果。</summary>
    Passive,
    /// <summary>アイテムを設置したエリアで適用する効果。</summary>
    Installed
}

[Serializable]
public sealed class EquipmentDefinition
{
    public string Name = "";
    public List<string> Categories = new List<string>();
    public string Rarity = "";
    // 0 はグレードの記載なし。★で示された特殊グレードは原記号を保持する。
    [Min(0)] public int Grade;
    public string SpecialGrade = "";
    // Nonzero only for a concrete non-base variant written as 《Name》☆N.
    [Min(0)] public int VariantGrade;
    public EquipmentValueDefinition Value = new EquipmentValueDefinition { Kind = EquipmentValueKind.Unspecified };
    public List<string> EquipSlots = new List<string>();
    public List<EquipmentRequirementDefinition> EquipRequirements = new List<EquipmentRequirementDefinition>();
    public string Size = "";
    // 0 は記載なし、-1 は上限なし（∞）。
    [Min(-1)] public int MaxDurability;
    [Min(0)] public int RequiredSpace;
    // 資料で威力式に付けられた名前。計算式とは分離する。
    public string WeaponPowerName = "";
    // 個別の威力式を持たない武器にも、解決済みグレードの加算値を保持できる。
    public int WeaponPowerBonus;
    public string WeaponPowerFormula = "";
    public List<EquipmentEffectGroup> EffectGroups = new List<EquipmentEffectGroup>();
    public List<EquipmentWeaponMode> WeaponModes = new List<EquipmentWeaponMode>();
    public List<StateDefinition> States = new List<StateDefinition>();
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
