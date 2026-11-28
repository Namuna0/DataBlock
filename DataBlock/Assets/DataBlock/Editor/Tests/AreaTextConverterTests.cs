using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

internal sealed class AreaTextConverterTests
{
    private const string Title = "試験国：《山道》\n〈戦闘エリア, 平地〉\n";
    private const string Table = "●エンカウント\n【進行ロール】3回連続：1d6\n"
        + "- 1～3：《小鳥》×2, 《大鳥》×1とエンカウント\n- 4～6：何もなし";

    [TestCase("罠の説明。", "")]
    [TestCase("", "エリアの説明。")]
    [TestCase("罠の説明。", "エリアの説明。")]
    public void TerminalEventAndAreaKeepTheirOwnFlavor(string eventFlavor, string areaFlavor)
    {
        const string terminal = "●エンカウント\n【進行ロール】1d6\n- 1：《試しの罠》\n〈攻撃〉\n"
            + "【アクティブ効果】自身のHPを10回復する。\n――――――――――――――――\n罠の説明。";
        var data = AreaTextConverter.Parse(Title + terminal);
        Assert.That(data.Events.Single().Flavor, Is.EqualTo("罠の説明。"));
        data.Events.Single().Flavor = eventFlavor;
        data.Area.Flavor = areaFlavor;

        var rebuilt = AreaTextConverter.Parse(AreaTextConverter.Build(data));
        Assert.That(rebuilt.Events.Single().Flavor, Is.EqualTo(eventFlavor));
        Assert.That(rebuilt.Area.Flavor, Is.EqualTo(areaFlavor));
    }

    [Test]
    public void MixedCsvImportsOnlyRolledAreasAndKeepsOtherDataTypes()
    {
        string[] inputs = {
            Title + "●エンカウント\n【進行ロール】無し",
            Title + "●ボスエンカウント\n- 《大鳥》×1とエンカウント\n●報酬\n【ドロップロール】1d100",
            Title + Table,
            "《試験》\n〈一般スキル〉\n【アクティブ効果】自身のHPを10回復する。"
        };
        var excluded = new List<RulebookCell>();
        var cells = RulebookCsv.Read(new StringReader(string.Join(",", inputs.Select(s => '\u0022' + s + '\u0022'))),
            onExcluded: excluded.Add).ToArray();
        Assert.That(excluded.Select(c => c.Column), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(cells.Select(c => c.Column), Is.EqualTo(new[] { 3, 4 }));
        Assert.That(cells[0].Data, Is.TypeOf<AreaTextData>(), cells[0].Error);
        Assert.That(cells[1].Data, Is.TypeOf<SkillTextData>(), cells[1].Error);
        Assert.That(cells[0].Name, Is.EqualTo("山道"));
    }

    [Test]
    public void InspectorRebuildAndJsonReflectAnEditedEncounterCount()
    {
        var go = new GameObject("AreaInspectorTest");
        Editor editor = null;
        try
        {
            var block = go.AddComponent<AreaDataBlock>();
            editor = Editor.CreateEditor(block);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var text = editor.GetType().GetField("_text", flags);
            var run = editor.GetType().GetMethod("Run", flags);
            void Apply(int operation) => run.Invoke(editor, new object[] { operation });
            text.SetValue(editor, Title + Table);
            Apply(0);
            Apply(1);
            var table = block.Data.Area.Sections.SelectMany(s => s.Encounters).Single();
            Assert.That(table.Dice, Is.EqualTo("1d6"));
            Assert.That(table.Rolls, Is.EqualTo(3));
            Assert.That(table.Entries[0].Result.Enemies.Select(e => e.Count), Is.EqualTo(new[] { 2, 1 }));
            table.Entries[0].Result.Enemies[0].Count = 7;
            Apply(2);
            var built = (string)text.GetValue(editor);
            Assert.That(built, Does.Contain("《小鳥》×7"));
            Assert.That(AreaTextConverter.Normalize(built), Is.EqualTo(built));
            Apply(3);
            var preview = ((AreaDataBlockEditor)editor).JsonPreview;
            Assert.That((string)JsonNode.Parse(preview.Main)["Area"]["Name"], Is.EqualTo("山道"));
        }
        finally
        {
            if (editor != null) Object.DestroyImmediate(editor);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ASecondTableCanQualifyTheAreaAndOverlappingResultsArePreserved()
    {
        string source = Title + "●水中エンカウント\n【進行ロール】無し\n"
            + "●上空エンカウント\n【進行ロール】1d20\n- 1～5：《小鳥》×1とエンカウント\n- 5～8：《大鳥》×2とエンカウント";
        var data = AreaTextConverter.Parse(source);
        var tables = data.Area.Sections.SelectMany(s => s.Encounters).ToArray();
        Assert.That(tables[0].NoProgressRoll, Is.True);
        Assert.That(tables[1].Entries.Select(r => r.Minimum), Is.EqualTo(new[] { 1, 5 }));
        Assert.That(tables[1].Entries.Select(r => r.Maximum), Is.EqualTo(new[] { 5, 8 }));
        Assert.That(AreaTextConverter.Warnings(data).Single(), Does.Contain("5").And.Contain("重複"));
    }

    [Test]
    public void ATrapDrawsFromItsOwnTableWithoutOverlappingTheAreaRoll()
    {
        var data = AreaTextConverter.Parse(Title + "●エンカウント\n【進行ロール】1d100\n"
            + "- 1～5：《移動床》\n〈罠〉\n【アクティブ効果】対象をパーティーから離脱させ、新しいパーティとして扱う。\n"
            + "その後、ダイスロール1d100を行う。\n- 1～5：《大鳥》×2とエンカウント");
        var table = data.Area.Sections.SelectMany(s => s.Encounters).Single();
        var trap = data.Events.Single();
        Assert.That(table.Entries.Single().EventId, Is.EqualTo(trap.Id));
        Assert.That(trap.FollowUpEntries.Single().Result.Enemies.Single().Count, Is.EqualTo(2));
        Assert.That(AreaTextConverter.Warnings(data), Is.Empty);
    }

    [Test]
    public void GatheringKeepsBothRollCountAndRequiredSuccesses()
    {
        var data = AreaTextConverter.Parse(Title + Table + "\n●採取\n"
            + "- 岩壁：3回連続：1d100*[筋力B]目標値46を2回達成で《石》×4を獲得");
        var check = data.Area.Sections.SelectMany(s => s.Actions).SelectMany(a => a.Entries)
            .Single(e => e.Type == AreaActionEntryType.Check);
        Assert.That(check.Roll.Count, Is.EqualTo(3));
        Assert.That(check.RequiredSuccesses, Is.EqualTo(2));
        Assert.That(AreaTextConverter.Build(data), Does.Contain("を2回達成"));
    }

    [Test]
    public void RebuildReportsMissingAndUnreferencedEvents()
    {
        var data = AreaTextConverter.Parse(Title + Table);
        var row = data.Area.Sections.SelectMany(s => s.Encounters).Single().Entries[0];
        row.EventId = 4;
        Assert.Throws<System.InvalidOperationException>(() => AreaTextConverter.Build(data));
        row.EventId = 0;
        data.Events.Add(new AreaEncounterEvent { Id = 4 });
        Assert.Throws<System.InvalidOperationException>(() => AreaTextConverter.Build(data));
    }

    [Test]
    public void ExportSeparatesBothAreaStatesAndNestedSkillDefinitions()
    {
        var area = AreaTextConverter.Parse(Title + Table);
        area.States.Add(new StateDefinition { Name = "濃霧" });
        var trap = SkillTextConverter.Parse("《落石》\n〈攻撃〉\n【宣言条件】キャラクターを1体選択\n"
            + "【アクティブ効果】自身のHPを10回復する。");
        trap.States.Add(new StateDefinition { Name = "気絶" });
        trap.Summons.Add(new SummonedEntityDefinition { Name = "岩塊" });
        var encounter = new AreaEncounterEvent { Id = 1, Action = trap };
        area.Events.Add(encounter);
        var row = area.Area.Sections.SelectMany(s => s.Encounters).Single().Entries[0];
        row.Result = new AreaEncounterResult { Type = AreaEncounterOutcome.Event, Value = trap.Skill.Name };
        row.EventId = encounter.Id;
        var preview = new DataBlockJsonPreview(area);
        var exported = JsonNode.Parse(preview.Main)["Events"][0]["Action"]["Skill"];
        var full = JsonNode.Parse(JsonUtility.ToJson(trap.Skill));
        Assert.That(JsonNode.DeepEquals(exported, full), Is.True);
        Assert.That(preview.Main, Does.Not.Contain("\"States\"").And.Not.Contain("\"Summons\""));
        CollectionAssert.AreEquivalent(new[] { "濃霧", "気絶" },
            JsonNode.Parse(preview.States).AsArray().Select(s => (string)s["Name"]));
        Assert.That((string)JsonNode.Parse(preview.Summons)[0]["Name"], Is.EqualTo("岩塊"));
    }
}
