using System;
using System.Collections.Generic;
using System.Linq;

public static partial class SkillTextConverter
{
    private static string[] PrepareBeastSecondLines(string[] source)
    {
        var lines = source.ToList();
        var result = new List<string>();
        bool flavor = false;
        string entity = "";
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
            line = line.Replace("、属性威力は", "属性威力は");
            // A missing 効果 is accepted only for an explicit enchantment definition.
            line = RegexReplace(line, @"^静謐〈エンチャント〉[：:]", "静謐効果〈エンチャント〉：");
            line = RegexReplace(line, @"^●(自身または対象の《[^》]+》状態が解除されたとき、)", "*$1");
            line = RegexReplace(line, @"([0-9]+ターンの間)(暴走)状態になる", "$1《$2》状態になる");

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
            line = RegexReplace(line, @"このターン〈([^〉]+)〉または〈([^〉]+)〉を含むスキルのみ宣言可能。", "戦闘開始1ターン目は〈$1〉または〈$2〉を含むスキルのみ宣言可能。");
            line = line.Replace("出来ない, さらに〈移動〉及び〈回避〉を宣言する事が出来ない。", "出来ない。\n自身は〈移動〉を宣言する事が出来ない。\n自身は〈回避〉を宣言する事が出来ない。");
            line = RegexReplace(line, @"エリアに〈([^〉]+)〉が含まれている時、\[([^\]]+)\]が×([0-9.]+)倍され、\[([^\]]+)\]による行為判定の達成値は([+-][0-9]+)される。", "エリアに〈$1〉が含まれている時、[$2]が×$3倍される。\nエリアに〈$1〉が含まれている時、[$4]による行為判定の達成値は$5される。");
            line = RegexReplace(line, @"自身の防御点は(\+.+?)加算される。", "自身の防御点$1");
            line = line.Replace("されます。", "される。");
            line = RegexReplace(line, @"毎ターンACT-(.+?)減少, スキルによるSPとMPの消費量が×(.+?)倍される。", "毎ターン開始時、自身はACTを$1消費する。\nスキルによるSPの消費量が×$2倍される。\nスキルによるMPの消費量が×$2倍される。");
            line = RegexReplace(line, @"毎ターンSPとMPが(.+?)回復する。", "毎ターン開始時、自身はSPを$1回復する。\n毎ターン開始時、自身はMPを$1回復する。");
            line = RegexReplace(line, @"自身の〈([^〉]+)〉による行為判定の目標値は([+-][0-9]+)加算され、自身のターン開始時にACT-([0-9]+)及びSP-([0-9]+)。", "自身の〈$1〉による行為判定の目標値は$2加算される。\n自身のターン開始時、自身はACTを$3消費する。\n自身のターン開始時、自身はSPを$4消費する。");
            line = RegexReplace(line, @"自身のターン開始時に対象を(.+?)の威力で攻撃する。", "付与者のターン開始時に付与者がこの状態の対象を$1の威力で攻撃する。（付与者の現在のステータスを参照）");
            line = RegexReplace(line, @"自身のターン開始時にHP最大値の([0-9]+)%のダメージを受けます。", "自身は自身のターン開始時に[HP最大値]*$1/100のダメージを受ける。");
            line = line.Replace("自身はHP及びMPを回復することができなくなる。", "自身はHPを回復することができなくなる。\n自身はMPを回復することができなくなる。");
            result.AddRange(line.Split('\n'));
        }
        return result.ToArray();
    }

    private static bool ReadBeastSecondConditions(ConditionSet set, string text)
    {
        var m = M(text.Trim(), @"^自身が〈([^〉]+)状態〉かつ〈([^〉]+)〉を受けた時、その対象へ宣言可能。?$");
        if (m.Success)
        {
            set.And.Add(Condition(ConditionType.OwnState, m.Groups[1].Value));
            set.And.Add(Condition(ConditionType.ReactionTarget, m.Groups[2].Value, "Self", "Source"));
            return true;
        }
        // The canonical combined condition retains the reaction's comma.
        m = M(text.Trim(), @"^自身が《([^》]+)》状態の時[,、]\s*(自身が〈[^〉]+〉を受けた時、その対象へ(?:宣言可能。)?)$");
        if (m.Success)
        {
            set.And.Add(Condition(ConditionType.OwnState, m.Groups[1].Value));
            return ReadReactionCondition(set, m.Groups[2].Value);
        }
        return false;
    }
    private static ConditionEntry ReadBeastSecondCondition(string text)
    {
        var own = M(text.TrimEnd('。'), @"^自身が《([^》]+)》状態の時$");
        if (own.Success) return Condition(ConditionType.OwnState, own.Groups[1].Value);
        var m = M(text.TrimEnd('。'), @"^エリアに〈([^〉]+)〉が(?:含まれない事|含まれていない時)$");
        if (m.Success) return Condition(ConditionType.AreaWithoutCategory, m.Groups[1].Value);
        m = M(text.TrimEnd('。'), @"^《([^》]+)》状態のキャラクターを([0-9]+)体選択$");
        if (m.Success) return Condition(ConditionType.SelectCharactersWithState, m.Groups[1].Value, m.Groups[2].Value);
        m = M(text.TrimEnd('。'), @"^〈([^〉]+)〉を発動した敵キャラクターを対象に自動発動$");
        if (m.Success) return Condition(ConditionType.AutomaticEnemyAction, m.Groups[1].Value);
        return null;
    }
    private static string BeastSecondConditionText(ConditionEntry item)
    {
        if (item == null) return null;
        switch (item.Type)
        {
            case ConditionType.OwnState: return "自身が《" + Args(item.Parameters, 1, "OwnState")[0] + "》状態の時";
            case ConditionType.AreaWithoutCategory: return "エリアに〈" + Args(item.Parameters, 1, "AreaWithoutCategory")[0] + "〉が含まれない事";
            case ConditionType.SelectCharactersWithState:
                var p = Args(item.Parameters, 2, "SelectCharactersWithState"); Number(p[1], 1, "人数");
                return "《" + p[0] + "》状態のキャラクターを" + p[1] + "体選択";
            case ConditionType.AutomaticEnemyAction: return "〈" + Args(item.Parameters, 1, "AutomaticEnemyAction")[0] + "〉を発動した敵キャラクターを対象に自動発動";
            default: return null;
        }
    }
}
