using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnityEngine;

// Project Unity's serialized shape for export without changing the editable data.
internal sealed class DataBlockJsonExport
{
    private static readonly JsonSerializerOptions Compact = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly JsonSerializerOptions Indented = new JsonSerializerOptions(Compact) { WriteIndented = true };

    internal static string Format(JsonNode value, bool pretty = false) => value.ToJsonString(pretty ? Indented : Compact);

    internal sealed class Definitions
    {
        private sealed class Variant
        {
            internal JsonObject Definition;
            internal readonly List<(string Name, int Row, int Column)> Sources = new List<(string, int, int)>();
        }

        private readonly Dictionary<string, List<Variant>> byName = new Dictionary<string, List<Variant>>(StringComparer.Ordinal);
        private readonly List<List<Variant>> inSourceOrder = new List<List<Variant>>();

        internal void Extract(JsonObject owner, string field, string source, int row, int column)
        {
            var definitions = owner[field]?.AsArray();
            owner.Remove(field);
            if (definitions == null) return;
            foreach (var item in definitions)
            {
                var definition = item?.AsObject();
                string name = (string)definition?["Name"];
                if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException($"《{source}》の{field}に名称のない定義があります。");
                if (!byName.TryGetValue(name, out var variants))
                {
                    variants = new List<Variant>();
                    byName.Add(name, variants);
                    inSourceOrder.Add(variants);
                }

                var variant = variants.Find(v => JsonNode.DeepEquals(v.Definition, definition));
                if (variant == null)
                {
                    variant = new Variant { Definition = definition };
                    variants.Add(variant);
                }
                variant.Sources.Add((source, row, column));
            }
        }

        internal JsonArray ToArray()
        {
            var items = new JsonArray();
            foreach (var variants in inSourceOrder)
            foreach (var variant in variants)
            {
                var definition = variant.Definition.DeepClone().AsObject();
                if (variants.Count > 1)
                {
                    var sources = new JsonArray();
                    foreach (var source in variant.Sources.Distinct())
                    {
                        var entry = new JsonObject { ["Name"] = source.Name };
                        if (source.Row > 0) { entry["Row"] = source.Row; entry["Column"] = source.Column; }
                        sources.Add(entry);
                    }
                    definition.Add("Sources", sources);
                }
                items.Add(definition);
            }
            return items;
        }

        internal IEnumerable<string> Conflicts => byName.Where(p => p.Value.Count > 1).Select(p => p.Key);
    }

    internal readonly Definitions Summons = new Definitions();
    internal readonly Definitions States = new Definitions();

    internal JsonObject Main(object data, int row = 0, int column = 0)
    {
        if (data == null) throw new InvalidOperationException("出力するデータがありません。");
        var root = JsonNode.Parse(JsonUtility.ToJson(data)).AsObject();
        void SplitSkill(JsonObject skill)
        {
            string source = (string)skill["Skill"]?["Name"];
            Summons.Extract(skill, "Summons", source, row, column);
            States.Extract(skill, "States", source, row, column);
        }
        switch (data)
        {
            case SkillTextData _: SplitSkill(root); break;
            case CharacterProfileData _: SplitSkill(root["Definition"].AsObject()); break;
            case SiegeWeaponData _: SplitSkill(root["Action"].AsObject()); break;
            case EquipmentTextData _:
                var equipment = root["Equipment"].AsObject();
                States.Extract(equipment, "States", (string)equipment["Name"], row, column);
                foreach (var skill in equipment["Skills"].AsArray()) SplitSkill(skill.AsObject());
                break;
            case EnemyTextData _:
                foreach (var skill in root["Enemy"]["Skills"].AsArray()) SplitSkill(skill.AsObject());
                break;
            default: throw new InvalidOperationException("未対応のJSON出力形式です。");
        }
        return root;
    }

    internal void WriteCsv(TextWriter main, TextWriter summons, TextWriter states, IEnumerable<RulebookCell> entries)
    {
        main.Write("[");
        bool first = true;
        foreach (var cell in entries.Where(c => c.Data != null))
        {
            string kind = cell.Data switch
            {
                SkillTextData _ => "skill",
                EquipmentTextData _ => "equipment",
                CharacterProfileData _ => "profile",
                SiegeWeaponData _ => "siegeWeapon",
                _ => throw new InvalidOperationException("未対応のCSV出力形式です。")
            };
            var record = new JsonObject
            {
                ["row"] = cell.Row, ["column"] = cell.Column, ["kind"] = kind,
                ["data"] = Main(cell.Data, cell.Row, cell.Column)
            };
            if (!first) main.Write(",");
            main.WriteLine();
            main.Write(Format(record));
            first = false;
        }
        main.Write("\n]");
        summons.Write(Format(Summons.ToArray(), true));
        states.Write(Format(States.ToArray(), true));
    }
}
