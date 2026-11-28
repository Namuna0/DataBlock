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
    [TestCase("スキルによって〈近接武器〉で武器攻撃を行う時、発動ロール達成値が×1.15倍される。")]
    [TestCase("[知力B]および[魔力B]による行為判定を行う際、達成値が×1.15倍される。")]
    public void ResultMultiplierSpikes_RoundTripWithOnlyTheNewFactor(string passive)
    {
        const string second = "【セカンドスパイク】このパッシブ効果による達成値上昇は×1.2倍に変化する。";
        const string third = "【サードスパイク】このパッシブ効果による達成値上昇は×1.25倍に変化する。";
        string source = "《倍率試験》\n〈種族スキル〉\n【パッシブ効果】" + passive + "\n" + second + "\n" + third;
        var data = SkillTextConverter.Parse(source);

        string rebuilt = SkillTextConverter.Build(data);
        StringAssert.Contains(second + "\n" + third, rebuilt);
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(rebuilt)), Is.EqualTo(JsonUtility.ToJson(data)));
        string explicitSecond = "【セカンドスパイク】このパッシブ効果の補正を「" + passive.Replace("1.15", "1.2") + "」に変更する。";
        Assert.That(SkillTextConverter.Normalize(source.Replace(second, explicitSecond)), Is.EqualTo(rebuilt));
    }

    [Test]
    public void ResultMultiplierSpikes_KeepTheTargetWhenTwoMultipliersMatch()
    {
        const string weapon = "スキルによって〈近接武器〉で武器攻撃を行う時、発動ロール達成値が×1.15倍される。";
        const string stat = "[知力B]による行為判定を行う際、達成値が×1.1倍される。";
        const string header = "《複数倍率試験》\n〈種族スキル〉\n【パッシブ効果】\n" + weapon + "\n" + stat + "\n【セカンドスパイク】";
        string change = "このパッシブ効果の補正を「" + weapon.Replace("1.15", "1.2") + "」に変更する。";
        var data = SkillTextConverter.Parse(header + change);
        string rebuilt = SkillTextConverter.Build(data);

        StringAssert.Contains("【セカンドスパイク】" + change, rebuilt);
        Assert.That(SkillTextConverter.Parse(rebuilt).Skill.Overrides.Single(e => e.Type == EffectType.SecondSpike)
            .Contents.Single().Parameters, Is.EqualTo(new[] { "Rule", "WeaponActivation", "近接武器", "1.2" }));
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Parse(header + "このパッシブ効果による達成値上昇は×1.2倍に変化する。"));
    }

    [TestCase("パッシブ", "環境による行為判定を行う際、達成値が+10される。", "達成値上昇", "+10", "+15")]
    [TestCase("パッシブ", "〈錬金術〉による行為判定の達成値が+10される。", "達成値上昇", "+10", "+15")]
    [TestCase("アクティブ", "このターン中、〈弓〉の武器威力を参照する〈攻撃〉の発動ロールの達成値が+10される。", "達成値上昇", "+10", "+15")]
    [TestCase("パッシブ", "戦闘開始から2ターンの間、[敏捷B]による発動ロールの達成値が+5される。", "達成値上昇", "+5", "+7")]
    [TestCase("パッシブ", "戦闘開始から2ターンの間、自身の被ダメージは×0.8倍される。", "被ダメージ", "×0.8倍", "×0.75倍")]
    [TestCase("パッシブ", "〈弓, 忍具〉の装備を制作するとき、目標値が-10される。", "目標値の減少", "-10", "-15")]
    [TestCase("パッシブ", "スキル習得に必要な消費決意が-5される。", "消費決意", "-5", "-7")]
    [TestCase("パッシブ", "クラス《ナイト》の習得決意が×0.9倍される。", "消費決意", "×0.9倍", "×0.85倍")]
    [TestCase("パッシブ", "一日の採取回数上限が+1追加される。", "採取回数上限", "+1", "+3")]
    [TestCase("パッシブ", "SP最大値が×1.2倍される。", "SP最大値", "×1.2倍", "×1.25倍")]
    [TestCase("パッシブ", "NPCへの〈貨幣〉を除くアイテムの売値が×1.1倍される。", "売値増加", "×1.1倍", "×1.15倍")]
    [TestCase("パッシブ", "〈神聖術〉の威力が×1.2倍される。", "威力", "×1.2倍", "×1.25倍")]
    [TestCase("パッシブ", "〈神聖術〉によるリソースの回復量が×1.2倍される。", "リソースの回復量", "×1.2倍", "×1.25倍")]
    public void ModifierSpikes_ShortenQuotedChangesAndPreserveData(string section, string basis, string subject, string oldAmount, string newAmount)
    {
        string header = "《補正試験》\n〈種族スキル〉\n【" + section + "効果】" + basis + "\n【セカンドスパイク】";
        string change = "この" + section + "効果の補正を「" + basis.Replace(oldAmount, newAmount) + "」に変更する。";
        string shortChange = "この" + section + "効果による" + subject + "は" + newAmount + "に変化する。";
        var data = SkillTextConverter.Parse(header + change);
        string rebuilt = SkillTextConverter.Build(data);
        StringAssert.Contains("【セカンドスパイク】" + shortChange, rebuilt);
        Assert.That(SkillTextConverter.Normalize(header + shortChange), Is.EqualTo(rebuilt));
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(rebuilt)), Is.EqualTo(JsonUtility.ToJson(data)));
    }

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
        EffectDefinition reduction = echo.Overrides.Single(x => x.Contents.Any(c => c.Type == EffectContentType.ReduceResourceCostPerMappedStacks));
        Assert.That(reduction.Contents.Single().Parameters, Is.EqualTo(new[] { "Self", "ACT", "顕現", "3", "1", "One" }));
        Assert.That(reduction.Triggers.Single().Timing, Is.EqualTo(TriggerTiming.ResourceCost));
        EffectDefinition recovery = echo.Effects.Single(x => x.Triggers.Any(t => t.Timing == TriggerTiming.StateStackChanged));
        Assert.That(recovery.Triggers.Single().Conditions.And.Single().Parameters,
            Is.EqualTo(new[] { "Self", "顕現", "3", "One" }));
        Assert.That(recovery.Contents.Single().Parameters, Is.EqualTo(new[] { "Self", "ACT", "1" }));

        SkillBody whisper = skills["エレメンタルウィスパー"].Skill;
        EffectDefinition power = whisper.Overrides.Single(x => x.Type == EffectType.Passive && x.Contents.Any(c => c.Type == EffectContentType.MultiplyActionPower));
        Assert.That(power.Triggers, Is.Empty);
        Assert.That(power.Contents.Single().Parameters, Is.EqualTo(new[] { "Self", "精霊魔法", "1.1" }));
        Assert.That(whisper.Overrides.Single(x => x.Contents.Any(c => c.Type == EffectContentType.MultiplyResourceCost)).Contents.Single().Parameters,
            Is.EqualTo(new[] { "Self", "MP", "0.9" }));
        Assert.That(whisper.Overrides.Where(x => x.Type == EffectType.SecondSpike).SelectMany(x => x.Contents)
            .Where(x => x.Type == EffectContentType.SetModifier).Select(x => x.Parameters), Is.EquivalentTo(new[]
        {
            new[] { "MultiplyActionPower", "ActionCategory", "精霊魔法", "1.15" },
            new[] { "MultiplyResourceCost", "ActionCategory", "精霊魔法", "MP", "0.85" }
        }));

        SkillBody veil = skills["マナヴェール"].Skill;
        Assert.That(veil.Overrides.Where(x => x.Type == EffectType.Passive).SelectMany(x => x.Contents)
            .Where(x => x.Type == EffectContentType.ModifyStat).Select(x => x.Parameters), Is.EquivalentTo(new[]
        {
            new[] { "Self", "防御点", "Add", "+30" },
            new[] { "Self", "MP最大値", "Add", "-10" }
        }));
        Assert.That(veil.Overrides.Where(x => x.Type == EffectType.SecondSpike).SelectMany(x => x.Contents).Single().Parameters,
            Is.EqualTo(new[] { "ModifyStat", "Self", "防御点", "Add", "+40" }));

        SkillBody wisdom = skills["長命種の叡智"].Skill;
        EffectDefinition result = wisdom.Overrides.Single(x => x.Type == EffectType.Passive);
        Assert.That(result.Contents.Single().Type, Is.EqualTo(EffectContentType.AddActionResult));
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
            .Count(x => x.Type == EffectContentType.ReduceResourceCostPerMappedStacks), Is.EqualTo(2));
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
        EffectContent defense = parsed.Skill.Overrides.SelectMany(x => x.Contents)
            .Single(x => x.Type == EffectContentType.ModifyStat && x.Parameters[1] == "防御点");

        Assert.That(defense.Parameters, Is.EqualTo(new[] { "Target", "防御点", "Add", "+30" }));
        Assert.That(SkillTextConverter.Parse(SkillTextConverter.Build(parsed)).Skill.Overrides.SelectMany(x => x.Contents)
            .Single(x => x.Type == EffectContentType.ModifyStat && x.Parameters[1] == "防御点").Parameters,
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
