public static partial class SkillTextConverter
{
    private static readonly SkillTextRule[] NonlivingRules =
    {
        new SkillTextRule(EffectContentType.ReviveAfterBattle, false, EffectType.Passive,
            "戦闘終了後、味方が全滅していなければ、自身は《{0}》状態を解除してHP{1}の状態で復活する。", B2Name, B2Positive),
        new SkillTextRule(EffectContentType.AbsentResource, false, EffectType.Passive,
            "自身は{0}が存在しない。", B2Resource),
        new SkillTextRule(EffectContentType.StateImmunity, false, EffectType.Passive,
            "自身は《{0}》状態にならない。", B2Name),
        new SkillTextRule(EffectContentType.StateOnIncomingCritical, false, EffectType.Passive,
            "自身は発動ロールクリティカルによる〈{0}〉を受けた時《{1}》状態になる。", B2Name, B2Name),
        new SkillTextRule(EffectContentType.EnvironmentPermanentState, false, EffectType.Passive,
            "自身は環境に《{0}》を含む場合、永続的に《{1}》状態となる。", B2Name, B2Name),
        new SkillTextRule(EffectContentType.SuppressSourceDamageAndState, false, EffectType.Passive,
            "〈{0}〉によるダメージを受けた時、《{1}》による被ダメ上昇と《{2}》状態を受けなくなる。", B2Name, B2Name, B2Name),
        new SkillTextRule(EffectContentType.SuppressSourceEnvironmentState, false, EffectType.Passive,
            "環境に《{0}》を含む時、《{1}》による《{2}》状態を受けなくなる。", B2Name, B2Name, B2Name),
        new SkillTextRule(EffectContentType.HitRaceState, false, EffectType.Active,
            "この攻撃が命中した時、対象の種族が〈{0}〉のいずれかなら、《{1}》状態を{2}ターン付与する。", B2Name, B2Name, B2Positive),
        new SkillTextRule(EffectContentType.WeaponHitRaceState, true, EffectType.Passive,
            "自身の〈{0}〉による武器攻撃が命中した時、対象の種族が〈{1}〉のいずれかなら、《{2}》状態を{3}ターン付与する。", B2Name, B2Name, B2Name, B2Positive),
        new SkillTextRule(EffectContentType.OptionalStackOnSelected, false, EffectType.Passive,
            "自身が選択対象になる時、相手に《{0}》スタックを{1}付与しても良い。", B2Name, B2Positive),
        new SkillTextRule(EffectContentType.OptionalRecoveryIfStackExists, false, EffectType.Passive,
            "自身のターン開始時、《{0}》スタックが付与されているキャラクターが1体でもいれば{1}を{2}回復しても良い。", B2Name, B2Resource, B2Formula),
        new SkillTextRule(EffectContentType.AdditionalRaceSkills, false, EffectType.Passive,
            "選択可能な種族スキルを追加で{0}つ習得する事が出来る。", B2Positive),
        new SkillTextRule(EffectContentType.IncomingCategoriesState, false, EffectType.Passive,
            "自身が〈{0}〉のいずれかを受けた時、自身は{2}ターンの間《{1}》状態になる。", B2Name, B2Name, B2Positive),
        new SkillTextRule(OverrideContentType.PowerAgainstState, false, EffectType.Active,
            "この攻撃の前から対象が《{0}》状態の時、威力は×{1}倍される。", B2Name, B2Factor),
        new SkillTextRule(OverrideContentType.WeaponPowerAgainstState, true, EffectType.Passive,
            "自身の〈{0}〉による武器攻撃の前から対象が《{1}》状態の時、その攻撃の威力は×{2}倍される。", B2Name, B2Name, B2Factor),
        new SkillTextRule(OverrideContentType.IncomingCategoriesDamageMultiplier, false, EffectType.Passive,
            "自身が〈{0}〉のいずれかを受けた時、被ダメージが×{1}倍される。", B2Name, B2Factor)
    };
}
