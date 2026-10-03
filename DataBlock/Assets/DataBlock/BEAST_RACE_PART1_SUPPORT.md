# 獣人のスキル・特性 その1

「獣人の実装すべきスキルと特性その1.txt」の24件に対する文章変換・データ表現を追加した。対象はInspectorの構文統一 → シリアライズセット → テキスト再構築 → JSON出力。戦闘・移動・所持品操作そのものの実行は、従来どおりJSONの利用側が実装する。

初回追加時には、依頼に従いテスト・ビルド・Inspector操作による動作確認を実施していない。以下はデータ表現と解釈の記録。

## 対応内容

| スキル・特性 | 対応 |
|---|---|
| 猫の夜目 | 指定された環境名による環境効果を受けない |
| キャットクロウ | 既存の直接攻撃、Critical、威力のスパイク置換 |
| 獣の跳躍 | 既存の接近と発動ロール式のスパイク置換 |
| 猫回避 | 既存の反応対象・攻撃無効・発動ロール式のスパイク置換 |
| ライオンダイブ | 非接近条件、接近後の武器攻撃、1ターン目のスキル値変更 |
| ビーストクロウ | 既存の直接攻撃とスパイク |
| 獣の咆哮 | 対象への畏怖、攻撃に対する回避達成値低下、付与ターン数変更 |
| 捕食者の愉悦 | 自身の攻撃で対象を戦闘不能にした時の最大HP割合回復、その割合変更 |
| 跳躍回避 | 「その対象へ発動」を既存の反応対象選択へ統一 |
| ラッキーヒット | 攻撃Critical時の威力と達成値の乗算、他効果との重複 |
| 恵みの蓄え | 自身が使用した持続アイテムの効果延長、サイズ別所持上限 |
| 機敏 | 既存の敏捷Bによる行為判定倍率 |
| 角撃 | 接近、直接攻撃、威力式変更 |
| スケープゴート | 戦闘不能時の任意キャラクターの行為判定への反応、戦闘中1回・蘇生で回数リセット、Critical／Fumbleの任意選択 |
| 崖歩き | 種族選択時の習得、任意対象、既存ロールプレイ効果 |
| 羊の巻き毛 | 日次回数制限、アイテム名・個数・ランク式を保持する獲得効果 |
| 水牛角撃 | 接近、直接攻撃、踏破の付与、状態内被ダメージ倍率のスパイク置換 |
| 突き上げ | 既存の直接攻撃、ACT減少、威力式変更 |
| 頑強無比 | HPが0以下になる攻撃への反応、攻撃無効、重症ロール由来の状態解除 |
| 馬疾駆 | 任意の進行ロール回数減少、平地で減少回数を置換 |
| 馬の早駆 | 既存Choicesによる接近／退避 |
| 鏑流馬 | 条件を満たす装備スキル選択、自身への移動の発動者への反応、移動無効、選択スキルの任意宣言と威力補正 |
| 鹿の早駆 | 既存Choicesによる接近／退避 |
| 鏑流鹿 | 所持武器または素手の選択、戦闘中の武器交換 |

## 表記の解釈と既存型の再利用

- 《キャットクロウ》のサードスパイクの`150+2d100*[筋力B]*`は、末尾に乗算対象がない誤記と判断し、`150+2d100*[筋力B]`へ補正する。数値を補完しない。
- 《角撃》のサードスパイクは、原文どおり`150+2d100*[筋力B]*3.5`。基本・セカンドの`1d100`へ揃えない。
- 《崖歩き》の〈種族学スキル〉は〈種族スキル〉の誤記として補正する。「種族選択時」は自動／任意が明記されていないため、新しいAtRaceSelectionで未指定のまま保持する。
- 「更に」「さらに」「さらに、」は同じ接続表現として扱う。フレーバー文章は補正しない。
- 《跳躍回避》の「自身が攻撃を受けた時、その対象へ発動」は、既存ReactionTargetの`攻撃, Self, Source`で表現する。
- 《羊の巻き毛》の「1日1度のみ」は既存ActivationLimitの`Day, 1`。ランクの`☆×(2d3-1)`は星ランクを表す式として保持し、変換時には抽選しない。
- 《ライオンダイブ》の「240のスキル値で武器攻撃」は既存WeaponAttack。1ターン目の288は既存SetSkillValue＋SkillValue／TurnNumberで保持する。基本240・初回288・スパイク293／298を原文どおり別々に記録し、倍率や別のスパイク値は推定しない。
- 《獣の咆哮》の「1ターンの間、対象は」は既存ApplyStateの語順違い。「達成値を-10減少」は符号付き加減量-10として既存AddEvasionResultで保持する。
- 《水牛角撃》の「被ダメージは×0.75倍されます」は既存MultiplyDamageTaken。サードの0.7は被ダメージの倍率を0.75から0.7へ置換する意味であり、0.75×0.7ではない。
- 《捕食者の愉悦》は状態の付与を引き起こした時に発動する。既に戦闘不能の相手を攻撃するだけでは発動しない。回復率15／18／22%はそれぞれ自身の最大HP基準。
- 《ラッキーヒット》の「他効果と重複」は乗算の併用を許す意味としてStackを保持する。達成値補正も同じ攻撃Criticalを条件とする。
- 《恵みの蓄え》は使用者が自身なら他者へのアイテム効果も延長する。所持上限60／20は加算量ではなくサイズ別の上限値。
- 《スケープゴート》は戦闘中1回で、〈蘇生〉を受けると使用回数がリセットされる。選択肢はCriticalかFumble、効果の使用自体は任意。
- 《頑強無比》は受けるとHPが閾値以下になる攻撃への反応。既にHP0以下である条件とは区別する。解除対象は名前が「重症」の状態だけではなく、重症ロールで発生した状態。
- 《馬疾駆》の平地は-1に-2を加算せず、減少回数を2へ置換する。任意性は維持する。
- 《鏑流馬》の「対象」は自身へ移動を発動したキャラクター。追加宣言で免除するのはACTのみ。他リソース・宣言条件は維持する。0.5倍は追加宣言するスキルへ適用し、Criticalの1.5倍も同じ追加スキルに適用する。
- 《鏑流鹿》の素手選択は武器を外して素手へ切り替えることを許す指定。フレーバー内の「持ち帰る」は変更しない。

## Runtime/Model変更一覧

既存enumの番号とJSONのSkill／Summons／States構造を維持する。今回はクラスのフィールド追加はない。

### ConditionModels.cs

| ConditionType | Parameters |
|---|---|
| WithoutOwnState = 55 | 状態名 |
| IncapacitatedCheckReaction = 56 | 状態名, Battle, 1, AnyCharacterAction, 回数リセットの受信カテゴリー |
| AtRaceSelection = 57 | なし |
| LethalIncomingAction = 58 | リソース, AtMost, 閾値, 行動カテゴリー |
| SelectEquipmentSkill = 59 | 選択数, リソース, AtMost, 消費上限, NoMeleeRequirement |
| ActionTowardsSelf = 60 | 行動カテゴリー, Source |
| SelectCarriedWeapon = 61 | 1, AllowUnarmed |

### EffectModels.cs

| EffectContentType | Parameters |
|---|---|
| EnvironmentImmunity = 63 | Self, 環境名... |
| RestoreOnAppliedState = 64 | Self, 行動カテゴリー, Target, 付与状態, リソース, MaximumPercent, 回復率 |
| ChooseCheckOutcome = 65 | Target, Critical, Fumble, Optional |
| GrantItem = 66 | Self, アイテム名, 個数, 星ランク式 |
| RemoveStatesByOrigin = 67 | Self, Roll, ロール名 |
| DeclareSelectedSkill = 68 | Self, Target, ACT, 0, Optional, 威力倍率 |
| SwapWeapon = 69 | Self, SelectedWeapon, Battle, AllowUnarmed |

| OverrideContentType | Parameters |
|---|---|
| SetAppliedStateDuration = 43 | Target, 状態名, 置換ターン数 |
| SetStateDamageMultiplier = 44 | ローカル状態名, Self, 置換被ダメージ倍率 |
| SetOnAppliedStateRecovery = 45 | リソース, 置換回復率（最大値の%） |
| CriticalCategoryMultiplier = 46 | 行動カテゴリー, PowerまたはActionResult, 倍率, Stack |
| ItemEffectDuration = 47 | Self, AllTargets, 追加ターン数 |
| ItemCapacity = 48 | サイズ, 所持上限 |
| OptionalAreaProgressReduction = 49 | 通常の減少回数, エリアカテゴリー, 置換減少回数, Optional |
| MultiplyDeclaredSkillPower = 50 | 追加宣言スキルへの倍率 |

入力の整形は `SkillInputTextCodec`、効果の読み取り・再構築は `SkillEffectReaders / SkillContentTextCodec`、補正とスパイクは `SkillModifierTextCodec / SkillSpikeTextCodec`、整合性と参照先の検証は `SkillEffectValidation / SkillDataValidation` が担当する。読み取り・再構築の両方向、参照先・効果種別・スパイクの基本効果との対応を検証する。
