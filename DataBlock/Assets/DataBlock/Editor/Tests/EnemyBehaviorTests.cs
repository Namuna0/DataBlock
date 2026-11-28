#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;

internal sealed class EnemyBehaviorTests
{
    private static EnemyTextData ParseActions(string actions)
        => EnemyTextConverter.Parse("《行動試験》\n〈一般エネミー〉\n【危険度】☆\n【行動】\n" + actions);

    [Test]
    public void RollTableRetainsConditionsTargetRangeAndFallback()
    {
        const string text = "●毎ターン1d2+1回、《警戒》状態の時、かつ接近状態ではない時または同じ接近グループに選択可能な敵が居ない場合、ダイスロール1d6\n" +
            "- 1～4：重複しない選択可能なランダムな味方2体に《補助》を発動する。\n" +
            "味方が居ない時はランダムな敵に《補助》を発動する。\n" +
            "- 5～6：接近状態の時、何もしない。";
        var parsed = ParseActions(text);
        var rule = parsed.Enemy.Actions.Single();
        Assert.That(rule.UsesPerTurnFormula, Is.EqualTo("1d2+1"));
        Assert.That(rule.AllConditions.Single().Value, Is.EqualTo("警戒"));
        Assert.That(rule.AnyConditions.Select(c => c.Type), Is.EqualTo(new[] {
            EnemyActionConditionType.OwnState, EnemyActionConditionType.NoSelectableEnemyInMeleeGroup }));
        Assert.That(rule.BranchRoll, Is.EqualTo("1d6"));
        Assert.That(rule.Branches.Select(b => b.MinimumRoll), Is.EqualTo(new[] { 1, 0, 5 }));
        Assert.That(rule.Branches[0].MaximumRoll, Is.EqualTo(4));
        Assert.That(rule.Branches[0].TargetCount, Is.EqualTo(2));
        Assert.That(rule.Branches[0].DistinctTargets && rule.Branches[0].SelectableOnly, Is.True);
        Assert.That(rule.Branches[1].AllConditions.Single().Type, Is.EqualTo(EnemyActionConditionType.NoAllyTarget));
        Assert.That(rule.Branches[2].DoNothing, Is.True);
        string built = EnemyTextConverter.Build(parsed);
        Assert.That(EnemyTextConverter.Normalize(built), Is.EqualTo(built));
    }

    [Test]
    public void ReactionQuotaAndStateTableUseExplicitValues()
    {
        var actions = ParseActions("●毎ターン2回まで、自身への達成値50以上の〈攻撃〉に対して2回連続で《試験》を発動する。\n" +
            "●スタンバイフェーズ、《凍結》状態の敵全員に《確認》を発動する。").Enemy.Actions;
        Assert.That(actions[0].Timing, Is.EqualTo(EnemyActionTiming.Reaction));
        Assert.That(actions[0].AllConditions.Single().Type, Is.EqualTo(EnemyActionConditionType.IncomingResultMinimum));
        Assert.That(actions[0].UsesPerTurn, Is.EqualTo(2));
        Assert.That(actions[0].UpToUsesPerTurn, Is.True);
        Assert.That(actions[0].RepeatCount, Is.EqualTo(2));
        Assert.That(actions[0].Consecutive, Is.True);
        Assert.That(actions[1].TargetSelector, Is.EqualTo(EnemyActionTargetSelector.AllEnemies));
        Assert.That(actions[1].TargetFilters.Single().Values, Is.EqualTo(new[] { "凍結" }));
    }

    [Test]
    public void PreservesStackThresholdAndBothIncomingCategories()
    {
        var parsed = ParseActions("●毎ターン1回、《印》スタックを2以上持つ選択可能な敵全体に《解放》を発動する。\n" +
            "●あらゆる自身への〈攻撃〉かつ〈魔法〉に対して《反撃》を発動する。");
        var all = parsed.Enemy.Actions[0];
        Assert.That(all.TargetSelector, Is.EqualTo(EnemyActionTargetSelector.AllEnemies));
        Assert.That(all.SelectableOnly, Is.True);
        var threshold = all.TargetFilters.Single();
        Assert.That(threshold.Type, Is.EqualTo(EnemyTargetFilterType.StateStacksAtLeast));
        Assert.That(threshold.Values, Is.EqualTo(new[] { "印", "2" }));
        var reaction = parsed.Enemy.Actions[1];
        Assert.That(reaction.Timing, Is.EqualTo(EnemyActionTiming.Reaction));
        var condition = reaction.AllConditions.Single();
        Assert.That(condition.Type, Is.EqualTo(EnemyActionConditionType.IncomingBothCategories));
        Assert.That(condition.Value, Is.EqualTo("攻撃"));
        Assert.That(condition.AdditionalValues, Is.EqualTo(new[] { "魔法" }));
        string built = EnemyTextConverter.Build(parsed);
        Assert.That(EnemyTextConverter.Normalize(built), Is.EqualTo(built));
    }

    [TestCase("《試験》", EnemyActionTiming.Listed)]
    [TestCase("何もしない。", EnemyActionTiming.None)]
    public void DistinguishesUndescribedActionFromInaction(string body, EnemyActionTiming timing)
    {
        var parsed = ParseActions(body);
        Assert.That(parsed.Enemy.Actions.Single().Timing, Is.EqualTo(timing));
        Assert.That(EnemyTextConverter.Parse(EnemyTextConverter.Build(parsed)).Enemy.Actions.Single().Timing, Is.EqualTo(timing));
    }

    [TestCase("1～2", "2～3")]
    [TestCase("1～2", "3～4")]
    public void RejectsInvalidDiceRanges(string first, string second)
    {
        Assert.Throws<InvalidOperationException>(() => ParseActions("●毎ターン1回、ダイスロール1d3\n- " + first + "：《甲》を発動する。\n- " + second + "：《乙》を発動する。"));
    }

    [TestCase("達成値が次の時、対象に2ターン状態を付与する。\n- 99～104：《凍結》", "TargetStateFromAchievement")]
    [TestCase("対象はダイスロール1d6を行い、2ターンの間次の状態が付与される。\n1：《凍結》\n2～6：何もなし", "TargetStateFromTargetRoll")]
    public void SharesStateTablesWithSkillConverter(string body, string kind)
    {
        var data = SkillTextConverter.Parse("《試験》\n〈魔法〉\n【アクティブ効果】" + body);
        var table = data.Skill.Effects.Single().Contents;
        Assert.That(table[0].Type, Is.EqualTo(EffectContentType.RollTable));
        Assert.That(table[0].Parameters[0], Is.EqualTo(kind));
        Assert.That(table[1].Parameters.Last(), Is.EqualTo("凍結"));
        string built = SkillTextConverter.Build(data);
        Assert.That(SkillTextConverter.Normalize(built), Is.EqualTo(built));
    }
}
#endif
