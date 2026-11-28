using static CommonEffectTextCodec;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class EquipmentTextConverter
{
    private const string Separator = "――――――――――――――――";
    private static readonly HashSet<string> ItemCategories = new HashSet<string>(StringComparer.Ordinal)
    { "装備アイテム", "投擲武器", "アイテム", "アクセサリー", "家具アイテム", "乗用アイテム", "ファミリアカード" };
    internal const int MaximumEquipmentGrade = 100;

    public static EquipmentTextData Parse(string source) => Parse(source, csvCell: false);

    internal static EquipmentTextData Parse(string source, bool csvCell)
    {
        List<string> blocks = InputBlocks(source, csvCell);
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

    private static List<string> Categories(string text)
    {
        if (!M(text, @"^(?:〈[^〉]+〉)+$").Success)
            throw new InvalidOperationException("カテゴリーの形式が不正です：" + text);
        return DataBlockText.GetRegex(@"〈([^〉]+)〉").Matches(text).Cast<Match>()
            .SelectMany(x => DataBlockText.GetRegex(@"\s*[,、]\s*|(?<=[^\s,、])\s+(?=[^\s,、]+武器(?:\s*[,、]|$))").Split(x.Groups[1].Value.Trim()))
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

    internal static string PrepareInput(string source, bool csvCell = false)
    {
        string text = DataBlockText.NormalizeBreaks(source).Trim();
        if (!text.Contains("\n"))
        {
            Match inline = M(text, @"^(`{3,}\w*)\s+(《[^》]+》)\s+(〈[^〉]+〉)\s+(【レアリティ】.+?)\s+(―{8,})\s+(.+?)\s+`{3,}$");
            if (inline.Success)
            {
                string body = RegexReplace(inline.Groups[4].Value, @"\s+(?=【)", "\n");
                body = RegexReplace(body, @"(【装備効果】[^\n]+?)(?<=\d)\s+(?=自身)", "$1\n");
                text = string.Join("\n", new[] { inline.Groups[1].Value, inline.Groups[2].Value,
                    inline.Groups[3].Value, body, inline.Groups[5].Value, inline.Groups[6].Value, "```" });
            }
        }
        text = RegexReplace(text, @"(?m)^(`{3,})\.[ \t]*$", "$1");
        text = RegexReplace(text, @"(?<=[^`\n])(`{3,})$", "\n$1");
        if (!csvCell) return text;

        int separator = M(text, @"(?m)^―{8,}[。.]?[ \t]*$").Index;
        if (separator <= 0) return text;
        string mechanics = text.Substring(0, separator);
        string flavor = text.Substring(separator);
        // A comma following a complete list of numeric additions, immediately before flavor.
        mechanics = RegexReplace(mechanics,
            @"(?m)^(【装備効果】\s*[^【+\-,、。\s]+[+-]\d+(?:\.\d+)?(?:\s*,\s*[^【+\-,、。\s]+[+-]\d+(?:\.\d+)?)*),[ \t]*(?=\n\s*\z)", "$1");
        // A CSV boundary may close a complete single item. Never consume another block as flavor.
        Match fence = M(mechanics, @"^(`{3,})(?:\w+)?\n《[^》]+》\n〈[^〉]+〉\n");
        if (fence.Success && !text.Substring(fence.Index + fence.Length).Contains("```") &&
            M(flavor, @"\n\s*\S").Success && !M(flavor, @"(?m)^《[^》]+》\s*\n\s*〈").Success)
            flavor += "\n" + fence.Groups[1].Value;
        return mechanics + flavor;
    }

    private static string[] EquipmentLines(string source)
    {
        // Limit header spelling corrections to the mechanics of this one block.
        Match divider = M(source, @"(?m)^[ \t]*―{8,}[。.]?[ \t]*$");
        int length = divider.Success ? divider.Index : source.Length;
        string body = source.Substring(0, length);
        body = RegexReplace(body, @"(?m)^【武器威力(?=\[スキル値\])", "【武器威力】");
        body = RegexReplace(body, @"(?m)^(【レアリティ】[^【\n]+?)[ \t]*(?=【グレード】)", "$1\n");
        return (body + source.Substring(length)).Split('\n');
    }

    private static List<string> InputBlocks(string source, bool csvCell = false) => DataBlockText.ReadBlocks(PrepareInput(source, csvCell), "装備");

    private static InvalidOperationException LineError(int index, string line, string message, Exception inner = null)
    {
        return new InvalidOperationException((index + 1) + "行目：" + message + "\n原文：" + line, inner);
    }
}
