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
        Assert.That(resolved.Equipment.GradeAdjustments, Is.Empty);
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
        Assert.That(book.Effects.Single().Type, Is.EqualTo(EquipmentEffectType.ModifySelectedWeaponAction));
        Assert.That(book.Effects.Single().Parameters, Is.EqualTo(new[] { "攻撃", "1.1", "0.91" }));
        AssertModifyStats(maelstrom, new[] { "Self", "水属性B", "Add", "+0.2" });

        AssertModifyStats(items["白いスカーフ"].Equipment,
            new[] { "Self", "敏捷B", "Add", "+0.05" });

        EquipmentDefinition shield = items["ドラゴンスケールシールド"].Equipment;
        AssertModifyStats(shield, new[] { "Self", "防御点", "Add", "+13" });
        EquipmentEffectDefinition scales = shield.EffectGroups.Single(x => x.Name == "竜鱗").Effects.Single();
        Assert.That(scales.Type, Is.EqualTo(EquipmentEffectType.MultiplyIncomingDamage));
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
        EquipmentEffectDefinition sweets = Effects(bonnet).Single(x => x.Type == EquipmentEffectType.AllowItemUse);
        Assert.That(sweets.Parameters, Is.EqualTo(new[] { "Self", "Encounter", "お菓子" }));

        EquipmentDefinition eternalNight = items["永夜の魔導服"].Equipment;
        Assert.That(StatParameters(eternalNight), Does.Contain(new[] { "Self", "防御点", "Add", "+40" }));
        Assert.That(StatParameters(eternalNight), Does.Contain(new[] { "Self", "MP最大値", "Add", "+20" }));
        EquipmentEffectDefinition magicPower = Effects(eternalNight).Single(x => x.Type == EquipmentEffectType.MultiplyActionPower);
        Assert.That(magicPower.Parameters, Is.EqualTo(new[] { "Self", "魔法", "1.2" }));
        EquipmentEffectDefinition stack = Effects(eternalNight).Single(x => x.Type == EquipmentEffectType.GainStateStacksOnAction);
        Assert.That(stack.Parameters, Is.EqualTo(new[] { "Self", "魔法", "無限の霜星", "1" }));
        EquipmentStateDefinition frost = eternalNight.States.Single();
        Assert.That(frost.Name, Is.EqualTo("無限の霜星"));
        Assert.That(frost.Categories, Is.EqualTo(new[] { "スタック" }));
        Assert.That(frost.Modifiers.Single().Type, Is.EqualTo(EquipmentEffectType.ModifyStat));
        Assert.That(frost.Modifiers.Single().Parameters,
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
            scarf.Replace("〈装備アイテム, アクセサリー, 縫製〉", "〈アクセサリー, 縫製〉")));
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
            Effects = new List<EquipmentEffectDefinition>
            {
                new EquipmentEffectDefinition
                {
                    Type = EquipmentEffectType.MultiplyIncomingDamage,
                    Parameters = new List<string> { "Self", "魔法", "0.5" }
                }
            }
        });
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(duplicateGroup));

        EquipmentTextData unknownState = EquipmentTextConverter.Parse(Block("永夜の魔導服"));
        Effects(unknownState.Equipment).Single(x => x.Type == EquipmentEffectType.GainStateStacksOnAction).Parameters[2] = "未定義状態";
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(unknownState));

        EquipmentTextData duplicateSkill = EquipmentTextConverter.Parse(Block("ウォッチャーのモノクル"));
        duplicateSkill.Equipment.Skills.Add(duplicateSkill.Equipment.Skills[0]);
        Assert.Throws<InvalidOperationException>(() => EquipmentTextConverter.Build(duplicateSkill));

        EquipmentTextData canonicalLevel = EquipmentTextConverter.Parse(Block("ウォッチャーのモノクル"));
        canonicalLevel.Equipment.EquipRequirements.Single().Parameters[0] = "06";
        StringAssert.Contains("【装備条件】6レベル以上", EquipmentTextConverter.Build(canonicalLevel));

        EquipmentTextData canonicalStack = EquipmentTextConverter.Parse(Block("永夜の魔導服"));
        Effects(canonicalStack.Equipment).Single(x => x.Type == EquipmentEffectType.GainStateStacksOnAction)
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
            hat.Replace("- ☆2：耐久最大値-2, 防御点-1",
                "- ☆3：耐久最大値-2, 防御点-1")));
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
            Is.EqualTo(EquipmentEffectType.MultiplyIncomingDamage));
        Assert.That(EquipmentTextConverter.Build(EquipmentTextConverter.Parse(EquipmentTextConverter.Build(parsed))),
            Is.EqualTo(EquipmentTextConverter.Build(parsed)));
    }

    private static void AssertModifyStats(EquipmentDefinition equipment, params string[][] expected)
    {
        Assert.That(StatParameters(equipment), Is.EquivalentTo(expected));
    }

    private static List<List<string>> StatParameters(EquipmentDefinition equipment)
    {
        return Effects(equipment).Where(x => x.Type == EquipmentEffectType.ModifyStat)
            .Select(x => x.Parameters).ToList();
    }

    private static IEnumerable<EquipmentEffectDefinition> Effects(EquipmentDefinition equipment)
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
