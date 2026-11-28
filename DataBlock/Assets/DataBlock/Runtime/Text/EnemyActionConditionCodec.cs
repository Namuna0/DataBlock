using System;
using System.Collections.Generic;
using System.Linq;

public static partial class EnemyTextConverter
{
    private const string ActionNamePart = @"[^《》【】、\r\n]+";
    private const string ActionTagsPart = @"[^〈〉【】、\r\n]+";
    private const string ActionNumberPart = @"[0-9]+";
    private static DataBlockSentencePattern ConditionPattern(EnemyActionConditionType type, string body, params string[] values)
        => new DataBlockSentencePattern(type.ToString(), body, values);

    private static readonly DataBlockSentencePattern[] ActionConditions =
    {
        ConditionPattern(EnemyActionConditionType.NoSelectableEnemyInMeleeGroup, "同じ接近グループに選択可能な敵が居ない場合"),
        ConditionPattern(EnemyActionConditionType.EnemyWithoutState, "《{0}》状態ではない敵がいる時", ActionNamePart),
        ConditionPattern(EnemyActionConditionType.AllyWithoutState, "《{0}》状態ではない味方がいる時", ActionNamePart),
        ConditionPattern(EnemyActionConditionType.NoEnemyWithState, "《{0}》状態の敵が居ない場合", ActionNamePart),
        ConditionPattern(EnemyActionConditionType.WoundedEnemy, "敵キャラクターに重症による状態が付与されたキャラクターが居る場合"),
        ConditionPattern(EnemyActionConditionType.AllyPresent, "味方に《{0}》のキャラクターが居るなら", ActionNamePart),
        ConditionPattern(EnemyActionConditionType.AllyCount, "味方に《{0}》が{1}体以上居る時", ActionNamePart, ActionNumberPart),
        ConditionPattern(EnemyActionConditionType.TraitEnabled, "特性《{0}》が有効な場合", ActionNamePart),
        ConditionPattern(EnemyActionConditionType.OwnResourceAtMost, "自身の{0}が{1}以下の場合", "[A-Z]+", ActionNumberPart),
        ConditionPattern(EnemyActionConditionType.EitherResourceAtMost, "自身の{0}または{1}が{2}以下の場合", "[A-Z]+", "[A-Z]+", ActionNumberPart),
        ConditionPattern(EnemyActionConditionType.OwnResourceLost, "自身の{0}が{1}以上減少している時", "[A-Z]+", ActionNumberPart),
        ConditionPattern(EnemyActionConditionType.OwnResourceReached, "自身の{0}が{1}になった時", "[A-Z]+", ActionNumberPart),
        ConditionPattern(EnemyActionConditionType.OwnStateCategory, "〈{0}〉状態を付与されている場合", ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.ActsFirst, "自身が先行の時"),
        ConditionPattern(EnemyActionConditionType.SkillNotDeclared, "《{0}》を宣言しない場合", ActionNamePart),
        ConditionPattern(EnemyActionConditionType.NoPriorTarget, "居ない場合は"),
        ConditionPattern(EnemyActionConditionType.NoAllyTarget, "味方が居ない時は"),
        ConditionPattern(EnemyActionConditionType.OwnerActsOnEnemy, "自身の所有者が〈{0}〉を敵に対して発動した時", ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.OwnerTargeted, "このアイテムの所持者が〈{0}〉から選択対象になる時", ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.EnterOwnState, "自身が《{0}》状態になる時", ActionNamePart),
        ConditionPattern(EnemyActionConditionType.IncomingBothCategories, "あらゆる自身への〈{0}〉かつ〈{1}〉に対して", ActionTagsPart, ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.IncomingAction, "あらゆる自身への〈{0}〉に対して", ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.AnyAction, "あらゆる〈{0}〉に対して", ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.AllyAction, "あらゆる味方への〈{0}〉に対して", ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.NamedActorAction, "《{0}》への〈{1}〉に対して", ActionNamePart, ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.IncomingActive, "あらゆる自身へのアクティブ効果に対して"),
        ConditionPattern(EnemyActionConditionType.NamedActorActive, "あらゆる《{0}》へのアクティブ効果に対して", ActionNamePart),
        ConditionPattern(EnemyActionConditionType.IncomingSkill, "自身への《{0}》に対して", ActionNamePart),
        ConditionPattern(EnemyActionConditionType.IncomingState, "あらゆる自身への状態付与に対して"),
        ConditionPattern(EnemyActionConditionType.IncomingStateCategory, "自身への〈{0}〉状態付与に対して", ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.IncomingActionOrState, "あらゆる自身への〈{0}〉及び状態〈{1}〉付与に対して", ActionTagsPart, ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.IncomingResultMinimum, "自身への達成値{0}以上の〈{1}〉に対して", ActionNumberPart, ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.IncomingResultMaximum, "自身への達成値{0}以下の〈{1}〉に対して", ActionNumberPart, ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.IncomingResultBelow, "達成値{0}を下回る自身への〈{1}〉に対して", ActionNumberPart, ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.IncomingResourceDamage, "あらゆる自身への{0}または{1}値にダメージを与える効果に対して", "[A-Z]+", "[A-Z]+"),
        ConditionPattern(EnemyActionConditionType.MeleeEnemyAction, "同じ接近グループの敵の〈{0}〉に対して", ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.IncomingMeleeAction, "同じ接近グループからの自身への〈{0}〉に対して", ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.EnemyAction, "敵の〈{0}〉の発動に対して", ActionTagsPart),
        ConditionPattern(EnemyActionConditionType.StateApplier, "自身を《{0}》または《{1}》状態にした対象に対して", ActionNamePart, ActionNamePart)
    };

    private static readonly Dictionary<string, DataBlockSentencePattern> ActionConditionByKey =
        ActionConditions.ToDictionary(pattern => pattern.Key, StringComparer.Ordinal);

    private static bool TryActionCondition(string text, out EnemyActionCondition condition)
    {
        condition = null;
        var own = Match(text, @"^(?:自身が)?(?:《([^》]+)》|(接近))状態(ではない|では無い)?(?:の時|時|なら|ではないなら|かつ)?$");
        if (own.Success)
        {
            condition = new EnemyActionCondition { Type = EnemyActionConditionType.OwnState,
                Value = own.Groups[1].Success ? own.Groups[1].Value : own.Groups[2].Value,
                Negated = own.Groups[3].Success || text.EndsWith("ではないなら", StringComparison.Ordinal) };
            return true;
        }
        if (text == "HPが減少している") text = "自身のHPが1以上減少している時";
        text = text.Replace("自身に対する〈", "自身への〈").Replace("自身へのあらゆる状態付与", "あらゆる自身への状態付与")
            .Replace("自身に状態〈", "自身への〈").Replace("〉及び〈", ", ").Replace("を下回るの自身", "を下回る自身")
            .Replace("自身と同じ接近グループの敵による", "同じ接近グループの敵の");
        if (Match(text, @"^自身への〈[^〉]+〉に対して$").Success) text = "あらゆる" + text;
        text = Replace(text, @"^([^《》]+)を宣言しない場合$", "《$1》を宣言しない場合");
        foreach (var pattern in ActionConditions)
        {
            string[] values = pattern.Read(text);
            if (values == null) continue;
            condition = new EnemyActionCondition { Type = (EnemyActionConditionType)Enum.Parse(typeof(EnemyActionConditionType), pattern.Key),
                Value = values.FirstOrDefault() ?? "", AdditionalValues = values.Skip(1).ToList() };
            return true;
        }
        return false;
    }

    private static string ActionConditionText(EnemyActionCondition condition)
    {
        if (condition == null || condition.AdditionalValues == null)
            throw new InvalidOperationException("行動条件が空です。");
        if (condition.Type == EnemyActionConditionType.OwnState)
        {
            string name = Token(condition.Value, "状態名", "《》、");
            if (condition.AdditionalValues.Count > 0)
                throw new InvalidOperationException("行動条件の状態名が不正です。");
            return "《" + name + "》状態" + (condition.Negated ? "ではない時" : "の時");
        }
        if (condition.Negated || !ActionConditionByKey.TryGetValue(condition.Type.ToString(), out var pattern))
            throw new InvalidOperationException("行動条件の種類または否定が不正です。");
        var parameters = new List<string>();
        if (!string.IsNullOrEmpty(condition.Value)) parameters.Add(condition.Value);
        parameters.AddRange(condition.AdditionalValues);
        return pattern.Write(parameters);
    }

    private static bool IsReaction(EnemyActionCondition condition)
        => condition != null &&
           (condition.Type == EnemyActionConditionType.IncomingAction || condition.Type == EnemyActionConditionType.IncomingBothCategories ||
            condition.Type == EnemyActionConditionType.OwnerActsOnEnemy || condition.Type == EnemyActionConditionType.OwnerTargeted ||
            condition.Type == EnemyActionConditionType.EnterOwnState ||
            (condition.Type >= EnemyActionConditionType.AnyAction && condition.Type <= EnemyActionConditionType.StateApplier));
}
