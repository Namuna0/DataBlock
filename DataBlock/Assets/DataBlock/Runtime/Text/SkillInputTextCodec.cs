using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static string[] NormalizeWrappedAppliedStateSentences(string[] lines)
    {
        bool flavor = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (M(lines[i].Trim(), @"^―{8,}$").Success) flavor = true;
            if (flavor) continue;
            lines[i] = lines[i].Replace("〈種族学スキル", "〈種族スキル");
            // Remove only the dangling multiplication operator in a power replacement.
            lines[i] = RegexReplace(lines[i], @"(このアクティブ効果による威力は[^\r\n]+)\*に変化する", "$1に変化する");
            if (i + 1 < lines.Length && lines[i].Contains("によって相手を《") && lines[i].TrimEnd().EndsWith("状態にした時、", StringComparison.Ordinal))
            { lines[i] += lines[i + 1].Trim(); lines[i + 1] = ""; }
        }
        return lines;
    }

    private static string[] NormalizeStructuralSkillLines(string[] source)
    {
        var lines = source.ToList();
        var result = new List<string>();
        bool flavor = false;
        string entity = "";
        string battleTurn = null;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (M(line.Trim(), @"^―{8,}$").Success) flavor = true;
            if (flavor) { result.Add(line); continue; }
            var name = M(line.Trim(), @"^《([^》]+)》$");
            if (name.Success) entity = name.Groups[1].Value;
            // Only mechanical text is repaired; flavor remains byte-for-byte text.
            line = line.Replace("【サードスパイクこのアクティブ", "【サードスパイク】このアクティブ")
                .Replace("さらに対象をを", "さらに対象を").Replace("対象をを", "対象を")
                .Replace("更に", "さらに").Replace("１ターン", "1ターン")
                .Replace("自身に〈移動〉を発動した対象", "自身へ〈移動〉を発動した対象");
            line = RegexReplace(line, @"(【(?:セカンド|サード)スパイク】このアクティブ効果による)(?=[0-9])", "$1威力は");
            // A missing 効果 is accepted only for an explicit enchantment definition.
            line = RegexReplace(line, @"^([^〈〉《》【】。]+?)(?<!効果)(〈[^〉]+〉)[：:]", "$1効果$2：");
            line = RegexReplace(line, @"^●(自身または対象の《[^》]+》状態が解除されたとき、)", "*$1");
            line = RegexReplace(line, @"([0-9]+ターンの間)([^《》。\r\n]+?)状態になる", "$1《$2》状態になる");

            if (i + 1 < lines.Count &&
                (line.TrimEnd().EndsWith("フェーズ、", StringComparison.Ordinal) ||
                 line.TrimEnd().EndsWith("を発動する際、", StringComparison.Ordinal) ||
                 M(line, @"戦闘中[0-9]+度だけ、$").Success ||
                 M(line, @"体以上のキャラクターを選択するスキルは$").Success ||
                 (line.Contains("の属性威力が追加される。") && lines[i + 1].Trim().StartsWith("（加算値は", StringComparison.Ordinal))))
                line += lines[++i].Trim();
            if (line.Contains("回カウンター効果を宣言する事が出来る。") && i + 1 < lines.Count &&
                lines[i + 1].Trim().StartsWith("この宣言は同一対象へ", StringComparison.Ordinal)) line += lines[++i].Trim();

            if (line.Trim() == "攻撃が命中した時、自属性に応じた追加効果を適用する。")
            {
                Require(i + 1 < lines.Count && M(lines[i + 1].Trim(), @"^- [火水風電冷土]属性：").Success);
                continue; // The condition is stored on every typed branch below.
            }
            line = RegexReplace(line, @"^- ([火水風電冷土])属性：対象に《([^》]+)》状態を([0-9]+)ターン付与します。$", "この攻撃が命中した時、自属性が$1属性なら対象に《$2》状態を$3ターン付与する。");
            line = RegexReplace(line, @"^- ([火水風電冷土])属性：対象の《([^》]+)》状態を解除します。$", "この攻撃が命中した時、自属性が$1属性なら対象の《$2》状態を解除する。");
            line = RegexReplace(line, @"^※自属性が存在しない時(.+?)のみの威力となる。$", "自属性が存在しない時、属性威力と自属性の追加効果を適用せず、$1のみの威力となる。");
            var removal = M(line.Trim(), @"^発動後《([^》]+)》は除去される。$");
            if (removal.Success)
            {
                Require(removal.Groups[1].Value == entity);
                line = "【宣言効果】スキルの処理後にこの罠は除去される。";
            }
            var turn = M(line, @"戦闘開始([0-9]+)ターン目、");
            if (turn.Success) battleTurn = turn.Groups[1].Value;
            if (line.Contains("このターン〈"))
            {
                Require(battleTurn != null);
                line = RegexReplace(line, @"このターン〈([^〉]+)〉または〈([^〉]+)〉を含むスキルのみ宣言可能。", "戦闘開始" + battleTurn + "ターン目は〈$1〉または〈$2〉を含むスキルのみ宣言可能。");
            }
            line = RegexReplace(line, @"出来ない, さらに〈([^〉]+)〉及び〈([^〉]+)〉を宣言する事が出来ない。", "出来ない。\n自身は〈$1〉を宣言する事が出来ない。\n自身は〈$2〉を宣言する事が出来ない。");
            line = RegexReplace(line, @"エリアに〈([^〉]+)〉が含まれている時、\[([^\]]+)\]が×([0-9.]+)倍され、\[([^\]]+)\]による行為判定の達成値は([+-][0-9]+)される。", "エリアに〈$1〉が含まれている時、[$2]が×$3倍される。\nエリアに〈$1〉が含まれている時、[$4]による行為判定の達成値は$5される。");
            line = RegexReplace(line, @"自身の防御点は(\+.+?)加算される。", "自身の防御点$1");
            line = line.Replace("されます。", "される。");
            line = RegexReplace(line, @"毎ターンACT-(.+?)減少, スキルによるSPとMPの消費量が×(.+?)倍される。", "毎ターン開始時、自身はACTを$1消費する。\nスキルによるSPの消費量が×$2倍される。\nスキルによるMPの消費量が×$2倍される。");
            line = RegexReplace(line, @"毎ターンSPとMPが(.+?)回復する。", "毎ターン開始時、自身はSPを$1回復する。\n毎ターン開始時、自身はMPを$1回復する。");
            line = RegexReplace(line, @"自身の〈([^〉]+)〉による行為判定の目標値は([+-][0-9]+)加算され、自身のターン開始時にACT-([0-9]+)及びSP-([0-9]+)。", "自身の〈$1〉による行為判定の目標値は$2加算される。\n自身のターン開始時、自身はACTを$3消費する。\n自身のターン開始時、自身はSPを$4消費する。");
            line = RegexReplace(line, @"自身のターン開始時にHP最大値の([0-9]+)%のダメージを受けます。", "自身は自身のターン開始時に[HP最大値]*$1/100のダメージを受ける。");
            line = line.Replace("自身はHP及びMPを回復することができなくなる。", "自身はHPを回復することができなくなる。\n自身はMPを回復することができなくなる。");
            result.AddRange(line.Split('\n'));
        }
        return result.ToArray();
    }

    private static string[] NormalizeCombinedStackSentences(string[] source)
    {
        var result = new List<string>();
        bool flavor = false;
        bool minimumOne = source.Any(x => x.Trim() == "※消費ACT及びMPは0以下にはならない。");
        bool costRule = source.Any(x => x.Contains("このスキルによる消費ACT及び消費MPは自身の《"));
        for (int i = 0; i < source.Length; i++)
        {
            string line = source[i];
            if (M(line.Trim(), @"^―{8,}$").Success) flavor = true;
            if (flavor) { result.Add(line); continue; }
            if (line.Trim() == "※消費ACT及びMPは0以下にはならない。") { Require(costRule); continue; }
            if (i + 1 < source.Length && (line.TrimEnd().EndsWith("が足りない時、", StringComparison.Ordinal) || line.TrimEnd().EndsWith("その場にいる場合、", StringComparison.Ordinal)))
                line += source[++i].Trim();
            if (line.Contains("状態の効果を無視してそのキャラクターを使用する事が出来る。") && i + 1 < source.Length && source[i + 1].TrimStart().StartsWith("ただし《", StringComparison.Ordinal))
                line += source[++i].Trim();
            line = RegexReplace(line, @"このスキルの消費リソースの([A-Z]+)が足りない時、可能な限り\1を支払い不足分を次のターン開始時に消費する事でアクティブ効果を発動できる。",
                "このスキルの消費リソースの$1が足りない時、可能な限り支払い、不足分を自身の次のターン開始時に消費する事でアクティブ効果を発動できる。");
            if (line.Contains("このスキルによる消費ACT及び消費MPは自身の《"))
            {
                Require(minimumOne);
                line = RegexReplace(line, @"このスキルによる消費ACT及び消費MPは自身の《([^》]+)》スタック1つにつき-([0-9]+)される。",
                    "このスキルの消費ACTは自身の《$1》スタック1つにつき-$2される。（最低消費1）\nこのスキルの消費MPは自身の《$1》スタック1つにつき-$2される。（最低消費1）");
            }
            line = RegexReplace(line, @"さらに《([^》]+)》スタックが([0-9]+)以上の場合、このスキルの威力は([0-9.]+)倍される。", "自身の《$1》スタックが$2以上の場合、このスキルの威力は$3倍される。");
            line = RegexReplace(line, @"その後、自身の《([^》]+)》スタックは全て除去される。", "このスキルの攻撃後、自身の《$1》スタックは全て除去される。");
            line = RegexReplace(line, @"自身の〈([^〉]+)〉による行為判定の目標値は([+-][0-9]+)加算され、自身のターン開始時に([A-Z]+)-([0-9]+)及び([A-Z]+)-([0-9]+)。",
                "自身の〈$1〉による行為判定の目標値は$2加算される。\n自身のターン開始時、自身は$3を$4消費する。\n自身のターン開始時、自身は$5を$6消費する。");
            line = RegexReplace(line, @"《([^》]+)》状態のキャラクターのプレイヤーがその場にいる場合、《\1》状態の効果を無視してそのキャラクターを使用する事が出来る。ただし《\1》状態は解除できない。",
                "《$1》状態のキャラクターは、そのプレイヤーがその場にいる場合、この状態の効果を無視して使用できる。ただし、この状態は解除できない。");
            if (i + 1 < source.Length && M(line, @"対象へ対象の[A-Z]+最大値の[0-9]+%のダメージを与える。$").Success &&
                source[i + 1].Trim() == "この攻撃による威力及び被ダメージは、防御点、効果、状態によって増加も軽減もされない。")
            {
                line += "（この攻撃による威力及び被ダメージは、防御点、効果、状態によって増加も軽減もされない）";
                i++;
            }
            result.AddRange(line.Split('\n'));
        }
        return result.ToArray();
    }

    private static string[] NormalizeWrappedMappings(string[] lines)
    {
        bool flavor = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (M(lines[i].Trim(), @"^―{8,}$").Success) flavor = true;
            if (flavor) continue;
            lines[i] = lines[i].Replace("（エルフ魔法〉", "〈エルフ魔法〉").Replace("ここのパッシブ", "このパッシブ");
            if (i + 1 < lines.Length && ((lines[i].Contains("複数の〈") && lines[i].TrimEnd().EndsWith("を発動させた場合、", StringComparison.Ordinal)) ||
                (lines[i].Contains("次のライフパスを一つ選択") && lines[i + 1].TrimStart().StartsWith("《", StringComparison.Ordinal))))
            { lines[i] += lines[i + 1].Trim(); lines[i + 1] = ""; }
        }
        return lines;
    }

    private static string[] NormalizeCombinedResourceSentences(string[] source)
    {
        bool flavor = false;
        var result = new List<string>();
        foreach (string raw in source)
        {
            string line = raw;
            if (M(line.Trim(), @"^―{8,}$").Success) flavor = true;
            if (flavor) { result.Add(raw); continue; }
            line = line.Replace("さらに、", "さらに");
            line = RegexReplace(line, @"^([^〈〉《》【】。]+?)(?<!効果)(〈[^〉]+〉)[：:]", "$1効果$2：");
            line = RegexReplace(line, @"^((?:【パッシブ効果】)?)([A-Z]+が[0-9]+以下になった時、.+?ロールを行う必要がない。)$", "$1自身は$2");
            line = RegexReplace(line, @"自身は([A-Z]+)が存在せず《([^》]+)》状態にならない。", "自身は$1が存在しない。\n自身は《$2》状態にならない。");
            line = line.Replace("自身のHPとMPを回復する事が出来なくなる。", "自身はHPを回復することができなくなる。\n自身はMPを回復することができなくなる。");
            line = RegexReplace(line, @"自身は《([^》]+)》による効果を受けない。", "特性《$1》による効果をすべて無効にする。");
            line = RegexReplace(line, @"環境〈([^〉]+)〉および〈([^〉]+)〉による環境効果を受けない。", "環境《$1》および《$2》による環境効果を受けない。");
            line = RegexReplace(line, @"自身は自身のターン開始時([0-9]+)ダメージを受ける。", "自身は自身のターン開始時に$1のダメージを受ける。");
            line = RegexReplace(line, @"自身の\s*((?:\[[^\]]+\])(?:及び\[[^\]]+\])*)による達成値が([0-9.]+)倍される。", "$1による行為判定の達成値が×$2倍される。");
            line = RegexReplace(line, @"(【宣言条件】)自身が《([^》]+)》を受けた時、その対象へ発動。?$", "$1自身が〈$2〉を受けた時、その対象へ宣言可能。");
            line = RegexReplace(line, @"自身への〈([^〉]+)〉を全て無効にする。", "自身への〈$1〉を無効にする。");
            // These two penalties have the same OR selector, but remain separate data.
            line = RegexReplace(line, @"自身は〈([^〉]+)〉および〈([^〉]+)〉を受けた時、被ダメージが×([0-9.]+)倍され、さらに([0-9]+)ターンの間《([^》]+)》状態となる。",
                "自身が〈$1, $2〉のいずれかを受けた時、被ダメージが×$3倍される。\n自身が〈$1, $2〉のいずれかを受けた時、自身は$4ターンの間《$5》状態になる。");
            result.AddRange(line.Split('\n'));
        }
        return result.ToArray();
    }

    private static string[] NormalizeWrappedEffectSentences(string[] source)
    {
        var lines = source.ToList();
        int separator = lines.FindIndex(x => M(x.Trim(), @"^―{8,}$").Success);
        if (separator < 0) separator = lines.Count;
        for (int i = 0; i < separator; i++)
        {
            lines[i] = lines[i].Replace("》態になる", "》状態になる").Replace("SANダメ―ジ", "SANダメージ");
            // Unwrapped, canonical lines and flavor are deliberately left intact.
            if (i + 1 < separator && (lines[i].TrimEnd().EndsWith("発動ロールを行う際、", StringComparison.Ordinal) ||
                lines[i].TrimEnd().EndsWith("を発動する際、", StringComparison.Ordinal) ||
                (lines[i].Contains("かつ自身と対象が") && lines[i].TrimEnd().EndsWith("状態の時、", StringComparison.Ordinal))))
            { lines[i] += lines[i + 1].Trim(); lines.RemoveAt(i + 1); separator--; i--; continue; }
        }
        return lines.ToArray();
    }


    private static string[] PrepareSkillLines(string[] source)
    {
        return NormalizeWrappedAppliedStateSentences(NormalizeWrappedEffectSentences(NormalizeWrappedMappings(
            NormalizeStructuralSkillLines(NormalizeCombinedResourceSentences(NormalizeCombinedStackSentences(source))))));
    }
}
