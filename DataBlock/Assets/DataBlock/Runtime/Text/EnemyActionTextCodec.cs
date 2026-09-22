using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class EnemyTextConverter
{
    private static EnemyActionRule ParseAction(string text)
    {
        Match reaction = Match(text, @"^あらゆる自身への〈([^〉]+)〉に対して《([^》]+)》を発動する。?$");
        if (reaction.Success)
        {
            return new EnemyActionRule
            {
                Timing = EnemyActionTiming.Reaction,
                SkillName = Need(reaction.Groups[2].Value, "行動スキル名"),
                AllConditions = new List<EnemyActionCondition>
                {
                    NewActionCondition(EnemyActionConditionType.IncomingAction, reaction.Groups[1].Value)
                }
            };
        }

        Match turn = Match(text,
            @"^毎ターン([0-9]+)回、(.+)、(同じ接近グループの選択可能なランダムな敵|選択可能なランダムな敵)に《([^》]+)》を発動する。?$");
        if (!turn.Success) throw new InvalidOperationException("未対応の行動文です：" + text);

        string conditionText = turn.Groups[2].Value;
        bool hasAnd = conditionText.Contains("、かつ");
        bool hasOr = conditionText.Contains("、または");
        if (hasAnd && hasOr) throw new InvalidOperationException("行動条件で「かつ」と「または」は混在できません。");

        string separator = hasOr ? "、または" : "、かつ";
        List<EnemyActionCondition> conditions = conditionText.Split(new[] { separator }, StringSplitOptions.None)
            .Select(ParseActionCondition).ToList();
        if (conditions.Count == 0) throw new InvalidOperationException("行動条件がありません。");

        var rule = new EnemyActionRule
        {
            Timing = EnemyActionTiming.Turn,
            UsesPerTurn = Number(turn.Groups[1].Value, 1, "毎ターンの発動回数"),
            TargetSelector = turn.Groups[3].Value.StartsWith("同じ接近グループ", StringComparison.Ordinal)
                ? EnemyActionTargetSelector.RandomSelectableEnemyInMeleeGroup
                : EnemyActionTargetSelector.RandomSelectableEnemy,
            SkillName = Need(turn.Groups[4].Value, "行動スキル名")
        };
        if (hasOr) rule.AnyConditions.AddRange(conditions);
        else rule.AllConditions.AddRange(conditions);
        return rule;
    }

    private static EnemyActionCondition ParseActionCondition(string text)
    {
        text = text.Trim();
        Match state = Match(text, @"^《([^》]+)》状態ではない時$");
        if (state.Success) return NewActionCondition(EnemyActionConditionType.OwnState, state.Groups[1].Value, true);
        state = Match(text, @"^《([^》]+)》状態の時$");
        if (state.Success) return NewActionCondition(EnemyActionConditionType.OwnState, state.Groups[1].Value);
        if (text == "同じ接近グループに選択可能な敵が居ない場合")
            return NewActionCondition(EnemyActionConditionType.NoSelectableEnemyInMeleeGroup);
        throw new InvalidOperationException("未対応の行動条件です：" + text);
    }

    private static string ActionText(EnemyActionRule action)
    {
        if (action == null) throw new InvalidOperationException("行動がnullです。");
        if (action.AllConditions == null || action.AnyConditions == null)
            throw new InvalidOperationException("行動条件を初期化してください。");
        if (action.AllConditions.Any(x => x == null) || action.AnyConditions.Any(x => x == null))
            throw new InvalidOperationException("行動条件にnullがあります。");
        string skillName = Token(action.SkillName, "行動スキル名", "《》");

        if (action.Timing == EnemyActionTiming.Reaction)
        {
            if (action.UsesPerTurn != 0 || action.TargetSelector != EnemyActionTargetSelector.None ||
                action.AnyConditions.Count != 0 || action.AllConditions.Count != 1 ||
                action.AllConditions[0].Type != EnemyActionConditionType.IncomingAction ||
                action.AllConditions[0].Negated)
                throw new InvalidOperationException("反応行動は、自身への行動カテゴリー1件だけを条件にしてください。");
            return "あらゆる自身への〈" + Token(action.AllConditions[0].Value, "反応する行動カテゴリー", "〈〉") +
                   "〉に対して《" + skillName + "》を発動する。";
        }

        if (action.Timing != EnemyActionTiming.Turn)
            throw new InvalidOperationException("未対応の行動タイミングです：" + action.Timing);
        if (action.UsesPerTurn < 1) throw new InvalidOperationException("毎ターンの発動回数は1以上にしてください。");
        if (action.AllConditions.Count != 0 && action.AnyConditions.Count != 0)
            throw new InvalidOperationException("行動のAND条件とOR条件は同時に指定できません。");
        if (action.AllConditions.Count == 0 && action.AnyConditions.Count < 2)
            throw new InvalidOperationException("毎ターン行動にはAND条件1件以上、またはOR条件2件以上が必要です。");
        if (action.AllConditions.Any(x => x.Type == EnemyActionConditionType.IncomingAction) ||
            action.AnyConditions.Any(x => x.Type == EnemyActionConditionType.IncomingAction))
            throw new InvalidOperationException("IncomingAction条件は反応行動だけに指定できます。");

        string target;
        switch (action.TargetSelector)
        {
            case EnemyActionTargetSelector.RandomSelectableEnemy:
                target = "選択可能なランダムな敵";
                break;
            case EnemyActionTargetSelector.RandomSelectableEnemyInMeleeGroup:
                target = "同じ接近グループの選択可能なランダムな敵";
                break;
            default:
                throw new InvalidOperationException("毎ターン行動の対象選択が不正です：" + action.TargetSelector);
        }

        bool isOr = action.AnyConditions.Count != 0;
        IEnumerable<EnemyActionCondition> conditions = isOr ? action.AnyConditions : action.AllConditions;
        string conditionText = string.Join(isOr ? "、または" : "、かつ", conditions.Select(ActionConditionText));
        return "毎ターン" + action.UsesPerTurn + "回、" + conditionText + "、" + target + "に《" + skillName + "》を発動する。";
    }

    private static string ActionConditionText(EnemyActionCondition condition)
    {
        switch (condition.Type)
        {
            case EnemyActionConditionType.OwnState:
                string stateName = Token(condition.Value, "状態名", "《》");
                if (stateName.Contains("、または") || stateName.Contains("、かつ"))
                    throw new InvalidOperationException("行動条件の状態名に「、または」「、かつ」は使用できません。");
                return "《" + stateName + "》状態" + (condition.Negated ? "ではない時" : "の時");
            case EnemyActionConditionType.NoSelectableEnemyInMeleeGroup:
                if (condition.Negated || !string.IsNullOrWhiteSpace(condition.Value))
                    throw new InvalidOperationException("接近グループ対象なし条件には値や否定を指定できません。");
                return "同じ接近グループに選択可能な敵が居ない場合";
            case EnemyActionConditionType.IncomingAction:
                throw new InvalidOperationException("IncomingAction条件は反応行動として出力してください。");
            default:
                throw new InvalidOperationException("未対応の行動条件種別です：" + condition.Type);
        }
    }

    private static EnemyActionCondition NewActionCondition(EnemyActionConditionType type, string value = "", bool negated = false)
    {
        return new EnemyActionCondition { Type = type, Value = value, Negated = negated };
    }
}
