using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static SkillTextData ReadDefinitionFields(string source, Func<string, string, bool> field)
    {
        var blocks = InputBlocks(source);
        if (blocks.Count != 1) throw new InvalidOperationException("定義を1件指定してください。");
        var lines = blocks[0].Replace("\r", "").Split('\n');
        var body = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (M(line, @"^―{8,}$").Success) { body.AddRange(lines.Skip(i)); break; }
            if (line.StartsWith("【レアリティ】") && line.Contains("【グレード】"))
            {
                int at = line.IndexOf("【グレード】", StringComparison.Ordinal);
                if (field("レアリティ", line.Substring("【レアリティ】".Length, at - "【レアリティ】".Length))) line = line.Substring(at);
            }
            var m = M(line, @"^【([^】]+)】(.*)$");
            if (m.Success)
            {
                string key = m.Groups[1].Value, value = m.Groups[2].Value.Trim();
                if (key == "種族特性" && value.StartsWith("次の内の特性を必ず") && i + 1 < lines.Length)
                    value += "\n" + lines[++i].Trim();
                if (field(key, value)) continue;
            }
            body.Add(line);
        }
        return Parse(string.Join("\n", body));
    }

    private static string AddDefinitionFields(SkillTextData data, List<string> fields)
    {
        var lines = Build(data).Split('\n').ToList();
        lines.InsertRange(3, fields);
        return string.Join("\n", lines);
    }

    private static List<ProfileBonus> ProfileBonuses(string text, bool percent)
    {
        var values = new List<ProfileBonus>();
        foreach (string word in DataBlockText.GetRegex(@"[\s,、]+").Split(text.Trim()))
        {
            var match = M(word, @"^([^+\-\s,%]+)([+-][0-9]+)" + (percent ? "%" : "") + "$");
            if (!match.Success || !int.TryParse(match.Groups[2].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int amount))
                throw new InvalidOperationException("能力値の補正が不正です：" + word);
            values.Add(new ProfileBonus { Stat = match.Groups[1].Value, Amount = amount });
        }
        Require(values.Select(x => x.Stat).Distinct().Count() == values.Count);
        return values;
    }

    private static List<string> ProfileNames(string value)
    {
        value = value.TrimStart(',', '、', ' ');
        Require(M(value, @"^《[^《》]+》(?:(?:\s*[,、]\s*|および)《[^《》]+》)*$").Success);
        return AllMatches(value, @"《([^《》]+)》").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
    }

    public static CharacterProfileData ParseProfile(string text)
    {
        var data = new CharacterProfileData();
        var seen = new HashSet<string>();
        data.Definition = ReadDefinitionFields(text, (key, value) =>
        {
            switch (key)
            {
                case "ランク": data.Rank = Number(value, 1, key); break;
                case "種族能力値ボーナス": data.AbilityBonuses = ProfileBonuses(value, true); break;
                case "種族属性値ボーナス": data.ElementBonuses = ProfileBonuses(value, true); break;
                case "クラス能力値": data.ClassStats = ProfileBonuses(value, false); break;
                case "種族スキル": data.Skills = ProfileNames(value); break;
                case "選択可能特性":
                    Require(value.EndsWith("選択不可能", StringComparison.Ordinal));
                    data.ProhibitedTraits = ProfileNames(value.Substring(0, value.Length - "選択不可能".Length)); break;
                case "種族特性":
                    var choice = M(value, @"^次の内の特性を必ず([0-9]+)つ選択。（ただし種族によって選択できない特性があります）\n(.+)$");
                    if (choice.Success) data.RequiredTraitChoices = Number(choice.Groups[1].Value, 1, "選択数");
                    data.Traits = ProfileNames(choice.Success ? choice.Groups[2].Value : value); break;
                default: return false;
            }
            Require(seen.Add(key)); return true;
        });
        BuildProfile(data);
        return data;
    }

    public static string BuildProfile(CharacterProfileData data)
    {
        Require(data?.Definition?.Skill?.Categories != null);
        Require(data.AbilityBonuses != null && data.ElementBonuses != null && data.ClassStats != null &&
            data.Skills != null && data.Traits != null && data.ProhibitedTraits != null);
        bool race = data.Definition.Skill.Categories.Contains("種族");
        bool job = data.Definition.Skill.Categories.Any(c => c.EndsWith("クラス", StringComparison.Ordinal));
        Require(race != job && data.RequiredTraitChoices >= 0 && data.RequiredTraitChoices <= data.Traits.Count);
        Require(job ? data.Rank > 0 && data.AbilityBonuses.Count + data.ElementBonuses.Count + data.Skills.Count + data.Traits.Count + data.ProhibitedTraits.Count == 0
                    : data.Rank == 0 && data.ClassStats.Count == 0);
        var fields = new List<string>();
        if (job) fields.Add("【ランク】" + data.Rank);
        void Stats(string label, List<ProfileBonus> list, bool percent)
        {
            if (list.Count == 0) return;
            Require(list.All(x => x != null));
            string body = string.Join(", ", list.Select(x => Need(x.Stat, label) + x.Amount.ToString("+0;-0;+0", CultureInfo.InvariantCulture) + (percent ? "%" : "")));
            ProfileBonuses(body, percent);
            fields.Add("【" + label + "】" + body);
        }
        void Names(string key, List<string> items, string prefix = "", string suffix = "")
        {
            if (items.Count == 0) return;
            string body = string.Join(", ", items.Select(x => "《" + Need(x, key) + "》"));
            Require(ProfileNames(body).SequenceEqual(items));
            fields.Add("【" + key + "】" + prefix + body + suffix);
        }
        Stats("クラス能力値", data.ClassStats, false);
        Stats("種族能力値ボーナス", data.AbilityBonuses, true);
        Stats("種族属性値ボーナス", data.ElementBonuses, true);
        Names("種族特性", data.Traits, data.RequiredTraitChoices == 0 ? "" : "次の内の特性を必ず" + data.RequiredTraitChoices + "つ選択。（ただし種族によって選択できない特性があります）\n");
        Names("種族スキル", data.Skills);
        Names("選択可能特性", data.ProhibitedTraits, "", "選択不可能");
        return AddDefinitionFields(data.Definition, fields);
    }

    public static SiegeWeaponData ParseSiegeWeapon(string text)
    {
        var data = new SiegeWeaponData();
        var seen = new HashSet<string>();
        data.Action = ReadDefinitionFields(text, (key, value) =>
        {
            switch (key)
            {
                case "レアリティ": data.Rarity = Need(value, key); break;
                case "グレード": Require(M(value, "^☆{1,100}$").Success); data.Grade = value.Length; break;
                case "サイズ": data.Size = Need(value, key); break;
                case "必要敷地": data.RequiredSpace = Number(value, 1, key); break;
                default: return false;
            }
            Require(seen.Add(key)); return true;
        });
        BuildSiegeWeapon(data);
        return data;
    }

    public static string BuildSiegeWeapon(SiegeWeaponData data)
    {
        Require(data?.Action?.Skill?.Categories != null && data.Action.Skill.Categories.Contains("兵器"));
        Require(data.Grade > 0 && data.Grade <= 100 && data.RequiredSpace > 0);
        return AddDefinitionFields(data.Action, new List<string> {
            "【レアリティ】" + Need(data.Rarity, "レアリティ"), "【グレード】" + new string('☆', data.Grade),
            "【サイズ】" + Need(data.Size, "サイズ"), "【必要敷地】" + data.RequiredSpace });
    }
}
