using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static partial class EnemyTextConverter
{
    private const string DropRowPattern = @"^[-－]\s*(?:(確定)|([0-9]*)(?:\s*[～~]\s*([0-9]+))?)\s*(?:[：:]|(?=《))\s*(?:([^《：:]+)[：:]\s*)?《([^《》]+)》\s*[×xX]\s*([0-9]+)(?:を獲得)?[。、,.]?$";
    private const string DuelLootText = "決闘の敗者から物を取る事は出来ない。";

    private static bool IsDropHeader(string line) =>
        line.StartsWith("【ドロップロール】", StringComparison.Ordinal) ||
        line.StartsWith("【捕獲ドロップロール】", StringComparison.Ordinal) ||
        (line.StartsWith("-", StringComparison.Ordinal) && line.Contains("を獲得") && Match(line, DropRowPattern).Success);

    private static bool StartsDropTable(string[] lines, int index)
    {
        string line = SyntaxLine(lines[index]);
        if (IsDropHeader(line)) return true;
        if (!line.StartsWith("【宣言条件】", StringComparison.Ordinal)) return false;
        int next = index + 1;
        SkipEmpty(lines, ref next);
        return next < lines.Length && IsDropHeader(SyntaxLine(lines[next]));
    }

    private static void ParseDrops(EnemyDefinition enemy, string[] lines, ref int index)
    {
        string header = SyntaxLine(lines[index]);
        if (header.StartsWith("【宣言条件】", StringComparison.Ordinal))
        {
            Match victory = Match(header, @"^【宣言条件】\s*戦闘開始から([0-9]+)ターン目までに勝利。?$");
            if (!victory.Success) throw LineError(index, lines[index], "未対応のドロップ条件です。");
            enemy.LootVictoryTurnLimit = Number(victory.Groups[1].Value, 1, "勝利期限のターン");
            index++;
            SkipEmpty(lines, ref index);
            header = SyntaxLine(lines[index]);
        }
        enemy.LootKind = EnemyLootKind.Defeat;
        if (header.StartsWith("【", StringComparison.Ordinal))
        {
            string value = header.Substring(header.IndexOf('】') + 1).Trim();
            if (header.StartsWith("【捕獲", StringComparison.Ordinal))
            {
                enemy.LootKind = EnemyLootKind.Capture;
                if (value != "1d100") enemy.CaptureRollFormula = value;
            }
            else if (value.TrimEnd('。') == "無し") enemy.LootKind = EnemyLootKind.None;
            else if (value == "???" || value == "？？？") enemy.LootKind = EnemyLootKind.Undisclosed;
            else if (value == DuelLootText) enemy.LootKind = EnemyLootKind.NoLootFromDuelLoser;
            else if (value.Length > 0 && value != "1d100")
                throw LineError(index, lines[index], "ドロップロールは固定の1d100です。");
            index++;
        }
        for (; index < lines.Length; index++)
        {
            string line = SyntaxLine(lines[index]);
            if (line.Length == 0) continue;
            if (Match(line, @"^―{8,}").Success) break;
            Match m = Match(line, DropRowPattern);
            if (!m.Success || (!m.Groups[1].Success && m.Groups[2].Length == 0 && !m.Groups[3].Success))
                throw LineError(index, lines[index], "ドロップの範囲・アイテム名・個数を確認してください。");
            var drop = new EnemyDropDefinition {
                ResultName = m.Groups[4].Value.Trim(), ItemName = m.Groups[5].Value,
                Amount = Number(m.Groups[6].Value, 1, "ドロップ個数")
            };
            if (!m.Groups[1].Success)
            {
                if (m.Groups[2].Length > 0) drop.Minimum = Number(m.Groups[2].Value, 1, "ドロップ範囲の最小値");
                drop.Maximum = m.Groups[3].Success ? Number(m.Groups[3].Value, 1, "ドロップ範囲の最大値") : drop.Minimum;
            }
            enemy.Drops.Add(drop);
        }
    }

    private static void ValidateDrops(EnemyDefinition enemy)
    {
        if (!Enum.IsDefined(typeof(EnemyLootKind), enemy.LootKind) || enemy.Drops == null || enemy.Drops.Any(x => x == null))
            throw new InvalidOperationException("戦利品の種類またはドロップ表が不正です。");
        if (enemy.LootVictoryTurnLimit < 0 || (enemy.LootVictoryTurnLimit > 0 && enemy.LootKind != EnemyLootKind.Defeat))
            throw new InvalidOperationException("勝利期限は撃破ドロップに対して正のターン数で指定してください。");
        bool capture = enemy.LootKind == EnemyLootKind.Capture;
        if (!string.IsNullOrEmpty(enemy.CaptureRollFormula) &&
            (!capture || !Match(enemy.CaptureRollFormula, @"^1d100(?:[*/+-](?:\[[^\[\]。\r\n]+\]|[0-9]+(?:\.[0-9]+)?))+$").Success))
            throw new InvalidOperationException("捕獲ロールには1d100から始まる補正式を指定してください。");
        if (!capture && enemy.LootKind != EnemyLootKind.Defeat && enemy.Drops.Count != 0)
            throw new InvalidOperationException("戦利品の指定とドロップ表が矛盾しています。");
        int last = -1;
        foreach (var drop in enemy.Drops.OrderBy(x => x.Minimum).ThenBy(x => x.Maximum))
        {
            Token(drop.ItemName, "ドロップアイテム名", "《》");
            if (!string.IsNullOrEmpty(drop.ResultName)) Token(drop.ResultName, "ドロップ結果名", "：:《》");
            if (drop.Amount < 1) throw new InvalidOperationException("ドロップ個数は1以上にしてください。");
            if (drop.Minimum < 0 || drop.Maximum < drop.Minimum)
                throw new InvalidOperationException("ドロップ範囲の上下限が不正です。");
            if (drop.Minimum == 0 && drop.Maximum == 0) continue;
            if (string.IsNullOrEmpty(enemy.CaptureRollFormula) && drop.Maximum > 100)
                throw new InvalidOperationException("ドロップの最大値が1d100を超えています。");
            if (drop.Minimum <= last) throw new InvalidOperationException("ドロップ範囲が重複しています。");
            last = drop.Maximum;
        }
    }

    private static void WriteDrops(StringBuilder builder, EnemyDefinition enemy)
    {
        if (enemy.LootKind == EnemyLootKind.Unspecified) return;
        builder.AppendLine();
        if (enemy.LootVictoryTurnLimit > 0)
            builder.AppendLine("【宣言条件】戦闘開始から" + enemy.LootVictoryTurnLimit + "ターン目までに勝利");
        string label = enemy.LootKind == EnemyLootKind.Capture ? "捕獲ドロップロール" : "ドロップロール";
        string value = enemy.LootKind switch
        {
            EnemyLootKind.None => "無し",
            EnemyLootKind.Undisclosed => "???",
            EnemyLootKind.NoLootFromDuelLoser => DuelLootText,
            EnemyLootKind.Capture when !string.IsNullOrEmpty(enemy.CaptureRollFormula) => enemy.CaptureRollFormula,
            _ => "1d100"
        };
        builder.AppendLine("【" + label + "】" + value);
        foreach (var drop in enemy.Drops.OrderBy(x => x.Minimum).ThenBy(x => x.Maximum))
        {
            string range = drop.Maximum == 0 ? "確定" : drop.Minimum == drop.Maximum ? drop.Maximum.ToString() :
                (drop.Minimum == 0 ? "" : drop.Minimum.ToString()) + "～" + drop.Maximum;
            string result = string.IsNullOrEmpty(drop.ResultName) ? "" : drop.ResultName + "：";
            builder.AppendLine("- " + range + "：" + result + "《" + drop.ItemName + "》×" + drop.Amount + "を獲得");
        }
    }
}
