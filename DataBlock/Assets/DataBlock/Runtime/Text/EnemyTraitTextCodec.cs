using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static partial class EnemyTextConverter
{
    private static void AddTraits(EnemyDefinition enemy, IEnumerable<string> lines)
    {
        string name = null;
        var body = new List<string>();
        void Add()
        {
            if (name != null) enemy.Traits.Add(SkillTextConverter.Parse("《" + name + "》\n【パッシブ効果】\n" + NormalizeTraitStatements(body)));
        }
        foreach (string raw in lines)
        {
            string line = SyntaxLine(raw);
            if (line.Length == 0) continue;
            Match title = Match(line, @"^《([^《》]+)》\s*[：:]?\s*(.*)$");
            if (title.Success)
            {
                Add();
                name = title.Groups[1].Value;
                body.Clear();
                line = title.Groups[2].Value;
            }
            if (name == null) throw new InvalidOperationException("特性名がありません：" + line);
            if (line.Length > 0) body.Add(line.TrimStart('・'));
        }
        Add();
    }

    private static string TraitBodyText(SkillTextData trait)
    {
        string body = SkillBodyText(trait);
        int fields = body.IndexOf('\n');
        if (fields < 0 || !body.Substring(fields + 1).StartsWith("【パッシブ効果】", StringComparison.Ordinal))
            throw new InvalidOperationException("特性にはパッシブ効果を指定してください：" + trait.Skill.Name);
        return body.Substring(0, fields) + "：" + body.Substring(fields + 1 + "【パッシブ効果】".Length).TrimStart('\n');
    }

    private static string NormalizeTraitStatements(IEnumerable<string> lines)
    {
        string text = string.Join("\n", lines)
            .Replace("スタンバイフェース", "スタンバイフェーズ")
            .Replace("。（同名の効果は重複しない）", "。\n同名の効果は重複しない。")
            .Replace("（同名の効果は重複しない）", "同名の効果は重複しない。")
            .Replace("ＨＰ", "HP").Replace("状態、ではない", "状態ではない")
            .Replace("自身の状態はが受ける", "自身が受ける")
            .Replace("〉受けた時", "〉を受けた時")
            .Replace("度だけ。自身は", "度だけ、自身は")
            .Replace("自身が状態異常を付与の対象となる時\n", "自身に状態を付与される時、")
            .Replace("。成功時状態付与を無効にする。", "。\n成功時、状態付与を無効にする。")
            .Replace("成功した場合、全ての自身に付与された", "成功した場合、自身に付与された");
        string Rule(string pattern, string replacement) =>
            Replace(text, pattern, replacement, RegexOptions.Multiline);

        text = Rule(@"^毎ターン([A-Z]+)(\d+)回復。?$", "毎ターン$1を$2回復する。");
        text = Rule(@"^自身は毎ターン開始時(\d+)の([A-Z]+)を回復する。?$", "毎ターン開始時、自身の$2を$1回復する。");
        text = Rule(@"^毎ターン開始時、自身の([A-Z]+)を(\d+)回復し、\n自身に付与されている全ての状態を解除する。?$",
            "毎ターン開始時、自身の$1を$2回復する。\n毎ターン開始時、自身に付与された状態を全て解除する。");
        text = Rule(@"^(敵キャラクターの行動が終了する度に、)([A-Z]+)(\d+)回復。?$", "$1$2を$3回復する。");
        text = Rule(@"^([火水風電冷土光闇])属性威力によるダメージを受けない。?$", "自身に対する$1の属性威力による被ダメージを0にする。");
        text = Rule(@"^〈([^〉]+)〉からの被ダメージを([^。]+)軽減する。?$", "自身が受ける〈$1〉による被ダメージは$2減少する。");
        text = Rule(@"^自身への〈([^〉]+)〉(?:による効果を無効にする|は無効化される)。?$", "自身はあらゆる〈$1〉からの効果を受けない。");
        text = Rule(@"^自身への〈([^〉]+)〉以外からの状態付与を無効化する。?》?$", "自身に対する〈$1〉を除くあらゆる状態付与は無効化される。");
        text = Rule(@"^自身への〈([^〉]+)〉及び〈([^〉]+)〉状態の付与を無効にする。?$", "自身は〈$1, $2〉状態を無効にする。");
        text = Rule(@"^自身は〈([^〉]+)〉または〈([^〉]+)〉以外によって", "自身は〈$1, $2〉以外によって");
        text = Rule(@"^(《[^》]+》が無効なら、)?自身の\[?([一-龯]+B)\]?は([0-9.]+)、\[?([一-龯]+B)\]?は([0-9.]+)になる。?$",
            "$1自身の$2は$3になる。\n$1自身の$4は$5になる。");
        text = Rule(@"^(自身に状態を付与される時、[^ ]+) (?:目標値)?(\d+)の?行為判定を行う。?$", "$1 目標値$2の行為判定を行う。");
        text = Rule(@"^成功した場合、自身に付与された〈([^〉]+)〉状態を解除する。?$", "成功した場合、自身に付与された〈$1〉状態を全て解除する。");
        text = Rule(@"^自身が受ける〈([^〉]+)〉による被ダメージは×([0-9.]+)倍される。?$", "自身の〈$1〉による被ダメージは×$2倍される。");
        return text;
    }
}
