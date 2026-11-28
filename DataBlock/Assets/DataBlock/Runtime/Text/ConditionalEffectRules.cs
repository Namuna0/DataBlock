using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class CommonEffectTextCodec
{
    // ConditionalRule parameters are trigger key, operation key, then their arguments in order.
    // Names of items, states, categories and skills are arguments, including for named equipment effects.
    private const string Label = @"[^《》〈〉【】\[\]：:,。\r\n]+?";
    private const string TagList = @"[^《》〈〉【】。\r\n]+?";
    private const string StateRef = @"[^《》、。\r\n]+";
    private const string Stat = @"(?:[一-龠ァ-ヶー]+B|防御点|[A-Z]+最大値)";
    private const string Resource = @"(?:HP|MP|SP|ACT|SAN)";
    private const string RuleAmount = @"[0-9]+";
    private const string RuleFactor = @"[0-9]+(?:\.[0-9]+)?";
    private const string Delta = @"[+-][0-9]+(?:\.[0-9]+)?";
    private const string Formula = @"(?:\[[^\[\]。]+\]|[0-9.+*/d()×\-])+";

    // A rule may refer to a state defined outside the block that owns it.
    private static readonly Dictionary<string, int[]> StateArgumentIndexes = new Dictionary<string, int[]>();
    private static DataBlockSentencePattern Sentence(string key, string template, params string[] arguments)
    {
        var stateArgs = Enumerable.Range(0, arguments.Length).Where(i => arguments[i] == StateRef).ToArray();
        if (stateArgs.Length > 0) StateArgumentIndexes.Add(key, stateArgs);
        return new DataBlockSentencePattern(key, template, arguments);
    }

    private static readonly DataBlockSentencePattern[] Triggers =
    {
        Sentence("SelectedStatCheck", "この武器の選択を条件に[{0}]による行為判定を行う時", Stat),
        Sentence("SelectedCategoryAction", "この武器の選択を条件に〈{0}〉を発動する時", Label),
        Sentence("SelectedCategoryAttack", "この武器の選択を条件に対象を〈{0}〉した時", Label),
        Sentence("SelectedCategoryDamage", "この武器の選択を条件に〈{0}〉によってダメージを与えた時", Label),
        Sentence("SelectedHealing", "この武器の選択を条件に対象の{0}を回復する際", Resource),
        Sentence("AfterUsingWeapon", "この武器の選択を条件にスキルを発動した後"),
        Sentence("SelectedStateApplied", "この武器の選択を条件に対象を《{0}》付与した時", StateRef),
        Sentence("SelectedWithoutStateRequirement", "この武器の選択を条件に自身の《{0}》状態を宣言条件に含めないスキルを発動する場合", StateRef),
        Sentence("CounterSelectedWeapon", "この武器の選択を条件に発動されたスキルにカウンター効果を発動する場合"),
        Sentence("SelectedRepeatHits", "この武器の選択を条件に発動したスキルによって、{0}ターンの間に同じ対象に{1}回以上ダメージを与えた時", RuleAmount, RuleAmount),
        Sentence("SelfRaceName", "自身の種族が《{0}》である場合", Label),
        Sentence("IncomingAttack", "自身が攻撃を受けた時"),
        Sentence("IncomingFromEither", "〈{0}〉または〈{1}〉から、〈{2}〉及び〈{3}〉の対象になった時", Label, Label, Label, Label),
        Sentence("InTownForTreatment", "自身が町エリアで治療のアクティブ効果を受ける時"),
        Sentence("TriggerOnItem", "自身が〈{0}〉の効果を受ける時", Label),
        Sentence("CheckSucceeded", "成功した場合"),
        Sentence("SuccessfulStatRoll", "{0}による発動ロールを成功させた時", Stat),
        Sentence("HealingWithCategory", "自身が〈{0}〉によってキャラクターの体力を回復するとき", Label),
        Sentence("ApplyingStateWithCategory", "自身が〈{0}〉によってキャラクターへ状態を付与する時", Label),
        Sentence("TargetHasTurnRecovery", "攻撃対象に毎ターンリソースを回復する状態が付与されている場合"),
        Sentence("NoBattleDamage", "戦闘中、自身が{0}度もダメージを受けなかったなら", RuleAmount),
        Sentence("MoveAgainstEnemy", "自身が敵キャラクターを選択して〈{0}〉を発動した時", Label),
        Sentence("CounterReductionOfDualType", "自身が発動した〈{0}〉かつ〈{1}〉の威力が〈{2}〉によって軽減される場合", Label, Label, Label),
        Sentence("SkillActingAsCategory", "自身の発動した《{0}》を〈{1}〉として扱うなら", Label, Label),
        Sentence("OnlyArmorReduced", "この武器による〈{0}〉に対して防御点以外からダメージ軽減されなかった場合", Label),
        Sentence("WornOnOccasion", "{0}の夜に装備して置くと", Label),
        Sentence("FromListedCategories", "自身が〈{0}〉によって〈{1}〉を発動した時", TagList, Label),
        Sentence("SuccessfulTurnStartCheck", "毎ターン開始時：{0} 達成値{1}の行為判定に成功した時", Formula, RuleAmount),
        Sentence("UseItem", "自身がアイテムを使用した時"),
        Sentence("UseItemCategory", "〈{0}〉を消費する際", Label),
        Sentence("ConsumeItemCategory", "自身が〈{0}〉を消費する時", Label),
        Sentence("ProcCategory", "自身が〈{0}〉を発動した時", Label),
        Sentence("CastingCategory", "自身が〈{0}〉を発動する時", Label),
        Sentence("DeclareCategory", "自身が〈{0}〉を宣言する場合", Label),
        Sentence("ActivateSkill", "自身が《{0}》を発動する時", Label),
        Sentence("GrantedSkillActivation", "自身がスキル《{0}》を発動する時", Label),
        Sentence("SuccessfulSkill", "自身がスキル《{0}》の発動に成功した時", Label),
        Sentence("AreaSkillSuccess", "自身がエリア〈{0}〉でスキル《{1}》の発動に成功した時", Label, Label),
        Sentence("CounterOwnAction", "自身の行動に対してカウンターを発動する時"),
        Sentence("DealElementPower", "自身が[{0}]による属性威力を与えた時", Stat),
        Sentence("IncomingCategory", "自身が〈{0}〉の対象になる時", Label),
        Sentence("IncomingElementAttack", "自身が{0}属性威力を含む〈{1}〉の対象になった時", Label, Label),
        Sentence("StatFailure", "自身が{0}による行為判定を通常失敗した時", Stat),
        Sentence("StatSuccess", "自身が[{0}]による行為判定を成功させた場合", Stat),
        Sentence("CheckFailure", "行為判定が失敗した場合"),
        Sentence("AfterStatCheck", "自身が[{0}]による行為判定を行ったターン", Stat),
        Sentence("StatCheckSucceededAtLeast", "自身の{0}による発動ロールの達成値が{1}以上で成功した時", Stat, RuleAmount),
        Sentence("AreaCheckSucceededAtLeast", "目標値がエリア指定である行為判定を達成値{0}以上で成功した時", RuleAmount),
        Sentence("RollContainsHighDie", "自身が[{0}]を含むダイスロールを行った時、ダイスの目に一つ以上{1}以上が含まれているなら", Stat, RuleAmount),
        Sentence("EquippedClass", "自身がクラス《{0}》を設定している場合", Label),
        Sentence("ClassEquipped", "クラス《{0}》を装備しているなら", Label),
        Sentence("OwnRace", "自身が種族〈{0}〉である場合", Label),
        Sentence("WhileInState", "自身が《{0}》状態の時", StateRef),
        Sentence("EquippedCategories", "自身が〈{0}〉かつ〈{1}〉を装備しているなら", Label, Label),
        Sentence("HasItem", "アイテムを所持している場合"),
        Sentence("AreaCategory", "エリアに〈{0}〉が含まれる場合", Label),
        Sentence("SelfInArea", "自身はエリアに〈{0}〉が含まれている時", Label),
        Sentence("FirstBattleTurns", "戦闘開始から{0}ターン目", RuleAmount),
        Sentence("OnTurnCategory", "戦闘開始から{0}ターン目、自身が〈{1}〉を発動した場合", RuleAmount, Label),
        Sentence("BattleTurn", "戦闘開始{0}ターン目", RuleAmount),
        Sentence("InactiveCombat", "自身が戦闘中ではない時"),
        Sentence("WeaponAttackDamage", "自身がこの武器威力による〈{0}〉によってダメージを与えた時", Label),
        Sentence("WeaponDamage", "この武器による〈{0}〉で対象にダメージを与えた時", Label),
        Sentence("WeaponKill", "この武器によって対象を《{0}》状態にしたとき", StateRef),
        Sentence("WeaponMultiHit", "この武器によって、{0}ターンの間に同一対象に{1}回以上のダメージを与えた時", RuleAmount, RuleAmount),
        Sentence("WeaponBlocked", "この武器による〈{0}〉に対して〈{1}〉を宣言される時", Label, Label),
        Sentence("WeaponEvaded", "この武器による〈{0}〉を〈{1}〉によって無効にされた時", Label, Label),
        Sentence("ItemDealDamage", "このアイテムによって対象にダメージを与えた時"),
        Sentence("ItemAttack", "このアイテムによって対象を攻撃した場合"),
        Sentence("ItemCategoryAction", "このアイテムによって〈{0}〉を発動する時", Label),
        Sentence("ItemMeleeAction", "このアイテムによって自身と同じ接近グループの対象に〈{0}〉を発動する時", Label),
        Sentence("ItemConsumedAsCost", "このアイテムを消費リソースとして消費する場合"),
        Sentence("ItemEquipped", "このアイテムを装備している時"),
        Sentence("ItemConsumedForActive", "アイテムを消費してアクティブ効果を発動する際"),
        Sentence("BuffAlly", "自身が、味方にバフを付与した時"),
        Sentence("GiveAllyBuff", "自身が味方に対してバフを付与する時"),
        Sentence("GiveAllyState", "自身が味方に対して状態を付与する時"),
        Sentence("GiveEnemyState", "自身が敵キャラクターに状態を付与したとき"),
        Sentence("WeaponVsTarget", "この武器による攻撃対象が〈{0}〉なら", Label),
        Sentence("DefenseReferenced", "自身の防御点が参照された時"),
        Sentence("AnyCheckWithStat", "自身が[{0}]による行為判定を行う時", Stat),
        Sentence("OwnTurnStart", "自身は自身のターン開始時に"),
        Sentence("StackAtLeast", "このスタックが{0}以上になった時", RuleAmount),
        Sentence("StackEven", "スタックが偶数の時"),
        Sentence("StackOdd", "スタックが奇数の時"),
        Sentence("StackZero", "スタックが0の時"),
        Sentence("OtherEntersArea", "〈{0}〉キャラクターがこのエリアに進入した時", Label),
        Sentence("PartyMovesToArea", "パーティー全員は〈{0}〉へ移動する度に", Label),
        Sentence("BattleDamageThreshold", "戦闘中、敵へ{0}を超えるダメージを出した時", RuleAmount),
        Sentence("ItemDeclaredInBattle", "自身が戦闘中〈{0}〉を使用する場合", Label),
        Sentence("StandbyRollAtLeast", "自身がスタンバイフェーズの行動順ロールで{0}以上の達成値を出した時", RuleAmount),
        Sentence("LevelAtMost", "自身のレベルが{0}以下である場合", RuleAmount),
        Sentence("IncomingMagic", "自身が〈{0}〉によってダメージを受ける時", Label),
        Sentence("EnemyTurnEndWithAct", "敵のターンの終了時点で自身のACTが{0}以上残っている時", RuleAmount),
        Sentence("FirstActivationRollDie", "発動ロールで{0}のダイスを出した時", RuleAmount),
        Sentence("DurabilityFace", "自身の耐久ロールで{0}が出た時", RuleAmount),
        Sentence("FirstAreaEquipmentDay", "装備から{0}日以上経過しているなら", RuleAmount),
        Sentence("NamedSkillAfterUses", "自身がこのターン{0}回以上《{1}》を宣言しているなら", RuleAmount, Label),
        Sentence("RepeatedInstrumentSkill", "この楽器によって{0}ターン連続で同名スキルを発動した時", RuleAmount),
        Sentence("OwnElementSkill", "自身が{0}属性威力を含むスキルを宣言する時", Label),
        Sentence("MeleeAttackTarget", "自身は同じ接近グループから〈{0}〉の対象になる時", Label),
        Sentence("NamedSkillMultiHit", "自身が《{0}》によって{1}体以上のキャラクターにダメージを与えた時", Label, RuleAmount),
        Sentence("TargetNegationType", "自身がこの武器威力による〈{0}〉によって〈{1}〉かつ〈{2}〉にダメージを与えた時", Label, Label, Label),
        Sentence("SameMeleeBlock", "この盾によって自身と同じ接近グループの相手から〈{0}〉を〈{1}〉した時", Label, Label),
        Sentence("TargetsAllyWithAction", "自身が味方を対象に〈{0}〉を宣言する時", Label),
        Sentence("PartyOtherItem", "自身を除くパーティーのキャラクターが〈{0}〉の使用を宣言する時", Label),
        Sentence("MagicAgainstTwoTags", "さらに〈{0}〉の対象が〈{1}〉及び〈{2}〉の場合", Label, Label, Label),
        Sentence("TargetHpAtMost", "対象のHPが{0}以下の時", RuleAmount),
        Sentence("SelfRecoversHp", "自身がHPを回復した時"),
        Sentence("BeforeSelfRecoversHp", "自身がHPを回復する時"),
        Sentence("GivenSanDamage", "自身がSAN値ダメージを受ける時"),
        Sentence("StackThreshold", "このスタックが{0}に到達した時", RuleAmount),
        Sentence("RollStatCheckAfterFailure", "[{0}]による行為判定に失敗した次の{1}回", Stat, RuleAmount),
        Sentence("NamedSkillRollAtLeast", "《{0}》の発動ロールで達成値{1}以上を出した時", Label, RuleAmount),
    };

    private static readonly DataBlockSentencePattern[] Operations =
    {
        Sentence("GiveSelectedState", "このアイテムによるスキルの選択対象を{0}ターンの間《{1}》にしても良い", RuleAmount, StateRef),
        Sentence("GiveExtraTimedState", "さらに対象へ{0}ターンの間《{1}》状態を付与する", RuleAmount, StateRef),
        Sentence("ChangeResultDice", "達成値は+{0}される", Formula),
        Sentence("EarnOnCategoryDeclare", "自身が〈{0}〉を宣言する度《{1}》スタックを永続的に{2}得る", Label, StateRef, RuleAmount),
        Sentence("CategoryCheckMultiplier", "〈{0}〉による発動ロールの達成値が×{1}倍される", Label, RuleFactor),
        Sentence("ReflectMitigatedDamage", "軽減した被ダメージの半分、相手のHPを減少させる。（最大{0}まで）", RuleAmount),
        Sentence("MaybeReceiveGift", "プレゼントがもらえるかもしれない"),
        Sentence("DamageAndSanDelta", "自身の被ダメージ及びSANダメージは{0}される", Delta),
        Sentence("LowerCriticalByFormula", "さらにクリティカルになるダイス目が{0}低くなる", Formula),
        Sentence("CategoryActMultiplier", "〈{0}〉による発動ロールは×{1}倍される", Label, RuleFactor),
        Sentence("RecoveryWithAddedCost", "消費{0}を+{1}する代わりに、{2}回復効果を×{3}倍しても良い", Resource, RuleAmount, Resource, RuleFactor),
        Sentence("ExtraCriticalTargets", "発動ロールがクリティカルなら対象を追加で{0}体選んでも良い", RuleAmount),
        Sentence("TransferRegeneration", "その状態を{0}スタック解除して、同ターン{1}スタック自身に付け替えても良い", RuleAmount, RuleAmount),
        Sentence("DualSkillCategoryPower", "《{0}》及び《{1}》による〈{2}〉の威力は+{3}加算される", Label, Label, Label, RuleAmount),
        Sentence("CategoryResultChange", "自身の〈{0}〉による達成値は{1}される", Label, Delta),
        Sentence("CheckToRepeat", "ダイスロール{0} 目標値{1}の行為判定を行う事が出来る", Formula, RuleAmount),
        Sentence("ReduceSingleTargetResource", "アクティブ効果が敵を{0}体対象にするなら対象の{1}を{2}しても良い", RuleAmount, Resource, Delta),
        Sentence("RecoverPortion", "自身の{0}をその回復量の×{1}倍の値で回復する", Resource, RuleFactor),
        Sentence("TargetTagPower", "対象が〈{0}〉である場合〈{1}〉威力は×{2}倍される", Label, Label, RuleFactor),
        Sentence("AdjustMitigation", "軽減量は{0}倍される", RuleFactor),
        Sentence("RollAndDamage", "達成値は{0}かつ被ダメージは×{1}倍される", Delta, RuleFactor),
        Sentence("MultiplyPower", "威力は×{0}倍される", RuleFactor),
        Sentence("ChangeTarget", "目標値は{0}される", Delta),
        Sentence("UntilOtherTurn", "相手のターン開始まで対象を《{0}》状態にしても良い", StateRef),
        Sentence("DispelOnEitherDamage", "この状態はダメージまたはSANダメージを受けると解除される"),
        Sentence("GrantAdditionalTargetState", "さらに選択可能なキャラクターを{0}体選んで、{1}ターンの間《{2}》状態を付与しても良い", RuleAmount, RuleAmount, StateRef),
        Sentence("StateStatCheckFactor", "自身は[{0}]による行為判定の達成値が×{1}倍される", Stat, RuleFactor),
        Sentence("ConvertStacksInBattle", "*戦闘中、任意のタイミングでこのスタックを全て《{0}》状態に変換する", StateRef),
        Sentence("PartyEntryResource", "パーティー全員は〈{0}〉へ移動する度に{1}を{2}回復する", Label, Resource, Formula),
        Sentence("DualTagStatMultiplier", "自身が〈{0}〉及び〈{1}〉の場合[{2}]は×{3}倍される", Label, Label, Stat, RuleFactor),
        Sentence("DualElementStats", "自身の[{0}]と[{1}]は{2}される", Stat, Stat, Delta),
        Sentence("StateStatGain", "自身の{0}が{1}加算される", Stat, Delta),
        Sentence("StateRollAddition", "自身の[{0}]による行為判定の達成値は{1}される", Stat, Delta),
        Sentence("StateRollMultiplier", "[{0}]による行為判定を行う際、達成値が×{1}倍される", Stat, RuleFactor),
        Sentence("OptionalNamedRoll", "毎ターン開始時：{0} 達成値{1}の行為判定を行う", Formula, RuleAmount),
        Sentence("GainResource", "{0}を{1}得る", Resource, RuleAmount),
        Sentence("GainResourceOnSuccess", "成功時{0}を{1}得る", Resource, RuleAmount),
        Sentence("ExtraElementAttack", "以降追加で{0}の{1}属性攻撃を与えても良い", Formula, Label),
        Sentence("GiveEffectTargetState", "発動対象を《{0}》状態にする", StateRef),
        Sentence("DateLimit", "この効果は日本時間の日にちに「{0}」が付く日、{1}日{2}回まで適用可能", RuleAmount, RuleAmount, RuleAmount),
        Sentence("RollCheapestMaterialDiscount", "{0}のダイスロールを行い、{1}なら《{2}》による消費リソースの内、最も価値の低い素材を×{3}削減する事が出来る", Formula, RuleAmount, Label, RuleAmount),
        Sentence("ThreeStateStacksForItem", "自身は戦闘開始時に《{0}》×{1}を消費して《{2}, {3}, {4}》スタックをそれぞれ{5}得ても良い", Label, RuleAmount, StateRef, StateRef, StateRef, Formula),
        Sentence("GainCategoryStatePermanently", "永続的に自身はカテゴリーに応じた《{0}》状態になる", StateRef),
        Sentence("ReplaceCategoryState", "*自身に別名の〈{0}〉が付与された時、前の状態を解除して、武器パッシブのあらゆる数値を×{1}倍しても良い", Label, RuleFactor),
        Sentence("ThreeSkillFreeCost", "戦闘中{0}度だけ《{1}》, 《{2}》, 《{3}》による消費{4}を{5}にしても良い", RuleAmount, Label, Label, Label, Resource, RuleAmount),
        Sentence("DamageElementPowerFactor", "自身の{0}属性威力による被ダメージは×{1}倍される", Label, RuleFactor),
        Sentence("CostDiscountMilestones", "戦闘中、あるいはその場のスキル使用時（宿泊や採取など、エリアの機能を使用した場合回数はリセット）[{0}]による行為判定を連続で成功するたびに、{1}回連続で消費{2}{3}→{4}回連続で消費{5}{6}→{7}回連続で消費{8}{9}、と{10}回復量が上昇してゆく。（最大消費{11}{12}まで）", Stat, RuleAmount, Resource, Delta, RuleAmount, Resource, Delta, RuleAmount, Resource, Delta, Resource, Resource, Delta),
        Sentence("MayRecoverResource", "{0}を{1}回復しても良い", Resource, Formula),
        Sentence("MayRecoverExtra", "{0}を追加で{1}回復しても良い", Resource, Formula),
        Sentence("Recover", "{0}を{1}回復する", Resource, Formula),
        Sentence("MayRecoverCharacter", "そのキャラクターの{0}を{1}回復しても良い", Resource, Formula),
        Sentence("MayRecoverSeveral", "{0}, {1}, {2}を{3}回復しても良い", Resource, Resource, Resource, Formula),
        Sentence("RecoverTwo", "自身の{0}と{1}を{2}回復しても良い", Resource, Resource, Formula),
        Sentence("RecoverParty", "味方全員の{0}を{1}回復する", Resource, Formula),
        Sentence("OptionalAdditionalDamage", "追加でさらに{0}ダメージを与えても良い", Formula),
        Sentence("DealBonusDamage", "追加で{0}のダメージを与える", Formula),
        Sentence("MayDamageTarget", "対象に{0}のダメージを与えても良い", Formula),
        Sentence("DamageTargetGroup", "同じ接近グループのキャラクター全てに追加で{0}のダメージを与える", Formula),
        Sentence("MayReduceDamage", "被ダメージを{0}軽減しても良い", Formula),
        Sentence("ReduceIncoming", "定数被ダメージ軽減効果を×{0}倍しても良い", RuleFactor),
        Sentence("SelfStatChange", "{0}は{1}される", Stat, Delta),
        Sentence("ExtraSelfStat", "{0}はさらに{1}される", Stat, Delta),
        Sentence("RacialStat", "{0}が{1}される", Stat, Delta),
        Sentence("SelfResourceChange", "自身の{0}は{1}される", Resource, Delta),
        Sentence("ResourceChange", "{0}は{1}される", Resource, Delta),
        Sentence("VariableCost", "消費{0}が{1}される", Resource, Delta),
        Sentence("ResourceCost", "{0}消費は{1}される", Resource, Delta),
        Sentence("MultiplyResourceCost", "{0}消費は×{1}倍に軽減される", Resource, RuleFactor),
        Sentence("CategoryCostDelta", "自身の〈{0}〉による消費{1}は{2}される", Label, Resource, Delta),
        Sentence("FirstCategoryCost", "戦闘開始{0}ターン目に宣言する最初の〈{1}〉による消費{2}は{3}される", RuleAmount, Label, Resource, Delta),
        Sentence("MayExchangeResource", "{0}を{1}消費して{2}を+{3}回復しても良い", Resource, RuleAmount, Resource, RuleAmount),
        Sentence("TargetRollDelta", "対象の発動ロールの達成値を{0}させる", Delta),
        Sentence("StatRollBonus", "自身の[{0}]による達成値は+{1}される", Stat, Formula),
        Sentence("CheckResultDelta", "達成値は{0}される", Delta),
        Sentence("ActionResultDelta", "〈{0}〉による達成値は{1}される", Label, Delta),
        Sentence("RollAtStat", "[{0}]による行為判定の達成値は{1}される", Stat, Delta),
        Sentence("MultiplyStatCheck", "{0}による達成値は×{1}倍される", Stat, RuleFactor),
        Sentence("StatAndCheck", "[{0}]が×{1}倍され、[{2}]による行為判定の達成値は{3}される", Stat, RuleFactor, Stat, Delta),
        Sentence("StatActionResult", "自身の[{0}]による発動ロールの達成値は{1}される", Stat, Delta),
        Sentence("StatRollFactor", "自身の[{0}]による達成値は×{1}倍される", Stat, RuleFactor),
        Sentence("MultiplySlotAction", "〈{0}〉による威力は{1}倍される", Label, RuleFactor),
        Sentence("MultiplyWeapon", "この武器による威力は{0}倍される", RuleFactor),
        Sentence("ElementalBonus", "この武器による威力には、{0}の{1}属性威力が加算される", Formula, Label),
        Sentence("WeaponElementPower", "この武器による威力は{0}の{1}属性威力が加算される", Formula, Label),
        Sentence("NextElementalBonus", "このターンの次の{0}回[{1}]よる属性威力は{2}倍される", RuleAmount, Stat, RuleFactor),
        Sentence("DamageFactor", "与ダメージは×{0}倍される", RuleFactor),
        Sentence("OverrideWeaponPower", "武器威力は{0}として扱われる", Formula),
        Sentence("StateOutgoingResourceDamage", "自身の与{0}ダメージは×{1}倍される", Resource, RuleFactor),
        Sentence("StateDamageScale", "自身の状態による被ダメージは×{0}倍される", RuleFactor),
        Sentence("OwnDefenseScale", "自身の防御点は×{0}倍される", RuleFactor),
        Sentence("DefenseScale", "自身の防御点は×{0}倍して扱われる", RuleFactor),
        Sentence("WeaponStrengthPerStack", "この武器による威力は×{0}倍される", Formula),
        Sentence("SelfStatPerStack", "自身の[{0}]は+{1}上昇する", Stat, Formula),
        Sentence("SelfStatDecrease", "自身の{0}は{1}減少する", Stat, Delta),
        Sentence("StatStackResult", "自身の行為判定の達成値は+{0}加算される", Formula),
        Sentence("OwnStatScale", "自身の[{0}]による達成値はは×{1}倍される", Stat, Formula),
        Sentence("ApplyTargetState", "対象に《{0}》状態を付与する", StateRef),
        Sentence("SetTargetState", "対象を《{0}》状態にする", StateRef),
        Sentence("GiveTargetState", "対象に《{0}》を付与する", StateRef),
        Sentence("AddTargetTagStack", "対象に《{0}》を{1}スタック付与する", StateRef, RuleAmount),
        Sentence("GiveTargetTimedState", "対象に《{0}》状態を{1}ターン付与する", StateRef, RuleAmount),
        Sentence("ApplyTargetTimedState", "対象に{0}ターンの間《{1}》を付与する", RuleAmount, StateRef),
        Sentence("SetTargetTimedState", "対象を{0}ターンの間《{1}》状態にする", RuleAmount, StateRef),
        Sentence("TargetEntersTimedState", "対象は{0}ターンの間《{1}》状態となる", RuleAmount, StateRef),
        Sentence("TargetGainsStacks", "対象に《{0}》スタックを{1}付与する", StateRef, Formula),
        Sentence("OptionalBonusState", "追加で《{0}》状態を付与する", StateRef),
        Sentence("OwnState", "自身は《{0}》状態になる", StateRef),
        Sentence("OwnTimedState", "自身は{0}ターンの間《{1}》状態になる", RuleAmount, StateRef),
        Sentence("OwnPermanentState", "永続的に《{0}》状態になる", StateRef),
        Sentence("GainStack", "《{0}》スタックを{1}得る", StateRef, Formula),
        Sentence("OptionalExtraStacks", "追加で《{0}》スタック{1}つ得ても良い", StateRef, RuleAmount),
        Sentence("ExtraStacksForSkill", "《{0}》スタックを追加で{1}獲得する", StateRef, Formula),
        Sentence("PermanentStackLimit", "永続的に《{0}》スタックを{1}得る。（最大{2}スタックまで）", StateRef, RuleAmount, RuleAmount),
        Sentence("PermanentStacks", "《{0}》スタックを永続的に{1}得る", StateRef, RuleAmount),
        Sentence("NoState", "自身は《{0}》状態を無効にできる", StateRef),
        Sentence("ResetStateStacks", "《{0}》スタックは{1}になる", StateRef, RuleAmount),
        Sentence("CancelLowResult", "達成値が{0}以下ならそれを無効にする", RuleAmount),
        Sentence("PreventStateRemoval", "自身は《{0}》に解除効果を受ける事が出来ない", StateRef),
        Sentence("PreventLeaveState", "自身は《{0}》状態を解除する事は出来ない", StateRef),
        Sentence("MayDispelCategory", "対象の〈{0}〉状態を一つ選択して解除しても良い", Label),
        Sentence("FreeHands", "両手は素手として扱われる"),
        Sentence("OptionalUnarmedWeapon", "この武器は素手として扱っても良い"),
        Sentence("AreaEnvironment", "このエリアは常に環境《{0}》として扱われる", StateRef),
        Sentence("ModifyAreaCheckTarget", "このエリアのキャラクターの[{0}]による目標値は×{1}倍される", Stat, RuleFactor),
        Sentence("AddCarriedCount", "所持出来る〈{0}〉の個数が+{1}増加する", Label, RuleAmount),
        Sentence("AddCarriedArrows", "所持出来る〈{0}〉の本数が+{1}増加する", Label, RuleAmount),
        Sentence("StandbyEnemyRollDelta", "スタンバイフェーズの敵の敏捷ロールの達成値合計は{0}される", Delta),
        Sentence("NonStackable", "この効果は重複しない"),
        Sentence("RepeatActive", "{0}回アクティブ効果を適用する事が出来る", RuleAmount),
        Sentence("RefundConsumedItems", "アクティブ効果の適用後に消費した分が所持品に戻る"),
        Sentence("PreventDurabilityLoss", "装備の耐久値が減少しなくなる"),
        Sentence("CountdownTitle", "称号《{0}》を得る", Label),
        Sentence("DamageSelfAtTurnStart", "自身は自身のターン開始時に{0}のダメージを受ける", Formula),
        Sentence("IgnoreDefense", "攻撃対象の防御点を{0}点無視する", RuleAmount),
        Sentence("EffectiveRepeatedTurns", "{0}ターン連続で発動した事にしても良い", RuleAmount),
        Sentence("ExtraHarvest", "〈{0}〉エリアで{1}日{2}回まで追加{3}回採取を行うことができる", Label, RuleAmount, RuleAmount, RuleAmount),
        Sentence("NextCastPower", "次の{0}回、自身の〈{1}〉による威力は×{2}倍される", RuleAmount, Label, RuleFactor),
        Sentence("SubstituteItemRecipient", "キャラクターを{0}体選択して自身の代わりに効果を適用できる", RuleAmount),
        Sentence("GroupOrderRoll", "味方の全員の達成値の平均にさらに{0}を加算しても良い", Delta),
        Sentence("TargetNumberWeaponAttack", "武器攻撃を行うスキルの目標値は{0}される", Delta),
        Sentence("DefenseTargetFactor", "この武器攻撃に対する〈{0}〉の目標値は×{1}倍される", Label, RuleFactor),
        Sentence("DefenseReductionFactor", "この武器攻撃に対する〈{0}〉の被ダメージ軽減量は×{1}倍される", Label, RuleFactor),
        Sentence("SanDamageReplacement", "代りにSANダメージを与える"),
        Sentence("CannotReduceDamage", "この効果による被ダメージは軽減する事は出来ない"),
        Sentence("IgnoresStateNegation", "この武器による〈{0}〉は《{1}》状態に効果を無効にされない", Label, Label),
        Sentence("PowerAgainstCategory", "この武器による〈{0}〉は〈{1}〉に対して威力が×{2}倍される", Label, Label, RuleFactor),
        Sentence("FirstBattleField", "自身はエリアに〈{0}〉が含まれない戦闘エリアへ移動する事が出来ない", Label),
        Sentence("RestrictedUnequipWeekday", "この装備は現実時間の{0}曜日にしか解除が出来ない", Label),
        Sentence("ResourceRecoveryEndCombat", "終了時に{0}を{1}回復しても良い", Resource, Formula),
        Sentence("ApplyEquipmentHitState", "この武器によって自身と同じ接近グループの対象にダメージを与えると《{0}》状態を付与する", StateRef),
        Sentence("WeaponTagHitState", "この武器による攻撃対象が〈{0}〉なら《{1}》を{2}ターン付与する", Label, StateRef, RuleAmount),
        Sentence("RestoreOnDamage", "さらに対象の{0}を{1}回復させても良い", Resource, Formula),
        Sentence("TargetHealingDealsDamage", "その回復量の{0}倍のダメージを与える", RuleFactor),
        Sentence("HealLessAndPay", "その回復量は×{0}倍され、{1}を{2}失う", RuleFactor, Resource, RuleAmount),
        Sentence("SanDamageExtra", "さらに{0}のSANダメージを受ける", RuleAmount),
        Sentence("NullifyTraits", "自身の特性による効果は無効になる"),
        Sentence("DisableAllActions", "眠りに落ちて一切の行動ができなくなります"),
        Sentence("RecoverResourceOnTurn", "ターン開始時{0}が+{1}追加される", Resource, RuleAmount),
        Sentence("StateActionPower", "自身の〈{0}〉による威力は×{1}倍される", Label, RuleFactor),
        Sentence("StateDualCategoryPower", "自身の〈{0}〉かつ〈{1}〉の威力は{2}倍される", Label, Label, RuleFactor),
        Sentence("StateResourceCost", "自身の〈{0}〉による{1}の消費は{2}になる", Label, Resource, RuleAmount),
        Sentence("SpendStackForDiscount", "スタックを{0}消費する事で〈{1}〉による{2}を{3}軽減する事が出来る", RuleAmount, Label, Resource, RuleAmount),
        Sentence("MinimumStacks", "※スタックは{0}を下回らない", RuleAmount),
        Sentence("ExpireAfterApplication", "*効果適用後この効果は解除される"),
        Sentence("MoreTargets", "自身の《{0}》による対象選択は追加でキャラクターをもう{1}体選ぶことができる", Label, RuleAmount),
        Sentence("PassThroughArmor", "自身の〈{0}〉によるダメージは防御点を{1}無視する", Label, RuleAmount),
        Sentence("OnEquipHpChange", "装備時{0}が{1}減少する", Resource, RuleAmount),
        Sentence("AddGatheringRoll", "自身は《{0}》によって採取を行う際、達成値を+{1}しても良い", Label, RuleAmount),
        Sentence("IgnoreInjuryState", "自身は重症ロールによる《{0}》を無効にする", Label),
        Sentence("ModifyStateStackLoss", "自身に付与された《{0}》スタックの減少量は{1}倍される", StateRef, RuleFactor),
        Sentence("AreaEncounterProgress", "自身の{0}エンカウントによる進行ロールの達成値は×{1}倍される", Label, RuleFactor),
        Sentence("ProgressRoll", "進行ロールによる達成値は×{0}倍される", RuleFactor),
        Sentence("TargetReductionCost", "この効果を解除する対象は{0}を追加で{1}払う必要がある", Resource, RuleAmount),
        Sentence("SetRollOutcome", "発動ロールは{0}となる", StateRef),
        Sentence("DoubleCriticalEffect", "アクティブ効果のクリティカル効果を二重に受けることができる"),
        Sentence("ReduceMaxHpByPower", "威力と同値の対象の最大体力を減少させる"),
        Sentence("DamageSelfUnreducibleTurn", "自身は毎ターン開始時{0}のダメージを受ける。この効果によるダメージは軽減も無効化もされない", Formula),
        Sentence("AcquisitionCheck", "〈{0}〉による効果をもう一度受ける", Label),
        Sentence("AllowUnlearnedCategory", "さらに未習得かつ装備していない全ての〈{0}〉を宣言する事が出来る", Label),
        Sentence("AddJudgementTarget", "あらゆる行為判定の目標値が{0}される", Delta),
        Sentence("ExtraGroupTarget", "自身を除く、自身と同じ接近グループのキャラクター全員の{0}による行為判定の目標値は{1}加算される", Stat, Delta),
        Sentence("ManaBoostAfterSkill", "以降の《{0}》の達成値は×{1}倍される", Label, RuleFactor),
        Sentence("AdvancedMagicalSummon", "戦闘開始から{0}ターン目まで、自身が〈{1}〉を〈{2}〉する場合、その達成値は×{3}倍、消費{4}は{5}される", RuleAmount, Label, Label, RuleFactor, Resource, Delta),
        Sentence("PermanentMaxLoss", "スタックが{0}溜まるごとに自身の{1}最大値は恒久的に{2}されてゆく", RuleAmount, Resource, Delta),
        Sentence("CaptureSkillSlot", "自身の装備スキルが〈{0}〉のみの場合、{1}×{2}", TagList, Stat, RuleFactor),
        Sentence("ResourceCostSet", "自身の《{0}》による消費{1}は{2}になる", Label, Resource, RuleAmount),
        Sentence("NamedResourceCost", "自身の《{0}》及び《{1}》の消費{2}は{3}される", Label, Label, Resource, Delta),
        Sentence("OnSkillConsumeNamed", "自身が《{0}》を宣言する時、《{1}》の消費量は×{2}倍される", Label, Label, RuleFactor),
        Sentence("DurabilityStack", "自身の耐久ロールの度に永続的に《{0}》スタックを{1}得る", StateRef, Formula),
        Sentence("CombatTurnGainStack", "自身は戦闘開始時に《{0}》×{1}を消費して《{2}》スタックを{3}得ても良い", Label, RuleAmount, StateRef, RuleAmount),
        Sentence("NextActivationStacks", "自身は《{0}》スタックを{1}分増減する", StateRef, Formula),
        Sentence("ActivationTargetStacks", "対象に《{0}》スタックを{1}分増減する", StateRef, Formula),
        Sentence("StackCostOnReference", "この武器威力が参照されるとき《{0}》スタックは{1}減少する", StateRef, RuleAmount),
        Sentence("AfterStackUse", "適用後スタックは{0}される", Delta),
        Sentence("ResourceOnStandby", "自身のはスタンバイフェーズ《{0}》スタックを{1}得る", StateRef, Formula),
        Sentence("EquipSingleStat", "{0}{1}", Stat, Delta),
        Sentence("SpecialCounterTarget", "カウンター効果の発動ロールの目標値は{0}される", Delta),
        Sentence("LimitPeakRoll", "発動ロールを除く、{0}を含むダイスロールの結果を+{1}加算しても良い", Stat, RuleAmount),
    };

    private static readonly Dictionary<string, DataBlockSentencePattern> TriggersByKey =
        Triggers.ToDictionary(rule => rule.Key, StringComparer.Ordinal);
    private static readonly Dictionary<string, DataBlockSentencePattern> OperationsByKey =
        Operations.ToDictionary(rule => rule.Key, StringComparer.Ordinal);

    internal static bool TryReadConditional(string source, out EffectContent effect)
    {
        string text = source.Trim().TrimEnd('。').Replace("してもよい", "しても良い").Replace("更に", "さらに")
            .Replace("によう発動ロール", "による発動ロール")
            .Replace("、●", "、").Replace("スキルを発動する場合する時", "スキルを発動する時");
        if (text.StartsWith("の武器の選択を条件に", StringComparison.Ordinal)) text = "こ" + text;
        if (text.StartsWith("自身がこの武器の選択を条件に", StringComparison.Ordinal)) text = text.Substring(3);
        text = RegexReplace(text, @"^(この武器の選択を条件に〈[^〉]+〉)を発動する(?:際|場合する時)、", "$1を発動する時、");
        text = RegexReplace(text, @"(自身は[0-9]+ターンの間)は([^《》。]+)状態になる$", "$1《$2》状態になる");
        text = text.Replace("状態する", "状態にする");
        if (ReadOperation(text, "Always", Array.Empty<string>(), out effect)) return true;
        // Only split at a separator if the entire left side names a recognized trigger.
        for (int i = 0; i < text.Length; i++)
        {
            if ("、,。".IndexOf(text[i]) < 0 || i == text.Length - 1) continue;
            string left = text.Substring(0, i).Trim();
            foreach (var trigger in Triggers)
            {
                var values = trigger.Read(left);
                if (values != null && ReadOperation(text.Substring(i + 1).Trim(), trigger.Key, values, out effect))
                    return true;
            }
        }
        return false;
    }

    private static bool ReadOperation(string text, string trigger, string[] arguments, out EffectContent effect)
    {
        foreach (var operation in Operations)
        {
            if (operation.Key == "EquipSingleStat" && trigger == "Always") continue;
            var values = operation.Read(text);
            if (values == null) continue;
            effect = trigger == "Always" ? UnconditionalContent(operation.Key, values) : null;
            if (effect == null)
                effect = Effect(EffectContentType.ConditionalRule, new[] { trigger, operation.Key }.Concat(arguments).Concat(values).ToArray());
            return true;
        }
        effect = null;
        return false;
    }

    // Standalone state changes carry the same operands as changes in skill text.
    // A triggered operation retains its trigger arguments and continuation scope.
    private static EffectContent UnconditionalContent(string operation, string[] values)
    {
        switch (operation)
        {
            case "OwnState": return Effect(EffectContentType.ApplyState, "Self", values[0]);
            case "OwnTimedState": return Effect(EffectContentType.ApplyState, "Self", values[1], values[0]);
            case "ApplyTargetState":
            case "SetTargetState":
            case "GiveTargetState": return Effect(EffectContentType.ApplyState, "Target", values[0]);
            case "GiveTargetTimedState": return Effect(EffectContentType.ApplyState, "Target", values[0], values[1]);
            case "ApplyTargetTimedState":
            case "SetTargetTimedState":
            case "TargetEntersTimedState": return Effect(EffectContentType.ApplyState, "Target", values[1], values[0]);
            case "GainStack": return Effect(EffectContentType.GainStack, "Self", values[0], values[1]);
            case "AddTargetTagStack":
            case "TargetGainsStacks": return Effect(EffectContentType.GainStack, "Target", values[0], values[1]);
            case "StateRollMultiplier": return Effect(EffectContentType.StatRollFactorForAction, values);
            case "StatRollFactor": return Effect(EffectContentType.CheckByAbilityFactor, values);
            case "OwnDefenseScale": return Effect(EffectContentType.ModifyStat, "Self", "防御点", "Multiply", values[0]);
            default: return null;
        }
    }

    internal static string ConditionalText(EffectContent effect)
    {
        if (effect.Type != EffectContentType.ConditionalRule) return null;
        var p = effect.Parameters;
        if (p == null || p.Count < 2 || !OperationsByKey.TryGetValue(p[1], out var operation))
            throw new InvalidOperationException("効果の操作が不正です。");
        if (p[0] == "Always")
        {
            if (operation.Key == "EquipSingleStat")
                throw new InvalidOperationException("条件のない能力値加算はModifyStatで指定してください。");
            return operation.Write(p.Skip(2).ToList()) + "。";
        }
        if (!TriggersByKey.TryGetValue(p[0], out var trigger))
            throw new InvalidOperationException("効果の発動条件が不正です。");
        return trigger.Write(p.Skip(2).Take(trigger.ParameterCount).ToList()) + "、" +
            operation.Write(p.Skip(2 + trigger.ParameterCount).ToList()) + "。";
    }

    internal static IEnumerable<string> ReferencedRuleStates(EffectContent effect)
    {
        if (effect.Type != EffectContentType.ConditionalRule) yield break;
        var args = effect.Parameters;
        int offset = 2;
        foreach (var key in args.Take(2))
        {
            if (StateArgumentIndexes.TryGetValue(key, out var indices))
                foreach (var index in indices) yield return args[offset + index];
            if (TriggersByKey.TryGetValue(key, out var trigger)) offset += trigger.ParameterCount;
        }
    }
}
