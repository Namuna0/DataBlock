# 精神体のスキル・特性対応

対象は「精神体で実装すべき種族と特性.txt」の18件。Inspectorの構文統一、シリアライズセット、テキスト再構築、JSON出力に使うデータ表現と文章変換を追加した。戦闘処理や数式評価は従来どおり利用側の責務。初回追加時には、依頼に従いテスト・Inspector操作・ビルドによる確認を実施していない。以下はデータ表現と解釈の記録。

## 対応項目

| スキル・特性 | 表現 |
|---|---|
| 魔力体 | MP最大値補正、MP閾値で戦闘不能、HP閾値で重症ロール不要 |
| アストラルフェード | エンカウントフェーズ条件、戦闘離脱 |
| 純然たる自然の魔力 | 属性威力の被ダメージを、該当属性の不利属性Bの最大値で乗算 |
| フェアリーブリンク | 既存Choicesで接近／退避、効果へのカウンター対象化禁止 |
| 妖精の鱗粉 | 対象HP回復、Criticalで回復量1.5倍 |
| 悪戯 | キャラクターのActiveを持つスキルを選択、選択スキルを1ターン封印 |
| 実体無き身体 | カテゴリー別免疫／対象化禁止、HP・SAN最大値補正、SP消費のMP置換、神聖術被ダメージ補正、クエスト経験値加算、非生物の別名 |
| ソウルドレイン | 精神体・神族を除く対象、自動成功・固定達成値50、SANダメージ |
| 冥界の息吹 | 非生物・神族を除く対象、自動成功・固定達成値50、MPダメージ |
| 壁抜け | 戦闘／ロールプレイのChoices、1ターンの物理攻撃防御点無視 |
| 怪力乱神 | 攻撃の発動ロール限定のCritical範囲、神聖術被ダメージ補正 |
| 鬼神楽 | 宴酒／歌哭／幽歩のChoices、鬼禊・鬼火・接近-獄鎖-の状態定義 |
| 雷鼓 | 付与時の対象・威力を保持する遅延攻撃、攻撃後解除、Criticalの遅延攻撃威力補正 |
| 逢魔ヶ刻 | 自身・対象・相互接近の状態をすべて要求する威力／ACT補正、威力のスパイク置換 |
| 魔性 | 魅力判定補正、魅了中の全キャラクターへの自身ターン開始時の刻淫、神聖術被ダメージ補正、ロールプレイ説明 |
| 催眠 | 同接近グループの対象、付与者による毎ターンの代理行動宣言、行動中の味方扱い、スパイク |
| 夢喰らい | 対象の刻淫5以上、SANダメージ、実際に与えたSANダメージからHP・MP回復 |
| ラブタップ | 同接近グループの対象、刻淫付与、目標値／付与スタック数のスパイク |

## 表記の解釈・既存表現の再利用

- 《鬼神楽》の重複した名前・カテゴリー・習得条件・説明は、原文を一組に整理してから読み込む必要がある。スキル名に基づく重複の自動除去は行わない。
- 《壁抜け》の2組の発動ロールは、原文に選択式の【説明】と「●戦闘」「●ロールプレイ」を明記して既存のChoicesへ分ける必要がある。共通消費ACT2・MP2も両方の子スキルに明記する。スキル名から用途を推定する自動分割は行わない。
- 《壁抜け》の「態になる」は「状態になる」に補正する。「任意の対象」はキャラクターに限定しない。
- 《鬼神楽》の「さらにHP150回復」を宴酒の回復として扱う場合は、原文に【アクティブ効果】を明記して状態説明から分離する必要がある。特定の数値を見て効果の所属を自動変更する処理は行わない。
- 《夢喰らい》の「SANダメ―ジ」の長音表記を補正し、式の乗算記号「×」を「*」へ統一する。回復は同じスキル解決で実際に与えたSANダメージを参照し、HP1倍・MP0.1倍。原文にない最大値超過は許可しない。
- 原文の《精神体》《神族》《非生物》は種族カテゴリーの除外条件と判断する。
- 《宣言不能》《すり抜け》の状態カテゴリーは原文にないため追加しない。カテゴリー未指定の状態定義も再構築できるようにした。
- 《魔力体》の「自身はMPが0以下」は、既存のResourceChanged＋ResourceValue＋ApplyStateを再利用する。
- HP／MP／SAN最大値、魅力判定、状態付与・解除・接近、鬼火の回避補正、獄鎖の一方向の接近扱いと解除条件、非生物の種族別名は既存の型を使用する。
- 《妖精の鱗粉》の「効果量」は回復量の意味として既存MultiplyRecoveryをCriticalにも対応させる。
- 《魔性》の姿を変える記述と「魔族を参照する効果は無効にできない」という注記は、既存RoleplayDescriptionへ保持する。魔族カテゴリーを削除する効果は作らない。
- 《逢魔ヶ刻》のACT消費4以上は補正前の消費で判断する。3つの状態条件はANDであり、接近-獄鎖-は自身と対象の間の関係を要求する。
- 《雷鼓》は付与時に対象と攻撃式を評価した結果を保持する。Critical補正もその時に取り込み、1ターン後に再抽選しない。MultiplyStateAttackPowerは通常威力部分を乗算し、属性威力式そのものは変更しない。
- 《催眠》の「3回までに宣言」は毎ターンの代理行動宣言上限を3に変更する意味として扱う。スパイクは各段階の指定値への置換であり、基本値との加算ではない。

## Runtime/Model変更一覧

既存のenum番号は変更していない。JSONのルートはSkill／Summons／Statesを維持する。

### ConditionModels.cs

| ConditionType | Parameters |
|---|---|
| EncounterPhase = 50 | なし |
| SelectCharacterSkill = 51 | キャラクター数, Active, スキル数 |
| ExcludeTargetRaces = 52 | キャラクター数, 除外種族カテゴリー... |
| TargetStackMinimum = 53 | 状態名, スタック下限, キャラクター数 |
| SelectAnyTarget = 54 | 対象数 |

### EffectModels.cs

`DiceRollDefinition.FixedResult`を追加。-1は指定なし、0以上は自動成功時の固定達成値。通常のダイスロールの目標値とは別。JSONのRollにもこのフィールドが加わる。

| EffectContentType | Parameters |
|---|---|
| LeaveBattle = 50 | Self |
| PreventRollAtResource = 51 | Self, リソース, AtMost, 閾値, ロール名 |
| CategoryImmunity = 52 | Self, カテゴリー...（いずれか） |
| ProhibitCategoryTarget = 53 | Self, カテゴリー |
| ConvertResourceCost = 54 | Self, 元リソース, 先リソース, All |
| AddQuestReward = 55 | Self, 報酬名, 加算量 |
| ApplySelectedSkillState = 56 | SelectedSkill, 状態名, ターン数 |
| ProhibitSelectedSkill = 57 | Self, SelectedSkill |
| ResourceDamage = 58 | Target, リソース, 式 |
| RestoreFromResourceDamage = 59 | Self, ダメージ対象リソース, 回復リソース, 倍率, ThisResolution |
| GrantActionControl = 60 | Applier, Self, Turn, 上限, AllyDuringAction |
| DelayedStateAttack = 61 | Target, 遅延ターン数, 通常威力, 属性威力, SnapshotOnApply, RemoveAfterAttack |
| TurnStartStateStacks = 62 | Self, 対象の必要状態, 付与スタック名, 個数, AllCharacters |

状態内のSelfは状態保持者、Applierは付与者。DelayedStateAttackのTargetは状態付与時に選択した対象を保持する。選択スキルの封印も、付与時のスキルの識別情報を状態インスタンスへ保持する。

| OverrideContentType | Parameters |
|---|---|
| AttributeWeaknessDamage = 35 | Self, HighestDisadvantageAttributeBonus |
| PreventCounterTarget = 36 | ThisEffect |
| SetStateControlLimit = 37 | 状態名, Turn, 上限 |
| ConditionalStatePower = 38 | 行動カテゴリー, 自身の状態, 対象の状態, 相互接近状態, 倍率 |
| ConditionalStateCost = 39 | 上と同じ4条件, リソース, 補正前消費下限, 加減量 |
| SetConditionalStatePower = 40 | 上記威力補正の置換倍率 |
| IgnoreDefenseForCategories = 41 | Self, カテゴリー1, カテゴリー2（両方） |
| MultiplyStateAttackPower = 42 | 状態名, 倍率 |

既存SetRollRangeに`Activation, Critical, 下限, 上限, 行動カテゴリー`の形式を追加。既存SetCooldown・SetStackAmountをスパイクでも利用する。

## 変更箇所

- `SkillInputTextCodec.cs`: 原文の整形。宣言条件は `ConditionTextCodec.cs`。
- `SkillEffectReaders.cs / SkillContentTextCodec.cs`: 通常効果の読み取り・再構築。
- `SkillModifierTextCodec.cs / SkillSpikeTextCodec.cs`: 補正・スパイクの読み取り・再構築。
- `StateEffectTextCodec.cs / StateModifierTextCodec.cs`: 状態の効果・補正。
- `SkillEffectValidation.cs / SkillDataValidation.cs`: 効果の整合性、参照先検証、遅延攻撃のCritical参照確定。
- 既存のParser／Writer／条件・効果・状態codec／効果検証に上記処理を接続。

初回追加時のInspector操作・JSON出力の成功確認は未実施。
