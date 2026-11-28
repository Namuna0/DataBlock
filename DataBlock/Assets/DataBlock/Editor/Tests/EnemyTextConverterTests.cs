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

        StringAssert.StartsWith("```\n", normalized);
        Assert.That(normalized.Split('\n'), Does.Contain("【宣言条件】自身が〈攻撃〉を受けた時"));
        Assert.That(EnemyTextConverter.Build(parsed), Is.EqualTo(normalized));
        Assert.That(EnemyTextConverter.Normalize(normalized), Is.EqualTo(normalized));

        string json = JsonUtility.ToJson(parsed, true);
        StringAssert.DoesNotContain("DropRoll", json);
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

        Assert.That(enemy.Drops.Select(x => x.Minimum + "-" + x.Maximum + ":" + x.ItemName + "x" + x.Amount),
            Is.EqualTo(new[] { "31-70:鳥肉x1", "71-100:小さな羽根x1" }));
        StringAssert.StartsWith("白くて可愛らしい小鳥", enemy.Flavor);
    }

    [Test]
    public void ReferenceWarnings_PreserveUndefinedAndIndirectlyUsedSkillNames()
    {
        EnemyTextData data = EnemyTextConverter.Parse(ReadFixture("ChirupippiEnemy.txt"));
        data.Enemy.Actions[0].SkillName = "未定義スキル";
        var restored = EnemyTextConverter.Parse(EnemyTextConverter.Build(data));
        string[] warnings = EnemyTextConverter.ReferenceWarnings(restored.Enemy).ToArray();
        Assert.That(warnings, Has.Some.Contains("行動スキル定義がありません：《未定義スキル》"));
        Assert.That(warnings, Has.Some.Contains("行動欄から直接参照されていないスキル定義です：《飛来》"));
    }

    [Test]
    public void TraitOnlyEnemy_PreservesMissingStatAndTypedEffects()
    {
        const string source = "《試験》\n〈一般エネミー〉\n【危険度】☆\n【防御点】\n【特性】\n"
            + "《再生》：毎ターンHP300回復\n《不明》：???\n――――――――――――――――\n変わった魔物。";
        var data = EnemyTextConverter.Parse(source);
        var contents = data.Enemy.Traits.SelectMany(trait => trait.Skill.Effects).SelectMany(effect => effect.Contents).ToArray();
        Assert.That(contents[0].Type, Is.EqualTo(EffectContentType.RecoverEachTurn));
        Assert.That(contents[0].Parameters, Is.EqualTo(new[] { "HP", "300" }));
        Assert.That(contents[1].Type, Is.EqualTo(EffectContentType.UndisclosedEffect));
        Assert.That(EnemyTextConverter.ReferenceWarnings(data.Enemy), Has.Some.Contains("防御点"));
        Assert.That(data.Enemy.Flavor, Is.EqualTo("変わった魔物。"));
        Assert.That(EnemyTextConverter.Normalize(EnemyTextConverter.Build(data)), Is.EqualTo(EnemyTextConverter.Build(data)));
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
    public void VictoryDeadlineBeforeLoot_IsSeparateFromSkillFlavor()
    {
        const string condition = "【宣言条件】戦闘開始から2ターン目までに勝利";
        var data = EnemyTextConverter.Parse(ReadFixture("ChirupippiEnemy.txt")
            .Replace("【ドロップロール】", condition + "\n\n【ドロップロール】"));
        Assert.That(data.Enemy.LootVictoryTurnLimit, Is.EqualTo(2));
        Assert.That(data.Enemy.Skills.Last().Skill.Flavor, Does.Not.Contain("勝利"));
        Assert.That(data.Enemy.Drops.Count, Is.EqualTo(2));
        string canonical = EnemyTextConverter.Build(data);
        Assert.That(canonical, Does.Contain(condition + "\n【ドロップロール】1d100"));
        Assert.That(EnemyTextConverter.Parse(canonical).Enemy.LootVictoryTurnLimit, Is.EqualTo(2));
    }

    [Test]
    public void DropRoll_IsAlwaysOneHundredSided()
    {
        string invalid = ReadFixture("ChirupippiEnemy.txt")
            .Replace("【ドロップロール】1d100", "【ドロップロール】1d20");

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => EnemyTextConverter.Parse(invalid));
        StringAssert.Contains("固定の1d100", error.Message);
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
        Assert.That(EnemyTextConverter.Normalize(source.Replace("```\n《チルピッピ》", "```none\n《チルピッピ》")), Is.EqualTo(EnemyTextConverter.Normalize(source)));
        Assert.That(EnemyTextConverter.Parse(quoted).Enemy.Flavor,
            Is.EqualTo("白くて\"可愛らしい\"小鳥の魔物、人を見つけると沢山集まってくる。"));

        string categoryless = source.Replace("《飛来》\n〈移動〉\n", "《飛来》\n");
        string normalized = EnemyTextConverter.Normalize(categoryless);
        Assert.That(EnemyTextConverter.Build(EnemyTextConverter.Parse(normalized)), Is.EqualTo(normalized));

        string paddedHeader = source.Replace("《飛来》\n〈移動〉", "《 飛来 》\n〈移動〉");
        Assert.That(EnemyTextConverter.Normalize(paddedHeader), Is.EqualTo(EnemyTextConverter.Normalize(source)));

        string choiceSkill = source.Replace(
            "《飛来》\n〈移動〉",
            "《飛来》\n【説明】下記の効果を一つ選んで実行する。\n\n●飛ぶ\n〈移動〉");
        EnemyTextData choiceParsed = EnemyTextConverter.Parse(EnemyTextConverter.Normalize(choiceSkill));
        Assert.That(choiceParsed.Enemy.Skills[0].Skill.Choices.Single().Name, Is.EqualTo("飛ぶ"));
    }

    [TestCase("【宝探しロール】1d6\n1～6：《記念品》×1")]
    [TestCase("【成功効果】個数+1")]
    [TestCase("【失敗効果】何もなし")]
    [TestCase("【成功/クリティカル効果】個数+1")]
    [TestCase("【消費ACT】1\n【発動】自動成功\n【成功効果】個数+1")]
    public void CategorylessActionUsesTheSameHeadingsAsAStandaloneSkill(string body)
    {
        string skill = "《帰還》\n" + body;
        string source = "《試験エネミー》\n〈一般エネミー〉\n【危険度】☆\n【行動】\n"
            + "●あらゆる自身への〈攻撃〉に対して《帰還》を発動する。\n" + skill + "\n【ドロップロール】1d100\n- 1～100：《羽根》×1を獲得";
        var enemy = EnemyTextConverter.Parse(source);
        Assert.That(JsonUtility.ToJson(enemy.Enemy.Skills.Single()), Is.EqualTo(JsonUtility.ToJson(SkillTextConverter.Parse(skill))));
        string built = EnemyTextConverter.Build(enemy);
        Assert.That(EnemyTextConverter.Normalize(built), Is.EqualTo(built));
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
