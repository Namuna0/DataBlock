using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class EnemyTextConverter
{
    private static EnemyDropDefinition ParseDrop(string text)
    {
        Match match = Match(text, @"^[-－]\s*([0-9]+)\s*[～~]\s*([0-9]+)\s*[：:]\s*《([^》]+)》\s*[×xX]\s*([0-9]+)を獲得。?$");
        if (!match.Success) throw new InvalidOperationException("ドロップは「- 最小～最大：《アイテム》×個数を獲得」の形式です。");
        return new EnemyDropDefinition
        {
            Minimum = Number(match.Groups[1].Value, 1, "ドロップ範囲の最小値"),
            Maximum = Number(match.Groups[2].Value, 1, "ドロップ範囲の最大値"),
            ItemName = Need(match.Groups[3].Value, "ドロップアイテム名"),
            Amount = Number(match.Groups[4].Value, 1, "ドロップ個数")
        };
    }

    private static List<EnemyDropDefinition> OrderedDrops(EnemyDefinition enemy)
    {
        const int sides = 100;
        if (enemy.Drops == null || enemy.Drops.Count == 0)
            throw new InvalidOperationException("ドロップ定義がありません。");
        if (enemy.Drops.Any(x => x == null)) throw new InvalidOperationException("ドロップ定義にnullがあります。");

        List<EnemyDropDefinition> ordered = enemy.Drops.OrderBy(x => x.Minimum).ThenBy(x => x.Maximum).ToList();
        EnemyDropDefinition previous = null;
        foreach (EnemyDropDefinition drop in ordered)
        {
            if (drop.Minimum < 1 || drop.Maximum < drop.Minimum)
                throw new InvalidOperationException("ドロップ範囲は1以上かつ最小値以下ではない最大値にしてください。");
            if (drop.Maximum > sides)
                throw new InvalidOperationException("ドロップ範囲が1d" + sides + "の最大値を超えています。");
            if (drop.Amount < 1) throw new InvalidOperationException("ドロップ個数は1以上にしてください。");
            Token(drop.ItemName, "ドロップアイテム名", "《》");
            if (previous != null && drop.Minimum <= previous.Maximum)
                throw new InvalidOperationException("ドロップ範囲が重複しています：" + previous.Minimum + "～" + previous.Maximum + " / " + drop.Minimum + "～" + drop.Maximum);
            previous = drop;
        }
        return ordered;
    }

    private static string DropText(EnemyDropDefinition drop)
    {
        return "- " + drop.Minimum + "～" + drop.Maximum + "：《" + Token(drop.ItemName, "ドロップアイテム名", "《》") + "》×" + drop.Amount + "を獲得";
    }
}
