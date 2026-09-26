using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class EquipmentTextConverter
{
    private const string Separator = "――――――――――――――――";
    private const string EquipmentCategory = "装備アイテム";
    internal const int MaximumEquipmentGrade = 100;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly ConcurrentDictionary<EquipmentRegexKey, Regex> RegexCache =
        new ConcurrentDictionary<EquipmentRegexKey, Regex>();

    public static EquipmentTextData Parse(string source)
    {
        List<string> blocks = InputBlocks(source);
        if (blocks.Count != 1)
            throw new InvalidOperationException(blocks.Count + "件の装備があります。シリアライズセットは1件ずつ行ってください。構文統一はまとめて実行できます。");
        EquipmentTextData data = ParseOne(blocks[0]);
        Build(data);
        return data;
    }

    public static string Normalize(string source)
    {
        return string.Join("\n\n", InputBlocks(source).Select(x => Build(ParseOne(x))));
    }

    private static Regex GetRegex(string pattern, RegexOptions options = RegexOptions.None)
    {
        options |= RegexOptions.CultureInvariant;
        return RegexCache.GetOrAdd(new EquipmentRegexKey(pattern, options), CreateRegex);
    }

    private static Regex CreateRegex(EquipmentRegexKey key)
    {
        return new Regex(key.Pattern, key.Options, RegexTimeout);
    }

    private static Match M(string text, string pattern)
    {
        return GetRegex(pattern).Match(text);
    }

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
        if (values == null || values.Count != count)
            throw new InvalidOperationException(label + "のパラメーターは" + count + "個です。");
        return values.Select(x => Need(x, label + "のパラメーター")).ToArray();
    }

    private static int Number(string value, int minimum, string label)
    {
        int result;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) || result < minimum)
            throw new InvalidOperationException(label + "は" + minimum + "以上の整数にしてください。");
        return result;
    }

    private static string Signed(string value)
    {
        value = Need(value, "加減算値");
        if (!value.StartsWith("+", StringComparison.Ordinal) && !value.StartsWith("-", StringComparison.Ordinal))
            value = "+" + value;
        if (value.Length < 2) throw new InvalidOperationException("加減算値に数値または式が必要です。");
        return value;
    }

    private static List<string> Split(string text, string delimiter)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if ("([〈《（［".IndexOf(c) >= 0) depth++;
            if (")]〉》）］".IndexOf(c) >= 0) depth--;
            if (depth < 0) throw new InvalidOperationException("括弧の対応が不正です：" + text);
            if (depth == 0 && i + delimiter.Length <= text.Length &&
                string.CompareOrdinal(text, i, delimiter, 0, delimiter.Length) == 0)
            {
                string part = text.Substring(start, i - start).Trim();
                if (part.Length == 0)
                    throw new InvalidOperationException("区切り文字の前後に空の項目があります：" + text);
                result.Add(part);
                i += delimiter.Length - 1;
                start = i + 1;
            }
        }
        if (depth != 0) throw new InvalidOperationException("括弧が閉じていません：" + text);
        string last = text.Substring(start).Trim();
        if (last.Length == 0 && result.Count > 0)
            throw new InvalidOperationException("区切り文字の前後に空の項目があります：" + text);
        if (last.Length > 0) result.Add(last);
        return result;
    }

    private static List<string> Categories(string text)
    {
        if (!M(text, @"^(?:〈[^〉]+〉)+$").Success)
            throw new InvalidOperationException("カテゴリーの形式が不正です：" + text);
        return GetRegex(@"〈([^〉]+)〉").Matches(text).Cast<Match>()
            .SelectMany(x => x.Groups[1].Value.Split(','))
            .Select(x => Need(x, "カテゴリー")).ToList();
    }

    private static string CategoryText(List<string> values)
    {
        if (values == null) throw new InvalidOperationException("Categoriesがnullです。");
        return "〈" + string.Join(", ", values.Select(x => Need(x, "カテゴリー"))) + "〉";
    }

    private static string SyntaxLine(string line)
    {
        return line.Trim().Replace("\\*", "*");
    }

    private static string Unbullet(string text)
    {
        return RegexReplace(text.Trim(), @"^(?:・|●|⚫[\uFE0E\uFE0F]?)\s*", "");
    }

    private static List<string> InputBlocks(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new InvalidOperationException("入力テキストが空です。");
        string text = source.Replace("\r\n", "\n").Replace("\r", "\n");
        text = RegexReplace(text, @"\n?[ \t]*<br\s*/?>[ \t]*\n?", "\n", RegexOptions.IgnoreCase);
        string[] lines = text.Split('\n');
        bool fenced = lines.Any(x => OpeningFence(NormalizeFenceLine(x)).Success);
        if (!fenced)
        {
            string plain = text.Trim();
            if (plain.Length >= 2 && plain.StartsWith("|", StringComparison.Ordinal) &&
                plain.EndsWith("|", StringComparison.Ordinal))
                plain = plain.Substring(1, plain.Length - 2).Trim();
            return new List<string> { plain };
        }

        var blocks = new List<string>();
        var body = new List<string>();
        bool inside = false;
        bool quoted = false;
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
                    if (line.StartsWith("《", StringComparison.Ordinal) || line.StartsWith("【", StringComparison.Ordinal))
                        throw new InvalidOperationException("装備の行がコードブロック外にあります：" + raw);
                    continue;
                }
                quoted = line.Contains("\"");
                openingFenceLength = opening.Groups["ticks"].Value.Length;
                inside = true;
                body.Clear();
                continue;
            }

            Match closing = M(line, @"^(?<ticks>`{3,})""?\s*\|?$");
            if (closing.Success && closing.Groups["ticks"].Value.Length >= openingFenceLength)
            {
                string block = string.Join("\n", body);
                if (quoted) block = block.Replace("\"\"", "\"");
                blocks.Add("```\n" + block + "\n```");
                inside = false;
                continue;
            }
            body.Add(raw);
        }
        if (inside) throw new InvalidOperationException("末尾のコードフェンスがありません。");
        if (blocks.Count == 0) throw new InvalidOperationException("装備のコードブロックがありません。");
        return blocks;
    }

    private static Match OpeningFence(string line)
    {
        return M(line, @"^(?:\|\s*)?""?(?<ticks>`{3,})(?:[A-Za-z][A-Za-z0-9_.+-]*)?$");
    }

    private static string NormalizeFenceLine(string raw)
    {
        string line = raw.Trim();
        if (!line.Contains("\\`")) return line;
        string candidate = line.Replace("\\`", "`");
        return OpeningFence(candidate).Success || M(candidate, @"^`{3,}""?\s*\|?$").Success ? candidate : line;
    }

    private static InvalidOperationException LineError(int index, string line, string message, Exception inner = null)
    {
        return new InvalidOperationException((index + 1) + "行目：" + message + "\n原文：" + line, inner);
    }

    private readonly struct EquipmentRegexKey : IEquatable<EquipmentRegexKey>
    {
        public readonly string Pattern;
        public readonly RegexOptions Options;

        public EquipmentRegexKey(string pattern, RegexOptions options)
        {
            Pattern = pattern;
            Options = options;
        }

        public bool Equals(EquipmentRegexKey other)
        {
            return Options == other.Options && string.Equals(Pattern, other.Pattern, StringComparison.Ordinal);
        }

        public override bool Equals(object value)
        {
            return value is EquipmentRegexKey && Equals((EquipmentRegexKey)value);
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
