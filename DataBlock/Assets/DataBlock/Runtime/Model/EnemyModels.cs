using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyActionTiming
{
    Turn = 0,
    Reaction = 1
}

public enum EnemyActionTargetSelector
{
    None = 0,
    RandomSelectableEnemy = 1,
    RandomSelectableEnemyInMeleeGroup = 2
}

public enum EnemyActionConditionType
{
    OwnState = 0,
    NoSelectableEnemyInMeleeGroup = 1,
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
