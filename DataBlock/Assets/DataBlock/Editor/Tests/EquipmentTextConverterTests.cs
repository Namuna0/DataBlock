#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

internal sealed class EquipmentTextConverterTests
{
    private static readonly string[] ExpectedNames =
    {
        "メルストロム・テラー",
        "白いスカーフ",
        "ドラゴンスケールシールド",
        "ショコラティエ・クラシカルボンネット",
        "永夜の魔導服",
        "ウォッチャーのモノクル",
        "見習い魔女の紺色とんがり帽"
    };

    private const string MinimalSkill =
        "```\n" +
        "《試験》\n" +
        "〈攻撃〉\n" +
        "【宣言条件】キャラクターを1体選択して宣言可能。\n" +
        "【アクティブ効果】対象をスキル値10で武器攻撃する。\n" +
        "```";

    [Test]
    public void GrantedCounterKeepsWeaponConditionAndCategoryRatio()
    {
        const string text = "《潮の槍》\n〈装備アイテム〉\n【レアリティ】Rare\n【サイズ】大\n" +
            "●スキル：《追撃》\n【宣言条件】この武器による自身の〈刺突〉が命中した時、キャラクターを1体選択\n" +
            "【カウンター効果】命中した〈刺突〉の威力の×0.67倍の威力で対象を攻撃する。";
        var item = EquipmentTextConverter.Parse(text);
        var skill = item.Equipment.Skills.Single().Skill;
        Assert.That(skill.DeclarationConditions.And.First().Type, Is.EqualTo(ConditionType.HitWithThisWeapon));
        var effect = skill.Effects.Single().Contents.Single();
        Assert.That(effect.Type, Is.EqualTo(EffectContentType.AttackFromHitPower));
        CollectionAssert.AreEqual(new[] { "刺突", "0.67" }, effect.Parameters);
        Assert.That(EquipmentTextConverter.Normalize(EquipmentTextConverter.Build(item)), Is.EqualTo(EquipmentTextConverter.Build(item)));
    }

    [Test]
    public void RollTableKeepsStateReferenceAndRejectsOverlappingRows()
    {
        const string text = "《入浴剤》\n〈装備アイテム〉\n【レアリティ】Common\n【サイズ】小\n" +
            "【装備効果】自身が〈休憩所〉でMPを全回復した時、1d4のダイスロールを行い以下の効果を適用しても良い。\n" +
            "- 1～3：何もなし。\n- 4：自身は永続的に《安息》状態になる。安息効果〈マインド〉：自身のMP最大値が+2加算される。";
        var data = EquipmentTextConverter.Parse(text);
        var table = data.Equipment.EffectGroups.Single().Effects;
        CollectionAssert.AreEqual(new[] { "FullRecoveryInArea", "休憩所", "MP", "4" }, table[0].Parameters);
        CollectionAssert.AreEqual(new[] { "PermanentState", "4", "4", "安息" }, table[2].Parameters);
        var export = new DataBlockJsonExport();
        var main = export.Main(data)["Equipment"].AsObject();
        Assert.That(main.ContainsKey("States"), Is.False);
        Assert.That((string)main["EffectGroups"][0]["Effects"][2]["Parameters"][3], Is.EqualTo("安息"));
        Assert.That((string)export.States.ToArray()[0]["Name"], Is.EqualTo("安息"));
        string built = EquipmentTextConverter.Build(data);
        Assert.That(EquipmentTextConverter.Normalize(built), Is.EqualTo(built));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(text.Replace("- 1～3", "- 1～4")));
    }

    [Test]
    public void EquipmentItems_AllSevenNormalizeAndRoundTrip()
    {
        string source = ReadFixture("EquipmentItems.txt");
        MatchCollection sourceBlocks = Blocks(source);
        string normalized = EquipmentTextConverter.Normalize(source);
        MatchCollection normalizedBlocks = Blocks(normalized);

        Assert.That(sourceBlocks.Count, Is.EqualTo(ExpectedNames.Length));
        Assert.That(normalizedBlocks.Count, Is.EqualTo(ExpectedNames.Length));
        Assert.That(EquipmentTextConverter.Normalize(normalized), Is.EqualTo(normalized));
        StringAssert.DoesNotContain("```css", normalized);
        StringAssert.DoesNotContain("```swift", normalized);
        StringAssert.DoesNotContain("```none", normalized);
        StringAssert.DoesNotContain("```diff", normalized);
        Assert.That(normalized.Split('\n'), Does.Contain("【宣言条件】装備から1日以上経過している事, 1日1回まで"));
        StringAssert.Contains("無限の霜星効果〈スタック〉：", normalized);

        for (int i = 0; i < ExpectedNames.Length; i++)
        {
            EquipmentTextData parsed = EquipmentTextConverter.Parse(sourceBlocks[i].Value);
            Assert.That(parsed.Equipment.Name, Is.EqualTo(ExpectedNames[i]));
            Assert.That(EquipmentTextConverter.Build(parsed), Is.EqualTo(normalizedBlocks[i].Value));
            Assert.That(EquipmentTextConverter.Build(EquipmentTextConverter.Parse(normalizedBlocks[i].Value)),
                Is.EqualTo(normalizedBlocks[i].Value));
        }
    }

    [Test]
    public void WitchHat_MapsGradeAdjustmentsAndNormalizesInlineDurabilityEffect()
    {
        EquipmentDefinition hat = Items()["見習い魔女の紺色とんがり帽"].Equipment;

        Assert.That(hat.Categories,
            Is.EqualTo(new[] { "装備アイテム", "魔法服", "縫製", "白綿製" }));
        Assert.That(hat.Grade, Is.EqualTo(3));
        Assert.That(hat.MaxDurability, Is.EqualTo(5));
        Assert.That(StatTotal(hat, "MP最大値"), Is.EqualTo(5m));
        Assert.That(StatTotal(hat, "防御点"), Is.EqualTo(10m));
        Assert.That(hat.GradeAdjustments.Select(x => x.Grade), Is.EqualTo(new[] { 1, 2, 4, 5 }));

        var expected = new Dictionary<int, int[]>
        {
            { 1, new[] { -4, -2 } },
            { 2, new[] { -2, -1 } },
            { 4, new[] { 2, 1 } },
            { 5, new[] { 4, 2 } }
        };
        foreach (EquipmentGradeAdjustmentDefinition adjustment in hat.GradeAdjustments)
        {
            Assert.That(adjustment.MaxDurabilityDelta,
                Is.EqualTo(expected[adjustment.Grade][0]));
            Assert.That(adjustment.Modifiers.Single().Parameters,
                Is.EqualTo(new[]
                {
                    "Self", "防御点", "Add",
                    SignedIntegerText(expected[adjustment.Grade][1])
                }));
        }

        string normalized = EquipmentTextConverter.Build(
            Items()["見習い魔女の紺色とんがり帽"]);
        StringAssert.Contains("【耐久最大値】5\n", normalized);
        StringAssert.DoesNotContain("【耐久最大値】5,", normalized);
        StringAssert.Contains("・MP最大値+5", normalized);
        StringAssert.Contains("【グレード補正】\n- ☆1：耐久最大値-4, 防御点-2", normalized);

        EquipmentTextData inlineResolved = EquipmentTextConverter.Parse(
            Block("見習い魔女の紺色とんがり帽").Replace(
                "《見習い魔女の紺色とんがり帽》",
                "《見習い魔女の紺色とんがり帽》☆5"));
        Assert.That(inlineResolved.Equipment.Grade, Is.EqualTo(5));
        Assert.That(inlineResolved.Equipment.VariantGrade, Is.EqualTo(5));
        Assert.That(inlineResolved.Equipment.MaxDurability, Is.EqualTo(9));
        Assert.That(inlineResolved.Equipment.GradeAdjustments, Is.Empty);
    }

    [TestCase("", 3, 5, 10)]
    [TestCase("☆1", 1, 1, 8)]
    [TestCase("☆2", 2, 3, 9)]
    [TestCase("☆4", 4, 7, 11)]
    [TestCase("☆5", 5, 9, 12)]
    public void GradeResolver_AppliesReferenceWithoutMutatingTemplate(
        string suffix,
        int expectedGrade,
        int expectedDurability,
        int expectedDefense)
    {
        EquipmentTextData template = Items()["見習い魔女の紺色とんがり帽"];
        string reference = "《見習い魔女の紺色とんがり帽》" + suffix;
        EquipmentTextData resolved = EquipmentGradeResolver.Resolve(template, reference);

        Assert.That(resolved.Equipment.Grade, Is.EqualTo(expectedGrade));
        Assert.That(resolved.Equipment.VariantGrade,
            Is.EqualTo(suffix.Length == 0 ? 0 : expectedGrade));
        Assert.That(resolved.Equipment.MaxDurability, Is.EqualTo(expectedDurability));
        Assert.That(StatTotal(resolved.Equipment, "防御点"), Is.EqualTo((decimal)expectedDefense));
        Assert.That(StatTotal(resolved.Equipment, "MP最大値"), Is.EqualTo(5m));
        Assert.That(resolved.Equipment.GradeAdjustments.Count, Is.EqualTo(suffix.Length == 0 ? 4 : 0));
        Assert.That(EquipmentGradeResolver.FormatReference(template, expectedGrade), Is.EqualTo(reference));

        Assert.That(template.Equipment.Grade, Is.EqualTo(3));
        Assert.That(template.Equipment.MaxDurability, Is.EqualTo(5));
        Assert.That(template.Equipment.GradeAdjustments.Count, Is.EqualTo(4));

        EquipmentTextData resolvedAgain = EquipmentGradeResolver.Resolve(resolved, expectedGrade);
        Assert.That(resolvedAgain.Equipment.MaxDurability, Is.EqualTo(expectedDurability));
        Assert.That(StatTotal(resolvedAgain.Equipment, "防御点"), Is.EqualTo((decimal)expectedDefense));
        string rebuilt = EquipmentTextConverter.Build(resolved);
        StringAssert.StartsWith("```\n" + reference + "\n", rebuilt);
        Assert.That(EquipmentTextConverter.Build(EquipmentTextConverter.Parse(rebuilt)), Is.EqualTo(rebuilt));
    }

    [Test]
    public void EquipmentItems_MapMetadataAndOptionalFields()
    {
        Dictionary<string, EquipmentTextData> items = Items();
        EquipmentDefinition maelstrom = items["メルストロム・テラー"].Equipment;
        EquipmentDefinition scarf = items["白いスカーフ"].Equipment;
        EquipmentDefinition bonnet = items["ショコラティエ・クラシカルボンネット"].Equipment;
        EquipmentDefinition eternalNight = items["永夜の魔導服"].Equipment;
        EquipmentDefinition watcher = items["ウォッチャーのモノクル"].Equipment;

        Assert.That(maelstrom.Categories,
            Is.EqualTo(new[] { "装備アイテム", "魔法書", "魔法武器", "革加工" }));
        Assert.That(maelstrom.Rarity, Is.EqualTo("Ultimate Rare"));
        Assert.That(maelstrom.Grade, Is.EqualTo(5));
        Assert.That(maelstrom.Value.Kind, Is.EqualTo(EquipmentValueKind.Currency));
        Assert.That(maelstrom.Value.Amount, Is.EqualTo(432));
        Assert.That(maelstrom.Value.Currency, Is.EqualTo("ジルダ"));
        Assert.That(maelstrom.EquipSlots, Is.EqualTo(new[] { "片手" }));
        Assert.That(maelstrom.Size, Is.EqualTo("大"));
        Assert.That(maelstrom.MaxDurability, Is.EqualTo(5));
        Assert.That(maelstrom.WeaponPowerFormula, Is.EqualTo("[スキル値]*1.5+3d100*[魔力B]"));

        Assert.That(scarf.Rarity, Is.EqualTo("Common"));
        Assert.That(scarf.Grade, Is.EqualTo(2));
        Assert.That(scarf.Value.Amount, Is.EqualTo(6));
        Assert.That(scarf.Size, Is.EqualTo("中"));
        Assert.That(scarf.WeaponPowerFormula, Is.Empty);

        Assert.That(bonnet.Value.Kind, Is.EqualTo(EquipmentValueKind.Untradeable));
        Assert.That(bonnet.Value.Amount, Is.EqualTo(0));
        Assert.That(bonnet.Value.Currency, Is.Empty);

        Assert.That(eternalNight.EquipSlots, Is.EqualTo(new[] { "胴体", "脚/足" }));
        Assert.That(eternalNight.MaxDurability, Is.EqualTo(10));

        EquipmentRequirementDefinition requirement = watcher.EquipRequirements.Single();
        Assert.That(requirement.Type, Is.EqualTo(EquipmentRequirementType.MinimumLevel));
        Assert.That(requirement.Parameters, Is.EqualTo(new[] { "6" }));
        Assert.That(watcher.EquipSlots, Is.EqualTo(new[] { "アクセサリー2" }));
        StringAssert.StartsWith("細い金属枠に磨き上げた片眼鏡", watcher.Flavor);
    }

    [Test]
    public void EquipmentItems_MapEffectsStatesAndGrantedSkillToSemanticNodes()
    {
        Dictionary<string, EquipmentTextData> items = Items();

        EquipmentDefinition maelstrom = items["メルストロム・テラー"].Equipment;
        EquipmentEffectGroup book = maelstrom.EffectGroups.Single(x => x.Name == "魔法書効果");
        Assert.That(book.Effects.Single().Type, Is.EqualTo(EffectContentType.ModifySelectedWeaponAction));
        Assert.That(book.Effects.Single().Parameters, Is.EqualTo(new[] { "攻撃", "1.1", "0.91" }));
        AssertModifyStats(maelstrom, new[] { "Self", "水属性B", "Add", "+0.2" });

        AssertModifyStats(items["白いスカーフ"].Equipment,
            new[] { "Self", "敏捷B", "Add", "+0.05" });

        EquipmentDefinition shield = items["ドラゴンスケールシールド"].Equipment;
        AssertModifyStats(shield, new[] { "Self", "防御点", "Add", "+13" });
        EffectContent scales = shield.EffectGroups.Single(x => x.Name == "竜鱗").Effects.Single();
        Assert.That(scales.Type, Is.EqualTo(EffectContentType.MultiplyIncomingDamage));
        Assert.That(scales.Parameters, Is.EqualTo(new[] { "Self", "魔法", "0.83" }));

        EquipmentDefinition bonnet = items["ショコラティエ・クラシカルボンネット"].Equipment;
        Assert.That(StatParameters(bonnet), Is.EquivalentTo(new[]
        {
            new[] { "Self", "防御点", "Add", "+5" },
            new[] { "Self", "HP最大値", "Add", "+25" },
            new[] { "Self", "生命B", "Add", "+0.08" },
            new[] { "Self", "器用B", "Add", "+0.08" },
            new[] { "Self", "魅力B", "Add", "+0.08" }
        }));
        EffectContent sweets = Effects(bonnet).Single(x => x.Type == EffectContentType.AllowItemUse);
        Assert.That(sweets.Parameters, Is.EqualTo(new[] { "Self", "Encounter", "お菓子" }));

        EquipmentDefinition eternalNight = items["永夜の魔導服"].Equipment;
        Assert.That(StatParameters(eternalNight), Does.Contain(new[] { "Self", "防御点", "Add", "+40" }));
        Assert.That(StatParameters(eternalNight), Does.Contain(new[] { "Self", "MP最大値", "Add", "+20" }));
        EffectContent magicPower = Effects(eternalNight).Single(x => x.Type == EffectContentType.MultiplyActionPower);
        Assert.That(magicPower.Parameters, Is.EqualTo(new[] { "Self", "魔法", "1.2" }));
        EffectContent stack = Effects(eternalNight).Single(x => x.Type == EffectContentType.GainStateStacksOnAction);
        Assert.That(stack.Parameters, Is.EqualTo(new[] { "Self", "魔法", "無限の霜星", "1" }));
        StateDefinition frost = eternalNight.States.Single();
        Assert.That(frost.Name, Is.EqualTo("無限の霜星"));
        Assert.That(frost.Categories, Is.EqualTo(new[] { "スタック" }));
        Assert.That(frost.Overrides.Single().Contents.Single().Type, Is.EqualTo(EffectContentType.ModifyStat));
        Assert.That(frost.Overrides.Single().Contents.Single().Parameters,
            Is.EqualTo(new[] { "Self", "MP最大値", "Multiply", "(1+[スタック]*0.1)" }));

        EquipmentDefinition watcher = items["ウォッチャーのモノクル"].Equipment;
        Assert.That(StatParameters(watcher), Is.EquivalentTo(new[]
        {
            new[] { "Self", "知力B", "Add", "+0.05" },
            new[] { "Self", "感知B", "Add", "+0.05" }
        }));
        SkillBody observation = watcher.Skills.Single().Skill;
        Assert.That(observation.Name, Is.EqualTo("観察眼"));
        Assert.That(observation.DeclarationConditions.And.Single(x => x.Type == ConditionType.MinimumEquippedDays).Parameters,
            Is.EqualTo(new[] { "1" }));
        Assert.That(observation.DeclarationConditions.And.Single(x => x.Type == ConditionType.ActivationLimit).Parameters,
            Is.EqualTo(new[] { "Day", "1" }));
        Assert.That(observation.Roll.Count, Is.EqualTo(1));
        Assert.That(observation.Roll.Formula, Is.EqualTo("自動成功"));
        Assert.That(observation.Roll.Target, Is.Empty);
        EffectContent gathering = observation.Effects.SelectMany(x => x.Contents).Single();
        Assert.That(gathering.Type, Is.EqualTo(EffectContentType.AddAreaGathering));
        Assert.That(gathering.Parameters, Is.EqualTo(new[] { "1" }));
        Assert.That(watcher.Flavor, Does.Not.Contain("【アクティブ効果】"));
    }

    [Test]
    public void NamedWeaponEffectsKeepTheirOwnScopeAndParameters()
    {
        const string original = "自身の敏捷Bが+0.05される。";
        string effect = "《任意の効果名》：この武器の選択を条件にスキルを発動する場合、" +
            "自身の[器用B]による達成値×1.1倍かつ効果対象が〈大型〉である場合〈攻撃〉の威力は×0.91倍される。";
        string first = Block("白いスカーフ").Replace(original, effect);
        string second = first.Replace("白いスカーフ", "別の装備").Replace("大型", "小型").Replace("0.91", "0.75");
        var groups = new[] { first, second }.Select(source =>
        {
            var data = EquipmentTextConverter.Parse(source);
            var roundTrip = JsonUtility.FromJson<EquipmentTextData>(JsonUtility.ToJson(data));
            Assert.That(EquipmentTextConverter.Build(roundTrip), Is.EqualTo(EquipmentTextConverter.Normalize(source)));
            return roundTrip.Equipment.EffectGroups.Single();
        }).ToArray();
        foreach (var group in groups)
        {
            Assert.That(group.Name, Is.EqualTo("任意の効果名"));
            Assert.That(group.Effects.Count, Is.EqualTo(2));
            Assert.That(group.Effects.All(e => e.Type == EffectContentType.SelectedWeaponRule), Is.True);
            CollectionAssert.AreEqual(new[] { "StatResultFactor", "器用B", "1.1" }, group.Effects[0].Parameters);
        }
        CollectionAssert.AreEqual(new[] { "TargetCategoryPower", "大型", "攻撃", "0.91" }, groups[0].Effects[1].Parameters);
        CollectionAssert.AreEqual(new[] { "TargetCategoryPower", "小型", "攻撃", "0.75" }, groups[1].Effects[1].Parameters);
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(first.Replace("0.91倍", "不明倍")));
    }

    [TestCase("このアイテムを消費リソースとして消費する場合、2回アクティブ効果を適用する事が出来る。",
        "アクティブ効果の適用後に消費した分が所持品に戻る。", "ItemConsumedAsCost", "RefundConsumedItems")]
    [TestCase("このアイテムによって自身と同じ接近グループの対象に〈攻撃〉を発動する時、与ダメージは×1.33倍される。",
        "対象は3ターンの間《試験》状態となる。", "ItemMeleeAction", "TargetEntersTimedState")]
    [TestCase("この武器によって、1ターンの間に同一対象に3回以上のダメージを与えた時、以降追加で150の水属性攻撃を与えても良い。",
        "この効果による被ダメージは軽減する事は出来ない。", "WeaponMultiHit", "CannotReduceDamage")]
    public void ContinuationSharesTheTriggerAndNamedGroup(string action, string continuation,
        string trigger, string operation)
    {
        string source = Block("白いスカーフ").Replace("自身の敏捷Bが+0.05される。",
            "《追加効果》：" + action + "\n" + continuation);
        var data = EquipmentTextConverter.Parse(source);
        var group = data.Equipment.EffectGroups.Single();
        Assert.That(group.Name, Is.EqualTo("追加効果"));
        Assert.That(group.Effects.Count, Is.EqualTo(2));
        Assert.That(group.Effects[0].Parameters[0], Is.EqualTo(trigger));
        var follow = group.Effects[1].Parameters;
        Assert.That(follow.Take(2), Is.EqualTo(new[] { trigger, operation }));
        if (trigger == "ItemMeleeAction") Assert.That(follow[2], Is.EqualTo("攻撃"));
        if (trigger == "WeaponMultiHit") Assert.That(follow.Skip(2), Is.EqualTo(new[] { "1", "3" }));
        string built = EquipmentTextConverter.Build(data);
        Assert.That(JsonUtility.ToJson(EquipmentTextConverter.Parse(built)), Is.EqualTo(JsonUtility.ToJson(data)));
    }

    private const string SharedSkillHeader = "《合図》\n〈一般スキル〉\n";
    private const string SharedItemHeader = "《呼び鈴》\n〈アイテム〉\n【レアリティ】Common\n【サイズ】小\n【パッシブ効果】";
    private const string AreaRecoveryRule = "自身がエリア〈虹の庭〉でスキル《鐘》の発動に成功した時、MPを19回復しても良い。";

    [Test]
    public void EachSharedEffectHasADistinctSerializedValue()
    {
        var values = Enum.GetValues(typeof(EffectContentType)).Cast<EffectContentType>().ToArray();
        Assert.That(values.Distinct().Count(), Is.EqualTo(values.Length));
    }

    [TestCase("対象を137+1d100の威力で攻撃する。", "アクティブ効果", EffectContentType.SkillAttack, "137+1d100")]
    [TestCase(AreaRecoveryRule, "パッシブ効果", EffectContentType.ConditionalRule, "虹の庭")]
    [TestCase("防御点+17", "パッシブ効果", EffectContentType.ModifyStat, "+17")]
    public void SameEffectWorksInSkillsEquipmentItemsAndEnemySkills(
        string sentence, string section, EffectContentType type, string argument)
    {
        string skill = SharedSkillHeader + "【" + section + "】" + sentence;
        var skillData = SkillTextConverter.Parse(skill);
        var expected = SingleSkillEffect(skillData.Skill);
        Assert.That(expected.Type, Is.EqualTo(type));
        Assert.That(expected.Parameters, Does.Contain(argument));
        string expectedJson = JsonUtility.ToJson(expected);
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(SkillTextConverter.Build(skillData))),
            Is.EqualTo(JsonUtility.ToJson(skillData)));

        string equipment = Block("白いスカーフ").Replace("自身の敏捷Bが+0.05される。", "《鈴音》：" + sentence);
        string item = SharedItemHeader + sentence;
        foreach (string source in new[] { equipment, item })
        {
            var data = EquipmentTextConverter.Parse(source);
            var restored = JsonUtility.FromJson<EquipmentTextData>(JsonUtility.ToJson(data));
            var group = restored.Equipment.EffectGroups.Single();
            Assert.That(group.Name, Is.EqualTo(source == equipment ? "鈴音" : ""));
            Assert.That(JsonUtility.ToJson(group.Effects.Single()), Is.EqualTo(expectedJson));
            Assert.That(JsonUtility.ToJson(EquipmentTextConverter.Parse(EquipmentTextConverter.Build(restored))),
                Is.EqualTo(JsonUtility.ToJson(data)));
        }

        string enemy = ReadFixture("ChirupippiEnemy.txt").Replace("【アクティブ効果】対象を50+1d100の威力で攻撃する。",
            "【" + section + "】" + sentence);
        var enemyData = EnemyTextConverter.Parse(enemy);
        var enemySkill = enemyData.Enemy.Skills.Single(x => x.Skill.Name == "つつく");
        Assert.That(JsonUtility.ToJson(SingleSkillEffect(enemySkill.Skill)), Is.EqualTo(expectedJson));
        Assert.That(JsonUtility.ToJson(EnemyTextConverter.Parse(EnemyTextConverter.Build(enemyData))),
            Is.EqualTo(JsonUtility.ToJson(enemyData)));
    }

    [TestCase("自身の〈魔法〉による威力が×1.2倍される。")]
    [TestCase("〈魔法〉の威力が×1.2倍される。")]
    public void CategoryPowerModifierAndItsSpikeReferToTheSameEffect(string passive)
    {
        const string equipped = "自身の〈魔法〉による威力が×1.2倍される。";
        string skill = SharedSkillHeader + "【パッシブ効果】" + passive +
            "\n【セカンドスパイク】このパッシブ効果による威力上昇は×1.5倍に変化する。";
        var data = SkillTextConverter.Parse(skill);
        var baseModifier = data.Skill.Overrides.Single(x => x.Type == EffectType.Passive);
        var atom = baseModifier.Contents.Single();
        Assert.That(baseModifier.Triggers, Is.Empty);
        Assert.That(atom.Type, Is.EqualTo(EffectContentType.MultiplyActionPower));
        Assert.That(atom.Parameters, Is.EqualTo(new[] { "Self", "魔法", "1.2" }));
        var equipment = EquipmentTextConverter.Parse(Block("白いスカーフ").Replace("自身の敏捷Bが+0.05される。", equipped));
        Assert.That(JsonUtility.ToJson(equipment.Equipment.EffectGroups.Single().Effects.Single()), Is.EqualTo(JsonUtility.ToJson(atom)));
        var spike = data.Skill.Overrides.Single(x => x.Type == EffectType.SecondSpike).Contents.Single();
        Assert.That(spike.Type, Is.EqualTo(EffectContentType.SetModifier));
        Assert.That(spike.Parameters, Does.Contain("魔法"));
        Assert.That(spike.Parameters.Last(), Is.EqualTo("1.5"));
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(SkillTextConverter.Build(data))), Is.EqualTo(JsonUtility.ToJson(data)));
    }

    [TestCase("自身の〈法術, 巫女スキル〉による目標値は-7される。", "法術, 巫女スキル", "-7")]
    [TestCase("〈隠密〉の発動ロールの目標値が+5される。", "隠密", "+5")]
    public void OwnCategoryTargetAdjustmentUsesTheSameRuleInItemsAndSkills(string sentence, string category, string delta)
    {
        var equipment = EquipmentTextConverter.Parse(SharedItemHeader + sentence);
        var skill = SkillTextConverter.Parse(SharedSkillHeader + "【パッシブ効果】" + sentence);
        var adjustment = skill.Skill.Effects.Concat(skill.Skill.Overrides).Single().Contents.Single();
        Assert.That(adjustment.Type, Is.EqualTo(EffectContentType.RuleModifier));
        Assert.That(adjustment.Parameters, Is.EqualTo(new[] { "CategoryTargetDelta", category, delta }));
        Assert.That(JsonUtility.ToJson(equipment.Equipment.EffectGroups.Single().Effects.Single()), Is.EqualTo(JsonUtility.ToJson(adjustment)));
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(SkillTextConverter.Build(skill))), Is.EqualTo(JsonUtility.ToJson(skill)));
    }

    [Test]
    public void RollTableEmitsIdenticalRowsInAnItemAndSkill()
    {
        const string table = "自身が〈虹の庭〉でSPを全回復した時、1d6のダイスロールを行い以下の効果を適用しても良い。\n" +
            "- 1～4：何もなし。\n- 5～6：自身に付与されている永続効果を一つ選んで解除する。";
        const string skill = SharedSkillHeader + "【パッシブ効果】";
        var skillData = SkillTextConverter.Parse(skill + table);
        var itemData = EquipmentTextConverter.Parse(SharedItemHeader + table);
        var rows = itemData.Equipment.EffectGroups.Single().Effects;
        Assert.That(rows.Count, Is.EqualTo(3));
        Assert.That(rows.Last().Parameters, Is.EqualTo(new[] { "ClearPermanent", "5", "6" }));
        Assert.That(skillData.Skill.Effects.SelectMany(x => x.Contents).Concat(skillData.Skill.Overrides.SelectMany(x => x.Contents))
            .Select(x => JsonUtility.ToJson(x)), Is.EqualTo(rows.Select(x => JsonUtility.ToJson(x))));
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(SkillTextConverter.Build(skillData))), Is.EqualTo(JsonUtility.ToJson(skillData)));
        Assert.That(JsonUtility.ToJson(EquipmentTextConverter.Parse(EquipmentTextConverter.Build(itemData))), Is.EqualTo(JsonUtility.ToJson(itemData)));
        string overlapping = table.Replace("1～4", "1～5");
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Parse(skill + overlapping));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(SharedItemHeader + overlapping));
    }

    [TestCase("《宝玉》×2を獲得。", "ランクは☆×4となる。", EffectContentType.GrantItem, "4")]
    [TestCase("対象へ選択したスキルをACT消費無しで宣言してもよい。", "そのスキルで与える威力は×1.4倍される。", EffectContentType.DeclareSelectedSkill, "1.4")]
    public void SharedEffectReadsItsContinuation(string first, string continuation, EffectContentType type, string value)
    {
        string paragraph = first + "\n" + continuation;
        foreach (var source in new[] { SharedItemHeader + paragraph,
            Block("白いスカーフ").Replace("自身の敏捷Bが+0.05される。", paragraph) })
        {
            var item = EquipmentTextConverter.Parse(source);
            var effect = item.Equipment.EffectGroups.Single().Effects.Single();
            Assert.That(effect.Type, Is.EqualTo(type));
            Assert.That(effect.Parameters.Last(), Is.EqualTo(value));
            Assert.That(JsonUtility.ToJson(EquipmentTextConverter.Parse(EquipmentTextConverter.Build(item))), Is.EqualTo(JsonUtility.ToJson(item)));
        }
        var skill = SkillTextConverter.Parse(SharedSkillHeader + "【アクティブ効果】自身は《贈物》状態になる。\n贈物効果〈顕現〉：\n" + paragraph);
        var stateEffect = StateEffects(skill.States.Single()).Single();
        Assert.That(stateEffect.Type, Is.EqualTo(type));
        Assert.That(stateEffect.Parameters.Last(), Is.EqualTo(value));
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(SkillTextConverter.Build(skill))), Is.EqualTo(JsonUtility.ToJson(skill)));
    }

    [TestCase("生命B", "")]
    [TestCase("器用B", "敏捷B")]
    public void AbilityCheckMultiplierSharesOperandsAndSpikeSelector(string first, string second)
    {
        string stats = "[" + first + "]" + (second == "" ? "" : "および[" + second + "]");
        string sentence = stats + "による行為判定を行う際、達成値が×1.1倍される。";
        var skill = SkillTextConverter.Parse(SharedSkillHeader + "【パッシブ効果】" + sentence +
            "\n【セカンドスパイク】このパッシブ効果による達成値上昇は×1.2倍に変化する。");
        var modifier = skill.Skill.Overrides.Single(e => e.Type == EffectType.Passive);
        var atom = modifier.Contents.Single();
        Assert.That(modifier.Triggers, Is.Empty);
        Assert.That(atom.Type, Is.EqualTo(EffectContentType.StatRollFactorForAction));
        Assert.That(atom.Parameters, Is.EqualTo(second == "" ? new[] { first, "1.1" } : new[] { first, second, "1.1" }));
        var spike = skill.Skill.Overrides.Single(e => e.Type == EffectType.SecondSpike).Contents.Single();
        Assert.That(spike.Parameters, Is.EqualTo(new[] { "Rule", "ActionStats" }.Concat(atom.Parameters.Take(atom.Parameters.Count - 1)).Concat(new[] { "1.2" })));
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(SkillTextConverter.Build(skill))), Is.EqualTo(JsonUtility.ToJson(skill)));

        var item = EquipmentTextConverter.Parse(SharedItemHeader + sentence);
        var state = SkillTextConverter.Parse(SharedSkillHeader + "【アクティブ効果】自身は《試練》状態になる。\n試練効果〈顕現〉：" + sentence);
        Assert.That(JsonUtility.ToJson(item.Equipment.EffectGroups.Single().Effects.Single()), Is.EqualTo(JsonUtility.ToJson(atom)));
        Assert.That(JsonUtility.ToJson(StateEffects(state.States.Single()).Single()), Is.EqualTo(JsonUtility.ToJson(atom)));
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(SkillTextConverter.Build(state))), Is.EqualTo(JsonUtility.ToJson(state)));
    }

    [Test]
    public void ConditionalAbilityCheckMultiplierPreservesItsOuterTrigger()
    {
        var item = EquipmentTextConverter.Parse(SharedItemHeader + "この武器の選択を条件にスキルを発動した後、[生命B]による行為判定を行う際、達成値が×1.1倍される。");
        var atom = item.Equipment.EffectGroups.Single().Effects.Single();
        Assert.That(atom.Type, Is.EqualTo(EffectContentType.ConditionalRule));
        Assert.That(atom.Parameters, Is.EqualTo(new[] { "AfterUsingWeapon", "StateRollMultiplier", "生命B", "1.1" }));
        Assert.That(JsonUtility.ToJson(EquipmentTextConverter.Parse(EquipmentTextConverter.Build(item))), Is.EqualTo(JsonUtility.ToJson(item)));
    }

    [Test]
    public void ItemAndSkillStatesUseTheSameEffect()
    {
        string skill = SharedSkillHeader + "【アクティブ効果】自身は2ターンの間《音色》状態となる。\n音色効果〈マインド〉：" + AreaRecoveryRule;
        string item = Block("白いスカーフ").Replace("自身の敏捷Bが+0.05される。",
            "このアイテムによるスキルの選択対象を1ターンの間《音色》にしても良い。\n音色〈マインド〉：" + AreaRecoveryRule);
        var skillData = SkillTextConverter.Parse(skill);
        var itemData = EquipmentTextConverter.Parse(item);
        var state = skillData.States.Single();
        var effect = StateEffects(state).Single();
        Assert.That(JsonUtility.ToJson(StateEffects(itemData.Equipment.States.Single()).Single()), Is.EqualTo(JsonUtility.ToJson(effect)));

        var export = new DataBlockJsonExport();
        export.Main(skillData);
        export.Main(itemData);
        Assert.That(export.States.ToArray().Count, Is.EqualTo(1));
        Assert.That(export.States.Conflicts, Is.Empty);
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(SkillTextConverter.Build(skillData))), Is.EqualTo(JsonUtility.ToJson(skillData)));
        Assert.That(JsonUtility.ToJson(EquipmentTextConverter.Parse(EquipmentTextConverter.Build(itemData))), Is.EqualTo(JsonUtility.ToJson(itemData)));
    }

    [TestCase("")]
    [TestCase("・")]
    public void ActionAfterAnInlineStateDefinitionKeepsItsSkillScope(string bullet)
    {
        string source = SharedSkillHeader + "【アクティブ効果】自身は2ターンの間《音色》状態となる。\n" +
            "音色効果〈マインド〉：自身はアクティブスキルによる効果の選択対象にならなくなる。\n" +
            bullet + "次のターンの開始時、自身は《死色二重》〈エンチャント〉状態となりACTを6得る。";
        var data = SkillTextConverter.Parse(source);
        Assert.That(data.Skill.Effects.SelectMany(x => x.Contents).Select(x => x.Type),
            Does.Contain(EffectContentType.NextTurnCategorizedStateAndRecovery));
        Assert.That(StateEffects(data.States.Single()).Single().Type, Is.EqualTo(EffectContentType.CannotBeSelectedByActive));
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(SkillTextConverter.Build(data))), Is.EqualTo(JsonUtility.ToJson(data)));
    }

    [TestCase("自身のMP最大値は×(1+[スタック]*0.1)される。")]
    [TestCase("自身がSAN値ダメージを受ける時、更に100のSANダメージを受ける。")]
    [TestCase("自身はエリアに〈水中〉が含まれている時、[敏捷B]が×1.33倍され、[敏捷B]による行為判定の達成値は+5される。")]
    [TestCase("さらにクリティカルになるダイス目が[スタック]低くなる。")]
    [TestCase("さらに〈魔法〉の対象が〈準魔族〉及び〈魔族〉の場合、追加で50のダメージを与える")]
    [TestCase("さらに未習得かつ装備していない全ての〈ダークナイトスキル〉を宣言する事が出来る。")]
    public void SkillInputNormalizationPreservesCompleteSharedRules(string body)
    {
        var item = EquipmentTextConverter.Parse(SharedItemHeader + body);
        var skill = SkillTextConverter.Parse(SharedSkillHeader + "【パッシブ効果】" + body);
        Assert.That(JsonUtility.ToJson(SingleSkillEffect(skill.Skill)), Is.EqualTo(JsonUtility.ToJson(item.Equipment.EffectGroups.Single().Effects.Single())));
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(SkillTextConverter.Build(skill))), Is.EqualTo(JsonUtility.ToJson(skill)));
    }

    [Test]
    public void ItemEffectListCombinesStatAndAction()
    {
        var data = EquipmentTextConverter.Parse(SharedItemHeader + "防御点+7, 対象のHPを11回復する。");
        Assert.That(data.Equipment.EffectGroups.Single().Effects.Select(c => c.Type),
            Is.EqualTo(new[] { EffectContentType.ModifyStat, EffectContentType.RestoreResource }));
        Assert.That(JsonUtility.ToJson(EquipmentTextConverter.Parse(EquipmentTextConverter.Build(data))), Is.EqualTo(JsonUtility.ToJson(data)));
    }

    [Test]
    public void RollTableInsideAnItemStateCanReferenceAnotherState()
    {
        const string body = "自身は2ターンの間《音色》状態となる。\n" +
            "音色効果〈マインド〉：自身が〈虹の庭〉でSPを全回復した時、1d6のダイスロールを行い以下の効果を適用しても良い。\n" +
            "- 1～6：自身は永続的に《余韻》状態になる。\n余韻効果〈マインド〉：防御点+7";
        var data = EquipmentTextConverter.Parse(SharedItemHeader + body);
        Assert.That(data.Equipment.States.Select(s => s.Name), Is.EqualTo(new[] { "音色", "余韻" }));
        Assert.That(JsonUtility.ToJson(EquipmentTextConverter.Parse(EquipmentTextConverter.Build(data))), Is.EqualTo(JsonUtility.ToJson(data)));
    }

    [Test]
    public void SharedEffectRejectsUnknownTextAndInvalidSerializedArguments()
    {
        const string unknown = "この文章は未知の効果である。";
        string skill = SharedSkillHeader + "【パッシブ効果】";
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Parse(skill + unknown));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(SharedItemHeader + unknown));
        var skillData = SkillTextConverter.Parse(skill + AreaRecoveryRule);
        var itemData = EquipmentTextConverter.Parse(SharedItemHeader + AreaRecoveryRule);
        var atoms = new[] { SingleSkillEffect(skillData.Skill), itemData.Equipment.EffectGroups.Single().Effects.Single() };
        foreach (var atom in atoms) atom.Parameters.RemoveAt(atom.Parameters.Count - 1);
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Build(skillData));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(itemData));
    }

    private static IEnumerable<EffectContent> StateEffects(StateDefinition state)
    {
        return state.Effects.SelectMany(x => x.Contents).Concat(state.Overrides.SelectMany(x => x.Contents));
    }

    private static EffectContent SingleSkillEffect(SkillBody skill)
    {
        return skill.Effects.SelectMany(x => x.Contents).Concat(skill.Overrides.SelectMany(x => x.Contents)).Single();
    }

    [Test]
    public void TurnStartRewardUsesTheResultOfItsOwnCheck()
    {
        string source = Block("白いスカーフ").Replace("自身の敏捷Bが+0.05される。",
            "毎ターン開始時：1d100*[魔力B] 達成値70の行為判定を行う。\n成功時ACTを1得る。");
        var data = EquipmentTextConverter.Parse(source);
        var effect = data.Equipment.EffectGroups.Single().Effects.Single();
        Assert.That(effect.Parameters, Is.EqualTo(new[] { "SuccessfulTurnStartCheck", "GainResource",
            "1d100*[魔力B]", "70", "ACT", "1" }));
        Assert.That(JsonUtility.ToJson(EquipmentTextConverter.Parse(EquipmentTextConverter.Build(data))),
            Is.EqualTo(JsonUtility.ToJson(data)));
    }

    [Test]
    public void EquipmentState_ExportsItsModifiersAndValidatesTheReference()
    {
        string item = Block("白いスカーフ").Replace("自身の敏捷Bが+0.05される。",
            "このアイテムによるスキルの選択対象を1ターンの間《音色》にしても良い。\n" +
            "音色〈マインド〉：\n・自身の[魔力B]による達成値は×0.83倍される。\n・自身の与SANダメージは×0.75倍される。");
        var data = EquipmentTextConverter.Parse(item);
        Assert.That(data.Equipment.EffectGroups.Count, Is.EqualTo(1));
        Assert.That(StateEffects(data.Equipment.States.Single()).Count(), Is.EqualTo(2));
        var preview = new DataBlockJsonPreview(data);
        Assert.That(preview.Main, Does.Not.Contain("0.75"));
        Assert.That(preview.States, Does.Contain("0.75"));
        string rebuilt = EquipmentTextConverter.Build(data);
        Assert.That(EquipmentTextConverter.Normalize(rebuilt), Is.EqualTo(rebuilt));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(item.Replace("音色〈マインド〉", "別名〈マインド〉")));
    }

    [Test]
    public void EquipmentItems_JsonRoundTrip()
    {
        string normalized = EquipmentTextConverter.Normalize(ReadFixture("EquipmentItems.txt"));
        foreach (Match block in Blocks(normalized).Cast<Match>())
        {
            EquipmentTextData original = EquipmentTextConverter.Parse(block.Value);
            string expected = EquipmentTextConverter.Build(original);
            string json = JsonUtility.ToJson(original, true);
            EquipmentTextData restored = JsonUtility.FromJson<EquipmentTextData>(json);

            Assert.That(json, Is.Not.Empty, original.Equipment.Name);
            Assert.That(EquipmentTextConverter.Build(restored), Is.EqualTo(expected), original.Equipment.Name);
        }
    }

    [Test]
    public void Transport_AcceptsCssAndSwiftAndWritesPlainFences()
    {
        string source = Block("白いスカーフ");
        string expected = EquipmentTextConverter.Normalize(source);

        Assert.That(EquipmentTextConverter.Normalize(WithFenceLanguage(source, "css")), Is.EqualTo(expected));
        Assert.That(EquipmentTextConverter.Normalize(WithFenceLanguage(source, "swift")), Is.EqualTo(expected));
        StringAssert.StartsWith("```\n", expected);
    }

    [Test]
    public void EquipmentSkillAndEnemyConverters_RejectOppositeRoots()
    {
        string equipment = Block("白いスカーフ");
        string enemy = ReadFixture("ChirupippiEnemy.txt");

        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(MinimalSkill));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(enemy));
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Parse(equipment));
        Assert.Throws<InvalidOperationException>(() => EnemyTextConverter.Parse(equipment));
    }

    [Test]
    public void Parse_RejectsMultipleEquipmentItems()
    {
        string source = Block("白いスカーフ") + "\n\n" + Block("ドラゴンスケールシールド");
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(source));
    }

    [Test]
    public void InvalidTextAndSerializedData_AreRejected()
    {
        string scarf = Block("白いスカーフ");
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(
            scarf.Replace("〈装備アイテム, アクセサリー, 縫製〉", "〈一般スキル〉")));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(
            scarf.Replace("【レアリティ】Common\n", "")));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(
            scarf.Replace("【レアリティ】Common", "【レアリティ】Common\n【レアリティ】Rare")));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(
            scarf.Replace("【グレード】☆☆", "【グレード】☆A")));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(
            scarf.Replace("【価値】6ジルダ", "【価値】-1ジルダ")));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(
            scarf.Replace("【耐久最大値】5", "【耐久最大値】0")));

        EquipmentTextData duplicateCategory = EquipmentTextConverter.Parse(scarf);
        duplicateCategory.Equipment.Categories.Add("アクセサリー");
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(duplicateCategory));

        EquipmentTextData duplicateGroup = EquipmentTextConverter.Parse(Block("ドラゴンスケールシールド"));
        duplicateGroup.Equipment.EffectGroups.Add(new EquipmentEffectGroup
        {
            Name = "竜鱗",
            Effects = new List<EffectContent>
            {
                new EffectContent
                {
                    Type = EffectContentType.MultiplyIncomingDamage,
                    Parameters = new List<string> { "Self", "魔法", "0.5" }
                }
            }
        });
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(duplicateGroup));

        EquipmentTextData unknownState = EquipmentTextConverter.Parse(Block("永夜の魔導服"));
        Effects(unknownState.Equipment).Single(x => x.Type == EffectContentType.GainStateStacksOnAction).Parameters[2] = "未定義状態";
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(unknownState));

        EquipmentTextData duplicateSkill = EquipmentTextConverter.Parse(Block("ウォッチャーのモノクル"));
        duplicateSkill.Equipment.Skills.Add(duplicateSkill.Equipment.Skills[0]);
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(duplicateSkill));

        EquipmentTextData canonicalLevel = EquipmentTextConverter.Parse(Block("ウォッチャーのモノクル"));
        canonicalLevel.Equipment.EquipRequirements.Single().Parameters[0] = "06";
        StringAssert.Contains("【装備条件】6レベル以上", EquipmentTextConverter.Build(canonicalLevel));

        EquipmentTextData canonicalStack = EquipmentTextConverter.Parse(Block("永夜の魔導服"));
        Effects(canonicalStack.Equipment).Single(x => x.Type == EffectContentType.GainStateStacksOnAction)
            .Parameters[3] = "01";
        StringAssert.Contains("状態を1スタック得る。", EquipmentTextConverter.Build(canonicalStack));

        EquipmentTextData ambiguousStat = EquipmentTextConverter.Parse(scarf);
        Effects(ambiguousStat.Equipment).Single().Parameters[1] = "敏捷+補正";
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(ambiguousStat));

        EquipmentTextData ambiguousValue = EquipmentTextConverter.Parse(scarf);
        Effects(ambiguousValue.Equipment).Single().Parameters[3] = "+func(1),2";
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(ambiguousValue));

        EquipmentTextData fencedFlavor = EquipmentTextConverter.Parse(scarf);
        fencedFlavor.Equipment.Flavor = "前\n```\n後";
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(fencedFlavor));

        EquipmentTextData escapedFenceFlavor = EquipmentTextConverter.Parse(scarf);
        escapedFenceFlavor.Equipment.Flavor = "前\n\\`\\`\\`\n後";
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(escapedFenceFlavor));

        EquipmentTextData ambiguousRoll = EquipmentTextConverter.Parse(Block("ウォッチャーのモノクル"));
        ambiguousRoll.Equipment.Skills.Single().Skill.Roll =
            new DiceRollDefinition { Count = 1, Formula = "1d100\t目標値99", Target = "30" };
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(ambiguousRoll));

        EquipmentTextData collidingChildHeader = EquipmentTextConverter.Parse(Block("ウォッチャーのモノクル"));
        collidingChildHeader.Equipment.Skills.Single().Skill.Choices.Add(
            new SkillBody { Name = "スキル：《別名》" });
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(collidingChildHeader));

        string hat = Block("見習い魔女の紺色とんがり帽");
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(
            hat.Replace("- ☆2：耐久最大値-2, 防御点-1",
                "- ☆1：耐久最大値-2, 防御点-1")));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(
            hat.Replace("耐久最大値-4, 防御点-2", "耐久最大値-5, 防御点-2")));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(
            hat.Replace("【耐久最大値】5, MP最大値+5",
                "【耐久最大値】5,, MP最大値+5")));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(
            hat.Replace("耐久最大値-4, 防御点-2",
                "耐久最大値-4,, 防御点-2")));
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Parse(
            hat.Replace("- ☆5：耐久最大値+4, 防御点+2",
                "- ☆2147483647：耐久最大値+4, 防御点+2")));

        EquipmentTextData hatData = EquipmentTextConverter.Parse(hat);
        Assert.Throws<InvalidOperationException>(() =>
            EquipmentGradeResolver.Resolve(hatData, "《見習い魔女の紺色とんがり帽》☆3"));
        Assert.Throws<InvalidOperationException>(() =>
            EquipmentGradeResolver.Resolve(hatData, "《見習い魔女の紺色とんがり帽》☆6"));
        Assert.Throws<InvalidOperationException>(() =>
            EquipmentGradeResolver.Resolve(hatData, "《見習い魔女の紺色とんがり帽》☆05"));
        Assert.Throws<InvalidOperationException>(() =>
            EquipmentGradeResolver.Resolve(hatData, "《別名》☆5"));
        Assert.Throws<InvalidOperationException>(() =>
            EquipmentGradeResolver.Resolve(hatData, int.MaxValue));

        EquipmentTextData nullAdjustments = EquipmentTextConverter.Parse(hat);
        nullAdjustments.Equipment.GradeAdjustments = null;
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(nullAdjustments));

        EquipmentTextData excessiveGrade = EquipmentTextConverter.Parse(hat);
        excessiveGrade.Equipment.Grade = 101;
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(excessiveGrade));
    }

    [Test]
    public void EquipmentGrammar_DoesNotDependOnItemOrEffectNames()
    {
        string renamed = Block("ドラゴンスケールシールド")
            .Replace("《ドラゴンスケールシールド》", "《試験装備》")
            .Replace("《竜鱗》：", "《耐魔》：");

        EquipmentTextData parsed = EquipmentTextConverter.Parse(renamed);
        Assert.That(parsed.Equipment.Name, Is.EqualTo("試験装備"));
        Assert.That(parsed.Equipment.EffectGroups.Single(x => x.Name == "耐魔").Effects.Single().Type,
            Is.EqualTo(EffectContentType.MultiplyIncomingDamage));
        Assert.That(EquipmentTextConverter.Build(EquipmentTextConverter.Parse(EquipmentTextConverter.Build(parsed))),
            Is.EqualTo(EquipmentTextConverter.Build(parsed)));
    }

    [Test]
    public void GradeEffectsUseDestinationWithoutApplyingDisplayedGradeRow()
    {
        const string source = "《格闘具》\n〈装備アイテム〉\n【レアリティ】Rare\n【グレード】☆☆☆☆☆\n【サイズ】大\n" +
            "【耐久最大値】10\n【グレード補正】\n- ☆1：耐久最大値-2, 武器威力-20\n- ☆2：耐久最大値-1, 武器威力-10\n" +
            "- ☆4：耐久最大値+1, 武器威力+10\n- ☆5：耐久最大値+2, 武器威力+20";
        var item = EquipmentTextConverter.Parse(source);
        var same = EquipmentGradeResolver.Resolve(item, 5);
        Assert.That(same.Equipment.MaxDurability, Is.EqualTo(10));
        Assert.That(same.Equipment.WeaponPowerBonus, Is.Zero);
        Assert.That(same.Equipment.GradeAdjustments.Count, Is.EqualTo(4));

        var neutral = EquipmentGradeResolver.Resolve(item, 3);
        Assert.That(neutral.Equipment.MaxDurability, Is.EqualTo(10));
        Assert.That(neutral.Equipment.WeaponPowerBonus, Is.Zero);
        var changed = EquipmentGradeResolver.Resolve(item, 4);
        Assert.That(changed.Equipment.MaxDurability, Is.EqualTo(11));
        Assert.That(changed.Equipment.WeaponPowerFormula, Is.Empty);
        Assert.That(changed.Equipment.WeaponPowerBonus, Is.EqualTo(10));
        Assert.That(EquipmentTextConverter.Build(EquipmentGradeResolver.Resolve(changed, 4)),
            Is.EqualTo(EquipmentTextConverter.Build(changed)));
    }

    private static void AssertModifyStats(EquipmentDefinition equipment, params string[][] expected)
    {
        Assert.That(StatParameters(equipment), Is.EquivalentTo(expected));
    }

    private static List<List<string>> StatParameters(EquipmentDefinition equipment)
    {
        return Effects(equipment).Where(x => x.Type == EffectContentType.ModifyStat)
            .Select(x => x.Parameters).ToList();
    }

    private static IEnumerable<EffectContent> Effects(EquipmentDefinition equipment)
    {
        return equipment.EffectGroups.SelectMany(x => x.Effects);
    }

    private static decimal StatTotal(EquipmentDefinition equipment, string stat)
    {
        return StatParameters(equipment)
            .Where(x => x[1] == stat)
            .Sum(x => decimal.Parse(x[3], NumberStyles.Float, CultureInfo.InvariantCulture));
    }

    private static string SignedIntegerText(int value)
    {
        return (value > 0 ? "+" : "") + value.ToString(CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, EquipmentTextData> Items()
    {
        string normalized = EquipmentTextConverter.Normalize(ReadFixture("EquipmentItems.txt"));
        return Blocks(normalized).Cast<Match>().Select(x => EquipmentTextConverter.Parse(x.Value))
            .ToDictionary(x => x.Equipment.Name, StringComparer.Ordinal);
    }

    private static string Block(string name)
    {
        return Blocks(ReadFixture("EquipmentItems.txt")).Cast<Match>()
            .Single(x => x.Value.Contains("《" + name + "》")).Value;
    }

    private static MatchCollection Blocks(string source)
    {
        return Regex.Matches(source.Replace("\r\n", "\n"), @"(?s)```.*?```");
    }

    private static string WithFenceLanguage(string block, string language)
    {
        if (!block.StartsWith("```\n", StringComparison.Ordinal))
            throw new InvalidOperationException("テストfixtureの開始フェンスが不正です。");
        return "```" + language + block.Substring(3);
    }

    private static string ReadFixture(string fileName)
    {
        foreach (string seed in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(seed);
            while (directory != null)
            {
                string projectPath = Path.Combine(directory.FullName, "Assets", "DataBlock", "Editor", "Tests", "Fixtures", fileName);
                if (File.Exists(projectPath)) return File.ReadAllText(projectPath);
                string packagePath = Path.Combine(directory.FullName, "Editor", "Tests", "Fixtures", fileName);
                if (File.Exists(packagePath)) return File.ReadAllText(packagePath);
                directory = directory.Parent;
            }
        }
        throw new FileNotFoundException("テストfixtureが見つかりません。", fileName);
    }
}
#endif
