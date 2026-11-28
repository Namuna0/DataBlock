#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

internal sealed class RecipeTextConverterTests
{
    private const string Book = "《試験用レシピ書》\n〈レシピアイテム〉\n【レアリティ】Common\n【価値】1ジルダ\n【サイズ】小\n【説明】下記の効果を一つ選んで実行する。\n";
    private const string Divider = "――――――――――――――――";

    [Test]
    public void ConstructionPhasesKeepProductsAndGroupedMaterialAmounts()
    {
        var item = ItemTextConverter.Parse(Book +
            "◆小屋\n●土台\n【消費リソース】《釘, 板》×2, SP-5\n【発動ロール】3回連続：1d100*[器用B] 目標値30\n" +
            "【アクティブ効果】発動ロール2回以上達成で《土台》×1と《端材》×2獲得。\n" +
            "【クリティカル効果】達成回数+2, グレード+1\n" +
            "◆敷物\n【アクティブ効果】発動ロールの結果に応じて《敷物》を獲得します。\n" + Divider + "\n本の説明。");
        var items = item.Item.Skills;
        Assert.That(items.Select(e => e.Skill.Name), Is.EqualTo(new[] { "小屋", "敷物" }));
        var phase = items[0].Skill.Choices.Single();
        Assert.That(phase.Costs.Select(c => c.Resource), Is.EqualTo(new[] { "釘", "板", "SP" }));
        Assert.That(phase.Costs.Select(c => c.Amount), Is.EqualTo(new[] { 2, 2, 5 }));
        var products = phase.Effects.Where(e => e.Type == EffectType.Active).SelectMany(e => e.Contents).ToArray();
        Assert.That(products.All(p => p.Type == EffectContentType.RequireSuccessesForProduct), Is.True);
        Assert.That(products.Select(p => string.Join("/", p.Parameters)), Is.EqualTo(new[] { "2/土台/1", "2/端材/2" }));
        Assert.That(phase.Effects.Where(e => e.Type == EffectType.Critical).SelectMany(e => e.Contents).Select(c => c.Type),
            Is.EqualTo(new[] { EffectContentType.ChangeSuccessCount, EffectContentType.ChangeProductGrade }));
        var rebuilt = ItemTextConverter.Build(item);
        Assert.That(JsonUtility.ToJson(ItemTextConverter.Parse(rebuilt)), Is.EqualTo(JsonUtility.ToJson(item)));
    }

    [Test]
    public void StateExpiryAndFlavorBelongToTheRecipeThatDeclaredThem()
    {
        var item = ItemTextConverter.Parse(Book +
            "●占術\n【アクティブ効果】対象を永続的に《見通し》状態にする。\n" +
            "見通し効果：自身の[幸運B]及び[感知B]による達成値は×1.2倍される。\n" +
            "●幸運B]及び[感知B]によるダイスロールを行った後、この状態は解除される。\n" +
            "【成功効果】達成回数+1\n" + Divider + "\n占術の説明。\n" + Divider + "\n本の説明。");
        var recipe = item.Item.Skills.Single();
        var state = recipe.States.Single();
        Assert.That(state.Name, Is.EqualTo("見通し"));
        Assert.That(state.Effects.SelectMany(e => e.Contents).Last().Type, Is.EqualTo(EffectContentType.ExpireAfterTwoAbilityRolls));
        Assert.That(recipe.Skill.Effects.Any(e => e.Type == EffectType.Success), Is.True);
        Assert.That(recipe.Skill.Flavor, Is.EqualTo("占術の説明。"));
        Assert.That(item.Item.Flavor, Is.EqualTo("本の説明。"));
        Assert.That(JsonUtility.ToJson(ItemTextConverter.Parse(ItemTextConverter.Build(item))), Is.EqualTo(JsonUtility.ToJson(item)));
    }
}
#endif
