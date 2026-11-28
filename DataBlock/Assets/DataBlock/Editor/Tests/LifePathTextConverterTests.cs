using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

internal sealed class LifePathTextConverterTests
{
    private const string Basic = "《旅育ち》\n〈ライフパス〉\n【ライフパス能力値】感知+10, 幸運-5";
    private static string CsvCell(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";

    [Test]
    public void CsvKeepsColumnClassificationAcrossAStandaloneRoundTrip()
    {
        string source = "性格,出自\n," + CsvCell(Basic) + "\n" +
            CsvCell("《回復》\n〈一般スキル〉\n【アクティブ効果】自身のHPを10回復する。") + ",";
        var cells = RulebookCsv.Read(new StringReader(source)).ToArray();
        Assert.That(cells.Length, Is.EqualTo(2));
        Assert.That(cells[0].Error, Is.Null);
        var life = (LifePathTextData)cells[0].Data;
        Assert.That(life.Group, Is.EqualTo("出自"));
        Assert.That(life.AbilityBonuses.Select(b => b.Amount), Is.EqualTo(new[] { 10, -5 }));
        var again = LifePathTextConverter.Parse(cells[0].BuildText());
        Assert.That(JsonUtility.ToJson(again), Is.EqualTo(JsonUtility.ToJson(life)));
        Assert.That(cells[1].Data, Is.TypeOf<SkillTextData>());
    }

    [Test]
    public void NamedCandidatesRetainTheirRewardKindAndEditedQuantity()
    {
        string choices = Basic + "\n【パッシブ効果】以下のクラスから1つ選択して習得する。\n- 《織り手》\n- 《木工師》\n"
            + "さらに以下の内の1つから選択してアイテムを獲得する。\n- 家畜アイテム《羊》×2\n- 《土地証書》";
        var data = LifePathTextConverter.Parse(choices);
        var groups = data.Definition.Skill.Effects;
        Assert.That(groups.Select(g => g.Contents[0].Parameters[0]), Is.EqualTo(new[] { "Class", "Item" }));
        Assert.That(groups[1].Contents[1].Parameters, Is.EqualTo(new[] { "Item", "羊", "2", "家畜アイテム" }));
        groups[1].Contents[1].Parameters[2] = "4";
        string rebuilt = LifePathTextConverter.Build(data);
        Assert.That(rebuilt, Does.Contain("家畜アイテム《羊》×4"));
        Assert.That(JsonUtility.ToJson(LifePathTextConverter.Parse(rebuilt)), Is.EqualTo(JsonUtility.ToJson(data)));
    }

    [Test]
    public void RepeatedStatOverridesAndMissingModifierTargetsNeedCorrection()
    {
        const string stage = "\n【セカンドスパイク】このライフパス能力値による感知は";
        Assert.Throws<System.InvalidOperationException>(() =>
            LifePathTextConverter.Parse(Basic + stage + "+12に変化する。" + stage + "+14に変化する。"));
        Assert.Throws<System.InvalidOperationException>(() =>
            LifePathTextConverter.Parse(Basic + "\n【パッシブ効果】自身の×0.86倍される。"));
    }

    [Test]
    public void ConflictingExplicitGroupIsReportedAtTheOriginalCell()
    {
        string text = Basic.Replace("【ライフパス能力値】", "【ライフパス分類】心身\n【ライフパス能力値】");
        var cell = RulebookCsv.Read(new StringReader("能力\n" + CsvCell(text))).Single();
        Assert.That(cell.Row, Is.EqualTo(2));
        Assert.That(cell.Data, Is.Null);
        Assert.That(cell.Error, Does.Contain("心身").And.Contain("能力"));
    }

    [Test]
    public void InspectorRebuildAndJsonUseTheEditedBonusAndSeparateDefinitions()
    {
        var go = new GameObject("LifePathInspectorTest");
        Editor editor = null;
        try
        {
            var block = go.AddComponent<LifePathDataBlock>();
            editor = Editor.CreateEditor(block);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var text = editor.GetType().GetField("_text", flags);
            var run = editor.GetType().GetMethod("Run", flags);
            void Apply(int operation) => run.Invoke(editor, new object[] { operation });
            text.SetValue(editor, Basic);
            Apply(0);
            Apply(1);
            Assert.That(block.Data.Group, Is.Empty);
            block.Data.AbilityBonuses[0].Amount = 12;
            Apply(2);
            string rebuilt = (string)text.GetValue(editor);
            Assert.That(LifePathTextConverter.Parse(rebuilt).AbilityBonuses[0].Amount, Is.EqualTo(12));
            block.Data.Definition.States.Add(new StateDefinition { Name = "守護" });
            block.Data.Definition.Summons.Add(new SummonedEntityDefinition { Name = "使い魔" });
            Apply(3);
            var preview = ((LifePathDataBlockEditor)editor).JsonPreview;
            Assert.That((string)JsonNode.Parse(preview.Main)["Definition"]["Skill"]["Name"], Is.EqualTo("旅育ち"));
            Assert.That(preview.Main, Does.Not.Contain("\"States\"").And.Not.Contain("\"Summons\""));
            Assert.That((string)JsonNode.Parse(preview.States)[0]["Name"], Is.EqualTo("守護"));
            Assert.That((string)JsonNode.Parse(preview.Summons)[0]["Name"], Is.EqualTo("使い魔"));
        }
        finally
        {
            if (editor != null) Object.DestroyImmediate(editor);
            Object.DestroyImmediate(go);
        }
    }
}
