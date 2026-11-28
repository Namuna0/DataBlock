# DataBlock 構成

## 方針

- `Runtime/Model` をスキル・エネミー・装備・アイテムの現行モデルとし、カタログに版番号は持たせません。
- テキスト解析はインポート時に行い、ゲーム中は `SkillCatalog` の索引から取得します。
- スキルは表示名ではなく、変更しない `SkillCatalogRecord.Id` で識別します。
- 1万件を1つの巨大データへ保存しない場合は、`SkillCatalogSharding` で固定数のShardへ決定的に分割します。同じIDは常に同じShardへ入ります。

## フォルダー

- `DataBlock.cs`: スキル用MonoBehaviourのエントリーポイント。
- `EnemyDataBlock.cs`: エネミー用MonoBehaviourのエントリーポイント。スキル用データとは分けて保持します。
- `EquipmentDataBlock.cs`: 装備用MonoBehaviourのエントリーポイント。スキル・エネミーとは分けて保持します。
- `EquipmentGradeResolver.cs`: 基準装備を変更せず、名前末尾の`☆N`に対応するグレード補正をコピーへ適用します。
- `Runtime/Model`: 現行のシリアライズモデルとenum。
- `Runtime/Text`: 各データの見出し・条件と、共通の効果文型の読み書き。
- `Runtime/Import`: 大量テキストを1件ずつ解析し、失敗を個別に返す処理。
- `Runtime/Catalog`: 安定ID、Shard分割、ID・名前・カテゴリー索引。
- `Editor`: Inspector専用コード。Playerビルドには入りません。
- `Editor/Tests`: 往復変換、バッチのエラー隔離、Shard、各文型のEditMode回帰テスト。

## 効果を追加する時

装備、アイテム、スキル、エネミーの行動、召喚、状態は、効果の種類と引数を `EffectContent` で保持します。たとえば「防御点+17」は、どのデータでも `ModifyStat` と `["Self", "防御点", "Add", "+17"]` になります。効果の適用箇所は装備欄、設置欄、スキルの発動タイミング、スパイクなどの親データで指定します。装備に付いた《効果名》はその装備内に保持します。

効果単体の読取・再構築には `CommonEffectTextCodec` を使います。条件付きの操作、選択武器による補正、出目表の文型はそれぞれ共通の実装に追加します。状態の定義にはすべての所有元で `StateDefinition` を使います。新しい文型は、異なる所有元で効果の種類と引数が一致し、テキストとJSONを往復できることを確認してください。

## エネミーテキスト

`EnemyTextConverter` は、エネミー本体、行動規則、行動スキル、ドロップ表を1件の `EnemyTextData` へ変換します。行動スキルの中身には既存の `SkillTextConverter` を再利用し、同じ文型を二重実装しません。エネミー名やスキル名による個別処理は持ちません。

行動規則は次の意味データへ分けて保存します。

- 発動タイミング（毎ターン／自身への行動に対する反応）
- 毎ターンの回数
- AND条件またはOR条件
- ランダム対象の選択範囲
- 発動する行動スキル名

特性と行動スキルはいずれも共通のスキルモデルを使用します。行動から参照されたスキル名、本文側だけにあるスキル、空欄の能力値は原文どおり保持し、参照先の欠落などをCSV画面で確認できます。同一エネミー内の重複定義はエラーになります。

通常のドロップ判定は固定の `1d100` です。確定報酬と出目範囲を区別し、出目範囲の重複と上限を検証します。捕獲には捕獲用の補正式と結果名を指定できます。ドロップ欄の未記載、明示的な「無し」、「???」は区別して保持します。CSV一括出力では本体が `kind: enemy` になり、特性・行動スキル内の召喚と状態もそれぞれ独立したJSONに出力されます。

`EnemyDataBlockEditor` では、スキル用Inspectorと独立して、構文統一、テキストからのシリアライズ設定、テキスト再構築、JSON出力を実行できます。

## アイテム・食べ物・レシピ

単体の文章は `ItemDataBlock` のInspector、一括変換は `Tools > DataBlock > ルールブックCSV` を使用します。CSV内の装備とエネミーはそれぞれの形式で出力します。アイテム本体には価格、所持条件、ペットの行動、グレード補正などを保存します。食べ物、使用時の効果、レシピの各選択肢は通常スキルと同じ条件・消費・ロール・効果を使います。 本体の使用・所持・設置効果は OwnSkills（0件または1件）、付属スキルとレシピの選択肢は Skills に保存します。付属スキルを並べ替えても本体の効果は保持されます。

製作レシピの材料と完成品は各選択肢に属します。 一括変換画面で除外した定義と選択肢の行・列・理由を確認できます。書籍と選択肢の説明文は個別に再構築します。家具の未確定の設置効果は保留欄に原文を保存します。アイテム内の特性やスキルで定義した状態・召喚も、CSV出力とInspectorのJSON表示では共通の状態・召喚欄にまとめます。

## 装備テキスト

`EquipmentTextConverter` は装備本体、価値、複数の装備部位、装備条件、武器威力、装備効果、状態、装備が付与するスキルを1件の `EquipmentTextData` へ変換します。`EquipmentDataBlockEditor` から、ほかのデータ種別と独立して構文統一、シリアライズ設定、再構築、JSON出力を実行できます。

名前付き効果は `EquipmentEffectGroup` として保持し、《魔法書効果》のような表示名と意味データを分離します。能力値補正、行動倍率、被ダメージ倍率、アイテム使用許可、状態スタック付与を装備名に依存しない効果種別へ変換し、未対応行を生テキストのまま保存しません。

装備が付与するスキルは装備データ内の `SkillTextData` として保持します。トップレベルのスキルカタログへは混在させず、条件・ロール・効果の構文と意味モデルだけを通常スキルと共有するため、スキル文型を二重実装しません。装備テキストの区切り線以降は常に装備本体のフレーバーであり、内包スキルには個別フレーバーを持たせません。

入力時は `css`、`swift`、`diff` などのコードフェンス言語名を輸送情報として受け付けますが、再構築時は言語名なしのフェンスへ統一します。

`【グレード補正】`は各グレードの耐久最大値・武器威力の差分、能力値加算、装備効果の変更を保持します。`【耐久最大値】5, MP最大値+5`のような入力は、耐久最大値`5`と通常の`MP最大値+5`へ意味分離して正規化します。基準装備は補正表を保持し、`EquipmentGradeResolver`が`《装備名》☆5`の参照を解決した時だけ深いコピーへ補正を適用します。解決結果から補正表を除くため、同じ補正が二重適用されません。

本文の【グレード】と補正表はそのまま保存します。本文を読み込んだ段階と表示グレードのままでは、数値の差分を加算しません。グレードを変更した時に、元の装備から変更先の行を1回適用します。☆1・2・4・5の標準表で省略された☆3の数値差分は0です。基準グレードの参照にはsuffixを付けません。非基準グレードの具体化結果は`VariantGrade`を持ち、テキスト再構築でも`《装備名》☆N`を維持します。
武器の条件付き補正は発動条件と操作の組み合わせで保存します。同じ効果名でも各装備が自分の効果と引数を持ちます。装備時・所持時・設置時の効果、武器の選択肢、ダイスの出目表も装備内に保持します。

グレード値は再構築時の過大な文字列確保を防ぐため1～100に制限し、補正表の範囲外にあるグレードはエラーになります。

## ルールブックCSV

Unity の `Tools > DataBlock > ルールブックCSV` でファイルを選択します。セルごとに元の行・列、再構築した本文、JSONを確認でき、成功分をまとめて出力できます。CSVの座標は原稿の確認に使用します。ゲーム側のカタログでは安定IDを使用します。

本文に「秘匿」または「兵器」を含むセルは除外します。指定された5件だけを `RulebookCsv` の収録例外にしています。見出しだけのセル、簡略メモ、目標値や選択肢が欠けた草案は読み飛ばします。本文のある未対応の書式はセルの位置と理由を表示します。

スキル・種族特性、種族・クラスの定義、兵器、装備はそれぞれの型で保存します。装備・投擲アイテムなどの本文は装備用Inspectorと同じ変換処理を通り、一括JSONでは `kind: equipment` で識別できます。装備の名前付き効果は本体に、状態とサモンの定義はそれぞれの出力に保存します。効果の文型を追加する際は `Runtime/Text` の行動・常時・状態・結果別の `*SentenceRules.cs` に、意味を表す種別と引数を指定します。同じ文型は全スキルで共有します。ゲーム中のダイスや戦闘はこれらの型を利用する側で処理します。

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

`Editor/Tests/Fixtures/AdventurerSkills.txt` は、通常の発動ロールと各スパイク段階による発動式の置換を固定した回帰fixtureです。

`Editor/Tests/Fixtures/ChirupippiEnemy.txt` は、OR行動条件、接近グループ内の対象選択、攻撃への反応、3つの行動スキル、欠落範囲を含むドロップ表を固定したエネミー回帰fixtureです。

`Editor/Tests/Fixtures/EquipmentItems.txt` は、通貨と取引不可、複数装備部位、武器威力、名前付き効果、スタック状態、装備が付与するスキル、グレード補正を含む代表装備7件を固定した回帰fixtureです。

## 選択式スキルの共有スタック状態

`WizardSkills.txt`は、通常展開と即時展開で同じ状態を共有する代表文型です。実行コードにスキル名による分岐は設けません。

- `GainStack`の3引数形は、正の整数に加えて`[スキル値Nの武器威力]*倍率`を保持します。式は評価・丸めをせず保存し、不正な式や非正の指定値は拒否します。
- スタックに比例する防御点は、既存`ModifyStat`の`[Self, 防御点, Add, +[スタック]*防御点/参照スタック数]`として保持します。`[スタック]`はその状態の現在スタック数です。
- `ConsumeStacks`は`[Self, 状態名, ReceivedActionPower]`。`ActionReceived`と対象Self・行動カテゴリーで、受けた行動の解決後を表します。防御適用前に防御用スタックを消費したり、宣言だけで減らしたりしません。参照するのは受けた攻撃の威力であり、軽減後の被ダメージではありません。被ダメージが0でも攻撃を受けた場合は区別して扱います。状態の自動解除や丸め規則は追加しません。
- 異なる子スキル間で同じ状態を再掲する場合、カテゴリーが一致し、片方の説明が他方の完全な部分集合なら、情報の多い定義を1件保持します。同じ所有者内の重複、矛盾した値、相互に異なる追加効果の自動合成は拒否します。
- コードフェンスがエスケープされ、ブロックの各行が一貫して末尾のバックスラッシュで継続された入力は輸送表現として復号します。通常のフレーバー中のバックスラッシュは変更しません。

2026-11-16の確認では、追加文型・名前と数値の変更・共有定義の矛盾・エスケープ入力・Inspectorの4操作・JSON往復を含むEditModeテスト107件がすべて成功しました。

## パラメーター契約

### 一般スキル

- `HasFreeHand`は少なくとも片手が素手であることを表します。`SkillAttack`の`[対象, IgnoreOwnStateCategory, カテゴリー, 威力式]`は、この一撃の間だけ使用者に付与された該当カテゴリーの効果を無視します。
- クリティカル時の回復量は`MultiplyRecovery`、ファンブル時の対象への固定ダメージは`Damage`で保持します。スパイクの`AddTargetValue`は各段階で元の発動ロール目標値に加算します。GMまたはエリアが指定する目標値もそのまま残します。
- `ApplyPermanentLinkedStates`と`RemoveLinkedStates`の引数は順に「自身の状態名」「相手の状態名」です。付与時に選んだ1体を相手として記録し、解除時にはその組を参照します。`UsePartnerMeleeGroup`、`UsePartnerStat`、`SharePartnerAreaEligibility`、`FollowPartnerBattleExit`はいずれも保持者から同じ相手を参照します。

### 基本の条件・効果

- `SelectEquippedWeaponFromCategories`: `[個数, カテゴリー1, カテゴリー2, ...]`。候補は2件以上で重複不可です。
- `ActionTarget`: `[Source|Receiver, 行動カテゴリー, SameMeleeGroup|Any|Self]`。
- `ActionOrigin`: 現在は `[Skill]`、`EffectKind`: 現在は `[Counter]` です。
- `SkillAttack`: `[対象actor, 威力式]`。
- `MinimumEquippedDays`: `[装備からの経過日数]`、日単位の`ActivationLimit`: `[Day, 回数]`。
- `ModifyResource`: `[対象actor, リソース名, signedDelta]`。実行時は現在値へ `signedDelta` を加算し、正規文は「-1変化させる」です。
- `InvalidateAction`: `[対象actor, Single|And|Or, 行動カテゴリー...]`。`Single`は1件、`And` / `Or`は2件以上です。
- `ReduceDamage`: `[対象actor, 軽減式]`。
- `MultiplyDamageTaken`: `[対象actor, 倍率]`、`PreventCounterDamage`: `[対象actor, 行動カテゴリー]`、`AddActionResult`: `[signedDelta]`、`SetDamageReduction`: `[軽減式]`。
- `AddResourceCost`の`Automatic`形は `[Self, リソース名, signedDelta, Automatic]` です。
- `AddAreaGathering`: `[追加回数]`。自動成功ロールは `Count=1`、`Formula=自動成功`、空の`Target`で保持します。
- `SetActivationRollFormula`: `[置換後の式]`。Second/Third Spikeに無条件で各1件だけ指定でき、通常発動ロールの`Count`と`Target`は維持します。

条件付き`SetSkillValue`は、無条件の基準値より後に適用します。優先順位のない複数条件が同時成立する曖昧さを避けるため、Activeおよび各Spike段階につき状態条件は1件までです。`SetDamageReduction`のSpikeは、無条件のCounter `ReduceDamage` 1件へ適用します。

### 複合属性・追加ロール・召喚

- 複合属性の`SkillAttack`: `[対象, ElementalWeapon, スキル値, 属性威力式, 属性1, 属性2, ...]`。
- 拡張対象は`AllAllies`、`AllEnemiesExceptSelf`、`AllWithState:<状態名>`を共通利用します。
- `SetAttackComponent` / `MultiplyAttackComponent`: `[AttributePower, 値]`。
- `SetAttackRule`: 防御無視は`[Defense, Ignore]`、応答禁止は`[Response, カテゴリー, Prohibit]`。
- `RollDice`: `[ロールID, 式]`、対応する`RollResult`: `[ロールID, 出目]`。同じ出目に複数効果を関連付けられ、`1dN`では1～Nの範囲に制限します。
- `ReapplyEffects`: `[Turn, バフ, All, 回数]`、`Summon`: `[召喚名]`。
- `SummonedEntityDefinition`は`SkillBody`と式付きステータスを持ち、`Choices`とは区別します。ペットにはHP最大値が必要です。

## 獣人系の13件と追加文型

`Editor/Tests/Fixtures/BeastfolkSkills.txt` は、犬人・狼・狐・狸・熊の13件を保持します。実運用では1件ずつInspectorの構文統一→シリアライズセット→再構築→JSON出力を行います。JSONのルートは `Skill / Summons / States`、enumの既存数値は変更しません。

- 罠は `Summons` の〈罠〉定義として保存し、`PlaceTrap` で参照します。ペットと異なりHP最大値を必須とせず、`RemoveSummon` は罠のDeclaration・AfterSkillResolutionでのみ有効です。
- `ReactionTarget`: `[行動カテゴリー, Self, Source]`、または `[行動カテゴリー, Ally, Receiver, Automatic]`。自身に対する行動への反応と、味方への行動に反応する自動発動を区別します。
- `SkillAttack` の追加形は `[Target, PowerAndAttribute, 威力式, 属性威力式]`。`SetAttackComponent` の `Power` と `AttributePower` はそれぞれを変更します。
- `GainStack` の上限なし形は `[対象, 状態名, 付与数]`。上限ありの形は4引数として区別します。
- `MultiplyDamageTaken` の上限付き形は `[Self, 倍率式, 最大倍率]`。上限なしの形は2引数として区別します。
- `AllAlliesExceptSelf` は味方への行為判定補正と任意のリソースダメージ補正で使用します。
- 状態の複数イベントは各効果の `Triggers` に分けます。複数トリガーはORです。単独の被ダメージ・攻撃威力補正は既存の状態共通トリガー形式を維持します。
- ロールプレイ効果のみ `RoleplayDescription` に説明文を保存します。戦闘効果の未対応文を説明文へ逃がしません。

`BeastfolkSkillTextConverterTests` は13件のInspector操作処理とUnity JsonUtilityの往復、意味データの各値、罠参照、スタック補正の適用先、重複スパイク、不正な状態倍率などを検証します。

## 文型処理の責務

実行コードは種族・クラス・追加時期で分けず、次の責務へまとめます。

- `SkillInputTextCodec`: 改行や表記の正規化。スキル名で入力を補修しません。
- `ConditionTextCodec`: 習得・宣言・対象選択・反応条件。
- `SkillEffectReaders` / `SkillContentTextCodec` / `SkillTriggerTextCodec`: 効果文の解析、意味データの書出し、イベント条件。
- `SkillModifierTextCodec` / `SkillSpikeTextCodec`: 補正の対象と、段階ごとの置換。
- `StateTextCodec` / `StateEffectTextCodec` / `StateModifierTextCodec`: 状態の効果・イベント・補正。
- `SemanticRuleTextCodec`: 名前や数値を引数として扱う共通文型表。
- `CharacterRuleTextCodec`: エリア・種族選択・配分・報酬・パーティー条件の意味データ。
- `SkillEffectValidation` / `SkillDataValidation`: 効果内の整合性と、状態・召喚をまたぐ参照の整合性。

入力の名前、地名、数値はデータとして保持します。曖昧な重複発動ロールや不足したフェンスはエラーとし、特定のスキル名を理由に別の構造へ変換しません。状態の「自身のターン」と「付与者のターン」は別の条件として保持します。スパイクの重複は同じ段階・同じ適用先について検証します。

既存の各種文型の意味・引数は、`HUMAN_SKILL_SUPPORT.md`、`MAGIC_SKILL_SUPPORT.md`、`SPIRIT_SKILL_SUPPORT.md`、`BEAST_RACE_PART1_SUPPORT.md`、`BEAST_RACE_PART2_SUPPORT.md`、`NONLIVING_SKILL_SUPPORT.md`、`GOD_SKILL_SUPPORT.md`に記録しています。これらは入力資料の分類であり、実行コードの分割基準にはしません。

`SkillArchitectureTests`は代表文型の往復変換、名前・値の変更、重複ロール、段階別スタック変更、状態の行動者とターンの区別、正規化の冪等性、スキル値と属性威力の分離、1万件のShard分割と索引を検証します。追加7バッチの原文全件はfixtureとして保存されていないため、代表文型と既存fixtureによる回帰確認です。1万件のテストはカタログの件数・検索の正しさを対象とし、実戦効果の全組合せや実行速度の保証ではありません。

2026-10-03の確認では、Unity 6000.6.2f1のEditModeテスト89件がすべて成功しました。JSON往復、Inspectorの4操作、1万件のカタログ検証を含みます。
