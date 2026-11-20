using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

internal static class DataBlockText
{
    internal const string OpeningPattern = @"^(?:\|\s*)?""?(?<ticks>`{3,})(?:[A-Za-z][A-Za-z0-9_.+-]*)?$";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    private static readonly ConcurrentDictionary<(string Pattern, RegexOptions Options), Regex> Regexes =
        new ConcurrentDictionary<(string, RegexOptions), Regex>();

    internal static Regex GetRegex(string pattern, RegexOptions options = RegexOptions.None)
    {
        return Regexes.GetOrAdd((pattern, options | RegexOptions.CultureInvariant),
            key => new Regex(key.Pattern, key.Options, Timeout));
    }

    internal static string NormalizeBreaks(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new InvalidOperationException("入力テキストが空です。");
        string text = source.Replace("\r\n", "\n").Replace("\r", "\n");
        return GetRegex(@"\n?[ \t]*<br\s*/?>[ \t]*\n?", RegexOptions.IgnoreCase).Replace(text, "\n");
    }

    internal static List<string> ReadBlocks(string text, string kind, bool keepFences = true,
        Func<string, List<string>> splitPlain = null, string openingPattern = OpeningPattern)
    {
        string[] lines = text.Split('\n');
        bool fenced = lines.Any(x => OpeningFence(NormalizeFenceLine(x, openingPattern), openingPattern).Success);
        if (!fenced)
        {
            string plain = text.Trim();
            if (plain.Length >= 2 && plain.StartsWith("|", StringComparison.Ordinal) &&
                plain.EndsWith("|", StringComparison.Ordinal))
                plain = plain.Substring(1, plain.Length - 2).Trim();
            return splitPlain == null ? new List<string> { plain } : splitPlain(plain);
        }

        var blocks = new List<string>();
        var body = new List<string>();
        bool inside = false;
        bool quoted = false;
        int openingFenceLength = 0;
        foreach (string raw in lines)
        {
            string line = NormalizeFenceLine(raw, openingPattern);
            if (!inside)
            {
                if (line.Length == 0 || line == "|" || GetRegex(@"^\|\s*:?-{3,}:?\s*\|$").IsMatch(line)) continue;
                Match opening = OpeningFence(line, openingPattern);
                if (!opening.Success)
                {
                    if (line.StartsWith("《", StringComparison.Ordinal) || line.StartsWith("【", StringComparison.Ordinal))
                        throw new InvalidOperationException(kind + "の行がコードブロック外にあります：" + raw);
                    continue;
                }
                quoted = line.Contains("\"");
                openingFenceLength = opening.Groups["ticks"].Value.Length;
                inside = true;
                body.Clear();
                continue;
            }

            Match closing = GetRegex(@"^(?<ticks>`{3,})""?\s*\|?$").Match(line);
            if (closing.Success && closing.Groups["ticks"].Value.Length >= openingFenceLength)
            {
                string block = string.Join("\n", body);
                if (quoted) block = block.Replace("\"\"", "\"");
                blocks.Add(keepFences ? "```\n" + block + "\n```" : block);
                inside = false;
                continue;
            }
            body.Add(raw);
        }
        if (inside) throw new InvalidOperationException("末尾のコードフェンスがありません。");
        if (blocks.Count == 0) throw new InvalidOperationException(kind + "のコードブロックがありません。");
        return blocks;
    }

    internal static Match OpeningFence(string line, string openingPattern = OpeningPattern)
    {
        return GetRegex(openingPattern).Match(line);
    }

    internal static string NormalizeFenceLine(string raw, string openingPattern = OpeningPattern)
    {
        string line = raw.Trim();
        if (!line.Contains("\\`")) return line;
        string candidate = line.Replace("\\`", "`");
        return OpeningFence(candidate, openingPattern).Success || GetRegex(@"^`{3,}""?\s*\|?$").IsMatch(candidate) ? candidate : line;
    }
}
