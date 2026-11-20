public static partial class SkillTextConverter
{
    private static void RegisterSuccessRules(RuleSection rules)
    {
        rules.Including(EffectType.Critical, EffectType.Fumble).Add(EffectContentType.ModifyOutputCount, "個数{0}", RuleSignedPattern);
        rules.Including(EffectType.Failure).Add(EffectContentType.CraftGradeShift, "生産に成功した時、生産したアイテムのグレードは{0}つだけ{1}される。", RulePositivePattern, RuleSignedPattern);
        rules.Including(EffectType.Critical, EffectType.Fumble).Add(EffectContentType.ChangeSelectedItemDurability, "耐久{0}", RuleSignedPattern);
    }

    private static void RegisterFailureRules(RuleSection rules)
    {
        rules.Add(EffectContentType.PartyActsLast, "味方チームは確定後攻となる。");
        rules.Including(EffectType.Fumble).Add(EffectContentType.RemoveSelfNamedState, "自身の《{0}》状態は解除される。", RuleNamePattern);
        rules.Including(EffectType.Fumble).Add(EffectContentType.SetTwoResourcesAndStates, "自身の{0}と{1}は{2}になり。《{3}》及び《{4}》状態になる。", RuleResourcePattern, RuleResourcePattern, @"[0-9]+", RuleNamePattern, RuleNamePattern);
        rules.Add(EffectContentType.PurgeOwnCategoryOnFailure, "自身に付与されている〈{0}〉状態は全て解除される。", RuleNamePattern);
        rules.Add(EffectContentType.NoRollEffect, "何もなし");
    }

    private static void RegisterCriticalRules(RuleSection rules)
    {
        rules.Add(EffectContentType.RepeatActiveEffect, "アクティブ効果を追加で{0}回適用する。", RulePositivePattern);
        rules.Add(EffectContentType.ChooseDiceOutcome, "ダイスロールから任意の結果を選べる。");
        rules.Add(OverrideContentType.StackGrantMultiplier, "スタック付与×{0}倍", RuleFactorPattern);
        rules.Add(OverrideContentType.AdvanceActionWithCap, "対象はメインフェーズの開始時に先行して{0}{1}までの行動を宣言できる。", RuleResourcePattern, RulePositivePattern);
        rules.Add(OverrideContentType.SetResourceCostFixed, "{0}の消費が{1}になる。", RuleResourcePattern, @"[0-9]+");
        rules.Add(OverrideContentType.ReplaceResourceLoss, "対象の{0}減少量は{1}に変更される。", RuleResourcePattern, RuleSignedPattern);
        rules.Add(EffectContentType.EnsureCraftRollSuccess, "〈{0}〉の発動ロールは{1}つだけ自動成功となる。", RuleNamePattern, RulePositivePattern);
        rules.Add(OverrideContentType.SetGrantedStackCount, "《{0}》スタックの付与は{1}に変化する。", RuleNamePattern, RulePositivePattern);
        rules.Add(OverrideContentType.ReplaceStatBonusIncrease, "{0}の上昇値は{1}に変化する。", RuleNamePattern, @"[+-][0-9]+(?:\.[0-9]+)?");
        rules.Add(EffectContentType.CriticalDoesNotEnsureSuccess, "このスキルはクリティカルによって自動成功にならない。");
        rules.Add(EffectContentType.PurgeInjuryRollStates, "自身の重傷ロールによって付与された状態を全て解除する。");
        rules.Add(EffectContentType.StealAdditionalItem, "追加で所持品をさらにもう{0}つ選択して奪う。", RulePositivePattern);
        rules.Including(EffectType.Fumble).Add(EffectContentType.MultiplySummonHPAndPower, "召喚された《{0}》のHPと威力は×{1}倍される。", RuleNamePattern, RuleFactorPattern);
    }

    private static void RegisterFumbleRules(RuleSection rules)
    {
        rules.Add(EffectContentType.DestroyEquipmentCategory, "〈{0}〉は消滅する。", RuleNamePattern);
        rules.Add(OverrideContentType.ReplaceStackQuantity, "《{0}》スタックの付与は{1}に変化する。", RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.PartyLastAndLoseOpeningResource, "味方チームは確定後攻となり、対象は{0}ターン目開始時に{1}{2}を失う。", RulePositivePattern, RuleResourcePattern, RulePositivePattern);
        rules.Add(EffectContentType.FailAllCraftRolls, "〈{0}〉の発動ロールは全て自動失敗となる。", RuleNamePattern);
    }

    private static void RegisterSecondSpikeRules(RuleSection rules)
    {
        rules.Add(OverrideContentType.ReplacePassiveStatBonus, "このパッシブ効果による[{0}]上昇は{1}加算に変化する。", RuleNamePattern, @"[+-][0-9]+(?:\.[0-9]+)?");
        rules.Add(OverrideContentType.SetDelayedStateSkillValue, "《{0}》の遅延攻撃のスキル値は{1}に変化する。", RuleNamePattern, @"[0-9]+");
        rules.Add(OverrideContentType.QualifyNamedSkill, "《{0}》の習得条件を達成する。", RuleNamePattern);
        rules.Add(OverrideContentType.SetActivationRollBonus, "このスキルの発動ロールによる達成値は{0}される。", RuleSignedPattern);
        rules.Add(OverrideContentType.SetStateResourceLossRatio, "{0}効果によるリソースを減少は{1}になる。", RuleNamePattern, ArithmeticPattern);
        rules.Add(OverrideContentType.SetDamageAndResourcesPercent, "このアクティブ効果によるダメージは{0}, 減少は{1}と{2}を{3}%に変化する。", ArithmeticPattern, RuleResourcePattern, RuleResourcePattern, RulePositivePattern);
        rules.Add(OverrideContentType.SetRepeatPowerProgression, "このパッシブ効果による威力上昇は、{0}回目以降、威力は×{1}倍→×{2}倍→×{3}倍に変化する。", RulePositivePattern, RuleFactorPattern, RuleFactorPattern, RuleFactorPattern);
        rules.Add(OverrideContentType.SetStateBattleEndStackRecovery, "{0}効果が「スタックを{1}以上残して戦闘終了した時、{2}を{3}回復しても良い。」に変化する。", RuleNamePattern, RulePositivePattern, RuleResourcePattern, RulePositivePattern);
        rules.Add(OverrideContentType.SetItemCostAndStackGain, "〈{0}〉の消費リソースが{1}に変化する代わりに、《{2}》スタックを×{3}得る。", RuleNamePattern, RuleSignedPattern, RuleNamePattern, RulePositivePattern);
        rules.Add(OverrideContentType.CraftQuantityRankBonus, "このアクティブ効果によるアイテム獲得数は{0}される。", RuleSignedPattern);
        rules.Add(OverrideContentType.SetTwoResourceCosts, "このスキルの消費リソースは{0}-{1}, {2}-{3}に変化する。", RuleResourcePattern, @"[0-9]+", RuleResourcePattern, @"[0-9]+");
        rules.Add(OverrideContentType.SetEntityActivationRoll, "《{0}》の発動ロールは{1}に変化する。", RuleNamePattern, ArithmeticPattern);
        rules.Add(OverrideContentType.SetActiveResourceRecovery, "このアクティブ効果による{0}回復は{1}に変化する。", RuleResourcePattern, RulePositivePattern);
        rules.Add(OverrideContentType.SetNamedStateDamage, "《{0}》によるダメージは{1}に変化する。", RuleNamePattern, ArithmeticPattern);
        rules.Add(OverrideContentType.SetDefenseIgnoredAtRank, "このパッシブ効果による防御点の無視は{0}に変化する。", RulePositivePattern);
        rules.Add(OverrideContentType.NoSpikeChange, "無し。");
        rules.Add(OverrideContentType.IncreaseRacialBonusByPosition, "種族能力値ボーナスの内{0}番の物が{1}%加算される。", RulePositivePattern, RuleSignedPattern);
        rules.Add(OverrideContentType.LearnNamedSkillAtRank, "スキル《{0}》を習得する。", RuleNamePattern);
        rules.Add(OverrideContentType.SetAdditionalRacialSkillCount, "このパッシブ効果によって追加習得する種族スキルは{0}つに変化する。", RulePositivePattern);
        rules.Add(OverrideContentType.SetPartyDefenseBonus, "このパッシブ効果による防御点加算は{0}に変化する。", RuleSignedPattern);
        rules.Add(OverrideContentType.SetResourceDamageAtRank, "このアクティブ効果による{0}ダメージは{1}に変化する。", RuleResourcePattern, ArithmeticPattern);
        rules.Add(OverrideContentType.SetActivationResultFactor, "この発動ロールによる達成値は×{0}倍される。", RuleFactorPattern);
        rules.Add(OverrideContentType.SetInjuryResourceThreshold, "このパッシブ効果による重症ロールの{0}は{1}を下回った時に変化する。", RuleResourcePattern, RulePositivePattern);
        rules.Add(OverrideContentType.SetStatePowerIncrease, "《{0}》による威力上昇は×{1}倍に変化する。", RuleNamePattern, RuleFactorPattern);
        rules.Add(OverrideContentType.SetWeaponCategoryPower, "〈{0}〉による武器威力は{1}に変化する。", RuleNamePattern, ArithmeticPattern);
    }
}
