# 人間の特性・スキル16件の文型対応

対象資料：`人間の特性とスキルリスト.txt`。Inspectorでは従来どおり1件ずつ、構文統一→テキストからシリアライズセット→データブロックテキスト再構築→JSON出力を使用します。初回追加時には、依頼に従いテストを実施していません。以下はデータ表現と解釈の記録です。

## 既存表現を再利用した判断

| 該当箇所 | 判断・保存方法 |
|---|---|
| 運命の引き寄せ・不屈の胆力の「戦闘中1度だけ」 | 既存の `ActivationLimit` に `Battle` 単位を追加。効果の適用回数を表す `BattleEffectLimit` とは区別。 |
| 運命の引き寄せの「通常失敗」 | 既存 `RollResult` の `Activation / NormalFailure`。ファンブルまで含めない。振り直し自体は新しい効果。 |
| メレーウェポンマスタリー | 既存 `MultiplyActionResult` と武器攻撃の選別条件を再利用。発動ロールに限定する条件を追加。 |
| ハルマティア人の先祖帰り | 既存 `ActionStat` の複数能力値指定と `MultiplyActionResult`。知力Bまたは魔力Bの判定に1回適用。 |
| 適応力・アルケミックセンスの達成値上昇 | 既存 `AddActionResult`。前者には環境判定の条件を追加し、後者には既存 `ActionCategory` を使用。 |
| 秘伝の職人技 | 既存 `AddTargetValue`。装備制作と候補カテゴリを表す条件を追加。独立行のカテゴリ一覧は次行の「上記の装備」に接続。 |
| 宝探しの目 | 既存 `ModifyStat` で「一日の採取回数上限」を加算。エリア内の追加採取である `AddAreaGathering` とは区別。 |
| 勇敢 | 既存 `AddActionResult`・`MultiplyDamageTaken` を使用し、戦闘開始からのターン範囲を条件として追加。「および」の改行は1文として解釈。 |
| 持久力 | 既存 `ModifyStat / Multiply` によるSP最大値倍率。スパイクも同じ補正の置換。 |
| ハイランダー | 既存 `RemoveState` にカテゴリ指定・全解除の引数形式を追加。〈行動阻害, スネア〉のいずれかのカテゴリを持つ状態が宣言条件で、該当する状態は全て解除。個別の状態名《…》とは区別。 |
| 不屈の胆力のHP条件 | 既存 `ResourceValue / Self / HP / AtMost / 0`。HPを1にする処理は回復量1とは異なるため新定義。 |
| ハクィナス教徒の威力 | 既存 `MultiplyPower` と `ActionCategory`。回復量倍率は独立した新定義とし、スパイクは両方を置換。 |
| 各種スパイク | 既存 `SetModifier` を拡張し、基本補正を意味上の対象で特定して値を置換。上昇分を重ねて加算・乗算する扱いにはしない。 |
| 探求心のサードスパイク「このカウンター効果」 | カウンター効果がなく、消費決意の変更先がパッシブ補正として一意に存在するため、パッシブの表記揺れとして処理。 |
| アルケミックセンスの「によるの行為判定」 | 「による行為判定」と同じ文型として処理。 |
| 宝探しの目のセカンドスパイク「無し」 | 追加効果は保存しないが、サードスパイクが存在する場合は、構文統一・再構築時に「【セカンドスパイク】無し」を必ず表記する。この規則は全スキルに共通。 |

## 新しい定義と既存定義の追加形式

既存enum番号と `Skill / Summons / States` のJSON構造は維持します。以下はデータ表現・文章変換の契約であり、戦闘や判定を実行する機能ではありません。`TriggerTiming.ResourceRecovery = 25` を回復量が決まるタイミングとして追加しています。

### ConditionType

- `CheckContext = 44`: `[Activation]`、`[Environment]`、`[Crafting, 装備カテゴリ...]`。制作カテゴリは候補のいずれか。
- `BattleTurnRange = 45`: `[開始ターン, 終了ターン]`。戦闘開始を1とし、両端を含む。
- `OwnStateCategories = 46`: `[Any, 状態カテゴリ...]`。該当状態を1つ以上受けている条件。
- 既存 `ActivationLimit`: `[Battle, 回数]` を追加。
- 既存 `RollResult`: 宣言条件の `[Activation, NormalFailure]` を追加。既存の追加ダイス用形式は維持。
- 既存 `EffectKind`: 回復量補正の `[RestoreResource]` を追加。

### EffectContentType

- `RerollActivation = 33`: `[Self, 置換後の能力値B, TriggeringRoll]`。契機になった発動ロールの能力値ボーナスを置き換えて振り直す。元の目標値や他の式要素は保持。
- `SetResourceValue = 34`: `[Self, HP, 値]`。現在HPの設定であり、同量の回復ではない。
- `SkipRoll = 35`: `[Self, ロール種別, Optional, ThisResolution]`。今回の処理に限り、指定ロールを任意で省略。
- `LimitAcquisition = 36`: `[Self, RaceSelection, スキルカテゴリ, 上限]`。0は選択禁止。種族別名の追加から習得権を自動推論しない。
- `RaceAlias = 37`: `[Self, 追加の種族カテゴリ]`。元の種族を置換せず追加扱い。
- 既存 `RemoveState`: `[Self, Categories, All, 状態カテゴリ...]` を追加。いずれかのカテゴリを持つ状態を全解除。

### OverrideContentType

- `AddTimedActionResult = 26`: `[Self, CurrentTurn, WeaponPower, 武器カテゴリ, 行動カテゴリ, 加算値]`。「穂焔狙撃術」の武器威力参照・攻撃・発動ロール・当ターン限定を保持。
- `ModifyAcquisitionCost = 27`: `[Self, 決意, Add|Multiply, AllSkills|Class|SkillCategory, 選択対象, 値]`。`AllSkills` の選択対象は `All`。クラス名とスキルカテゴリは別の補正。
- `MultiplySalePrice = 28`: `[NPC, ExceptCategory, 除外カテゴリ, 倍率]`。
- `MultiplyRecovery = 29`: `[倍率]`。行動カテゴリ・回復効果の指定は `Triggers`。リソース全般の回復量を対象とする。
- `SetActivationRollTarget = 30`: `[置換後の目標値]`。通常発動ロールの式・回数を保持。
- 既存 `SetModifier` に `[Rule, 対象識別子, 対象の引数..., 置換値]` を追加。`SkillModifierTextCodec.cs` の `CreateModifierRule` が識別子から元の条件・内容を構成し、基本補正が1件に定まることを検証する。リスト番号・スキル名・原文への参照ではない。

`Rule` の対象識別子と引数：

| 識別子 | 引数 |
|---|---|
| WeaponActivation | 武器カテゴリ |
| ActionStats | 能力値Bの候補（1個以上） |
| EnvironmentCheck | なし |
| CraftCategories | 装備カテゴリの候補（1個以上） |
| TimedWeaponActivation | 武器カテゴリ、行動カテゴリ |
| AcquisitionCost | AddまたはMultiply、AllSkills・Class・SkillCategoryのいずれか、選択対象 |
| DailyGathering | なし |
| EarlyBattleActivation | 終了ターン、能力値B |
| EarlyBattleDamage | 終了ターン |
| ResourceMaximum | リソース名 |
| SalePrice | 除外カテゴリ |
| CategoryPower / CategoryHealing / CategoryCheck | 行動カテゴリ |
| RerollResult | 振り直しに使用する能力値B |

スパイクは基本補正に対する段階別の置換であり、セカンド・サードを累積適用しません。`RerollResult` のみ、対応する振り直し効果の達成値倍率（基本値1）を置換します。正規化したスパイク文は変更対象の補正全文を明記するため、複数補正を持つスキルでも対象を保持できます。
