#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

internal sealed class ConsumableEffectTests
{
    [Test]
    public void RecoversEachResourceUsingItsOwnMaximum()
    {
        var data = SkillTextConverter.Parse("《試料》\n【アクティブ効果】自身のHP, SP, SAN, MPを最大値の4%回復する。");
        var effects = data.Skill.Effects.SelectMany(e => e.Contents).ToArray();
        Assert.That(effects.Select(e => e.Parameters[1]), Is.EqualTo(new[] { "HP", "SP", "SAN", "MP" }));
        foreach (var effect in effects)
        {
            Assert.That(effect.Type, Is.EqualTo(EffectContentType.RestoreResource));
            Assert.That(effect.Parameters[2], Is.EqualTo("[" + effect.Parameters[1] + "最大値]*4/100"));
        }
        var rebuilt = SkillTextConverter.Build(data);
        StringAssert.Contains("自身はHPを最大値の4%回復する。", rebuilt);
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(rebuilt)), Is.EqualTo(JsonUtility.ToJson(data)));
    }

    [Test]
    public void GroupedIngredientCostConsumesEachListedItem()
    {
        var data = SkillTextConverter.Parse("《調合》\n【消費リソース】SP-2, 《素材甲、素材乙》×3\n【アクティブ効果】自身のHPを10回復する。");
        Assert.That(data.Skill.Costs.Select(c => c.Resource), Is.EqualTo(new[] { "SP", "素材甲", "素材乙" }));
        Assert.That(data.Skill.Costs.Select(c => c.Amount), Is.EqualTo(new[] { 2, 3, 3 }));
    }

    [Test]
    public void RepeatedRollKeepsBothEffectsOnTheSameOutcome()
    {
        var data = SkillTextConverter.Parse("《抽選試験》\n【アクティブ効果】ダイスロール1d6を2回行い効果を受ける。\n- 1：自身のHPとSANは500減少する。\n- 6：HPとMPを1000を回復する。");
        var effects = data.Skill.Effects;
        var roll = effects[0].Contents.Single();
        Assert.That(roll.Type, Is.EqualTo(EffectContentType.RollDice));
        Assert.That(roll.Parameters, Is.EqualTo(new[] { "1", "1d6", "2" }));
        Assert.That(effects.Skip(1).Select(e => e.Triggers.Single().Conditions.And.Single().Parameters.Last()),
            Is.EqualTo(new[] { "1", "1", "6", "6" }));
        Assert.That(effects.Skip(1).Select(e => e.Contents.Single().Parameters[2]), Is.EqualTo(new[] { "-500", "-500", "1000", "1000" }));
        var rebuilt = SkillTextConverter.Build(data);
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(rebuilt)), Is.EqualTo(JsonUtility.ToJson(data)));
    }

    [Test]
    public void SecondaryCheckPreservesTheUseRollAndTheStateDefinition()
    {
        var data = SkillTextConverter.Parse("《試飲》\n【発動ロール】自動成功\n【アクティブ効果】自身は《軽減》状態になる。\n軽減効果〈マインド〉：自身がSPを消費する際、消費量は-2される。\n行為判定1d100*[生命B] 目標値40を行う。\n失敗した時、自身は永続的に《酔い》スタックを3得る。");
        var check = data.Skill.Effects.SelectMany(e => e.Contents).Single(c => c.Type == EffectContentType.FailedCheckPermanentStacks);
        Assert.That(check.Parameters, Is.EqualTo(new[] { "1d100*[生命B]", "40", "酔い", "3" }));
        Assert.That(data.States.Single().Effects.Single().Contents.Single().Parameters, Is.EqualTo(new[] { "ResourceCostDelta", "SP", "-2" }));
        var rebuilt = SkillTextConverter.Build(data);
        StringAssert.Contains("【発動ロール】自動成功", rebuilt);
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(rebuilt)), Is.EqualTo(JsonUtility.ToJson(data)));
    }
}
#endif
