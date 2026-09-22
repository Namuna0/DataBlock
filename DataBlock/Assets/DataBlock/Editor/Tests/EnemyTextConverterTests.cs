#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

internal sealed class EnemyTextConverterTests
{
    private const string MinimalSkill =
        "```\n" +
        "《試験》\n" +
        "〈攻撃〉\n" +
        "【宣言条件】キャラクターを1体選択して宣言可能。\n" +
        "【アクティブ効果】対象をスキル値10で武器攻撃する。\n" +
        "```";

    [Test]
    public void Chirupippi_NormalizeBuildAndJsonRoundTrip()
    {
        string source = ReadFixture("ChirupippiEnemy.txt");
        string normalized = EnemyTextConverter.Normalize(source);
        EnemyTextData parsed = EnemyTextConverter.Parse(source);

        Assert.That(EnemyTextConverter.Build(parsed), Is.EqualTo(normalized));
        Assert.That(EnemyTextConverter.Normalize(normalized), Is.EqualTo(normalized));

        string json = JsonUtility.ToJson(parsed, true);
        EnemyTextData restored = JsonUtility.FromJson<EnemyTextData>(json);
        Assert.That(EnemyTextConverter.Build(restored), Is.EqualTo(normalized));
    }

    [Test]
    public void Chirupippi_MapsEnemyActionsSkillsAndDropsToSemanticNodes()
    {
        EnemyDefinition enemy = EnemyTextConverter.Parse(ReadFixture("ChirupippiEnemy.txt")).Enemy;

        Assert.That(enemy.Name, Is.EqualTo("チルピッピ"));
        Assert.That(enemy.Categories, Is.EqualTo(new[] { "一般エネミー", "魔法生物", "小型" }));
        Assert.That(enemy.DangerLevel, Is.EqualTo("☆"));
        Assert.That(enemy.Stats.Select(x => x.Name + "=" + x.Formula), Is.EqualTo(new[]
            { "敏捷B=0.5", "HP最大値=100", "SAN最大値=100", "防御点=0" }));

        EnemyActionRule arrival = enemy.Actions[0];
        Assert.That(arrival.Timing, Is.EqualTo(EnemyActionTiming.Turn));
        Assert.That(arrival.UsesPerTurn, Is.EqualTo(1));
        Assert.That(arrival.AllConditions, Is.Empty);
        Assert.That(arrival.AnyConditions.Select(x => x.Type), Is.EqualTo(new[]
            { EnemyActionConditionType.OwnState, EnemyActionConditionType.NoSelectableEnemyInMeleeGroup }));
        Assert.That(arrival.AnyConditions[0].Value, Is.EqualTo("接近"));
        Assert.That(arrival.AnyConditions[0].Negated, Is.True);
        Assert.That(arrival.TargetSelector, Is.EqualTo(EnemyActionTargetSelector.RandomSelectableEnemy));
        Assert.That(arrival.SkillName, Is.EqualTo("飛来"));

        EnemyActionRule peck = enemy.Actions[1];
        Assert.That(peck.AllConditions.Single().Type, Is.EqualTo(EnemyActionConditionType.OwnState));
        Assert.That(peck.AllConditions.Single().Negated, Is.False);
        Assert.That(peck.TargetSelector, Is.EqualTo(EnemyActionTargetSelector.RandomSelectableEnemyInMeleeGroup));

        EnemyActionRule evade = enemy.Actions[2];
        Assert.That(evade.Timing, Is.EqualTo(EnemyActionTiming.Reaction));
        Assert.That(evade.AllConditions.Single().Type, Is.EqualTo(EnemyActionConditionType.IncomingAction));
        Assert.That(evade.AllConditions.Single().Value, Is.EqualTo("攻撃"));

        Assert.That(enemy.Skills.Select(x => x.Skill.Name), Is.EqualTo(new[] { "飛来", "つつく", "鳥回避" }));
        SkillBody birdEvade = enemy.Skills.Single(x => x.Skill.Name == "鳥回避").Skill;
        Assert.That(birdEvade.DeclarationConditions.And.Single().Parameters,
            Is.EqualTo(new[] { "Receiver", "攻撃", "Self" }));
        Assert.That(birdEvade.Effects.SelectMany(x => x.Contents)
            .Single(x => x.Type == EffectContentType.InvalidateIncomingAction).Parameters,
            Is.EqualTo(new[] { "Self", "攻撃" }));

        Assert.That(enemy.DropRoll, Is.EqualTo("1d100"));
        Assert.That(enemy.Drops.Select(x => x.Minimum + "-" + x.Maximum + ":" + x.ItemName + "x" + x.Amount),
            Is.EqualTo(new[] { "31-70:鳥肉x1", "71-100:小さな羽根x1" }));
        StringAssert.StartsWith("白くて可愛らしい小鳥", enemy.Flavor);
    }

    [Test]
    public void Build_RejectsUnknownAndUnusedSkillReferences()
    {
        string source = ReadFixture("ChirupippiEnemy.txt");
        EnemyTextData unknown = EnemyTextConverter.Parse(source);
        unknown.Enemy.Actions[0].SkillName = "未定義スキル";
        InvalidOperationException unknownError = Assert.Throws<InvalidOperationException>(() => EnemyTextConverter.Build(unknown));
        StringAssert.Contains("行動スキル定義がありません", unknownError.Message);

        EnemyTextData unused = EnemyTextConverter.Parse(source);
        unused.Enemy.Actions.RemoveAt(2);
        InvalidOperationException unusedError = Assert.Throws<InvalidOperationException>(() => EnemyTextConverter.Build(unused));
        StringAssert.Contains("行動から参照されていない", unusedError.Message);
    }

    [Test]
    public void Validation_RejectsDuplicateStatsAndSkills()
    {
        string source = ReadFixture("ChirupippiEnemy.txt");
        string duplicateStat = source.Replace("【防御点】0", "【敏捷B】1");
        InvalidOperationException statError = Assert.Throws<InvalidOperationException>(() => EnemyTextConverter.Parse(duplicateStat));
        StringAssert.Contains("能力値が重複", statError.Message);

        EnemyTextData duplicateSkill = EnemyTextConverter.Parse(source);
        duplicateSkill.Enemy.Skills.Add(duplicateSkill.Enemy.Skills[0]);
        InvalidOperationException skillError = Assert.Throws<InvalidOperationException>(() => EnemyTextConverter.Build(duplicateSkill));
        StringAssert.Contains("行動スキル名が重複", skillError.Message);

        EnemyTextData spacedStat = EnemyTextConverter.Parse(source);
        spacedStat.Enemy.Stats.Add(new EnemyStatDefinition { Name = " 敏捷B ", Formula = "1" });
        Assert.Throws<InvalidOperationException>(() => EnemyTextConverter.Build(spacedStat));

        EnemyTextData brokenDelimiter = EnemyTextConverter.Parse(source);
        brokenDelimiter.Enemy.Name = "壊れ》名";
        Assert.Throws<InvalidOperationException>(() => EnemyTextConverter.Build(brokenDelimiter));

        EnemyTextData summonCollision = EnemyTextConverter.Parse(source);
        summonCollision.Enemy.Skills[0].Summons.Add(new SummonedEntityDefinition { Name = " 飛来 " });
        Assert.Throws<InvalidOperationException>(() => EnemyTextConverter.Build(summonCollision));

        EnemyTextData whitespaceFlavor = EnemyTextConverter.Parse(source);
        whitespaceFlavor.Enemy.Flavor = "   ";
        string built = EnemyTextConverter.Build(whitespaceFlavor);
        Assert.That(EnemyTextConverter.Build(EnemyTextConverter.Parse(built)), Is.EqualTo(built));

        EnemyTextData reservedConnector = EnemyTextConverter.Parse(source);
        reservedConnector.Enemy.Actions[0].AnyConditions[0].Value = "A、またはB";
        Assert.Throws<InvalidOperationException>(() => EnemyTextConverter.Build(reservedConnector));
    }

    [TestCase("- 71～100：《小さな羽根》×1を獲得", "- 70～100：《小さな羽根》×1を獲得", "重複")]
    [TestCase("- 71～100：《小さな羽根》×1を獲得", "- 71～101：《小さな羽根》×1を獲得", "最大値")]
    [TestCase("- 71～100：《小さな羽根》×1を獲得", "- 100～71：《小さな羽根》×1を獲得", "ドロップ範囲")]
    [TestCase("- 71～100：《小さな羽根》×1を獲得", "- 71～100：《小さな羽根》×0を獲得", "ドロップ個数")]
    public void DropRanges_AreValidated(string original, string replacement, string message)
    {
        string invalid = ReadFixture("ChirupippiEnemy.txt").Replace(original, replacement);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => EnemyTextConverter.Parse(invalid));
        StringAssert.Contains(message, error.Message);
    }

    [Test]
    public void EnemyAndSkillConverters_RejectOppositeRoots()
    {
        Assert.Throws<InvalidOperationException>(() => EnemyTextConverter.Parse(MinimalSkill));
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Parse(ReadFixture("ChirupippiEnemy.txt")));
    }

    [Test]
    public void TransportAndFlavorHeaders_DoNotCorruptSkillBoundaries()
    {
        string source = ReadFixture("ChirupippiEnemy.txt");
        string flavorHeader = source.Replace(
            "ぱたぱたとこちらに向かってくる。\n\n《つつく》",
            "ぱたぱたとこちらに向かってくる。\n《つつく》\nまだ飛んでいる。\n\n《つつく》");
        EnemyTextData parsed = EnemyTextConverter.Parse(flavorHeader);
        Assert.That(parsed.Enemy.Skills[0].Skill.Flavor,
            Is.EqualTo("ぱたぱたとこちらに向かってくる。\n《つつく》\nまだ飛んでいる。"));

        string quoted = source.TrimEnd();
        quoted = "\"" + quoted.Substring(0, quoted.Length - 3) + "```\"";
        quoted = quoted.Replace("白くて可愛らしい小鳥", "白くて\"\"可愛らしい\"\"小鳥");
        Assert.That(EnemyTextConverter.Parse(quoted).Enemy.Flavor,
            Is.EqualTo("白くて\"可愛らしい\"小鳥の魔物、人を見つけると沢山集まってくる。"));
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
