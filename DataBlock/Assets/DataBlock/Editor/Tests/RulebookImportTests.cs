using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

internal sealed class RulebookImportTests
{
    [Test]
    public void RulebookExamples_RoundTripThroughJsonAndText()
    {
        string path = Path.Combine(Application.dataPath, "DataBlock/Editor/Tests/Fixtures/RulebookCells.csv");
        using (var input = File.OpenText(path))
        {
            var cells = RulebookCsv.Read(input).ToList();
            Assert.That(cells.Count, Is.EqualTo(8));
            foreach (var cell in cells)
            {
                Assert.That(cell.Error, Is.Null, cell.Name);
                var copy = JsonUtility.FromJson(JsonUtility.ToJson(cell.Data), cell.Data.GetType());
                string expected = cell.BuildText();
                if (copy is SkillTextData skill)
                {
                    Assert.That(SkillTextConverter.Build(skill), Is.EqualTo(expected));
                    Assert.That(SkillTextConverter.Normalize(expected), Is.EqualTo(expected));
                }
                else if (copy is CharacterProfileData profile)
                {
                    Assert.That(SkillTextConverter.BuildProfile(profile), Is.EqualTo(expected));
                    Assert.That(SkillTextConverter.BuildProfile(SkillTextConverter.ParseProfile(expected)), Is.EqualTo(expected));
                }
                else
                {
                    Assert.That(SkillTextConverter.BuildSiegeWeapon((SiegeWeaponData)copy), Is.EqualTo(expected));
                    Assert.That(SkillTextConverter.BuildSiegeWeapon(SkillTextConverter.ParseSiegeWeapon(expected)), Is.EqualTo(expected));
                }
            }
            var repair = ((SkillTextData)cells[5].Data).Skill;
            Assert.That(repair.Roll.Count, Is.EqualTo(5));
            Assert.That(repair.Effects.Single(e => e.Type == EffectType.Fumble).Contents.Single().Parameters, Is.EqualTo(new[] { "-1" }));
            Assert.That(((SiegeWeaponData)cells[3].Data).RequiredSpace, Is.GreaterThan(0));
        }
    }

    [Test]
    public void Csv_KeepsCoordinatesAndQuotedFlavor_SkipsDraftsAndSecret()
    {
        string valid = "《試験》\n〈生活スキル〉\n【ロールプレイ効果】観察する。\n――――――――\n彼は\"鳥,魚\"と呼ぶ。";
        string quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        string draft = "《草案》\n足止め+自身接近";
        string secret = "《未公開》\n〈特性〉\n【パッシブ効果】秘匿";
        string invalid = "《誤記》\n〈一般スキル〉\n【アクティブ効果】未知の戦闘処理。";
        string csv = "見出し," + quote(secret) + "\r\n" + quote(draft) + ",《名前》," + quote(invalid) + "\r\n," + quote(valid);
        var cells = RulebookCsv.Read(new StringReader(csv)).ToList();
        Assert.That(cells.Count, Is.EqualTo(2));
        Assert.That(cells[0].Error, Is.Not.Empty);
        Assert.That(cells[0].Row, Is.EqualTo(2));
        Assert.That(cells[0].Column, Is.EqualTo(3));
        Assert.That(cells[1].Row, Is.EqualTo(3));
        Assert.That(cells[1].Column, Is.EqualTo(2));
        Assert.That(((SkillTextData)cells[1].Data).Skill.Flavor, Does.Contain("\"鳥,魚\""));
        Assert.Throws<FormatException>(() => RulebookCsv.Read(new StringReader("\"broken")).ToList());
    }

    [Test]
    public void Csv_ExcludesUnfinishedChoicesAndKeepsCompletedNeighbors()
    {
        string drafts = "\"《途中》\n〈一般スキル〉\n【発動ロール】1d100 目標値\n【アクティブ効果】対象を1の威力で攻撃する。\","
            + "\"《選択肢未記入》\n〈一般スキル〉\n【説明】下記の効果を一つ選んで実行する。\n【アクティブ効果】対象を1の威力で攻撃する。\",";
        var cells = RulebookCsv.Read(new StringReader(drafts + "\"《観察》\n〈生活スキル〉\n【ロールプレイ効果】周囲を観察する。\"")).ToList();
        Assert.That(cells.Select(c => c.Name), Is.EqualTo(new[] { "観察" }));
        Assert.That(cells[0].Column, Is.EqualTo(3));
        using (var output = new StringWriter())
        {
            RulebookCsvWindow.WriteJson(output, cells);
            Assert.That(output.ToString(), Does.Contain("\"column\":3").And.Contain("\"kind\":\"skill\""));
        }
    }

    [Test]
    public void StateChoiceLabelsAndRewardTableNamesComeFromTheSource()
    {
        const string source = "《試験》\n〈一般スキル〉\n【アクティブ効果】自身は《先見》状態になる。\n"
            + "先見効果：\n●見通す：未来についての出来事をGMの指定によって知る事が出来る。\n"
            + "●見定める：自身の次の2回の行為判定を3度振りどちらかの結果を選ぶ事が出来る。\n"
            + "【宝探しロール】1d6\n1～6：《記念品》×1";
        var data = SkillTextConverter.Parse(source);
        var choices = data.States.Single().Effects.SelectMany(e => e.Contents).ToArray();
        Assert.That(choices[0].Parameters, Is.EqualTo(new[] { "見通す" }));
        Assert.That(choices[1].Parameters, Is.EqualTo(new[] { "見定める", "2", "3" }));
        var table = data.Skill.Effects.SelectMany(e => e.Contents).Single(c => c.Type == EffectContentType.RollNamedRewardTable);
        Assert.That(table.Parameters, Is.EqualTo(new[] { "宝探しロール", "1d6" }));
        string text = SkillTextConverter.Build(data);
        Assert.That(SkillTextConverter.Normalize(text), Is.EqualTo(text));
    }

    [Test]
    public void CombinedOutcomesApplyEachLineToEachResult()
    {
        const string source = "《試験》\n〈一般スキル〉\n【成功/クリティカル効果】\n個数+1\n耐久+2";
        var data = SkillTextConverter.Parse(source);
        foreach (var stage in new[] { EffectType.Success, EffectType.Critical })
            Assert.That(data.Skill.Effects.Where(e => e.Type == stage).SelectMany(e => e.Contents).Select(c => c.Type),
                Is.EqualTo(new[] { EffectContentType.ModifyOutputCount, EffectContentType.ChangeSelectedItemDurability }));
        string text = SkillTextConverter.Build(data);
        Assert.That(SkillTextConverter.Normalize(text), Is.EqualTo(text));
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Parse(source.Replace("成功/クリティカル", "成功/成功")));
    }

    [Test]
    public void CommonSummonMaximumAndRecoveryRulesUseTheDeclaredResources()
    {
        const string source = "《召喚》\n〈一般スキル〉\n【アクティブ効果】ペット《仲間》を召喚する。\n"
            + "《仲間》\n〈ペット〉\n【HP/MP/SP最大値】12\n【アクティブ効果】自身は《不調》状態になる。\n"
            + "不調効果：自身はSP及びSANを回復することができなくなる。";
        var data = SkillTextConverter.Parse(source);
        Assert.That(data.Summons.Single().Stats.Select(s => s.Name), Is.EqualTo(new[] { "HP", "MP", "SP" }));
        Assert.That(data.Summons.Single().Stats.All(s => s.Formula == "12"), Is.True);
        var text = SkillTextConverter.Build(data);
        Assert.That(text, Does.Contain("SPを回復する").And.Contain("SANを回復する"));
        Assert.That(SkillTextConverter.Normalize(text), Is.EqualTo(text));
    }

    [Test]
    public void Costs_PreserveKindsAndQuantities()
    {
        string text = "《消費確認》\n〈一般スキル〉\n【消費リソース】任意のHP, 《任意名称》スタック-4, MP-(4+[蓄積]*4)";
        var skill = SkillTextConverter.Parse(text);
        Assert.That(skill.Skill.Costs[0].Quantity, Is.EqualTo(CostQuantity.Chosen));
        Assert.That(skill.Skill.Costs[1].Kind, Is.EqualTo(CostKind.NamedStack));
        Assert.That(skill.Skill.Costs[1].Amount, Is.EqualTo(4));
        Assert.That(skill.Skill.Costs[2].Formula, Is.EqualTo("(4+[蓄積]*4)"));
        Assert.That(SkillTextConverter.Normalize(SkillTextConverter.Build(skill)), Is.EqualTo(SkillTextConverter.Build(skill)));
    }
}
