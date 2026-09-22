using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

// 通常の効果と上書き効果の文章変換のみ。式の評価・戦闘実行・データ移行は行いません。
public static partial class SkillTextConverter
{
    private const string Description = "下記の効果を一つ選んで実行する。";
    private const string Separator = "――――――――――――――――";
    private const string Bullet = "・";
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly ConcurrentDictionary<RegexCacheKey, Regex> RegexCache = new ConcurrentDictionary<RegexCacheKey, Regex>();
    private static readonly Dictionary<EffectType, string> EffectNames = new Dictionary<EffectType, string>
    {
        { EffectType.Active, "アクティブ効果" },
        { EffectType.Counter, "カウンター効果" },
        { EffectType.Passive, "パッシブ効果" },
        { EffectType.Roleplay, "ロールプレイ効果" },
        { EffectType.Critical, "クリティカル効果" },
        { EffectType.Fumble, "ファンブル効果" },
        { EffectType.Declaration, "宣言効果" },
        { EffectType.SecondSpike, "セカンドスパイク" },
        { EffectType.ThirdSpike, "サードスパイク" }
    };
    private static readonly Dictionary<string, EffectType> EffectTypesByName = EffectNames.ToDictionary(x => x.Value, x => x.Key, StringComparer.Ordinal);
    private static Regex GetRegex(string pattern, RegexOptions options = RegexOptions.None)
    {
        options |= RegexOptions.CultureInvariant;
        return RegexCache.GetOrAdd(new RegexCacheKey(pattern, options), CreateRegex);
    }
    private static Regex CreateRegex(RegexCacheKey key) { return new Regex(key.Pattern, key.Options, RegexTimeout); }
    private static Match M(string text, string pattern) { return GetRegex(pattern).Match(text); }
    private static MatchCollection AllMatches(string text, string pattern) { return GetRegex(pattern).Matches(text); }
    private static string RegexReplace(string text, string pattern, string replacement, RegexOptions options = RegexOptions.None)
    {
        return GetRegex(pattern, options).Replace(text, replacement);
    }
    private static string Need(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException(label + "が空です。");
        if (value.Contains("\n") || value.Contains("\r")) throw new InvalidOperationException(label + "に改行は使用できません。");
        return value.Trim();
    }
    private static string[] Args(List<string> values, int count, string label)
    {
        if (values == null || values.Count != count) throw new InvalidOperationException(label + "のパラメーターは" + count + "個です。");
        return values.Select(x => Need(x, label + "のパラメーター")).ToArray();
    }
    private static int Number(string value, int minimum, string label)
    {
        int result;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) || result < minimum)
            throw new InvalidOperationException(label + "は" + minimum + "以上の整数にしてください。");
        return result;
    }
    private static string Actor(string value)
    {
        switch (value)
        {
            case "Self": return "自身";
            case "Target": return "対象";
            case "Applier": return "付与者";
            default: throw new InvalidOperationException("人物はSelf / Target / Applierで指定してください。");
        }
    }
    private static string ActorKey(string value)
    {
        switch (value)
        {
            case "自身": return "Self";
            case "対象": return "Target";
            case "付与者": return "Applier";
            default: throw new InvalidOperationException("人物を読み取れません：" + value);
        }
    }
    private const string AllWithStatePrefix = "AllWithState:";
    private static bool IsActorTarget(string value) { return value == "Self" || value == "Target" || value == "Applier"; }
    private static string TargetText(string value)
    {
        if (IsActorTarget(value)) return Actor(value);
        if (value == "AllAllies") return "味方キャラクター全員";
        if (value == "AllEnemiesExceptSelf") return "自身を除くすべての敵キャラクター";
        if (value != null && value.StartsWith(AllWithStatePrefix, StringComparison.Ordinal))
            return "《" + Need(value.Substring(AllWithStatePrefix.Length), "対象状態名") + "》状態の全ての対象";
        throw new InvalidOperationException("対象指定が不正です：" + value);
    }
    private static string StateTarget(string stateName) { return AllWithStatePrefix + Need(stateName, "対象状態名"); }
    private static ConditionEntry Condition(ConditionType type, params string[] p) { return new ConditionEntry { Type = type, Parameters = p.ToList() }; }
    private static EffectContent Content(EffectContentType type, params string[] p) { return new EffectContent { Type = type, Parameters = p.ToList() }; }
    private static TriggerDefinition Trigger(TriggerTiming timing, params ConditionEntry[] conditions)
    {
        return new TriggerDefinition { Timing = timing, Conditions = new ConditionSet { And = conditions.ToList() } };
    }
    private static bool Empty(ConditionSet value)
    {
        return value != null && value.And != null && value.Or != null && value.And.Count == 0 && value.Or.Count == 0;
    }
    // カンマは括弧の外側だけを分割。計算式・カテゴリー内のカンマは残します。
    private static List<string> Split(string text, string delimiter)
    {
        var result = new List<string>();
        int depth = 0, start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if ("([〈《（［".IndexOf(c) >= 0) depth++;
            if (")]〉》）］".IndexOf(c) >= 0) depth--;
            if (depth < 0) throw new InvalidOperationException("括弧の対応が不正です：" + text);
            if (depth == 0 && i + delimiter.Length <= text.Length && string.CompareOrdinal(text, i, delimiter, 0, delimiter.Length) == 0)
            {
                string part = text.Substring(start, i - start).Trim();
                if (part.Length > 0) result.Add(part);
                i += delimiter.Length - 1;
                start = i + 1;
            }
        }
        if (depth != 0) throw new InvalidOperationException("括弧が閉じていません：" + text);
        string last = text.Substring(start).Trim();
        if (last.Length > 0) result.Add(last);
        return result;
    }
    private static string Unbullet(string text)
    {
        return RegexReplace(text.Trim(), @"^(?:・|⚫[\uFE0E\uFE0F]?)\s*", "");
    }
    private static List<string> Categories(string text)
    {
        if (!M(text, @"^(?:〈[^〉]+〉)+$").Success) throw new InvalidOperationException("カテゴリーの形式が不正です：" + text);
        return AllMatches(text, @"〈([^〉]+)〉").Cast<Match>().SelectMany(x => x.Groups[1].Value.Split(',')).Select(x => Need(x, "カテゴリー")).ToList();
    }
    private static string CategoryText(List<string> values)
    {
        if (values == null) throw new InvalidOperationException("Categoriesがnullです。");
        return values.Count == 0 ? "" : "〈" + string.Join(", ", values.Select(x => Need(x, "カテゴリー"))) + "〉";
    }

    // 構文統一は複数件の文章も扱います。Dataへのセットは1件だけです。
    public static SkillTextData Parse(string source)
    {
        List<string> blocks = InputBlocks(source);
        if (blocks.Count != 1) throw new InvalidOperationException(blocks.Count + "件のスキルがあります。シリアライズセットは1件ずつ行ってください。構文統一はまとめて実行できます。");
        SkillTextData data = ParseOne(blocks[0]);
        Build(data);
        return data;
    }
    private static List<string> InputBlocks(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new InvalidOperationException("入力テキストが空です。");
        string text = source.Replace("\r\n", "\n").Replace("\r", "\n");
        // 改行＋<br>は1改行。連続した<br>による空行は残します。
        text = RegexReplace(text, @"\n?[ \t]*<br\s*/?>[ \t]*\n?", "\n", RegexOptions.IgnoreCase);
        string[] lines = text.Split('\n');
        bool fenced = lines.Any(x => OpeningFence(NormalizeFenceLine(x)).Success);
        if (!fenced)
        {
            string plain = text.Trim();
            if (plain.Length >= 2 && plain.StartsWith("|") && plain.EndsWith("|")) plain = plain.Substring(1, plain.Length - 2).Trim();
            return new List<string> { plain };
        }
        var blocks = new List<string>();
        var body = new List<string>();
        bool inside = false, quoted = false;
        int openingFenceLength = 0;
        foreach (string raw in lines)
        {
            string line = NormalizeFenceLine(raw);
            if (!inside)
            {
                if (line.Length == 0 || line == "|" || M(line, @"^\|\s*:?-{3,}:?\s*\|$").Success) continue;
                Match opening = OpeningFence(line);
                if (!opening.Success)
                {
                    // フェンスがある入力では、その外側の説明文はスキルに含めません。
                    if (line.StartsWith("《") || line.StartsWith("【")) throw new InvalidOperationException("スキルの行がコードブロック外にあります：" + raw);
                    continue;
                }
                quoted = line.Contains("\""); openingFenceLength = opening.Groups["ticks"].Value.Length; inside = true; body.Clear(); continue;
            }
            Match closing = M(line, @"^(?<ticks>`{3,})""?\s*\|?$");
            if (closing.Success && closing.Groups["ticks"].Value.Length >= openingFenceLength)
            {
                string block = string.Join("\n", body);
                if (quoted) block = block.Replace("\"\"", "\"");
                blocks.Add("```\n" + block + "\n```"); inside = false; continue;
            }
            body.Add(raw);
        }
        if (inside) throw new InvalidOperationException("末尾のコードフェンスがありません。");
        if (blocks.Count == 0) throw new InvalidOperationException("スキルのコードブロックがありません。");
        return blocks;
    }
    private static Match OpeningFence(string line)
    {
        return M(line, @"^(?:\|\s*)?""?(?<ticks>`{3,})(?:text|txt|markdown|md)?$");
    }
    private static string NormalizeFenceLine(string raw)
    {
        string line = raw.Trim();
        if (!line.Contains("\\`")) return line;
        string candidate = line.Replace("\\`", "`");
        return OpeningFence(candidate).Success || M(candidate, @"^`{3,}""?\s*\|?$").Success ? candidate : line;
    }
    private static readonly EffectType[] DisplayOrder = { EffectType.Declaration, EffectType.Active, EffectType.Counter, EffectType.Passive, EffectType.Roleplay, EffectType.Critical, EffectType.Fumble, EffectType.SecondSpike, EffectType.ThirdSpike };
    private static OverrideContent Change(OverrideContentType type, params string[] p) { return new OverrideContent { Type = type, Parameters = p.ToList() }; }
    private static void AddOverride(List<OverrideDefinition> list, EffectType type, List<TriggerDefinition> triggers, params OverrideContent[] contents)
    {
        list.Add(new OverrideDefinition { Type = type, Triggers = triggers, Contents = contents.ToList() });
    }
    private static bool IsOrdinary(EffectType type)
    {
        switch (type)
        {
            case EffectType.Active:
            case EffectType.Counter:
            case EffectType.Passive:
            case EffectType.Roleplay:
            case EffectType.Critical:
            case EffectType.Fumble:
            case EffectType.Declaration:
                return true;
            default:
                return false;
        }
    }
    private static string[] VariableArgs(List<string> p, int min, string label)
    {
        if (p == null || p.Count < min) throw new InvalidOperationException(label + "のパラメーターは" + min + "個以上です。");
        return p.Select(x => Need(x, label)).ToArray();
    }
    private static string Signed(string value)
    {
        value = Need(value, "加減算値");
        if (!value.StartsWith("+") && !value.StartsWith("-")) value = "+" + value;
        if (value.Length < 2) throw new InvalidOperationException("加減算値に数値または式が必要です。");
        return value;
    }
    private static bool SameConditions(ConditionSet a, ConditionSet b)
    {
        if (a == null || b == null || a.And == null || b.And == null || a.Or == null || b.Or == null) return false;
        return SameEntries(a.And, b.And) && SameEntries(a.Or, b.Or);
    }
    private static bool SameEntries(List<ConditionEntry> a, List<ConditionEntry> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] == null || b[i] == null || a[i].Type != b[i].Type || a[i].Parameters == null || b[i].Parameters == null || !a[i].Parameters.SequenceEqual(b[i].Parameters)) return false;
        return true;
    }
    private static bool SameTrigger(TriggerDefinition a, TriggerDefinition b) { return a != null && b != null && a.Timing == b.Timing && SameConditions(a.Conditions, b.Conditions); }
    private static bool Matches(List<TriggerDefinition> triggers, params TriggerDefinition[] expected)
    {
        return triggers != null && triggers.Count == expected.Length && expected.All(e => triggers.Count(t => SameTrigger(t, e)) == 1);
    }
    private static List<TriggerDefinition> NoTriggers() { return new List<TriggerDefinition>(); }
    private static List<TriggerDefinition> On(TriggerDefinition trigger) { return new List<TriggerDefinition> { trigger }; }
    private static bool HasTrigger(TriggerDefinition t, TriggerTiming timing, params ConditionEntry[] entries) { return SameTrigger(t, Trigger(timing, entries)); }
    private static string CategoryAlternatives(IEnumerable<string> values, string connector)
    {
        return string.Join(connector, values.Select(x => "〈" + Need(x, "カテゴリー") + "〉"));
    }

    private readonly struct RegexCacheKey : IEquatable<RegexCacheKey>
    {
        public readonly string Pattern;
        public readonly RegexOptions Options;

        public RegexCacheKey(string pattern, RegexOptions options)
        {
            Pattern = pattern;
            Options = options;
        }

        public bool Equals(RegexCacheKey other)
        {
            return Options == other.Options && string.Equals(Pattern, other.Pattern, StringComparison.Ordinal);
        }

        public override bool Equals(object value)
        {
            return value is RegexCacheKey && Equals((RegexCacheKey)value);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Pattern == null ? 0 : StringComparer.Ordinal.GetHashCode(Pattern)) * 397) ^ (int)Options;
            }
        }
    }
}
