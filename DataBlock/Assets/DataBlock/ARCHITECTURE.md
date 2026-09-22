# DataBlock 構成

## 方針

- `Runtime/Model` をスキル・エネミーデータの単一モデルとし、カタログに版番号は持たせません。
- テキスト解析はインポート時に行い、ゲーム中は `SkillCatalog` の索引から取得します。
- スキルは表示名ではなく、変更しない `SkillCatalogRecord.Id` で識別します。
- 1万件を1つの巨大データへ保存しない場合は、`SkillCatalogSharding` で固定数のShardへ決定的に分割します。同じIDは常に同じShardへ入ります。

## フォルダー

- `DataBlock.cs`: スキル用MonoBehaviourのエントリーポイント。
- `EnemyDataBlock.cs`: エネミー用MonoBehaviourのエントリーポイント。スキル用データとは分けて保持します。
- `Runtime/Model`: 現行のシリアライズモデルとenum。
- `Runtime/Text`: 構文の読取、条件、スキル効果、状態効果、書出し。
- `Runtime/Import`: 大量テキストを1件ずつ解析し、失敗を個別に返す処理。
- `Runtime/Catalog`: 安定ID、Shard分割、ID・名前・カテゴリー索引。
- `Editor`: Inspector専用コード。Playerビルドには入りません。
- `Editor/Tests`: 往復変換、バッチのエラー隔離、Shard、各文型のEditMode回帰テスト。

## エネミーテキスト

`EnemyTextConverter` は、エネミー本体、行動規則、行動スキル、ドロップ表を1件の `EnemyTextData` へ変換します。行動スキルの中身には既存の `SkillTextConverter` を再利用し、同じ文型を二重実装しません。エネミー名やスキル名による個別処理は持ちません。

行動規則は次の意味データへ分けて保存します。

- 発動タイミング（毎ターン／自身への行動に対する反応）
- 毎ターンの回数
- AND条件またはOR条件
- ランダム対象の選択範囲
- 発動する行動スキル名

参照された行動スキルは同じエネミー内にちょうど1件必要です。未定義、未参照、重複したスキルはエラーになります。ドロップロールは全エネミー共通の `1d100` とし、シリアライズ項目には持ちません。ドロップ表は1～100の範囲内で、各範囲が重ならないことを検証します。範囲に空きがある場合は「ドロップなし」として許可します。

`EnemyDataBlockEditor` では、スキル用Inspectorと独立して、構文統一、テキストからのシリアライズ設定、テキスト再構築、JSON出力を実行できます。

## 大量インポート

`SkillTextBatchProcessor.Parse` は遅延列挙です。入力と結果を全件同時に保持せず、1件が不正でも残りを処理できます。

```csharp
IEnumerable<SkillTextBatchResult> results = SkillTextBatchProcessor.Parse(sources, cancellationToken);

var records = new List<SkillCatalogRecord>();
foreach (SkillTextBatchResult result in results)
{
    if (result.Succeeded) records.Add(result.Record);
    else Debug.LogError(result.SourceId + ": " + result.ErrorMessage);
}

IReadOnlyList<SkillCatalogShard> shards = SkillCatalogSharding.CreateShards(records);
SkillCatalog catalog = SkillCatalog.FromShards(shards);
```

`SkillCatalog` は構築時点の索引です。公開DTOを編集した場合は、索引を作り直してください。

`SkillCatalogShard` は保存単位を分けるためのシリアライズ可能なDTOです。実際のAsset保存、Addressablesによる遅延読込、必要Shardだけのロードは利用側のインポーター／ローダーで実装します。

## 文型を追加する時

1. 必要な条件・効果・トリガーを現行モデルへ追加します。
2. 対象のCodecへ読取、書出し、検証をまとめて追加します。
3. `Build(Parse(text))` と `Normalize(text)` が一致する往復テストを追加します。
4. 正規表現は `M`、`AllMatches`、`RegexReplace` を通し、1秒のタイムアウトと共有キャッシュを維持します。

`Editor/Tests/Fixtures/KnightSkills.txt` は、複数装備候補、状態条件付きスキル値、直接攻撃、軽減、カウンター対象条件などの代表文型9件を固定した回帰fixtureです。

`Editor/Tests/Fixtures/ElementalSageSkills.txt` は、複合属性攻撃、対象範囲、追加ロール、ターン契機、状態連鎖、召喚などの代表文型9件を固定した回帰fixtureです。

`Editor/Tests/Fixtures/ChirupippiEnemy.txt` は、OR行動条件、接近グループ内の対象選択、攻撃への反応、3つの行動スキル、欠落範囲を含むドロップ表を固定したエネミー回帰fixtureです。

## パラメーター契約

### 基本の条件・効果

- `SelectEquippedWeaponFromCategories`: `[個数, カテゴリー1, カテゴリー2, ...]`。候補は2件以上で重複不可です。
- `ActionTarget`: `[Source|Receiver, 行動カテゴリー, SameMeleeGroup|Any|Self]`。
- `ActionOrigin`: 現在は `[Skill]`、`EffectKind`: 現在は `[Counter]` です。
- `SkillAttack`: `[対象actor, 威力式]`。
- `ModifyResource`: `[対象actor, リソース名, signedDelta]`。実行時は現在値へ `signedDelta` を加算し、正規文は「-1変化させる」です。
- `InvalidateAction`: `[対象actor, Single|And|Or, 行動カテゴリー...]`。`Single`は1件、`And` / `Or`は2件以上です。
- `ReduceDamage`: `[対象actor, 軽減式]`。
- `MultiplyDamageTaken`: `[対象actor, 倍率]`、`PreventCounterDamage`: `[対象actor, 行動カテゴリー]`、`AddActionResult`: `[signedDelta]`、`SetDamageReduction`: `[軽減式]`。
- `AddResourceCost`の`Automatic`形は `[Self, リソース名, signedDelta, Automatic]` です。

条件付き`SetSkillValue`は、無条件の基準値より後に適用します。優先順位のない複数条件が同時成立する曖昧さを避けるため、Activeおよび各Spike段階につき状態条件は1件までです。`SetDamageReduction`のSpikeは、無条件のCounter `ReduceDamage` 1件へ適用します。

### 複合属性・追加ロール・召喚

- 複合属性の`SkillAttack`: `[対象, ElementalWeapon, スキル値, 属性威力式, 属性1, 属性2, ...]`。
- 拡張対象は`AllAllies`、`AllEnemiesExceptSelf`、`AllWithState:<状態名>`を共通利用します。
- `SetAttackComponent` / `MultiplyAttackComponent`: `[AttributePower, 値]`。
- `SetAttackRule`: 防御無視は`[Defense, Ignore]`、応答禁止は`[Response, カテゴリー, Prohibit]`。
- `RollDice`: `[ロールID, 式]`、対応する`RollResult`: `[ロールID, 出目]`。同じ出目に複数効果を関連付けられ、`1dN`では1～Nの範囲に制限します。
- `ReapplyEffects`: `[Turn, バフ, All, 回数]`、`Summon`: `[召喚名]`。
- `SummonedEntityDefinition`は`SkillBody`と式付きステータスを持ち、`Choices`とは区別します。ペットにはHP最大値が必要です。
