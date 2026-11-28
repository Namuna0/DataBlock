using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>グレードごとに変化する、アイテム使用時の効果の種類。</summary>
public enum ItemGradeEffect
{
    /// <summary>そのグレードでは追加効果を適用しない。</summary>
    None,
    /// <summary>最大値を基準とする回復率に増減するパーセントポイント。</summary>
    RecoveryPercentagePoints,
    /// <summary>属性ボーナス上昇の倍率を指定値に置き換える。</summary>
    ElementBonusMultiplier,
    /// <summary>対象から指定状態を任意で解除できる。</summary>
    OptionalRemoveState,
    /// <summary>対象が指定の行為判定に成功すると、指定状態を任意で解除できる。</summary>
    OptionalRemoveStateOnCheck
}

[Serializable]
public sealed class ItemGradeAdjustment
{
    [Min(1)] public int Grade;
    public bool AndAbove;
    public ItemGradeEffect Type;
    public List<string> Parameters = new List<string>();
}

[Serializable]
public sealed class ItemStockEntry
{
    public string Name = "";
    [Min(0)] public int Price;
    [Min(0)] public int Quantity;
}

[Serializable]
public sealed class ItemDefinition
{
    public string Name = "";
    public List<string> Categories = new List<string>();
    public string Rarity = "";
    [Min(0)] public int Grade;
    public string SpecialGrade = "";
    // 《名前》☆N として使用する時の実グレード。0は記載グレードを使用。
    [Min(0)] public int VariantGrade;
    public EquipmentValueDefinition Value = new EquipmentValueDefinition { Kind = EquipmentValueKind.Unspecified };
    public string Size = "";
    // 捕獲HP、カードのCOST、ペットの能力値など、記載された項目だけを順序通り保持する。
    public List<FormulaStat> Stats = new List<FormulaStat>();
    public string HoldingLimitCategory = "";
    [Min(0)] public int HoldingLimit;
    // 選択肢全体に共通するレシピ書の宣言条件。
    public ConditionSet DeclarationConditions = new ConditionSet();
    public bool InstalledEffect;
    // 設置した店舗の取扱商品。所有者が品目・価格（ジルダ）・在庫数を設定する。
    public List<ItemStockEntry> Stock = new List<ItemStockEntry>();
    // 発動させない保留中の設置効果。未確定の原文だけを保持する。
    [TextArea(2, 6)] public string DeferredInstalledEffect = "";
    public List<ItemGradeAdjustment> GradeAdjustments = new List<ItemGradeAdjustment>();
    public List<EnemyActionRule> Actions = new List<EnemyActionRule>();
    public List<SkillTextData> Traits = new List<SkillTextData>();
    // 有効ならSkillsの先頭がアイテム本体の使用・所持・設置効果。付属スキルの名称とも区別する。
    public bool HasOwnSkill;
    // 本体の使用効果に続く付属スキル、またはレシピ書の選択肢。
    public List<SkillTextData> Skills = new List<SkillTextData>();
    [TextArea(3, 10)] public string Flavor = "";
}

[Serializable]
public sealed class ItemTextData
{
    public ItemDefinition Item = new ItemDefinition();
}
