using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static string[] NormalizeWrappedAppliedStateSentences(string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].Replace("〈種族学スキル", "〈種族スキル");
            lines[i] = lines[i].Replace("攻撃攻撃する", "攻撃する").Replace("によるをスキル値", "によるスキル値")
                .Replace("発動ロールの達成値は1d", "発動ロールは1d").Replace("に変化する。。", "に変化する。")
                .Replace("レシピ記載のアイテムを入手する事が出来ます", "レシピ記載のアイテムを入手する事が出来る")
                .Replace("自信を除くキャラクター", "自身を除くキャラクター");
            // Remove only the dangling multiplication operator in a power replacement.
            lines[i] = RegexReplace(lines[i], @"(このアクティブ効果による威力は[^\r\n]+)\*に変化する", "$1に変化する");
            if (i + 1 < lines.Length && lines[i].Contains("によって相手を《") && lines[i].TrimEnd().EndsWith("状態にした時、", StringComparison.Ordinal))
            { lines[i] += lines[i + 1].Trim(); lines[i + 1] = ""; }
        }
        return lines;
    }

    private static Match RewardTableHeader(string line, string firstEntry)
    {
        var header = M(line, @"^【(?!発動ロール】)([^【】]+ロール)】([1-9][0-9]*d[1-9][0-9]*)$");
        return header.Success && M(firstEntry, @"^[0-9]+～[0-9]+：《[^》]+》×[0-9]+$").Success ? header : Match.Empty;
    }

    private static string[] NormalizeStructuralSkillLines(string[] source)
    {
        var lines = source.ToList();
        var result = new List<string>();

        string entity = "";
        string battleTurn = null;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            line = line.Replace("自身のはあらゆる宣言", "自身はあらゆる宣言")
                .Replace("【特性】", "【パッシブ効果】").Replace("による与ダメージは", "によるダメージは")
                .Replace("効果適応後", "効果適用後");
            line = RegexReplace(line, @"^((?:【パッシブ効果】)?自身の\[[^\]]+B\]は)([+-][0-9.]+)される", "$1$2加算される");
            line = RegexReplace(line, @"^自身の([^〈〉《》\[\]]+B)は([0-9.]+)加算される", "自身の$1が+$2加算される");
            line = RegexReplace(line, @"^([A-Z]+)値最大値が([+-][0-9]+)される。?$", "$1最大値$2");
            line = RegexReplace(line, @"^([^\s〈〉《》：。]+)：(?=この状態付与から|\[[^\]]+B\]による行為判定)", "$1効果：");
            line = RegexReplace(line, @"^-\s*《([^》]+)》$", "選択候補の称号は《$1》。");
            line = RegexReplace(line, @"^-\s*(?=[0-9]+スタック：|消費した〈|《[^》]+》を[0-9]+個以上)", "");
            line = RegexReplace(line, @"^\*別の(〈[^〉]+〉へ移動するまで持続。)$", "別の$1");
            line = RegexReplace(line, @"^\*自身が(〈[^〉]+〉)を宣言した時、この状態は解除される。$", "*この状態は自身が$1を宣言した時解除される。");
            line = RegexReplace(line, @"^-(?: )?([火水風電冷土])属性：自身《([^》]+)》(〈[^〉]+〉)：(.+)$", "【アクティブ効果】$1属性を選んだ時、自身は《$2》状態になる。\n$2効果$3：$4");
            line = RegexReplace(line, @"による〈([^〈〉《》]+)》スタック", "による《$1》スタック");
            line = RegexReplace(line, @"^([^：]+効果(?:〈[^〉]+〉)?：)〈([^〉]+)〉を装備している物と同等", "$1自身は〈$2〉を装備している物と同等");
            line = RegexReplace(line, @"(自身は《[^》]+》スタックを[0-9]+得る。)\(最大([0-9]+)スタック\)", "$1（最大$2スタック）");
            if (i + 1 < lines.Count && M(line, @"(?:スキル効果による威力上昇は、|パッシブ効果による威力上昇は、|を解除、|分軽減して、|効果を宣言する場合、|状態が解除される時。|エンカウントフェーズ。)$").Success &&
                !M(lines[i + 1].Trim(), @"^(?:[【●―]|$)").Success) line += lines[++i].Trim();
            if (i + 1 < lines.Count && line.TrimStart().StartsWith("【発動ロール】") &&
                line.EndsWith(",") && lines[i + 1].StartsWith("または"))
            {
                result.Add(line.TrimEnd(','));
                line = "【宣言効果】" + lines[++i].Trim();
            }
            if (M(line, @"^エリアに〈[^〉]+〉が含まれる時、目標値は").Success) line = "【宣言効果】" + line;
            if (M(line, @"^その後名前を《[^》]+》に変更する。$").Success) line = "【アクティブ効果】" + line;
            line = RegexReplace(line, @"^【発動ロール】([^〈〉。]+カード)記載$", "【発動ロール】〈$1〉に記載");
            line = RegexReplace(line, @"^・(?=[^\s〈〉《》【】。]+〈[^〉]+〉[：:])", "");
            line = RegexReplace(line, @"^((?:【[^】]+】)?自身は)([^〈〉《》。、0-9]+)状態になる。$", "$1《$2》状態になる。");
            line = RegexReplace(line, @"^(数例[0-9]+：)([0-9]+),([0-9]{3})ジルダ", "$1$2$3ジルダ");
            line = RegexReplace(line, @"(数例[0-9]+：.+?,)  +", "$1 ");
            line = RegexReplace(line, @"^(任意の数字階段[0-9]+桁：)\s+", "$1");
            var reward = i + 1 < lines.Count ? RewardTableHeader(line, lines[i + 1].Trim()) : Match.Empty;
            if (reward.Success) line = "【アクティブ効果】報酬表〈" + reward.Groups[1].Value + "〉のダイスは" + reward.Groups[2].Value + "。";
            line = RegexReplace(line, @"^([0-9]+～[0-9]+)：《([^》]+)》×([0-9]+)$", "出目$1：《$2》×$3を獲得する。");
            line = RegexReplace(line, @"^(- [0-9]+：《[^》]+》)((?:片手|両手|胴体|脚/足|頭|アクセサリー[12]?)(?:, (?:脚/足|アクセサリー2))?)$", "$1装備部位を〈$2〉に変更する。");
            var name = M(line.Trim(), @"^《([^》]+)》$");
            if (name.Success) entity = name.Groups[1].Value;
            // Only mechanical text is repaired; flavor remains byte-for-byte text.
            line = RegexReplace(line, @"^(〈[^〉]+),,+", "$1,");
            line = RegexReplace(line, @"^【(アクティブ効果|カウンター効果)】】", "【$1】");
            line = RegexReplace(line, @"(このアクティブ効果による威力は[^。]+?[0-9])変化する。", "$1に変化する。");
            if (i + 1 < lines.Count && line.EndsWith("、", StringComparison.Ordinal) &&
                line.Contains("による発動ロールの達成値が") && lines[i + 1].TrimStart().StartsWith("自身の被ダメージは"))
                line = line.TrimEnd('、') + "および" + lines[++i].Trim();
            line = line.Replace("【サードスパイクこのアクティブ", "【サードスパイク】このアクティブ")
                .Replace("対象をを", "対象を")
                .Replace("更に", "さらに").Replace("１ターン", "1ターン")
                .Replace("自身に〈移動〉を発動した対象", "自身へ〈移動〉を発動した対象");
            line = RegexReplace(line, @"(【(?:セカンド|サード)スパイク】このアクティブ効果による)(?=[0-9])", "$1威力は");
            // A missing 効果 is accepted only for an explicit enchantment definition.
            line = RegexReplace(line, @"^([^\s・\-〈〉《》【】。][^〈〉《》【】。]*?)(?<!効果)(〈[^〉]+〉)[：:]", "$1効果$2：");
            line = RegexReplace(line, @"^([^〈〉《》【】。]+効果〈[^〉]+〉)(?=対象は|自身は)", "$1：");
            if (line.Contains("自身が以下のいずれかの種族である") && i + 1 < lines.Count)
            {
                var races = M(lines[i + 1].Trim(), @"^〈([^〉]+)〉,\s*《([^》]+)》$");
                if (races.Success)
                {
                    line = RegexReplace(line, @"自身が以下のいずれかの種族である(?:事|こと)。?$", "自身の種族が〈" + races.Groups[1].Value + "〉のいずれか、または《" + races.Groups[2].Value + "》であること");
                    i++;
                }
            }
            if (i + 1 < lines.Count && lines[i + 1].Trim().Length > 0 &&
                M(line, @"(?:を宣言する時、|を装備している時、|を発動する場合、|を装備しているなら、|状態を付与してい?ない時、|状態を付与している時、|かつ〈[^〉]+〉の場合、|装備している物と同等の扱いとなり、|ジルダと|のターン開始時|の1ターン目、|以下の時、|以外なら、|戦闘を行う場合、|ファンブルとして扱われ、|このアクティブ効果による(?:与)?ダメージは|重症ロールを行う時。)$").Success &&
                !M(lines[i + 1].Trim(), @"^[【《●*―]").Success)
                line += lines[++i].Trim();
            if (i + 1 < lines.Count && (lines[i + 1].Trim().StartsWith("スキルによって上記の武器") ||
                lines[i + 1].Trim().StartsWith("によるダイスロールで")))
                line += lines[++i].Trim();
            line = line.Replace("【【宣言条件】", "【宣言条件】").Replace("達成値が低い側となる", "達成値が最も低い値となる");
            line = RegexReplace(line, @"できなくなる、$", "できなくなる。");
            line = RegexReplace(line, @"(。自身は)([^〈〉《》。、0-9]+)状態になる。$", "$1《$2》状態になる。");
            // A stray closing parenthesis after the unit does not change the
            // explicitly grouped weapon-plus-element expression.
            line = RegexReplace(line, @"(対象を\(スキル値[0-9]+の武器威力\+[0-9]+\*\[[^\]]+属性B\]\)\*[0-9.]+の属性威力)\)で攻撃する", "$1で攻撃する");
            line = RegexReplace(line, @"^●自身が〈([^〉]+)〉を宣言した時、この状態は解除される。$", "*この状態は自身が〈$1〉を宣言した時解除される。");
            if (line.Contains("によって選択したキャラクターと接近状態になった時、") && i + 2 < lines.Count &&
                lines[i + 1].Trim().StartsWith("または、そのキャラクターが《") && lines[i + 2].Trim() == "この状態は解除される。")
                line += lines[++i].Trim() + lines[++i].Trim();
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

            // The second sentence identifies the other member of the same state pair.
            if (i + 1 < lines.Count)
            {
                string next = lines[i + 1].Trim();
                if (next.StartsWith("選択した対象は永続的に《", StringComparison.Ordinal) && M(line, @"自身は永続的に《[^》]+》状態となります。$").Success)
                {
                    line = (line + next).Replace("状態となります。", "状態になる。");
                    i++;
                }
                else if (next.StartsWith("自身が騎乗していたキャラクターの《", StringComparison.Ordinal) && M(line, @"自身は《[^》]+》状態を解除します。$").Success)
                {
                    line = (line + next).Replace("状態を解除します。", "状態を解除する。").Replace("自身が騎乗していたキャラクターの《", "その状態で結び付いていた相手の《");
                    i++;
                }
            }

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
            line = RegexReplace(line, @"毎ターン([A-Z]+)-(.+?)減少, スキルによる([A-Z]+)と([A-Z]+)の消費量が×(.+?)倍される。", "毎ターン開始時、自身は$1を$2消費する。\nスキルによる$3の消費量が×$5倍される。\nスキルによる$4の消費量が×$5倍される。");
            line = RegexReplace(line, @"毎ターン([A-Z]+)と([A-Z]+)が(.+?)回復する。", "毎ターン開始時、自身は$1を$3回復する。\n毎ターン開始時、自身は$2を$3回復する。");
            line = RegexReplace(line, @"自身のターン開始時にHP最大値の([0-9]+)%のダメージを受けます。", "自身は自身のターン開始時に[HP最大値]*$1/100のダメージを受ける。");
            result.AddRange(line.Split('\n'));
        }
        return result.ToArray();
    }

    private static string[] NormalizeCombinedStackSentences(string[] source)
    {
        var result = new List<string>();

        const string stackCost = @"このスキルによる消費([A-Z]+)及び消費([A-Z]+)は自身の《([^》]+)》スタック1つにつき-([0-9]+)される。";
        var costPairs = new HashSet<(string, string)>();
        var minimumPairs = new HashSet<(string, string)>();
        for (int i = 0; i < source.Length; i++)
        {
            string line = source[i];
            var minimum = M(line.Trim(), @"^※消費([A-Z]+)及び([A-Z]+)は0以下にはならない。$");
            if (minimum.Success)
            {
                minimumPairs.Add((minimum.Groups[1].Value, minimum.Groups[2].Value));
                continue;
            }
            if (i + 1 < source.Length && (line.TrimEnd().EndsWith("が足りない時、", StringComparison.Ordinal) || line.TrimEnd().EndsWith("その場にいる場合、", StringComparison.Ordinal)))
                line += source[++i].Trim();
            if (line.Contains("状態の効果を無視してそのキャラクターを使用する事が出来る。") && i + 1 < source.Length && source[i + 1].TrimStart().StartsWith("ただし《", StringComparison.Ordinal))
                line += source[++i].Trim();
            line = RegexReplace(line, @"このスキルの消費リソースの([A-Z]+)が足りない時、可能な限り\1を支払い不足分を次のターン開始時に消費する事でアクティブ効果を発動できる。",
                "このスキルの消費リソースの$1が足りない時、可能な限り支払い、不足分を自身の次のターン開始時に消費する事でアクティブ効果を発動できる。");
            var cost = M(line, stackCost);
            if (cost.Success)
            {
                costPairs.Add((cost.Groups[1].Value, cost.Groups[2].Value));
                line = RegexReplace(line, stackCost,
                    "このスキルの消費$1は自身の《$3》スタック1つにつき-$4される。（最低消費1）\nこのスキルの消費$2は自身の《$3》スタック1つにつき-$4される。（最低消費1）");
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
        Require(costPairs.SetEquals(minimumPairs));
        return result.ToArray();
    }

    private static string[] NormalizeWrappedMappings(string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = RegexReplace(lines[i], @"（([^（）：〈〉《》【】。,、]+)〉(?=の(?:発動時|威力))", "〈$1〉").Replace("ここのパッシブ", "このパッシブ");
            if (i + 1 < lines.Length && ((lines[i].Contains("複数の〈") && lines[i].TrimEnd().EndsWith("を発動させた場合、", StringComparison.Ordinal)) ||
                (lines[i].Contains("次のライフパスを一つ選択") && lines[i + 1].TrimStart().StartsWith("《", StringComparison.Ordinal))))
            { lines[i] += lines[i + 1].Trim(); lines[i + 1] = ""; }
        }
        return lines;
    }

    private static string[] NormalizeCombinedResourceSentences(string[] source)
    {
        var result = new List<string>();
        foreach (string raw in source)
        {
            string line = raw;
            line = line.Replace("さらに、", "さらに");
            line = RegexReplace(line, @"^((?:【パッシブ効果】)?)([A-Z]+が[0-9]+以下になった時、.+?ロールを行う必要がない。)$", "$1自身は$2");
            line = RegexReplace(line, @"自身は([A-Z]+)が存在せず《([^》]+)》状態にならない。", "自身は$1が存在しない。\n自身は《$2》状態にならない。");
            line = RegexReplace(line, @"自身[はの]([A-Z]+)(?:と|及び)([A-Z]+)を回復する(?:事が出来|ことができ)なくなる。", "自身は$1を回復することができなくなる。\n自身は$2を回復することができなくなる。");
            line = RegexReplace(line, @"自身は《([^》]+)》による効果を受けない。", "特性《$1》による効果をすべて無効にする。");
            line = RegexReplace(line, @"環境〈([^〉]+)〉および〈([^〉]+)〉による環境効果を受けない。", "環境《$1》および《$2》による環境効果を受けない。");
            line = RegexReplace(line, @"自身は自身のターン開始時([0-9]+)ダメージを受ける。", "自身は自身のターン開始時に$1のダメージを受ける。");
            line = RegexReplace(line, @"自身の\s*((?:\[[^\]]+\])(?:及び\[[^\]]+\])*)による達成値が([0-9.]+)倍される。", "$1による行為判定の達成値が×$2倍される。");
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
        for (int i = 0; i < lines.Count; i++)
        {
            lines[i] = lines[i].Replace("》態になる", "》状態になる").Replace("SANダメ―ジ", "SANダメージ");
            // Unwrapped, canonical lines and flavor are deliberately left intact.
            if (i + 1 < lines.Count && (lines[i].TrimEnd().EndsWith("発動ロールを行う際、", StringComparison.Ordinal) ||
                lines[i].TrimEnd().EndsWith("を発動する際、", StringComparison.Ordinal) ||
                (lines[i].Contains("かつ自身と対象が") && lines[i].TrimEnd().EndsWith("状態の時、", StringComparison.Ordinal))))
            { lines[i] += lines[i + 1].Trim(); lines.RemoveAt(i + 1); i--; continue; }
        }
        return lines.ToArray();
    }


    // Two separately declared rolls, the second immediately followed by roleplay,
    // describe independent uses. Keep each use's costs and conditions on its own body.
    private static string[] SplitCombatAndRoleplayRolls(string[] source)
    {
        var lines = source.ToList();
        if (lines.Any(line => line.TrimStart().StartsWith("●"))) return source;
        var rolls = Enumerable.Range(0, lines.Count).Where(i => lines[i].TrimStart().StartsWith("【発動ロール】")).ToArray();
        if (rolls.Length != 2 || rolls[1] + 1 >= lines.Count || !lines[rolls[1] + 1].TrimStart().StartsWith("【ロールプレイ効果】"))
            return source;
        int first = lines.FindIndex(line => M(line.Trim(), @"^【(?:宣言条件|消費リソース|発動ロール)】").Success);
        int second = lines.FindLastIndex(rolls[1], line => line.TrimStart().StartsWith("【宣言条件】"));
        if (second <= rolls[0] || !lines.Skip(rolls[0]).Take(second - rolls[0]).Any(line => line.TrimStart().StartsWith("【アクティブ効果】"))) return source;
        lines.Insert(second, "●ロールプレイ");
        lines.Insert(first, "●戦闘");
        lines.Insert(first, "【説明】" + Description);
        return lines.ToArray();
    }

    private static string[] PrepareSkillLines(string[] source)
    {
        int end = Array.FindIndex(source, line => M(line.Trim(), @"^―{8,}$").Success);
        string[] rules = end < 0 ? source : source.Take(end).ToArray();
        rules = SplitCombatAndRoleplayRolls(rules);
        rules = NormalizeWrappedAppliedStateSentences(NormalizeWrappedEffectSentences(NormalizeWrappedMappings(
            NormalizeStructuralSkillLines(NormalizeCombinedResourceSentences(NormalizeCombinedStackSentences(rules))))));
        return end < 0 ? rules : rules.Concat(source.Skip(end)).ToArray();
    }
}
