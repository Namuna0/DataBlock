public static partial class SkillTextConverter
{
    private static readonly SkillTextRule[] GodRules =
    {
        new SkillTextRule(OverrideContentType.EquippedSkillLimit, false, EffectType.Passive,
            "自身が装備できるスキルは{0}個までとなる。", B2Formula),
        new SkillTextRule(EffectContentType.DeferMissingResourceCost, false, EffectType.Passive,
            "このスキルの消費リソースの{0}が足りない時、可能な限り支払い、不足分を自身の次のターン開始時に消費する事でアクティブ効果を発動できる。", B2Resource),
        new SkillTextRule(OverrideContentType.ThisSkillStackCost, false, EffectType.Passive,
            "このスキルの消費{1}は自身の《{0}》スタック1つにつき-{2}される。（最低消費{3}）", B2Name, B2Resource, B2Positive, B2Positive),
        new SkillTextRule(OverrideContentType.ThisSkillStackPower, false, EffectType.Passive,
            "自身の《{0}》スタックが{1}以上の場合、このスキルの威力は{2}倍される。", B2Name, B2Positive, B2Factor),
        new SkillTextRule(EffectContentType.RemoveAllStacks, false, EffectType.Active,
            "このスキルの攻撃後、自身の《{0}》スタックは全て除去される。", B2Name),
        new SkillTextRule(EffectContentType.TimedTargetStacks, false, EffectType.Active,
            "対象は{2}ターンの間《{0}》スタックを{1}得る。", B2Name, B2Positive, B2Positive),
        new SkillTextRule(EffectContentType.UseStateBoundCharacter, false, EffectType.Passive,
            "《{0}》状態のキャラクターは、そのプレイヤーがその場にいる場合、この状態の効果を無視して使用できる。ただし、この状態は解除できない。", B2Name),
        new SkillTextRule(EffectContentType.SetTargetResourceAndStates, false, EffectType.Active,
            "対象の{0}を{1}にして《{2}》及び《{3}》状態にする。", B2Resource, @"[0-9]+", B2Name, B2Name),
        new SkillTextRule(EffectContentType.SucceedFailedActivationRoll, false, EffectType.Counter,
            "発動ロールを自動成功にする。"),
        new SkillTextRule(EffectContentType.UnmodifiableMaximumResourceDamage, false, EffectType.Active,
            "対象へ対象の{0}最大値の{1}%のダメージを与える。（この攻撃による威力及び被ダメージは、防御点、効果、状態によって増加も軽減もされない）", B2Resource, B2Positive),
        new SkillTextRule(OverrideContentType.AllBonusStatsMultiplier, true, EffectType.Passive,
            "自身のすべての能力値Bは×{0}倍される。", B2Factor)
    };
}
