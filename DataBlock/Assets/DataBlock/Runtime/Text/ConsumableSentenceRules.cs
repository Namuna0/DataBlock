using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static void RegisterConsumableActions(RuleSection rules)
    {
        rules.Add(EffectContentType.FailedCheckPermanentStacks, "行為判定{0} 目標値{1}を行い、失敗した時、自身は永続的に《{2}》スタックを{3}得る。", ArithmeticPattern, RulePositivePattern, RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.SuccessfulCheckReward, "行為判定{0} 目標値{1}を行い、達成した場合、{2}+{3}を得る。", ArithmeticPattern, RulePositivePattern, @"経験値|決意", RulePositivePattern);
        rules.Add(EffectContentType.ActiveCostFactorThisTurn, "このターン、自身のアクティブ効果の発動によって消費する{0}は×{1}倍される。", RuleResourcePattern, RuleFactorPattern);
        rules.Add(EffectContentType.ConsumedItemCount, "使用したアイテムを{0}つ消費する。", RulePositivePattern);
        rules.Add(EffectContentType.NextWorkQuestScore, "次に達成する作業クエストの得点を{0}する。", RuleSignedPattern);
        rules.Add(EffectContentType.RestoreIfAnyRace, "自身の種族が《{0}》のいずれかの場合、さらに{1}を+{2}回復する。", @"[^《》【】。]+", RuleResourcePattern, RulePositivePattern);
        rules.Add(EffectContentType.NextNamedSkillAchievementFactor, "自身の次の一回の《{0}》による達成値は×{1}倍される。", RuleNamePattern, RuleFactorPattern);
        rules.Add(EffectContentType.ExcessRecoveryToStacks, "{0}が最大値を超過する場合、超過分を最大{1}まで《{2}》スタックとして永続付与する。", RuleResourcePattern, RulePositivePattern, RuleNamePattern);
        rules.Add(EffectContentType.ChooseStateCategoriesToRemove, "自身に付与されている〈{0}〉及び〈{1}〉を{2}つまで選んで解除する。", RuleNamePattern, RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.GainProgressionRewards, "{0}の経験値と決意を得る。", RulePositivePattern);
        rules.Add(EffectContentType.RollNamedTable, "自身は{0}ロールを{1}回行う。", RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.IncreaseSelectedStacks, "自身の状態スタックを{0}つ選んで{1}増加させる。", RulePositivePattern, RulePositivePattern);
        rules.Add(EffectContentType.CategorySelfState, "自身が〈{0}〉なら《{1}》状態になる。", RuleNamePattern, RuleNamePattern);
        rules.Add(EffectContentType.RaceSelfState, "自身が《{0}》なら《{1}》状態になる。", RuleNamePattern, RuleNamePattern);
        rules.Add(EffectContentType.NextCategoryCriticalDelta, "自身の次の{0}回の〈{1}〉によるクリティカルの目は{2}される。", RulePositivePattern, RuleNamePattern, RuleSignedPattern);
    }

    private static void RegisterConsumableStates(RuleSection rules)
    {
        rules.Add(EffectContentType.AreaMoveEncounterCheckBonus, "エリアの移動条件および特殊エンカウントの行為判定による達成値は{0}される。", RuleSignedPattern);
        rules.Add(EffectContentType.ClassFamilyCheckFactor, "自身の{0}系クラススキルによる達成値は×{1}倍される。", RuleNamePattern, RuleFactorPattern);
        rules.Add(EffectContentType.OutgoingElementPowerFactor, "自身の{0}属性による威力は×{1}倍される。", ElementSetPattern, RuleFactorPattern);
        rules.Add(EffectContentType.ActiveCostOffset, "自身はアクティブ効果を発動する際、消費する{0}は{1}される。", RuleResourcePattern, RuleSignedPattern);
        rules.Add(EffectContentType.CategoryResourceCostFactor, "〈{0}〉による{1}消費は×{2}倍される。", RuleNamePattern, RuleResourcePattern, RuleFactorPattern);
        rules.Add(EffectContentType.QuestCategoryRewardBonus, "〈{0}〉クエストで受け取る決意及び経験値は{1}される。", RuleNamePattern, RuleSignedPattern);
        rules.Add(EffectContentType.NonStackingNonzeroRewards, "重複不可、元々の獲得経験値と決意が0の時は加算されない。");
        rules.Add(EffectContentType.PeriodicMaxResourceRecovery, "ターン開始時に自身の{0}を最大値の{1}%回復する。", RuleResourcePattern, RulePositivePattern);
        rules.Add(EffectContentType.IncomingCounterResourceFactor, "自身がカウンターによる{0}ダメージを受ける時、被ダメージは×{1}倍される。", RuleResourcePattern, RuleFactorPattern);
        rules.Add(EffectContentType.TurnStartNamedStacks, "毎ターン開始時《{0}》スタックを{1}獲得する。", RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.CategoryStackDamageMitigation, "自身はこのバフスタック×{0}につき〈{1}〉によるダメージ及びSANダメージを{2}減少させる。", RulePositivePattern, RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.LoseStacksByDamageAndSan, "このスタックは自身が〈{0}〉を受けた時その{1}及びSANダメージ分減少する。", RuleNamePattern, @"威力|ダメージ");
        rules.Add(EffectContentType.SpendStacksInsteadOfResource, "自身の{0}が減少する時、このスタックを代わりに消費する。", RuleResourcePattern);
        rules.Add(EffectContentType.ParityTurnStartResource, "自身の{0}ターン開始時に{1}を+{2}得る。", @"奇数|偶数", RuleResourcePattern, RulePositivePattern);
        rules.Add(EffectContentType.NamedSkillTargetValue, "自身の《{0}》による目標値は{1}になる。", RuleNamePattern, RulePositivePattern);
        rules.Add(EffectContentType.NamedSkillStackMultiplier, "《{0}》発動で得る《{1}》スタックは×{2}に変化する。", RuleNamePattern, RuleNamePattern, RuleFactorPattern);
    }

    private static string[] NormalizeConsumableLines(string[] lines)
    {
        var result = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            var allRolls = M(line, @"^(【発動ロール】([1-9][0-9]*)回(?:連続)?[：:].+目標値[0-9]+)(?:すべて|全て)達成。?$");
            if (allRolls.Success && i + 1 < lines.Length && lines[i + 1].TrimStart().StartsWith("【アクティブ効果】", StringComparison.Ordinal))
            {
                result.Add(allRolls.Groups[1].Value);
                line = lines[++i].Trim().Replace("【アクティブ効果】", "【アクティブ効果】達成" + allRolls.Groups[2].Value + "回以上で発動。\n");
            }
            // 追加判定とその結果を一つの効果にまとめ、発動ロールとは独立して扱う。
            var check = M(line, @"^(.*?行為判定[0-9][^。]*?目標値[0-9]+)を行う。?$");
            if (check.Success && i + 1 < lines.Length && M(lines[i + 1].Trim(), @"^(?:失敗した時、自身は永続的に《|達成した場合、(?:経験値|決意)\+)").Success)
                line = check.Groups[1].Value + "を行い、" + lines[++i].Trim();

            line = RegexReplace(line, @"^(?:《([^》]+)》効果|([^・●*\-【《》〈〉：:。][^【《》〈〉：:。]*?))(〈[^〉]+〉)[：:]", "$1$2効果$3：")
                .Replace("効果効果〈", "効果〈");
            line = RegexReplace(line, @"^([^【《》〈〉：:。]+)(?<!効果)：(?=ターン開始時に自身の[A-Z]+を最大値)", "$1効果：");
            line = RegexReplace(line, @"^(.*?効果(?:〈[^〉]+〉)?：)毎ターン開始時《([^》]+)》と《([^》]+)》スタックを([0-9]+)獲得する。?$",
                "$1毎ターン開始時《$2》スタックを$4獲得する。\n毎ターン開始時《$3》スタックを$4獲得する。");
            line = RegexReplace(line, @"^\*効果適用後、バフは解除される。?$", "*効果適用後、この状態は解除される。");
            line = RegexReplace(line, @"\[(\w+B)\]によるダイスロールは×([0-9.]+)倍される。?$", "[$1]による行為判定を行う際、達成値が×$2倍される。");
            line = RegexReplace(line, @"自身のターン開始時([A-Z]+)が([0-9]+)回復する。?$", "自身のターン開始時に自身の$1を$2回復する。");
            line = RegexReplace(line, @"〈([^〉]+)〉による消費([A-Z]+)は(-[0-9]+)減少する。?$", "自身の〈$1〉による$2消費は$3される。");
            line = RegexReplace(line, @"自身の([A-Z]+)消費は([+-][0-9]+)される。?$", "$1消費$2");
            line = RegexReplace(line, @"自身が([A-Z]+)を消費する際、消費量は([+-][0-9]+)される。?$", "$1消費$2");
            line = RegexReplace(line, @"自身の([A-Z]+)最大値は([0-9]+)加算される。?$", "$1最大値+$2");
            line = RegexReplace(line, @"^さらに防御点を([0-9]+)得る。?$", "防御点+$1");
            line = RegexReplace(line, @"自身の([A-Z]+)が(?:現象|減少)する時、このスタックを\1の代わりに消費する。?$", "自身の$1が減少する時、このスタックを代わりに消費する。");
            line = RegexReplace(line, @"HPが最大体力を超過する場合、余分を《([^》]+)》スタックとして永続付与する。[(（]最大([0-9]+)まで[)）]", "HPが最大値を超過する場合、超過分を最大$2まで《$1》スタックとして永続付与する。");
            line = RegexReplace(line, @"(?:さらに)?次のターン開始時([A-Z]+)を([0-9]+)得る。?$", "自身の次のターン開始時、$1を$2得る。");
            line = RegexReplace(line, @"このアイテムを([0-9]+)つ消費して(?=自身の)", "使用したアイテムを$1つ消費する。\n");
            line = RegexReplace(line, @"^(【[^】]+】)?(?:その後、)?《([^》]+)》×([0-9]+)を獲得する。?$", "$1《$2》×$3を獲得。");
            line = RegexReplace(line, @"自身([0-9]+ターンの間《)", "自身は$1");
            line = RegexReplace(line, @"^(【[^】]+】)?自身は永続的に《([^》]+)》状態になる。?$", "$1自身へ永続的に《$2》状態を付与する。");
            line = RegexReplace(line, @"自身は《([^》]+)》かつ《([^》]+)》状態になる。?$", "自身は《$1》状態になる。\n自身は《$2》状態になる。");
            line = RegexReplace(line, @"^(?:さらに)?ダイスロール([0-9]+d[0-9]+)を行う。?$", "$1のダイスロールを行う。");
            line = RegexReplace(line, @"^([0-9]+(?:～[0-9]+)?：)(?=自身は)", "- $1");
            line = RegexReplace(line, @"(?<=：)([A-Z]+)\+([0-9]+)回復。?$", "$1$2回復。");
            line = RegexReplace(line, @"^(-\s*[0-9]+(?:～[0-9]+)?[：:]\s*)称号《([^》]+)》を獲得する。?$", "$1自身は称号《$2》を獲得する。");
            line = RegexReplace(line, @"([1-9][0-9]*d[1-9][0-9]*)で([0-9]+)を出すと([A-Z]+)([-+][0-9]+)$",
                "$1のダイスロールを行う。\n- $2：自身の$3を$4変化させる。");
            line = RegexReplace(line, @"([A-Z]+)をさらに([0-9]+)回復する。?$", "$1を$2回復する。");
            line = RegexReplace(line, @"最大値(?:の)?([0-9]+)%", "最大値の$1%");
            line = RegexReplace(line, @"^((?:【[^】]+】|・)?)(?:自身の|自身は)?([A-Z]+)を((?:最大値の)?[0-9]+%?)(?:,\s*|と)([A-Z]+)を((?:最大値の)?[0-9]+%?)回復する。?$",
                "$1自身の$2を$3回復する。\n自身の$4を$5回復する。");
            line = RegexReplace(line, @"自身の([A-Z]+(?:(?:と|,\s*)[A-Z]+)*)全回復。?$", "自身の$1を最大値の100%回復する。");
            var grants = M(line, @"^(【(?:アクティブ|カウンター|宣言)効果】)?(?:自身は)?((?:《[^》]+》×[0-9]+[,、]\s*)+《[^》]+》×[0-9]+)を得る。?$");
            if (grants.Success)
                line = grants.Groups[1].Value + string.Join("\n", AllMatches(grants.Groups[2].Value, @"《[^》]+》×[0-9]+")
                    .Cast<Match>().Select(item => item.Value + "を獲得。"));
            var many = M(line, @"^((?:【[^】]+】|・|[-－]\s*[0-9]+(?:～[0-9]+)?：)?)(?:自身の|自身は)?([A-Z]+(?:(?:と|,\s*)[A-Z]+)+)を((?:最大値の)?[0-9]+%?)を?回復する。?$");
            if (many.Success)
            {
                string[] resources = Regex.Split(many.Groups[2].Value, @"(?:と|,\s*)");
                string prefix = many.Groups[1].Value;
                bool branch = M(prefix, @"^[-－]\s*[0-9]+(?:～[0-9]+)?：$").Success;
                line = prefix + string.Join("\n" + (branch ? prefix : ""), resources.Select(resource => "自身の" + resource + "を" + many.Groups[3].Value + "回復する。"));
            }
            line = RegexReplace(line, @"^([-－]\s*[0-9]+(?:～[0-9]+)?：)自身の([A-Z]+)と([A-Z]+)は([0-9]+)減少する。?$",
                "$1自身の$2を-$4変化させる。\n$1自身の$3を-$4変化させる。");
            result.AddRange(line.Split('\n'));
        }
        return result.ToArray();
    }
}
