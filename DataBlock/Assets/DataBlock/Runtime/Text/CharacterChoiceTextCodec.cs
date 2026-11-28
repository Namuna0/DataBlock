using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// 選択の指示と、その指示で使う候補を一つの効果にまとめて保存する。
internal static class CharacterChoiceTextCodec
{
    private const string Positive = @"[1-9][0-9]*";
    private const string Signed = @"[+-][0-9]+";
    private const string Factor = @"[0-9]+(?:\.[0-9]+)?";
    private const string Name = @"[^《》〈〉【】\r\n。：:]+?";
    private const string TitleInstruction = "文字列を前後で組み合わせその名の称号を得る。";
    private const string TitleEffect = "効果はパッシブ効果として扱われる。";
    private const string MealInstruction = "食事セット効果として以下を受けることができる。";

    private static readonly DataBlockSentencePattern[] Options =
    {
        new DataBlockSentencePattern("Skill", "キャラ作成時に選べるクラススキル枠を{0}つ消費して、以下の内の{1}つから選択してスキルを習得する。", Positive, Positive),
        new DataBlockSentencePattern("Item", "以下の内の{0}つから選択してアイテムを獲得する。", Positive),
        new DataBlockSentencePattern("Class", "以下のクラスから{0}つ選択して習得する。", Positive),
        new DataBlockSentencePattern("OtherRace", "下記の内から自身とは別種族を{0}つ選ぶ。", Positive),
        new DataBlockSentencePattern("CombinedTitle", "{0}と{1}から一つずつテキストを選択する。", Name, Name),
    };

    private static readonly DataBlockSentencePattern[] Conditions =
    {
        new DataBlockSentencePattern("ResourceAtMostPercent", "自身の{0}が最大値の{1}%以下の時。", "[A-Z]+", @"[0-9]+"),
        new DataBlockSentencePattern("FromBattleTurn", "戦闘開始から{0}ターン目以降。", Positive),
        new DataBlockSentencePattern("NextTurnAfterNormalFailure", "自身が行為判定に通常失敗した次ターンのスキル{0}回。", Positive),
        new DataBlockSentencePattern("NextTurnAfterResourceLoss", "自身が{0}を{1}以上失った次のターン。", "[A-Z]+", Positive),
        new DataBlockSentencePattern("TurnOfStatCheck", "自身が[{0}]による行為判定を行ったターン。", Name),
        new DataBlockSentencePattern("EndTurnWithResource", "自身が{0}を{1}以上残して行動終了を宣言したターン。", "[A-Z]+", Positive)
    };

    private static readonly DataBlockSentencePattern[] Modifiers =
    {
        new DataBlockSentencePattern("ResourceEffectFactor", "キャラクターのリソースに影響を与える効果値が×{0}倍される。", Factor),
        new DataBlockSentencePattern("HighestAbilityBonusFactor", "自身の最も高い[能力値B]が×{0}倍される。", Factor),
        new DataBlockSentencePattern("CriticalThresholdDelta", "クリティカルになるダイスの目が{0}される。", Signed),
        new DataBlockSentencePattern("SkillCostPerTurn", "1ターンに{0}度までスキルの消費{1}が{2}される。", Positive, "[A-Z]+", Signed),
        new DataBlockSentencePattern("AppliedStateDurationDelta", "自身が付与する状態の持続ターンが{0}される。", Signed),
        new DataBlockSentencePattern("StatCheckTargetDelta", "[{0}]による行為判定の目標値が{1}される。", Name, Signed),
        new DataBlockSentencePattern("CounterRollFactor", "自身のカウンター効果の発動ロール達成値は×{0}倍される。", Factor)
    };

    private static readonly DataBlockSentencePattern MealEffects = new DataBlockSentencePattern("MealRewards",
        "経験値{0}, 決意{1}, {2}日全てのリソース最大値{3}倍及び全ての行為判定の達成値×{4}倍", Signed, Signed, Positive, Factor, Factor);

    private static Match Match(string text, string pattern) => DataBlockText.GetRegex(pattern).Match(text);
    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException("選択効果：" + message);
    }

    private static EffectContent Part(bool head, params string[] values) =>
        CommonEffectTextCodec.Effect(head ? EffectContentType.CharacterChoice : EffectContentType.CharacterChoiceEntry, values);

    private static string Next(string[] lines, ref int i, int end)
    {
        while (i < end)
        {
            var s = lines[++i].Trim();
            if (s.Length > 0) return s;
        }
        throw new InvalidOperationException("選択の記述が途中で終わっています。");
    }

    private static bool NextOption(string[] lines, ref int index, int end)
    {
        int next = index + 1;
        while (next <= end && string.IsNullOrWhiteSpace(lines[next])) next++;
        if (next > end || !lines[next].TrimStart().StartsWith("-", StringComparison.Ordinal)) return false;
        index = next;
        return true;
    }

    internal static bool TryRead(string header, string[] lines, ref int i, int end, out List<EffectContent> parts)
    {
        parts = null;
        header = header.TrimStart('・').Trim().Replace("一つづつ", "一つずつ");
        if (header == MealInstruction)
        {
            var meal = Match(Next(lines, ref i, end), @"^《([^》]+)》[：:]\s*((?:〈[^〉]+〉\+)*〈[^〉]+〉)かつ合計グレードが☆([1-9][0-9]*)$");
            Check(meal.Success, "食事セットの名称・カテゴリー・合計グレードを指定してください。");
            string text = Next(lines, ref i, end);
            const string active = "【アクティブ効果】";
            var effect = text.StartsWith(active, StringComparison.Ordinal) ? MealEffects.Read(text.Substring(active.Length).Trim()) : null;
            Check(effect != null, "食事セットで得られる効果を確認してください。");
            parts = new List<EffectContent> {
                Part(true, new[] { "MealSet", meal.Groups[1].Value, meal.Groups[3].Value }
                    .Concat(meal.Groups[2].Value.Split('+').Select(x => x.Trim('〈', '〉'))).ToArray()),
                Part(false, new[] { MealEffects.Key }.Concat(effect).ToArray())
            };
            return true;
        }
        if (header.StartsWith("さらに以下の内の", StringComparison.Ordinal)) header = header.Substring(3);
        foreach (var pattern in Options)
        {
            var args = pattern.Read(header);
            if (args == null) continue;
            parts = new List<EffectContent> { Part(true, new[] { pattern.Key }.Concat(args).ToArray()) };
            if (pattern.Key == "CombinedTitle") ReadTitles(parts, lines, ref i, end, args);
            else if (pattern.Key == "OtherRace")
            {
                var list = Next(lines, ref i, end);
                while (!list.EndsWith("》", StringComparison.Ordinal))
                {
                    string more = Next(lines, ref i, end);
                    Check(!more.StartsWith("【") && !more.StartsWith("・") && !more.StartsWith("―"), "種族候補の《》が閉じていません。");
                    list += (list.EndsWith(",") || list.EndsWith("、") ? " " : ", ") + more;
                }
                var match = Match(list, @"^《([^《》]+)》$");
                Check(match.Success, "種族候補を《》で指定してください。");
                foreach (var race in match.Groups[1].Value.Split(',', '、')) parts.Add(Part(false, "Race", race.Trim()));
            }
            else
            {
                while (NextOption(lines, ref i, end))
                {
                    var option = Match(lines[i].Trim(), @"^-\s*([^《》]*)《([^《》]+)》(習得。?|×([1-9][0-9]*))?$");
                    Check(option.Success, "候補名・個数の書式が不正です。");
                    var category = option.Groups[1].Value.Trim();
                    var name = option.Groups[2].Value;
                    string suffix = option.Groups[3].Value;
                    string count = option.Groups[4].Value;
                    Check(pattern.Key == "Skill" ? !suffix.StartsWith("×") : pattern.Key == "Item" ? !suffix.StartsWith("習得") : suffix.Length == 0, "候補の種類と指定内容が一致しません。");
                    var values = new List<string> { pattern.Key, name };
                    if (pattern.Key == "Item") values.Add(count.Length > 0 ? count : "1");
                    if (category.Length > 0) values.Add(category);
                    parts.Add(Part(false, values.ToArray()));
                }
            }
            Check(parts.Count > 1, "候補がありません。");
            return true;
        }
        return false;
    }

    private static void ReadTitles(List<EffectContent> parts, string[] lines, ref int i, int end, string[] groups)
    {
        Check(Next(lines, ref i, end) == TitleInstruction && Next(lines, ref i, end) == TitleEffect, "称号の組み合わせ方が不明です。");
        var heading = Next(lines, ref i, end);
        if (heading == "●例")
        {
            var name = Match(Next(lines, ref i, end), @"^《([^》]+)》$");
            string example = Next(lines, ref i, end);
            var m = Match(example, @"^【パッシブ効果】([^。]+。)(.+)$");
            Check(name.Success && m.Success, "称号の例には名称・条件・効果が必要です。");
            parts.Add(Part(false, "Example", name.Groups[1].Value));
            parts.Add(RulePart("ExampleCondition", "", m.Groups[1].Value, Conditions));
            parts.Add(RulePart("ExampleModifier", "", m.Groups[2].Value, Modifiers));
            heading = Next(lines, ref i, end);
        }
        for (int g = 0; g < groups.Length; g++)
        {
            if (g > 0) heading = Next(lines, ref i, end);
            Check(heading == groups[g], "称号の候補表「" + groups[g] + "」がありません。");
            int before = parts.Count;
            while (NextOption(lines, ref i, end))
            {
                var option = Match(lines[i].Trim(), @"^-\s*《([^》]+)》[：:]\s*(.+)$");
                Check(option.Success, "称号候補に名前と条件または効果を指定してください。");
                parts.Add(RulePart(g == 0 ? "Condition" : "Modifier", option.Groups[1].Value, option.Groups[2].Value, g == 0 ? Conditions : Modifiers));
            }
            Check(parts.Count > before, "称号の候補表が空です。");
        }
    }

    private static EffectContent RulePart(string kind, string name, string sentence, DataBlockSentencePattern[] rules)
    {
        sentence = sentence.Replace("%を以下", "%以下").Replace("最も高い能力値B", "最も高い[能力値B]");
        foreach (var rule in rules)
        {
            var args = rule.Read(sentence);
            if (args != null)
            {
                var prefix = name.Length > 0 ? new[] { kind, name, rule.Key } : new[] { kind, rule.Key };
                return Part(false, prefix.Concat(args).ToArray());
            }
        }
        throw new InvalidOperationException("未対応の称号の条件・効果です：" + sentence);
    }

    internal static string Write(List<EffectContent> parts)
    {
        Check(parts.Count >= 2 && parts.Skip(1).All(x => x?.Type == EffectContentType.CharacterChoiceEntry), "候補の構成が不正です。");
        Check(parts.All(p => p.Parameters != null && p.Parameters.Count > 0 && p.Parameters.All(x => !string.IsNullOrWhiteSpace(x) && x.IndexOfAny("\r\n".ToCharArray()) < 0)), "選択効果の引数が空です。");
        var head = parts[0].Parameters;
        var rows = parts.Skip(1).Select(x => x.Parameters).ToList();
        if (head[0] == "MealSet")
        {
            Check(head.Count >= 4 && Match(head[2], "^" + Positive + "$").Success && rows.Count == 1 && rows[0][0] == MealEffects.Key, "食事セットの構成が不正です。");
            return MealInstruction + "\n《" + head[1] + "》：" + string.Join("+", head.Skip(3).Select(x => "〈" + x + "〉")) + "かつ合計グレードが☆" + head[2] +
                "\n【アクティブ効果】" + MealEffects.Write(rows[0].Skip(1).ToList());
        }
        var pattern = Options.SingleOrDefault(x => x.Key == head[0]);
        Check(pattern != null, "未対応の候補種別です。");
        var result = new List<string> { pattern.Write(head.Skip(1).ToList()) };
        if (head[0] == "CombinedTitle")
        {
            result.Add(TitleInstruction);
            result.Add(TitleEffect);
            int n = 0;
            if (rows[0][0] == "Example")
            {
                Check(rows[0].Count == 2 && rows.Count >= 3 && rows[1][0] == "ExampleCondition" && rows[2][0] == "ExampleModifier", "称号例に条件と効果が必要です。");
                result.Add("●例");
                result.Add("《" + rows[0][1] + "》");
                result.Add("【パッシブ効果】" + RuleText(rows[1], 1, Conditions) + RuleText(rows[2], 1, Modifiers));
                n = 3;
            }
            for (int g = 1; g <= 2; g++)
            {
                result.Add("");
                result.Add(head[g]);
                var kind = g == 1 ? "Condition" : "Modifier";
                int first = n;
                while (n < rows.Count && rows[n][0] == kind)
                {
                    var row = rows[n++];
                    Check(row.Count >= 3, "称号候補の引数が不足しています。");
                    result.Add("- 《" + row[1] + "》：" + RuleText(row, 2, g == 1 ? Conditions : Modifiers));
                }
                Check(n > first, "称号を構成する候補が不足しています。");
            }
            Check(n == rows.Count, "称号の候補の順序が不正です。");
        }
        else if (head[0] == "OtherRace")
        {
            Check(rows.All(p => p.Count == 2 && p[0] == "Race"), "種族候補の名前が必要です。");
            result.Add("《" + string.Join(", ", rows.Select(p => p[1])) + "》");
        }
        else foreach (var p in rows)
        {
            bool item = head[0] == "Item";
            int size = item ? 3 : 2;
            Check(p[0] == head[0] && (p.Count == size || p.Count == size + 1) &&
                (!item || Match(p[2], "^" + Positive + "$").Success), "候補の種類または個数が不正です。");
            result.Add("- " + (p.Count > size ? p[size] : "") + "《" + p[1] + "》" + (item ? "×" + p[2] : p[0] == "Skill" ? "習得。" : ""));
        }
        return string.Join("\n", result);
    }

    private static string RuleText(List<string> entry, int first, DataBlockSentencePattern[] rules)
    {
        var rule = rules.SingleOrDefault(x => x.Key == entry[first]);
        Check(rule != null, "未対応の称号ルールです：" + entry[first]);
        return rule.Write(entry.Skip(first + 1).ToList());
    }
}

