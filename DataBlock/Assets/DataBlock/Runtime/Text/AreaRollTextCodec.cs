using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

internal static class AreaRollTextCodec
{
    private const string Dice = @"[1-9][0-9]*d[1-9][0-9]*";
    private static readonly Regex Progress = DataBlockText.GetRegex(@"^(【進行ロール】|進行ロール後[：:])\s*(?:([1-9][0-9]*)回連続[：:]\s*)?(" + Dice + ")$");
    private static readonly Regex Range = DataBlockText.GetRegex(@"^([0-9]+)(?:[～〜~\-]([0-9]+))?\s*(?:[：:]\s*|(?=《))(.*)$");
    private static readonly Regex SubRoll = DataBlockText.GetRegex(@"^(?:その後、|さらに)?ダイスロール(" + Dice + @")を行う。?$");
    private static readonly Regex Enemy = DataBlockText.GetRegex(@"《([^《》]+)》×([0-9]*)");
    private static readonly Regex EnemyGroup = DataBlockText.GetRegex(@"^(?:《[^《》]+》×[0-9]*\s*[,、]?\s*)+(?:とエンカウント)?。?$");
    private const string Separator = "――――――――――――――――";

    internal static bool IsEncounterHeading(string name) => (name ?? "").Contains("エンカウント");
    private static Match Match(string s, string pattern) => DataBlockText.GetRegex(pattern).Match(s);
    private static int Number(string s) => int.Parse(s, CultureInfo.InvariantCulture);
    private static string Line(string s) => (s ?? "").Trim();
    private static string Unbullet(string s) => s.StartsWith("-") ? s.Substring(1).TrimStart() : s;

    internal static bool HasRoll(IList<string> lines) =>
        lines.Any(x => Progress.IsMatch(Unbullet(Line(x)))) && lines.Any(x => Range.IsMatch(Unbullet(Line(x))));

    internal static bool HasRoll(AreaRollTable t) => !string.IsNullOrEmpty(t.Dice) && t.Entries.Any(x => x.Minimum > 0);

    internal static AreaRollTable Parse(string name, IList<string> body, List<StateDefinition> states, List<AreaEncounterEvent> events)
    {
        var t = new AreaRollTable { Name = name };
        var lines = body.Select(Line).Where(x => x.Length > 0).ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            string s = Unbullet(lines[i]);
            var roll = Progress.Match(s);
            if (roll.Success)
            {
                t.AfterProgress = roll.Groups[1].Value.StartsWith("進行ロール後");
                t.Rolls = roll.Groups[2].Success ? Number(roll.Groups[2].Value) : 1;
                t.Dice = roll.Groups[3].Value;
                continue;
            }
            if (s == "【進行ロール】無し") { t.NoProgressRoll = true; continue; }
            if (s.StartsWith("【宣言条件】"))
            {
                t.Requirements.AddRange(AreaActionTextCodec.Parse(name, new[] { s }, states).Entries);
                continue;
            }

            // 複数行で列挙された一組のエネミーを、一つの結果として扱う。
            while (i + 1 < lines.Count && (s.EndsWith("：") || s.EndsWith(",") || s.EndsWith("、")) &&
                   (Enemy.IsMatch(lines[i + 1]) && lines[i + 1].StartsWith("《") || lines[i + 1] == "とエンカウント"))
                s += lines[++i];
            bool eventBody = i + 1 < lines.Count && (IsTags(lines[i + 1]) ||
                (lines[i + 1].StartsWith("【") && Match(s, @"《[^》]+》(?!.*とエンカウント)(?:×[0-9]+|[0-9]+回連続)?$").Success));
            var row = new AreaEncounterRow();
            ReadEntry(s, row, eventBody);
            t.Entries.Add(row);
            if (eventBody)
            {
                var definition = ReadEvent(lines, ref i, row.Result.Value);
                row.EventId = definition.Id = events.Count + 1;
                events.Add(definition);
            }
        }
        return t;
    }

    private static bool IsTags(string line) => Match(line, @"^〈[^〈〉]+〉$").Success;
    private static bool IsSeparator(string line) => Match(line, @"^―{3,}$").Success;
    private static bool IsEntry(string line) => line.StartsWith("-") || Range.IsMatch(line);

    private static AreaEncounterEvent ReadEvent(List<string> lines, ref int index, string name)
    {
        var e = new AreaEncounterEvent();
        string tags = IsTags(lines[index + 1]) ? lines[++index] : "";
        var fields = new List<string>();
        while (index + 1 < lines.Count)
        {
            string next = lines[index + 1];
            if (IsEntry(next))
            {
                if (fields.Count > 0 && fields.Last().EndsWith("次の効果を得る。"))
                {
                    while (index + 1 < lines.Count && Range.IsMatch(Unbullet(lines[index + 1])))
                        fields.Add(lines[++index]);
                    continue;
                }
                break;
            }
            if (IsSeparator(next))
            {
                index++;
                var flavor = new List<string>();
                while (index + 1 < lines.Count && !IsEntry(lines[index + 1])) flavor.Add(lines[++index]);
                e.Flavor = string.Join("\n", flavor);
                break;
            }
            index++;
            var roll = SubRoll.Match(next);
            if (!roll.Success) { fields.Add(next); continue; }

            e.FollowUpDice = roll.Groups[1].Value;
            if (fields.Count > 0 && fields.Last().EndsWith("場合、"))
            {
                e.FollowUpCondition = fields.Last();
                fields.RemoveAt(fields.Count - 1);
            }
            while (index + 1 < lines.Count && Range.IsMatch(Unbullet(lines[index + 1])))
            {
                var entry = new AreaRollEntry();
                ReadEntry(Unbullet(lines[++index]), entry, false);
                e.FollowUpEntries.Add(entry);
                // A tagged result begins a new event in the parent table.
                if (index + 1 < lines.Count && IsTags(lines[index + 1]))
                {
                    index--;
                    e.FollowUpEntries.RemoveAt(e.FollowUpEntries.Count - 1);
                    break;
                }
            }
        }
        string skillText = "```\n《" + name + "》\n" + tags + "\n" + string.Join("\n", fields) + "\n```";
        e.Action = SkillTextConverter.Parse(skillText);
        return e;
    }

    private static void ReadEntry(string s, AreaRollEntry row, bool eventBody)
    {
        var range = Range.Match(s);
        if (range.Success)
        {
            row.Minimum = Number(range.Groups[1].Value);
            row.Maximum = range.Groups[2].Success ? Number(range.Groups[2].Value) : row.Minimum;
            s = range.Groups[3].Value;
        }
        else
        {
            var labeled = Match(s, @"^([^：:]+)[：:]\s*(.*)$");
            if (labeled.Success)
            {
                row.Label = labeled.Groups[1].Value;
                s = labeled.Groups[2].Value;
                var step = Match(row.Label, @"^([1-9][0-9]*)回目$");
                if (step.Success) { row.Sequence = Number(step.Groups[1].Value); row.Label = ""; }
            }
        }

        var cost = Match(s, @"^(?:《([^》]+)》|〈([^〉]+)〉)×([1-9][0-9]*)消費[,、]\s*(.*)$");
        if (cost.Success)
        {
            bool category = cost.Groups[2].Success;
            row.Costs.Add(new ResourceCost { Kind = category ? CostKind.ItemCategory : CostKind.ItemName,
                Resource = cost.Groups[category ? 2 : 1].Value, Amount = Number(cost.Groups[3].Value) });
            s = cost.Groups[4].Value;
        }
        var check = Match(s, @"^(.+?)\s*目標値([0-9]+)達成で(.*)$");
        if (check.Success)
        {
            row.Check = check.Groups[1].Value.TrimEnd();
            row.Target = Number(check.Groups[2].Value);
            s = check.Groups[3].Value;
        }
        var failure = Match(s, @"^(.*?)[,、]\s*失敗で(.+)$");
        if (failure.Success && ReadEnemies(failure.Groups[2].Value, row.OnFailure))
            s = failure.Groups[1].Value;

        var r = row.Result;
        if (!eventBody && ReadEnemies(s, r.Enemies)) { r.Type = AreaEncounterOutcome.Encounter; return; }
        var conditional = Match(s, @"^(.+?達成で)(《.+)$");
        if (conditional.Success && ReadEnemies(conditional.Groups[2].Value, r.Enemies))
        { row.Condition = conditional.Groups[1].Value; r.Type = AreaEncounterOutcome.Encounter; return; }
        if (s.TrimEnd('。') == "無し" || s.TrimEnd('。') == "何もなし") { r.Type = AreaEncounterOutcome.Nothing; return; }
        var special = Match(s, @"^この1回分の進行ロールを《([^》]+)》宣言からやり直す。?$");
        if (special.Success) { r.Type = AreaEncounterOutcome.Retry; r.Value = special.Groups[1].Value; return; }
        special = Match(s, @"^称号《([^》]+)》を獲得する。?$");
        if (special.Success) { r.Type = AreaEncounterOutcome.Title; r.Value = special.Groups[1].Value; return; }
        special = Match(s, @"^《([^》]+)》(?:([1-9][0-9]*)回連続|×([1-9][0-9]*))?。?$");
        if (special.Success)
        {
            r.Type = AreaEncounterOutcome.Event; r.Value = special.Groups[1].Value;
            string count = special.Groups[2].Value + special.Groups[3].Value;
            if (count.Length > 0) r.Repetitions = Number(count);
            return;
        }
        r.Type = AreaEncounterOutcome.Rule;
        r.Value = s;
    }

    private static bool ReadEnemies(string s, List<AreaEncounterEnemy> result)
    {
        if (!EnemyGroup.IsMatch(s)) return false;
        foreach (Match m in Enemy.Matches(s)) result.Add(new AreaEncounterEnemy
        { Name = m.Groups[1].Value, Count = m.Groups[2].Length > 0 ? Number(m.Groups[2].Value) : 0 });
        return true;
    }

    internal static string Build(AreaRollTable t, IReadOnlyList<StateDefinition> states, IReadOnlyDictionary<int, AreaEncounterEvent> events)
    {
        var lines = new List<string>();
        if (!string.IsNullOrEmpty(t.Name)) lines.Add("●" + t.Name);
        lines.AddRange(AreaActionTextCodec.Build(new AreaActionDefinition { Name = t.Name, Entries = t.Requirements }, states));
        if (t.NoProgressRoll) lines.Add("【進行ロール】無し");
        if (!string.IsNullOrEmpty(t.Dice))
            lines.Add((t.AfterProgress ? "進行ロール後：" : "【進行ロール】") +
                (t.Rolls > 1 ? t.Rolls + "回連続：" : "") + t.Dice);
        foreach (var row in t.Entries)
        {
            lines.Add(EntryText(row));
            if (row.EventId != 0)
            {
                if (events == null || !events.TryGetValue(row.EventId, out var e) || e == null)
                    throw new InvalidOperationException("エリアイベントの定義が見つかりません：" + row.EventId);
                lines.AddRange(EnemyTextConverter.SkillBodyText(e.Action).Split('\n').Skip(1));
                if (!string.IsNullOrEmpty(e.FollowUpCondition)) lines.Add(e.FollowUpCondition);
                if (!string.IsNullOrEmpty(e.FollowUpDice))
                {
                    lines.Add("さらにダイスロール" + e.FollowUpDice + "を行う。");
                    lines.AddRange(e.FollowUpEntries.Select(EntryText));
                }
                if (!string.IsNullOrEmpty(e.Flavor)) { lines.Add(Separator); lines.Add(e.Flavor); }
            }
        }
        return string.Join("\n", lines);
    }

    private static string EnemyText(List<AreaEncounterEnemy> enemies) => string.Join(", ", enemies.Select(
        x => "《" + x.Name + "》×" + (x.Count == 0 ? "" : x.Count.ToString(CultureInfo.InvariantCulture)))) + "とエンカウント";

    private static string EntryText(AreaRollEntry row)
    {
        string prefix = row.Sequence > 0 ? row.Sequence + "回目：" : "- ";
        if (row.Minimum != 0 || row.Maximum != 0)
            prefix += row.Minimum + (row.Minimum == row.Maximum ? "" : "～" + row.Maximum) + "：";
        if (!string.IsNullOrEmpty(row.Label)) prefix += row.Label + "：";
        foreach (var c in row.Costs)
            prefix += (c.Kind == CostKind.ItemCategory ? "〈" + c.Resource + "〉" : "《" + c.Resource + "》") +
                "×" + c.Amount + "消費, ";
        if (!string.IsNullOrEmpty(row.Check)) prefix += row.Check + " 目標値" + row.Target + "達成で";
        prefix += row.Condition;
        var r = row.Result;
        string value;
        switch (r.Type)
        {
            case AreaEncounterOutcome.Encounter: value = EnemyText(r.Enemies); break;
            case AreaEncounterOutcome.Event: value = "《" + r.Value + "》" + (r.Repetitions > 1 ? r.Repetitions + "回連続" : ""); break;
            case AreaEncounterOutcome.Nothing: value = "無し"; break;
            case AreaEncounterOutcome.Retry: value = "この1回分の進行ロールを《" + r.Value + "》宣言からやり直す。"; break;
            case AreaEncounterOutcome.Title: value = "称号《" + r.Value + "》を獲得する。"; break;
            case AreaEncounterOutcome.Rule: value = r.Value; break;
            default: throw new InvalidOperationException("エンカウント結果の種類が不正です。");
        }
        if (row.OnFailure.Count > 0) value += ", 失敗で" + EnemyText(row.OnFailure);
        return prefix + value;
    }

    internal static IEnumerable<string> Warnings(AreaRollTable table, IReadOnlyDictionary<int, AreaEncounterEvent> events)
    {
        if (table?.Entries == null) yield break;
        foreach (string issue in RangeWarnings(table.Dice, table.Entries)) yield return issue;
        foreach (var row in table.Entries)
        {
            if (row == null || row.EventId == 0 || events == null || !events.TryGetValue(row.EventId, out var e) || e == null) continue;
            foreach (string issue in RangeWarnings(e.FollowUpDice, e.FollowUpEntries))
                yield return "《" + row.Result?.Value + "》 " + issue;
        }
    }

    private static IEnumerable<string> RangeWarnings(string dice, IEnumerable<AreaRollEntry> entries)
    {
        if (entries == null) yield break;
        var ranges = new List<AreaRollEntry>();
        var d = Match(dice ?? "", @"^([1-9][0-9]*)d([1-9][0-9]*)$");
        foreach (var row in entries)
        {
            if (row?.Result == null) { yield return "エンカウント結果が空欄です。"; continue; }
            foreach (var enemy in (row.Result.Enemies ?? Enumerable.Empty<AreaEncounterEnemy>()).Concat(row.OnFailure ?? Enumerable.Empty<AreaEncounterEnemy>()))
                if (enemy != null && enemy.Count < 1) yield return "《" + enemy.Name + "》の個体数が未指定です。";
            if (row.Result.Type == AreaEncounterOutcome.Rule && string.IsNullOrWhiteSpace(row.Result.Value))
                yield return "エンカウント結果が空欄です。";
            if (row.Minimum == 0 && row.Maximum == 0) continue;
            if (row.Minimum > row.Maximum || (d.Success && (row.Minimum < Number(d.Groups[1].Value) ||
                row.Maximum > (long)Number(d.Groups[1].Value) * Number(d.Groups[2].Value))))
                yield return "出目" + row.Minimum + "～" + row.Maximum + "はロールの範囲外です。";
            foreach (var prior in ranges)
            {
                int lo = Math.Max(row.Minimum, prior.Minimum);
                int hi = Math.Min(row.Maximum, prior.Maximum);
                if (lo <= hi) yield return "出目" + lo + (lo == hi ? "" : "～" + hi) + "が重複しています。";
            }
            ranges.Add(row);
        }
    }
}
