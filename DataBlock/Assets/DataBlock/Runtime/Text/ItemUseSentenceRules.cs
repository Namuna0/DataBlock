public static partial class SkillTextConverter
{
    private static void RegisterItemActiveRules(RuleSection rules)
    {
        rules.Add(EffectContentType.RepairEquippedCategory, "〈{0}〉の耐久を{1}回復する。", RuleNamePattern, ArithmeticPattern);
        rules.Add(EffectContentType.RestartAreaAfterDefeat, "《{0}》の最後に全滅した階層と進行ロールからやり直すことができる。", RuleNamePattern);
        rules.Add(EffectContentType.IgnoreEnvironmentUntilLeaving, "このエリアを移動するまで、環境効果《{0}》及び《{1}》の効果を無効にする。", RuleNamePattern, RuleNamePattern);
        rules.Add(EffectContentType.ApplyStateAfterPoolBelow, "このダメージによって対象の{0}が{1}を下回った時、{2}ターンの間対象を《{3}》状態にする。", RuleResourcePattern, RulePositivePattern, RulePositivePattern, RuleNamePattern);
        rules.Add(EffectContentType.DamagePartyWithTrait, "パーティメンバーの《{0}》は{1}の{2}と{3}ダメージを受ける。", RuleNamePattern, ArithmeticPattern, RuleResourcePattern, RuleResourcePattern);
        rules.Add(EffectContentType.ReturnPartyToLastTown, "パーティーメンバー全員は最後に訪れた町エリアに移動する。");
        rules.Add(EffectContentType.NamedTargetFleeAndGrantItem, "対象が《{0}》の場合、戦闘を離脱して《{1}》×{2}を得る。", RuleNamePattern, RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.StackExceptNamedTarget, "対象が《{0}》ではない場合、対象へ永続的に《{1}》スタックを×{2}付与する。", RuleNamePattern, RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.DailyUseDepletion, "一日に{0}回以上服用すると{1}最大値が{2}になり、翌日まで《{3}》状態になる。", RulePositivePattern, RuleResourcePattern, @"[0-9]+", RuleNamePattern);
        rules.Add(EffectContentType.PartyPhaseAbilityScale, "敵に〈{0}〉が含まれている場合、味方全員の{1}フェーズと{2}フェーズの[{3}B]及び[{4}B]による達成値は×{5}倍される。", RuleNamePattern, RuleNamePattern, RuleNamePattern, RuleNamePattern, RuleNamePattern, RuleFactorPattern);
        rules.Add(EffectContentType.OnSpellHitDamagePerStack, "対象魔法が命中した時、付与されている《{0}》スタック×{1}の{2}を減少させる。", RuleNamePattern, ArithmeticPattern, RuleResourcePattern);
        rules.Add(EffectContentType.RemoveOneStateOfCategories, "自身に付与されている〈{0}〉を1つ選択して解除する。", RuleNamePattern);
        rules.Add(EffectContentType.RemoveNamedStates, "自身に付与されている《{0}》状態を全て解除する。", RuleNamePattern);
        rules.Add(EffectContentType.DeclarePhaseActions, "自身は〈{0}〉の行動を{1}つ宣言できる。", RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.IgnoreCurrentAreaEnvironment, "自身はこのエリア効果の《{0}》から受ける効果を無効にする。", RuleNamePattern);
        rules.Add(EffectContentType.TravelToSelectedDestination, "自身は対象の〈{0}〉エリアの行先を一つ選択してエリアを移動する。", RuleNamePattern);
        rules.Add(EffectContentType.TravelByTransport, "{0}で移動できる行先へ移動する。", RuleNamePattern);
        rules.Add(EffectContentType.ReturnItemAfterUse, "処理後この《{0}》は{1}に返納される。", RuleNamePattern, RuleNamePattern);
        rules.Add(EffectContentType.LearnTitle, "称号《{0}》を得る。", RuleNamePattern);
        rules.Add(EffectContentType.LearnTitleAtSuccessCount, "{0}回以上達成で称号《{1}》を習得する。", RulePositivePattern, RuleNamePattern);
        rules.Add(EffectContentType.GrantClassifiedReward, "{0}《{1}》×{2}を獲得する。", @"スキル|ペット", RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.GrantPersonalWeapon, "キャラクター専用武器を獲得する。");
        rules.Add(EffectContentType.GainPhaseProgress, "このエリアの進行ロールは×{0}倍される。", RuleFactorPattern);
    }

    private static void RegisterItemCounterRules(RuleSection rules)
    {
        rules.Add(EffectContentType.RerollProduction, "{0} 目標値{1}で{2}を振りなおす事が出来る。", ArithmeticPattern, RulePositivePattern, RuleNamePattern);
        rules.Add(EffectContentType.NoFurtherReroll, "この効果に対してさらに振りなおしは不可。");
        rules.Add(EffectContentType.GradeOnChangedSuccess, "その際に失敗から成功へ変化したなら成果物のグレードは{0}される。", RuleSignedPattern);
        rules.Add(EffectContentType.NamedSkillHarvestDelta, "《{0}》によって採取するアイテムの個数は{1}される。", RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.NamedSkillAchievementFactor, "《{0}》による達成値は×{1}倍される。", RuleNamePattern, RuleFactorPattern);
        rules.Add(EffectContentType.ConsumedItemQuantityDelta, "《{0}》の消費が{1}される。", RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.BonusYieldOnCritical, "クリティカルを出した時、個数が{0}される。", RuleSignedPattern);
        rules.Add(EffectContentType.FixDiceFaces, "ダイス目を{0}つ「{1}」に固定できる。", RulePositivePattern, RulePositivePattern);
        rules.Add(EffectContentType.IncreaseProducedGrade, "生産したアイテムのグレードを{0}する。", RuleSignedPattern);
        rules.Add(EffectContentType.OptionalProductionRollDelta, "生産のダイスロール{0}回分の達成値を{1}しても良い。", RulePositivePattern, RuleSignedPattern);
        rules.Add(EffectContentType.ReviveAndRecoverFlat, "自身の《{0}》状態を解除して、{1}を{2}回復する。", RuleNamePattern, RuleResourcePattern, ArithmeticPattern);
        rules.Add(EffectContentType.ClaimQuestRewardsIgnoringLevel, "自身のレベルに関わらず、適正レベル{0}～{1}のクエストによる{2}報酬を受け取ることが出来る。", RulePositivePattern, RulePositivePattern, RuleNamePattern);
        rules.Add(EffectContentType.ClaimQuestRewardsAtLevel, "適正レベル{0}のクエストによる{1}報酬を受け取ることが出来る。", RulePositivePattern, RuleNamePattern);
    }

    private static void RegisterItemPassiveRules(RuleSection rules)
    {
        rules.Add(EffectContentType.ConsumeOnMessage, "RPによって、相手からのメッセージを受け取った時、このアイテムは消費される。");
        rules.Add(EffectContentType.UseWithoutInventoryEquip, "このアイテムは所持品に装備していなくても、〈{0}〉から使用する事が出来る。", RuleNamePattern);
        rules.Add(EffectContentType.JoinBattleWhileHeld, "このアイテムを所持している場合、味方キャラクターとして扱い戦闘に参加する。");
        rules.Add(EffectContentType.TreatNamedItemAsGrade, "このアイテムを所持している状態で《{0}》を使用した場合、グレードを☆{1}として扱っても良い。", RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.SacrificeAsMultipleCards, "このカードを生贄に捧げる時、カード{0}枚分として扱うことができる。", RulePositivePattern);
        rules.Add(EffectContentType.HalveRescueFee, "アイテムを所持している場合、すべてのクエストの救助費を半額にする。");
        rules.Add(EffectContentType.HalveRescueFeeBelowLevel, "アイテムを所持している場合、推奨レベル{0}以下のクエストの救助費を半額にする。", RulePositivePattern);
        rules.Add(EffectContentType.TradeInventorySlots, "サイズ{0}の所持品枠を{1}個分使ってサイズ{2}のアイテムを{3}つ追加で所持できる。", RuleNamePattern, RulePositivePattern, RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.MountedInventoryCapacity, "このアイテムを所持してペットに騎乗している間、サイズ{0}のアイテムの所持数がそれぞれ{1}される。", RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.MountedEnvironmentImmunity, "このアイテムを所持してペットに騎乗している間、環境効果《{0}》を受けない。", RuleNamePattern);
        rules.Add(EffectContentType.OptionalCategoryActivationScale, "自身が〈{0}〉を宣言した時、発動ロール{1}つの達成値を×{2}倍しても良い。", RuleNamePattern, RulePositivePattern, RuleFactorPattern);
        rules.Add(EffectContentType.RestoreResourceOnEitherCategory, "自身が〈{0}〉及び〈{1}〉を宣言した時、自身の{2}を{3}回復する。", RuleNamePattern, RuleNamePattern, RuleResourcePattern, ArithmeticPattern);
        rules.Add(EffectContentType.OptionalActionTargetFactor, "〈{0}〉による発動ロールの目標値を×{1}倍しても良い。", RuleNamePattern, RuleFactorPattern);
        rules.Add(EffectContentType.ShopInHousing, "設置されたハウジングエリアに訪れたキャラクターは、ハウジングエリアの所有者が設定した〈{0}〉アイテム、価格、在庫数から、買取取引を行うことができる。", RuleNamePattern);
        rules.Add(EffectContentType.AllowAbsentShopOwner, "取引は所有者が別エリアにいる、または不在でも成立する。");
    }

    private static void RegisterItemStateRules(RuleSection rules)
    {
        rules.Add(EffectContentType.BecomeStateAtStackCount, "スタックが{0}以上の時《{1}》状態となる。", RulePositivePattern, RuleNamePattern);
        rules.Add(EffectContentType.IncomingHealingScale, "自身が受ける{0}回復効果は{1}倍される。", RuleResourcePattern, RuleFactorPattern);
        rules.Add(EffectContentType.CategoryAchievementDelta, "自身が発動する〈{0}〉の達成値は{1}される。", RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.AbilityReductionPerStack, "自身の[{0}B]はスタック×{1}減少する。", RuleNamePattern, RuleFactorPattern);
        rules.Add(EffectContentType.PartyOrSelfRollStackScale, "自身の〈{0}〉及び〈{1}〉の達成値は×{2}倍される。", RuleNamePattern, RuleNamePattern, ArithmeticPattern);
        rules.Add(EffectContentType.TwoCategoryFlatPower, "〈{0}〉かつ〈{1}〉の威力は{2}加算される。", RuleNamePattern, RuleNamePattern, ArithmeticPattern);
        rules.Add(EffectContentType.AllAbilityAndResourceScale, "全ての能力値Bとリソース最大値は×{0}倍される。", RuleFactorPattern);
        rules.Add(EffectContentType.AllElementBonusScale, "自身の属性値Bは×{0}倍される。", RuleFactorPattern);
        rules.Add(EffectContentType.ApplierControlsActions, "自身のターン、行動と選択対象は付与者によって選択される。");
        rules.Add(EffectContentType.OnCategoryDepletionRecover, "〈{0}〉によって自身の{1}が0になる時、《{2}》状態を解除して{3}が全回復する。", RuleNamePattern, RuleResourcePattern, RuleNamePattern, RuleResourcePattern);
        rules.Add(EffectContentType.RemoveNamedStateOnTurnStart, "ターン開始時、自身の《{0}》状態を解除する。", RuleNamePattern);
        rules.Add(EffectContentType.ResourcePairMaximumScale, "自身の{0}及び{1}最大値は×{2}倍される。", RuleResourcePattern, RuleResourcePattern, RuleFactorPattern);
        rules.Add(EffectContentType.SkillCategoryDamageFactor, "自身の〈{0}〉スキルによるダメージは×{1}倍される。", RuleNamePattern, RuleFactorPattern);
    }

    private static string NormalizeItemUseSentence(string text)
    {
        text = text.TrimEnd('．').Replace("ダメ―ジ", "ダメージ");
        text = RegexReplace(text, @"^(?:さらに)?([0-9]+d[0-9]+(?:\*[0-9.]+)?)の威力で対象を攻撃する。?$", "対象を$1の威力で攻撃する。");
        text = RegexReplace(text, @"^(?:その後|さらに)戦闘を離脱する。?$", "自身は戦闘を離脱する。");
        text = RegexReplace(text, @"^対象からの(〈[^〉]+〉)を無効にする。?$", "対象の$1を無効にする。");
        text = RegexReplace(text, @"^([0-9]+)回以上達成で称号《([^》]+)》を習得。?$", "$1回以上達成で称号《$2》を習得する。");
        text = RegexReplace(text, @"^(?:自身は)?(《[^》]+》×[0-9]+)を得る。?$", "$1獲得");
        if (text.TrimEnd('。') == "なにもなし") return "何もなし。";
        if (text.TrimEnd('。') == "キャラクター専用武器プレゼント") return "キャラクター専用武器を獲得する。";
        text = RegexReplace(text, @"^(スキル|ペット)(《[^》]+》×[0-9]+)。?$", "$1$2を獲得する。");
        text = RegexReplace(text, @"^自身は永続的に(《[^》]+》)状態となる。?$", "自身へ永続的に$1状態を付与する。");
        return text;
    }
}
