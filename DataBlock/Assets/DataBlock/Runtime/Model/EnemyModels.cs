using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>エネミーの行動を評価するタイミング。</summary>
public enum EnemyActionTiming
{
    /// <summary>毎ターンの行動。</summary>
    Turn = 0,
    /// <summary>指定された行為や状態付与への反応。</summary>
    Reaction = 1,
    /// <summary>スタンバイフェーズの行動。</summary>
    Standby = 2,
    /// <summary>エンカウントフェーズの行動。</summary>
    Encounter = 3,
    /// <summary>スタンバイフェーズと各ターンに行う行動。</summary>
    StandbyAndTurn = 4,
    /// <summary>戦闘開始から指定したターンの行動。</summary>
    BattleTurn = 5,
    /// <summary>戦闘開始から指定したターンの開始時の行動。</summary>
    BattleTurnStart = 6,
    /// <summary>ターン回数が定められていない条件付き行動。</summary>
    Conditional = 7,
    /// <summary>行動しない。</summary>
    None = 8,
    /// <summary>名前のみが列挙され、条件・頻度が未指定の行動。</summary>
    Listed = 9
}

/// <summary>行動の対象集合と選択方法。候補の制約はTargetFiltersが保持する。</summary>
public enum EnemyActionTargetSelector
{
    /// <summary>本文の指定に従い、行動ルールで追加の対象を指定しない。</summary>
    None = 0,
    /// <summary>敵からランダムに選ぶ。</summary>
    RandomSelectableEnemy = 1,
    /// <summary>同じ接近グループにいる敵からランダムに選ぶ。</summary>
    RandomSelectableEnemyInMeleeGroup = 2,
    /// <summary>味方からランダムに選ぶ。</summary>
    RandomAlly = 3,
    /// <summary>陣営を限定せずランダムに選ぶ。</summary>
    RandomCharacter = 4,
    /// <summary>敵全員を対象とする。</summary>
    AllEnemies = 5,
    /// <summary>同じ接近グループの敵全員を対象とする。</summary>
    AllEnemiesInMeleeGroup = 6,
    /// <summary>味方全員を対象とする。</summary>
    AllAllies = 7,
    /// <summary>同じ接近グループの敵味方全員を対象とする。</summary>
    AllInMeleeGroup = 8,
    /// <summary>敵味方すべてを対象とする。</summary>
    AllCharacters = 9,
    /// <summary>自身を除くすべてのキャラクターを対象とする。</summary>
    AllOtherCharacters = 10,
    /// <summary>自身を除くすべての味方を対象とする。</summary>
    AllOtherAllies = 11,
    /// <summary>このターン最後に指定のスキルによって敵にダメージを与えた対象を選ぶ。</summary>
    LastDamageTarget = 12,
    /// <summary>エリアの指定スキルを宣言したキャラクターを選ぶ。</summary>
    AreaSkillDeclarer = 13,
    /// <summary>進行ロールを行った代表者を選ぶ。</summary>
    ProgressRepresentative = 14,
    /// <summary>条件を満たす敵を指定する。</summary>
    Enemy = 15
}

/// <summary>行動の実行条件と反応するイベント。</summary>
public enum EnemyActionConditionType
{
    /// <summary>自身が指定状態であるか判定する。Negatedは状態を持たない場合。</summary>
    OwnState = 0,
    /// <summary>同じ接近グループに選択可能な敵がいない場合。</summary>
    NoSelectableEnemyInMeleeGroup = 1,
    /// <summary>自身への指定カテゴリーの行為を受けた場合。</summary>
    IncomingAction = 2,
    /// <summary>指定状態の敵がいない場合。</summary>
    NoEnemyWithState = 3,
    /// <summary>指定状態を持たない敵がいる場合。</summary>
    EnemyWithoutState = 4,
    /// <summary>指定状態を持たない味方がいる場合。</summary>
    AllyWithoutState = 5,
    /// <summary>重症由来の状態を持つ敵がいる場合。</summary>
    WoundedEnemy = 6,
    /// <summary>指定名の味方がいる場合。</summary>
    AllyPresent = 7,
    /// <summary>指定名の味方が指定数以上いる場合。</summary>
    AllyCount = 8,
    /// <summary>指定特性が有効な場合。</summary>
    TraitEnabled = 9,
    /// <summary>自身のリソースが上限値以下の場合。</summary>
    OwnResourceAtMost = 10,
    /// <summary>自身の二つのリソースのいずれかが上限値以下の場合。</summary>
    EitherResourceAtMost = 11,
    /// <summary>自身のリソースが指定量以上減少している場合。</summary>
    OwnResourceLost = 12,
    /// <summary>自身のリソースが指定値になった時。</summary>
    OwnResourceReached = 13,
    /// <summary>自身に指定カテゴリーの状態が付与されている場合。</summary>
    OwnStateCategory = 14,
    /// <summary>自身が先行の場合。</summary>
    ActsFirst = 15,
    /// <summary>指定スキルを宣言しない場合。</summary>
    SkillNotDeclared = 16,
    /// <summary>対象を限定せず指定カテゴリーの行為に反応する。</summary>
    AnyAction = 17,
    /// <summary>味方への指定カテゴリーの行為に反応する。</summary>
    AllyAction = 18,
    /// <summary>指定名の対象への行為カテゴリーに反応する。</summary>
    NamedActorAction = 19,
    /// <summary>自身へのアクティブ効果に反応する。</summary>
    IncomingActive = 20,
    /// <summary>指定名の対象へのアクティブ効果に反応する。</summary>
    NamedActorActive = 21,
    /// <summary>自身への指定名のスキルに反応する。</summary>
    IncomingSkill = 22,
    /// <summary>自身への任意の状態付与に反応する。</summary>
    IncomingState = 23,
    /// <summary>自身への指定カテゴリーの状態付与に反応する。</summary>
    IncomingStateCategory = 24,
    /// <summary>自身への指定カテゴリーの行為または状態付与に反応する。</summary>
    IncomingActionOrState = 25,
    /// <summary>自身への行為の達成値が指定値以上の場合に反応する。</summary>
    IncomingResultMinimum = 26,
    /// <summary>自身への行為の達成値が指定値以下の場合に反応する。</summary>
    IncomingResultMaximum = 27,
    /// <summary>自身への行為の達成値が指定値未満の場合に反応する。</summary>
    IncomingResultBelow = 28,
    /// <summary>自身の指定リソースのいずれかにダメージを与える効果に反応する。</summary>
    IncomingResourceDamage = 29,
    /// <summary>同じ接近グループの敵が行う指定カテゴリーの行為に反応する。</summary>
    MeleeEnemyAction = 30,
    /// <summary>同じ接近グループから自身への指定カテゴリーの行為に反応する。</summary>
    IncomingMeleeAction = 31,
    /// <summary>敵が指定カテゴリーの行為を発動した時に反応する。</summary>
    EnemyAction = 32,
    /// <summary>自身に指定状態のいずれかを付与した者に反応する。</summary>
    StateApplier = 33,
    /// <summary>対象となる味方がいない場合に次の対象選択を使う。</summary>
    NoAllyTarget = 34,
    /// <summary>直前の規則で対象を選べなかった場合。</summary>
    NoPriorTarget = 35,
    /// <summary>自身への一つの行為が、指定した二つのカテゴリーを両方持つ時に反応する。</summary>
    IncomingBothCategories = 36
}

/// <summary>行動対象を選ぶときに候補を絞り込む条件。</summary>
public enum EnemyTargetFilterType
{
    /// <summary>指定状態を持つ対象に限定する。</summary>
    State = 0,
    /// <summary>指定状態を持たない対象に限定する。</summary>
    WithoutState = 1,
    /// <summary>指定カテゴリーの対象に限定する。</summary>
    Category = 2,
    /// <summary>指定種族のいずれかに限定する。</summary>
    Race = 3,
    /// <summary>HPが指定値以下の対象に限定する。</summary>
    HpAtMost = 4,
    /// <summary>HPが減少している対象に限定する。</summary>
    Injured = 5,
    /// <summary>対象候補のうちHPが最も低い者に限定する。</summary>
    LowestHp = 6,
    /// <summary>指定名の状態のスタック数が指定数以上の対象に限定する。</summary>
    StateStacksAtLeast = 7
}

[Serializable]
public sealed class EnemyActionCondition
{
    public EnemyActionConditionType Type;
    public string Value = "";
    public bool Negated;
    public List<string> AdditionalValues = new List<string>();
}

[Serializable]
public sealed class EnemyTargetFilter
{
    public EnemyTargetFilterType Type;
    public List<string> Values = new List<string>();
}

[Serializable]
public class EnemyActionStep
{
    // 全AllConditionsと、AnyConditionsの少なくとも一つを満たす。
    public List<EnemyActionCondition> AllConditions = new List<EnemyActionCondition>();
    public List<EnemyActionCondition> AnyConditions = new List<EnemyActionCondition>();
    public EnemyActionTargetSelector TargetSelector;
    public string TargetReference = "";
    public List<EnemyTargetFilter> TargetFilters = new List<EnemyTargetFilter>();
    public bool SelectableOnly;
    // 全対象を選択するルールでも「ランダム」が明示されている場合。
    public bool RandomizeAllTargets;
    public bool DistinctTargets;
    public int TargetCount;
    public bool UpToTargetCount;
    public string SkillName = "";
    public bool UseItem;
    public int RepeatCount = 1;
    public bool Consecutive;
    public bool DoNothing;
}

[Serializable]
public sealed class EnemyActionBranch : EnemyActionStep
{
    // 0は出目の指定なし。同じ出目範囲の続きは、直前の範囲を引き継ぐ。
    public int MinimumRoll;
    public int MaximumRoll;
}

[Serializable]
public sealed class EnemyActionRule : EnemyActionStep
{
    public EnemyActionTiming Timing;
    public int UsesPerTurn;
    public string UsesPerTurnFormula = "";
    public bool UpToUsesPerTurn;
    public bool TeamLimit;
    public int UsesPerBattle;
    public int BattleTurn;
    public string BranchRoll = "";
    public List<EnemyActionBranch> Branches = new List<EnemyActionBranch>();
}

[Serializable]
public sealed class EnemyStatDefinition
{
    public string Name = "";
    public string Formula = "";
}

/// <summary>戦利品が記載されている場面と、その情報の有無を区別する。</summary>
public enum EnemyLootKind
{
    /// <summary>原文に戦利品欄がない。</summary>
    Unspecified,
    /// <summary>撃破時の戦利品。通常の出目は1d100で判定する。</summary>
    Defeat,
    /// <summary>捕獲に成功した時の戦利品。捕獲条件による出目補正を使用できる。</summary>
    Capture,
    /// <summary>戦利品を獲得しないことが明記されている。</summary>
    None,
    /// <summary>戦利品が未公開である。</summary>
    Undisclosed,
    /// <summary>決闘の敗者からは戦利品を獲得できない。</summary>
    NoLootFromDuelLoser
}

[Serializable]
public sealed class EnemyDropDefinition
{
    // 上下限とも0なら確定入手。下限だけ0なら「～最大値」を表す。
    [Min(0)] public int Minimum;
    [Min(0)] public int Maximum;
    public string ResultName = "";
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
    public List<SkillTextData> Traits = new List<SkillTextData>();
    public List<SkillTextData> Skills = new List<SkillTextData>();
    public EnemyLootKind LootKind;
    // 戦利品を得るにはこのターンの終了までに勝利する。0は期限なし。
    [Min(0)] public int LootVictoryTurnLimit;
    // 通常の1d100から変化する捕獲時だけ、補正後の出目の式を指定する。
    public string CaptureRollFormula = "";
    public List<EnemyDropDefinition> Drops = new List<EnemyDropDefinition>();
    [TextArea(3, 10)] public string Flavor = "";
}

[Serializable]
public sealed class EnemyTextData
{
    public EnemyDefinition Enemy = new EnemyDefinition();
}
