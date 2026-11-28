using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>スキルのコストとして支払うリソース、アイテム、スタック、通貨の種類を表す。</summary>
public enum CostKind
{
    /// <summary>ACT、SP、MPなどの名前付きリソースを消費する。</summary>
    Resource = 0,
    /// <summary>指定カテゴリーに属するアイテムを消費する。</summary>
    ItemCategory = 1,
    /// <summary>名前で指定したアイテムを消費する。</summary>
    ItemName = 2,
    /// <summary>名前で指定した状態のスタックを消費する。</summary>
    NamedStack,
    /// <summary>指定カテゴリーに属する状態のスタックを消費する。</summary>
    CategoryStack,
    /// <summary>宣言時に選んだ所持品を消費する。</summary>
    SelectedItem,
    /// <summary>通貨で費用を支払う。</summary>
    Currency,
    /// <summary>指定カテゴリーから宣言時に選ぶアイテムを消費する。</summary>
    SelectedCategoryItem,
    /// <summary>列挙されたアイテムのいずれかを消費する。</summary>
    ChoiceOfNamedItems = 10,
    /// <summary>使用者本人を対象として採取された指定カテゴリーの素材を消費する。</summary>
    SelfHarvestedItemCategory = 11,
    /// <summary>指定フェーズの行動1回を消費する。</summary>
    PhaseAction = 12,
    /// <summary>指定個数の名前付きアイテムを奉納して消費する。</summary>
    OfferedItem = 13,

}
/// <summary>コストの消費量の決め方を表す。固定値、任意量、全消費、計算式などを区別する。</summary>
public enum CostQuantity
{
    /// <summary>指定した固定量を消費する。</summary>
    Fixed,
    /// <summary>該当するリソースやスタックの全量を消費する。</summary>
    All,
    /// <summary>使用者が消費量を任意に決める。</summary>
    Chosen,
    /// <summary>指定量だけ残して残りをすべて消費する。</summary>
    LeaveRemaining,
    /// <summary>計算式から消費量を求める。</summary>
    Formula,
    /// <summary>異なる名前のアイテムを、指定した最少数から最多数の範囲で消費する。</summary>
    DistinctNameRange
}
[Serializable]
public class ResourceCost
{
    public CostKind Kind;
    public string Resource = "";
    public CostQuantity Quantity;
    [Min(0)] public int Amount;
    [Min(0)] public int Maximum;
    public string Formula = "";
}
/// <summary>効果や補正を適用する時点を表す。誰の行為かなどの追加条件はトリガーの条件で指定する。</summary>
public enum TriggerTiming
{
    /// <summary>特定の出来事を待たず、追加条件があればその条件のもとで適用する。</summary>
    Always = 0,
    /// <summary>攻撃の威力を算出する時に適用する。</summary>
    AttackPower = 1,
    /// <summary>回避の達成値を算出する時に適用する。</summary>
    EvasionResult = 2,
    /// <summary>状態が解除された時に適用する。</summary>
    StateRemoved = 3,
    /// <summary>武器攻撃を行った後に適用する。</summary>
    AfterWeaponAttack = 4,
    /// <summary>スキルの処理が終了した後に適用する。</summary>
    AfterSkillResolution = 5,
    /// <summary>発動ロールの結果が出た後に適用する。</summary>
    AfterActivationRoll = 6,
    /// <summary>効果が発動する直前に適用する。</summary>
    BeforeEffectActivation = 7,
    /// <summary>すでに受けている状態が再び付与された時に適用する。</summary>
    StateReapplied = 8,
    /// <summary>判定の目標値を算出する時に適用する。</summary>
    TargetValue = 9,
    /// <summary>リソースの消費量を算出する時に適用する。</summary>
    ResourceCost = 10,

    /// <summary>受けるダメージを算出する時に適用する。</summary>
    IncomingDamage = 11,
    /// <summary>スキル値を算出する時に適用する。</summary>
    SkillValue = 12,
    /// <summary>行為判定の達成値を算出する時に適用する。</summary>
    ActionResult = 13,
    /// <summary>追加ロールの出目が確定した時に、該当する分岐の効果を適用する。</summary>
    RandomResult = 14,
    /// <summary>属性威力を算出する時に適用する。</summary>
    ElementalPower = 15,
    /// <summary>ターン開始時に適用する。</summary>
    TurnStart = 16,
    /// <summary>キャラクターが戦闘不能になった時に適用する。</summary>
    CharacterIncapacitated = 17,

    /// <summary>指定したリソース量が変化した時に適用する。</summary>
    ResourceChanged = 18,
    /// <summary>行為が発動した時に適用する。</summary>
    ActionActivated = 19,
    /// <summary>状態のスタック数が変化した時に適用する。</summary>
    StateStackChanged = 20,
    /// <summary>相手にダメージを与えた時に適用する。</summary>
    DamageDealt = 21,
    /// <summary>ダメージを受けた時に適用する。</summary>
    DamageReceived = 22,
    /// <summary>行為が宣言された時に適用する。</summary>
    ActionDeclared = 23,
    /// <summary>戦闘の終了時に適用する。</summary>
    BattleEnd = 24,
    /// <summary>リソースの回復量を算出する時に適用する。</summary>
    ResourceRecovery = 25, // when a resource restoration amount is determined
    /// <summary>このスキルによる攻撃が対象に命中した時に適用する。</summary>
    AttackHit = 26, // this skill's attack hit its target (not merely a successful roll)
    /// <summary>自身に対する行為の解決後に適用する。ダメージがゼロの場合も含む。</summary>
    ActionReceived = 27 // after an incoming action resolves on the holder, including zero damage
}
[Serializable]
public class TriggerDefinition
{
    public TriggerTiming Timing;
    public ConditionSet Conditions = new ConditionSet();
}
[Serializable]
public class DiceRollDefinition
{
    [Tooltip("式全体を評価する回数。0なら発動ロールなし。1d100の1とは別です。")]
    [Min(0)] public int Count;
    public string Formula = "";
    public string Target = "";
    // -1: no fixed result. Used only by an automatic-success roll.
    public int FixedResult = -1;
    public bool UseSummonerResult;
    public List<string> SourceItemCategories = new List<string>();
    public int RepresentativeCount;
}
/// <summary>スキル本文のどの効果欄に属するかを表す。発動方法、ロール結果、スパイク段階を区別する。</summary>
public enum EffectType
{
    /// <summary>効果の種類が未指定であることを表す。</summary>
    None = 0,
    /// <summary>スキルを能動的に発動した時の効果を表す。</summary>
    Active = 1,
    /// <summary>ほかの行為や出来事に反応して発動する効果を表す。</summary>
    Counter = 2,
    /// <summary>習得中や所定の条件下で適用されるパッシブ効果を表す。</summary>
    Passive = 3,
    /// <summary>ロールプレイでできる行動やその結果を表す。</summary>
    Roleplay = 4,
    /// <summary>クリティカル時に適用する効果を表す。</summary>
    Critical = 5,
    /// <summary>ファンブル時に適用する効果を表す。</summary>
    Fumble = 6,
    /// <summary>スキルを宣言した時の効果を表す。</summary>
    Declaration = 7,
    /// <summary>セカンドスパイクで適用する変更や追加効果を表す。</summary>
    SecondSpike = 8,
    /// <summary>サードスパイクで適用する変更や追加効果を表す。</summary>
    ThirdSpike = 9,
    /// <summary>成功時に適用する効果を表す。</summary>
    Success = 10,
    /// <summary>失敗時に適用する効果を表す。</summary>
    Failure = 11
}
/// <summary>発動タイミングと追加条件を、そのとき適用する効果の列に関連付ける。</summary>
[Serializable]
public class EffectDefinition
{
    public EffectType Type;
    // 空なら効果種別に従う。複数指定時はいずれかが成立した時、同じイベントに1回適用する。
    public List<TriggerDefinition> Triggers = new List<TriggerDefinition>();
    public List<EffectContent> Contents = new List<EffectContent>();
}
/// <summary>スキル・装備・アイテム・状態が適用する効果や補正の種類。対象、量、計算式などを共通の引数で指定する。</summary>
public enum EffectContentType
{
    /// <summary>効果の内容が未指定であることを表す。</summary>
    None = 0,
    /// <summary>指定した人物のリソースを回復する。</summary>
    RestoreResource = 1,
    /// <summary>対象に状態を付与する。持続ターンがあればその間だけ適用する。</summary>
    ApplyState = 2,
    /// <summary>指定した人物のリソースを消費する。</summary>
    ConsumeResource = 3,
    /// <summary>自身と対象を同じ接近グループの《接近》状態にする。</summary>
    JoinMeleeGroup = 4,
    /// <summary>誰から見たときに別の状態として扱えるかを指定する。</summary>
    StateAlias = 5,
    /// <summary>指定した人物の状態を解除する。</summary>
    RemoveState = 6,
    /// <summary>対象に指定のスキル値で武器攻撃を行う。</summary>
    WeaponAttack = 7,
    /// <summary>消費したアイテムのアクティブ効果を適用する。</summary>
    ApplyConsumedItemActive = 8,
    /// <summary>指定した状態のスタックを付与する。上限があれば適用する。</summary>
    GainStack = 9, // actor, state, amount/formula, optional maximum stacks
    /// <summary>対象に指定値または式によるダメージを与える。</summary>
    Damage = 10,
    /// <summary>同じ接近グループにいる対象に状態を付与する。</summary>
    ApplyStateInMeleeGroup = 11,
    /// <summary>自身に向けられた指定カテゴリーの行動を無効にする。</summary>
    InvalidateIncomingAction = 12,
    /// <summary>発動しようとした効果を無効にする。</summary>
    InvalidateTriggeredEffect = 13,
    /// <summary>対象と威力式を指定して攻撃する。属性威力や状態の無視も表せる。</summary>
    SkillAttack = 14, // actor, power | actor, PowerAndAttribute, power, attribute power | actor, IgnoreOwnStateCategory, category, power | ElementalWeapon form
    /// <summary>対象のリソースを符号付きの値だけ変化させる。</summary>
    ModifyResource = 15,
    /// <summary>対象の指定カテゴリーの行動を無効にする。</summary>
    InvalidateAction = 16,
    /// <summary>受けるダメージを指定値または式の結果だけ軽減する。</summary>
    ReduceDamage = 17,
    /// <summary>指定した期間に発動した効果を、指定回数まで再適用する。</summary>
    ReapplyEffects = 18,
    /// <summary>追加のダイスを振り、その出目を後続の条件から参照できるようにする。</summary>
    RollDice = 19,
    /// <summary>名前で指定した召喚体を召喚する。</summary>
    Summon = 20,
    // actor, amount, multi-attribute policy, rule key, then attribute/state pairs.
    /// <summary>属性と状態名の対応表に従ってスタックを付与する。</summary>
    GainMappedStateStacks = 21,
    // amount
    /// <summary>そのエリアで行える採取の回数を増やす。</summary>
    AddAreaGathering = 22,
    /// <summary>ロールプレイで可能になる行動を文章で表す。</summary>
    RoleplayDescription = 23, // Narrative permission, not executable combat text.
    /// <summary>指定したタイミングで対象スキルを習得することを禁止する。</summary>
    ProhibitAcquisition = 24, // actor, timing, skill names...
    /// <summary>自身を指定カテゴリーに含めるかどうかを、その都度選べる。</summary>
    OptionalCategoryExclusion = 25, // actor, category, PerOccurrence
    /// <summary>与えたダメージに応じてリソースを回復する。</summary>
    RestoreFromDamage = 26, // actor, resource, dealt-damage multiplier
    /// <summary>対象の指定状態のスタックをすべて消費し、この攻撃で対象が受けるダメージに指定倍率を掛ける。</summary>
    ConsumeStacksForDamage = 27, // actor, state, All, multiplier, ThisAttack
    /// <summary>名前で指定した罠を設置する。</summary>
    PlaceTrap = 28, // local Summons definition name
    /// <summary>スキルの処理後に発動元の罠を取り除く。</summary>
    RemoveSummon = 29, // Self
    /// <summary>指定した人物を効果の選択対象にできなくする。</summary>
    PreventSelection = 30, // actor, All
    /// <summary>指定した種類の効果の発動を禁止する。</summary>
    ProhibitEffect = 31, // actor, effect kind
    /// <summary>例外として指定した状態以外の付与を防ぐ。</summary>
    PreventStateApplication = 32, // actor, Except, state names...
    /// <summary>参照する能力値を変更して発動ロールを振り直す。</summary>
    RerollActivation = 33, // Self, replacement ability bonus, TriggeringRoll
    /// <summary>指定した人物のリソースを指定値にする。</summary>
    SetResourceValue = 34, // actor, resource, value
    /// <summary>今回の処理で指定したロールを省略できるようにする。</summary>
    SkipRoll = 35, // actor, roll kind, Optional, ThisResolution
    /// <summary>種族選択時に習得できる指定カテゴリーのスキル数に上限を設ける。</summary>
    LimitAcquisition = 36, // Self, RaceSelection, skill category, maximum (0 prohibits)
    /// <summary>自身を追加の種族カテゴリーとしても扱う。</summary>
    RaceAlias = 37, // Self, additional race category
    /// <summary>指定スキルによる採取を、1日の上限内で振り直せるようにする。</summary>
    RerollGathering = 38, // skill name, Day, maximum uses
    /// <summary>武器を通常と異なる手数で装備できるようにする。</summary>
    EquipSlotSubstitution = 39, // Weapon, BothHands, OneHand
    /// <summary>自身のスタックが指定数に達した時、HPとMPを全回復し、指定状態になる。</summary>
    TransformAtStacks = 40, // Self, stack state, Equals, threshold, HP, MP, resulting state (after this gain)
    /// <summary>指定カテゴリーの行動でスタックを得て、規定数に達するとリソースを回復してスタックをリセットする。</summary>
    CyclingStateStacks = 41, // Self, action category, state, state category, gain, threshold, resource, restore, reset
    /// <summary>敵全員のリソースを減らし、実際に減った合計を自身が得る。</summary>
    DrainResource = 42, // AllEnemies, resource, positive reduction formula, Self, ActualTotal, AllowOverflow
    /// <summary>自身と対象のために新しい接近グループを作る。</summary>
    CreateMeleeGroup = 43, // Self, Target, state name; create a new group
    /// <summary>食事や食事セットによるリソース・能力値の減少を防ぐ。</summary>
    PreventMealPenalties = 44, // Self, MealAndMealSet, ResourceAndStatDecrease
    /// <summary>キャラクター作成時に候補から指定数のライフパスを選べるようにする。</summary>
    GrantCreationChoice = 45, // LifePath, count, candidate names...
    /// <summary>指定した種族カテゴリーの特性を習得する。</summary>
    GrantRaceTrait = 46, // race category, count
    /// <summary>種別とパラメーターの組でキャラクター固有のルールを表す。</summary>
    CharacterRule = 47, // character-rule kind followed by semantic parameters; see CharacterRuleTextCodec
    /// <summary>達成値が上限以下の指定カテゴリーの行動を任意で無効にする。</summary>
    OptionalInvalidateAction = 48, // Self, action category, AtMost, achievement value
    /// <summary>規定のスタック数を超えたら、状態名とスタックを保ったまま別の状態定義の効果を使う。</summary>
    UseStateDefinitionAtStacks = 49, // GreaterThan, threshold, definition name, ReplaceEffects, KeepIdentityAndStacks
    /// <summary>自身が戦闘から離脱する。</summary>
    LeaveBattle = 50, // Self
    /// <summary>リソースが指定値以下のとき、指定のロールを行えなくする。</summary>
    PreventRollAtResource = 51, // Self, resource, AtMost, threshold, roll name
    /// <summary>指定カテゴリーの効果を受けなくする。</summary>
    CategoryImmunity = 52, // Self, categories... (any)
    /// <summary>指定カテゴリーの選択対象にならなくする。</summary>
    ProhibitCategoryTarget = 53, // Self, category
    /// <summary>あるリソースの消費をすべて別のリソースの消費に置き換える。</summary>
    ConvertResourceCost = 54, // Self, from resource, to resource, All
    /// <summary>クエストで受け取る報酬に指定量を加える。</summary>
    AddQuestReward = 55, // Self, resource, amount
    /// <summary>選択したスキルに指定の状態を指定ターン付与する。</summary>
    ApplySelectedSkillState = 56, // SelectedSkill, state, turns
    /// <summary>選択したスキルの使用を禁止する。</summary>
    ProhibitSelectedSkill = 57, // Self, SelectedSkill
    /// <summary>指定されたキャラクターのリソースにダメージを与える。</summary>
    ResourceDamage = 58, // Self, Target or AllEnemies, resource, formula
    /// <summary>与えたリソースダメージに応じて、自身の指定リソースを回復する。</summary>
    RestoreFromResourceDamage = 59, // Self, source resource, restored resource, multiplier, ThisResolution
    /// <summary>付与者が対象を味方として扱い、ターンごとの上限内でその行動を宣言する。</summary>
    GrantActionControl = 60, // Applier, Self, Turn, limit, AllyDuringAction
    /// <summary>状態付与時に対象と威力を決め、指定ターン後に攻撃する。</summary>
    DelayedStateAttack = 61, // Target, delay turns, power, attribute power, SnapshotOnApply, RemoveAfterAttack
    /// <summary>自身のターン開始時、指定状態のキャラクター全員に別の指定状態のスタックを付与する。</summary>
    TurnStartStateStacks = 62, // Self, required state, gained state, amount, AllCharacters
    /// <summary>指定した環境の効果を受けなくする。</summary>
    EnvironmentImmunity = 63, // Self, environment names...
    /// <summary>指定カテゴリーの行動で相手に状態を付与すると、自身のリソースを最大値の一定割合だけ回復する。</summary>
    RestoreOnAppliedState = 64, // Self, action category, Target, applied state, resource, MaximumPercent, percent
    /// <summary>対象の行為判定をクリティカルまたはファンブルに変更できるようにする。</summary>
    ChooseCheckOutcome = 65, // Target, Critical, Fumble, Optional
    /// <summary>指定した名前・個数・ランクのアイテムを獲得する。</summary>
    GrantItem = 66, // Self, item name, count, rank formula
    /// <summary>指定したロールなどを原因とする状態を解除する。</summary>
    RemoveStatesByOrigin = 67, // Self, Roll, roll name
    /// <summary>選択したスキルを対象へ宣言できるようにし、その消費と威力の扱いを指定する。</summary>
    DeclareSelectedSkill = 68, // Self, Target, ACT, 0, Optional, power factor (other costs/conditions retained)
    /// <summary>装備中の武器と選択した武器を戦闘中に入れ替える。</summary>
    SwapWeapon = 69, // Self, SelectedWeapon, Battle, AllowUnarmed
    /// <summary>指定カテゴリーを含む戦闘エリアに移動先を限定する。</summary>
    RequireBattleAreaCategory = 70, // required destination area category
    /// <summary>戦闘開始から指定ターン目に宣言できるスキルを、指定カテゴリーに限定する。</summary>
    RestrictBattleTurnDeclarations = 71, // turn, allowed category A, allowed category B (OR)
    /// <summary>指定した特性による効果をすべて無効にする。</summary>
    SuppressTraitEffects = 72, // trait name
    /// <summary>最も高い基礎属性値の属性を自身の「自属性」として扱う。</summary>
    DefineOwnAttribute = 73, // no parameters: highest base attribute; ties remain unresolved
    /// <summary>指定カテゴリーへのカウンターでファンブルしたとき、自身に状態を付与する。</summary>
    CounterFumbleState = 74, // incoming category, state name, turns
    /// <summary>自身への指定カテゴリーの効果を任意で無効にする。</summary>
    OptionalCategoryImmunity = 75, // incoming category (Self, optional)
    /// <summary>攻撃命中時、自属性が条件を満たせば対象に指定ターンの状態を付与する。</summary>
    OwnAttributeHitState = 76, // own attribute, state, turns (Target, on this attack hit)
    /// <summary>攻撃命中時、自属性が条件を満たせば対象の指定状態を解除する。</summary>
    OwnAttributeHitRemoval = 77, // own attribute, removed state (Target, on this attack hit)
    /// <summary>自属性がないときは属性威力と追加効果を適用せず、指定した威力だけを使う。</summary>
    MissingOwnAttributePower = 78, // base power; omit attribute power and attribute branches if absent
    /// <summary>自身の攻撃スキルと、自身を含む選択可能な対象をランダムに選んで発動する。</summary>
    RandomAttackDeclaration = 79, // no parameters: random own attack, random legal targets including Self
    /// <summary>自身を除く選択可能なキャラクターから、指定数の対象をランダムに決める。</summary>
    RandomTargetSelection = 80, // count; random legal targets excluding Self
    /// <summary>指定したリソースの回復を禁止する。</summary>
    PreventResourceRecovery = 81, // resource (Self)
    /// <summary>指定カテゴリーの効果を発動できなくする。</summary>
    ProhibitActionCategory = 82, // category (Self, prevent declaration/activation)
    /// <summary>戦闘中の指定回数だけ、1スキルへのカウンター宣言回数と同一対象への発動回数を拡張する。</summary>
    ExtraCountersPerSkill = 83, // battle uses, declarations per triggering skill, same-target declarations
    /// <summary>次の指定回数の宣言で、特定状態を要求せず規定数以上を選ぶスキルに対象を追加できるようにする。</summary>
    NextDeclarationExtraTargets = 84, // uses, excluded required state, minimum targets, optional extra targets
    /// <summary>自身が宣言するスキルに、現在より高い段階のディスティニースパイクを適用できるようにする。</summary>
    AdvanceDestinySpike = 85, // stages; optional use of an existing higher stage
    /// <summary>異なる接近グループからの指定カテゴリーの効果の選択対象にならなくする。</summary>
    PreventSelectionOutsideMelee = 86, // incoming category
    /// <summary>付与者のターン開始時、付与者の現在の能力値を参照して状態の対象を攻撃する。</summary>
    ApplierTurnAttack = 87, // power; Applier attacks state holder on Applier's turn start, current Applier stats
    /// <summary>攻撃のダメージ処理後、対象のHP割合が閾値以下ならHPを指定値にし、状態を付与する。</summary>
    ExecuteAfterAttackDamage = 88, // HP maximum percent threshold, resulting HP, applied state (Target)
    /// <summary>指定カテゴリーの宣言を禁止する。</summary>
    ProhibitDeclarationCategory = 89, // category (Self, declaration only)
    /// <summary>戦闘終了時に味方が全滅していなければ、指定状態を解除し、指定HPで復活する。</summary>
    ReviveAfterBattle = 90, // required/removed own state, HP; only if allies were not wiped out
    /// <summary>自身に指定のリソースが存在しないことを表す。</summary>
    AbsentResource = 91, // resource does not exist (not zero)
    /// <summary>指定した状態の付与を防ぐ。</summary>
    StateImmunity = 92, // state name (Self)
    /// <summary>クリティカルした指定カテゴリーの行動を受けたとき、自身に状態を付与する。</summary>
    StateOnIncomingCritical = 93, // incoming action category, applied state; activation-roll critical
    /// <summary>指定した環境を含む場合に自身に永続状態を付与する。</summary>
    EnvironmentPermanentState = 94, // environment name, maintained state without a turn limit
    /// <summary>指定カテゴリーによるダメージを受けたとき、指定の被ダメージ増加と状態付与を防ぐ。</summary>
    SuppressSourceDamageAndState = 95, // incoming category, source trait, state (only that trait's penalties)
    /// <summary>指定環境にいる間、特定の発生元からの状態付与を防ぐ。</summary>
    SuppressSourceEnvironmentState = 96, // environment, source trait, state
    /// <summary>命中した対象の種族が条件に合うとき、指定ターンの状態を付与する。</summary>
    HitRaceState = 97, // allowed target races (comma separated, OR), state, turns; this attack hit
    /// <summary>指定カテゴリーの武器攻撃が該当種族に命中したとき、指定ターンの状態を付与する。</summary>
    WeaponHitRaceState = 98, // weapon category, allowed target races (OR), state, turns
    /// <summary>自身が選択対象になったとき、選択した相手にスタックを任意で付与する。</summary>
    OptionalStackOnSelected = 99, // state, amount; give to the selecting actor, optional each selection
    /// <summary>自身のターン開始時、指定スタックを持つ人物がいればリソースを任意で回復する。</summary>
    OptionalRecoveryIfStackExists = 100, // state, resource, amount; own turn start, any character with >=1 stack
    /// <summary>選択可能な種族スキルを追加で習得できるようにする。</summary>
    AdditionalRaceSkills = 101, // additional count, from currently selectable race skills
    /// <summary>指定カテゴリーの行動を受けたとき、自身に指定ターンの状態を付与する。</summary>
    IncomingCategoriesState = 102, // incoming categories (comma separated, OR), state, turns (Self)
    /// <summary>今回の消費を可能な限り支払い、不足分を自身の次のターン開始時に繰り延べる。</summary>
    DeferMissingResourceCost = 103, // resource; this skill's Active only, pay available now, remainder next own turn start
    /// <summary>このスキルの攻撃後、自身の指定スタックをすべて取り除く。</summary>
    RemoveAllStacks = 104, // own stack state, after this skill's attack (not its power/cost calculation)
    /// <summary>対象に指定ターンの間、指定数のスタックを付与する。</summary>
    TimedTargetStacks = 105, // state, count, duration turns; only these newly granted stacks expire
    /// <summary>プレイヤーがその場にいれば、状態を維持したままその効果を無視してキャラクターを使える。</summary>
    UseStateBoundCharacter = 106, // state; player must be present, ignore state effects for use, state cannot be removed
    /// <summary>対象のリソースを指定値にし、2種類の状態を付与する。</summary>
    SetTargetResourceAndStates = 107, // resource, value, state A, state B (both)
    /// <summary>発動ロールを自動成功にする。</summary>
    SucceedFailedActivationRoll = 108, // no parameters; triggering normally failed roll becomes success, not Critical
    /// <summary>対象のリソース最大値に対する割合で、防御点・効果・状態による増減を受けないダメージを与える。</summary>
    UnmodifiableMaximumResourceDamage = 109, // target resource, maximum percent; no power/damage increases or reductions from defense/effects/states
    /// <summary>指定した状態のスタックを消費する。</summary>
    ConsumeStacks = 110, // Self, local state, ReceivedActionPower (not post-mitigation damage)
    /// <summary>自身と選択対象に、対になる永続状態をそれぞれ付与する。</summary>
    ApplyPermanentLinkedStates = 111, // Self state, selected character's state; retain this pair's partner
    /// <summary>自身の状態を解除し、その状態で結び付いていた相手の対応状態も解除する。</summary>
    RemoveLinkedStates = 112, // Self state, partner state; resolve partner before clearing either instance
    /// <summary>自身の指定状態と接近グループの判定を、選択した相手に委ねる。</summary>
    UsePartnerMeleeGroup = 113, // engagement state name; inherit its check and the partner's group
    /// <summary>判定で参照する能力値を、被騎乗キャラクターの能力値に置き換える。</summary>
    UsePartnerStat = 114, // stat name; read the linked partner's current value
    /// <summary>被騎乗キャラクターが満たしたエリア移動条件を騎乗者も満たしたものとする。</summary>
    SharePartnerAreaEligibility = 115, // holder qualifies when partner meets the area's movement conditions
    /// <summary>被騎乗キャラクターが戦闘を離脱したら騎乗者も離脱する。</summary>
    FollowPartnerBattleExit = 116, // holder also leaves when partner leaves the battle
    /// <summary>レシピに記載されたアイテムを入手できるようにする。</summary>
    GrantSelectedRecipeProduct = 117,
    /// <summary>レシピに記載された効果を発動する。</summary>
    ActivateSelectedRecipe = 118,
    /// <summary>レシピに記載された効果を対象に発動できるようにする。</summary>
    OfferSelectedRecipeEffect = 119,
    /// <summary>レシピで得たアイテムを指定数消費して、対象にその効果を適用する。</summary>
    UseRecipeProductOnTarget = 120,
    /// <summary>発動ロールの結果に応じて指定カテゴリーの耐久を増減する。</summary>
    ResolveDurabilityByRoll = 121,
    /// <summary>このロール結果による追加の効果がないことを表す。</summary>
    NoRollEffect = 122,
    /// <summary>指定カテゴリーからスキルを1つ選んで習得する。</summary>
    AcquireSkillInCategory = 123,
    /// <summary>食事や食事セットによるステータス減少を防ぐ。</summary>
    PreventMealStatPenalties = 124,
    /// <summary>自身が指定状態の間、環境効果を受けなくする。</summary>
    IgnoreEnvironmentInOwnState = 125,
    /// <summary>自身とは異なる種族の特性を選んで習得する。</summary>
    AcquireOtherRaceTraits = 126,
    /// <summary>指定した2カテゴリーのアイテムの使用を禁止する。</summary>
    CannotUseItemCategories = 127,
    /// <summary>指定特性によるアイテムカテゴリーの使用制限を免除する。</summary>
    IgnoreTraitItemRestriction = 128,
    /// <summary>指定カテゴリーの武器を常に装備している扱いとし、指定式の武器威力を使えるようにする。</summary>
    EquippedWeaponEquivalent = 129,
    /// <summary>指定値または式で求めた防御点を得る。</summary>
    GrantCalculatedDefense = 130,
    /// <summary>エンカウントフェーズの後に自身が戦闘を離脱できるようにする。</summary>
    OptionalEncounterExit = 131,
    /// <summary>対象の指定状態のスタックをすべて解除する。</summary>
    ClearTargetStacks = 132,
    /// <summary>指定カテゴリーの行動を対象に代わって受ける。</summary>
    ReceiveActionForTarget = 133,
    /// <summary>選択したカテゴリーの装備を外す。</summary>
    UnequipSelectedCategory = 134,
    /// <summary>対象の所持品から指定数のアイテムを選んで奪う。</summary>
    StealSelectedPossessions = 135,
    /// <summary>対象に指定した状態を永続的に付与する。</summary>
    ApplyPermanentTargetState = 136,
    /// <summary>消費した指定カテゴリーのアイテムのアクティブ効果を適用する。</summary>
    SelectedConsumableActive = 137,
    /// <summary>自身の指定状態と、指定カテゴリーに属する状態をすべて解除する。</summary>
    ClearOwnStateAndCategory = 138,
    /// <summary>自身へのアクティブ効果を無効にする。</summary>
    CancelIncomingActive = 139,
    /// <summary>対象と同じ接近グループのキャラクターに状態を付与する。</summary>
    ApplyStateToTargetGroup = 140,
    /// <summary>対象と同じ接近グループの全員に指定ターンの状態を付与する。</summary>
    ApplyTimedStateToTargetGroup = 141,
    /// <summary>パーティー全体が指定した2種類の環境効果を受けなくなる。</summary>
    PartyEnvironmentImmunities = 142,
    /// <summary>指定したスキル値で対象を回復する。</summary>
    HPHealBySkillValue = 143,
    /// <summary>任意で指定数のスタックを消費し、別の状態のスタックを得る。</summary>
    OptionalStackExchange = 144,
    /// <summary>味方全員から指定カテゴリーの状態を解除する。</summary>
    RemovePartyStateCategory = 145,
    /// <summary>対象を同じ新規接近グループにまとめ、指定状態にする。</summary>
    GroupInNewMelee = 146,
    /// <summary>対象のリソースを最大値の指定割合だけ回復して蘇生する。</summary>
    ReviveWithMaximumRatio = 147,
    /// <summary>このキャラクターは自身の行動終了後に消滅する。</summary>
    DespawnAfterOwnAction = 148,
    /// <summary>対象に付与された指定カテゴリーの状態を解除する。</summary>
    CleanseTargetStateCategories = 149,
    /// <summary>対象が次に振るダイス1回の出目に倍率を掛ける。</summary>
    NextTargetDieMultiplier = 150,
    /// <summary>対象が指定状態ならリソースを回復する。</summary>
    ConditionalSelfRestore = 151,
    /// <summary>次の指定回数だけ、該当カテゴリーのリソース消費と達成値にそれぞれ倍率を掛ける。</summary>
    NextCategoryCostAndResult = 152,
    /// <summary>次の指定回数だけ、該当カテゴリーの達成値と威力にそれぞれ倍率を掛ける。</summary>
    NextCategoryResultAndPower = 153,
    /// <summary>次の指定回数だけ、該当カテゴリーの発動ロールの目標値を増減する。</summary>
    NextCategoryRollTargetDelta = 154,
    /// <summary>次の指定回数だけ、該当カテゴリーの発動ロールの達成値を増減する。</summary>
    NextCategoryActivationDelta = 155,
    /// <summary>ロール結果に従って対象アイテムの耐久を増減する。</summary>
    ChangeSelectedItemDurability = 156,
    /// <summary>指定カテゴリーのエリアが移動先に含まれる戦闘エリアへ移動を限定する。</summary>
    RequireDestinationArea = 157,
    /// <summary>任意で自身を指定した種族カテゴリーとして扱う。</summary>
    OptionalRaceCategory = 158,
    /// <summary>クリティカルした指定カテゴリーの行動を受けると、自身に指定ターンの状態を付与する。</summary>
    CriticalHitGrantsState = 159,
    /// <summary>通常選択できる種族スキルに選択肢を限定する。</summary>
    RequireEligibleRaceSkill = 160,
    /// <summary>指定した装備部位以外からの装備効果を受けなくする。</summary>
    IgnoreGearOutsideSlots = 161,
    /// <summary>指定の能力値を別の能力値に置き換えてダイスを振れるようにする。</summary>
    OptionalSubstituteRollStat = 162,
    /// <summary>指定した2カテゴリーの効果を無効にする。</summary>
    ImmuneToTwoCategories = 163,
    /// <summary>エンカウントフェーズにアイテムを任意で消費して指定状態になる。</summary>
    OptionalEncounterSpendItemForState = 164,
    /// <summary>指定カテゴリーを宣言するたびに自身のリソースを消費する。</summary>
    SpendResourcePerDeclaration = 165,
    /// <summary>戦闘開始時に自身が指定状態のスタックを得る。</summary>
    BattleStartStacks = 166,
    /// <summary>対応するスキルの指定リソース消費を免除できるようにする。</summary>
    WaiveTriggerSkillCost = 167,
    /// <summary>自身への状態付与を無効にする。</summary>
    InvalidateStateApplication = 168,
    /// <summary>選択対象を自身に変更する。</summary>
    RedirectTargetToSelf = 169,
    /// <summary>対象のアイテムのアクティブ効果を無効にする。</summary>
    CancelItemActive = 170,
    /// <summary>耐久ロールの結果を任意で無効にする。</summary>
    CancelDurabilityRoll = 171,
    /// <summary>自身に付与されている指定カテゴリーの状態をすべて解除する。</summary>
    RemoveOwnStateCategories = 172,
    /// <summary>対象に発動ロールを振り直させる。</summary>
    RerollTargetActivation = 173,
    /// <summary>指定カテゴリーの行動の選択対象を任意で変更する。</summary>
    RedirectCategoryTarget = 174,
    /// <summary>対象に及ぶ効果を無効にする。</summary>
    InvalidateTargetEffect = 175,
    /// <summary>対象の行動を無効にする。</summary>
    InvalidateTargetAction = 176,
    /// <summary>設置者が対象と同じ接近グループの全員を指定スキル値で武器攻撃する。</summary>
    InstallerGroupWeaponAttack = 177,
    /// <summary>対象と同じ接近グループの全員を指定スキル値で武器攻撃する。</summary>
    GroupSkillValueAttack = 178,
    /// <summary>対象と同じ接近グループの全員を指定威力で攻撃する。</summary>
    GroupPowerAttack = 179,
    /// <summary>自身の重傷ロールによって付与された状態をすべて解除する。</summary>
    PurgeInjuryRollStates = 180,
    /// <summary>追加で指定数の所持品を選んで奪う。</summary>
    StealAdditionalItem = 181,
    /// <summary>指定した召喚体のHPと威力に倍率を掛ける。</summary>
    MultiplySummonHPAndPower = 182,
    /// <summary>戦闘中に自身が消費した数だけ、指定状態のスタックを得る。</summary>
    ReturnStacksSpentInBattle = 183,
    /// <summary>指定式で求めた量だけ対象のリソースを減らす。</summary>
    ResetTargetResourceByFormula = 184,
    /// <summary>対象を自身の接近グループの《接近》状態にする。</summary>
    SetTargetMeleeGroup = 185,
    /// <summary>スキル値に係数を掛けて対象のSANを回復する。</summary>
    HealSanityWithSkillValue = 186,
    /// <summary>スキル値に係数を掛けた威力で対象を回復する。</summary>
    HealPowerWithSkillValue = 187,
    /// <summary>対象の2種類のリソースに同じ威力のダメージを与える。</summary>
    DealTwoResourceDamage = 188,
    /// <summary>指定カテゴリーのアイテムを指定数得る。</summary>
    GrantCategoryItems = 189,
    /// <summary>このエリアの進行ロールの出目を指定回数分確認する。</summary>
    InspectAreaProgressRolls = 190,
    /// <summary>このエリアの進行ロールを追加で指定回数行う。</summary>
    AdditionalAreaProgressRolls = 191,
    /// <summary>このエリアを移動するまで自身に状態を付与する。</summary>
    ApplyStateUntilAreaMove = 192,
    /// <summary>対象に指定状態のスタックを永続的に付与する。</summary>
    GainPermanentTargetStacks = 193,
    /// <summary>選択した対象の数だけ自身が永続スタックを得る。</summary>
    GrantSelfStacksPerTarget = 194,
    /// <summary>指定カテゴリーだけがこの攻撃を無効化できるようにする。</summary>
    ProhibitInvalidationExceptCategory = 195,
    /// <summary>指定した属性と威力で対象を攻撃する。</summary>
    ElementAttackPower = 196,
    /// <summary>自身以外の敵味方全員に指定ターンの状態を付与する。</summary>
    ApplyStateToAllOthers = 197,
    /// <summary>次の指定回数だけ、対象がエリア機能を宣言する際のジルダ消費に倍率を掛ける。</summary>
    NextAreaMoneyFactor = 198,
    /// <summary>次の指定回数だけ、対象が指定カテゴリーのスキルを宣言する際の目標値に倍率を掛ける。</summary>
    NextCategoryTargetFactor = 199,
    /// <summary>プレイヤーの敵として敵チームのターンに戦闘へ合流する。</summary>
    BattleRejoinEnemyTurn = 200,
    /// <summary>発動ロールが指定回数成功するごとに対象を指定威力で攻撃する。</summary>
    AttackPerSuccessfulRoll = 201,
    /// <summary>すべての発動ロールが成功したときに対象を武器攻撃する。</summary>
    AttackIfAllRollsSucceed = 202,
    /// <summary>自身の次のターン開始時に指定リソースを得る。</summary>
    NextOwnTurnResource = 203,
    /// <summary>対象に、自身の達成値を目標値とする行為判定を行わせる。</summary>
    OpposedTargetCheck = 204,
    /// <summary>自身の指定能力値に影響するバフを解除する。</summary>
    RemoveBuffsAffectingStat = 205,
    /// <summary>自身のリソースを指定式の分だけ減らす。減少には0の下限を設ける。</summary>
    SubjectResourceLossFloorZero = 206,
    /// <summary>次の指定回数のスキルで、該当カテゴリーのロールにダイスを追加する。</summary>
    NextCategoryDiceBonus = 207,
    /// <summary>指定カテゴリーの対象選択を受けず、指定状態を無効にする。</summary>
    ImmuneToTargetingAndState = 208,
    /// <summary>対象を特性のパッシブ効果に記載された武器の装備者として扱い、その武器威力を使えるようにする。</summary>
    UseGrantedWeaponDefinition = 209,
    /// <summary>指定部位に防具を装備した扱いとし、付与者と同じ防御点を加える。</summary>
    EquipArmorWithApplierDefense = 210,
    /// <summary>自身に対する効果の対象選択を任意で無効にする。</summary>
    MayCancelAnyTargeting = 211,
    /// <summary>自身が指定カテゴリーを宣言したときにこの状態を解除する。</summary>
    ExpireOnCategoryDeclaration = 213,
    /// <summary>自身が宣言できる行動を指定した2カテゴリーに限定する。</summary>
    RestrictDeclaredCategories = 214,
    /// <summary>例外の武器カテゴリーを除き、自身または自身の接近グループを対象とする効果を無効にする。</summary>
    NegateEffectsOnGroupExceptWeapons = 215,
    /// <summary>例外の武器カテゴリーを除き、自身が関わる同一接近グループ必須の効果を無効にする。</summary>
    NegateSameGroupEffectsExceptWeapons = 216,
    /// <summary>状態付与後、次の敵ターンの開始時に自身が指定スキル値の武器攻撃を受ける。</summary>
    ReceiveDelayedEnemyTurnAttack = 217,
    /// <summary>指定カテゴリーの効果と指定状態の解除を受け付けず、リソースを指定値にする。</summary>
    BlockRevivalAndSetResource = 218,
    /// <summary>状態の効果で指定式の武器威力を使用できるようにする。</summary>
    UseStateAsWeapon = 219,
    /// <summary>自身への指定2カテゴリーの行動が他の効果で無効にされることを防ぐ。</summary>
    ProtectIncomingCategoriesFromInvalidation = 220,
    /// <summary>自身のターン開始時に指定属性のダメージを受ける。</summary>
    OwnTurnElementDamage = 221,
    /// <summary>消費リソースが規定値以下の該当行動について、接近の宣言条件を免除する。</summary>
    WaiveMeleeBelowCost = 222,
    /// <summary>付与から指定ターン後、スキル値に係数を掛けて対象を武器攻撃する。</summary>
    DelayedSkillValueAttack = 223,
    /// <summary>自身のエリア移動時にこのスタック数を増減する。</summary>
    GainStacksOnAreaMove = 224,
    /// <summary>スタックが指定リソースの最大値を超えると、すべての宣言を禁止する。</summary>
    ForbidDeclarationsAboveResourceCap = 225,
    /// <summary>付与者の所持品を自身が代わりに使用できるようにする。</summary>
    UseApplierPossessions = 226,
    /// <summary>敵からの指定カテゴリーの状態付与を指定回数まで無効にする。</summary>
    PreventEnemyStateApplications = 227,
    /// <summary>アクティブスキルの効果の選択対象にならなくする。</summary>
    CannotBeSelectedByActive = 228,
    /// <summary>攻撃を受けたとき、スキル値に係数を掛けた量だけリソースを回復する。</summary>
    RecoveryOnAttackBySkillValue = 229,
    /// <summary>選択した対象スキルを自身のスキルとして宣言する。</summary>
    DeclareSelectedSkillAsOwn = 230,
    /// <summary>対象が指定したいずれかの状態であっても、この状態の効果を適用する。</summary>
    ApplyDespiteTargetStates = 231,
    /// <summary>対象が該当種族なら指定ターンの状態を付与する。</summary>
    ApplyRaceConditionalState = 232,
    /// <summary>自身の指定状態を解除し、別の状態を指定ターン付与する。</summary>
    GainTimedStateAfterRemove = 233,
    /// <summary>消費したリソース量に比例する数のスタックを得る。</summary>
    StackGainPerSpentResource = 234,
    /// <summary>達成値が目標値を規定幅以上上回ると、自身への指定カテゴリーの行動を無効にする。</summary>
    NegateCategoryOnRollMargin = 235,
    /// <summary>指定カテゴリーの威力に倍率を掛けたダメージを自身が受ける。</summary>
    ReceiveIncomingPowerShare = 236,
    /// <summary>自身にカテゴリー付きの状態を指定ターン付与する。</summary>
    TimedStateWithCategory = 237,
    /// <summary>武器の切り替え時に素手を切り替え元または切り替え先に選べる。</summary>
    MaySwitchBareHanded = 238,
    /// <summary>戦闘中、装備中の指定カテゴリーの装備と対象装備を入れ替える。</summary>
    SwapEquippedSlots = 239,
    /// <summary>自身を除く対象に指定数のスタックを付与する。</summary>
    GainStackOnOtherTarget = 240,
    /// <summary>付与者が指定スキルを再び発動するとこの状態を解除する。</summary>
    ExpireOnApplierSkill = 242,
    /// <summary>自身が宣言したスキルまたはアイテムの処理後にこの状態を解除する。</summary>
    ExpireAfterOwnSkillOrItem = 243,
    /// <summary>指定カテゴリーの処理後、消費した指定アイテムの数だけスタックを減らす。</summary>
    SpendStacksPerUsedItem = 244,
    /// <summary>付与者が指定カテゴリーの発動に成功した後、この状態を解除する。</summary>
    ExpireOnApplierSuccess = 245,
    /// <summary>効果の適用後に自身の状態スタックを指定数減らす。</summary>
    ConsumeStacksAfterEffect = 246,
    /// <summary>効果の適用後にこの効果を解除する。</summary>
    ExpireAfterEffect = 247,
    /// <summary>指定スキルで選んだ相手と接近するか、その相手が指定状態になると、この状態を解除する。</summary>
    ExpireOnMeleeOrSelectedState = 248,
    /// <summary>自身のアクティブ効果の宣言を禁止する。</summary>
    CannotDeclareActive = 249,
    /// <summary>効果を宣言する際に指定した個数と面数のダイスを振る。</summary>
    RollOnDeclaration = 250,
    /// <summary>次のターン開始時に自身に状態を付与する。</summary>
    GainStateNextTurn = 251,
    /// <summary>対象に付与された状態を指定数解除する。</summary>
    RemoveSelectedStateCount = 252,
    /// <summary>状態を付与した時点で指定リソースに倍率を掛ける。</summary>
    ScaleResourceOnApplication = 254,
    /// <summary>毎ターン開始時に指定量のリソースを消費する。</summary>
    SpendResourceEachTurn = 255,
    /// <summary>状態を解除した時点で指定リソースに倍率を掛ける。</summary>
    ScaleResourceOnRemoval = 256,
    /// <summary>並び順で選んだ種族能力値ボーナスに割合を加え、指定条件に応じて増加量を変える。</summary>
    RacialAbilityPercentByOrdinal = 257,
    /// <summary>自身と対象を同じ接近グループに入れ、2種類の指定状態にする。</summary>
    JoinMeleeAndSecondState = 258,
    /// <summary>スキル値に係数を掛けた武器威力で、指定回数の連続攻撃を行う。</summary>
    RepeatAttackScaledWeapon = 259,
    /// <summary>武器威力に倍率を掛け、ダイスで決めた回数だけ連続攻撃する。</summary>
    RepeatAttackRolledCount = 260,
    /// <summary>発動ロールに成功するたびに、スキル値と係数を用いて武器攻撃する。</summary>
    AttackForEachSuccessfulActivation = 261,
    /// <summary>対象に付与されたバリアのスタック数に倍率を掛ける。</summary>
    MultiplySelectedBarrierStacks = 262,
    /// <summary>現在のエリアの進行ロールの達成値に倍率を掛ける。</summary>
    CurrentAreaProgressFactor = 263,
    /// <summary>発動対象が自身と同じ接近グループにいる場合に武器攻撃する。</summary>
    TriggerAttackIfMelee = 265,
    /// <summary>自身の被ダメージと指定リソースへのダメージの両方を軽減する。</summary>
    ReduceDamageAndResourceDamage = 266,
    /// <summary>対象が指定状態なら、他のキャラクターから見た達成値に倍率を掛ける。</summary>
    TargetStateCheckFactor = 267,
    /// <summary>対象の最大HPの指定割合をダメージとして与える。</summary>
    DamagePercentTargetHP = 268,
    /// <summary>誰からも効果の選択対象にならないようにする。</summary>
    PreventAllTargetSelection = 269,
    /// <summary>敵から自身へのすべての効果を無効にする。</summary>
    IgnoreIncomingEnemyEffects = 270,
    /// <summary>この状態の終了時、それまで無効にしたすべての効果を付与者が受ける。</summary>
    TransferPreventedEffectsAtExpiry = 271,
    /// <summary>自身への指定カテゴリーの効果を無効にする。</summary>
    NegateIncomingCategory = 272,
    /// <summary>バフ付与者が状態の保持者に代わって行動を指定し宣言できる。</summary>
    ApplierControlsDeclarations = 273,
    /// <summary>自身のターン開始時、指定カテゴリーの判定で敵全員を攻撃する。</summary>
    AttackAllEnemiesOnOwnTurn = 274,
    /// <summary>自身のターン開始時、自身以外の指定数のキャラクターのリソースを回復する。</summary>
    HealOtherCharacterOnOwnTurn = 275,
    /// <summary>不足した消費を以後のターン開始時に分割して支払い、完済後にアクティブ効果を発動する。</summary>
    IncrementalPaymentBeforeActivation = 276,
    /// <summary>次のターンを待たずに行動可能にする。</summary>
    ImmediateNextAction = 277,
    /// <summary>遭遇したエネミーと戦闘するかを選べるようにする。</summary>
    ChooseWhetherEncounterEnemy = 278,
    /// <summary>自身が指定の称号を獲得する。</summary>
    CasterNamedTitle = 279,
    /// <summary>このスキルはクリティカルしても自動成功扱いにしない。</summary>
    CriticalDoesNotEnsureSuccess = 280,
    /// <summary>パーティーを編成している間、味方ターンの最初に自身が行動する。</summary>
    MinimumRollAlliesFirst = 281,
    /// <summary>対象のリソースを減らし、指定ターンの状態を付与する。</summary>
    DropResourceAndApplyTimedState = 282,
    /// <summary>対象の接近グループと指定した状態を自身に付け替える。</summary>
    TransferMeleeToCaster = 283,
    /// <summary>装備の切り替え元または切り替え先として空き部位を選べる。</summary>
    MayEmptyGearSlot = 284,
    /// <summary>対象が指定カテゴリーならHPを0にする。</summary>
    ZeroHPForCategory = 285,
    /// <summary>自身に永続状態を付与する。</summary>
    ApplyPermanentOwnState = 286,
    /// <summary>敵の効果がカテゴリーで対象を選ぶ際、このスキルを指定カテゴリーから除外する。</summary>
    ExcludeCategoryForEnemyEffects = 287,
    /// <summary>対象に付与された指定カテゴリーの状態を解除する。</summary>
    RemoveTargetStateCategory = 288,
    /// <summary>自身の指定カテゴリーの状態を解除し、代わりに指定スタックを得る。</summary>
    RemoveForOwnStacks = 289,
    /// <summary>達成値を指定の参照値として扱う。</summary>
    SetReferenceActionResult = 290,
    /// <summary>複数のロール結果の合計を達成値として扱う。</summary>
    SumRollsAsResult = 291,
    /// <summary>自身が指定リソースへのダメージを受ける。</summary>
    SelfResourceDamage = 292,
    /// <summary>自身が宣言した行動の発動を取り消し、対象のカウンター効果を無効にする。</summary>
    CancelDeclarationAndCounter = 293,
    /// <summary>設置者が対象を指定スキル値で武器攻撃する。</summary>
    InstallerWeaponAttack = 294,
    /// <summary>対象全員のリソースの合計を人数で割った値に、各対象のリソースをそろえる。</summary>
    EqualizeTargetResources = 295,
    /// <summary>選択した状態スタックをすべて対象に移す。</summary>
    TransferSelectedStateStacks = 296,
    /// <summary>対象の代わりに指定した2カテゴリーの行動を受ける。</summary>
    TakeTwoActionsForTarget = 297,
    /// <summary>非戦闘時に指定スタックのスキル消費を無視できる。</summary>
    IgnoreStackCostOutsideBattle = 298,
    /// <summary>自身が受ける通常のダメージと指定リソースのダメージを軽減する。</summary>
    DualDamageReduction = 299,
    /// <summary>次のターン開始時にカテゴリー付きの状態になり、指定リソースを得る。</summary>
    NextTurnCategorizedStateAndRecovery = 300,
    /// <summary>対象と指定の接近状態になる。</summary>
    PlaceTargetInMeleeState = 301,
    /// <summary>指定カテゴリーに属する状態のスタックを永続的に得る。</summary>
    GrantPermanentStateStacks = 302,
    /// <summary>自身に2種類の永続状態を付与する。</summary>
    TwoPermanentOwnStates = 303,
    /// <summary>指定カテゴリーの行動で特定リソースの消費を免除できる。</summary>
    WaiveCategoryCost = 304,
    /// <summary>対象に指定カテゴリーのアイテムのアクティブ効果を適用する。</summary>
    ApplyTargetConsumableCategory = 305,
    /// <summary>今回付与したバフを永続効果として扱う。</summary>
    MakeGrantedBuffPermanent = 306,
    /// <summary>指定カテゴリーに記載されたアイテムを入手できる。</summary>
    EarnRecipeProductByCategory = 307,
    /// <summary>指定した2カテゴリーに記載されたアイテムを入手できる。</summary>
    EarnRecipeProductByEitherCategory = 308,
    /// <summary>この効果に対するカウンター効果の発動を禁止する。</summary>
    DisableCounterAgainstEffect = 309,
    /// <summary>対象に追加の属性威力による攻撃とリソースダメージを与える。</summary>
    BonusElementAndResourceDamage = 310,
    /// <summary>次の指定回数のスキルに、対象への属性攻撃とリソースダメージを追加する。</summary>
    NextSkillBonusElementAndDamage = 311,
    /// <summary>この武器を選択してスキルを発動した場合、効果対象への指定カテゴリーの威力に倍率を掛ける。</summary>
    NamedWeaponBonusAttack = 312,
    /// <summary>この武器を選択してスキルを発動した場合、攻撃対象の防御点を指定点数だけ無視する。</summary>
    NamedWeaponDefensePenetration = 313,
    /// <summary>この武器を選択してスキルを発動した場合、指定カテゴリーの達成値と威力に別々の倍率を掛ける。</summary>
    NamedWeaponAccuracyPower = 314,
    /// <summary>リソース最大値が条件を満たすと、状態解除・リソース全回復・永続スタック獲得を選べる。</summary>
    OptionalReviveByMaxResource = 315,
    /// <summary>自身の指定状態を解除し、指定リソースを全回復する。</summary>
    RemoveStateFullyRecover = 316,
    /// <summary>指示された行動の行為判定を状態の保持者自身が行う。</summary>
    HolderPerformsOrderedChecks = 317,
    /// <summary>この付与を無効化できるカテゴリーを制限する。</summary>
    RestrictionOnNegatingState = 318,
    /// <summary>引き金となった指定カテゴリーの行動を無効にする。</summary>
    NegateTriggeringAction = 319,
    /// <summary>判定に失敗したとき、対象を指定状態にする。</summary>
    TargetStateOnCheckFailure = 320,
    /// <summary>指定式で求めた数の状態スタックを対象に付与する。</summary>
    ApplyFormulaTargetStacks = 321,
    /// <summary>対象の指定カテゴリーに属するものをすべて解除する。</summary>
    RemoveAllTargetCategory = 322,
    /// <summary>ダイスの出目が指定値以下なら宣言を失敗にする。</summary>
    FailDeclarationOnRollAtMost = 323,
    /// <summary>対象の指定カテゴリーの応答を無効にし、元の効果を与える。</summary>
    NegateTargetResponseAndResolve = 324,
    /// <summary>指定能力値による行為判定ではクリティカルもファンブルも発生させない。</summary>
    NoCriticalFumbleForStat = 325,
    /// <summary>戦闘開始から指定ターンの間、対象に状態を付与する。</summary>
    InitialBattleTargetState = 326,
    /// <summary>対象のドロップロールを指定回数行う。</summary>
    RollTargetDrop = 327,
    /// <summary>指定スキル値の武器威力に倍率を掛けて対象を攻撃する。</summary>
    ScaleWeaponPower = 328,
    /// <summary>この攻撃の威力と被ダメージを、防御点・効果・状態による増減の対象外にする。</summary>
    UnmodifiableDamage = 329,
    /// <summary>対象の指定カテゴリーの装備を解除する。</summary>
    DisarmTargetCategory = 330,
    /// <summary>名前で指定したものを発動できるようにする。</summary>
    ActivateNamedEntity = 331,
    /// <summary>対象の2つの指定カテゴリーを解除する。</summary>
    RemoveTwoTargetCategories = 333,
    /// <summary>自身に指定カテゴリーの状態を付与する。</summary>
    GrantOwnCategorizedState = 334,
    /// <summary>指定行動で敵のリソースを減らすと、自身もスタック数に応じて同じリソースを失う。</summary>
    MirrorEnemyResourceLossByStacks = 335,
    /// <summary>指定状態の効果を無視し、指定上限まで対象に代わって宣言する。</summary>
    DeclareForTargetIgnoringState = 336,
    /// <summary>武器威力と属性威力を足し、対象の指定リソースにダメージを与える。</summary>
    ElementWeaponResourceDamage = 337,
    /// <summary>規定スタック数に達したとき、ランダムなパーティーメンバーに指定スタックを付与する。</summary>
    SpreadAtStackCount = 339,
    /// <summary>指定スキル値の武器威力で対象の指定リソースにダメージを与える。</summary>
    WeaponResourceDamage = 340,
    /// <summary>指定状態のすべての対象を指定威力で攻撃する。</summary>
    StateGroupPowerAttack = 341,
    /// <summary>武器威力と属性威力の合計に倍率を掛けて攻撃する。</summary>
    ScaledElementWeaponAttack = 342,
    /// <summary>設置者が武器威力で対象の指定リソースにダメージを与える。</summary>
    InstallerWeaponResourceDamage = 343,
    /// <summary>指定カテゴリーのペットの行動を1つ選んで発動する。</summary>
    ChoosePetAction = 344,
    /// <summary>この効果を発動するたび、上限に達するまで自身が指定スタックを得る。</summary>
    StacksPerActivation = 345,
    /// <summary>次の指定回数のスキルで、指定能力値によるロールにダイスを追加する。</summary>
    NextStatDiceBonus = 346,
    /// <summary>指定カテゴリーの行動の対象をその発動者に変更する。</summary>
    ReturnCategoryToCaster = 347,
    /// <summary>対象が指定状態なら、代わりに別の状態を指定ターン確定で付与する。</summary>
    GuaranteedStateAlternative = 348,
    /// <summary>対象の2種類のリソースを指定割合だけ減らす。</summary>
    ReduceTwoResourcesPercent = 349,
    /// <summary>ターン終了後にこの状態を解除する。</summary>
    ExpireAtTurnEnd = 350,
    /// <summary>指定カテゴリーとバフ付与の適用先を発動者へ変更する。付随する攻撃は移さない。</summary>
    RedirectBuffOrAuraToCaster = 351,
    /// <summary>移動先の対象と同じ接近グループに入り、指定状態になる。</summary>
    JoinDestinationMeleeState = 352,
    /// <summary>指定スキル値の武器威力に相当するスタックを対象に付与する。</summary>
    ApplyWeaponPowerStacks = 353,
    /// <summary>処理後のリソースが指定値以上なら、指定ロールで付与された状態をすべて解除する。</summary>
    ClearInjuryAfterHPThreshold = 354,
    /// <summary>リソースの増加を最大値までに制限する。</summary>
    CapAtResourceMaximum = 355,
    /// <summary>リソースが指定値以上になるなら、指定ロールによる状態をすべて解除する。</summary>
    ClearRollStatesAtThreshold = 356,
    /// <summary>自身を追加の種族カテゴリーとして扱う。</summary>
    AdditionalRaceCategory = 357,
    /// <summary>味方の先攻・後攻にかかわらず、対象がメインフェーズ開始時に指定上限まで先に行動できる。</summary>
    PrioritizedMainPhaseAction = 358,
    /// <summary>付与者に指定量のダメージを与える。</summary>
    DamageToApplier = 359,
    /// <summary>選択したカテゴリーの武器威力に係数を掛けて対象を攻撃する。</summary>
    SelectedCategoryWeaponPower = 360,
    /// <summary>対象の習得済みスキル名を使ったアイテムを生成する。</summary>
    CreateItemFromSkillName = 361,
    /// <summary>選択したカテゴリーの武器威力に係数を掛け、対象を指定回数攻撃する。</summary>
    RepeatedSelectedWeaponPower = 362,
    /// <summary>このスキルによる回復を受けられる回数に1日あたりの上限を設ける。</summary>
    DailyLimitOnReceivingRecovery = 363,
    /// <summary>アイテムの発動ロールとリソース消費を免除する。</summary>
    WaiveItemActivationAndCosts = 364,
    /// <summary>後に記載されたロール結果に対応する効果を適用する。</summary>
    ApplyRollOutcome = 365,
    /// <summary>発動ロールの結果に従って、指定名・カテゴリーの生産物を得る。</summary>
    CraftNamedCategoryFromOutcome = 366,
    /// <summary>状態の保持者による指定カテゴリーの宣言を禁止する。</summary>
    ProhibitStateCategoryDeclarations = 367,
    /// <summary>代行・指示された宣言の消費リソースを保持者自身が支払う。</summary>
    DeclarationCostsPaidByHolder = 368,
    /// <summary>指定ロールによって付与された状態をすべて解除する。</summary>
    RollStatusCleanup = 369,
    /// <summary>付与者が指定スキルの発動に成功した後、この状態を解除する。</summary>
    ExpireOnApplierNamedSkillSuccess = 370,
    /// <summary>自身がキャラクターを選択する際、対象に必ず付与者を含める。</summary>
    MustIncludeApplier = 372,
    /// <summary>指定カテゴリーの効果を発動すると、この効果を解除する。</summary>
    ExpireOnCategoryActivation = 373,
    /// <summary>自身がスキルを宣言することを禁止する。</summary>
    CannotDeclareSkills = 374,
    /// <summary>状態に付随する効果がないことを明示する。</summary>
    AllowNoStateEffect = 375,
    /// <summary>自身のすべての能力値Bを同じ指定値に置き換える。</summary>
    ReplaceEveryAbilityBonus = 376,
    /// <summary>自身に対する指定状態を無効にする。</summary>
    CategoryImmunityToPosition = 377,
    /// <summary>直前の処理後にスタック数を指定量だけ増減する。</summary>
    IncreasedStacksAfterLoss = 378,
    /// <summary>スタックが規定数に達したとき、自身に永続状態を付与する。</summary>
    PermanentStateAtStacks = 379,
    /// <summary>この状態の解除時に自身が指定威力の攻撃を受ける。</summary>
    ReceiveAttackAtRemoval = 380,
    /// <summary>この効果に対する指定2カテゴリーの宣言を禁止する。</summary>
    CannotRespondWithTwoCategories = 381,
    /// <summary>スタックが閾値を超えるとリソースダメージを受け、指定ターンの状態になる。</summary>
    DamageAndStateOverStacks = 382,
    /// <summary>規定数以上の敵から接近されるとき、その接近を任意で無効にする。</summary>
    MayRefuseGroupMelee = 383,
    /// <summary>毎ターン開始時に自身の指定リソースを増減する。</summary>
    TurnStartResourceDelta = 384,
    /// <summary>後で発動する効果の対象と威力を状態付与時に決める。</summary>
    SnapshotRecipientsAndPower = 385,
    /// <summary>この状態による攻撃の後に状態を解除する。</summary>
    ExpireAfterTriggeredAttack = 387,
    /// <summary>自身のターン開始時に自身の指定リソースを回復する。</summary>
    OwnTurnResourceRecovery = 388,
    /// <summary>指定カテゴリーのスキルで列挙された武器を使う攻撃の威力に倍率を掛ける。</summary>
    EquipmentCategoryMasteryList = 389,
    /// <summary>自身による指定2カテゴリーの宣言を禁止する。</summary>
    ProhibitTwoDeclarations = 390,
    /// <summary>装備者が経験値と決意を得ると、自身も1日の上限まで同じ報酬を得られる。</summary>
    WearerSharesRewards = 391,
    /// <summary>指定状態を付与した他のプレイヤーと同じエリアにいる扱いにでき、移動時の耐久ロールを省く。</summary>
    WearerSharesArea = 392,
    /// <summary>他のプレイヤーに指定状態を付与していない間、エリア機能を宣言できなくする。</summary>
    AreaUnavailableWithoutWielder = 393,
    /// <summary>この攻撃では対象の防御点を無視する。</summary>
    IgnoreDefenseForTurnAttack = 394,
    /// <summary>指定カテゴリーの宣言ごとに、符号付きの指定量だけリソースを変化させる。</summary>
    SignedCategoryCost = 396,
    /// <summary>指定した2カテゴリーのいずれかを装備していると、戦闘開始時に指定状態になる。</summary>
    InitialEquipmentPairState = 397,
    /// <summary>この攻撃のダメージで生じた相手の重症ロールの出目を直接指定できる。</summary>
    SelectTriggeredInjuryRoll = 398,
    /// <summary>指定ターンの間、特性の制約を超えてエリア機能や指定カテゴリーを宣言できる。</summary>
    TemporarilyWaiveTraitLimits = 399,
    /// <summary>進行ロールで戦闘する場合、開始前に各パーティーメンバーがアクティブ効果の発動や装備変更を行える。</summary>
    PartyPrepareBeforeProgressBattle = 400,
    /// <summary>後続のターン開始時に不足した消費を分割して支払い、完済後にアクティブ効果を発動する。</summary>
    DeferEffectPayment = 401,
    /// <summary>敵味方に同じ先行効果があるとき、先攻側を優先する。</summary>
    AlliedTurnInitiativeTie = 402,
    /// <summary>召喚体を自身の行動終了後に取り除く。</summary>
    RemoveSummonAfterAction = 404,
    /// <summary>参照した威力に倍率を掛けて対象を攻撃する。</summary>
    NextCategoryAttackAtPowerRatio = 405,
    /// <summary>対象の回避の目標値を指定量だけ増減する。</summary>
    SetTargetResponseThreshold = 406,
    /// <summary>達成値が指定範囲内なら、対応するリソースを指定量消費する。</summary>
    ResolveCostByResultRange = 407,
    /// <summary>達成値が指定下限以上なら、対応するリソースを指定量消費する。</summary>
    ResolveCostByResultAtLeast = 408,
    /// <summary>選択した材料に応じて、後続の生産条件で定めたアイテムを得る。</summary>
    TransformSelectedIngredients = 409,
    /// <summary>指定サイズと2種類の材料の必要数に基づき、生産物と個数を決める。</summary>
    CraftWithTwoElementsBySize = 410,
    /// <summary>指定サイズかつ候補カテゴリーのいずれかに該当する材料から生産物を得る。</summary>
    CraftFromAnyItemCategory = 411,
    /// <summary>指定サイズかつ指定カテゴリーの材料から生産物を得る。</summary>
    CraftFromItemCategory = 412,
    /// <summary>材料を消費せずに指定アイテムを得る。</summary>
    CraftFreeItem = 413,
    /// <summary>指定名の材料から指定名・個数の生産物を得る。</summary>
    CraftFromNamedItem = 414,
    /// <summary>生産成功時に、指定数の生産物のグレードを増減する。</summary>
    CraftGradeShift = 415,
    /// <summary>指定カテゴリーの発動ロールを指定数だけ自動成功にする。</summary>
    EnsureCraftRollSuccess = 416,
    /// <summary>指定カテゴリーの発動ロールをすべて自動失敗にする。</summary>
    FailAllCraftRolls = 417,
    /// <summary>指定カテゴリーの価格に応じたジルダとクエスト扱いの経験値・決意を、1日の上限内で得る。</summary>
    AppraiseForQuestReward = 418,
    /// <summary>対象アイテムが指定グレードなら追加の決意を得る。</summary>
    AwardBonusResolveForGrade = 419,
    /// <summary>リソースが指定値以下になった際の状態付与を保留する。</summary>
    DeferLowResourceState = 420,
    /// <summary>指定カテゴリーの行動を受けた時点または自身のターン開始時にリソース条件を満たせば、保留した状態を付与する。</summary>
    ApplyDeferredStateOnHitOrTurn = 421,
    /// <summary>自身に付与された指定名の状態を解除する。</summary>
    RemoveSelfNamedState = 422,
    /// <summary>戦闘終了後に自身の2つのリソースを指定値にし、2つの状態を付与する。</summary>
    SetTwoResourcesAndStatesAtBattleEnd = 423,
    /// <summary>自身の2つのリソースを指定値にし、2つの状態を付与する。</summary>
    SetTwoResourcesAndStates = 424,
    /// <summary>自身に対する指定カテゴリーの効果を無効にする。</summary>
    IgnoreCategoryEffects = 425,
    /// <summary>指定した武器枠の武器威力に倍率を掛け、対象を指定回数攻撃する。</summary>
    AttackWithWeaponSlot = 426,
    /// <summary>直前の攻撃に続き、指定した武器枠の威力に倍率を掛けて対象を指定回数攻撃する。</summary>
    FollowUpWithWeaponSlot = 427,
    /// <summary>状態付与後の指定回目のターン開始時に、スタック数と同じダメージを受ける。</summary>
    StackDamageOnTurnAfterApplication = 428,
    /// <summary>指定した2カテゴリーのいずれかの装備を外すと、このバフを解除する。</summary>
    RemoveWhenEitherWeaponUnequipped = 429,
    /// <summary>対象の指定カテゴリーの行動を無効にする。</summary>
    CancelTargetActionCategory = 430,
    /// <summary>後続の効果で参照するダメージ軽減値を式から決める。</summary>
    ComputeReduction = 431,
    /// <summary>自身の被ダメージを軽減し、受けた指定カテゴリーと同等の効果で攻撃者を攻撃する。</summary>
    ReduceAndReflectIncomingCategory = 432,
    /// <summary>相手の被ダメージにも同じ軽減値を適用する。</summary>
    ReduceReflectedDamage = 433,
    /// <summary>選択した武器と同等の武器威力と装備効果を使えるようにする。</summary>
    UseSelectedWeaponProperties = 434,
    /// <summary>指定カードに記載されたキャラクターを召喚できる。</summary>
    SummonFromCard = 435,
    /// <summary>次の指定回数だけ、該当カテゴリーの威力ロールを任意で振り直せる。</summary>
    OptionalNextPowerReroll = 436,
    /// <summary>威力を振り直した場合に、元の出目を使えなくする。</summary>
    DiscardOriginalPowerRoll = 437,
    /// <summary>指定武器を装備した扱いとし、この状態を武器に選ぶ該当カテゴリーの行動で指定式の武器威力を使う。</summary>
    UseStateWeaponForCategory = 438,
    /// <summary>任意のタイミングでこの状態を解除し、後続の効果を得る。</summary>
    ReleaseToChooseEffect = 439,
    /// <summary>選択肢の効果として、GMが指定する未来の出来事を知ることができる。</summary>
    RevealFutureByGM = 440,
    /// <summary>次の指定回数の行為判定を複数回振り、採用する結果を選べる。</summary>
    ChooseNextRollResult = 441,
    /// <summary>対象の指定状態が解除されると、指定スキル値と能力値から求めた量のリソースを失う。</summary>
    ResourceLossOnStateRemoval = 442,
    /// <summary>後から使う威力を状態付与時に算出する。</summary>
    SnapshotPowerOnApply = 443,
    /// <summary>効果1回で解除できるスタック数に上限を設ける。</summary>
    CapStacksRemovedPerEffect = 444,
    /// <summary>指定数の対象について、状態解除・最大値割合の回復・リソースダメージを伴って蘇生する。</summary>
    ReviveWithRecoveryAndDamage = 445,
    /// <summary>倍率を個別に掛けた武器威力と2属性の属性威力を合算して攻撃する。</summary>
    ScaledDualElementAttack = 446,
    /// <summary>エンカウントフェーズに、このエリアで遭遇する全敵を指定スキル値で攻撃した扱いにする。</summary>
    EncounterAllEnemiesAttack = 447,
    /// <summary>指定カテゴリーの発動ロールでファンブルすると、さらにスタック数が変化する。</summary>
    GainStacksOnCategoryFumble = 448,
    /// <summary>自身にカテゴリー付きの永続状態を付与する。</summary>
    PermanentCategorizedState = 450,
    /// <summary>指定カテゴリーの武器に持ち替え、続けて武器攻撃を行う。</summary>
    ChangeWeaponAndAttack = 451,
    /// <summary>指定カテゴリーの行動または効果を無効にする。</summary>
    InvalidateCategory = 452,
    /// <summary>対象に付与された状態を指定数解除する。</summary>
    RemoveTargetStatesByCount = 453,
    /// <summary>自身への指定カテゴリーの達成値が目標値に対する条件を満たすと、対応するカテゴリーを無効にする。</summary>
    InvalidateCategoryNearThreshold = 454,
    /// <summary>自身の指定名の状態を解除する。</summary>
    RemoveNamedSelfState = 455,
    /// <summary>召喚者の指定スキル値を使って対象を武器攻撃する。</summary>
    AttackWithSummonerSkillValue = 456,
    /// <summary>行動の処理後にこのペットを取り除く。</summary>
    RemovePetAfterAction = 457,
    /// <summary>次の指定回数のうち、両カテゴリーに該当する宣言に失敗するとファンブル扱いになり、自身もダメージを受ける。</summary>
    NextCategoriesFailureAsFumbleAndDamage = 458,
    /// <summary>別の指定カテゴリーのエリアへ移動するまで効果が持続する。</summary>
    PersistUntilAreaCategoryChange = 459,
    /// <summary>カウンターを発動した人物を除いて対象を選び直し、元の効果を発動する。</summary>
    RetargetExceptCounterUser = 460,
    /// <summary>候補から指定数の称号を選んで習得する。</summary>
    ChooseTitle = 461,
    /// <summary>選択して習得できる称号の候補を1つ示す。</summary>
    TitleOption = 462,
    /// <summary>選択した属性に応じた状態を自身に付与する。</summary>
    ApplyStateByChosenElement = 463,
    /// <summary>特定の属性を選んだ場合に自身が得る状態を定める。</summary>
    ElementStateChoice = 464,
    /// <summary>このスキルで指定した名前の効果を適用することを禁止する。</summary>
    ProhibitSkillEffectApplication = 465,
    /// <summary>付与から指定ターン後に自身のリソースを増減する。</summary>
    ResourceAfterDelay = 466,
    /// <summary>自身が指定種族以外なら、アクティブ効果を発動するたびにリソースを失い、最大値割合のダメージを受ける。</summary>
    LoseResourcesOnActiveExceptRace = 467,
    /// <summary>指定カテゴリーの発動ロールで所定の目標値を達成すると、この効果を解除する。</summary>
    RemoveOnSuccessfulCategoryRoll = 468,
    /// <summary>自身と同じ接近グループのキャラクターを、武器威力と2属性の属性威力の合計で攻撃する。</summary>
    MeleeGroupElementWeaponAttack = 469,
    /// <summary>自身に指定カテゴリーの行動を発動した相手に追加ダメージを与える。</summary>
    DamageIncomingCategoryUser = 471,
    /// <summary>自身の行為判定にスタック数と係数の積を加算する。</summary>
    ActionBonusFromStack = 472,
    /// <summary>行為判定の後にこの効果を解除する。</summary>
    RemoveAfterActionRoll = 473,
    /// <summary>スキル値に係数を掛けた武器威力と2属性の属性威力を合算して攻撃する。</summary>
    ScaledSkillValueWithElementDamage = 474,
    /// <summary>与えたダメージと同じ数のスタックを対象に付与する。</summary>
    GrantDamageAsStacks = 475,
    /// <summary>スタックが2つのリソース最大値の合計と係数から求めた閾値を超えると、すべての宣言を禁止する。</summary>
    BlockDeclarationsByStackThreshold = 476,
    /// <summary>状態の指定ターン目に、指定カテゴリーの発動を禁止する。</summary>
    ProhibitCategoriesOnStateTurn = 477,
    /// <summary>このターンの指定リソースの減少を防ぐ。</summary>
    PreventResourceLossThisTurn = 478,
    /// <summary>召喚者自身を除き、召喚者と同じ対象を選んで同じスキルを発動する。</summary>
    RepeatSummonerSkillOnSameTargets = 479,
    /// <summary>指定カテゴリーの宣言を禁止する。</summary>
    ForbidCategoryDeclaration = 480,
    /// <summary>対象が2カテゴリーの両方に該当すれば、武器威力に相当する状態スタックを付与する。</summary>
    CategoryPairWeaponStackGrant = 481,
    /// <summary>対象が2カテゴリーの両方に該当すれば、指定スキル値で武器攻撃する。</summary>
    CategoryPairWeaponAttack = 482,
    /// <summary>対象の2つのリソースをそれぞれ指定量だけ変化させる。</summary>
    ModifyTwoResources = 483,
    /// <summary>自身の戦闘離脱を禁止する。</summary>
    ForbidCombatExit = 484,
    /// <summary>接近状態を解除する効果の宣言を禁止する。</summary>
    ForbidDisengagement = 485,
    /// <summary>毎ターン開始時にリソースを変化させ、能力値Bによるダイスロールに倍率を掛ける。</summary>
    TurnResourceAndStatRoll = 486,
    /// <summary>戦闘終了時に自身の2つのリソースを追加で増減する。</summary>
    LoseTwoResourcesAtBattleEnd = 487,
    /// <summary>毎ターン開始時に指定リソースの最大値に比例するダメージを受ける。</summary>
    MaxResourceDamageEachTurn = 488,
    /// <summary>味方チームの後攻を確定する。</summary>
    PartyActsLast = 489,
    /// <summary>味方チームを後攻とし、指定ターンの開始時に対象のリソースを減らす。</summary>
    PartyLastAndLoseOpeningResource = 490,
    /// <summary>自身のリソースを指定量回復する。</summary>
    RecoverSelfFlat = 491,
    /// <summary>装備中の指定カテゴリーの武器すべてを選択できなくする。</summary>
    PreventEquippedWeaponsSelection = 492,
    /// <summary>次のターン開始時に、元の行動で選択した対象を武器攻撃する。</summary>
    AttackOriginalTargetNextTurn = 493,
    /// <summary>このキャラクターに対する指定状態の付与を防ぐ。</summary>
    EntityStateImmunity = 494,
    /// <summary>別の能力値Bを参照して、指定回数だけ発動ロールをやり直す。</summary>
    RerollWithReplacementStat = 495,
    /// <summary>指定スタック数の段階で、該当カテゴリーの達成値を増減する。</summary>
    StackTierCategoryRoll = 496,
    /// <summary>指定スタック数の段階で、該当カテゴリーのリソース消費を軽減する。</summary>
    StackTierCategoryCost = 497,
    /// <summary>指定スタック数の段階で、指定の属性値を増減する。</summary>
    StackTierAttributeOffset = 498,
    /// <summary>指定スタック数の段階で、該当カテゴリー宣言時のリソースを減らし、指定属性も補正する。</summary>
    StackTierCostAndAttribute = 499,
    /// <summary>指定スタック数とリソース条件を満たすと、ターン開始時に別のリソースを失って指定状態中も行動できる。</summary>
    StackTierActWithEmptyResource = 500,
    /// <summary>指定スタック数の段階で、該当カテゴリーのスキルのクールタイムを増減する。</summary>
    StackTierCooldown = 501,
    /// <summary>指定スタック数の段階で、該当カテゴリーの威力とリソース回復量に倍率を掛ける。</summary>
    StackTierPowerAndHealing = 502,
    /// <summary>指定スタック数の段階で、該当カテゴリー発動時の2種類のリソースを減らす。</summary>
    StackTierTwoResourceCost = 503,
    /// <summary>指定スタック数の段階で、消費不足分を別のリソースで支払って発動できる。</summary>
    StackTierPayAlternateResource = 504,
    /// <summary>指定カテゴリー以外を宣言するとスタックを指定数に戻す。</summary>
    ResetStacksOutsideCategory = 505,
    /// <summary>条件に応じて生産物に指定数のカテゴリーを追加できる。</summary>
    AddOptionalCraftCategory = 506,
    /// <summary>消費した材料の種類数が下限に達すると、生産物に指定カテゴリーを付ける。</summary>
    CraftCategoryByIngredientVariety = 507,
    /// <summary>特定の名前の材料を必要個数以上使うと、生産物に指定カテゴリーを付ける。</summary>
    CraftCategoryByNamedIngredient = 508,
    /// <summary>指定名の材料の個数と他の材料の種類数を満たすと、生産物に指定カテゴリーを付ける。</summary>
    CraftCategoryByItemAndVariety = 509,
    /// <summary>後続の条件に従って生産物のランクを決める。</summary>
    DetermineCraftRank = 510,
    /// <summary>消費した材料の合計ランクが下限以上なら、生産物を指定ランクにする。</summary>
    CraftRankByIngredientTotal = 511,
    /// <summary>指定の出目をフェイタルとし、追加のアイテムを獲得する。</summary>
    FatalRollBonusItem = 512,
    /// <summary>指定属性の威力を含む指定カテゴリーの発動成功を、もう一つの成立条件とする。</summary>
    AlternateElementRollSuccess = 513,
    /// <summary>パーティー全員が戦闘から離脱する。</summary>
    WholePartyExit = 514,
    /// <summary>次の進行ロールまで、該当カテゴリーの効果をパーティー全員で共有する。</summary>
    ShareCategoryUntilProgress = 515,
    /// <summary>改造後の装備を指定の名前に変更する。</summary>
    RenameModifiedEquipment = 516,
    /// <summary>既に施された改造をすべて新しい改造で上書きする。</summary>
    ReplacePriorModifications = 517,
    /// <summary>この武器を取引不可にする。</summary>
    MakeWeaponUntradeable = 518,
    /// <summary>後続の出目表に従って装備部位を変更する。</summary>
    RollEquipmentSlots = 519,
    /// <summary>後続の出目表に従って装備効果を追加する。</summary>
    RollEquipmentEffect = 520,
    /// <summary>後続の結果表で参照するダイスを指定式で振る。</summary>
    RollOutcomeDice = 521,
    /// <summary>指定の出目に対応する武器威力の増減量を定める。</summary>
    RolledWeaponPowerOffset = 522,
    /// <summary>指定の出目で得る装備効果として、この武器を選んだ発動ロールの補正を定める。</summary>
    RolledWeaponCheckOffset = 523,
    /// <summary>指定の出目で得る装備効果として、最大耐久の補正を定める。</summary>
    RolledDurabilityOffset = 524,
    /// <summary>ダイスロールから適用する結果を任意に選ぶ。</summary>
    ChooseDiceOutcome = 525,
    /// <summary>指定カテゴリーの装備を消滅させる。</summary>
    DestroyEquipmentCategory = 526,
    /// <summary>直前の処理が終わった後にこの状態を解除する。</summary>
    ExpireAfterResolution = 527,
    /// <summary>効果を適用した後にこの状態を解除する。</summary>
    ExpireAfterApplication = 528,
    /// <summary>自身が指定カテゴリーを発動したとき、この状態を解除する。</summary>
    ExpireWhenUsingCategory = 529,
    /// <summary>対象を指定ターンの間、指定状態にする。</summary>
    ApplyTimedNamedStates = 530,
    /// <summary>対象に付与されている指定の接近状態を解除する。</summary>
    DisengageAffectedTarget = 531,
    /// <summary>指定式から得た属性威力で対象を攻撃する。</summary>
    AttackElementPowerFormula = 532,
    /// <summary>ロール結果に応じて生産物の個数を増減する。</summary>
    ModifyOutputCount = 533,
    /// <summary>指定の出目に対応する装備の名前と変更後の装備部位を定める。</summary>
    RollEquipmentSlotResult = 534,
    /// <summary>報酬判定用のダイスを振り、出た順番を使う。</summary>
    OrderedRollForRewards = 535,
    /// <summary>連続した出目に指定の並びが含まれる場合に報酬を与える。</summary>
    MatchConsecutiveDice = 536,
    /// <summary>複数の出目の並びが重複して成立した場合、すべての報酬を与える。</summary>
    AwardAllOverlappingMatches = 537,
    /// <summary>該当する並びでスタックを得て、次回の指定条件を満たさなければスタックを規定値に戻す。</summary>
    RollMatchStacksWithNextReset = 538,
    /// <summary>同じ数字が2つ連続した場合にジルダを得る。</summary>
    RepeatSequenceCurrency = 539,
    /// <summary>指定桁数の数字階段が成立すると、ジルダと永続スタックを得る。</summary>
    StraightSequenceCurrencyAndStacks = 540,
    /// <summary>指定桁数の数字階段が成立すると、経験値・決意と指定回数のロールを得る。</summary>
    StraightSequenceRewards = 541,
    /// <summary>同じ数字が1つおきに3回並ぶと指定スタックを得る。</summary>
    AlternatingSequenceStacks = 542,
    /// <summary>指定の並びでスタックをすべて消費し、指定スタック数の積に相当するジルダを得る。</summary>
    ExactSequenceStackProductCurrency = 543,
    /// <summary>指定の並びでジルダ・経験値・決意と指定回数のロールを得る。</summary>
    ExactSequenceRewards = 544,
    /// <summary>指定状態で除外数字以外の3連が成立すると、スタックを消費し、べき乗で決まるジルダと経験値・決意を得る。</summary>
    ConditionalTripleStackCurrency = 545,
    /// <summary>指定スタック数以上なら、ある状態が続く間は別の指定状態にもなる。</summary>
    StateWhileStackThreshold = 546,
    /// <summary>指定スタックを得るたびに報酬ロールを指定回数行う。</summary>
    LootRollOnStackGain = 547,
    /// <summary>指定の数字の並びが成立すると称号を得る。</summary>
    ExactSequenceTitle = 548,
    /// <summary>アクティブ効果を追加で指定回数適用する。</summary>
    RepeatActiveEffect = 549,
    /// <summary>名前付き報酬表の判定に使うダイス式を定める。</summary>
    RollNamedRewardTable = 550,
    /// <summary>報酬表の出目の範囲に獲得アイテム名と個数を対応させる。</summary>
    RewardTableItemRange = 551,
    /// <summary>指定カテゴリーの武器を装備した扱いとし、この状態を武器として選んだときに指定式の武器威力を使う。</summary>
    EquipStateWeaponFormula = 552,
    /// <summary>武器威力と属性Bに係数を掛けた値を足し、その合計に倍率を掛けた属性威力で対象を攻撃する。</summary>
    GroupedElementWeaponAttack = 553,
    /// <summary>指定の二部位に装備中の装備と対象装備を交換する。</summary>
    SwapEquipmentInTwoSlots = 554,
    /// <summary>指定カテゴリーの品名を参照値を含む名前に変更し、指定先に登録する。</summary>
    RenameAndRegisterItem = 555,
    /// <summary>コマンドを実行し、その効果を対象に適用する。</summary>
    ApplyCommandToTarget = 556,
    /// <summary>自身に指定個数の永続スタックを付与する。</summary>
    GainPermanentStacks = 557,
    /// <summary>対象に指定式で行為判定させる。</summary>
    TargetPerformsCheck = 558,
    /// <summary>当日の終了まで、結果の各条件を満たす占い効果を重複して付与する。</summary>
    GrantCumulativeOutcomesUntilDayEnd = 559,
    /// <summary>達成値のしきい値を満たすと、指定ロールの出目を増減する。</summary>
    TierRollDelta = 560,
    /// <summary>達成値がしきい値以上ならクエストで得る報酬を加算する。</summary>
    TierQuestReward = 561,
    /// <summary>達成値がしきい値以上なら指定ロールの回数を加算する。</summary>
    TierAdditionalRoll = 562,
    /// <summary>通常威力と指定属性の威力をそれぞれの式で算出して攻撃する。</summary>
    MixedPowerAttack = 563,
    /// <summary>自身を対象とする選択を無効にする。</summary>
    InvalidateSelection = 564,
    /// <summary>行為判定の出目を指定量変化させて結果を再計算する。</summary>
    ModifyCheckDieAfterRoll = 565,
    /// <summary>対象スキルのクールタイムを指定量減らす。</summary>
    ReduceTargetCooldown = 566,
    /// <summary>指定カテゴリーの敵が行動するときに自身を必ず選択させる。</summary>
    ForceEnemyActionTarget = 567,
    /// <summary>発動した行為のカテゴリーに応じて達成値に倍率を掛ける。</summary>
    ScaleActivatedActionResult = 568,
    /// <summary>発動ロールを指定式に置き換えた追加ロールを任意で行う。</summary>
    OptionalRepeatWithFormula = 569,
    /// <summary>命中した指定カテゴリーの攻撃の威力に倍率を掛けて対象を攻撃する。</summary>
    AttackFromHitPower = 570,
    /// <summary>指定カテゴリーの状態の付与ターン数を増減する。</summary>
    ModifyAppliedCategoryTurns = 571,
    /// <summary>対象になった指定カテゴリーの行為の威力に倍率を掛ける。</summary>
    ScaleDeclaredActionPower = 572,
    /// <summary>直前に対象へ行った状態付与をもう一度適用する。</summary>
    ReapplyStateToTarget = 573,
    /// <summary>対象のリソースを指定値にして状態を付与する。</summary>
    SetResourceAndApplyState = 574,
    /// <summary>付与者が自身を対象に指定カテゴリーの行為を宣言すれば発動ロールを成功扱いにする。</summary>
    ApplierAlwaysSucceedsOnCategory = 575,
    /// <summary>指定カテゴリーの効果と指定状態の解除を受け付けない。</summary>
    DenyEffectAndStateRemoval = 576,
    /// <summary>自身が召喚するキャラクターの指定能力値に倍率を掛ける。</summary>
    ScaleSummonedCharacterStat = 577,
    /// <summary>サイズ別のアイテム所持上限を指定量増減する。</summary>
    IncreaseItemCapacityBySize = 578,
    /// <summary>ファンブルに伴う効果を無効にする。</summary>
    CancelFumbleEffect = 579,
    /// <summary>判定結果が季節ロールを上回ると、そのロールの効果を任意で無視できる。</summary>
    IgnoreSeasonOnHigherRoll = 580,
    /// <summary>対象となるアクティブ攻撃のスキル値を置き換える。</summary>
    SetSkillValue = 581,
    /// <summary>条件に合う行動の威力に倍率を掛ける。</summary>
    MultiplyPower = 582,
    /// <summary>条件に合う回避の達成値に補正値を加える。</summary>
    AddEvasionResult = 583,
    /// <summary>特定リソースの消費量を指定値に置き換える。</summary>
    SetResourceCost = 584,
    /// <summary>スキルのクールタイムを指定値に置き換える。</summary>
    SetCooldown = 585,
    /// <summary>指定リソースの消費量を増減する。自動適用か任意適用かも表す。</summary>
    AddResourceCost = 586,
    /// <summary>行為判定や発動ロールの目標値を増減する。</summary>
    AddTargetValue = 587,
    /// <summary>該当する攻撃のクリティカルを確定する。</summary>
    SetCritical = 588,
    /// <summary>選択した自身の状態の持続ターンを増減する。</summary>
    AddStateDuration = 589,

    /// <summary>対象の被ダメージに倍率を掛ける。必要に応じて倍率の上限を設ける。</summary>
    MultiplyDamageTaken = 590, // actor, multiplier, optional maximum multiplier
    /// <summary>この攻撃に対するカウンターで自身がダメージを受けることを防ぐ。</summary>
    PreventCounterDamage = 591,
    /// <summary>条件に合う行為判定の達成値に補正値を加える。</summary>
    AddActionResult = 592,
    /// <summary>カウンターによる被ダメージの軽減値を置き換える。</summary>
    SetDamageReduction = 593,

    // Component/rule names live in Parameters to avoid one enum value per
    // element, response type, or future attack component.
    /// <summary>攻撃の威力または属性威力を指定式に置き換える。</summary>
    SetAttackComponent = 594, // Power|AttributePower, replacement formula
    /// <summary>攻撃の威力または属性威力に倍率を掛ける。</summary>
    MultiplyAttackComponent = 595,
    /// <summary>防御点の扱いや宣言可能な応答など、この攻撃の規則を変更する。</summary>
    SetAttackRule = 596,
    /// <summary>条件に合う行為判定の達成値に倍率を掛ける。</summary>
    MultiplyActionResult = 597,

    // actor, stat name, Add|Multiply, value.
    /// <summary>指定した人物のステータスに加算または乗算の補正を適用する。</summary>
    ModifyStat = 598,
    // actor, resource, factor. The action selector belongs to Triggers.
    /// <summary>対象の行動で消費する指定リソースに倍率を掛ける。</summary>
    MultiplyResourceCost = 599,
    // actor, resource, mapped-stack rule key, interval, reduction, selection policy.
    /// <summary>対応する属性のスタックが一定数増すごとに、行動のリソース消費を減らす。</summary>
    ReduceResourceCostPerMappedStacks = 600,
    // Base modifier kind, selector fields, replacement value.
    /// <summary>スパイク段階に応じて、指定した既存の補正項目の値を置き換える。</summary>
    SetModifier = 601,
    // Replacement formula for a skill activation roll. The base target is retained.
    /// <summary>発動ロールの計算式を置き換える。目標値は引き継ぐ。</summary>
    SetActivationRollFormula = 602,
    /// <summary>対象に与える指定リソースへのダメージに倍率を掛ける。</summary>
    MultiplyResourceDamage = 603, // actor, resource, multiplier, Optional
    /// <summary>指定状態の付与スタック数を置き換える。</summary>
    SetStackAmount = 604, // actor, state, amount
    /// <summary>現在のスタック数に対応する段階の倍率を行動の威力に掛ける。</summary>
    MultiplyPowerByStacks = 605, // actor, action category, multipliers for stacks 1..N
    /// <summary>このターン中、指定した武器威力を参照する行為の発動ロールの達成値に補正を加える。</summary>
    AddTimedActionResult = 606, // Self, CurrentTurn, WeaponPower, weapon category, action category, value
    /// <summary>対象スキルやクラスの習得に必要なリソースを加算または乗算で補正する。</summary>
    ModifyAcquisitionCost = 607, // Self, resource, Add|Multiply, AllSkills|Class|SkillCategory, selector, value
    /// <summary>指定カテゴリーを除くアイテムのNPCへの売却価格に倍率を掛ける。</summary>
    MultiplySalePrice = 608, // NPC, ExceptCategory, category, factor
    /// <summary>条件に合う行動による回復量に倍率を掛ける。</summary>
    MultiplyRecovery = 609, // factor; action category belongs to Triggers
    /// <summary>このスキルの発動ロールの目標値を置き換える。</summary>
    SetActivationRollTarget = 610, // replacement target for this skill's activation roll
    /// <summary>クリティカルまたはファンブルとなる出目の範囲を両端を含めて指定する。</summary>
    SetRollRange = 611, // Action, Fumble|Critical, lower inclusive, upper inclusive
    /// <summary>カウンター効果で軽減するダメージ量に倍率を掛ける。</summary>
    MultiplyDamageReduction = 612, // factor applied to the counter's reduction amount
    /// <summary>ターン内の発動順に応じて行動の威力に段階別の倍率を掛ける。</summary>
    MultiplyPowerByTurnActivations = 613, // action category, first ordinal, factors..., maximum (reset each turn)
    /// <summary>指定状態による毎ターンのリソース回復量を、スパイクの値に置き換える。</summary>
    StateTurnRecovery = 614, // state name, resource, amount, AnyTurn (spike replacement, not cumulative)
    /// <summary>属性威力による被ダメージに、その属性に不利な属性Bの最大値を掛ける。</summary>
    AttributeWeaknessDamage = 615, // Self, HighestDisadvantageAttributeBonus
    /// <summary>この効果をカウンター効果の対象にできなくする。</summary>
    PreventCounterTarget = 616, // ThisEffect
    /// <summary>指定状態によって対象の行動を指示できるターン内の回数を変更する。</summary>
    SetStateControlLimit = 617, // state, Turn, limit
    /// <summary>自身・対象の状態と接近条件を満たすとき、該当カテゴリーの威力に倍率を掛ける。</summary>
    ConditionalStatePower = 618, // action category, own state, target state, shared melee state, multiplier
    /// <summary>自身・対象の状態と接近条件を満たすとき、規定量以上のリソース消費を軽減する。</summary>
    ConditionalStateCost = 619, // same first four selectors, resource, minimum base cost, delta
    /// <summary>指定状態と接近条件による既存の威力倍率を置き換える。</summary>
    SetConditionalStatePower = 620, // replacement multiplier for ConditionalStatePower
    /// <summary>指定カテゴリーの条件を満たす行動で防御点を無視する。</summary>
    IgnoreDefenseForCategories = 621, // Self, categories... (all)
    /// <summary>状態による攻撃の威力に、状態付与時に決まる倍率を掛ける。</summary>
    MultiplyStateAttackPower = 622, // local state name, factor (captured when applying the state)
    /// <summary>対象に付与する指定状態の持続ターン数を置き換える。</summary>
    SetAppliedStateDuration = 623, // Target, state, replacement turns
    /// <summary>指定状態が自身の被ダメージに掛ける倍率を置き換える。</summary>
    SetStateDamageMultiplier = 624, // local state, Self, replacement damage multiplier
    /// <summary>状態付与時のリソース回復量を決める最大値割合を置き換える。</summary>
    SetOnAppliedStateRecovery = 625, // resource, replacement maximum percent
    /// <summary>指定カテゴリーのクリティカル時に威力または達成値に倍率を掛ける。</summary>
    CriticalCategoryMultiplier = 626, // action category, Power|ActionResult, factor, Stack
    /// <summary>アイテム効果の持続ターンを補正する。他の対象への使用にも適用する。</summary>
    ItemEffectDuration = 627, // Self, AllTargets, added turns
    /// <summary>指定サイズのアイテムを所持できる上限数を変更する。</summary>
    ItemCapacity = 628, // size, maximum count
    /// <summary>エリア進行を任意に減らせる量を、エリアカテゴリーに応じて指定する。</summary>
    OptionalAreaProgressReduction = 629, // default reduction, area category, replacement reduction, Optional
    /// <summary>選択して宣言するスキルの威力に倍率を掛ける。</summary>
    MultiplyDeclaredSkillPower = 630, // factor for the selected skill declaration
    /// <summary>指定カテゴリーの威力に、スタックに応じた上限付きの倍率を掛ける。</summary>
    CappedStackPower = 631, // action category, stack multiplier formula, maximum multiplier
    /// <summary>指定カテゴリーのエリア内で、対象の能力値に倍率を掛ける。</summary>
    AreaStatMultiplier = 632, // present area category, stat, multiplier
    /// <summary>指定カテゴリーのエリア内で、特定能力値による行為判定の達成値を増減する。</summary>
    AreaActionResult = 633, // present area category, check stat, added result
    /// <summary>2カテゴリーを併せ持つ行動に、算出時点の付与者の能力値を参照した属性威力を加える。</summary>
    ApplierAttributePower = 634, // action category A AND B, added attribute power (current Applier stats)
    /// <summary>該当種族のキャラクターに対する指定能力値の行為判定で、達成値を増減する。</summary>
    RaceCheckResult = 635, // target race categories (comma separated, OR), check stat, added result
    /// <summary>自属性の種族属性ボーナスに指定割合を加える。</summary>
    OwnAttributeBonus = 636, // added race attribute bonus percentage points
    /// <summary>弱点となる属性威力から受けるダメージに倍率を掛ける。</summary>
    OwnAttributeWeaknessMultiplier = 637, // incoming weak attribute power multiplier formula
    /// <summary>名前で指定したスキルの発動ロールの達成値を増減する。</summary>
    NamedSkillRollResult = 638, // skill name, added activation roll result
    /// <summary>次の指定回数だけ、該当カテゴリーの行動の威力に倍率を掛ける。</summary>
    NextActionPower = 639, // matching uses, action category, multiplier
    /// <summary>自身を含む味方が対象に付与した状態の数に応じて、指定カテゴリーの威力に倍率を掛ける。</summary>
    PowerByAlliedStateTypes = 640, // action category, multiplier formula; distinct Target states applied by Self/allies
    /// <summary>このスキルの発動ロールでクリティカルになる出目の範囲を指定する。</summary>
    ThisSkillCriticalRange = 641, // inclusive lower and upper activation dice bounds
    /// <summary>このスキルのクリティカル目を指定量だけずらす。</summary>
    CriticalThresholdDelta = 642, // signed delta to this skill's critical threshold
    /// <summary>指定カテゴリーのアイテムを使う際のリソース消費を増減する。</summary>
    ItemUseCostDelta = 643, // item category, resource, signed delta
    /// <summary>指定した2つのフェーズで自身の被ダメージに倍率を掛ける。</summary>
    PhaseDamageMultiplier = 644, // phase A OR B, incoming damage multiplier
    /// <summary>攻撃前から対象が指定状態の場合に、この攻撃の威力に倍率を掛ける。</summary>
    PowerAgainstState = 645, // target state before this attack, multiplier (this skill only)
    /// <summary>攻撃前から対象が指定状態の場合に、指定カテゴリーの武器攻撃の威力に倍率を掛ける。</summary>
    WeaponPowerAgainstState = 646, // weapon category, target state before this attack, multiplier
    /// <summary>該当カテゴリーの行動による被ダメージに倍率を掛ける。</summary>
    IncomingCategoriesDamageMultiplier = 647, // incoming categories (comma separated, OR), multiplier
    /// <summary>自身が装備できるスキルの個数に上限を設ける。</summary>
    EquippedSkillLimit = 648, // maximum equipped skill count formula (not acquired skill count)
    /// <summary>自身のスタック数に応じて、このスキルのリソース消費を最低消費量まで減らす。</summary>
    ThisSkillStackCost = 649, // own stack state, resource, reduction per stack, minimum cost
    /// <summary>自身のスタック数が指定値以上なら、このスキルの威力に倍率を掛ける。</summary>
    ThisSkillStackPower = 650, // own stack state, minimum stacks, multiplier; before stack removal
    /// <summary>戦闘中の指定回数まで、2種類のリソースを任意で消費して自身に状態を付与できる。</summary>
    OptionalSpikeState = 651, // resource A, resource B, cost each, granted own state, battle use limit; unlocked by spike stage
    /// <summary>自身のすべての能力値Bに倍率を掛ける。</summary>
    AllBonusStatsMultiplier = 652, // all ability bonuses B, multiplier (not base ability values)
    /// <summary>この攻撃に対する被ダメージの軽減割合を指定式の倍率で補正する。</summary>
    SetIncomingReductionRatio = 653,
    /// <summary>この攻撃の威力算出ではキャラクターに付与された状態の影響を除外する。</summary>
    IgnoreCharacterStatesForPower = 654,
    /// <summary>指定カテゴリーの発動ロールの達成値に倍率を掛ける。</summary>
    MultiplyCategoryActivationResult = 655,
    /// <summary>指定カテゴリーによる判定の達成値に倍率を掛ける。</summary>
    MultiplyCategoryCheckResult = 656,
    /// <summary>指定カテゴリーの習得に要する決意を固定値にする。</summary>
    SetCategoryAcquisitionCost = 657,
    /// <summary>指定ターンの間に同カテゴリーの宣言が規定回数に達すると、以後のリソース消費を増減する。</summary>
    RepeatedCategoryTurnCost = 658,
    /// <summary>指定種族から受けるダメージに追加の倍率を掛ける。</summary>
    ReduceIncomingFromRace = 659,
    /// <summary>指定フェーズで自身の行為判定の達成値に倍率を掛ける。</summary>
    PhaseCheckMultiplier = 660,
    /// <summary>この攻撃で、対象の防御点を指定点数だけ無視する。</summary>
    IgnoreAmountOfDefense = 661,
    /// <summary>指定カテゴリーを必要数装備していれば、戦闘開始時のリソースを増減する。</summary>
    EquippedBattleStartingResource = 662,
    /// <summary>防御点を無視する値が対象の防御点を上回った分をスキル値に加える。</summary>
    AddExcessDefensePenetration = 663,
    /// <summary>自身と対象が同じ指定状態なら威力に倍率を掛ける。</summary>
    SharedMeleeStatePower = 664,
    /// <summary>この攻撃の威力に指定式の結果を倍率として掛ける。</summary>
    AttackPowerFormulaFactor = 665,
    /// <summary>対象が指定状態のとき威力に倍率を掛ける。</summary>
    StateTargetPower = 666,
    /// <summary>自身が指定名のスキルで得る達成値に倍率を掛ける。</summary>
    NamedSkillResultMultiplier = 667,
    /// <summary>スパイク段階に応じてロールプレイ効果の説明を置き換える。</summary>
    SetRoleplayDescription = 668,
    /// <summary>自身が指定状態のとき、指定カテゴリーのスキルによる達成値を増減する。</summary>
    OwnStateCategoryResult = 669,
    /// <summary>指定カテゴリーへの防御行動の発動に失敗した場合、被ダメージに倍率を掛ける。</summary>
    FailedDefenseIncomingMultiplier = 670,
    /// <summary>指定した種族能力値ボーナスに割合を加算する。</summary>
    RacialAbilityPercent = 671,
    /// <summary>指定した属性の種族属性ボーナスに割合を加算する。</summary>
    RacialElementPercent = 672,
    /// <summary>指定カテゴリーの発動ロールの目標値を増減する。</summary>
    CategoryActivationTargetDelta = 673,
    /// <summary>指定カテゴリーによる被ダメージを指定量軽減する。</summary>
    CategoryIncomingReduction = 674,
    /// <summary>自身への指定リソースダメージを一定量軽減する。</summary>
    ResourceIncomingReduction = 675,
    /// <summary>リソースが指定値を下回ったときに重症ロールを行うようにする。</summary>
    InjuryRollThreshold = 676,
    /// <summary>指定部位が空いているとき、スタック数と係数の積を防御点に加える。</summary>
    EmptySlotDefensePerStack = 677,
    /// <summary>指定の発生元が与えるダメージを、指定属性・威力の攻撃に置き換える。</summary>
    ReplaceSourceDamageWithElement = 678,
    /// <summary>自身が対象になった指定スキルの目標値を増減する。</summary>
    IncomingSkillTargetDelta = 679,
    /// <summary>指定カテゴリーの行動によって自身が受けるダメージに倍率を掛ける。</summary>
    CategoryDamageTakenFactor = 680,
    /// <summary>2つの指定カテゴリーを併せ持つ行動の威力に倍率を掛ける。</summary>
    CombinedCategoryPower = 681,
    /// <summary>指定カテゴリーに上位段階のディスティニースパイクを適用する。</summary>
    CategorySpikeRankIncrease = 682,
    /// <summary>指定した能力値Bを固定値にする。</summary>
    SetFixedBonusStat = 683,
    /// <summary>所持する指定2カテゴリーの合計個数をスタック数の上限とする。</summary>
    StackCapFromPossessedItems = 684,
    /// <summary>指定アイテムの消費個数に応じて、指定カテゴリーのリソース消費を減らす。</summary>
    ReduceCostPerSpentItem = 685,
    /// <summary>カウンター効果の発動ロールの目標値を増減する。</summary>
    CounterActivationTargetDelta = 686,
    /// <summary>指定2カテゴリーを併せ持つ行動に属性威力を加える。</summary>
    TwoCategoryAttributePower = 687,
    /// <summary>2つの能力値を、別の能力値と係数の積で置き換える。</summary>
    ReplaceTwoStatsFromThird = 688,
    /// <summary>指定能力値による行為判定の目標値を増減する。</summary>
    CheckTargetDeltaByStat = 689,
    /// <summary>自身に対する指定属性の威力を含む指定カテゴリーの達成値に倍率を掛けて扱う。</summary>
    IncomingElementCategoryResult = 690,
    /// <summary>自身が与える威力に倍率を掛ける。</summary>
    OutgoingPowerFactor = 691,
    /// <summary>ターンごとの上限回数まで、指定した2スキルのリソース消費を固定値にする。</summary>
    PerTurnNamedSkillCostReplacement = 692,
    /// <summary>指定カテゴリーによる被ダメージに倍率を掛ける。</summary>
    CategoryIncomingDamage = 693,
    /// <summary>指定カテゴリーに対するカウンターの達成値に倍率を掛ける。</summary>
    CounterResultByCategory = 694,
    /// <summary>指定カテゴリーの目標値に倍率を掛ける。</summary>
    CategoryTargetFactor = 695,
    /// <summary>指定した2カテゴリーの目標値に倍率を掛ける。</summary>
    TwoCategoryTargetFactor = 696,
    /// <summary>自身に付与される指定状態のスタック数に倍率を掛ける。</summary>
    IncomingStackCountMultiplier = 697,
    /// <summary>指定属性による被ダメージに、指定の能力値を倍率として掛ける。</summary>
    SingleElementDamageFromStat = 698,
    /// <summary>指定した2属性のいずれかによる被ダメージに、指定の能力値を倍率として掛ける。</summary>
    EitherElementDamageFromStat = 699,
    /// <summary>指定カテゴリーに対する防御行動の発動に失敗した場合、与ダメージに倍率を掛ける。</summary>
    FailedDefenseOutgoingDamage = 700,
    /// <summary>自身が使った持続型回復アイテムの効果ターンを、使用対象を問わず増減する。</summary>
    RecoveryItemDuration = 701,
    /// <summary>指定した3能力値による判定の達成値に倍率を掛ける。</summary>
    SeveralStatChecksFactor = 702,
    /// <summary>指定した3カテゴリーの達成値に補正を加える。</summary>
    CategoryListCheckAddition = 703,
    /// <summary>状態の効果として自身の指定能力値に補正を加える。</summary>
    StateBonusAddition = 704,
    /// <summary>状態の効果として自身の指定能力値に倍率を掛ける。</summary>
    StateStatFactor = 705,
    /// <summary>状態の効果として自身の指定能力値を所定の値として扱う。</summary>
    FixedStatValue = 706,
    /// <summary>このターン中の被ダメージに倍率を掛ける。</summary>
    IncomingDamageThisTurn = 707,
    /// <summary>対象が指定状態のとき、付与するスタック数に倍率を掛ける。</summary>
    TargetStateStackFactor = 708,
    /// <summary>指定した2カテゴリーによる武器威力に倍率を掛ける。</summary>
    CategoryPairWeaponPower = 709,
    /// <summary>自身のカウンター効果の発動ロールの目標値に倍率を掛ける。</summary>
    CounterTargetFactor = 710,
    /// <summary>自身のカウンター効果で消費するリソース量を増減する。</summary>
    CounterResourceCostDelta = 711,
    /// <summary>味方全員の防御点に固定値を加える。</summary>
    AlliesFlatDefense = 712,
    /// <summary>自身が指定カテゴリーを習得するための決意消費を軽減する。</summary>
    CategoryLearnCostReduction = 713,
    /// <summary>該当カテゴリーのクラススキルの習得に必要な決意に倍率を掛ける。</summary>
    ClassCategoryLearnCostFactor = 714,
    /// <summary>相手が参照する達成値を、複数の発動ロールの合計に係数を掛けた値にする。</summary>
    OpposedResultFromRollTotal = 715,
    /// <summary>指定部位が空いているとき、獲得するスタック数を置き換える。</summary>
    ReplaceStacksIfSlotEmpty = 716,
    /// <summary>次の指定回数だけ、両カテゴリーに合う行動の威力と消費リソースに個別の倍率を掛ける。</summary>
    NextDoubleCategoryPowerCost = 717,
    /// <summary>必要な状態を持ち、除外対象の状態を持たないとき、行動の達成値と能力値による目標値を補正する。</summary>
    MountedRangedRollBonuses = 718,
    /// <summary>カウンター効果で指定カテゴリーの威力に倍率を掛ける。</summary>
    CounterCategoryPower = 719,
    /// <summary>この攻撃を指定カテゴリーの応答の選択対象から除く。</summary>
    ProhibitResponseTarget = 720,
    /// <summary>スタンバイフェーズで自身の行為判定の達成値に倍率を掛ける。</summary>
    StandbySelfCheckFactor = 721,
    /// <summary>指定の出目をアメイジングとし、達成値と指定カテゴリーの威力に倍率を掛ける。</summary>
    AmazingRollBonus = 722,
    /// <summary>対象の指定カテゴリーの発動ロールの達成値を増減する。</summary>
    TriggerCategoryResultDelta = 723,
    /// <summary>自身への指定カテゴリーに対するカウンター宣言時の達成値に倍率を掛ける。</summary>
    CounterToIncomingCategoryFactor = 724,
    /// <summary>ドロップロールの達成値に倍率を掛ける。</summary>
    DropRollFactor = 725,
    /// <summary>指定したいずれかの武器を選んで対象カテゴリーを発動するとき、被ダメージに倍率を掛ける。</summary>
    IncomingWithSelectedWeapons = 726,
    /// <summary>指定したいずれかの武器を選ぶ対象カテゴリーの行動で、目標値を増やし威力に倍率を掛ける。</summary>
    SelectedWeaponsTargetAndPower = 727,
    /// <summary>指定カテゴリー以外のすべての行為判定で達成値を増減する。</summary>
    CheckDeltaExceptCategory = 728,
    /// <summary>この攻撃に指定カテゴリーで応答する際、追加の発動ロールでの成功を要求する。</summary>
    RequireAdditionalSuccessfulResponses = 729,
    /// <summary>対象の達成値に倍率を掛ける。</summary>
    TargetResultFactor = 730,
    /// <summary>効果を宣言して消費するリソース量に倍率を掛ける。</summary>
    DeclarationResourceCostFactor = 731,
    /// <summary>対象のHPが最大値の半分以下なら威力に倍率を掛ける。</summary>
    PowerAtHalfTargetHP = 732,
    /// <summary>キャラクター以外に対する威力に倍率を掛ける。</summary>
    NonCharacterPowerFactor = 733,
    /// <summary>自身に対する判定が指定能力値を使う場合、対象の達成値に倍率を掛けた値を任意で参照できる。</summary>
    OptionalIncomingStatResultFactor = 734,
    /// <summary>指定したいずれかの発生元から該当カテゴリーの行動を受けると、被ダメージを一定量減らす。</summary>
    TwoSourcesIncomingReduction = 735,
    /// <summary>指定属性の威力から受けるダメージに倍率を掛ける。</summary>
    ElementDamageTakenFactor = 736,
    /// <summary>指定状態の付与スタック数を指定値に置き換える。</summary>
    SetGrantedStackCount = 737,
    /// <summary>指定能力値の上昇量を置き換える。</summary>
    ReplaceStatBonusIncrease = 738,
    /// <summary>スタンバイフェーズで対象の行為判定の達成値に倍率を掛ける。</summary>
    StandbyTargetCheckFactor = 739,
    /// <summary>式で求めた値を威力に加える。</summary>
    AddPowerFormula = 740,
    /// <summary>自身が指定状態でない場合に達成値を増減する。</summary>
    ResultWhenLackingState = 742,
    /// <summary>指示された行動の発動ロールの達成値を増減する。</summary>
    OrderedActionResultDelta = 743,
    /// <summary>そのスパイク段階で変更される効果がないことを明示する。</summary>
    NoSpikeChange = 745,
    /// <summary>種族能力値ボーナスの指定された順番の項目に割合を加える。</summary>
    IncreaseRacialBonusByPosition = 746,
    /// <summary>このスパイク段階で指定名のスキルを習得する。</summary>
    LearnNamedSkillAtRank = 747,
    /// <summary>パッシブ効果で追加習得できる種族スキル数を置き換える。</summary>
    SetAdditionalRacialSkillCount = 748,
    /// <summary>パッシブ効果で味方の防御点に加える値を置き換える。</summary>
    SetPartyDefenseBonus = 749,
    /// <summary>アクティブ効果で与える指定リソースダメージの値を置き換える。</summary>
    SetResourceDamageAtRank = 750,
    /// <summary>この発動ロールの達成値に倍率を掛ける。</summary>
    SetActivationResultFactor = 751,
    /// <summary>重症ロールを行う基準となるリソースの下限値を置き換える。</summary>
    SetInjuryResourceThreshold = 752,
    /// <summary>指定状態による威力上昇の倍率を置き換える。</summary>
    SetStatePowerIncrease = 753,
    /// <summary>指定カテゴリーによる武器威力を別の式に置き換える。</summary>
    SetWeaponCategoryPower = 754,
    /// <summary>このスキルが消費する2種類のリソースの量を置き換える。</summary>
    SetTwoResourceCosts = 755,
    /// <summary>指定名の召喚体などの発動ロールを置き換える。</summary>
    SetEntityActivationRoll = 756,
    /// <summary>アクティブ効果による指定リソースの回復量を置き換える。</summary>
    SetActiveResourceRecovery = 757,
    /// <summary>指定状態から与えるダメージを置き換える。</summary>
    SetNamedStateDamage = 758,
    /// <summary>パッシブ効果で無視できる防御点の値を置き換える。</summary>
    SetDefenseIgnoredAtRank = 760,
    /// <summary>自身の指定リソースの最大値に倍率を掛ける。</summary>
    MaxResourceMultiplier = 762,
    /// <summary>指定名のスキルから受けるダメージに倍率を掛ける。</summary>
    IncomingNamedSkillFactor = 763,
    /// <summary>指定カテゴリーで与えるダメージに倍率を掛ける。</summary>
    OutgoingCategoryDamage = 764,
    /// <summary>同じ接近グループの接近状態の人物と、指定カテゴリーから受けるダメージに倍率を掛ける。</summary>
    MeleeOrCategoryIncomingDamage = 765,
    /// <summary>指定カテゴリーの発動ロールの目標値に倍率を掛ける。</summary>
    ActionCategoryActivationTargetFactor = 766,
    /// <summary>付与者の指定カテゴリーの行動から受けるダメージに倍率を掛ける。</summary>
    IncomingFromApplier = 767,
    /// <summary>自身と同じ接近グループの対象に対する該当カテゴリーの威力に倍率を掛ける。</summary>
    SameMeleeCategoryPower = 768,
    /// <summary>戦闘開始から指定ターン目に、該当カテゴリーの達成値と威力にそれぞれ倍率を掛ける。</summary>
    BattleOpeningActionAndPower = 769,
    /// <summary>戦闘終了まで自身の被ダメージに倍率を掛ける。</summary>
    BattleUntilEndIncomingFactor = 770,
    /// <summary>自身のターンにスタック数に応じてリソースを失い、スキルの2種の消費リソースに倍率を掛ける。</summary>
    StackCostAndTurnLoss = 771,
    /// <summary>スタックが2つの最大値から求めた閾値を超えると、指定カテゴリーの宣言を禁止する。</summary>
    HPThresholdBlocksAction = 772,
    /// <summary>スタックが2つのリソース最大値から求めた閾値を超えると、すべての宣言を禁止する。</summary>
    CombinedResourceThresholdBlock = 773,
    /// <summary>指定カテゴリーから受ける特定リソースのダメージに倍率を掛ける。</summary>
    SelectedDefenseResourceFactor = 774,
    /// <summary>指定名のスキルの威力を指定倍率で引き上げる。</summary>
    StackScaleNamedSkill = 775,
    /// <summary>指定能力値による達成値に倍率を掛ける。</summary>
    CheckByAbilityFactor = 776,
    /// <summary>指定カテゴリーによる達成値に倍率を掛ける。</summary>
    CheckByCategoryFactor = 777,
    /// <summary>指定環境で能力値に倍率を掛け、指定能力値による判定の達成値も増減する。</summary>
    BonusesInNamedEnvironment = 778,
    /// <summary>指定の2カテゴリーを受けたとき、被ダメージに倍率を掛ける。</summary>
    TakeCategoryPairDamage = 779,
    /// <summary>指定カテゴリーの発動ロールによる達成値に倍率を掛ける。</summary>
    RollByCategoryActivation = 780,
    /// <summary>自身の指定能力値に補正値を加える。</summary>
    StatFlatBonus = 781,
    /// <summary>味方が規定人数以上なら、戦闘終了時に味方全員の4種類のリソースを最大値の指定割合だけ回復する。</summary>
    PartyBattleEndResourceRecovery = 782,
    /// <summary>指定したいずれかの武器で2カテゴリーに該当する行動を発動すると、対象の防御点を指定点数だけ無視する。</summary>
    TwoWeaponsArmorPiercing = 783,
    /// <summary>指定した2カテゴリーの装備を併用すると、自身のリソース最大値に固定値を加える。</summary>
    ArmorSetMaxResource = 784,
    /// <summary>指定武器を選択したスキルで、該当種族への指定カテゴリーの威力に倍率を掛ける。</summary>
    EffectAgainstRaceByWeapon = 785,
    /// <summary>採取・特殊行動・進行ロール・ダンジョン攻略報酬で指定の出目をアメイジングとし、複数回の効果を得る。</summary>
    ExtraRewardsOnAmazing = 786,
    /// <summary>対象の2種類のリソースをそれぞれの最大値の指定割合だけ回復する。</summary>
    HealMaxResourcePair = 788,
    /// <summary>アクティブ効果によるアイテムの獲得個数を増減する。</summary>
    CraftQuantityRankBonus = 789,
    /// <summary>指定期間内に該当カテゴリーを繰り返し発動すると、発動回数に従って威力倍率が上限まで段階的に上がる。</summary>
    RepeatCategoryPowerProgression = 790,
    /// <summary>パッシブ効果の威力上昇が始まる発動回数と、段階別の倍率を置き換える。</summary>
    SetRepeatPowerProgression = 791,
    /// <summary>スタックに応じて自身のリソース最大値に段階別の減少倍率を適用する。</summary>
    StackMaxResourceProgression = 792,
    /// <summary>スタックに応じて該当カテゴリーの威力に段階別の上昇倍率を適用する。</summary>
    StackPowerProgression = 793,
    /// <summary>指定部位が空いているときのスタック獲得量を指定式に置き換える。</summary>
    EmptySlotStackFormula = 794,
    /// <summary>指定スタック数以上を残して戦闘を終えたときの、状態による任意のリソース回復を設定する。</summary>
    SetStateBattleEndStackRecovery = 795,
    /// <summary>自身の防御点に係数を掛けた分だけ、指定の防御点を得る。</summary>
    DefenseFromDefenseFactor = 797,
    /// <summary>指定したいずれかの武器を選んで該当カテゴリーを発動するとき、威力に倍率を掛ける。</summary>
    EitherSelectedWeaponCategoryPower = 798,
    /// <summary>エンカウントフェーズに行う指定ロールの達成値に倍率を掛ける。</summary>
    EncounterStatRollFactor = 799,
    /// <summary>対象の指定リソースの減少量を置き換える。</summary>
    ReplaceResourceLoss = 800,
    /// <summary>指定カテゴリーの消費リソースを変更し、代わりに獲得するスタック量に倍率を掛ける。</summary>
    SetItemCostAndStackGain = 801,
    /// <summary>変数xと指定係数による式で、このダメージに対する軽減倍率を補正する。</summary>
    ScaleDamageReductionByVariable = 802,
    /// <summary>指定カテゴリーで消費するリソースを一定量減らす。</summary>
    ReducedCategoryCost = 803,
    /// <summary>このスキルの発動ロールの達成値を増減する。</summary>
    SetActivationRollBonus = 804,
    /// <summary>指定状態によるリソース減少の値を置き換える。</summary>
    SetStateResourceLossRatio = 805,
    /// <summary>次の指定回数だけ、対象が指定能力値を参照するときの達成値を補正する。</summary>
    NextStatResultPenalty = 806,
    /// <summary>アクティブ効果のダメージと、2種類のリソースを減らす割合を置き換える。</summary>
    SetDamageAndResourcesPercent = 807,
    /// <summary>指定カテゴリーの行動で消費するリソース量を増減する。</summary>
    CategoryCostOffset = 808,
    /// <summary>指定カテゴリーによる自傷ダメージに倍率を掛ける。</summary>
    CategorySelfDamageScale = 809,
    /// <summary>指定した状態の付与スタック数を置き換える。</summary>
    ReplaceStackQuantity = 810,
    /// <summary>付与するスタック数に倍率を掛ける。</summary>
    StackGrantMultiplier = 811,
    /// <summary>自身が指定カテゴリーの対象になるとき、発動者の目標値を増減する。</summary>
    IncomingCategoryTargetRollOffset = 812,
    /// <summary>指定名のスキルの習得条件を満たした扱いにする。</summary>
    QualifyNamedSkill = 813,
    /// <summary>自身の指定属性Bに倍率を掛ける。</summary>
    ElementBonusScale = 814,
    /// <summary>対象がメインフェーズ開始時に指定上限まで先に行動を宣言できるようにする。</summary>
    AdvanceActionWithCap = 815,
    /// <summary>味方から付与されたバフの効果値に倍率を掛ける。</summary>
    AllyBuffFactor = 816,
    /// <summary>指定リソースの消費量を固定値にする。</summary>
    SetResourceCostFixed = 817,
    /// <summary>対象の目標値を指定量だけ増減する。</summary>
    TargetThresholdOffset = 818,
    /// <summary>この攻撃へのカウンターは発動ロールを複数回行い、最も低い達成値を使う。</summary>
    CounterRollUseLowest = 819,
    /// <summary>指定カテゴリーによる状態のスタック付与量とリソース回復量に倍率を掛ける。</summary>
    CategoryStackAndRecoveryScale = 820,
    /// <summary>指定カテゴリーを含むエリアで目標値に倍率を掛ける。</summary>
    AreaTargetMultiplier = 821,
    /// <summary>指定状態による遅延攻撃のスキル値を置き換える。</summary>
    SetDelayedStateSkillValue = 822,
    /// <summary>指定した能力値による行為判定の達成値に倍率を掛ける。対象能力値を順に指定し、末尾に倍率を置く。</summary>
    StatRollFactorForAction = 823,
    /// <summary>パッシブ効果で指定能力値に加える上昇量を置き換える。</summary>
    ReplacePassiveStatBonus = 824,
    /// <summary>自身の指定カテゴリーによる達成値に倍率を掛ける。</summary>
    CategoryAchievementScale = 825,
    // action category, result factor, power factor
    /// <summary>この武器を選んで行う指定カテゴリーの行為の達成値と威力に、それぞれ倍率を掛ける。</summary>
    ModifySelectedWeaponAction = 827,
    // actor, action category, factor
    /// <summary>指定カテゴリーの行為によって対象が受けるダメージに倍率を掛ける。</summary>
    MultiplyIncomingDamage = 828,
    // actor, encounter phase, item category
    /// <summary>指定エンカウントフェーズに指定カテゴリーのアイテムの使用を宣言できる。</summary>
    AllowItemUse = 829,
    // actor, action category, factor
    /// <summary>対象が行う指定カテゴリーの行為の威力に倍率を掛ける。</summary>
    MultiplyActionPower = 830,
    // actor, action category, state, amount
    /// <summary>対象が指定カテゴリーの行為を発動した時に、指定状態のスタックを得る。</summary>
    GainStateStacksOnAction = 831,
    /// <summary>この武器を選択したスキルに限り、指定の条件と数値で判定・威力・防御点などを補正する。</summary>
    SelectedWeaponRule = 832,
    /// <summary>条件に応じて適用する補正や許可の種類と、対象・倍率などの引数を保持する。</summary>
    RuleModifier = 833,
    /// <summary>指定のイベント・条件を満たした時に適用する効果を保持する。</summary>
    ConditionalRule = 834,
    /// <summary>条件に応じて振るダイスの式、出目の範囲と対応する報酬や付与状態を表す。</summary>
    RollTable = 835,
    /// <summary>何も起こらない結果を表す。</summary>
    NoEffect = 840,
    /// <summary>対象範囲を選び、通常威力と属性威力を個別の式から求めて攻撃する。</summary>
    CombinedPowerAttack = 841,
    /// <summary>対象ごとに威力を個別に算出して攻撃する。</summary>
    IndividualTargetAttack = 842,
    /// <summary>指定した回数だけ同じ威力式による攻撃を繰り返す。</summary>
    RepeatedPowerAttack = 843,
    /// <summary>カウンターの宣言者に追加のロールと目標値の達成を要求する。</summary>
    RequireCounterCheck = 844,
    /// <summary>発動対象の指定リソースに、式で算出したダメージを与える。</summary>
    DamageActivationTargetPool = 845,
    /// <summary>名前を参照して指定数のモンスターを召喚する。</summary>
    SummonEnemy = 846,
    /// <summary>この攻撃で対象に指定状態を付与した場合、指定ロールの結果を固定する。</summary>
    SetRollOnInflictingState = 847,
    /// <summary>この攻撃に対する他の効果の適用を禁止する。</summary>
    PreventAttackEffects = 848,
    /// <summary>全ての味方が次の指定回数に行う、該当カテゴリーの行為の威力に倍率を適用する。</summary>
    NextAllyCategoryPower = 849,
    /// <summary>全ての敵に指定ターン数の状態を付与する。</summary>
    ApplyTimedStateAllEnemies = 850,
    /// <summary>この攻撃の被ダメージ計算に用いる防御点に倍率を適用する。</summary>
    AttackDefenseFactor = 851,
    /// <summary>自身を対象とする選択を取り消す。</summary>
    CancelSelfSelection = 852,
    /// <summary>対象の状態を解除し、最大リソースに対する割合で回復して蘇生する。</summary>
    ReviveTargetWithResourcePercent = 853,
    /// <summary>戦闘エリア内のプレイヤーキャラクターが行う次の進行ロールを所定回数増減する。</summary>
    AreaProgressRollDelta = 854,
    /// <summary>敵が対象に付与した指定カテゴリーの状態をランダムに一つ解除する。</summary>
    RemoveRandomEnemyAppliedCategory = 855,
    /// <summary>指定リソースへの威力を式で求めて攻撃する。</summary>
    ResourcePowerAttack = 856,
    /// <summary>指定ターンの経過後、対象を指定した属性の威力で攻撃する。</summary>
    DelayedElementAttack = 857,
    /// <summary>対象の指定カテゴリーの発動を失敗させる。</summary>
    FailTargetCategory = 858,
    /// <summary>対象のリソース最大値に対する割合を攻撃の威力とする。</summary>
    AttackWithTargetMaxPercent = 859,
    /// <summary>対象を自身と同じ接近グループに移動させる。</summary>
    MoveTargetToOwnGroup = 860,
    /// <summary>指定カテゴリーから自身が受けるダメージを式の値だけ軽減する。</summary>
    ReduceIncomingCategoryDamage = 861,
    /// <summary>対象の指定リソースに、式で算出したダメージを複数回連続で与える。</summary>
    RepeatedResourceDamage = 862,
    /// <summary>戦闘中の全てのキャラクターに称号を付与する。</summary>
    AwardBattleTitle = 863,
    /// <summary>列挙されたカテゴリーの効果を無効にする。</summary>
    CancelCategories = 864,
    /// <summary>名前で指定されたキャラクターを復活させる。</summary>
    ReviveNamedCharacter = 865,
    /// <summary>名前で選んだ味方に指定スキルを発動させる。</summary>
    AllyUsesSkill = 866,
    /// <summary>対象の代わりに自身がアクティブ効果を受ける。</summary>
    InterceptActiveEffect = 867,
    /// <summary>この攻撃で指定ロールが発生した場合、自身のリソースを0にして状態を付与する。</summary>
    DefeatSelfOnInjuryRoll = 868,
    /// <summary>与えたダメージの指定割合を自身のリソースとして回復する。</summary>
    HealFractionOfDamage = 869,
    /// <summary>対象と全ての味方を同じ接近グループの接近状態にする。</summary>
    EngageTargetWithAllAllies = 870,
    /// <summary>対象が所持する指定サイズのアイテムを、指定個数ランダムに奪う。</summary>
    StealRandomItemBySize = 871,
    /// <summary>対象の次のターン開始時に指定リソースを増減する。</summary>
    NextTargetTurnResourceDelta = 872,
    /// <summary>明示された対象範囲の全員を、指定属性の威力で攻撃する。</summary>
    ElementAttackWithScope = 873,
    /// <summary>自身のターン開始時に指定リソースへのダメージを受ける。</summary>
    TurnStartPoolDamage = 874,
    /// <summary>指定カテゴリーを持たないアクティブスキルの使用を禁止する。</summary>
    ProhibitActiveExceptCategory = 875,
    /// <summary>指定カテゴリーの対象を、自身以外の選択可能なキャラクターからランダムに選ぶ。</summary>
    RandomOtherCharacterForCategory = 876,
    /// <summary>効果の適用後、この状態のスタック数を0にする。</summary>
    ClearStacksAfterEffect = 877,
    /// <summary>自身の行動宣言を禁止する。</summary>
    ProhibitAllActions = 878,
    /// <summary>ターン開始時に獲得する指定リソースの量を増減する。</summary>
    TurnStartResourceGainDelta = 879,
    /// <summary>二つのカテゴリーが両方該当するとき、威力に固定値を加算する。</summary>
    PowerOffsetForBothCategories = 880,
    /// <summary>自身のターン開始時、付与時に選んだ対象を通常威力と属性威力で攻撃する。</summary>
    SelectedTargetMixedAttackAtTurnStart = 881,
    /// <summary>自身にスキルを発動した相手を指定属性の威力で攻撃する。</summary>
    RetaliateWithElement = 882,
    /// <summary>自身が列挙されたカテゴリーを宣言できなくなる。</summary>
    ProhibitCategoryDeclarations = 883,
    /// <summary>自身が列挙されたカテゴリーを永続的に宣言できなくなる。</summary>
    PermanentlyProhibitCategories = 884,
    /// <summary>付与から指定ターン経過後、自身のリソースを0にして指定状態を付与する。</summary>
    DefeatAfterStateTurns = 885,
    /// <summary>スタックが指定数以上なら、ターン開始時にダメージを受ける。</summary>
    TurnStartDamageAtStacks = 886,
    /// <summary>敵を選ぶ際、選択可能な敵からランダムに対象を決める。</summary>
    RandomSelectableEnemy = 887,
    /// <summary>いずれかの指定部位に装備しているアイテムの参照を禁止する。</summary>
    BlockEquipmentSlotReference = 888,
    /// <summary>この状態の保持者を指定カテゴリーの対象として選べなくする。</summary>
    PreventCategoryTargeting = 889,
    /// <summary>自身の防御点を指定値にする。</summary>
    SetOwnDefense = 890,
    /// <summary>次に発動する指定スキルの威力に倍率を掛ける。</summary>
    NextNamedSkillPower = 891,
    /// <summary>対象に指定状態がある場合、この宣言への指定カテゴリーの応答を禁止する。</summary>
    PreventResponsesForTargetState = 892,
    /// <summary>自身に付与された全ての状態を解除する。</summary>
    PurgeOwnStates = 893,
    /// <summary>指定カテゴリーで応答する場合、攻撃に選んだ武器の耐久を0にして装備を解除する。</summary>
    DisarmRespondingWeapon = 894,
    /// <summary>達成値が指定値以上なら、対象に指定ターン数の状態を付与する。</summary>
    TimedTargetStateAtCheck = 895,
    /// <summary>達成値が指定値以上なら、対象に指定状態を付与する。</summary>
    TargetStateAtCheck = 896,
    /// <summary>対象が指定状態なら、威力に倍率を掛ける。</summary>
    PowerFactorAtTargetState = 897,
    /// <summary>指定数のモンスターを召喚して発動者と接近させる。</summary>
    SummonAndEngageCaster = 898,
    /// <summary>次のターン開始時に自身が戦闘から離脱する。</summary>
    ExitNextTurn = 899,
    /// <summary>自身による効果の宣言を禁止する。</summary>
    ProhibitEffectDeclaration = 900,
    /// <summary>指定カテゴリーの達成値を増減する。</summary>
    CategoryResultOffset = 901,
    /// <summary>自身が指定状態になったとき、この効果を解除する。</summary>
    EndOnGainingState = 902,
    /// <summary>自身のターン開始時に指定リソースを増減する。</summary>
    TurnStartResourceOffset = 903,
    /// <summary>このターン中のクリティカルまたはファンブル判定の出目を増減する。</summary>
    ThresholdDeltaForTurn = 904,
    /// <summary>自身に対する指定の二つの状態を無効にする。</summary>
    NegateOwnStates = 905,
    /// <summary>自身と対象の間で、指定した二つのリソースをそれぞれ交換する。</summary>
    SwapResourcesWithTarget = 906,
    /// <summary>対象の指定リソースに、通常威力と属性威力から算出するダメージを与える。</summary>
    MixedPowerPoolDamage = 907,
    /// <summary>指定スキルの威力に式で求めた倍率を掛ける。</summary>
    NamedSkillPowerFormulaFactor = 908,
    /// <summary>ダメージを受けるとこの状態を解除する。</summary>
    EndWhenDamaged = 909,
    /// <summary>この攻撃にカウンターを宣言する相手の消費リソースを増減する。</summary>
    ResponseResourceCostDelta = 910,
    /// <summary>自身を攻撃した相手に、式から求めた威力で反撃する。</summary>
    AttackIncomingAttacker = 911,

    /// <summary>戦闘開始時に指定量のリソースを失う。</summary>
    BattleStartResourceLoss = 950,
    /// <summary>スタンバイフェーズに敵が行う判定の目標値を増減する。</summary>
    StandbyEnemyCheckDifficulty = 951,
    /// <summary>同名の効果の重複適用を防ぐ。</summary>
    SameNameDoesNotStack = 952,
    /// <summary>種族と人数の条件を満たすパーティーが指定アイテムを渡すと戦闘を終了する。</summary>
    EndBattleOnSoloPartyGift = 953,
    /// <summary>種族と人数の条件を満たすパーティーが指定称号を習得している場合に戦闘を終了する。</summary>
    EndBattleOnSoloPartyTitle = 954,
    /// <summary>指定の称号を習得する。</summary>
    LearnNamedTitle = 955,
    /// <summary>自身の行動直前に自身の状態をすべて解除する。</summary>
    ClearAllStatesBeforeActing = 956,
    /// <summary>指定した属性威力による被ダメージをゼロにする。</summary>
    ImmuneToElementDamage = 957,
    /// <summary>毎ターン、指定したリソースを回復する。</summary>
    RecoverEachTurn = 958,
    /// <summary>指定名のエンティティが場にいる間、指定カテゴリーの攻撃による被ダメージを式の分だけ軽減する。</summary>
    ReduceDamagePerPresentEntity = 959,
    /// <summary>指定ターンの開始時に別のエンティティに変身する。</summary>
    TransformAtTurnStart = 960,
    /// <summary>指定の分類を持つ攻撃を受けたとき、被ダメージに倍率を適用する。</summary>
    IncomingTaggedActionDamageFactor = 961,
    /// <summary>指定リソースが閾値以下に達したとき、プレイヤーの勝利とする。</summary>
    PlayerWinsAtResourceThreshold = 962,
    /// <summary>指定リソースへのダメージを受けなくする。</summary>
    IgnoreResourceDamage = 963,
    /// <summary>自身に向けられた指定カテゴリーの行為を、達成値が閾値以下なら無効にする。</summary>
    CancelLowResultIncomingCategory = 964,
    /// <summary>指定の特性が無効な間、自身の能力値を指定値にする。</summary>
    SetStatWhenTraitInactive = 965,
    /// <summary>自身の能力値やリソース最大値を指定値にする。</summary>
    SetOwnStatToValue = 966,
    /// <summary>一つのスキルへのカウンター回数を増やす。戦闘中の使用回数に制限がある。</summary>
    BattleLimitedCountersPerSkill = 967,
    /// <summary>指定状態による戦闘終了を防ぐ。</summary>
    StateDoesNotEndBattle = 968,
    /// <summary>自身が必ず先行する。</summary>
    AlwaysTakeInitiative = 969,
    /// <summary>指定カテゴリーまたは状態を持つキャラクターだけが、指定行為で自身を対象にできる。</summary>
    RequireTargeterCategoryOrState = 970,
    /// <summary>自身への状態付与時に、指定の式と目標値で抵抗判定を行う。</summary>
    RollAgainstIncomingState = 971,
    /// <summary>抵抗判定に成功した場合、状態の付与を無効にする。</summary>
    CancelStateOnSuccessfulCheck = 972,
    /// <summary>判定に成功した場合、自身に付与された指定カテゴリーの状態をすべて解除する。</summary>
    CleanseStateCategoryOnCheckSuccess = 973,
    /// <summary>指定した二つの属性威力を含む行為を無効にする。</summary>
    ImmuneToElementPairAction = 974,
    /// <summary>指定した二つの属性威力を含む行為からの被ダメージに倍率を適用する。</summary>
    ElementPairActionDamageFactor = 975,
    /// <summary>指定した二つの属性威力を含む行為を受けたとき、戦闘を終了する。</summary>
    EndBattleWhenHitByElementPair = 976,
    /// <summary>自身と接近していない相手が行う指定カテゴリーの判定の目標値を増減する。</summary>
    IncomingRangedCategoryDifficulty = 977,
    /// <summary>指定した味方が場に存在する間、自身を選択対象から外す。</summary>
    UntargetableWhileAllyPresent = 978,
    /// <summary>入力した式で自身へのダメージ軽減量を算出する。</summary>
    ReduceIncomingPowerByFormula = 979,
    /// <summary>指定状態になったとき、別のエンティティとして戦闘を再開する。</summary>
    RestartBattleAsEntityOnState = 980,
    /// <summary>ターン開始時に指定リソースを指定値にする。</summary>
    SetResourceAtTurnStart = 981,
    /// <summary>原文で効果の内容が伏せられていることを示す。</summary>
    UndisclosedEffect = 982,
    /// <summary>自身に付与される状態を無効にする。</summary>
    ImmuneToAllStates = 983,
    /// <summary>敵の指定リソースがゼロより小さい値になることを許可する。</summary>
    AllowNegativeEnemyResource = 984,
    /// <summary>敵のリソースの負値に応じて、自身の二つのリソースを回復する。</summary>
    RecoverFromEnemyNegativeResource = 985,
    /// <summary>どちらかのリソースがゼロに達すると、指定した二つのリソースを全回復する。</summary>
    RecoverResourcesWhenEitherDepleted = 986,
    /// <summary>自身に敗北したキャラクターが指定額を失う。</summary>
    CurrencyLossOnDefeat = 987,
    /// <summary>自身を対象とする判定のクリティカルとファンブルの出目範囲を個別に変更する。</summary>
    TargetedCriticalFumbleDiceShift = 988,
    /// <summary>毎ターンの開始時に自身の指定リソースを回復する。</summary>
    RecoverAtEachTurnStart = 989,
    /// <summary>敵が指定能力値で自身に対して行う判定を自動失敗にする。</summary>
    FailEnemyAbilityCheckAgainstSelf = 990,
    /// <summary>敵が行動を終えるたびに指定リソースを回復する。</summary>
    RecoverAfterEnemyAction = 991,
    /// <summary>指定カテゴリーの状態を無効にする。</summary>
    ImmuneToStateCategories = 992,
    /// <summary>先行ターンを得た場合、そのターンの与ダメージに倍率を適用する。</summary>
    OpeningTurnDamageFactor = 993,
    /// <summary>例外カテゴリーの攻撃を除き、リソースが尽きたときの状態移行を次の自身のターン終了まで延期する。</summary>
    DeferStateUnlessCategoryHit = 994,
    /// <summary>戦闘開始時に指定状態のスタックを得る。</summary>
    GainStateStacksAtBattleStart = 995,
    /// <summary>攻撃されたとき、攻撃した相手に指定量のダメージを与える。</summary>
    RetaliateWhenAttacked = 996,
    /// <summary>自身のターン開始時にリソースの不足量に応じて回復する。</summary>
    RecoverMissingResourcePercentAtOwnTurnStart = 997,
    /// <summary>同じ接近グループから指定カテゴリーの行為を受けたとき、行為者へ属性ダメージを与える。</summary>
    MeleeElementalRetaliation = 998,
    /// <summary>アイテムの効果を受けなくする。</summary>
    IgnoreAllItemEffects = 999,
    /// <summary>指定フェーズ中、敵が宣言できる行為を指定ロールに制限する。</summary>
    LimitEnemyDeclarationsByPhase = 1000,
    /// <summary>指定属性のダメージを無効にし、同じ量のリソースを回復する。</summary>
    AbsorbElementDamage = 1001,
    /// <summary>指定能力値による被ダメージに倍率を適用する。</summary>
    DamageByAbilityTakenFactor = 1002,
    /// <summary>自身を選択対象に含むクリティカルを無効にする。</summary>
    IgnoreTargetingCriticals = 1003,
    /// <summary>自身が与えたダメージに応じて指定リソースを回復する。</summary>
    RestoreByDamageDealt = 1004,
    /// <summary>指定カテゴリーに由来するものを除き、自身への状態付与を無効にする。</summary>
    ImmuneToStatesExceptCategory = 1005,
    /// <summary>指定カテゴリーに由来するものを除き、自身への効果を無効にする。</summary>
    ImmuneToEffectsExceptCategory = 1006,
    /// <summary>軽減量が被ダメージを上回った分をリソース回復に変える。</summary>
    RestoreOverReducedDamage = 1007,
    /// <summary>自身のターン開始時に付与されている状態をすべて解除する。</summary>
    ClearStatesAtOwnTurnStart = 1008,
    /// <summary>戦闘中の全キャラクターに指定状態を付与する。</summary>
    ApplyStateToAllCombatants = 1009,
    /// <summary>自身に対する行為判定の目標値を増減する。</summary>
    IncomingActionCheckDifficulty = 1010,
    /// <summary>指定した午前の時刻に戦闘を終了する。</summary>
    EndBattleAtClockTime = 1011,
    /// <summary>自身を対象に含む指定カテゴリーの行為の目標値に倍率を適用する。</summary>
    IncomingCategoryDifficultyFactor = 1012,
    /// <summary>毎ターンの開始時に自身に付与されたすべての状態を解除する。</summary>
    ClearStatesAtEachTurnStart = 1013,
    /// <summary>指定状態にある敵も選択対象に含められるようにする。</summary>
    TargetEnemiesWithState = 1014,
    /// <summary>名前で指定したキャラクターと同じ接近グループに入る。</summary>
    EngageNamedCharacter = 1040,
    /// <summary>指定カテゴリーを持つキャラクターに指定状態を直ちに付与する。</summary>
    ApplyImmediateStateToCategory = 1041,
    /// <summary>指定リソースがしきい値以下の対象が1体以上いれば、対象の総数×指定量だけ、自身の2つのリソースをそれぞれ回復する。</summary>
    RecoverPerTargetWhenResourceLow = 1042,
    /// <summary>対象のリソースがしきい値以下のとき、この攻撃が与える指定リソースへのダメージに倍率を適用する。</summary>
    ScaleResourceDamageAtTargetThreshold = 1043,
    /// <summary>この効果で対象が指定状態になったとき、自身のリソースを回復する。</summary>
    RecoverWhenEffectCausesState = 1044,
    /// <summary>発動ロールの結果による個数で指定アイテムを獲得する。</summary>
    CraftItemFromRolls = 1200,
    /// <summary>発動ロールの達成回数が下限に届いた時、指定個数のアイテムを製作する。</summary>
    RequireSuccessesForProduct = 1201,
    /// <summary>製作などの連続判定で達成回数を加減する。</summary>
    ChangeSuccessCount = 1202,
    /// <summary>製作物のグレードを加減する。</summary>
    ChangeProductGrade = 1203,
    /// <summary>獲得個数が下限に達した場合に追加のアイテムを得る。</summary>
    GrantBonusAtOutputCount = 1204,
    /// <summary>残りの連続判定を中断し、自動成功または自動失敗にする。</summary>
    EndRollsWithOutcome = 1205,
    /// <summary>連続判定の達成回数が下限に達した時にこの効果を発動する。</summary>
    RequireSuccessesForEffect = 1206,
    /// <summary>対象に同じカテゴリーの効果が既にある場合、このバフで上書きする。</summary>
    ReplacePriorCategoryBuff = 1207,
    /// <summary>出目範囲に応じて指定カテゴリーのアイテムを獲得する。</summary>
    ClassifiedRewardTableItem = 1208,
    /// <summary>指定の代替素材を使うと完成品のグレードを変更する。</summary>
    SubstituteIngredientForGrade = 1209,
    /// <summary>完成品に名前を付けて本文を記録する。</summary>
    NameAndWriteProducedItem = 1210,
    /// <summary>対象の種類に応じて完成品を置き換える。</summary>
    ReplaceProductForNamedTarget = 1211,
    /// <summary>数式の値をスキル値に使って対象を武器攻撃する。</summary>
    SkillValueFormulaWeaponAttack = 1212,
    /// <summary>町を出る前に指定リソースを全回復した時、選んだ状態を解除できる。</summary>
    CureOnFullResourceUntilTownExit = 1213,
    /// <summary>経験値と決意をそれぞれ指定量変化させる。</summary>
    GainExperienceAndDetermination = 1214,
    /// <summary>指定日数だけ能力ボーナスを変更する。</summary>
    TimedAbilityBonus = 1215,
    /// <summary>両カテゴリーの発動ロールのクリティカル境界を指定日数変更する。</summary>
    TimedCriticalThresholdTwoCategories = 1216,
    /// <summary>指定期間中、対象報酬の判定に成功した場合に追加の報酬ロールを許す。</summary>
    TimedExtraRewardOnCheck = 1217,
    /// <summary>このターンに味方が勝つと対象のドロップの出目を補正する。</summary>
    IncreaseDropsOnVictoryThisTurn = 1218,
    /// <summary>対象がエリアを離れる時の耐久ロールを任意にする。</summary>
    SkipDurabilityOnAreaExit = 1219,
    /// <summary>次の指定までの状態効果を指定ターンまたは以降に限定する。</summary>
    StateTurnScope = 1220,
    /// <summary>指定リソースが減少すると他の二つのリソースを回復する。</summary>
    RecoverTwoResourcesOnDecrease = 1221,
    /// <summary>指定主体のカテゴリーに属する達成値を補正する。</summary>
    ScaleRecipientCategoryAchievement = 1222,
    /// <summary>状態を受けた対象のリソースを加算する。</summary>
    AddRecipientResource = 1223,
    /// <summary>このターンのリソース減少を防ぐ。</summary>
    PreventResourceDecreaseThisTurn = 1224,
    /// <summary>敵が付与した状態の効果を無効にする。</summary>
    IgnoreEnemyStateEffects = 1225,
    /// <summary>状態の付与者による指定威力の武器攻撃を指定回数受ける。</summary>
    SourceRepeatedWeaponAttack = 1226,
    /// <summary>状態の受け手を対象にした指定カテゴリーの達成値を補正する。</summary>
    IncomingCategoryAchievement = 1227,
    /// <summary>二つの能力値による行為判定の達成値を乗算する。</summary>
    ScaleTwoAbilityChecks = 1228,
    /// <summary>対象者が指定のダイスを振る。</summary>
    TargetRollDice = 1229,
    /// <summary>出目に対応する占い効果を指定の日の終了まで適用する。</summary>
    GrantRolledFortuneUntilDayEnd = 1230,
    /// <summary>出目に対応するエリアを指定の占い効果に設定し指定の日の終了まで付与する。</summary>
    GrantAreaFortuneUntilDayEnd = 1231,
    /// <summary>指定範囲の出目のとき、続く名前付き占い効果を適用する。</summary>
    FortuneOutcome = 1232,
    /// <summary>出目の範囲から対象エリアを選ぶ。</summary>
    FortuneAreaOutcome = 1233,
    /// <summary>追加納品した場合の経験値と決意を補正する。</summary>
    FortuneDeliveryBonus = 1234,
    /// <summary>指定カテゴリーの敵に遭遇したとき味方の敏捷ロール平均値を補正する。</summary>
    FortuneEncounterAgility = 1235,
    /// <summary>キャラクターへの販売収入を乗算する。</summary>
    FortuneSaleMultiplier = 1236,
    /// <summary>一対一のロールプレイで相手にアイテムを追加付与する。</summary>
    FortuneRoleplayReward = 1237,
    /// <summary>抽選エリアで行為判定の達成値を補正する。</summary>
    FortuneAreaAchievement = 1238,
    /// <summary>抽選エリアでクエスト特別報酬の出目を補正する。</summary>
    FortuneAreaRewardRoll = 1239,
    /// <summary>抽選エリアでクエスト報酬にアイテムを加える。</summary>
    FortuneAreaRewardItem = 1240,
    /// <summary>指定の能力ボーナスを使用したダイスロール後に状態を解除する。</summary>
    ExpireAfterTwoAbilityRolls = 1241,
    /// <summary>〈指定名〉の耐久を指定値回復する。</summary>
    RepairEquippedCategory = 1300,
    /// <summary>種類、名前、個数で指定された報酬を使用者が獲得する。</summary>
    GrantClassifiedReward = 1363,
    /// <summary>使用者のキャラクター専用武器を獲得する。</summary>
    GrantPersonalWeapon = 1364,
    /// <summary>対象カテゴリーの発動ロールの目標値に任意で倍率を掛ける。</summary>
    OptionalActionTargetFactor = 1365,
    /// <summary>指定クラスを習得している場合、この行動に支払う指定リソースの量を変更する。</summary>
    ResourceCostIfClassLearned = 1366,
    /// <summary>《指定名》の最後に全滅した階層と進行ロールからやり直すことができる。</summary>
    RestartAreaAfterDefeat = 1301,
    /// <summary>このエリアを移動するまでの間、環境効果《指定名》及び《指定名》の効果を無効にする。</summary>
    IgnoreEnvironmentUntilLeaving = 1302,
    /// <summary>このダメージによって対象の指定リソースが指定数を下回った時、指定数ターンの間対象を《指定名》状態にする。</summary>
    ApplyStateAfterPoolBelow = 1303,
    /// <summary>パーティメンバーの《指定名》は指定値の指定リソースと指定リソースダメージを受ける。</summary>
    DamagePartyWithTrait = 1304,
    /// <summary>パーティーメンバー全員は最後に訪れた町エリアに移動する。</summary>
    ReturnPartyToLastTown = 1305,
    /// <summary>対象が《指定名》の場合、戦闘を離脱して《指定名》×指定数を得る。</summary>
    NamedTargetFleeAndGrantItem = 1306,
    /// <summary>対象が《指定名》ではない場合、対象へ永続的に《指定名》スタックを×指定数付与する。</summary>
    StackExceptNamedTarget = 1307,
    /// <summary>一日に指定数回以上服用すると指定リソース最大値が指定値になり、翌日まで《指定名》状態になる。</summary>
    DailyUseDepletion = 1308,
    /// <summary>指定カテゴリーの敵がいる場合、味方全員の指定フェーズでの指定能力値による達成値に倍率を掛ける。</summary>
    PartyPhaseAbilityScale = 1309,
    /// <summary>対象魔法が命中した時、付与されている《指定名》スタック×指定値の指定リソースを減少させる。</summary>
    OnSpellHitDamagePerStack = 1310,
    /// <summary>自身に付与されている〈指定名〉を1つ選択して解除する。</summary>
    RemoveOneStateOfCategories = 1312,
    /// <summary>自身に付与されている《指定名》状態を全て解除する。</summary>
    RemoveNamedStates = 1313,
    /// <summary>自身は〈指定名〉の行動を指定数つ宣言できる。</summary>
    DeclarePhaseActions = 1314,
    /// <summary>自身はこのエリア効果の《指定名》から受ける効果を無効にする。</summary>
    IgnoreCurrentAreaEnvironment = 1315,
    /// <summary>自身は対象の〈指定名〉エリアの行先を一つ選択してエリアを移動する。</summary>
    TravelToSelectedDestination = 1316,
    /// <summary>指定名で移動できる行先へ移動する。</summary>
    TravelByTransport = 1317,
    /// <summary>処理後この《指定名》は指定名に返納される。</summary>
    ReturnItemAfterUse = 1318,
    /// <summary>称号《指定名》を得る。</summary>
    LearnTitle = 1319,
    /// <summary>指定数回以上達成で称号《指定名》を習得する。</summary>
    LearnTitleAtSuccessCount = 1320,
    /// <summary>このエリアの進行ロールは×指定倍率倍される。</summary>
    GainPhaseProgress = 1321,
    /// <summary>指定値 目標値指定数で指定名を振りなおす事が出来る。</summary>
    RerollProduction = 1322,
    /// <summary>この効果に対してさらに振りなおしは不可。</summary>
    NoFurtherReroll = 1323,
    /// <summary>その際に失敗から成功へ変化したなら成果物のグレードは指定量される。</summary>
    GradeOnChangedSuccess = 1324,
    /// <summary>《指定名》によって採取するアイテムの個数は指定量される。</summary>
    NamedSkillHarvestDelta = 1325,
    /// <summary>《指定名》による達成値は×指定倍率倍される。</summary>
    NamedSkillAchievementFactor = 1326,
    /// <summary>《指定名》の消費が指定量される。</summary>
    ConsumedItemQuantityDelta = 1327,
    /// <summary>クリティカルを出した時、個数が指定量される。</summary>
    BonusYieldOnCritical = 1328,
    /// <summary>ダイス目を指定数つ「指定数」に固定できる。</summary>
    FixDiceFaces = 1329,
    /// <summary>生産したアイテムのグレードを指定量する。</summary>
    IncreaseProducedGrade = 1330,
    /// <summary>生産のダイスロール指定数回分の達成値を指定量しても良い。</summary>
    OptionalProductionRollDelta = 1331,
    /// <summary>自身の《指定名》状態を解除して、指定リソースを指定値回復する。</summary>
    ReviveAndRecoverFlat = 1332,
    /// <summary>自身のレベルに関わらず、適正レベル指定数～指定数のクエストによる指定名報酬を受け取ることが出来る。</summary>
    ClaimQuestRewardsIgnoringLevel = 1333,
    /// <summary>適正レベル指定数のクエストによる指定名報酬を受け取ることが出来る。</summary>
    ClaimQuestRewardsAtLevel = 1334,
    /// <summary>RPによって、相手からのメッセージを受け取った時、このアイテムは消費される。</summary>
    ConsumeOnMessage = 1335,
    /// <summary>このアイテムは所持品に装備していなくても、〈指定名〉から使用する事が出来る。</summary>
    UseWithoutInventoryEquip = 1336,
    /// <summary>このアイテムを所持している場合、味方キャラクターとして扱い戦闘に参加する。</summary>
    JoinBattleWhileHeld = 1337,
    /// <summary>このアイテムを所持している状態で《指定名》を使用した場合、グレードを☆指定数として扱っても良い。</summary>
    TreatNamedItemAsGrade = 1338,
    /// <summary>このカードを生贄に捧げる時、カード指定数枚分として扱うことができる。</summary>
    SacrificeAsMultipleCards = 1339,
    /// <summary>アイテムを所持している場合、すべてのクエストの救助費を半額にする。</summary>
    HalveRescueFee = 1340,
    /// <summary>アイテムを所持している場合、推奨レベル指定数以下のクエストの救助費を半額にする。</summary>
    HalveRescueFeeBelowLevel = 1341,
    /// <summary>サイズ指定名の所持品枠を指定数個分使ってサイズ指定名のアイテムを指定数つ追加で所持できる。</summary>
    TradeInventorySlots = 1342,
    /// <summary>このアイテムを所持してペットに騎乗している間、サイズ指定名のアイテムの所持数がそれぞれ指定量される。</summary>
    MountedInventoryCapacity = 1343,
    /// <summary>このアイテムを所持してペットに騎乗している間、環境効果《指定名》を受けない。</summary>
    MountedEnvironmentImmunity = 1344,
    /// <summary>自身が〈指定名〉を宣言した時、発動ロール指定数つの達成値を×指定倍率倍しても良い。</summary>
    OptionalCategoryActivationScale = 1345,
    /// <summary>自身が〈指定名〉及び〈指定名〉を宣言した時、自身の指定リソースを指定値回復する。</summary>
    RestoreResourceOnEitherCategory = 1346,
    /// <summary>設置されたハウジングエリアに訪れたキャラクターは、ハウジングエリアの所有者が設定した〈指定名〉アイテム、価格、在庫数から、買取取引を行うことができる。</summary>
    ShopInHousing = 1347,
    /// <summary>取引は所有者が別エリアにいる、または不在でも成立する。</summary>
    AllowAbsentShopOwner = 1348,
    /// <summary>スタックが指定数以上の時《指定名》状態となる。</summary>
    BecomeStateAtStackCount = 1349,
    /// <summary>自身が受ける指定リソース回復効果は指定倍率倍される。</summary>
    IncomingHealingScale = 1350,
    /// <summary>自身が発動する〈指定名〉の達成値は指定量される。</summary>
    CategoryAchievementDelta = 1351,
    /// <summary>自身の[指定名B]はスタック×指定倍率減少する。</summary>
    AbilityReductionPerStack = 1352,
    /// <summary>自身の〈指定名〉及び〈指定名〉の達成値は×指定値倍される。</summary>
    PartyOrSelfRollStackScale = 1353,
    /// <summary>〈指定名〉かつ〈指定名〉の威力は指定値加算される。</summary>
    TwoCategoryFlatPower = 1354,
    /// <summary>全ての能力値Bとリソース最大値は×指定倍率倍される。</summary>
    AllAbilityAndResourceScale = 1355,
    /// <summary>自身の属性値Bは×指定倍率倍される。</summary>
    AllElementBonusScale = 1356,
    /// <summary>自身のターン、行動と選択対象は付与者によって選択される。</summary>
    ApplierControlsActions = 1357,
    /// <summary>〈指定名〉によって自身の指定リソースが0になる時、《指定名》状態を解除して指定リソースが全回復する。</summary>
    OnCategoryDepletionRecover = 1358,
    /// <summary>ターン開始時、自身の《指定名》状態を解除する。</summary>
    RemoveNamedStateOnTurnStart = 1360,
    /// <summary>自身の指定リソース及び指定リソース最大値は×指定倍率倍される。</summary>
    ResourcePairMaximumScale = 1361,
    /// <summary>自身の〈指定名〉スキルによる与ダメージは×指定倍率倍される。</summary>
    SkillCategoryDamageFactor = 1362,
    /// <summary>アイテムを指定名のアイテムとして扱える。</summary>
    TreatAsNamedItem = 1100,
    /// <summary>追加の行為判定に失敗すると自身に永続スタックを与える。</summary>
    FailedCheckPermanentStacks = 1101,
    /// <summary>追加の行為判定に成功すると指定の報酬を得る。</summary>
    SuccessfulCheckReward = 1102,
    /// <summary>このターンにアクティブ効果で消費する指定リソースの量に倍率をかける。</summary>
    ActiveCostFactorThisTurn = 1103,
    /// <summary>発動したアイテムを効果の適用時に指定個数消費する。</summary>
    ConsumedItemCount = 1104,
    /// <summary>次に達成する作業クエストの得点を加減する。</summary>
    NextWorkQuestScore = 1105,
    /// <summary>自身の種族が指定リストに含まれていれば指定リソースを回復する。</summary>
    RestoreIfAnyRace = 1106,
    /// <summary>次に指定名のスキルを使用するとき達成値に倍率をかける。</summary>
    NextNamedSkillAchievementFactor = 1107,
    /// <summary>最大値を超えた回復量を上限まで永続スタックに変換する。</summary>
    ExcessRecoveryToStacks = 1108,
    /// <summary>自身の指定カテゴリーの状態を上限個数まで選んで解除する。</summary>
    ChooseStateCategoriesToRemove = 1109,
    /// <summary>指定量の経験値と決意をそれぞれ得る。</summary>
    GainProgressionRewards = 1110,
    /// <summary>指定名のロール表を指定回数振る。</summary>
    RollNamedTable = 1111,
    /// <summary>自身の状態を選びスタック数を増加させる。</summary>
    IncreaseSelectedStacks = 1112,
    /// <summary>自身が指定カテゴリーを持つ場合に状態を付与する。</summary>
    CategorySelfState = 1113,
    /// <summary>自身が指定種族の場合に状態を付与する。</summary>
    RaceSelfState = 1114,
    /// <summary>指定回数だけ指定カテゴリーのクリティカルの出目を加減する。</summary>
    NextCategoryCriticalDelta = 1115,
    /// <summary>アクティブ効果によるリソース消費量を加減する。</summary>
    ActiveCostOffset = 1116,
    /// <summary>指定カテゴリーによるリソース消費量に倍率をかける。</summary>
    CategoryResourceCostFactor = 1118,
    /// <summary>指定カテゴリーのクエストで得る決意と経験値を加減する。</summary>
    QuestCategoryRewardBonus = 1119,
    /// <summary>同じ報酬補正の重複を認めず、元の経験値と決意が0の場合は加算しない。</summary>
    NonStackingNonzeroRewards = 1120,
    /// <summary>ターン開始時、自身のリソースをその最大値に対する割合で回復する。</summary>
    PeriodicMaxResourceRecovery = 1121,
    /// <summary>カウンターによって受けるリソースダメージに倍率をかける。</summary>
    IncomingCounterResourceFactor = 1122,
    /// <summary>毎ターン開始時に指定した状態のスタックを得る。</summary>
    TurnStartNamedStacks = 1123,
    /// <summary>スタック数に応じて指定カテゴリーのダメージとSANダメージを減少させる。</summary>
    CategoryStackDamageMitigation = 1124,
    /// <summary>指定カテゴリーの威力またはダメージとSANダメージの合計だけスタックを減らす。</summary>
    LoseStacksByDamageAndSan = 1125,
    /// <summary>指定リソースが減少する時に代わりに状態スタックを消費する。</summary>
    SpendStacksInsteadOfResource = 1126,
    /// <summary>自身の奇数または偶数ターンの開始時にリソースを得る。</summary>
    ParityTurnStartResource = 1127,
    /// <summary>指定名のスキルの目標値を固定する。</summary>
    NamedSkillTargetValue = 1128,
    /// <summary>指定スキルで得る指定状態のスタック数に倍率をかける。</summary>
    NamedSkillStackMultiplier = 1129,
    /// <summary>エリア移動条件と特殊エンカウントの判定達成値を加減する。</summary>
    AreaMoveEncounterCheckBonus = 1130,
    /// <summary>指定系統のクラススキルによる達成値に倍率をかける。</summary>
    ClassFamilyCheckFactor = 1131,
    /// <summary>指定属性で与える威力に倍率をかける。</summary>
    OutgoingElementPowerFactor = 1132,
    /// <summary>指定したスタック数に応じて敵の指定特性を無効にする。</summary>
    SuppressEnemyTraitAtStack = 1133,
    /// <summary>無作為なキャラクターに指定式の威力で攻撃する。</summary>
    RandomCharacterAttack = 1134,
    /// <summary>重複を許し、無作為な対象へ指定回数リソースダメージを与える。</summary>
    RepeatedRandomResourceDamage = 1135,
    /// <summary>対象を現在のパーティーから離脱させ、新しいパーティーとする。</summary>
    RemoveTargetFromParty = 1136,
    /// <summary>対象をパーティーから外し進行ロールの達成回数を減らす。</summary>
    LeavePartyAndLoseProgress = 1137,
    /// <summary>指定分類以外の無作為なキャラクターのリソース最大値を割合で減らす。</summary>
    ReduceMaxResourcesExceptCategories = 1138,
    /// <summary>敵キャラクターに指定したどちらかのカテゴリーの宣言を義務付ける。</summary>
    RequireEnemyDeclaration = 1139,
    /// <summary>宣言が成功した時に宣言者へ指定した状態のスタックを付与する。</summary>
    GiveDeclarerStacksOnSuccess = 1140,
    /// <summary>指定数のキャラクターを選択し指定式の威力で攻撃する。</summary>
    AttackChosenCharacters = 1141,
    /// <summary>発動ロールの結果を参照して対象への連続攻撃を解決する。</summary>
    RepeatAttackByActivationResult = 1142,
    /// <summary>自身が指定属性のダメージを受け、二つの永続状態を得る。</summary>
    SelfElementDamageAndPersistentStates = 1143,
}
/// <summary>効果一つの種類と引数。適用元の名前・欄・発動条件は親データが保持する。</summary>
[Serializable]
public class EffectContent
{
    public EffectContentType Type;
    public List<string> Parameters = new List<string>();
}
