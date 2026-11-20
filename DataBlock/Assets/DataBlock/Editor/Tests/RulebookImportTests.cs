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
