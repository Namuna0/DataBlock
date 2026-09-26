#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

internal sealed class RacialSkillTextConverterTests
{
    [Test]
    public void RacialSkills_AllFiveNormalizeAndRoundTrip()
    {
        string normalized = SkillTextConverter.Normalize(ReadFixture());
        MatchCollection blocks = Regex.Matches(normalized, @"(?s)```.*?```");
        string[] expected = { "魔法代謝", "エレメンタルエコー", "エレメンタルウィスパー", "マナヴェール", "長命種の叡智" };

        Assert.That(blocks.Count, Is.EqualTo(expected.Length));
        Assert.That(SkillTextConverter.Normalize(normalized), Is.EqualTo(normalized));
        for (int i = 0; i < blocks.Count; i++)
        {
            SkillTextData data = SkillTextConverter.Parse(blocks[i].Value);
            Assert.That(data.Skill.Name, Is.EqualTo(expected[i]));
            Assert.That(SkillTextConverter.Build(data), Is.EqualTo(blocks[i].Value));
        }
    }

    [Test]
    public void RacialSkills_MapToGenericSemanticNodes()
    {
        Dictionary<string, SkillTextData> skills = Skills();

        SkillBody metabolism = skills["魔法代謝"].Skill;
        Assert.That(metabolism.AcquisitionConditions.And.Single().Type, Is.EqualTo(ConditionType.AutomaticAtRaceSelection));
        Assert.That(metabolism.Overrides.SelectMany(x => x.Contents).Single().Parameters,
            Is.EqualTo(new[] { "Self", "MP最大値", "Multiply", "1.2" }));
        EffectDefinition collapse = metabolism.Effects.Single();
        Assert.That(collapse.Triggers.Single().Timing, Is.EqualTo(TriggerTiming.ResourceChanged));
        Assert.That(collapse.Triggers.Single().Conditions.And.Single().Parameters,
            Is.EqualTo(new[] { "Self", "MP", "AtMost", "0" }));
        Assert.That(collapse.Contents.Single().Parameters, Is.EqualTo(new[] { "Self", "戦闘不能" }));

        SkillBody echo = skills["エレメンタルエコー"].Skill;
        EffectDefinition gainDefinition = echo.Effects.Single(x => x.Contents.Any(c => c.Type == EffectContentType.GainMappedStateStacks));
        Assert.That(gainDefinition.Triggers.Single().Timing, Is.EqualTo(TriggerTiming.ActionActivated));
        Assert.That(gainDefinition.Triggers.Single().Conditions.And.Single(x => x.Type == ConditionType.ActionCategory).Parameters,
            Is.EqualTo(new[] { "精霊魔法" }));
        EffectContent gain = gainDefinition.Contents.Single();
        Assert.That(gain.Parameters.Take(4), Is.EqualTo(new[] { "Self", "1", "Each", "顕現" }));
        Assert.That(gain.Parameters.Skip(4), Is.EqualTo(new[]
        {
            "火", "火精の昂り", "水", "水精のさざめき", "風", "風精の囁き",
            "電", "雷精の轟き", "冷", "氷精の嬉笑", "土", "土精の踊躍"
        }));
        OverrideDefinition reduction = echo.Overrides.Single(x => x.Contents.Any(c => c.Type == OverrideContentType.ReduceResourceCostPerMappedStacks));
        Assert.That(reduction.Contents.Single().Parameters, Is.EqualTo(new[] { "Self", "ACT", "顕現", "3", "1", "One" }));
        Assert.That(reduction.Triggers.Single().Timing, Is.EqualTo(TriggerTiming.ResourceCost));
        EffectDefinition recovery = echo.Effects.Single(x => x.Triggers.Any(t => t.Timing == TriggerTiming.StateStackChanged));
        Assert.That(recovery.Triggers.Single().Conditions.And.Single().Parameters,
            Is.EqualTo(new[] { "Self", "顕現", "3", "One" }));
        Assert.That(recovery.Contents.Single().Parameters, Is.EqualTo(new[] { "Self", "ACT", "1" }));

        SkillBody whisper = skills["エレメンタルウィスパー"].Skill;
        OverrideDefinition power = whisper.Overrides.Single(x => x.Type == EffectType.Passive && x.Contents.Any(c => c.Type == OverrideContentType.MultiplyPower));
        Assert.That(power.Triggers.Single().Conditions.And.Single(x => x.Type == ConditionType.ActionCategory).Parameters,
            Is.EqualTo(new[] { "精霊魔法" }));
        Assert.That(whisper.Overrides.Single(x => x.Contents.Any(c => c.Type == OverrideContentType.MultiplyResourceCost)).Contents.Single().Parameters,
            Is.EqualTo(new[] { "Self", "MP", "0.9" }));
        Assert.That(whisper.Overrides.Where(x => x.Type == EffectType.SecondSpike).SelectMany(x => x.Contents)
            .Where(x => x.Type == OverrideContentType.SetModifier).Select(x => x.Parameters), Is.EquivalentTo(new[]
        {
            new[] { "MultiplyPower", "ActionCategory", "精霊魔法", "1.15" },
            new[] { "MultiplyResourceCost", "ActionCategory", "精霊魔法", "MP", "0.85" }
        }));

        SkillBody veil = skills["マナヴェール"].Skill;
        Assert.That(veil.Overrides.Where(x => x.Type == EffectType.Passive).SelectMany(x => x.Contents)
            .Where(x => x.Type == OverrideContentType.ModifyStat).Select(x => x.Parameters), Is.EquivalentTo(new[]
        {
            new[] { "Self", "防御点", "Add", "+30" },
            new[] { "Self", "MP最大値", "Add", "-10" }
        }));
        Assert.That(veil.Overrides.Where(x => x.Type == EffectType.SecondSpike).SelectMany(x => x.Contents).Single().Parameters,
            Is.EqualTo(new[] { "ModifyStat", "Self", "防御点", "Add", "+40" }));

        SkillBody wisdom = skills["長命種の叡智"].Skill;
        OverrideDefinition result = wisdom.Overrides.Single(x => x.Type == EffectType.Passive);
        Assert.That(result.Contents.Single().Type, Is.EqualTo(OverrideContentType.AddActionResult));
        Assert.That(result.Triggers.Single().Conditions.And.Single(x => x.Type == ConditionType.ActionStat).Parameters,
            Is.EqualTo(new[] { "知力B" }));
        Assert.That(wisdom.Overrides.Where(x => x.Type == EffectType.ThirdSpike).SelectMany(x => x.Contents).Single().Parameters,
            Is.EqualTo(new[] { "AddActionResult", "ActionStat", "知力B", "+10" }));
    }

    [Test]
    public void RacialSkill_JsonRoundTrip()
    {
        SkillTextData original = Skills()["エレメンタルエコー"];
        string expected = SkillTextConverter.Build(original);

        string json = JsonUtility.ToJson(original, true);
        SkillTextData restored = JsonUtility.FromJson<SkillTextData>(json);

        Assert.That(json, Is.Not.Empty);
        Assert.That(SkillTextConverter.Build(restored), Is.EqualTo(expected));
    }

    [Test]
    public void RacialSkills_RejectDuplicateAttributeAndMissingSpikeTarget()
    {
        string echo = Block("エレメンタルエコー").Replace("水属性：《水精のさざめき》", "火属性：《水精のさざめき》");
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Parse(echo));

        string veil = Block("マナヴェール").Replace("防御点+30, ", "");
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Parse(veil));
    }

    [Test]
    public void MultipleMappedStackRules_NormalizeIdempotently()
    {
        const string secondRule =
            "自身が〈竜魔法〉を発動するたびに属性に応じた状態を2スタック得る。\n" +
            "光属性：《光精の煌めき》\n" +
            "※複数属性の場合はそれぞれ1種類ずつ。\n\n" +
            "効果〈第二顕現〉：自身の〈竜魔法〉による消費MPはその属性の[属性に応じたスタック]×2につき1減少する。\n" +
            "さらにスタックが2つ溜まるごとにMPが1回復する。\n" +
            "※複数属性の場合はどちらか片方の種類を参照。\n";
        string source = Block("エレメンタルエコー").Replace(
            "――――――――――――――――",
            secondRule + "――――――――――――――――");

        string normalized = SkillTextConverter.Normalize(source);
        SkillTextData parsed = SkillTextConverter.Parse(normalized);

        Assert.That(SkillTextConverter.Normalize(normalized), Is.EqualTo(normalized));
        Assert.That(parsed.Skill.Effects.SelectMany(x => x.Contents)
            .Count(x => x.Type == EffectContentType.GainMappedStateStacks), Is.EqualTo(2));
        Assert.That(parsed.Skill.Overrides.SelectMany(x => x.Contents)
            .Count(x => x.Type == OverrideContentType.ReduceResourceCostPerMappedStacks), Is.EqualTo(2));
        Assert.That(parsed.Skill.Effects.Count(x => x.Triggers.Any(t => t.Timing == TriggerTiming.StateStackChanged)), Is.EqualTo(2));

        List<EffectDefinition> recoveries = parsed.Skill.Effects
            .Where(x => x.Triggers.Any(t => t.Timing == TriggerTiming.StateStackChanged)).ToList();
        int first = parsed.Skill.Effects.IndexOf(recoveries[0]);
        int second = parsed.Skill.Effects.IndexOf(recoveries[1]);
        parsed.Skill.Effects[first] = recoveries[1];
        parsed.Skill.Effects[second] = recoveries[0];
        string reordered = SkillTextConverter.Build(parsed);
        Assert.That(SkillTextConverter.Build(SkillTextConverter.Parse(reordered)), Is.EqualTo(reordered));
    }

    [Test]
    public void StatModifier_PreservesExplicitTarget()
    {
        string source = Block("マナヴェール")
            .Replace("防御点+30", "対象の防御点+30")
            .Replace("【セカンドスパイク】このパッシブ効果による防御点増加は+40に変化する。\n", "")
            .Replace("【サードスパイク】このパッシブ効果による防御点増加は+60に変化する。\n", "");

        SkillTextData parsed = SkillTextConverter.Parse(source);
        OverrideContent defense = parsed.Skill.Overrides.SelectMany(x => x.Contents)
            .Single(x => x.Type == OverrideContentType.ModifyStat && x.Parameters[1] == "防御点");

        Assert.That(defense.Parameters, Is.EqualTo(new[] { "Target", "防御点", "Add", "+30" }));
        Assert.That(SkillTextConverter.Parse(SkillTextConverter.Build(parsed)).Skill.Overrides.SelectMany(x => x.Contents)
            .Single(x => x.Type == OverrideContentType.ModifyStat && x.Parameters[1] == "防御点").Parameters,
            Is.EqualTo(defense.Parameters));
    }

    private static Dictionary<string, SkillTextData> Skills()
    {
        string normalized = SkillTextConverter.Normalize(ReadFixture());
        return Regex.Matches(normalized, @"(?s)```.*?```").Cast<Match>()
            .Select(x => SkillTextConverter.Parse(x.Value)).ToDictionary(x => x.Skill.Name, StringComparer.Ordinal);
    }

    private static string Block(string name)
    {
        return Regex.Matches(ReadFixture(), @"(?s)```.*?```").Cast<Match>()
            .Single(x => x.Value.Contains("《" + name + "》")).Value;
    }

    private static string ReadFixture()
    {
        foreach (string seed in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(seed);
            while (directory != null)
            {
                string projectPath = Path.Combine(directory.FullName, "Assets", "DataBlock", "Editor", "Tests", "Fixtures", "RacialSkills.txt");
                if (File.Exists(projectPath)) return File.ReadAllText(projectPath);
                string packagePath = Path.Combine(directory.FullName, "Editor", "Tests", "Fixtures", "RacialSkills.txt");
                if (File.Exists(packagePath)) return File.ReadAllText(packagePath);
                directory = directory.Parent;
            }
        }
        throw new FileNotFoundException("種族スキルfixtureが見つかりません。", "RacialSkills.txt");
    }
}
#endif
