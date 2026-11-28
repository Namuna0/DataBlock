using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

internal static partial class AreaActionTextCodec
{
    private const string Ref = @"《([^《》]+)》";
    private const string Nat = @"[0-9]+";
    private const string Token = @"[^《》〈〉、。]+";
    private const string Dice = @"[0-9]+d[0-9]+(?:[+*\-/().0-9]|\[[^\]]+\])*";
    private const string KanjiDigits = "〇一二三四五六七八九";
    private const string RewardPattern = @"^《(?<item>[^》]+)》[×x](?<amount>[1-9][0-9]*)(?:を?獲得)(?<tail>.*)$";

    private static Match Match(string source, string pattern) => DataBlockText.GetRegex(pattern).Match(source);
    private static int Number(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    internal static AreaActionDefinition Parse(string name, IEnumerable<string> lines, List<StateDefinition> states = null)
    {
        var section = new AreaActionDefinition { Name = name ?? "" };
        string currentField = "";
        StateDefinition currentState = null;
        bool flavor = false;
        bool stackTable = false;
        foreach (string source in lines)
        {
            string line = source.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("――", StringComparison.Ordinal)) flavor = true;
            if (flavor)
            {
                section.Entries.Add(new AreaActionEntry { Note = line });
                continue;
            }
            var header = Match(line, @"^【([^】]+)】(.*)$");
            string field = "";
            if (header.Success)
            {
                currentState = null;
                currentField = field = header.Groups[1].Value.Trim();
                line = header.Groups[2].Value.Trim();
            }
            if (states != null && line.Contains("効果") && SkillTextConverter.TryStateHeader(line, out var state, out string body))
            {
                currentState = state;
                stackTable = body == SkillTextConverter.StackTableHeader;
                if (!stackTable && body.Length > 0 && !SkillTextConverter.ReadStateText(state, body))
                    throw new InvalidOperationException("未対応の状態効果です：" + body);
                states.Add(state);
                section.Entries.Add(new AreaActionEntry { Type = AreaActionEntryType.State, Field = field, Subject = state.Name });
                continue;
            }
            if (currentState != null)
            {
                string statement = stackTable ? SkillTextConverter.ReadStackTier(line) ?? line : line;
                if (SkillTextConverter.ReadStateText(currentState, statement)) continue;
            }
            currentState = null;
            if (field.Length == 0 && section.Name.StartsWith("行き先", StringComparison.Ordinal))
            {
                // Only a list made solely from area names admits a missing outer bracket.
                if (Match(line, @"^(?:《[^《》、,]+》\s*[,、]\s*)*《[^《》、,。]+$").Success) line += "》";
                if (Match(line, @"^[^《》、,:：。]+》(?:\s*[,、]\s*《[^《》]+》)*$").Success) line = "《" + line;
            }
            string[] sentences = DataBlockText.GetRegex(@"(?<=。)(?=[^。])").Split(line);
            if (sentences.Length > 1)
            {
                var parts = sentences.Select((s, i) => ParseEntry(i == 0 ? field : "", currentField, s)).ToList();
                if (parts.All(p => p.Type == AreaActionEntryType.AreaRule))
                {
                    section.Entries.AddRange(parts);
                    continue;
                }
            }
            section.Entries.Add(ParseEntry(field, currentField, line));
        }
        return section;
    }

    private static AreaActionEntry ParseEntry(string field, string currentField, string line)
    {
        var e = new AreaActionEntry { Field = field };
        Match m;
        if (line.StartsWith("○", StringComparison.Ordinal))
        {
            e.Type = AreaActionEntryType.Subheading;
            e.Subject = line.Substring(1).Trim();
            return e;
        }
        if ((m = Match(line, @"^〈([^〈〉]+)〉$")).Success)
        {
            e.Type = AreaActionEntryType.Categories;
            e.Categories = m.Groups[1].Value.Split(',').Select(x => x.Trim()).ToList();
            return e;
        }
        if (field == "治安レベル" && (m = Match(line, @"^([^（(]+)[（(]([+-]?[0-9]+)[）)]$")).Success)
        {
            e.Type = AreaActionEntryType.Security;
            e.Subject = m.Groups[1].Value;
            e.Value = Number(m.Groups[2].Value);
            return e;
        }
        if (field == "消費リソース" && line.Length > 0)
        {
            var skill = new SkillBody();
            SkillTextConverter.ReadCosts(skill, line);
            e.Type = AreaActionEntryType.Costs;
            e.Costs = skill.Costs;
            return e;
        }
        if (field.Length > 0 && (field.EndsWith("ロール", StringComparison.Ordinal) || field.EndsWith("ドロップ", StringComparison.Ordinal)) &&
            (m = Match(line, @"^(?:(?<count>[1-9][0-9]*)回(?:連続)?[：:]\s*)?(?<roll>" + Dice + @")(?:\s*目標値(?<target>.+))?$")).Success)
        {
            e.Type = AreaActionEntryType.Roll;
            e.Roll = ReadRoll(m);
            return e;
        }

        var bullet = Match(line, @"^(?:-\s+|・)(.*)$");
        string text = bullet.Success ? bullet.Groups[1].Value : line;
        if ((m = Match(text, @"^(?<min>[0-9]+)(?:[～〜~](?<max>[0-9]+))?[：:]\s*(?<outcome>.+)$")).Success)
        {
            e.Type = AreaActionEntryType.Reward;
            e.Reward = ReadReward(m.Groups["outcome"].Value, out e.Note);
            if (!string.IsNullOrEmpty(e.Reward.ResultName) && TryRule(e.Reward.ResultName, out e.Rule, out e.Parameters))
                e.Reward.ResultName = "";
            e.Reward.Minimum = Number(m.Groups["min"].Value);
            e.Reward.Maximum = m.Groups["max"].Success ? Number(m.Groups["max"].Value) : e.Reward.Minimum;
            return e;
        }
        if (Match(text, RewardPattern).Success)
        {
            e.Type = AreaActionEntryType.Reward;
            e.Reward = ReadReward(text, out e.Note);
            return e;
        }
        m = Match(text, @"^(?:(?<name>《[^》]+》)(?:〈(?<tags>[^〉]+)〉)?\s*[：:]\s*)?(?<price>[0-9]+|[一二三四五六七八九])(?<currency>ジルダ|[^《》、,：:。\\s0-9]+銭)(?:,\s*(?<note>.+))?$");
        if (m.Success)
        {
            e.Type = AreaActionEntryType.Price;
            e.Subject = NameOf(m.Groups["name"].Value);
            if (m.Groups["tags"].Success) e.Categories = m.Groups["tags"].Value.Split(',').Select(x => x.Trim()).ToList();
            string price = m.Groups["price"].Value;
            int digit = price.Length == 1 ? KanjiDigits.IndexOf(price[0]) : -1;
            e.Value = digit >= 0 ? digit : Number(price);
            e.Currency = m.Groups["currency"].Value;
            e.Note = m.Groups["note"].Value;
            ReadExtraRule(e);
            return e;
        }
        if ((m = Match(text, @"^(?:《[^《》]+》[\s,、]*)+$")).Success)
        {
            e.Type = AreaActionEntryType.References;
            e.Names = DataBlockText.GetRegex(Ref).Matches(text).Cast<Match>().Select(x => x.Groups[1].Value).ToList();
            return e;
        }

        m = Match(text, @"^(?<label>《[^》]+》|[^：:]+)[：:]\s*(?<args>.+)$");
        if (m.Success)
        {
            e.Subject = m.Groups["label"].Value.Trim();
            text = m.Groups["args"].Value.Trim();
            if (ReadCheck(e, text)) return e;
        }

        if (field == "宣言条件")
        {
            var conditions = new ConditionSet();
            try
            {
                SkillTextConverter.AddConditions(conditions, text);
                if (conditions.And.Count + conditions.Or.Count > 0)
                {
                    e.Type = AreaActionEntryType.Conditions;
                    e.Conditions = conditions;
                    return e;
                }
            }
            catch (InvalidOperationException) { /* Area travel has additional declaration conditions. */ }
        }
        m = Match(text, @"^([A-Z]+(?:,\s*[A-Z]+)*)全回復$");
        if (m.Success)
        {
            e.Type = AreaActionEntryType.Effects;
            foreach (string resource in m.Groups[1].Value.Split(',').Select(x => x.Trim()))
                e.Effects.Add(CommonEffectTextCodec.Effect(EffectContentType.RestoreResource, "Self", resource,
                    "[" + resource + "最大値]*100/100"));
            return e;
        }
        string canonical = DataBlockText.GetRegex(@"^([^。]+による行為判定)が([+-][0-9]+される。?)$").Replace(text, "$1の達成値は$2");
        canonical = canonical == "進行ロール達成後、" ? "進行ロール達成後" : canonical;
        if (TryRule(canonical, out e.Rule, out e.Parameters))
        {
            e.Type = AreaActionEntryType.AreaRule;
            return e;
        }
        canonical = DataBlockText.GetRegex(@"(〈[^〉]+〉による発動ロール)×([0-9.]+)倍$").Replace(text, "$1の達成値が×$2倍される。");
        if ((currentField.EndsWith("効果", StringComparison.Ordinal) || field.Length == 0) &&
            text.Length > 0 && CommonEffectTextCodec.TryRead(canonical, out var effects))
        {
            e.Type = AreaActionEntryType.Effects;
            e.Effects = effects;
            return e;
        }
        e.Note = text;
        return e;
    }

    private static string NameOf(string text) => text.Length == 0 ? "" : text.Substring(1, text.Length - 2);

    private static DiceRollDefinition ReadRoll(Match m) => new DiceRollDefinition
    {
        Count = m.Groups["count"].Success ? Number(m.Groups["count"].Value) : 1,
        Formula = m.Groups["roll"].Value,
        Target = m.Groups["target"].Value
    };

    private static bool ReadCheck(AreaActionEntry entry, string text)
    {
        text = DataBlockText.GetRegex(@"^こ(?=[0-9]+d[0-9]+[^、。]*目標値[0-9]+)").Replace(text, "");
        var m = Match(text, @"^(?:(?<condition>.*?[、,])\s*)?(?:(?<count>[1-9][0-9]*)回連続[：:\s]*)?(?<roll>《[^》]+》|〈[^〉]+〉|" +
            Dice + @")\s*(?:(?<count>[1-9][0-9]*)回連続[：:]\s*)?目標値(?<target>[0-9]+)(?:を(?:(?<all>全て)|(?<needed>[0-9]+)回(?<minimum>以上)?))?(?:達成(?:後)?)?(?:[で、]\s*(?<result>.*))?$");
        if (!m.Success) return false;
        entry.Type = AreaActionEntryType.Check;
        entry.Roll = ReadRoll(m);
        entry.RequiredSuccesses = m.Groups["all"].Success ? -1 : m.Groups["needed"].Success ? Number(m.Groups["needed"].Value) : 0;
        entry.MinimumSuccesses = m.Groups["minimum"].Success;
        string condition = m.Groups["condition"].Value.TrimEnd('、', ',');
        if (condition.Length > 0 && !TryRule(condition, out entry.Requirement, out entry.RequirementParameters))
        {
            entry.Requirement = "Description";
            entry.RequirementParameters.Add(condition);
        }
        string result = m.Groups["result"].Value;
        if (Match(result, RewardPattern).Success) entry.Reward = ReadReward(result, out entry.Note);
        else entry.Note = result;
        ReadExtraRule(entry);
        return true;
    }

    private static void ReadExtraRule(AreaActionEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.Note) && TryRule(entry.Note, out entry.Rule, out entry.Parameters))
            entry.Note = "";
    }

    private static EnemyDropDefinition ReadReward(string text, out string note)
    {
        var m = Match(text, RewardPattern);
        note = m.Success ? m.Groups["tail"].Value.TrimStart(',', '、', ' ') : "";
        return m.Success
            ? new EnemyDropDefinition { ItemName = m.Groups["item"].Value, Amount = Number(m.Groups["amount"].Value) }
            : new EnemyDropDefinition { ResultName = text };
    }

    internal static IEnumerable<string> Build(AreaActionDefinition section, IReadOnlyList<StateDefinition> states = null)
    {
        foreach (var e in section.Entries)
        {
            string header = string.IsNullOrEmpty(e.Field) ? "" : "【" + e.Field + "】";
            string subject = string.IsNullOrEmpty(e.Subject) ? "" : e.Subject + "：";
            string body;
            switch (e.Type)
            {
                case AreaActionEntryType.Subheading: body = "○" + e.Subject; break;
                case AreaActionEntryType.Categories: body = "〈" + string.Join(", ", e.Categories) + "〉"; break;
                case AreaActionEntryType.Security:
                    body = e.Subject + "（" + e.Value + "）";
                    break;
                case AreaActionEntryType.References:
                    body = string.Join(", ", e.Names.Select(x => "《" + x + "》"));
                    break;
                case AreaActionEntryType.Price:
                    string product = string.IsNullOrEmpty(e.Subject) ? "" : "《" + e.Subject + "》" +
                        (e.Categories.Count == 0 ? "" : "〈" + string.Join(", ", e.Categories) + "〉") + "：";
                    string price = e.Currency.EndsWith("銭", StringComparison.Ordinal) && e.Value > 0 && e.Value < KanjiDigits.Length
                        ? KanjiDigits[e.Value].ToString() : e.Value.ToString(CultureInfo.InvariantCulture);
                    body = product + price + e.Currency + Extra(Note(e));
                    break;
                case AreaActionEntryType.Check:
                    string outcome = RewardText(e.Reward);
                    string needed = e.RequiredSuccesses < 0 ? "を全て" : e.RequiredSuccesses == 0 ? "" :
                        "を" + e.RequiredSuccesses + "回" + (e.MinimumSuccesses ? "以上" : "");
                    string condition = e.Requirement == "Description" ? e.RequirementParameters.Single() :
                        string.IsNullOrEmpty(e.Requirement) ? "" : RuleText(e.Requirement, e.RequirementParameters);
                    string extra = Note(e);
                    body = "- " + subject + (condition.Length == 0 ? "" : condition + "、") +
                        RollText(e.Roll) + needed + "達成" + (outcome.Length == 0 ? (string.IsNullOrEmpty(extra) ? "" : "で" + extra) : "で" + outcome + Extra(extra));
                    break;
                case AreaActionEntryType.Roll:
                    body = RollText(e.Roll);
                    break;
                case AreaActionEntryType.Reward:
                    var r = e.Reward;
                    string range = r.Minimum == 0 && r.Maximum == 0 ? "" : r.Minimum +
                        (r.Maximum != r.Minimum ? "～" + r.Maximum : "") + "：";
                    body = "- " + range + (string.IsNullOrEmpty(e.Rule) ? RewardText(r) : RuleText(e.Rule, e.Parameters)) + Extra(e.Note);
                    break;
                case AreaActionEntryType.Conditions:
                    var sb = new StringBuilder();
                    SkillTextConverter.WriteConditions(sb, e.Field, e.Conditions, true);
                    yield return sb.ToString().TrimEnd();
                    continue;
                case AreaActionEntryType.Effects:
                    bool fullRecovery = e.Effects.Count > 0 && e.Effects.All(x => x.Type == EffectContentType.RestoreResource &&
                        x.Parameters.Count == 3 && x.Parameters[0] == "Self" && x.Parameters[2] == "[" + x.Parameters[1] + "最大値]*100/100");
                    body = subject + (fullRecovery ? string.Join(", ", e.Effects.Select(x => x.Parameters[1])) + "全回復" :
                        CommonEffectTextCodec.WriteGroup(e.Effects) ?? string.Join("、", e.Effects.Select(CommonEffectTextCodec.Write)));
                    break;
                case AreaActionEntryType.Costs:
                    body = string.Join(", ", e.Costs.Select(SkillTextConverter.CostText));
                    break;
                case AreaActionEntryType.AreaRule:
                    body = subject + RuleText(e.Rule, e.Parameters);
                    break;
                case AreaActionEntryType.State:
                    var state = states?.FirstOrDefault(s => s.Name == e.Subject)
                        ?? throw new InvalidOperationException("状態定義が見つかりません：" + e.Subject);
                    yield return header + e.Subject + "効果" + (state.Categories.Count == 0 ? "" : "〈" + string.Join(", ", state.Categories) + "〉") + "：";
                    foreach (string line in SkillTextConverter.StateTexts(state)) yield return line;
                    continue;
                case AreaActionEntryType.Description:
                    body = subject + e.Note;
                    break;
                default:
                    throw new InvalidOperationException("未対応のエリア項目です：" + e.Type);
            }
            yield return header + body;
        }
    }

    private static string Note(AreaActionEntry e) => string.IsNullOrEmpty(e.Rule) ? e.Note : RuleText(e.Rule, e.Parameters);

    private static string Extra(string note) => string.IsNullOrEmpty(note) ? "" : ", " + note;

    private static string RewardText(EnemyDropDefinition reward) => reward == null ? "" :
        string.IsNullOrEmpty(reward.ItemName) ? reward.ResultName : "《" + reward.ItemName + "》×" + reward.Amount + "を獲得";

    private static string RollText(DiceRollDefinition roll)
    {
        if (roll == null || roll.Count < 1 || string.IsNullOrWhiteSpace(roll.Formula))
            throw new InvalidOperationException("エリアの判定には回数と式が必要です。");
        return (roll.Count == 1 ? "" : roll.Count + "回連続：") + roll.Formula +
            (string.IsNullOrEmpty(roll.Target) ? "" : " 目標値" + roll.Target);
    }
}
