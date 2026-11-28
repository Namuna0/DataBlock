using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

// A rule's parameter patterns delimit semantic values in both directions.
internal class DataBlockSentencePattern
{
    public readonly string Key;
    private readonly string template;
    private readonly string prefix;
    private readonly string pattern;
    private readonly int count;
    public int ParameterCount => count;

    public DataBlockSentencePattern(string key, string template, params string[] parameters)
    {
        Key = key;
        this.template = template;
        count = parameters.Length;
        string literal = template.TrimEnd('。');
        int index = literal.IndexOf('{');
        prefix = index < 0 ? literal : literal.Substring(0, index);
        string expression = Regex.Escape(literal);
        for (int i = 0; i < count; i++)
            expression = expression.Replace(Regex.Escape("{" + i + "}"), "(?<p" + i + ">" + parameters[i] + ")");
        pattern = "^" + expression + "。?$";
    }

    public string[] Read(string text)
    {
        if (!text.StartsWith(prefix, StringComparison.Ordinal)) return null;
        Match match = DataBlockText.GetRegex(pattern).Match(text);
        return match.Success ? Enumerable.Range(0, count).Select(i => match.Groups["p" + i].Value).ToArray() : null;
    }

    public string Write(IList<string> parameters)
    {
        if (parameters == null || parameters.Count != count || parameters.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(Key + "のパラメーターは" + count + "個の値が必要です。");
        string result = string.Format(CultureInfo.InvariantCulture, template, parameters.Cast<object>().ToArray());
        var parsed = Read(result);
        if (parsed == null || !parsed.SequenceEqual(parameters))
            throw new InvalidOperationException(Key + "のパラメーターの形式が不正です。");
        return result;
    }
}
