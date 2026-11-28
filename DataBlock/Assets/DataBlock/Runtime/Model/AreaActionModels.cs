using System;
using System.Collections.Generic;

/// <summary>エリアの施設、採取、移動、環境の各項目が持つ情報の種類。</summary>
public enum AreaActionEntryType
{
    /// <summary>役割や物語の説明、および個別の裁定を原文で保持する。</summary>
    Description,
    /// <summary>治安の区分とその数値を示す。</summary>
    Security,
    /// <summary>参照するエリア、クエスト、スキルなどの名前を列挙する。</summary>
    References,
    /// <summary>施設の料金または移動・購入する対象の価格を示す。</summary>
    Price,
    /// <summary>指定の判定を行い、成功時に報酬または追加処理を適用する。</summary>
    Check,
    /// <summary>発動判定または出目表のダイスを指定する。</summary>
    Roll,
    /// <summary>無条件または指定した出目の範囲で得るアイテム・結果を示す。</summary>
    Reward,
    /// <summary>スキル・装備等と共通の効果を適用する。</summary>
    Effects,
    /// <summary>スキル等と共通の消費リソースを指定する。</summary>
    Costs,
    /// <summary>スキル等と共通の宣言条件を指定する。</summary>
    Conditions,
    /// <summary>エリアでの移動・進行・環境に固有のルールと引数を指定する。</summary>
    AreaRule,
    /// <summary>共通の状態定義をここで記述する。</summary>
    State,
    /// <summary>一連の商品やサービスに付けた小見出しを示す。</summary>
    Subheading,
    /// <summary>エリア内で定義された行動などの分類を示す。</summary>
    Categories
}

[Serializable]
public sealed class AreaActionDefinition
{
    public string Name = "";
    public List<AreaActionEntry> Entries = new List<AreaActionEntry>();
}

[Serializable]
public sealed class AreaActionEntry
{
    public AreaActionEntryType Type;
    // 【項目】、採取場所や選択肢の名前。無記名の行は空欄。
    public string Field = "";
    public string Subject = "";
    public List<string> Names = new List<string>();
    public List<string> Categories = new List<string>();
    public int Value;
    public string Currency = "";
    // 採取などの必要成功回数。0は指定なし、-1は全ての判定に成功すること。
    public int RequiredSuccesses;
    public bool MinimumSuccesses;
    public string Rule = "";
    public List<string> Parameters = new List<string>();
    public string Requirement = "";
    public List<string> RequirementParameters = new List<string>();
    public string Note = "";
    public DiceRollDefinition Roll;
    public EnemyDropDefinition Reward;
    public List<EffectContent> Effects = new List<EffectContent>();
    public List<ResourceCost> Costs = new List<ResourceCost>();
    public ConditionSet Conditions;
}
