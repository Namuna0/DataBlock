using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using NUnit.Framework;
using UnityEngine;

internal sealed class DataBlockJsonExportTests
{
    internal static SkillTextData ReadSkill(DataBlockJsonPreview preview)
    {
        var main = JsonNode.Parse(preview.Main).AsObject();
        Assert.That(main.ContainsKey("Summons") || main.ContainsKey("States"), Is.False);
        main.Add("Summons", JsonNode.Parse(preview.Summons).AsArray());
        main.Add("States", JsonNode.Parse(preview.States).AsArray());
        return JsonUtility.FromJson<SkillTextData>(main.ToJsonString());
    }

    [Test]
    public void EmptyDefinitionsAreSeparateEmptyArrays()
    {
        var preview = new DataBlockJsonPreview(new SkillTextData());
        Assert.That(preview.States, Is.EqualTo("[]"));
        Assert.That(preview.Summons, Is.EqualTo("[]"));
        Assert.That(ReadSkill(preview).Skill, Is.Not.Null);
    }

    [Test]
    public void NestedSkillsAndEquipmentStatesUseSeparateLibraries()
    {
        var skill = new SkillTextData();
        skill.Skill.Name = "付与スキル";
        skill.Skill.Flavor = "引用 \"States\" / Summons";
        skill.Summons.Add(new SummonedEntityDefinition { Name = "番人" });
        skill.States.Add(new StateDefinition { Name = "守護" });
        var equipment = new EquipmentTextData();
        equipment.Equipment.Skills.Add(skill);
        equipment.Equipment.States.Add(new StateDefinition { Name = "霜星" });
        var enemy = new EnemyTextData();
        enemy.Enemy.Skills.Add(skill);
        foreach (object data in new object[] { equipment, enemy, new CharacterProfileData { Definition = skill }, new SiegeWeaponData { Action = skill } })
        {
            string before = JsonUtility.ToJson(data);
            var preview = new DataBlockJsonPreview(data);
            var root = JsonNode.Parse(preview.Main);
            var nested = data == equipment ? root["Equipment"]["Skills"][0] : data == enemy ? root["Enemy"]["Skills"][0] :
                data is CharacterProfileData ? root["Definition"] : root["Action"];
            Assert.That(nested["Skill"]["Flavor"].GetValue<string>(), Is.EqualTo(skill.Skill.Flavor));
            Assert.That(nested.AsObject().ContainsKey("States") || nested.AsObject().ContainsKey("Summons"), Is.False);
            Assert.That((string)JsonNode.Parse(preview.Summons)[0]["Name"], Is.EqualTo("番人"));
            var states = JsonNode.Parse(preview.States).AsArray();
            Assert.That(states.Select(s => (string)s["Name"]), Does.Contain("守護"));
            if (data == equipment)
            {
                Assert.That(root["Equipment"].AsObject().ContainsKey("States"), Is.False);
                Assert.That(states.Select(s => (string)s["Name"]), Does.Contain("霜星"));
            }
            Assert.That(JsonUtility.ToJson(data), Is.EqualTo(before));
        }
    }

    [Test]
    public void CsvPublishesThreeFilesAndPreservesConflictingDefinitionsWithSources()
    {
        string Cell(string name, string factor) =>
            "\"《" + name + "》\n【アクティブ効果】自身は《守護》状態になる。\n守護効果〈スタンス〉：自身の被ダメージは" + factor + "倍される。\"";
        string csv = string.Join(",", Cell("甲", "0.5"), Cell("乙", "0.5"), Cell("丙", "0.8"));
        var cells = RulebookCsv.Read(new StringReader(csv)).ToArray();
        Assert.That(cells.All(c => c.Error == null), Is.True);
        foreach (var cell in cells)
            ((SkillTextData)cell.Data).Summons.Add(new SummonedEntityDefinition { Name = "番人" });
        string parent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        try
        {
            var export = new DataBlockJsonExport();
            string folder = RulebookCsvWindow.ExportFiles(parent, "検証", cells, export);
            JsonArray Read(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(folder, name + ".json"))).AsArray();
            var main = Read("data");
            Assert.That(main.Count, Is.EqualTo(3));
            Assert.That(main.All(c => c["data"].AsObject().Count == 1 && c["data"]["Skill"] != null), Is.True);
            var summons = Read("summons");
            Assert.That(summons.Count, Is.EqualTo(1));
            Assert.That(summons[0].AsObject().ContainsKey("Sources"), Is.False);
            var states = Read("states");
            Assert.That(states.Count, Is.EqualTo(2));
            Assert.That(states.Select(s => (string)s["Name"]), Is.EqualTo(new[] { "守護", "守護" }));
            Assert.That(states[0]["Sources"].AsArray().Select(s => (string)s["Name"]), Is.EqualTo(new[] { "甲", "乙" }));
            Assert.That((int)states[1]["Sources"][0]["Column"], Is.EqualTo(3));
            Assert.That(export.States.Conflicts, Is.EqualTo(new[] { "守護" }));
            Assert.That(RulebookCsvWindow.ExportFiles(parent, "検証", cells, new DataBlockJsonExport()), Is.Not.EqualTo(folder));

            IEnumerable<RulebookCell> Interrupted()
            {
                yield return cells[0];
                throw new IOException("読込が中断");
            }
            Assert.Throws<IOException>(() => RulebookCsvWindow.ExportFiles(parent, "中断", Interrupted(), new DataBlockJsonExport()));
            Assert.That(Directory.GetDirectories(parent).Length, Is.EqualTo(2));
        }
        finally { Directory.Delete(parent, true); }
    }

    [Test]
    public void BulletStateRuleStaysInTheStateBeforeTheNextChoice()
    {
        const string source = "《試験》\n【説明】下記の効果を一つ選んで実行する。\n●展開\n"
            + "【アクティブ効果】自身は《守護》状態になる。\n守護効果〈顕現〉：\n"
            + "●このスタックは自身が〈攻撃〉を受けた時その威力分減少する。\n●次の選択\n【発動ロール】自動成功";
        var skill = SkillTextConverter.Parse(source);
        Assert.That(skill.Skill.Choices.Select(c => c.Name), Is.EqualTo(new[] { "展開", "次の選択" }));
        Assert.That(skill.States.Single().Effects.Count + skill.States.Single().Overrides.Count, Is.EqualTo(1));
    }
}
