using System.Collections.Generic;

public static partial class SkillTextConverter
{
    private static void RegisterRecipeRules(List<SkillTextRule> list)
    {
        var active = new RuleSection(list, false, EffectType.Active);
        var declaration = new RuleSection(list, false, EffectType.Declaration);
        var state = new RuleSection(list, true, EffectType.Passive);
        state.Add(EffectContentType.ExpireAfterTwoAbilityRolls, "[{0}]及び[{1}]によるダイスロールを行った後、この状態は解除される。", RuleNamePattern, RuleNamePattern);
        state.Add(EffectContentType.NoEffect, "何もなし。");
        declaration.Add(EffectContentType.SubstituteIngredientForGrade, "グレードを{0}する代わりに《{1}》を〈{2}〉で代用可能。", RuleSignedPattern, RuleNamePattern, RuleNamePattern);
        active.Add(EffectContentType.NameAndWriteProducedItem, "このアイテムを{0}（[任意の名前]）の様に名付け、本文を覚えて置いてください。", RuleNamePattern);
        active.Add(EffectContentType.ReplaceProductForNamedTarget, "対象が《{0}》の場合、《{1}》を代わりに獲得します。", RuleNamePattern, RuleNamePattern);
        active.Add(EffectContentType.SkillValueFormulaWeaponAttack, AttackParts.Weapon, "対象をスキル値{0}の武器威力で攻撃する。", ArithmeticPattern);
        active.Add(EffectContentType.CureOnFullResourceUntilTownExit, "対象がこの町エリアを出るまでに{0}を全回復する時、任意の状態を{1}つ全て解除しても良い。", RuleResourcePattern, RulePositivePattern);
        active.Add(EffectContentType.GainExperienceAndDetermination, "経験値{0}, 決意{1}", RuleSignedPattern, RuleSignedPattern);
        active.Add(EffectContentType.TimedAbilityBonus, "{0}日の間、{1}B{2}。", RulePositivePattern, RuleNamePattern, @"[+-][0-9]+(?:\.[0-9]+)?");
        active.Add(EffectContentType.TimedCriticalThresholdTwoCategories, "{0}日の間、〈{1}〉かつ〈{2}〉による発動ロールでクリティカルする目は{3}される。", RulePositivePattern, RuleNamePattern, RuleNamePattern, RuleSignedPattern);
        active.Add(EffectContentType.TimedExtraRewardOnCheck, "{0}日の間、{1}と{2}によるダイスロールを行う際、追加で{3} 目標値{4}のダイスロールを行い、成功した時、追加でもう一度その報酬のダイスロールを行っても良い。", RulePositivePattern, RuleNamePattern, RuleNamePattern, ArithmeticPattern, RulePositivePattern);
        active.Add(EffectContentType.IncreaseDropsOnVictoryThisTurn, "このターン味方の勝利で戦闘が終わった時、対象のドロップロールのダイス目を{0}する。", RuleSignedPattern);
        active.Add(EffectContentType.SkipDurabilityOnAreaExit, "対象はこのエリアから移動する時、耐久ロールを行わなくてもよい。");
        state.Add(EffectContentType.StateTurnScope, "継続{0}ターン目{1}の効果。", RulePositivePattern, @"のみ|以降");
        state.Add(EffectContentType.RecoverTwoResourcesOnDecrease, "自身の{0}が減少した時、{1}と{2}は{3}回復する。", RuleResourcePattern, RuleResourcePattern, RuleResourcePattern, RulePositivePattern);
        state.Add(EffectContentType.ScaleRecipientCategoryAchievement, "{0}の〈{1}〉達成値は{2}倍される。", @"自身|対象", RuleNamePattern, RuleFactorPattern);
        state.Add(EffectContentType.AddRecipientResource, "対象の{0}が{1}加算される。", RuleResourcePattern, RuleSignedPattern);
        state.Add(EffectContentType.PreventResourceDecreaseThisTurn, "自身はこのターンリソースが減らない。");
        state.Add(EffectContentType.IgnoreEnemyStateEffects, "敵から付与された状態の効果を受けない。");
        state.Add(EffectContentType.SourceRepeatedWeaponAttack, "付与者の武器威力{0}の威力で{1}回連続自身は攻撃を受ける。", RulePositivePattern, RulePositivePattern);
        state.Add(EffectContentType.IncomingCategoryAchievement, "自身に対する〈{0}〉の達成値は{1}される。", RuleNamePattern, RuleSignedPattern);
        state.Add(EffectContentType.ScaleTwoAbilityChecks, "自身の[{0}]及び[{1}]による達成値は×{2}倍される。", RuleNamePattern, RuleNamePattern, RuleFactorPattern);
        active.Add(EffectContentType.TargetRollDice, "対象は{0}のダイスロールを行う。", @"[1-9][0-9]*d[1-9][0-9]*");
        active.Add(EffectContentType.GrantRolledFortuneUntilDayEnd, "ダイスロールの結果に応じて以下の占い効果を{0}日終了まで付与する。", RulePositivePattern);
        active.Add(EffectContentType.GrantAreaFortuneUntilDayEnd, "ダイスロールの結果に応じて以下の占い効果《{0}》を{1}日終了まで付与する。", RuleNamePattern, RulePositivePattern);
        active.Add(EffectContentType.FortuneOutcome, "占い出目{0}～{1}：《{2}》を適用。", @"下限|[1-9][0-9]*", @"上限|[1-9][0-9]*", RuleNamePattern);
        active.Add(EffectContentType.FortuneAreaOutcome, "占い出目{0}～{1}：エリア《{2}》。", @"下限|[1-9][0-9]*", @"上限|[1-9][0-9]*", RuleNamePattern);
        active.Add(EffectContentType.FortuneDeliveryBonus, "納品クエストを達成する時、追加で納品物を{0}納品すると、決意と経験値は{1}される。", RuleSignedPattern, RuleSignedPattern);
        active.Add(EffectContentType.FortuneEncounterAgility, "自身が〈{0}〉を含む敵とエンカウントした時、味方パーティーのエンカウントフェーズの敏捷ロールの平均値は{1}される。", RuleNamePattern, RuleSignedPattern);
        active.Add(EffectContentType.FortuneSaleMultiplier, "任意のキャラクターにアイテムを販売した時、獲得ジルダは×{0}倍される。", RuleFactorPattern);
        active.Add(EffectContentType.FortuneRoleplayReward, "任意のキャラクターと1対1でRPを行った時、相手は報酬で《{0}》×{1}を追加で得る。", RuleNamePattern, RulePositivePattern);
        active.Add(EffectContentType.FortuneAreaAchievement, "出たエリアで行う行為判定の達成値は{0}。", RuleSignedPattern);
        active.Add(EffectContentType.FortuneAreaRewardRoll, "出たエリアで行うクエスト特別報酬のダイス目は{0}される。", RuleSignedPattern);
        active.Add(EffectContentType.FortuneAreaRewardItem, "出たエリアで行うクエストの報酬に《{0}》×{1}を追加する。", RuleNamePattern, RulePositivePattern);
        active.Add(EffectContentType.CraftItemFromRolls, "発動ロールの結果に応じて《{0}》を獲得します。", RuleNamePattern);
        active.Add(EffectContentType.RequireSuccessesForProduct, "発動ロール{0}回以上達成で《{1}》×{2}獲得。", RulePositivePattern, RuleNamePattern, RulePositivePattern);
        active.Add(EffectContentType.GrantBonusAtOutputCount, "個数が{0}個以上の時は追加で《{1}》を×{2}獲得。", RulePositivePattern, RuleNamePattern, RulePositivePattern);
        active.Including(EffectType.Roleplay).Add(EffectContentType.RequireSuccessesForEffect, "達成{0}回以上で発動。", RulePositivePattern);
        active.Add(EffectContentType.ReplacePriorCategoryBuff, "対象に〈{0}〉カテゴリーの効果が既に付与されている場合、このバフで上書きする。", RuleNamePattern);
        active.Add(EffectContentType.ClassifiedRewardTableItem, "出目{0}～{1}：〈{2}〉《{3}》×{4}を獲得する。", RulePositivePattern, RulePositivePattern, RuleNamePattern, RuleNamePattern, RulePositivePattern);
        var results = new RuleSection(list, false, EffectType.Success, EffectType.Failure, EffectType.Critical, EffectType.Fumble);
        results.Add(EffectContentType.ChangeSuccessCount, "達成回数{0}", RuleSignedPattern);
        results.Add(EffectContentType.ChangeProductGrade, "グレード{0}", RuleSignedPattern);
        results.Add(EffectContentType.EndRollsWithOutcome, "発動ロール中断自動{0}。", @"成功|失敗");
        results.Add(EffectContentType.NoEffect, "何もなし。");
    }
}
