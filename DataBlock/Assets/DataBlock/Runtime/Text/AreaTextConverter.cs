using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static class AreaTextConverter
{
    private const string Separator = "――――――――――――――――";
    private static readonly Regex Title = DataBlockText.GetRegex(@"^(?:(?<region>[^：《》:\r\n]+?)\s*[：:]\s*)?《(?<name>[^《》\r\n]+)》$");
    private static readonly Regex CategoryLine = DataBlockText.GetRegex(@"^(?:〈[^〈〉]+〉)+$");
    private static readonly Regex CategoryGroup = DataBlockText.GetRegex(@"〈([^〉]+)〉");

    // CSV側で他種の《名前》と区別するため、カテゴリー行まで検査する。
    public static bool TryGetHeader(string[] lines, out string name)
    {
        name = "";
        if (lines == null) return false;
        int start = StartOfBody(lines);
        return ReadHeader(lines, ref start, out _, out name, out _);
    }

    private static int StartOfBody(string[] lines)
    {
        int index = 0;
        while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index])) index++;
        if (index < lines.Length && DataBlockText.OpeningFence(DataBlockText.NormalizeFenceLine(lines[index])).Success) index++;
        return index;
    }

    public static bool HasEncounterRoll(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        string[] lines = DataBlockText.NormalizeBreaks(source).Split('\n');
        if (!AreaRollTextCodec.HasRoll(lines)) return false;
        int index = StartOfBody(lines);
        if (!ReadHeader(lines, ref index, out _, out _, out _)) return false;
        return ReadSections(lines, index).Any(s => s.IsEncounter && AreaRollTextCodec.HasRoll(s.Body));
    }

    public static AreaTextData Parse(string source)
    {
        List<string> blocks = Blocks(source);
        if (blocks.Count != 1) throw new InvalidOperationException("エリアのシリアライズセットは1件ずつ行ってください。");
        AreaTextData data = ParseBlock(blocks[0]);
        Build(data);
        return data;
    }

    public static string Normalize(string source)
    {
        return string.Join("\n\n", Blocks(source).Select(block => Build(ParseBlock(block))));
    }

    public static string Build(AreaTextData data)
    {
        AreaDefinition area = data?.Area;
        if (area == null) throw new InvalidOperationException("エリアデータがありません。");
        string title = (string.IsNullOrWhiteSpace(area.Region) ? "" : area.Region.Trim() + "：") + "《" + area.Name + "》";
        if (area.Categories == null || area.Categories.Any(x => string.IsNullOrWhiteSpace(x) || x.IndexOfAny("〈〉,，、\n\r".ToCharArray()) >= 0))
            throw new InvalidOperationException("エリアカテゴリーが不正です。");
        string categories = "〈" + string.Join(", ", area.Categories) + "〉";
        int index = 0;
        if (!ReadHeader(new[] { title, categories }, ref index, out var region, out var name, out var parsedCategories) ||
            name != area.Name || region != (area.Region ?? "") || !parsedCategories.SequenceEqual(area.Categories))
            throw new InvalidOperationException("エリア名・地域名・カテゴリーを確認してください。");

        var parts = new List<string> { title + "\n" + categories };
        var events = ValidateEventReferences(data);
        bool hasRoll = false;
        foreach (var section in area.Sections)
        {
            if (section?.Encounters == null || section.Actions == null) throw new InvalidOperationException("エリアの区画が不正です。");
            if (!string.IsNullOrEmpty(section.Facility))
            {
                string marker = string.IsNullOrEmpty(section.FacilityMarker) ? "✥" : section.FacilityMarker;
                if ((marker != "✥" && marker != "◆") || section.Facility.IndexOfAny("\r\n".ToCharArray()) >= 0)
                    throw new InvalidOperationException("施設の見出しが不正です。");
                parts.Add(marker + section.Facility);
            }
            foreach (var table in section.Encounters)
            {
                parts.Add(AreaRollTextCodec.Build(table, data.States, events));
                hasRoll |= AreaRollTextCodec.HasRoll(table);
            }
            foreach (var action in section.Actions)
            {
                if (action == null) throw new InvalidOperationException("エリアの行動が空です。");
                var lines = new List<string>();
                if (!string.IsNullOrEmpty(action.Name)) lines.Add("●" + action.Name);
                lines.AddRange(AreaActionTextCodec.Build(action, data.States));
                if (lines.Count > 0) parts.Add(string.Join("\n", lines));
            }
        }
        if (!hasRoll) throw new InvalidOperationException("エンカウントの出目表を持つエリアを指定してください。");
        string body = string.Join("\n\n", parts);
        if (!string.IsNullOrWhiteSpace(area.Flavor))
        {
            if (LastEventWithoutFlavor(data) != null) body += "\n" + Separator;
            body += "\n" + Separator + "\n" + area.Flavor.Trim();
        }
        return "```\n" + body.Replace("\r\n", "\n").Replace('\r', '\n') + "\n```";
    }

    public static IEnumerable<string> Warnings(AreaTextData data)
    {
        if (data?.Area?.Sections == null) yield break;
        var events = IndexEvents(data.Events, false);
        foreach (var section in data.Area.Sections)
        {
            if (section?.Encounters == null) continue;
            foreach (var table in section.Encounters)
            {
                if (table == null) continue;
                string name = string.IsNullOrWhiteSpace(table.Name) ? "進行ロール" : table.Name;
                foreach (var issue in AreaRollTextCodec.Warnings(table, events)) yield return name + "：" + issue;
            }
        }
    }

    public static IReadOnlyDictionary<int, AreaEncounterEvent> ValidateEventReferences(AreaTextData data)
    {
        if (data?.Area?.Sections == null) throw new InvalidOperationException("エリア本文がありません。");
        var events = IndexEvents(data.Events, true);
        var usedEvents = new HashSet<int>();
        foreach (var section in data.Area.Sections)
        {
            if (section?.Encounters == null) throw new InvalidOperationException("エリアの区画が不正です。");
            foreach (var table in section.Encounters)
            {
                if (table?.Entries == null) throw new InvalidOperationException("エンカウント表が空です。");
                foreach (var row in table.Entries)
                {
                    if (row == null) throw new InvalidOperationException("エンカウント表の行が空です。");
                    if (row.EventId == 0) continue;
                    if (!events.TryGetValue(row.EventId, out var definition)) throw new InvalidOperationException("イベントID " + row.EventId + " に対応する定義がありません。");
                    if (row.Result?.Type != AreaEncounterOutcome.Event || string.IsNullOrWhiteSpace(row.Result.Value) ||
                        row.Result.Value != definition.Action?.Skill?.Name)
                        throw new InvalidOperationException("イベントID " + row.EventId + " の出目の種類・名称とスキル名を確認してください。");
                    if (!usedEvents.Add(row.EventId)) throw new InvalidOperationException("イベントID " + row.EventId + " が複数の行に割り当てられています。");
                }
            }
        }
        if (usedEvents.Count != events.Count)
            throw new InvalidOperationException("出目表から参照されていないイベントIDがあります：" + string.Join(", ", events.Keys.Except(usedEvents)));
        return events;
    }

    private static Dictionary<int, AreaEncounterEvent> IndexEvents(List<AreaEncounterEvent> definitions, bool validate)
    {
        var events = new Dictionary<int, AreaEncounterEvent>();
        if (definitions == null) return events;
        foreach (var definition in definitions)
        {
            if (definition != null && definition.Id > 0 && !events.ContainsKey(definition.Id)) events.Add(definition.Id, definition);
            else if (validate) throw new InvalidOperationException("イベントIDには重複しない正の整数を指定してください。");
        }
        return events;
    }

    private static AreaTextData ParseBlock(string source)
    {
        string[] lines = source.Split('\n');
        int index = 0;
        var data = new AreaTextData();
        AreaDefinition area = data.Area;
        if (!ReadHeader(lines, ref index, out area.Region, out area.Name, out area.Categories))
            throw new InvalidOperationException("《エリア名》と〈戦闘エリア〉などのエリアカテゴリーを指定してください。");

        List<SourceSection> sections = ReadSections(lines, index).ToList();
        if (!sections.Any(s => s.IsEncounter && AreaRollTextCodec.HasRoll(s.Body)))
            throw new InvalidOperationException("エンカウントの出目表を持つエリアを指定してください。");
        var lastBody = sections[sections.Count - 1].Body;
        int divider = lastBody.FindLastIndex(IsSeparator);
        bool emptyEventFlavor = divider > 0 && IsSeparator(lastBody.Take(divider).LastOrDefault(s => !string.IsNullOrWhiteSpace(s)));
        if (divider >= 0 && !lastBody.Skip(divider + 1).Any(s => s.TrimStart().StartsWith("【", StringComparison.Ordinal) || s.TrimStart().StartsWith("- ", StringComparison.Ordinal)))
        {
            area.Flavor = string.Join("\n", lastBody.Skip(divider + 1)).Trim();
            lastBody.RemoveRange(divider, lastBody.Count - divider);
        }
        AreaSection group = null;
        foreach (var section in sections)
        {
            if (group == null || section.FacilityMarker.Length > 0 || (section.IsEncounter && group.Actions.Count > 0))
            {
                group = new AreaSection { Facility = section.Facility, FacilityMarker = section.FacilityMarker };
                area.Sections.Add(group);
            }
            if (section.IsEncounter)
                group.Encounters.Add(AreaRollTextCodec.Parse(section.Name, section.Body, data.States, data.Events));
            else if (section.Name.Length > 0 || section.Body.Any(s => !string.IsNullOrWhiteSpace(s)))
                group.Actions.Add(AreaActionTextCodec.Parse(section.Name, section.Body, data.States));
        }
        // 終端のイベントに続く最初の区切りはその説明。空の区切りを重ねればエリアの説明になる。
        if (!emptyEventFlavor && area.Flavor.Length > 0 && LastEventWithoutFlavor(data) is AreaEncounterEvent lastEvent)
        {
            lastEvent.Flavor = area.Flavor;
            area.Flavor = "";
        }
        return data;
    }

    private static AreaEncounterEvent LastEventWithoutFlavor(AreaTextData data)
    {
        var tail = data.Area.Sections.LastOrDefault();
        if (tail == null || tail.Actions.Count > 0) return null;
        var row = tail.Encounters.LastOrDefault()?.Entries.LastOrDefault();
        return row == null || row.EventId == 0 ? null :
            data.Events?.Find(e => e.Id == row.EventId && string.IsNullOrWhiteSpace(e.Flavor));
    }

    private static bool IsSeparator(string line)
    {
        return line != null && DataBlockText.GetRegex(@"^―{3,}$").IsMatch(line.Trim());
    }

    private static bool ReadHeader(string[] lines, ref int index, out string region, out string name, out List<string> categories)
    {
        region = name = "";
        categories = null;
        while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index])) index++;
        if (index >= lines.Length) return false;
        Match title = Title.Match(lines[index++].Trim().TrimStart('\uFEFF'));
        if (!title.Success) return false;
        while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index])) index++;
        if (index >= lines.Length) return false;
        string line = lines[index++].Trim();
        if (!CategoryLine.IsMatch(line)) return false;
        categories = CategoryGroup.Matches(line).Cast<Match>().SelectMany(m => m.Groups[1].Value.Split(',', '，', '、')).Select(s => s.Trim()).ToList();
        if (!categories.Any(s => s.EndsWith("エリア", StringComparison.Ordinal)) || categories.Any(string.IsNullOrEmpty)) return false;
        region = title.Groups["region"].Value.Trim();
        name = title.Groups["name"].Value.Trim();
        return name.Length > 0;
    }

    private static List<string> Blocks(string source)
    {
        return DataBlockText.ReadBlocks(DataBlockText.NormalizeBreaks(source), "エリア", false, SplitPlain);
    }

    private static List<string> SplitPlain(string source)
    {
        string[] lines = source.Split('\n');
        var blocks = new List<string>();
        int start = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            int probe = i;
            if (!ReadHeader(lines, ref probe, out _, out _, out _)) continue;
            blocks.Add(string.Join("\n", lines, start, i - start));
            start = i;
        }
        blocks.Add(string.Join("\n", lines, start, lines.Length - start));
        return blocks;
    }

    private sealed class SourceSection
    {
        internal string Name = "";
        internal string Facility = "";
        internal string FacilityMarker = "";
        internal bool IsEncounter;
        internal readonly List<string> Body = new List<string>();
        internal bool HasContent => Name.Length > 0 || FacilityMarker.Length > 0 || IsEncounter || Body.Any(s => !string.IsNullOrWhiteSpace(s));
    }

    private static IEnumerable<SourceSection> ReadSections(string[] lines, int index)
    {
        var section = new SourceSection();
        for (; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            bool facility = line.StartsWith("✥", StringComparison.Ordinal) || line.StartsWith("◆", StringComparison.Ordinal);
            bool named = line.StartsWith("●", StringComparison.Ordinal);
            bool bareRoll = !section.IsEncounter && section.Name.Length == 0 && line.StartsWith("【進行ロール】", StringComparison.Ordinal);
            if (facility || named || bareRoll)
            {
                if (section.HasContent)
                    yield return section;
                section = new SourceSection();
                if (bareRoll) section.IsEncounter = true;
                else
                {
                    string label = line.Substring(1).Trim();
                    if (label.Length == 0) throw new InvalidOperationException("エリアの見出し名がありません：" + line);
                    if (facility)
                    {
                        section.FacilityMarker = line.Substring(0, 1);
                        section.Facility = label;
                    }
                    else
                    {
                        section.Name = label;
                        section.IsEncounter = AreaRollTextCodec.IsEncounterHeading(label);
                    }
                    continue;
                }
            }
            section.Body.Add(lines[index]);
        }
        if (section.HasContent)
            yield return section;
    }
}
