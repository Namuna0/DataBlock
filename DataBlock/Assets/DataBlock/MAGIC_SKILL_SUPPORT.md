# 魔法生物のスキル・特性

文章変換とJSONのデータ表現を拡張する。戦闘・移動・成長の実行処理は従来どおり利用側で実装する。既存enum番号とJSONのルート構造は維持する。

## 表記の解釈

- 《血の刻紋》のコードフェンスと名前が同じ行にある表記、および「（エルフ魔法〉」を補正する。
- 《長命種の叡智》の「ここの」を「この」として扱う。
- 《フラッシュバリア》の宣言条件の《攻撃》を、他の反応条件と同じ行動カテゴリ〈攻撃〉として扱う。
- 「HP最大値が0.83倍」「行為判定の達成値」「採取回数上限が+1増加する」は既存の能力値・達成値補正へ変換する。
- 《岩読み》の「によるの」は「による」として扱う。
- 《プライマルパワー》は「3を超えた時」を採用し、4スタック以上で別定義の効果へ切り替える。元の状態名とスタック数は維持し、二つの倍率を累積させない。
- 《マナサージ》は敵全体から実際に減らしたMPの合計を回復し、回復先の最大値超過を許可する。回避禁止は攻撃の有無にかかわらずこのスキル全体に適用する。
- 《降魔の儀》は当該スタック獲得後に5と等しくなった時にHP・MPを全回復し、状態を付与する。未記載のスタック消費・解除は加えない。スパイクのACT回復量は段階別置換で累積しない。

## 新しい型と引数

ConditionType: `ActionName=47`（名前付きスキル）、`HasAttributeBonusPower=48`（属性B由来の威力の存在）、`CounterToOwnActive=49`（自身のアクティブへの反応）。

EffectContentType:

| 型 | Parameters |
|---|---|
| RerollGathering (38) | スキル名, Day, 回数 |
| EquipSlotSubstitution (39) | Weapon, BothHands, OneHand |
| TransformAtStacks (40) | Self, スタック状態, Equals, 閾値, HP, MP, 変化先状態 |
| CyclingStateStacks (41) | Self, 行動カテゴリ, 状態名, 状態カテゴリ, 獲得数, 閾値, リソース, 回復量, リセット値 |
| DrainResource (42) | AllEnemies, MP, 減少量の式（正の量）, Self, ActualTotal, AllowOverflow |
| CreateMeleeGroup (43) | Self, Target, 状態名 |
| PreventMealPenalties (44) | Self, MealAndMealSet, ResourceAndStatDecrease |
| GrantCreationChoice (45) | LifePath, 選択数, 候補名... |
| GrantRaceTrait (46) | 種族カテゴリ, 選択数 |
| OutsiderRule (47) | 固有ルール識別子, ルールの値... |
| OptionalInvalidateAction (48) | Self, 行動カテゴリ, AtMost, 達成値 |
| UseStateDefinitionAtStacks (49) | GreaterThan, 閾値, 定義名, ReplaceEffects, KeepIdentityAndStacks |

OverrideContentType:

- `SetRollRange=31`: Action, FumbleまたはCritical, 下限, 上限。両端を含む。スパイクは該当する基本範囲を置換する。
- `MultiplyDamageReduction=32`: 軽減量倍率。既存のカウンター軽減を参照する。
- `MultiplyPowerByTurnActivations=33`: 行動カテゴリ, 開始回数, 順次倍率..., 最大倍率。毎ターン回数をリセットする。
- `StateTurnRecovery=34`: 状態名, リソース, 回復量, AnyTurn。指定状態を持つ間のターン開始時に適用する。

既存型の追加形式：`ApplyState` の対象 `SameMeleeExceptSelf`、`RerollActivation` の能力値指定 `Original`、`InvalidateTriggeredEffect` の `Target / Counter`、`SetAttackRule` の `Response / 回避 / Prohibit / ThisSkill`。`AddResourceCost` の自動適用はカテゴリ指定Passiveにも対応する。

## 人外の固有ルール

`OutsiderRule` はこの特性専用の13項目に限定する。原文を不透明な文字列として保存せず、各条件・数値・対象を引数として保持する。対応表は `OutsiderTextCodec.cs` の `OutsiderRules` に集約する。

町進入禁止、戦闘エリアでの町行動、初期エリア、種族能力値ボーナス合計40%と各20%上限、☆4以下かつ一般エネミー・魔法生物からの選択、選択モンスターの行動条件と特性継承、リスト外スキルのターン制限、移動時回復、全滅時復帰、パーティー種族制限、討伐報酬をそれぞれ別項目とする。

「意外」は「以外」として解釈する。リスト外スキルは全体で1ターン1回の共有制限、移動時20%回復は最大値基準、討伐報酬の1日6点は経験値と決意それぞれの上限として扱う。

テストは依頼に従いInspectorの実操作のみ。テストランナーや別プロセスで変換処理を実行しない。

## Inspectorでの確認結果

- 添付原文32件の一括「構文統一」成功。統一後の32件を再度「構文統一」しても成功。
- 《人外》《降魔の儀》《アウェイキン・ザ・プライマルパワー》は、一件ずつ「構文統一 → テキストからシリアライズセット → データブロックテキスト再構築 → JSON出力」の全操作に成功。後二件の状態定義数はそれぞれ1件・2件。
- 《マナサージ》《精霊の加護》《マナレゾナンス》《トリックハザード》《報い》も一件ずつ4操作すべて成功。合計8件の個別JSON出力を確認した。
- 《運命のもぎ取り》は構文統一・シリアライズセットまで成功。再構築・JSON出力は未確認。
- 実操作で見つかった「さらに、対象…」の既存変換への引き渡し漏れ、および統一後の罠効果行が次行を取り込む不具合を修正し、Inspectorで再確認済み。
- 残り23件は一括の構文統一・再読込のみ確認済み。個別4操作と不正な判定範囲の拒否テストは未実施。ユーザーがテストを引き継ぐため、Inspector操作を終了した。

## Runtime/Modelの変更一覧

- `ConditionModels.cs`: `ConditionType` に `ActionName (47)`、`HasAttributeBonusPower (48)`、`CounterToOwnActive (49)` を追加。
- `EffectModels.cs`: `EffectContentType` に上表の38〜49を追加。`OverrideContentType` に31〜34を追加。
- 既存の列挙値は変更していない。`SkillBody` などのフィールド追加はなく、《人外》は `isMonster` フラグではなく専用の `OutsiderRule` で表現する。
