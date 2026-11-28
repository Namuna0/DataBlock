using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    // 習得・成長・戦闘の常時効果。名称と数値は文型の引数として保存する。
    private static void RegisterProgressionRules(RuleSection rules)
    {
        rules.Add(EffectContentType.ActiveEffectCheckDelta, "自身のアクティブ効果による行為判定の達成値が{0}される。", RuleSignedPattern);
        rules.Add(EffectContentType.EquippedSkillSlotDelta, "自身が装備できるスキル枠は{0}される。", RuleSignedPattern);
        rules.Add(EffectContentType.WeakElementDamageFactor, "属性威力による自身の被ダメージは×[{0}]*{1}倍される。", RuleNamePattern, RuleFactorPattern);
        rules.Add(EffectContentType.TurnStartResourceDelta, "自身のターン開始時、{0}は{1}される。", RuleResourcePattern, RuleSignedPattern);
        rules.Add(EffectContentType.ThresholdRangeExample, "（{0}～{1}ならば{2}～{3}と変化する。）", RulePositivePattern, RulePositivePattern, RulePositivePattern, RulePositivePattern);
        rules.Add(EffectContentType.CriticalMinimumDelta, "行為判定を行う際、クリティカルになるダイスの目が{0}される。", RuleSignedPattern);
        rules.Add(EffectContentType.SpendCreationSlotForSkill, "キャラ作成時に選べる{0}スキル枠を{1}つ消費して、スキル《{2}》を習得する。", RuleNamePattern, RulePositivePattern, RuleNamePattern);
        rules.Add(EffectContentType.ProtectLifePathStat, "自身はライフパス能力値によって{0}が下がらない。", RuleNamePattern);
        rules.Add(EffectContentType.LastPlayerTurnRecovery, "味方のプレイヤーキャラクターが戦闘不能になった事で自分1人の時、自身のターン開始時に{0}を+{1}得る、なおかつ自身の状態を{2}つ解除する事が出来る。", RuleResourcePattern, RulePositivePattern, RulePositivePattern);
        rules.Add(EffectContentType.GainItemOnAcquisition, "ライフパス習得時に{0}《{1}》×{2}を獲得する。", RuleNamePattern, RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.GainDestinySpikes, "ディスティニースパイクを{0}つ得る。", RulePositivePattern);
        rules.Add(EffectContentType.EitherCategoryCheckFactor, "〈{0}〉及び〈{1}〉による行為判定を行う際、達成値が×{2}倍される。", RuleNamePattern, RuleNamePattern, RuleFactorPattern);
        rules.Add(EffectContentType.EitherCategoryCheckDelta, "〈{0}〉及び〈{1}〉による行為判定を行う際、達成値が{2}される。", RuleNamePattern, RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.OptionalPermanentStackForActivation, "自身は〈{0}〉及び《{1}》を宣言する際、任意で《{2}》スタックを{3}つ永続的に付与することで、その発動ロールの達成値を{4}倍にできる。", RuleNamePattern, RuleNamePattern, RuleNamePattern, RulePositivePattern, RuleFactorPattern);
        rules.Add(EffectContentType.OptionalRollFaceAdjustment, "1日{0}回まで、自身は発動ロールのダイスロールの目を{1}または{2}できる。", RulePositivePattern, RuleSignedPattern, RuleSignedPattern);
        rules.Add(EffectContentType.IncreaseEnemyCounterTargetByAllyBuffs, "自身を除く味方から{0}種類以上のバフを受けている時、自身の行為判定に対する敵のカウンター効果の目標値は×{1}倍される。", RulePositivePattern, RuleFactorPattern);
        rules.Add(EffectContentType.CounterAgainstCategoryResultDelta, "自身は〈{0}〉に対するカウンター効果を発動する時、達成値が{1}される。", RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.LevelExperienceCostDelta, "レベル上昇に必要な消費経験値が{0}される。", RuleSignedPattern);
        rules.Add(EffectContentType.BattleCheckBonusPerAlly, "戦闘中、自身の{0}による達成値は自身を除く味方キャラクターの数×{1}上昇する。", RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.OtherAlliesStatResultDelta, "代償に自身を除く味方キャラクター全員の{0}による達成値が{1}される。", RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.LearnClass, "クラス《{0}》を習得する。", RuleNamePattern);
        rules.Add(EffectContentType.SpendSlotForOtherRaceSkill, "キャラ作成時に選べる{0}スキル枠を{1}つ消費して、習得条件が「{2}」と記載されている、自身とは別種族の種族スキルを{3}つ選んで習得する。", RuleNamePattern, RulePositivePattern, RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.ExcludeSharedAndRestrictedRaceSkills, "ただし種族共通スキルと通常選択不可能の種族スキルは選べない。");
        rules.Add(EffectContentType.UseSelectedRaceTravel, "自身は選択種族が移動できるエリアのみ移動可能となる。");
        rules.Add(EffectContentType.UseSelectedRaceClasses, "自身は選択種族が習得できるクラスのみ習得可能となる。");
        rules.Add(EffectContentType.SpendSlotForTaggedSkill, "キャラ作成時に選べる{0}スキル枠を{1}つ消費して、〈{2}〉を含む〈{3}〉を{4}つ選択して習得する。", RuleNamePattern, RulePositivePattern, RuleNamePattern, RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.CategoryActivationResultDelta, "〈{0}〉による発動ロールの達成値が{1}される。", RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.CriticalCategoryIncomingFactor, "自身は相手からクリティカルした〈{0}〉を受ける時、被ダメージが×{1}倍される。", RuleNamePattern, RuleFactorPattern);
        rules.Add(EffectContentType.DangerThresholdCombatBonus, "自身が★（☆{0}）以上のエネミーと戦闘中の時、{1}が{2}加算、さらに〈{3}〉及び〈{4}〉の達成値{5}される。", RulePositivePattern, RuleNamePattern, @"[+-][0-9]+(?:\.[0-9]+)?", RuleNamePattern, RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.TargetedCategoryActivationTargetDelta, "自身に対する〈{0}〉による発動ロールの目標値が{1}される。", RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.AdditionalDropsOnCategoryDefeat, "〈{0}〉カテゴリーのエネミーを倒した時、ドロップロールを{1}回追加で行う。", RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.SelectSkillOnAcquisition, "このライフパス習得時に、あらゆるスキルの中から{0}つ選択する。", RulePositivePattern);
        rules.Add(EffectContentType.SelectedSkillCheckDelta, "選択したスキルの達成値は{0}される。", RuleSignedPattern);
        rules.Add(EffectContentType.OptionalDailyItems, "1日{0}回まで、自身は《{1}》×{2}を得ても良い。", RulePositivePattern, RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.EnemyPartyWipeRewards, "敵プレイヤーのパーティーを全滅させた場合、自身は1日{0}回まで経験値+{1}, 決意+{2}を得る。", RulePositivePattern, RulePositivePattern, RulePositivePattern);
        rules.Add(EffectContentType.ResourceOnCategoryUsePerTurn, "自身は〈{0}〉を発動する際、1ターンに{1}度まで{2}を{3}得る。", RuleNamePattern, RulePositivePattern, RuleResourcePattern, RulePositivePattern);
        rules.Add(EffectContentType.AgainstEitherCategoryTargetDelta, "〈{0}〉または〈{1}〉に対する行為判定の目標値は{2}される。", RuleNamePattern, RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.AddAllyAndEnemyTargets, "自身が〈{0}〉を発動する際、追加で味方{1}体を選択対象にするなら同時に追加で敵{2}体をさらに選択できる（重複可能）", RuleNamePattern, RulePositivePattern, RulePositivePattern);
        rules.Add(EffectContentType.RecoverTwoResourcesOnCategoryFailure, "自身が〈{0}〉及び〈{1}〉に失敗したとき{2}と{3}が{4}回復する。", RuleNamePattern, RuleNamePattern, RuleResourcePattern, RuleResourcePattern, RulePositivePattern);
        rules.Add(EffectContentType.CreationRewards, "キャラクター作成時に経験値と決意を{0}獲得する。", RulePositivePattern);
        rules.Add(EffectContentType.ResourceFactorWithoutClasses, "自身はクラス〈{0}〉を装備していない場合{1}最大値が×{2}倍される。", RuleNamePattern, RuleResourcePattern, RuleFactorPattern);
        rules.Add(EffectContentType.StartingLevel, "あなたは{0}レベルから冒険が始まる。", @"[0-9]+");
        rules.Add(EffectContentType.LevelExperienceAndGrowth, "レベル{0}へ上がるためには、消費経験値{1}を支払う、その際に成長点{2}を得る。", RulePositivePattern, RulePositivePattern, RulePositivePattern);
        rules.Add(EffectContentType.ItemUseRecoveryAndState, "〈{0}〉を使用した時、自身は{1}が{2}回復する。さらに《{3}》状態になる。", RuleNamePattern, RuleResourcePattern, RulePositivePattern, RuleNamePattern);
        rules.Add(EffectContentType.CriticalFumbleThresholds, "行為判定を行う際、クリティカルになるダイスは{0}、ファンブルになるダイスは{1}される。", RuleSignedPattern, RuleSignedPattern);
        rules.Add(EffectContentType.ChooseTaggedSkills, "〈{0}〉スキルを{1}つ選んで習得しても良い。", RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.OwnTurnCheckTargetDelta, "{0}ターン目の自身のターン開始時、あらゆる行為判定の目標値は{1}される。", RulePositivePattern, RuleSignedPattern);
    }

    // 折り返しを含む本文を、単体スキルでも読み込める既存の文型へ揃える。
    private static string[] NormalizeProgressionSentences(string[] source)
    {
        var lines = new List<string>();
        for (int i = 0; i < source.Length; i++)
        {
            string line = source[i].TrimEnd();
            string next = i + 1 < source.Length ? source[i + 1].Trim() : "";
            if (M(line, @"(?:を宣言する際、|を受けている時、|カテゴリーのエネミーを倒した時、|敵プレイヤーのパーティーを全滅させた場合、)$").Success &&
                next.Length > 0 && !next.StartsWith("【", StringComparison.Ordinal))
            {
                line += next; i++;
                if (line.EndsWith("付与することで、", StringComparison.Ordinal) && i + 1 < source.Length)
                    line += source[++i].Trim();
            }
            else if (line.EndsWith("戦闘不能になった事で自分1人の時。", StringComparison.Ordinal) && next.StartsWith("自身のターン開始時", StringComparison.Ordinal))
            { line = line.TrimEnd('。') + "、" + next; i++; }
            else if (M(line, @"〈[^〉]+〉を使用した時、自身は[A-Z]+が[0-9]+回復する。$").Success && next.StartsWith("さらに《", StringComparison.Ordinal))
            { line += next; i++; }
            line = DataBlockText.GetRegex(@"自身の種族が((?:〈[^〉]+〉[,、]\s*)+〈[^〉]+〉)以外").Replace(line, m => "自身の種族が〈" + string.Join(", ", AllMatches(m.Groups[1].Value, @"〈([^〉]+)〉").Cast<Match>().Select(x => x.Groups[1].Value)) + "〉以外");
            line = RegexReplace(line, @"(自身の[A-Z]+が[0-9]+以下の時)、戦闘中", "$1, 戦闘中");
            line = RegexReplace(line, @"(戦闘開始[0-9]+ターン目)に(自身を除く味方キャラクターを)", "$1, $2");
            line = line.Replace("通常選択不可、管理者", "通常選択不可, 管理者");
            line = RegexReplace(line, @"戦闘([0-9]+)ターン目開始時に([A-Z]+が[+-][0-9]+される)", "戦闘開始$1ターン目、$2");
            line = RegexReplace(line, @"自身が受ける〈([^〉]+)〉による被ダメージが×", "自身の〈$1〉による被ダメージは×");
            line = RegexReplace(line, @"自身が与える〈([^〉]+)〉の威力が×", "〈$1〉による威力が×");
            line = RegexReplace(line, @"自身の([A-Z]+)最大値(?:は|が)×?([0-9.]+)倍される", "$1最大値が×$2倍される");
            line = RegexReplace(line, @"このパッシブスキルによる([A-Z]+)増加は([+-][0-9]+)に変化する", "このパッシブ効果による$1最大値増加は$2に変化する");
            line = RegexReplace(line, @"このパッシブ効果による([A-Z]+)値最大値は([+-][0-9]+)に変化する", "このパッシブ効果による$1最大値増加は$2に変化する");
            line = RegexReplace(line, @"このパッシブ効果の自身の([A-Z]+)最大値上昇は", "このパッシブ効果による$1最大値は");
            line = RegexReplace(line, @"このパッシブ効果(?:の達成値の上昇|によって上昇する達成値)は", "このパッシブ効果による達成値の上昇は");
            line = line.Replace("このパッシブ効果の威力上昇は","このパッシブ効果による威力上昇は")
                .Replace("ごとのHP最大値現象は", "ごとのHP最大値減少は").Replace("効果の効果による回復効果", "効果による回復効果")
                .Replace("成長点を追加でさらに", "成長点を追加で");
            line = RegexReplace(line, @"行為判定を行う際、クリティカルになるダイスが(\+[0-9]+)高い目を出さなければならなくなる。", "行為判定を行う際、クリティカルになるダイスの目が$1される。");
            line = RegexReplace(line, @"称号《([^》]+)》習得$", "称号《$1》を習得する。");
            line = RegexReplace(line, @"自身を〈([^〉]+)〉としても扱ってよい。", "あなたは任意で自身の種族を〈$1〉として扱っても良い。");
            line = RegexReplace(line, @"対象に《([^》]+)》バフを付与する。", "対象は《$1》状態になる。");
            line = RegexReplace(line, @"自身の〈([^〉,、]+)[,、]\s*([^〉]+)〉による達成値は×?([0-9.]+)倍される。", "〈$1〉及び〈$2〉による行為判定を行う際、達成値が×$3倍される。");
            line = RegexReplace(line, @"自身の〈([^〉]+)〉による達成値は×?([0-9.]+)倍される。", "〈$1〉による達成値が×$2倍される。");
            line = RegexReplace(line, @"([A-Z]+最大値[+-][0-9]+),\s*(?=〈)", "$1\n");
            line = DataBlockText.GetRegex(@"自身の((?:[^,、及。\s]+B(?:,\s*|、\s*|及び))+[^,、及。\s]+B)(?:が|は)([+-][0-9.]+)加算される。").Replace(line,
                m => string.Join("\n", DataBlockText.GetRegex(@",\s*|、\s*|及び").Split(m.Groups[1].Value)
                    .Select(stat => "自身の" + stat + "が" + m.Groups[2].Value + "加算される。")));
            line = RegexReplace(line, @"自身の([^、。\s]+B)は(-[0-9.]+)減少する。", "自身の$1が$2加算される。");
            line = RegexReplace(line, @"([A-Z]+最大値[+-][0-9]+),\s*(自身の〈)", "$1\n$2");
            line = RegexReplace(line, @"自身が装備できるスキルが([+-][0-9]+)される。", "自身が装備できるスキル枠は$1される。");
            line = line.Replace("その結果は全員に適応される", "その結果は全員に適用される");
            if (M(line, @"(?:^|】)自身の×[0-9.]+倍される。?$").Success)
                throw new InvalidOperationException("倍率を適用する能力値名またはリソース名がありません。");
            line = RegexReplace(line, @"(〈[^〉]+〉のアクティブ効果による行為判定の達成値は[+-][0-9]+される)、$", "$1。");
            line = RegexReplace(line, @"^([0-9]+)：(自身は《[^》]+》状態になる。)$", "- $1：$2");
            var pair = M(line, @"^([^〈〉《》。：]+)の([^、。]+B)は([+-][0-9.]+)減少に変化、$");
            if (pair.Success && i + 1 < source.Length)
            {
                var other = M(source[i + 1].Trim(), @"^([^〈〉《》。：]+)の([^、。]+B)及び([^、。]+B)は([+-][0-9.]+)加算に変化する。$");
                if (other.Success)
                {
                    line = "《" + pair.Groups[1].Value + "》の" + pair.Groups[2].Value + "は" + pair.Groups[3].Value +
                        "、《" + other.Groups[1].Value + "》の" + other.Groups[2].Value + "及び" + other.Groups[3].Value + "は" + other.Groups[4].Value + "に変化する。";
                    i++;
                }
            }
            lines.AddRange(line.Split('\n'));
        }
        return lines.ToArray();
    }
}
