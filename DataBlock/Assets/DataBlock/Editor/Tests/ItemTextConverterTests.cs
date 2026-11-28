#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using System.Reflection;
using System.Text.Json.Nodes;
using UnityEditor;
using UnityEngine;

internal sealed class ItemTextConverterTests
{
    private static ItemTextData RoundTrip(string source)
    {
        var data = ItemTextConverter.Parse(source);
        string built = ItemTextConverter.Build(data);
        Assert.That(ItemTextConverter.Normalize(built), Is.EqualTo(built));
        Assert.That(JsonUtility.ToJson(ItemTextConverter.Parse(built)), Is.EqualTo(JsonUtility.ToJson(data)));
        return data;
    }

    [Test]
    public void MetadataSupportsSpecialGradesAndItemsWithoutUseActions()
    {
        var item = RoundTrip("《見本》\n〈捕獲アイテム〉\n【レアリティ】Rare【グレード】★☆☆\n【サイズ】小\n" +
            "【価値】取引不可\n【捕獲HP】300\n【所持条件】〈捕獲アイテム〉は1つまで所持可能。\n――――――――\n第一行。\n\n第三行。").Item;
        Assert.That(item.SpecialGrade, Is.EqualTo("★☆☆"));
        Assert.That(item.HoldingLimit, Is.EqualTo(1));
        Assert.That(item.Stats.Single().Formula, Is.EqualTo("300"));
        Assert.That(item.Skills.Count, Is.EqualTo(0));
        Assert.That(item.Flavor, Is.EqualTo("第一行。\n\n第三行。"));
    }

    [Test]
    public void GradeSuffixSelectsExactAndCumulativeRules()
    {
        var item = RoundTrip("《試験薬》☆5\n〈魔法薬〉\n【レアリティ】Rare\n【グレード】☆☆☆\n" +
            "【アクティブ効果】自身のHPを最大値の30%回復する。\n【グレード補正】\n" +
            "- ☆1：回復値-10%\n- ☆4～：対象の《毒》状態を解除する事が出来る。\n- ☆5：回復値+10%").Item;
        var applied = ItemTextConverter.ApplicableGradeAdjustments(item).ToList();
        Assert.That(applied.Count, Is.EqualTo(2));
        Assert.That(applied[0].Parameters.Single(), Is.EqualTo("毒"));
        Assert.That(applied[1].Parameters.Single(), Is.EqualTo("+10"));
        item.VariantGrade = 1;
        Assert.That(ItemTextConverter.ApplicableGradeAdjustments(item).Single().Parameters.Single(), Is.EqualTo("-10"));
    }

    [Test]
    public void RandomItemRewardsKeepEveryQuantityAndShareTheSameRollResult()
    {
        var item = RoundTrip("《報酬箱》\n〈アイテム〉\n【レアリティ】Rare\n【アクティブ効果】1d6のダイスロールを行う。\n" +
            "- 2～4：《木片》×1, 《小石》×3\n- 5～6：ペット《子鳥》×1").Item;
        var effects = item.OwnSkills.Single().Skill.Effects;
        var grants = effects.Where(e => e.Contents.Single().Type == EffectContentType.GrantItem).ToArray();
        Assert.That(grants.Select(e => e.Contents.Single().Parameters[2]), Is.EqualTo(new[] { "1", "3" }));
        foreach (var effect in grants)
            Assert.That(effect.Triggers.Single().Conditions.And.Single().Parameters, Is.EqualTo(new[] { "1", "2～4" }));
        Assert.That(effects.Last().Contents.Single().Parameters, Is.EqualTo(new[] { "ペット", "子鳥", "1" }));
    }

    [Test]
    public void FailedCaptureUsesTheHoldersCaptureResource()
    {
        var item = RoundTrip("《網》\n〈道具〉\n【レアリティ】Rare\n【発動ロール】1d100 目標値50\n" +
            "【失敗効果】自身は50の捕獲HPダメージを受ける。").Item;
        var content = item.OwnSkills.Single().Skill.Effects.Single().Contents.Single();
        Assert.That(content.Type, Is.EqualTo(EffectContentType.ResourceDamage));
        Assert.That(content.Parameters, Is.EqualTo(new[] { "Self", "捕獲HP", "50" }));
    }

    [Test]
    public void PetActionsAndNamedTraitsRetainTheirOwnSkillAndFlavor()
    {
        var item = RoundTrip("《小さな鳥》\n〈ペット, 動物〉\n【レアリティ】Rare\n【HP最大値】100\n" +
            "《浮遊》：自身への〈地面〉による効果を無効にする。\n【行動】\n" +
            "●毎ターン1回、選択可能なランダムな敵に《つつく》を発動する。\n" +
            "《つつく》\n〈攻撃〉\n【発動ロール】1d100 目標値20\n【アクティブ効果】対象を50+1d100の威力で攻撃する。\n" +
            "――――――――\n子スキルの説明。\n――――――――\nペットの説明。").Item;
        Assert.That(item.Actions.Count, Is.EqualTo(1));
        Assert.That(item.Traits.Single().Skill.Name, Is.EqualTo("浮遊"));
        Assert.That(item.Skills.Single().Skill.Name, Is.EqualTo("つつく"));
        Assert.That(item.Skills.Single().Skill.Flavor, Is.EqualTo("子スキルの説明。"));
        Assert.That(item.Flavor, Is.EqualTo("ペットの説明。"));
    }

    [Test]
    public void DirectInputAcceptsTheSameTransportArtifactsAsCsvCells()
    {
        const string body = "《飲み物》\n〈飲み物〉\n【レアリティ】Common\n――――――――\n説明。";
        var closed = ItemTextConverter.Normalize("```\n" + body + "\n```");
        Assert.That(ItemTextConverter.Normalize("```css\n" + body + "\n```]"), Is.EqualTo(closed));
        Assert.That(ItemTextConverter.Normalize("```css\n" + body), Is.EqualTo(closed));
        Assert.That(ItemTextConverter.Normalize("````\n" + body), Is.EqualTo(closed));
    }

    [Test]
    public void StandalonePrizeTablesUseTheSameSkillGrammarAsTheCsvImporter()
    {
        const string source = "-抽選-\n【消費アイテム】《参加券》×1\n【景品ロール】1d100\n- 99～100：道具《賞品》";
        var skill = SkillTextConverter.Parse(source);
        Assert.That(skill.Skill.Name, Is.EqualTo("抽選"));
        Assert.That(skill.Skill.Costs.Single().Resource, Is.EqualTo("参加券"));
        string built = SkillTextConverter.Normalize(source);
        Assert.That(built, Is.EqualTo(SkillTextConverter.Build(skill)));
        Assert.That(JsonUtility.ToJson(SkillTextConverter.Parse(built)), Is.EqualTo(JsonUtility.ToJson(skill)));
    }

    [Test]
    public void MatchingItemAndGrantedSkillNamesRetainBothRolesAfterRename()
    {
        const string header = "《同名》\n〈道具〉\n【レアリティ】Rare\n";
        const string grant = "●スキル：《同名》\n【アクティブ効果】自身のHPを10回復する。";
        Assert.That(RoundTrip(header + grant).Item.OwnSkills, Is.Empty);
        var data = RoundTrip(header + "【アクティブ効果】自身のHPを20回復する。\n" + grant);
        Assert.That(data.Item.OwnSkills.Count, Is.EqualTo(1));
        Assert.That(data.Item.Skills.Count, Is.EqualTo(1));
        data.Item.Name = "改名";
        var renamed = RoundTrip(ItemTextConverter.Build(data)).Item;
        Assert.That(renamed.OwnSkills.Single().Skill.Name, Is.EqualTo("改名"));
        Assert.That(renamed.Skills.Single().Skill.Name, Is.EqualTo("同名"));
    }

    [Test]
    public void InspectorCanReorderSameCategorySkillsIndependentlyOfTheItemEffect()
    {
        var gameObject = new GameObject("ItemInspectorTest");
        Editor editor = null;
        try
        {
            var block = gameObject.AddComponent<ItemDataBlock>();
            block.Data = ItemTextConverter.Parse("《見本》\n〈道具〉\n【レアリティ】Rare\n" +
                "【アクティブ効果】自身のHPを21回復する。\n自身は《守護》状態になる。\n守護効果：防御点+1\n" +
                "●スキル：《見本》\n〈道具〉\n【アクティブ効果】自身のHPを7回復する。\n" +
                "●スキル：《予備》\n〈道具〉\n【アクティブ効果】自身のHPを3回復する。");
            editor = Editor.CreateEditor(block);
            var serialized = editor.serializedObject;
            serialized.FindProperty("Data.Item.Skills").MoveArrayElement(1, 0);
            serialized.FindProperty("Data.Item.Name").stringValue = "改名";
            serialized.ApplyModifiedProperties();
            var run = typeof(ItemDataBlockEditor).GetMethod("Run", BindingFlags.Instance | BindingFlags.NonPublic);
            run.Invoke(editor, new object[] { 2 });
            var text = (string)typeof(ItemDataBlockEditor).GetField("_text", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
            var rebuilt = ItemTextConverter.Parse(text).Item;
            Assert.That(rebuilt.OwnSkills.Single().Skill.Name, Is.EqualTo("改名"));
            Assert.That(rebuilt.OwnSkills.Single().Skill.Effects.SelectMany(e => e.Contents)
                .Single(c => c.Type == EffectContentType.RestoreResource).Parameters.Last(), Is.EqualTo("21"));
            Assert.That(rebuilt.Skills.Select(s => s.Skill.Name), Is.EqualTo(new[] { "予備", "見本" }));
            run.Invoke(editor, new object[] { 3 });
            var preview = ((ItemDataBlockEditor)editor).JsonPreview;
            var json = JsonNode.Parse(preview.Main)["Item"];
            Assert.That(json["OwnSkills"][0].AsObject().ContainsKey("States"), Is.False);
            Assert.That((string)JsonNode.Parse(preview.States)[0]["Name"], Is.EqualTo("守護"));
            Assert.That((string)json["OwnSkills"][0]["Skill"]["Name"], Is.EqualTo("改名"));
            Assert.That(json["Skills"].AsArray().Select(s => (string)s["Skill"]["Name"]), Is.EqualTo(new[] { "予備", "見本" }));
        }
        finally
        {
            if (editor != null) Object.DestroyImmediate(editor);
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void HousingStockCanBeConfiguredAndAnUnfinishedInstallationStaysDeferred()
    {
        const string header = "《家具》\n〈家具アイテム〉\n【レアリティ】Rare\n【必要敷地】1\n【設置効果】";
        var shop = RoundTrip(header + "\n・設置されたハウジングエリアに訪れたキャラクターは、ハウジングエリアの所有者が設定した〈食べ物〉アイテム、価格、在庫数から、買取取引を行うことができる。\n" +
            "取引は所有者が別エリアにいる、または不在でも成立する。\n・設定するアイテム").Item;
        Assert.That(shop.Stock.Count, Is.EqualTo(0));
        shop.Stock.Add(new ItemStockEntry { Name = "パン", Price = 5, Quantity = 3 });
        var offered = RoundTrip(ItemTextConverter.Build(new ItemTextData { Item = shop })).Item.Stock.Single();
        Assert.That(offered.Name, Is.EqualTo("パン"));
        Assert.That(offered.Price, Is.EqualTo(5));
        Assert.That(offered.Quantity, Is.EqualTo(3));
        var deferred = RoundTrip(header + "保留\n採取対象は未確定。").Item;
        Assert.That(deferred.DeferredInstalledEffect, Is.EqualTo("採取対象は未確定。"));
        Assert.That(deferred.Skills.Count, Is.EqualTo(0));
    }
}
#endif
