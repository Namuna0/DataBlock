using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static partial class ItemTextConverter
{
    private const string RecipeDescription = "【説明】下記の効果を一つ選んで実行する。";
    private const string RecipeDivider = "――――――――――――――――";
    private static Regex RecipePattern(string pattern) => DataBlockText.GetRegex(pattern);

    private static bool IsRecipeHeading(string line)
    {
        string text = line.Trim();
        return text.StartsWith("◆") || (text.StartsWith("●") && !text.EndsWith("。"));
    }

    internal static List<SkillTextData> ParseRecipeChoices(string text, out string flavor)
    {
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim().Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != RecipeDescription)
            throw new InvalidOperationException("レシピの選択式の説明が不正です。");
        var choices = new List<SkillTextData>();
        flavor = "";
        SkillTextData project = null;
        int index = 1;
        while (index < lines.Length)
        {
            string heading = lines[index++].Trim();
            if (heading.Length == 0) continue;
            bool projectHeading = heading.StartsWith("◆");
            if (projectHeading) project = null;
            bool set = heading == "○セット効果";
            if (set && index < lines.Length) heading = lines[index++].Trim();
            var name = RecipePattern(set ? @"^《([^》]+)》$" : @"^[◆●](.+)$").Match(heading);
            if (!name.Success) throw new InvalidOperationException("レシピの選択肢名を指定してください：" + heading);
            int start = index;
            while (index < lines.Length && !IsRecipeHeading(lines[index]) && lines[index].Trim() != "○セット効果") index++;
            int end = index;
            if (end == lines.Length)
            {
                int lastDivider = Array.FindLastIndex(lines, line => RecipePattern(@"^―{5,}$").IsMatch(line.Trim()));
                if (lastDivider >= start)
                {
                    flavor = string.Join("\n", lines.Skip(lastDivider + 1)).TrimEnd();
                    end = lastDivider;
                }
            }
            string body = string.Join("\n", lines.Skip(start).Take(end - start)).Trim();
            if (body.Length == 0 && projectHeading && index < lines.Length && lines[index].TrimStart().StartsWith("●"))
            {
                project = new SkillTextData();
                project.Skill.Name = name.Groups[1].Value.Trim();
                choices.Add(project);
            }
            if (body.Length == 0) continue;
            string normalized = NormalizeRecipeBody(body);
            if (normalized.Split(new[] { RecipeDivider }, StringSplitOptions.None)[0].Contains("《》")) continue;
            var source = "《" + name.Groups[1].Value.Trim() + "》\n" + (set ? "〈セット効果〉\n" : "") + normalized;
            try
            {
                var entry = SkillTextConverter.Parse(source);
                if (project == null) choices.Add(entry);
                else
                {
                    project.Skill.Choices.Add(entry.Skill);
                    project.States.AddRange(entry.States);
                    project.Summons.AddRange(entry.Summons);
                }
            }
            catch (InvalidOperationException ex) { throw new InvalidOperationException("《" + name.Groups[1].Value + "》：" + ex.Message, ex); }
        }
        if (choices.Count == 0) throw new InvalidOperationException("本文のあるレシピ選択肢がありません。");
        return choices;
    }

    internal static string BuildRecipeChoices(List<SkillTextData> skills, string flavor)
    {
        if (skills == null || skills.Count == 0) throw new InvalidOperationException("レシピの選択肢が空です。");
        var result = new StringBuilder(RecipeDescription);
        bool projectSeen = false;
        foreach (var choice in skills)
        {
            var lines = SkillTextConverter.Build(choice).Trim().Split('\n');
            bool set = choice.Skill.Categories.SequenceEqual(new[] { "セット効果" });
            bool hasPhases = choice.Skill.Choices.Count > 0;
            projectSeen |= hasPhases;
            result.Append("\n\n").Append(set ? "○セット効果\n《" + choice.Skill.Name + "》" : (projectSeen ? "◆" : "●") + choice.Skill.Name);
            int start = hasPhases ? Array.FindIndex(lines, l => l.StartsWith("●")) : set ? 3 : 2;
            foreach (string line in lines.Skip(start).Take(lines.Length - start - 1)) result.Append('\n').Append(line);
        }
        if (!string.IsNullOrEmpty(flavor) || !string.IsNullOrEmpty(skills[skills.Count - 1].Skill.Flavor))
            result.Append('\n').Append(RecipeDivider).Append('\n').Append(flavor);
        return result.ToString().TrimEnd();
    }

    // Normalize mechanical fields only. Flavor keeps its punctuation and line breaks.
    internal static string NormalizeRecipeBody(string body)
    {
        var lines = body.Split('\n');
        var result = new List<string>();
        bool fortuneRows = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (RecipePattern(@"^―{5,}$").IsMatch(line))
            {
                result.Add(RecipeDivider);
                result.AddRange(lines.Skip(i + 1));
                break;
            }
            line = RecipePattern(@"^●\[?([^\[\]]+B)\](及び\[[^\]]+B\]によるダイスロールを行った後、この状態は解除される。)$").Replace(line, "*[$1]$2");
            if (line.StartsWith("【消費リソース】"))
            {
                while (i + 1 < lines.Length && line.EndsWith(",") && lines[i + 1].TrimStart().StartsWith("《"))
                    line += " " + lines[++i].Trim();
                if (i + 2 < lines.Length)
                {
                    var amount = RecipePattern(@"^以下から1つ選択して×([0-9]+)個消費。$").Match(lines[i + 1].Trim());
                    var options = RecipePattern(@"^《([^》]+)》$").Match(lines[i + 2].Trim());
                    if (amount.Success && options.Success)
                    {
                        line += ", " + string.Join("または", options.Groups[1].Value.Split(',', '、').Select(v => "《" + v.Trim() + "》")) + "×" + amount.Groups[1].Value;
                        i += 2;
                    }
                }
                line = "【消費リソース】" + NormalizeIngredients(line.Substring("【消費リソース】".Length));
            }
            else if (line.StartsWith("【宣言条件】"))
            {
                line = RecipePattern(@"((?:(?:スキル|クラス)?《[^》]+》(?:\s*(?:と|[,、])\s*)?)+)(?:を習得している事|の習得)").Replace(line, m =>
                    string.Join(", ", RecipePattern(@"(クラス)?《([^》]+)》").Matches(m.Groups[1].Value).Cast<Match>()
                        .SelectMany(v => v.Groups[2].Value.Split(',', '、').Select(n => v.Groups[1].Value + "《" + n.Trim() + "》の習得"))));
                line = line.Replace("の習得。MP最大値", "の習得, MP最大値").Replace("の習得。キャラクター", "の習得, キャラクター");
                line = line.Replace("重複できる", "重複可能");
                line = RecipePattern(@"^【宣言条件】((?:《[^》]+》\+)+《[^》]+》)を消費。?$").Replace(line, m =>
                    "【消費リソース】" + NormalizeIngredients(m.Groups[1].Value.Replace("+", ", ")));
                line = RecipePattern(@"対象キャラクター([0-9]+)体を指定して発動可能").Replace(line, "キャラクターを$1体選択");
            }
            if (line.StartsWith("〈") && !line.Contains("〉")) line += "〉";
            line = RecipePattern(@"^《(初級|中級|上級)》$").Replace(line, "〈$1〉");
            line = line.Replace("》》×", "》×");
            line = RecipePattern(@"(?:ダイス|発動)ロールの結果に応じて、?(?=《)").Replace(line, "発動ロールの結果に応じて");
            line = RecipePattern(@"発動ロールの結果に応じて《([^》]+)》と《([^》]+)》を獲得します。?").Replace(line, "発動ロールの結果に応じて《$1》を獲得します。\n発動ロールの結果に応じて《$2》を獲得します。");
            line = RecipePattern(@"発動ロール([0-9]+)回以上達成で《([^》]+)》(×([0-9]+))?(?:と《([^》]+)》×([0-9]+))?(?:を)?獲得(?:します)?。?").Replace(line, m =>
                "発動ロール" + m.Groups[1].Value + "回以上達成で《" + m.Groups[2].Value + "》×" + (m.Groups[4].Success ? m.Groups[4].Value : "1") + "獲得。" +
                (m.Groups[5].Success ? "\n発動ロール" + m.Groups[1].Value + "回以上達成で《" + m.Groups[5].Value + "》×" + m.Groups[6].Value + "獲得。" : ""));
            line = RecipePattern(@"(個数[+-][0-9]+|達成回数[+-][0-9]+)[,、]\s*").Replace(line, "$1\n").TrimEnd();
            line = RecipePattern(@"対象の([0-9]+)のダメージ").Replace(line, "対象に$1のダメージ");
            line = RecipePattern(@"^○グレードを").Replace(line, "【宣言効果】グレードを");
            line = RecipePattern(@"(?:発動ロール|達成)([0-9]+)回以上達成で、?(?=対象)").Replace(line, "達成$1回以上で発動。\n");
            line = RecipePattern(@"対象に《([^》]+)》状態を永続的に付与。").Replace(line, "対象を永続的に《$1》状態にする。");
            line = RecipePattern(@"^継続([0-9]+)ターン目(以降|のみ)?：").Replace(line, m =>
                "継続" + m.Groups[1].Value + "ターン目" + (m.Groups[2].Success ? m.Groups[2].Value : "のみ") + "の効果。\n");
            line = line.Replace("。敵から付与", "。\n敵から付与").Replace("食事セット効果：", "");
            line = RecipePattern(@"([0-9]+日の間、)([^,]+B[+-][0-9.]+), ").Replace(line, "$1$2。\n$1"); 
            if (line.EndsWith("報酬によるダイスロールを行う際、") && i + 2 < lines.Length)
                line += lines[++i].Trim().Replace("を行う。", "を行い、") + lines[++i].Trim();
            if (line.Contains("以下の占い効果")) fortuneRows = true;
            else if (line.StartsWith("【")) fortuneRows = false;
            if (fortuneRows && line.StartsWith("- ")) line = RecipePattern(@"^- (\d*)～(\d*)：《([^》]+)》(.*)$").Replace(line, m =>
            {
                string min = m.Groups[1].Value, max = m.Groups[2].Value;
                string suffix = m.Groups[4].Value;
                return "占い出目" + (min.Length == 0 ? "下限" : min) + "～" + (max.Length == 0 ? "上限" : max) +
                    "：" + (suffix.Length == 0 ? "エリア《" + m.Groups[3].Value + "》。" : "《" + m.Groups[3].Value + "》を適用。\n" + suffix);
            });
            line = RecipePattern(@"達成([0-9]+)回以上達成で発動。?").Replace(line, "達成$1回以上で発動。");
            result.Add(line);
        }
        string normalized = string.Join("\n", result);
        // Some sheets paste the same outcome block twice, joined by the end of its roll.
        return RecipePattern(@"(?m)(?<block>^【アクティブ効果】[^\n]+\n【成功効果】[^\n]+\n【失敗効果】[^\n]+\n【クリティカル効果】[^\n]+\n【ファンブル効果】個数[+-][0-9]+)\[[^\]]+\] 目標値[0-9]+\n\k<block>(?=\n|$)").Replace(normalized, "${block}");
    }

    private static string NormalizeIngredients(string text)
    {
        text = RecipePattern(@"〈([^〈》〉]+)》×").Replace(text, "〈$1〉×");
        text = text.Replace(". ", ", ").Replace("かつ", "").Replace("アイテム〈", "〈");
        text = RecipePattern(@"(?<=[0-9》〉])\s+(?=[《〈A-Z])").Replace(text, ", ");
        return RecipePattern(@"(《[^》]+》(?:または《[^》]+》)*|〈[^〉]+〉)(?:×([0-9]+))?").Replace(text, m =>
        {
            string token = m.Groups[1].Value, amount = m.Groups[2].Success ? m.Groups[2].Value : "1";
            if (token.Contains("または")) return token + "×" + amount;
            var names = token.Substring(1, token.Length - 2).Split(',', '、');
            return string.Join(", ", names.Select(n => token[0] + n.Trim() + token[token.Length - 1] + "×" + amount));
        });
    }

    internal static bool TryNormalizePrizeTable(string source, out string textBlock)
    {
        textBlock = null;
        var lines = DataBlockText.NormalizeBreaks(source).Split('\n').Select(x => x.Trim()).Where(x => !x.StartsWith("\x60\x60\x60")).ToArray();
        int first = Array.FindIndex(lines, l => l.Length > 0);
        if (first < 0 || first + 2 >= lines.Length) return false;
        var name = RecipePattern(@"^-([^-].+)-$").Match(lines[first]);
        if (!name.Success || !lines[first+1].StartsWith("【消費アイテム】") || !lines[first+2].StartsWith("【景品ロール】")) return false;
        var text = new StringBuilder("《" + name.Groups[1].Value + "》\n【消費リソース】" + NormalizeIngredients(lines[first+1].Substring("【消費アイテム】".Length)));
        text.Append("\n【アクティブ効果】報酬表〈景品ロール〉のダイスは").Append(lines[first+2].Substring("【景品ロール】".Length)).Append("。");
        for (int i=first+3; i<lines.Length; i++)
        {
            if (lines[i].Length == 0) continue;
            if (RecipePattern(@"^―{5,}$").IsMatch(lines[i])) { text.Append('\n').Append(string.Join("\n",lines.Skip(i))); break; }
            var row = RecipePattern(@"^-\s*(\d+)(?:～(\d+))?[：:]\s*([^《]*)《([^》]+)》(?:×(\d+))?(?:を獲得)?。?$").Match(lines[i]);
            if (!row.Success) throw new InvalidOperationException("景品ロールの範囲とアイテムを指定してください：" + lines[i]);
            text.Append("\n出目").Append(row.Groups[1].Value).Append("～").Append(row.Groups[2].Success ? row.Groups[2].Value : row.Groups[1].Value).Append("：")
                .Append(row.Groups[3].Length == 0 ? "" : "〈" + row.Groups[3].Value + "〉").Append("《").Append(row.Groups[4].Value).Append("》×")
                .Append(row.Groups[5].Success ? row.Groups[5].Value : "1").Append("を獲得する。");
        }
        textBlock = text.ToString();
        return true;
    }
}
