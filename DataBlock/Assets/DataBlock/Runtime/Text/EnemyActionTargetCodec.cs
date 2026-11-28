using System;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class EnemyTextConverter
{
    private static void ReadActionTarget(EnemyActionStep step, string text)
    {
        string target = Replace(text, @"(?:からランダムに|を対象に|に対して|対して|へ|に)$", "");
        Match reference = Match(target, @"^このターン中に最後に《([^》]+)》によって敵にダメージを与えた対象$");
        if (reference.Success) { step.TargetSelector = EnemyActionTargetSelector.LastDamageTarget; step.TargetReference = reference.Groups[1].Value; return; }
        reference = Match(target, @"^エリアの《([^》]+)》を宣言したキャラクター$");
        if (reference.Success) { step.TargetSelector = EnemyActionTargetSelector.AreaSkillDeclarer; step.TargetReference = reference.Groups[1].Value; return; }
        if (target == "進行ロールを行った代表者") { step.TargetSelector = EnemyActionTargetSelector.ProgressRepresentative; return; }

        AddTargetFilter(step, ref target, @"《([^》]+)》スタックを([0-9]+)以上持つ", EnemyTargetFilterType.StateStacksAtLeast);
        AddTargetFilter(step, ref target, @"《([^》]+)》状態ではない", EnemyTargetFilterType.WithoutState);
        AddTargetFilter(step, ref target, @"《([^》]+)》状態の", EnemyTargetFilterType.State);
        AddTargetFilter(step, ref target, @"種族(?:が)?〈([^〉]+)〉(?:のいずれかの|の)?", EnemyTargetFilterType.Race);
        AddTargetFilter(step, ref target, @"〈([^〉]+)〉(?:の)?", EnemyTargetFilterType.Category);
        AddTargetFilter(step, ref target, @"HPが([0-9]+)以下(?:の)?", EnemyTargetFilterType.HpAtMost);
        AddTargetFilter(step, ref target, "HPが減少している", EnemyTargetFilterType.Injured);
        AddTargetFilter(step, ref target, "最もHPの低い", EnemyTargetFilterType.LowestHp);
        bool melee = TakeTargetPart(ref target, @"(?:自身と|毎)?同じ接近グループの");
        bool others = TakeTargetPart(ref target, "自身を除く");
        step.SelectableOnly = TakeTargetPart(ref target, "選?択可能な");
        step.DistinctTargets |= TakeTargetPart(ref target, "重複しない");
        Match count = Match(target, @"([0-9]+)体(まで)?(?:の)?");
        if (count.Success)
        {
            step.TargetCount = Number(count.Groups[1].Value, 1, "対象数");
            step.UpToTargetCount = count.Groups[2].Success;
            target = target.Remove(count.Index, count.Length);
        }
        bool all = TakeTargetPart(ref target, "(?:すべて|全て)(?:の)?|全員|全体");
        bool random = TakeTargetPart(ref target, "ランダムな") || text.EndsWith("からランダムに", StringComparison.Ordinal);
        step.RandomizeAllTargets = all && random;
        if (target == "敵キャラクター") target = "敵";
        if (target == "味方キャラクター") target = "味方";
        if (target == "敵" && !others)
            step.TargetSelector = all ? (melee ? EnemyActionTargetSelector.AllEnemiesInMeleeGroup : EnemyActionTargetSelector.AllEnemies)
                : random ? (melee ? EnemyActionTargetSelector.RandomSelectableEnemyInMeleeGroup : EnemyActionTargetSelector.RandomSelectableEnemy)
                : !melee ? EnemyActionTargetSelector.Enemy : throw new InvalidOperationException("接近グループの選択方法がありません。");
        else if (target == "味方" && !melee && (all || random && !others))
            step.TargetSelector = all ? (others ? EnemyActionTargetSelector.AllOtherAllies : EnemyActionTargetSelector.AllAllies) : EnemyActionTargetSelector.RandomAlly;
        else if (target == "敵味方" && melee && all && !others) step.TargetSelector = EnemyActionTargetSelector.AllInMeleeGroup;
        else if (target == "キャラクター" && all && !melee) step.TargetSelector = others ? EnemyActionTargetSelector.AllOtherCharacters : EnemyActionTargetSelector.AllCharacters;
        else if ((target.Length == 0 || target == "キャラクター") && random && !others && !melee && !all)
            step.TargetSelector = EnemyActionTargetSelector.RandomCharacter;
        else throw new InvalidOperationException("未対応の行動対象です：" + text);
    }

    private static bool TakeTargetPart(ref string text, string pattern)
    {
        var found = Match(text, pattern);
        if (!found.Success) return false;
        text = text.Remove(found.Index, found.Length);
        return true;
    }

    private static void AddTargetFilter(EnemyActionStep step, ref string target, string pattern, EnemyTargetFilterType type)
    {
        var found = Match(target, pattern);
        if (!found.Success) return;
        var filter = new EnemyTargetFilter { Type = type };
        for (int i = 1; i < found.Groups.Count; i++)
            filter.Values.AddRange(found.Groups[i].Value.Split(',').Select(value => Need(value.Trim(), "対象条件の値")));
        step.TargetFilters.Add(filter);
        target = target.Remove(found.Index, found.Length);
    }

    private static string ActionTargetText(EnemyActionStep step)
    {
        if (step.TargetFilters == null || step.TargetCount < 0 || step.UpToTargetCount && step.TargetCount == 0)
            throw new InvalidOperationException("行動対象の条件または人数が不正です。");
        bool hasReference = step.TargetSelector == EnemyActionTargetSelector.AreaSkillDeclarer || step.TargetSelector == EnemyActionTargetSelector.LastDamageTarget;
        if (hasReference == string.IsNullOrEmpty(step.TargetReference) ||
            (hasReference || step.TargetSelector == EnemyActionTargetSelector.ProgressRepresentative) &&
            (step.TargetFilters.Count > 0 || step.TargetCount != 0 || step.DistinctTargets || step.SelectableOnly))
            throw new InvalidOperationException("行動対象の参照先と選択条件が一致しません。");
        string prefix = step.DistinctTargets ? "重複しない" : "";
        foreach (var filter in step.TargetFilters)
        {
            if (filter == null || filter.Values == null) throw new InvalidOperationException("行動対象の条件が空です。");
            if (filter.Type == EnemyTargetFilterType.StateStacksAtLeast)
            {
                if (filter.Values.Count != 2) throw new InvalidOperationException("スタック条件には状態名と下限が必要です。");
                prefix += "《" + Token(filter.Values[0], "状態名", "《》、,") + "》スタックを" +
                    Number(filter.Values[1], 0, "スタックの下限") + "以上持つ";
                continue;
            }
            bool noValues = filter.Type == EnemyTargetFilterType.Injured || filter.Type == EnemyTargetFilterType.LowestHp;
            bool multiple = filter.Type == EnemyTargetFilterType.Category || filter.Type == EnemyTargetFilterType.Race;
            if (noValues ? filter.Values.Count != 0 : multiple ? filter.Values.Count < 1 : filter.Values.Count != 1)
                throw new InvalidOperationException("対象条件の値の数が不正です。");
            string value = string.Join(", ", filter.Values.Select(v => Token(v, "対象条件", "〈〉《》、,")));
            switch (filter.Type)
            {
                case EnemyTargetFilterType.State: prefix += "《" + Need(value, "状態名") + "》状態の"; break;
                case EnemyTargetFilterType.WithoutState: prefix += "《" + Need(value, "状態名") + "》状態ではない"; break;
                case EnemyTargetFilterType.Category: prefix += "〈" + Need(value, "カテゴリー") + "〉の"; break;
                case EnemyTargetFilterType.Race: prefix += "種族が〈" + Need(value, "種族") + "〉のいずれかの"; break;
                case EnemyTargetFilterType.HpAtMost: prefix += "HPが" + Number(value, 0, "対象HP上限") + "以下の"; break;
                case EnemyTargetFilterType.Injured: prefix += "HPが減少している"; break;
                case EnemyTargetFilterType.LowestHp: prefix += "最もHPの低い"; break;
                default: throw new InvalidOperationException("行動対象の条件が不正です。");
            }
        }
        if (step.TargetSelector == EnemyActionTargetSelector.RandomSelectableEnemyInMeleeGroup ||
            step.TargetSelector == EnemyActionTargetSelector.AllEnemiesInMeleeGroup || step.TargetSelector == EnemyActionTargetSelector.AllInMeleeGroup)
            prefix += "同じ接近グループの";
        if (step.SelectableOnly) prefix += "選択可能な";
        if (step.RandomizeAllTargets)
        {
            if (step.TargetSelector < EnemyActionTargetSelector.AllEnemies || step.TargetSelector > EnemyActionTargetSelector.AllOtherAllies)
                throw new InvalidOperationException("全対象へのランダム指定には全員を選択してください。");
            prefix += "ランダムな";
        }
        string target;
        switch (step.TargetSelector)
        {
            case EnemyActionTargetSelector.None:
                if (prefix.Length > 0 || step.TargetCount != 0) throw new InvalidOperationException("行動対象の指定が必要です。");
                return "";
            case EnemyActionTargetSelector.Enemy: target = "敵"; break;
            case EnemyActionTargetSelector.RandomSelectableEnemy:
            case EnemyActionTargetSelector.RandomSelectableEnemyInMeleeGroup: target = "ランダムな敵"; break;
            case EnemyActionTargetSelector.RandomAlly: target = "ランダムな味方"; break;
            case EnemyActionTargetSelector.RandomCharacter: target = "ランダムなキャラクター"; break;
            case EnemyActionTargetSelector.AllEnemies:
            case EnemyActionTargetSelector.AllEnemiesInMeleeGroup: target = "敵全員"; break;
            case EnemyActionTargetSelector.AllAllies: target = "味方全員"; break;
            case EnemyActionTargetSelector.AllInMeleeGroup: target = "敵味方すべて"; break;
            case EnemyActionTargetSelector.AllCharacters: target = "すべてのキャラクター"; break;
            case EnemyActionTargetSelector.AllOtherCharacters: target = "自身を除くすべてのキャラクター"; break;
            case EnemyActionTargetSelector.AllOtherAllies: target = "自身を除く味方全員"; break;
            case EnemyActionTargetSelector.LastDamageTarget: target = "このターン中に最後に《" + Token(step.TargetReference, "スキル名", "《》") + "》によって敵にダメージを与えた対象"; break;
            case EnemyActionTargetSelector.AreaSkillDeclarer: target = "エリアの《" + Token(step.TargetReference, "スキル名", "《》") + "》を宣言したキャラクター"; break;
            case EnemyActionTargetSelector.ProgressRepresentative: target = "進行ロールを行った代表者"; break;
            default: throw new InvalidOperationException("行動対象の選択方法が不正です。");
        }
        return prefix + target + (step.TargetCount > 0 ? step.TargetCount + (step.UpToTargetCount ? "体まで" : "体") : "") + "に";
    }
}
