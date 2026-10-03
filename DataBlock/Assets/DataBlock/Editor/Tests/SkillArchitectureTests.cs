#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

internal sealed class SkillArchitectureTests
{
    private static string Source(params string[] lines)
    {
        return "《任意技術》\n〈任意分類〉\n" + string.Join("\n", lines);
    }

    private static SkillTextData RoundTrip(string source)
    {
        var data = SkillTextConverter.Parse(source);
        string built = SkillTextConverter.Build(data);
        Assert.That(SkillTextConverter.Normalize(source), Is.EqualTo(built));
        Assert.That(SkillTextConverter.Normalize(built), Is.EqualTo(built));
        Assert.That(SkillTextConverter.Build(SkillTextConverter.Parse(built)), Is.EqualTo(built));
        return data;
    }

    private static IEnumerable<EffectContent> Contents(SkillTextData data)
    {
        return data.Skill.Effects.SelectMany(x => x.Contents);
    }

    private static IEnumerable<OverrideContent> Overrides(SkillTextData data, EffectType stage)
    {
        return data.Skill.Overrides.Where(x => x.Type == stage).SelectMany(x => x.Contents);
    }

    [Test]
    public void AcquisitionCostSpikes_PreserveTheirModifierTarget()
    {
        var data = RoundTrip(Source(
            "【パッシブ効果】スキル習得に必要な消費決意が-3される。",
            "【セカンドスパイク】このパッシブ効果による消費決意は-5に変化する。",
            "【サードスパイク】このパッシブ効果による消費決意は-7に変化する。"));
        Assert.That(Overrides(data, EffectType.Passive).Single().Type, Is.EqualTo(OverrideContentType.ModifyAcquisitionCost));
        Assert.That(Overrides(data, EffectType.Passive).Single().Parameters,
            Is.EqualTo(new[] { "Self", "決意", "Add", "AllSkills", "All", "-3" }));
        Assert.That(Overrides(data, EffectType.SecondSpike).Single().Parameters,
            Is.EqualTo(new[] { "Rule", "AcquisitionCost", "Add", "AllSkills", "All", "-5" }));
        Assert.That(Overrides(data, EffectType.ThirdSpike).Single().Parameters.Last(), Is.EqualTo("-7"));
    }

    [Test]
    public void RerollCounter_PreservesNormalFailureAndReplacementAbility()
    {
        var data = RoundTrip(Source(
            "【宣言条件】戦闘中1度だけ, 発動ロールに通常失敗した時に宣言可能。",
            "【発動ロール】自動成功",
            "【カウンター効果】能力値ボーナスを感知Bに置きかえて発動ロールをやり直す。",
            "【セカンドスパイク】このカウンター効果による[感知B]の達成値は×1.1倍される。"));
        Assert.That(data.Skill.DeclarationConditions.And.Single(x => x.Type == ConditionType.RollResult).Parameters,
            Is.EqualTo(new[] { "Activation", "NormalFailure" }));
        Assert.That(Contents(data).Single().Parameters, Is.EqualTo(new[] { "Self", "感知B", "TriggeringRoll" }));
        Assert.That(Overrides(data, EffectType.SecondSpike).Single().Parameters,
            Is.EqualTo(new[] { "Rule", "RerollResult", "感知B", "1.1" }));
    }

    [Test]
    public void CategoryModifiersAndCyclingStacks_PreserveSelectorsAndReset()
    {
        var data = RoundTrip(Source(
            "【パッシブ効果】自身への〈魔法〉の発動ロールの目標値が+5される。",
            "〈精霊魔法〉による消費MPが-1される。",
            "〈精霊魔法〉を発動するたびに《任意状態》〈顕現〉状態を1スタック得る。",
            "3スタック得た時、ACTは+2回復して、スタックは0になる。"));
        var target = data.Skill.Overrides.Single(x => x.Contents.Any(c => c.Type == OverrideContentType.AddTargetValue));
        Assert.That(target.Triggers.Single().Conditions.And.Single(x => x.Type == ConditionType.ActionTarget).Parameters, Is.EqualTo(new[] { "Self" }));
        Assert.That(target.Triggers.Single().Conditions.And.Single(x => x.Type == ConditionType.CheckContext).Parameters, Is.EqualTo(new[] { "Activation" }));
        Assert.That(Overrides(data, EffectType.Passive).Single(x => x.Type == OverrideContentType.AddResourceCost).Parameters,
            Is.EqualTo(new[] { "Self", "MP", "-1", "Automatic" }));
        Assert.That(Contents(data).Single(x => x.Type == EffectContentType.CyclingStateStacks).Parameters,
            Is.EqualTo(new[] { "Self", "精霊魔法", "任意状態", "顕現", "1", "3", "ACT", "2", "0" }));
    }

    [Test]
    public void SelectedSkillState_RemainsValidAfterSkillAndStateRenaming()
    {
        string source = Source(
            "【宣言条件】キャラクター1体のアクティブ効果を持つスキルを1つ選択して宣言可能。",
            "【発動ロール】自動成功",
            "【アクティブ効果】対象スキルを2ターンの間《任意封印》状態にする。",
            "任意封印効果：自身は指定されたスキルを宣言する事が出来なくなる。");
        var before = RoundTrip(source);
        var renamed = RoundTrip(source.Replace("任意技術", "別の技術").Replace("任意封印", "別の封印"));
        Assert.That(Contents(before).Single().Type, Is.EqualTo(EffectContentType.ApplySelectedSkillState));
        Assert.That(Contents(renamed).Single().Parameters, Is.EqualTo(new[] { "SelectedSkill", "別の封印", "2" }));
        Assert.That(renamed.States.Single().Name, Is.EqualTo("別の封印"));
        Assert.That(renamed.States.Single().Effects.Single().Contents.Single().Type, Is.EqualTo(EffectContentType.ProhibitSelectedSkill));
    }

    [Test]
    public void IncapacitatedReaction_PreservesUseLimitAndResetCategory()
    {
        var data = RoundTrip(Source(
            "【宣言条件】戦闘中、自身が《任意状態》状態の時に一度だけ、あらゆるキャラクターの行為判定に対して（〈回復〉を受けた場合回数リセット）宣言可能。",
            "【発動ロール】自動成功",
            "【カウンター効果】対象の行為判定をクリティカル、もしくはファンブルに変える事が出来る。"));
        Assert.That(data.Skill.DeclarationConditions.And.Single().Type, Is.EqualTo(ConditionType.IncapacitatedCheckReaction));
        Assert.That(data.Skill.DeclarationConditions.And.Single().Parameters,
            Is.EqualTo(new[] { "任意状態", "Battle", "1", "AnyCharacterAction", "回復" }));
        Assert.That(Contents(data).Single().Type, Is.EqualTo(EffectContentType.ChooseCheckOutcome));
    }

    [Test]
    public void AppliedStateDurationSpikes_PreserveStateIdentity()
    {
        var data = RoundTrip(Source(
            "【宣言条件】キャラクターを1体選択して宣言可能。",
            "【発動ロール】1d100*[魅力B] 目標値30",
            "【アクティブ効果】2ターンの間、対象は《任意状態》状態になる。",
            "【セカンドスパイク】このアクティブ効果による《任意状態》付与は3ターンに変化する。",
            "【サードスパイク】このアクティブ効果による《任意状態》付与は4ターンに変化する。"));
        Assert.That(Contents(data).Single().Parameters, Is.EqualTo(new[] { "Target", "任意状態", "2" }));
        Assert.That(Overrides(data, EffectType.SecondSpike).Single().Type, Is.EqualTo(OverrideContentType.SetAppliedStateDuration));
        Assert.That(Overrides(data, EffectType.SecondSpike).Single().Parameters, Is.EqualTo(new[] { "Target", "任意状態", "3" }));
        Assert.That(Overrides(data, EffectType.ThirdSpike).Single().Parameters, Is.EqualTo(new[] { "Target", "任意状態", "4" }));
    }

    [Test]
    public void ResourceAbsenceAndConditionalAttack_KeepDistinctMeanings()
    {
        var passive = RoundTrip(Source(
            "【パッシブ効果】自身はSPが存在しない。",
            "自身は《任意状態》状態にならない。"));
        Assert.That(Contents(passive).Single(x => x.Type == EffectContentType.AbsentResource).Parameters, Is.EqualTo(new[] { "SP" }));
        Assert.That(Contents(passive).Single(x => x.Type == EffectContentType.StateImmunity).Parameters, Is.EqualTo(new[] { "任意状態" }));
        var attack = RoundTrip(Source(
            "【宣言条件】キャラクターを1体選択して宣言可能。",
            "【発動ロール】1d100*[筋力B] 目標値30",
            "【アクティブ効果】対象を50の威力で攻撃する。",
            "既に対象が《任意状態》状態の時、威力は×1.5倍される。"));
        Assert.That(Overrides(attack, EffectType.Active).Single().Type, Is.EqualTo(OverrideContentType.PowerAgainstState));
        Assert.That(Overrides(attack, EffectType.Active).Single().Parameters, Is.EqualTo(new[] { "任意状態", "1.5" }));
        attack.Skill.Effects.Clear();
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Build(attack));
    }

    [Test]
    public void DeferredCostAndTimedStacks_PreserveResourcesAndExpiry()
    {
        var data = RoundTrip(Source(
            "【宣言条件】キャラクターを1体選択して宣言可能。",
            "【消費リソース】ACT-6, MP-3",
            "【発動ロール】1d100*[魔力B] 目標値30",
            "【パッシブ効果】このスキルの消費リソースのACTが足りない時、可能な限り支払い、不足分を自身の次のターン開始時に消費する事でアクティブ効果を発動できる。",
            "このスキルの消費ACTは自身の《任意状態》スタック1つにつき-2される。（最低消費1）",
            "自身の《任意状態》スタックが3以上の場合、このスキルの威力は2倍される。",
            "【アクティブ効果】対象を50の威力で攻撃する。",
            "このスキルの攻撃後、自身の《任意状態》スタックは全て除去される。",
            "対象は2ターンの間《任意状態》スタックを3得る。"));
        Assert.That(Contents(data).Single(x => x.Type == EffectContentType.DeferMissingResourceCost).Parameters, Is.EqualTo(new[] { "ACT" }));
        Assert.That(Contents(data).Single(x => x.Type == EffectContentType.TimedTargetStacks).Parameters, Is.EqualTo(new[] { "任意状態", "3", "2" }));
        Assert.That(Overrides(data, EffectType.Passive).Single(x => x.Type == OverrideContentType.ThisSkillStackCost).Parameters,
            Is.EqualTo(new[] { "任意状態", "ACT", "2", "1" }));
        data.Skill.Costs.RemoveAll(x => x.Resource == "ACT");
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Build(data));
    }

    [Test]
    public void AutomaticRoll_PreservesItsFixedAchievement()
    {
        var data = RoundTrip(Source(
            "【宣言条件】キャラクターを1体選択して宣言可能。",
            "【発動ロール】自動成功、達成値は50となる。",
            "【アクティブ効果】対象に30のSANダメージを与える。"));
        Assert.That(data.Skill.Roll.FixedResult, Is.EqualTo(50));
        Assert.That(data.Skill.Roll.Formula, Is.EqualTo("自動成功"));
        Assert.That(data.Skill.Roll.Target, Is.Empty);
        data.Skill.Roll.Formula = "1d100*[魔力B]";
        data.Skill.Roll.Target = "30";
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Build(data));
    }

    [Test]
    public void RepeatedRollWithoutExplicitChoices_IsRejected()
    {
        string source = Source(
            "【発動ロール】1d100*[敏捷B] 目標値30",
            "【アクティブ効果】HP5回復",
            "【発動ロール】1d100*[感知B] 目標値40",
            "【アクティブ効果】HP10回復");
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Parse(source));
    }

    [TestCase("アルゼーペン近郊", "モンスター", "40", "20", "0.4", "0.2")]
    [TestCase("別の初期エリア", "別の種族", "60", "15", "0.6", "0.15")]
    public void CharacterRules_AcceptAreaRaceAndBonusVariants(string area, string race,
        string totalPercent, string capPercent, string totalRatio, string capRatio)
    {
        var data = RoundTrip(Source(
            "【パッシブ効果】キャラクター作成時、自身はエリア《" + area + "》に移動する。",
            "種族《" + race + "》以外とパーティーを組むことはできない。",
            "任意の種類の種族能力値ボーナスを合計+" + totalPercent + "%まで選んで振り分ける事が出来る。",
            "※各能力値毎に振り分けられる種族能力値ボーナスは最大+" + capPercent + "%の加算まで。"));
        Assert.That(Contents(data).Select(x => x.Type), Is.All.EqualTo(EffectContentType.CharacterRule));
        var rules = Contents(data).ToDictionary(x => x.Parameters[0]);
        Assert.That(rules["StartingArea"].Parameters, Is.EqualTo(new[] { "StartingArea", "CharacterCreation", area }));
        Assert.That(rules["PartyRaceRestriction"].Parameters, Is.EqualTo(new[] { "PartyRaceRestriction", race, "Only" }));
        Assert.That(rules["AllocateRaceBonuses"].Parameters, Is.EqualTo(new[] { "AllocateRaceBonuses", "Any", totalRatio }));
        Assert.That(rules["RaceBonusCap"].Parameters, Is.EqualTo(new[] { "RaceBonusCap", "PerStat", capRatio }));
        rules["StartingArea"].Parameters[2] = "三つ目のエリア";
        rules["AllocateRaceBonuses"].Parameters[2] = "0.375";
        var edited = RoundTrip(SkillTextConverter.Build(data));
        Assert.That(Contents(edited).Single(x => x.Parameters[0] == "StartingArea").Parameters.Last(), Is.EqualTo("三つ目のエリア"));
        Assert.That(Contents(edited).Single(x => x.Parameters[0] == "AllocateRaceBonuses").Parameters.Last(), Is.EqualTo("0.375"));
    }

    [Test]
    public void CharacterRuleRecovery_UsesEditableRatioAndRejectsOverMaximum()
    {
        var data = RoundTrip(Source("【パッシブ効果】エリア移動時に全てのリソースを35%回復する。"));
        var rule = Contents(data).Single();
        Assert.That(rule.Type, Is.EqualTo(EffectContentType.CharacterRule));
        Assert.That(rule.Parameters, Is.EqualTo(new[] { "AreaMoveRecovery", "AllResources", "MaxRatio", "0.35" }));
        rule.Parameters[3] = "0.125";
        Assert.That(SkillTextConverter.Build(data), Does.Contain("12.5%回復する。"));
        RoundTrip(SkillTextConverter.Build(data));
        rule.Parameters[3] = "1.01";
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Build(data));
    }

    [Test]
    public void StackAmountSpikes_AreSeparatePerStageAndRejectDuplicatesWithinStage()
    {
        var data = RoundTrip(Source(
            "【宣言条件】キャラクターを1体選択して宣言可能。",
            "【発動ロール】1d100*[魅力B] 目標値30",
            "【アクティブ効果】対象に《任意状態》スタックを1付与する。",
            "【セカンドスパイク】このアクティブ効果による《任意状態》スタックの付与は2に変化する。",
            "【サードスパイク】このアクティブ効果による《任意状態》スタックの付与は3に変化する。"));
        Assert.That(Overrides(data, EffectType.SecondSpike).Single().Parameters, Is.EqualTo(new[] { "Target", "任意状態", "2" }));
        Assert.That(Overrides(data, EffectType.ThirdSpike).Single().Parameters, Is.EqualTo(new[] { "Target", "任意状態", "3" }));
        data.Skill.Overrides.Add(data.Skill.Overrides.Single(x => x.Type == EffectType.SecondSpike));
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Build(data));
    }

    [Test]
    public void StateAttackTurnOwner_DistinguishesHolderFromApplier()
    {
        string holderSource = Source(
            "【宣言条件】キャラクターを1体選択して宣言可能。",
            "【発動ロール】自動成功",
            "【アクティブ効果】対象へ2ターンの間《任意状態》状態を付与する。",
            "任意状態効果〈スネア〉：自身のターン開始時に対象を25の威力で攻撃する。");
        var holder = RoundTrip(holderSource);
        var holderEffect = holder.States.Single().Effects.Single();
        Assert.That(holderEffect.Contents.Single().Type, Is.EqualTo(EffectContentType.SkillAttack));
        Assert.That(holderEffect.Contents.Single().Parameters, Is.EqualTo(new[] { "Target", "25" }));
        Assert.That(holderEffect.Triggers.Single().Timing, Is.EqualTo(TriggerTiming.TurnStart));
        Assert.That(holderEffect.Triggers.Single().Conditions.And.Single().Parameters, Is.EqualTo(new[] { "Self" }));
        var applier = RoundTrip(holderSource.Replace("自身のターン開始時に対象を25の威力で攻撃する。",
            "付与者のターン開始時に付与者がこの状態の対象を25の威力で攻撃する。（付与者の現在のステータスを参照）"));
        Assert.That(applier.States.Single().Effects.Single().Contents.Single().Type, Is.EqualTo(EffectContentType.ApplierTurnAttack));
    }

    [TestCase("任意状態効果〈スネア〉：")]
    [TestCase("任意状態〈スネア〉：")]
    public void StateHeaderNormalization_PreservesNameAndIsIdempotent(string header)
    {
        var data = RoundTrip(Source(
            "【発動ロール】自動成功",
            "【アクティブ効果】自身は2ターンの間《任意状態》状態になる。",
            header + "自身は自身のターン開始時に10のダメージを受ける。"));
        Assert.That(data.States.Single().Name, Is.EqualTo("任意状態"));
    }

    [Test]
    public void ElementalAttackSpike_PreservesSeparateSkillValueAndAttributePower()
    {
        var data = RoundTrip(Source(
            "【宣言条件】装備している〈魔法武器〉を1つ選択, キャラクターを1体選択して宣言可能。",
            "【発動ロール】1d100*[知力B] 目標値39",
            "【アクティブ効果】対象をスキル値100の武器威力+60*([火属性B]+[土属性B])の火かつ土属性威力で攻撃する。",
            "【セカンドスパイク】このアクティブ効果によるスキル値は110、属性威力は70*([火属性B]+[土属性B])に変化する。"));
        Assert.That(Overrides(data, EffectType.SecondSpike).Single(x => x.Type == OverrideContentType.SetSkillValue).Parameters,
            Is.EqualTo(new[] { "110" }));
        Assert.That(Overrides(data, EffectType.SecondSpike).Single(x => x.Type == OverrideContentType.SetAttackComponent).Parameters,
            Is.EqualTo(new[] { "AttributePower", "70*([火属性B]+[土属性B])" }));
    }

    [Test]
    public void TenThousandSkills_AreShardedAndIndexedWithoutSpecialCases()
    {
        var records = Enumerable.Range(0, 10000).Select(i =>
        {
            var record = new SkillCatalogRecord { Id = "skill-" + i };
            record.Data.Skill.Name = "技術" + i;
            record.Data.Skill.Categories.Add("分類" + (i % 10));
            return record;
        }).ToArray();
        var shards = SkillCatalogSharding.CreateShards(records);
        var catalog = SkillCatalog.FromShards(shards);
        Assert.That(shards.Sum(x => x.Records.Count), Is.EqualTo(10000));
        Assert.That(catalog.Records.Count, Is.EqualTo(10000));
        foreach (var record in records)
        {
            Assert.That(catalog.TryGetById(record.Id, out var found), Is.True);
            Assert.That(found, Is.SameAs(record));
        }
        Assert.That(catalog.FindByName("技術9999").Single(), Is.SameAs(records[9999]));
        for (int i = 0; i < 10; i++) Assert.That(catalog.FindByCategory("分類" + i).Count, Is.EqualTo(1000));
    }
}
#endif
