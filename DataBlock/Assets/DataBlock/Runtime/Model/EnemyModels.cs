using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>エネミーの行動条件をいつ評価するかを表す。毎ターンの行動と他の行為への反応を区別する。</summary>
public enum EnemyActionTiming
{
    /// <summary>ターンごとの行動としてスキルの発動条件を判定する。</summary>
    Turn = 0,
    /// <summary>ほかの行為に対する反応としてスキルの発動条件を判定する。</summary>
    Reaction = 1
}

/// <summary>エネミーがスキルの対象を選ぶ範囲と方法を表す。</summary>
public enum EnemyActionTargetSelector
{
    /// <summary>行動ルールで新たな対象の選び方を指定しない。</summary>
    None = 0,
    /// <summary>選択可能な敵全体からランダムに選ぶ。</summary>
    RandomSelectableEnemy = 1,
    /// <summary>自身と同じ接近グループにいる選択可能な敵からランダムに選ぶ。</summary>
    RandomSelectableEnemyInMeleeGroup = 2
}

/// <summary>エネミーがスキルを使うかを判断する条件の種類。個々の条件は反転して使用できる。</summary>
public enum EnemyActionConditionType
{
    /// <summary>自身が指定状態であるかを判定する。</summary>
    OwnState = 0,
    /// <summary>自身と同じ接近グループに選択可能な敵がいない時に成立する。</summary>
    NoSelectableEnemyInMeleeGroup = 1,
    /// <summary>自身に対して指定カテゴリーの行為が行われた時に成立する。</summary>
    IncomingAction = 2
}

[Serializable]
public sealed class EnemyActionCondition
{
    public EnemyActionConditionType Type;
    public string Value = "";
    public bool Negated;
}

[Serializable]
public sealed class EnemyActionRule
{
    public EnemyActionTiming Timing;
    [Min(0)] public int UsesPerTurn;
    public List<EnemyActionCondition> AllConditions = new List<EnemyActionCondition>();
    public List<EnemyActionCondition> AnyConditions = new List<EnemyActionCondition>();
    public EnemyActionTargetSelector TargetSelector;
    public string SkillName = "";
}

[Serializable]
public sealed class EnemyStatDefinition
{
    public string Name = "";
    public string Formula = "";
}

[Serializable]
public sealed class EnemyDropDefinition
{
    [Min(1)] public int Minimum;
    [Min(1)] public int Maximum;
    public string ItemName = "";
    [Min(1)] public int Amount = 1;
}

[Serializable]
public sealed class EnemyDefinition
{
    public string Name = "";
    public List<string> Categories = new List<string>();
    public string DangerLevel = "";
    public List<EnemyStatDefinition> Stats = new List<EnemyStatDefinition>();
    public List<EnemyActionRule> Actions = new List<EnemyActionRule>();
    public List<SkillTextData> Skills = new List<SkillTextData>();
    public List<EnemyDropDefinition> Drops = new List<EnemyDropDefinition>();
    [TextArea(3, 10)] public string Flavor = "";
}

[Serializable]
public sealed class EnemyTextData
{
    public EnemyDefinition Enemy = new EnemyDefinition();
}
