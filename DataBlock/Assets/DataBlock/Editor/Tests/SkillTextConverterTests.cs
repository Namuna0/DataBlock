#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

internal sealed class SkillTextConverterTests
{
    private const string MinimalSkill =
        "```\n" +
        "《試験》\n" +
        "〈攻撃〉\n" +
        "【宣言条件】キャラクターを1体選択して宣言可能。\n" +
        "【アクティブ効果】対象をスキル値10で武器攻撃する。\n" +
        "```";

    [Test]
    public void ParseBuildAndNormalize_RoundTrip()
    {
        SkillTextData parsed = SkillTextConverter.Parse(MinimalSkill);
        string normalized = SkillTextConverter.Normalize(MinimalSkill);

        Assert.That(parsed.Skill.Name, Is.EqualTo("試験"));
        Assert.That(parsed.Skill.Effects.Count, Is.EqualTo(1));
        Assert.That(SkillTextConverter.Build(parsed), Is.EqualTo(normalized));
    }

    [Test]
    public void Parse_RejectsMultipleSkills()
    {
        string multiple = MinimalSkill + "\n\n" + MinimalSkill.Replace("試験", "試験2");
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Parse(multiple));

        StringAssert.Contains("2件のスキル", error.Message);
    }

    [Test]
    public void Batch_InvalidItemDoesNotStopFollowingItems()
    {
        var sources = new[]
        {
            new SkillTextSource("skill-1", MinimalSkill),
            new SkillTextSource("broken", "|"),
            new SkillTextSource("skill-2", MinimalSkill.Replace("試験", "試験2")),
            new SkillTextSource("skill-2", MinimalSkill)
        };

        List<SkillTextBatchResult> results = SkillTextBatchProcessor.Parse(sources).ToList();

        Assert.That(results.Select(x => x.Succeeded), Is.EqualTo(new[] { true, false, true, false }));
        Assert.That(results[1].ErrorCode, Is.EqualTo(SkillTextBatchErrorCode.InvalidSkillText));
        Assert.That(results[3].ErrorCode, Is.EqualTo(SkillTextBatchErrorCode.DuplicateId));
    }

    [Test]
    public void ShardsAndCatalog_UseStableIds()
    {
        var records = new[]
        {
            Record("skill-a", "名前A", "攻撃"),
            Record("skill-b", "名前B", "回復")
        };

        int first = SkillCatalogSharding.GetShardIndex("skill-a");
        int second = SkillCatalogSharding.GetShardIndex("skill-a");
        IReadOnlyList<SkillCatalogShard> shards = SkillCatalogSharding.CreateShards(records);
        SkillCatalog catalog = SkillCatalog.FromShards(shards);

        Assert.That(first, Is.EqualTo(second));
        Assert.That(shards.Sum(x => x.Records.Count), Is.EqualTo(2));
        Assert.That(catalog.Count, Is.EqualTo(2));
        Assert.That(catalog.FindByCategory("攻撃").Single().Id, Is.EqualTo("skill-a"));
        Assert.That(catalog.TryGetById("skill-b", out SkillCatalogRecord found), Is.True);
        Assert.That(found.Data.Skill.Name, Is.EqualTo("名前B"));
    }

    [Test]
    public void AdventurerEvasion_ActivationRollSpikesRoundTrip()
    {
        string source = ReadFixture("AdventurerSkills.txt");
        string normalized = SkillTextConverter.Normalize(source);
        SkillTextData parsed = SkillTextConverter.Parse(normalized);

        Assert.That(SkillTextConverter.Build(parsed), Is.EqualTo(normalized));
        StringAssert.DoesNotContain("```css", normalized);
        Assert.That(parsed.Skill.Name, Is.EqualTo("回避"));
        Assert.That(parsed.Skill.Categories, Is.EqualTo(new[] { "冒険者スキル", "回避" }));
        Assert.That(parsed.Skill.Roll.Count, Is.EqualTo(1));
        Assert.That(parsed.Skill.Roll.Formula, Is.EqualTo("1d100*0.75*[敏捷B]"));
        Assert.That(parsed.Skill.Roll.Target, Is.EqualTo("[対象の達成値]"));

        OverrideContent second = parsed.Skill.Overrides.Single(x => x.Type == EffectType.SecondSpike).Contents.Single();
        OverrideContent third = parsed.Skill.Overrides.Single(x => x.Type == EffectType.ThirdSpike).Contents.Single();
        Assert.That(second.Type, Is.EqualTo(OverrideContentType.SetActivationRollFormula));
        Assert.That(second.Parameters, Is.EqualTo(new[] { "1d100*[敏捷B]*0.8" }));
        Assert.That(third.Type, Is.EqualTo(OverrideContentType.SetActivationRollFormula));
        Assert.That(third.Parameters, Is.EqualTo(new[] { "1d100*[敏捷B]*0.85" }));
        StringAssert.Contains("【セカンドスパイク】この発動ロールは1d100*[敏捷B]*0.8に変化する。", normalized);
        StringAssert.Contains("【サードスパイク】この発動ロールは1d100*[敏捷B]*0.85に変化する。", normalized);
    }

    [Test]
    public void ActivationRollSpike_RejectsInvalidTargetsAndDuplicates()
    {
        string source = ReadFixture("AdventurerSkills.txt");
        SkillTextData automatic = SkillTextConverter.Parse(source);
        automatic.Skill.Roll.Formula = "自動成功";
        automatic.Skill.Roll.Target = "";
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Build(automatic));

        SkillTextData duplicate = SkillTextConverter.Parse(source);
        duplicate.Skill.Overrides.Single(x => x.Type == EffectType.SecondSpike).Contents.Add(new OverrideContent
        {
            Type = OverrideContentType.SetActivationRollFormula,
            Parameters = new List<string> { "1d100*[敏捷B]" }
        });
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Build(duplicate));
    }

    [Test]
    public void KnightSkills_AllNineNormalizeAndRoundTrip()
    {
        string source = ReadFixture("KnightSkills.txt");
        string normalized = SkillTextConverter.Normalize(source);
        MatchCollection blocks = Regex.Matches(normalized, @"(?s)```.*?```");
        string[] expectedNames =
        {
            "堅守", "ランスチャージ", "シールドバッシュ", "カウンタースタンス", "ピアシングウェイブ",
            "ハードストライク", "ショルダータックル", "パーフェクトガード", "ランスマスタリー"
        };

        Assert.That(blocks.Count, Is.EqualTo(expectedNames.Length));
        Assert.That(SkillTextConverter.Normalize(normalized), Is.EqualTo(normalized));
        StringAssert.Contains("対象のACTを-1変化させる。", normalized);
        StringAssert.Contains("このカウンター効果による軽減は115+2d100*[筋力B]に変化する。", normalized);
        for (int i = 0; i < blocks.Count; i++)
        {
            SkillTextData parsed = SkillTextConverter.Parse(blocks[i].Value);
            Assert.That(parsed.Skill.Name, Is.EqualTo(expectedNames[i]));
            Assert.That(SkillTextConverter.Build(parsed), Is.EqualTo(blocks[i].Value));
        }
    }

    [Test]
    public void KnightSkills_MapNewGrammarToDistinctSemanticNodes()
    {
        Dictionary<string, SkillTextData> skills = KnightSkills();

        StateDefinition ironWall = skills["堅守"].States.Single();
        Assert.That(ironWall.Trigger.Timing, Is.EqualTo(TriggerTiming.IncomingDamage));
        Assert.That(ironWall.Overrides.Single().Contents.Single().Type, Is.EqualTo(OverrideContentType.MultiplyDamageTaken));
        Assert.That(ironWall.Overrides.Single().Contents.Single().Parameters, Is.EqualTo(new[] { "Self", "0.33" }));

        SkillBody charge = skills["ランスチャージ"].Skill;
        OverrideDefinition mountedCharge = charge.Overrides.Single(x => x.Type == EffectType.Active && x.Contents.Any(c => c.Type == OverrideContentType.SetSkillValue));
        Assert.That(mountedCharge.Contents.Single().Parameters, Is.EqualTo(new[] { "150" }));
        Assert.That(mountedCharge.Triggers.Single().Conditions.And.Single().Parameters, Is.EqualTo(new[] { "騎乗" }));
        Assert.That(charge.Overrides.Count(x => x.Type == EffectType.SecondSpike), Is.EqualTo(2));
        Assert.That(charge.Overrides.Count(x => x.Type == EffectType.ThirdSpike), Is.EqualTo(2));
        Assert.That(charge.Overrides.Where(x => x.Type == EffectType.SecondSpike).SelectMany(x => x.Contents).Select(x => x.Parameters.Single()), Is.EqualTo(new[] { "110", "160" }));
        Assert.That(charge.Overrides.Where(x => x.Type == EffectType.ThirdSpike).SelectMany(x => x.Contents).Select(x => x.Parameters.Single()), Is.EqualTo(new[] { "120", "170" }));
        Assert.That(charge.Overrides.Where(x => x.Type == EffectType.SecondSpike && x.Triggers.Count > 0).Single().Triggers.Single().Timing, Is.EqualTo(TriggerTiming.SkillValue));

        SkillBody bash = skills["シールドバッシュ"].Skill;
        Assert.That(bash.DeclarationConditions.And.Single(x => x.Type == ConditionType.EquippedCategory).Parameters, Is.EqualTo(new[] { "Self", "盾" }));
        Assert.That(bash.Effects.SelectMany(x => x.Contents).Single(x => x.Type == EffectContentType.SkillAttack).Parameters, Is.EqualTo(new[] { "Target", "[〈盾〉の防御点]*1.5+1d100*[筋力B]*2" }));
        Assert.That(bash.Effects.SelectMany(x => x.Contents).Single(x => x.Type == EffectContentType.ModifyResource).Parameters, Is.EqualTo(new[] { "Target", "ACT", "-1" }));
        Assert.That(bash.Overrides.SelectMany(x => x.Contents).Single(x => x.Type == OverrideContentType.PreventCounterDamage).Parameters, Is.EqualTo(new[] { "Self", "攻撃" }));
        Assert.That(bash.Overrides.Single(x => x.Type == EffectType.Critical).Contents.Single().Parameters, Is.EqualTo(new[] { "Self", "ACT", "-1", "Automatic" }));

        StateDefinition counterStance = skills["カウンタースタンス"].States.Single();
        Assert.That(counterStance.Trigger.Timing, Is.EqualTo(TriggerTiming.ActionResult));
        Assert.That(counterStance.Trigger.Conditions.And.Single(x => x.Type == ConditionType.ActionSource).Parameters, Is.EqualTo(new[] { "Self" }));
        Assert.That(counterStance.Trigger.Conditions.And.Any(x => x.Type == ConditionType.EffectKind && x.Parameters.SequenceEqual(new[] { "Counter" })), Is.True);
        Assert.That(counterStance.Overrides.Single().Contents.Single().Type, Is.EqualTo(OverrideContentType.AddActionResult));
        Assert.That(counterStance.Overrides.Single().Contents.Single().Parameters, Is.EqualTo(new[] { "+5" }));

        SkillBody piercing = skills["ピアシングウェイブ"].Skill;
        Assert.That(piercing.DeclarationConditions.And.Single(x => x.Type == ConditionType.SelectEquippedWeaponFromCategories).Parameters, Is.EqualTo(new[] { "1", "槍", "ランス" }));

        SkillBody hardStrike = skills["ハードストライク"].Skill;
        Assert.That(hardStrike.DeclarationConditions.And.Any(x => x.Type == ConditionType.SelectEquippedWeaponFromCategories), Is.True);
        Assert.That(hardStrike.DeclarationConditions.And.Any(x => x.Type == ConditionType.SelectCharacters), Is.True);
        Assert.That(hardStrike.DeclarationConditions.Or.Select(x => x.Type), Is.EquivalentTo(new[] { ConditionType.RequiresSameMeleeGroup, ConditionType.OwnState }));
        Assert.That(hardStrike.DeclarationConditions.Or.Single(x => x.Type == ConditionType.OwnState).Parameters, Is.EqualTo(new[] { "騎乗" }));

        SkillBody tackle = skills["ショルダータックル"].Skill;
        Assert.That(tackle.DeclarationConditions.And.Single().Parameters, Is.EqualTo(new[] { "Source", "移動", "SameMeleeGroup" }));
        Assert.That(tackle.Effects.SelectMany(x => x.Contents).Single(x => x.Type == EffectContentType.SkillAttack).Parameters, Is.EqualTo(new[] { "Target", "1d100*[筋力B]*2" }));
        Assert.That(tackle.Effects.SelectMany(x => x.Contents).Single(x => x.Type == EffectContentType.InvalidateAction).Parameters, Is.EqualTo(new[] { "Target", "Single", "移動" }));

        SkillBody guard = skills["パーフェクトガード"].Skill;
        Assert.That(guard.DeclarationConditions.And.Single(x => x.Type == ConditionType.ActionTarget).Parameters, Is.EqualTo(new[] { "Receiver", "攻撃", "Any" }));
        Assert.That(guard.Effects.SelectMany(x => x.Contents).Single().Parameters, Is.EqualTo(new[] { "Target", "100+2d100*[筋力B]" }));
        Assert.That(guard.Overrides.Where(x => x.Type == EffectType.SecondSpike || x.Type == EffectType.ThirdSpike).SelectMany(x => x.Contents).All(x => x.Type == OverrideContentType.SetDamageReduction), Is.True);
        Assert.That(guard.Overrides.Where(x => x.Type == EffectType.SecondSpike || x.Type == EffectType.ThirdSpike).SelectMany(x => x.Contents).Select(x => x.Parameters.Single()), Is.EqualTo(new[] { "115+2d100*[筋力B]", "130+2d100*[筋力B]" }));

        OverrideDefinition mastery = skills["ランスマスタリー"].Skill.Overrides.Single();
        Assert.That(mastery.Contents.Single().Type, Is.EqualTo(OverrideContentType.MultiplyPower));
        Assert.That(mastery.Contents.Single().Parameters, Is.EqualTo(new[] { "1.1" }));
        Assert.That(mastery.Triggers.Single().Conditions.And.Any(x => x.Type == ConditionType.ActionOrigin && x.Parameters.SequenceEqual(new[] { "Skill" })), Is.True);
        Assert.That(mastery.Triggers.Single().Conditions.And.Single(x => x.Type == ConditionType.WeaponCategory).Parameters, Is.EqualTo(new[] { "ランス" }));
    }

    [Test]
    public void InputBlocks_AcceptsEscapedAndLongQuotedFence()
    {
        string body = MinimalSkill.Substring(4, MinimalSkill.Length - 8);
        string transported = "\"\\`\\`\\`\n" + body + "\n````\"";

        Assert.That(SkillTextConverter.Parse(transported).Skill.Name, Is.EqualTo("試験"));

        string literal = MinimalSkill.Replace("\n```", "\n――――――――\n説明 \\`code\\`\n```");
        Assert.That(SkillTextConverter.Parse(literal).Skill.Flavor, Is.EqualTo("説明 \\`code\\`"));
    }

    [Test]
    public void ConditionalSpike_WritesBaseFirstAndRejectsCompetingConditions()
    {
        string charge = Regex.Matches(SkillTextConverter.Normalize(ReadFixture("KnightSkills.txt")), @"(?s)```.*?```").Cast<Match>().Single(x => x.Value.Contains("《ランスチャージ》")).Value;
        const string baseLine = "・このアクティブ効果によるスキル値は110に変化する。";
        const string conditionalLine = "・自身が《騎乗》状態の時、このアクティブ効果によるスキル値は160に変化する。";
        string reversed = charge.Replace(baseLine + "\n" + conditionalLine, conditionalLine + "\n" + baseLine);
        Assert.That(SkillTextConverter.Normalize(reversed), Is.EqualTo(charge));

        string competing = charge.Replace(conditionalLine, conditionalLine + "\n・自身が《飛行》状態の時、このアクティブ効果によるスキル値は165に変化する。");
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Parse(competing));
    }

    [Test]
    public void InvalidateAction_RejectsMixedAndOrConnectors()
    {
        string tackle = Regex.Matches(SkillTextConverter.Normalize(ReadFixture("KnightSkills.txt")), @"(?s)```.*?```").Cast<Match>().Single(x => x.Value.Contains("《ショルダータックル》")).Value;
        string ambiguous = tackle.Replace("対象の〈移動〉を無効にする。", "対象の〈移動〉及び〈攻撃〉または〈魔法〉を無効にする。");

        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Parse(ambiguous));
    }

    [Test]
    public void ElementalSageSkills_AllNineNormalizeAndRoundTrip()
    {
        string source = ReadFixture("ElementalSageSkills.txt");
        string normalized = SkillTextConverter.Normalize(source);
        MatchCollection blocks = Regex.Matches(normalized, @"(?s)```.*?```");
        string[] expectedNames =
        {
            "ヴォルカニックエリミネート", "グラスシャードアローズ", "カタストロフィー",
            "ハウリングエコー", "クライオトレント", "スノウリースノウ", "マッドゴーレム",
            "ブロッサムストーム", "スーパーコンダクター・フォトンレイ"
        };

        Assert.That(blocks.Count, Is.EqualTo(expectedNames.Length));
        Assert.That(SkillTextConverter.Normalize(normalized), Is.EqualTo(normalized));
        for (int i = 0; i < blocks.Count; i++)
        {
            SkillTextData parsed = SkillTextConverter.Parse(blocks[i].Value);
            Assert.That(parsed.Skill.Name, Is.EqualTo(expectedNames[i]));
            Assert.That(SkillTextConverter.Build(parsed), Is.EqualTo(blocks[i].Value));
        }
    }

    [Test]
    public void ElementalSageSkills_MapGenericGrammarToSemanticNodes()
    {
        Dictionary<string, SkillTextData> skills = ElementalSageSkills();

        SkillBody volcanic = skills["ヴォルカニックエリミネート"].Skill;
        Assert.That(volcanic.DeclarationConditions.And.Any(x => x.Type == ConditionType.SelectMeleeCharacters), Is.True,
            "消費リソース見出し後に続く宣言条件も失わないこと。");
        Assert.That(volcanic.Roll.Formula, Is.EqualTo("1d100*[知力B]"), "Markdown転送用の\\*を式へ残さないこと。");
        Assert.That(SkillAttack(volcanic).Parameters, Is.EqualTo(new[]
            { "Target", "ElementalWeapon", "100", "60*([火属性B]+[土属性B])", "火", "土" }));
        Assert.That(volcanic.Overrides.SelectMany(x => x.Contents).Single(x => x.Type == OverrideContentType.SetAttackRule).Parameters,
            Is.EqualTo(new[] { "Defense", "Ignore" }));
        Assert.That(volcanic.Overrides.Where(x => x.Type == EffectType.SecondSpike).SelectMany(x => x.Contents).Select(x => x.Type),
            Is.EquivalentTo(new[] { OverrideContentType.SetSkillValue, OverrideContentType.SetAttackComponent }));

        SkillBody glass = skills["グラスシャードアローズ"].Skill;
        Assert.That(glass.Overrides.SelectMany(x => x.Contents).Single(x => x.Type == OverrideContentType.SetAttackRule).Parameters,
            Is.EqualTo(new[] { "Response", "回避", "Prohibit" }));

        SkillBody catastrophe = skills["カタストロフィー"].Skill;
        Assert.That(catastrophe.AcquisitionConditions.And.Where(x => x.Type == ConditionType.MinimumStat)
            .Select(x => string.Join("|", x.Parameters)), Is.EquivalentTo(new[] { "水属性|150", "電属性|150" }));
        Assert.That(SkillAttack(catastrophe).Parameters.Take(2), Is.EqualTo(new[] { "AllWithState:凪", "ElementalWeapon" }));

        SkillBody echo = skills["ハウリングエコー"].Skill;
        Assert.That(echo.DeclarationConditions.And.Single(x => x.Type == ConditionType.ActivationLimit).Parameters,
            Is.EqualTo(new[] { "Turn", "2" }));
        Assert.That(echo.DeclarationConditions.And.Single(x => x.Type == ConditionType.DeclarationTiming).Parameters,
            Is.EqualTo(new[] { "EnemyOrAlly", "End" }));
        Assert.That(echo.Effects.SelectMany(x => x.Contents).Single(x => x.Type == EffectContentType.ReapplyEffects).Parameters,
            Is.EqualTo(new[] { "Turn", "バフ", "All", "1" }));

        SkillBody cryo = skills["クライオトレント"].Skill;
        Assert.That(cryo.Effects.SelectMany(x => x.Contents).Single(x => x.Type == EffectContentType.RollDice).Parameters,
            Is.EqualTo(new[] { "1", "1d2" }));
        Assert.That(cryo.Effects.Where(x => x.Triggers.Any(t => t.Timing == TriggerTiming.RandomResult))
            .SelectMany(x => x.Triggers).SelectMany(x => x.Conditions.And).Where(x => x.Type == ConditionType.RollResult)
            .Select(x => string.Join("|", x.Parameters)), Is.EquivalentTo(new[] { "1|1", "1|2" }));

        SkillTextData snow = skills["スノウリースノウ"];
        Assert.That(snow.Skill.Effects.SelectMany(x => x.Contents).Single(x => x.Type == EffectContentType.ApplyState).Parameters.Take(2),
            Is.EqualTo(new[] { "AllEnemiesExceptSelf", "大降雪" }));
        TriggerDefinition snowfall = snow.States.Single(x => x.Name == "大降雪").Effects.Single().Triggers.Single();
        Assert.That(snowfall.Timing, Is.EqualTo(TriggerTiming.TurnStart));
        Assert.That(snowfall.Conditions.And.Single(x => x.Type == ConditionType.TurnOwner).Parameters, Is.EqualTo(new[] { "Any" }));
        StateDefinition frostbite = snow.States.Single(x => x.Name == "凍傷");
        Assert.That(frostbite.Effects.Single().Triggers.Single().Conditions.And
            .Single(x => x.Type == ConditionType.TurnOwner).Parameters, Is.EqualTo(new[] { "Self" }));
        OverrideDefinition dexterityPenalty = frostbite.Overrides.Single();
        Assert.That(dexterityPenalty.Triggers.Single().Conditions.And.Single(x => x.Type == ConditionType.ActionStat).Parameters,
            Is.EqualTo(new[] { "器用B" }));
        Assert.That(dexterityPenalty.Contents.Single().Parameters, Is.EqualTo(new[] { "0.75" }));

        SkillTextData mud = skills["マッドゴーレム"];
        Assert.That(mud.Skill.Effects.SelectMany(x => x.Contents).Single(x => x.Type == EffectContentType.Summon).Parameters,
            Is.EqualTo(new[] { "泥沼の巨兵" }));
        SummonedEntityDefinition soldier = mud.Summons.Single();
        Assert.That(soldier.Stats.Single(x => x.Name == "HP").Formula, Is.EqualTo("50+50*([水属性B]+[土属性B])"));
        Assert.That(soldier.DeclarationConditions.And.Single(x => x.Type == ConditionType.AutomaticActivation).Parameters,
            Is.EqualTo(new[] { "EveryTurn", "Start", "Random", "Enemy", "1" }));
        StateDefinition mudHand = mud.States.Single(x => x.Name == "マッドハンド");
        Assert.That(mudHand.Overrides.Single().Triggers.Single().Conditions.And.Single(x => x.Type == ConditionType.ActionStat).Parameters,
            Is.EqualTo(new[] { "敏捷B", "器用B" }));
        TriggerDefinition removeMudHand = mudHand.Effects.Single().Triggers.Single();
        Assert.That(removeMudHand.Timing, Is.EqualTo(TriggerTiming.CharacterIncapacitated));
        Assert.That(removeMudHand.Conditions.And.Single(x => x.Type == ConditionType.ActionSource).Parameters,
            Is.EqualTo(new[] { "Applier" }));

        SkillBody blossom = skills["ブロッサムストーム"].Skill;
        Assert.That(SkillAttack(blossom).Parameters.Take(2), Is.EqualTo(new[] { "Target", "ElementalWeapon" }));
        Assert.That(blossom.Effects.SelectMany(x => x.Contents).Single(x => x.Type == EffectContentType.RestoreResource).Parameters.First(),
            Is.EqualTo("AllAllies"));

        SkillBody photon = skills["スーパーコンダクター・フォトンレイ"].Skill;
        OverrideDefinition lowHp = photon.Overrides.Single(x => x.Triggers.SelectMany(t => t.Conditions.And)
            .Any(c => c.Type == ConditionType.ResourceRatio));
        Assert.That(lowHp.Triggers.Single().Timing, Is.EqualTo(TriggerTiming.ElementalPower));
        Assert.That(lowHp.Triggers.Single().Conditions.And.Single(x => x.Type == ConditionType.ResourceRatio).Parameters,
            Is.EqualTo(new[] { "Target", "HP", "AtMost", "0.5" }));
        Assert.That(lowHp.Contents.Single().Type, Is.EqualTo(OverrideContentType.MultiplyAttackComponent));
        Assert.That(lowHp.Contents.Single().Parameters, Is.EqualTo(new[] { "AttributePower", "2" }));
        Assert.That(photon.Overrides.Single(x => x.Type == EffectType.Critical).Contents.Single().Parameters,
            Is.EqualTo(new[] { "AttributePower", "3" }));
    }

    [Test]
    public void MultipleUnconditionalWeaponAttacks_AreSupported()
    {
        string source = MinimalSkill.Replace(
            "【アクティブ効果】対象をスキル値10で武器攻撃する。",
            "【アクティブ効果】\n・対象をスキル値10で武器攻撃する。\n・対象をスキル値20で武器攻撃する。");

        SkillTextData parsed = null;
        Assert.DoesNotThrow(() => parsed = SkillTextConverter.Parse(source));
        Assert.That(parsed.Skill.Effects.SelectMany(x => x.Contents).Count(x => x.Type == EffectContentType.WeaponAttack), Is.EqualTo(2));
    }

    [Test]
    public void RandomResult_AllowsMultipleEffectsForTheSameOutcome()
    {
        SkillTextData cryo = ElementalSageSkills()["クライオトレント"];
        EffectDefinition firstOutcome = cryo.Skill.Effects.Single(x => x.Triggers.Any(t => t.Timing == TriggerTiming.RandomResult &&
            t.Conditions.And.Any(c => c.Type == ConditionType.RollResult && c.Parameters.SequenceEqual(new[] { "1", "1" }))));
        firstOutcome.Contents.Add(new EffectContent
        {
            Type = EffectContentType.ApplyState,
            Parameters = new List<string> { "Target", "追加凍結", "1" }
        });

        string built = SkillTextConverter.Build(cryo);
        SkillTextData reparsed = SkillTextConverter.Parse(built);

        Assert.That(SkillTextConverter.Build(reparsed), Is.EqualTo(built));
        Assert.That(reparsed.Skill.Effects.Count(x => x.Triggers.Any(t => t.Timing == TriggerTiming.RandomResult &&
            t.Conditions.And.Any(c => c.Type == ConditionType.RollResult && c.Parameters.SequenceEqual(new[] { "1", "1" })))), Is.EqualTo(2));
    }

    [Test]
    public void AnonymousRoll_UsesAnAvailableIdAfterExplicitRoll()
    {
        string source = MinimalSkill.Replace(
            "【アクティブ効果】対象をスキル値10で武器攻撃する。",
            "【アクティブ効果】\n・対象をスキル値10で武器攻撃する。\n・追加ロール2として1d2のダイスロールを行う。\n・追加で1d1のダイスロールを行う。\n・- 1：対象へ1ターンの間《試験状態》状態を付与する。");

        SkillTextData parsed = SkillTextConverter.Parse(source);

        Assert.That(parsed.Skill.Effects.SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.RollDice)
            .Select(x => x.Parameters[0]), Is.EqualTo(new[] { "2", "1" }));
    }

    [Test]
    public void ChoicesAndMultipleSummons_RoundTripAsSiblings()
    {
        var data = new SkillTextData();
        data.Skill.Name = "複数召喚試験";
        data.Skill.Categories.Add("召喚");
        data.Skill.Choices.Add(new SkillBody { Name = "選択肢" });
        foreach (string name in new[] { "召喚体A", "召喚体B" })
        {
            data.Skill.Effects.Add(new EffectDefinition
            {
                Type = EffectType.Active,
                Contents = new List<EffectContent>
                {
                    new EffectContent { Type = EffectContentType.Summon, Parameters = new List<string> { name } }
                }
            });
            data.Summons.Add(new SummonedEntityDefinition
            {
                Name = name,
                Categories = new List<string> { "ペット" },
                Stats = new List<FormulaStat> { new FormulaStat { Name = "HP", Formula = "1" } }
            });
        }

        string built = SkillTextConverter.Build(data);
        SkillTextData reparsed = SkillTextConverter.Parse(built);

        Assert.That(reparsed.Summons.Select(x => x.Name), Is.EqualTo(new[] { "召喚体A", "召喚体B" }));
        Assert.That(SkillTextConverter.Build(reparsed), Is.EqualTo(built));
    }

    private static SkillCatalogRecord Record(string id, string name, string category)
    {
        var record = new SkillCatalogRecord { Id = id };
        record.Data.Skill.Name = name;
        record.Data.Skill.Categories.Add(category);
        return record;
    }

    private static Dictionary<string, SkillTextData> KnightSkills()
    {
        string normalized = SkillTextConverter.Normalize(ReadFixture("KnightSkills.txt"));
        return Regex.Matches(normalized, @"(?s)```.*?```").Cast<Match>()
            .Select(x => SkillTextConverter.Parse(x.Value)).ToDictionary(x => x.Skill.Name, StringComparer.Ordinal);
    }

    private static Dictionary<string, SkillTextData> ElementalSageSkills()
    {
        string normalized = SkillTextConverter.Normalize(ReadFixture("ElementalSageSkills.txt"));
        return Regex.Matches(normalized, @"(?s)```.*?```").Cast<Match>()
            .Select(x => SkillTextConverter.Parse(x.Value)).ToDictionary(x => x.Skill.Name, StringComparer.Ordinal);
    }

    private static EffectContent SkillAttack(SkillBody skill)
    {
        return skill.Effects.SelectMany(x => x.Contents).Single(x => x.Type == EffectContentType.SkillAttack);
    }

    private static string ReadFixture(string fileName)
    {
        foreach (string seed in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(seed);
            while (directory != null)
            {
                string projectPath = Path.Combine(directory.FullName, "Assets", "DataBlock", "Editor", "Tests", "Fixtures", fileName);
                if (File.Exists(projectPath)) return File.ReadAllText(projectPath);
                string packagePath = Path.Combine(directory.FullName, "Editor", "Tests", "Fixtures", fileName);
                if (File.Exists(packagePath)) return File.ReadAllText(packagePath);
                directory = directory.Parent;
            }
        }
        throw new FileNotFoundException("テストfixtureが見つかりません。", fileName);
    }
}
#endif
