using System;
using System.Collections.Generic;

/// <summary>出目・条件に対応して適用する結果の種類。</summary>
public enum AreaEncounterOutcome
{
    /// <summary>指定した敵の組と戦闘を開始する。</summary>
    Encounter,
    /// <summary>名前の付いたイベントまたは罠を発動する。</summary>
    Event,
    /// <summary>何も起きないと明記された結果。</summary>
    Nothing,
    /// <summary>指定したスキルの宣言から現在の進行ロールをやり直す。</summary>
    Retry,
    /// <summary>指定した称号を獲得する。</summary>
    Title,
    /// <summary>個別の条件や複数段階の戦闘を定める行。</summary>
    Rule
}

[Serializable]
public class AreaEncounterEnemy
{
    public string Name = "";
    // 0 は原典に個体数の記載がない場合。
    public int Count;
}

[Serializable]
public class AreaEncounterResult
{
    public AreaEncounterOutcome Type;
    public List<AreaEncounterEnemy> Enemies = new List<AreaEncounterEnemy>();
    public string Value = "";
    public int Repetitions = 1;
}

[Serializable]
public class AreaRollEntry
{
    // Minimum/Maximum は出目の場合だけ、Sequence は何戦目かを指定する場合だけ使用する。
    public int Minimum;
    public int Maximum;
    public int Sequence;
    public string Label = "";
    public string Condition = "";
    public string Check = "";
    public int Target;
    public List<ResourceCost> Costs = new List<ResourceCost>();
    public AreaEncounterResult Result = new AreaEncounterResult();
    public List<AreaEncounterEnemy> OnFailure = new List<AreaEncounterEnemy>();
}

[Serializable]
public class AreaEncounterEvent
{
    public int Id;
    public SkillTextData Action = new SkillTextData();
    public string FollowUpCondition = "";
    public string FollowUpDice = "";
    public List<AreaRollEntry> FollowUpEntries = new List<AreaRollEntry>();
    public string Flavor = "";
}

[Serializable]
public class AreaEncounterRow : AreaRollEntry
{
    public int EventId;
}

[Serializable]
public class AreaRollTable
{
    public string Name = "";
    public List<AreaActionEntry> Requirements = new List<AreaActionEntry>();
    public string Dice = "";
    public int Rolls;
    public bool AfterProgress;
    public bool NoProgressRoll;
    public List<AreaEncounterRow> Entries = new List<AreaEncounterRow>();
}
