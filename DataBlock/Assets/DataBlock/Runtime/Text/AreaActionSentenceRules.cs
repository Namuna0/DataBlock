using System.Collections.Generic;
using System.Linq;

internal static partial class AreaActionTextCodec
{
    // Conditions and effects specific to area progression, including named environments.
    private static readonly DataBlockSentencePattern[] Rules =
    {
        new DataBlockSentencePattern("ProgressCountByOwnCategory", "自身が〈{0}〉の場合、進行ロールの回数を{1}しても良い。", Token, @"[+-][0-9]+"),
        new DataBlockSentencePattern("TravelAfterRolling", "進行ロール後、次のエリアへ移動可能。"),
        new DataBlockSentencePattern("RequireMenuCategorySet", "{0}の食事から{1}", Token, @"〈[^〉]+〉(?:\+〈[^〉]+〉)+"),
        new DataBlockSentencePattern("ReplaceWipeRollOnStates", "このエリアで《{0}》及び《{1}》状態になった時、全滅ロールの代わりに以下の処理を行います。", Token, Token),
        new DataBlockSentencePattern("RevivalPenalties", "経験値-{0}, 決意-{1}, 蘇生費として{2}ジルダ消費。", Nat, Nat, Nat),
        new DataBlockSentencePattern("RecoverClearStatesAndTravel", "リソースの全回復と全ての状態を解除して、{0}に移動します。", Token),
        new DataBlockSentencePattern("GatherAfterPower", "〈{0}〉成功後、威力{1}以上達成で《{2}》×{3}を獲得", Token, Nat, Token, Nat),
        new DataBlockSentencePattern("TravelAfterDefeatWithItems", "エンカウントのエネミーを{0}体以上撃破後, 《{1}》×{2}と《{3}》×{4}を所持しているなら。《{5}》へ移動する。", Nat, Token, Nat, Token, Nat, Token),
        new DataBlockSentencePattern("ExtraDice", "さらにダイスロール{0}", Dice),
        new DataBlockSentencePattern("ExtraRollTable", "さらに{0}ロールを行う。", Token),
        new DataBlockSentencePattern("ProgressCountChange", "進行ロールの回数を{0}する。", @"[+-][0-9]+"),
        new DataBlockSentencePattern("ApplyStateOnFumble", "ファンブルした場合、自身は永続的に《{0}》状態となる。", Token),
        new DataBlockSentencePattern("RequireUnlearnedSkill", "〈{0}〉未習得", Token),
        new DataBlockSentencePattern("RequireWeatherOtherThan", "農地エリア《{0}》の天候が{1}以外の時", Token, Token),
        new DataBlockSentencePattern("AloneAndPerAreaLimit", "パーティーメンバーが自分のみ、かつこのエリアで{0}回まで", Nat),
        new DataBlockSentencePattern("FreeAtLevel", "{0}レベル以上で無料", Nat),
        new DataBlockSentencePattern("FreeWithItem", "《{0}》所持で無料", Token),
        new DataBlockSentencePattern("UseEncounterTable", "{0}の進行ロールを行う。", Token),
        new DataBlockSentencePattern("AfterProgress", "進行ロール達成後"),
        new DataBlockSentencePattern("TravelAfterProgress", "進行ロール達成後、次のエリアへ移動可能。"),
        new DataBlockSentencePattern("TravelAfterEncounterProgress", "{0}による進行ロール達成後、次のエリアへ移動可能。", Token),
        new DataBlockSentencePattern("TravelAfterConsecutiveProgress", "このエリアの進行ロールを{0}回連続達成後、次のエリアへ移動可能。", Nat),
        new DataBlockSentencePattern("AfterBossDefeat", "ボス討伐後"),
        new DataBlockSentencePattern("AfterBossEncounter", "ボスエンカウント後"),
        new DataBlockSentencePattern("TravelAfterBossDefeat", "ボス討伐後、次のエリアへ移動可能。"),
        new DataBlockSentencePattern("TravelAfterBossEncounter", "ボスエンカウント後、次のエリアへ移動可能。"),
        new DataBlockSentencePattern("AfterDropResult", "ドロップロールで{0}を出す。", Nat),
        new DataBlockSentencePattern("AreaChoiceCount", "以下から{0}つ選択する。", Nat),
        new DataBlockSentencePattern("PartyCheckForProgress", "パーティーメンバー全員が{0}目標値{1}達成で進行ロールを{2}回振ることが出来る。", @"《[^》]+》|〈[^〉]+〉", Nat, Nat),
        new DataBlockSentencePattern("PartyCheckAfterProgress", "{0}の進行ロール達成後、パーティーメンバー全員が{1}目標値{2}達成で進行ロールを{3}回振ることが出来る。", Token, @"《[^》]+》|〈[^〉]+〉", Nat, Nat),
        new DataBlockSentencePattern("UseAreaEffect", "エリア効果の通り。"),
        new DataBlockSentencePattern("DailyAtRealWeekdays", "1日{0}回まで, 現実時間の{1}曜日または{2}曜日の午前{3}時{4}分のみ宣言可能。", Nat, "[月火水木金土日]", "[月火水木金土日]", Nat, Nat),
        new DataBlockSentencePattern("AllPartyHaveState", "パーティーメンバー全員が《{0}》状態である事。", Token),
        new DataBlockSentencePattern("AllPartyHaveCategoryOrState", "パーティーメンバー全員が〈{0}〉か《{1}》状態である事。", Token, Token),
        new DataBlockSentencePattern("ProhibitCategory", "〈{0}〉を宣言する事は出来ない。", Token),
        new DataBlockSentencePattern("ProhibitNamedSkills", "《{0}》を宣言する事は出来ない。", Token),
        new DataBlockSentencePattern("NamedCheckTargetMultiplier", "《{0}》による目標値は×{1}倍される。", Token, @"[0-9.]+"),
        new DataBlockSentencePattern("AbilityCheckAdjustment", "{0}による行為判定の達成値は{1}される。", Token, @"[+-][0-9]+"),
        new DataBlockSentencePattern("ProgressConsumesResource", "進行ロール1回につき、{0}を{1}消費する。", "[A-Z]+", Nat),
        new DataBlockSentencePattern("ProgressRequiresCheck", "進行ロール1回につき{0} 目標値{1}の行為判定を行う。", Dice, Nat),
        new DataBlockSentencePattern("ProgressCheckFailureCost", "失敗時{0}は-{1}される。", "[A-Z]+", Nat),
        new DataBlockSentencePattern("CannotProgressApplyState", "自身は移動条件を満たせず進行ロールも行えない時、《{0}》状態になる。", Token),
        new DataBlockSentencePattern("CannotJoinPartyWhenAlone", "パーティーを組んでいない場合、他のパーティーへ合流する事は出来ない。"),
        new DataBlockSentencePattern("DisableCritical", "このエリアではクリティカルが発生しない。"),
        new DataBlockSentencePattern("AreaEntryRoll", "エリア進入時、代表者は{0}のダイスロールを行う。", Dice),
        new DataBlockSentencePattern("TravelOnAnyState", "このエリアで《{0}》または《{1}》状態になる時、キャラクターは《{2}》へ移動する。", Token, Token, Token),
        new DataBlockSentencePattern("RemoveItemAfterExit", "このエリアから移動後パーティーメンバーはすべての《{0}》を削除する。", Token),
        new DataBlockSentencePattern("ApplyStateAtAreaNumber", "エリアの数字が{0}の時、キャラクターは《{1}》状態になる。", Nat, Token),
        new DataBlockSentencePattern("ArriveAfterTime", "宣言後、現実時間の{0}時以降に寄港。", Nat),
        new DataBlockSentencePattern("ScheduledDeparture", "宣言後、現実時間の{0}に出航", @"[0-9時半, ]+"),
        new DataBlockSentencePattern("TravelAfterPartyChoice", "パーティーメンバーのいずれかが、次の内{0}つ達成で《{1}》へ", Nat, Token),
        new DataBlockSentencePattern("RetryAreaOnFailure", "失敗した場合、エリア再挑戦が必要。"),
        new DataBlockSentencePattern("TravelOnAllFailed", "全て失敗した場合は《{0}》へ", Token),
        new DataBlockSentencePattern("TravelByCheckAfterProgress", "進行ロール達成後、{0} 目標値{1}達成で次のエリアへ移動可能。", Dice, Nat),
        new DataBlockSentencePattern("TravelBySkillAfterProgress", "進行ロール達成後、《{0}》目標値{1}達成で、次のエリアへ移動可能。", Token, Nat),
        new DataBlockSentencePattern("TravelByPaymentAfterProgress", "進行ロール達成後、パーティーメンバーのいずれかが《{0}》×{1}を消費して、次のエリアへ移動可能。", Token, Nat),
        new DataBlockSentencePattern("RemoveAllItemsAfterTravel", "移動後パーティーメンバーはすべての《{0}》を削除する。", Token),
        new DataBlockSentencePattern("TravelAtLevel", "現在のエリアが{0}層なら、次のエリアへ移動可能。", Nat),
        new DataBlockSentencePattern("AreaNumberEquals", "エリアの数字が{0}の場合。", Nat),
        new DataBlockSentencePattern("TravelTo", "《{0}》へ移動する。", Token),
        new DataBlockSentencePattern("SelectDestination", "次の内、いずれかのエリアへ移動可能。"),
        new DataBlockSentencePattern("LimitChoicesPerVisit", "このエリアで{0}回まで以下の選択肢を宣言可能。", Nat),
        new DataBlockSentencePattern("RequireCategoryOrState", "〈{0}〉または《{1}》", Token, Token),
        new DataBlockSentencePattern("NoReentryUntilExit", "このエリアは《{0}》から{1}度出るまで再入場できない。", Token, Nat),
        new DataBlockSentencePattern("RemoveGatheringOnFirstReward", "キャラクターがこのアイテムを初めて入手した時、この採取はデータから削除される。"),
        new DataBlockSentencePattern("RequirePowerByCategory", "〈{0}〉によって威力{1}以上達成", Token, Nat),
        new DataBlockSentencePattern("PerDayQuantity", "1日{0}個まで。", Nat),
        new DataBlockSentencePattern("PayItemForChoice", "《{0}》×{1}", Token, Nat),
        new DataBlockSentencePattern("EnterWithNamedSkill", "《{0}》を習得していない場合、進入不可", Token),
        new DataBlockSentencePattern("EnterWithEitherSkill", "《{0}》または《{1}》を習得していないキャラクターは進入不可", Token, Token),
        new DataBlockSentencePattern("PriceFactorWithoutNamedSkill", "《{0}》を習得していない場合、あらゆるジルダ消費量は×{1}倍される。", Token, @"[0-9.]+"),
        new DataBlockSentencePattern("AreaAlliesPersist", "戦闘終了時もリソースと永続状態は継続する。"),
        new DataBlockSentencePattern("AreaAllyRecoveryOnProgress", "進行ロールを達成するたびこのキャラクターのリソースと《{0}》状態は全回復する。", Token),
        new DataBlockSentencePattern("AddPartyMembers", "プレイヤーのパーティーに{0}が仲間となる。", @"《[^》]+》×[0-9]+(?:,\s*《[^》]+》×[0-9]+)*"),
        new DataBlockSentencePattern("AddPartyMemberDuringQuest", "クエスト《{0}》を受注している場合、プレイヤーのパーティーに《{1}》×{2}が仲間となる。", Token, Token, Nat),
        new DataBlockSentencePattern("OptionalEnemySkillPerTurn", "毎ターン{0}回任意の敵に対して《{1}》を行っても良い。", Nat, Token),
        new DataBlockSentencePattern("EnemySkillTurnLimit", "毎ターン{0}回まで任意の敵に対して《{1}》を宣言しても良い。", Nat, Token),
        new DataBlockSentencePattern("RequireAllAreaCharactersInPartyAndIdle", "エリアにいるすべてのキャラクターとパーティーを結成していて、かつ全員が戦闘中ではない時のみ、進行ロールを行うことができる。"),
        new DataBlockSentencePattern("CraftService", "{0} {1}で《{2}》を代行する。", @"[0-9.]+", Token, Token),
        new DataBlockSentencePattern("RemoveAllPermanentStates", "《{0}》を含む永続状態を全て解除", Token),
        new DataBlockSentencePattern("RepairEquipment", "装備{0}つの耐久を全回復", Nat)
    };

    private static bool TryRule(string text, out string key, out List<string> parameters)
    {
        foreach (var pattern in Rules)
        {
            var args = pattern.Read(text);
            if (args == null) continue;
            key = pattern.Key;
            parameters = args.ToList();
            return true;
        }
        key = "";
        parameters = new List<string>();
        return false;
    }

    private static string RuleText(string key, List<string> parameters) => Rules.Single(x => x.Key == key).Write(parameters);
}
