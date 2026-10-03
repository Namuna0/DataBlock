using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static bool ReadEquipmentAndItemEffects(SkillBody skill, EffectType type, string text)
    {
        string s = RegexReplace(text.Trim(), @"^(?:さらに|更に)[、]?\s*", "");
        if (ReadEquipmentAndItemModifier(skill, type, s)) return true;
        Match m = M(s, @"^環境((?:《[^》]+》)(?:および《[^》]+》)*)による環境効果を受けない。?$");
        if (m.Success)
        { AddSkillEffect(skill, type, Content(EffectContentType.EnvironmentImmunity, new[] { "Self" }.Concat(AllMatches(m.Groups[1].Value, @"《([^》]+)》").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray())); return true; }
        m = M(s, @"^自身の〈([^〉]+)〉によって相手を《([^》]+)》状態にした時、([A-Z]+)を\3最大値の([0-9]+)%回復する。?$");
        if (m.Success)
        { AddSkillEffect(skill, type, Content(EffectContentType.RestoreOnAppliedState, "Self", m.Groups[1].Value, "Target", m.Groups[2].Value, m.Groups[3].Value, "MaximumPercent", m.Groups[4].Value)); return true; }
        if (s.TrimEnd('。') == "対象の行為判定をクリティカル、もしくはファンブルに変える事が出来る")
        { AddSkillEffect(skill, type, Content(EffectContentType.ChooseCheckOutcome, "Target", "Critical", "Fumble", "Optional")); return true; }
        m = M(s, @"^《([^》]+)》×([0-9]+)を獲得。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.GrantItem, "Self", m.Groups[1].Value, m.Groups[2].Value, "Unspecified")); return true; }
        m = M(s, @"^ランクは☆×(.+?)となる。?$");
        if (m.Success)
        {
            var item = skill.Effects.Where(x => x.Type == type).SelectMany(x => x.Contents).LastOrDefault(x => x.Type == EffectContentType.GrantItem);
            Require(item != null && item.Parameters[3] == "Unspecified"); item.Parameters[3] = m.Groups[1].Value; return true;
        }
        m = M(s, @"^(.+?)ロールによる状態を解除する。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.RemoveStatesByOrigin, "Self", "Roll", m.Groups[1].Value)); return true; }
        if (s.TrimEnd('。') == "対象へ選択したスキルをACT消費無しで宣言してもよい")
        { AddSkillEffect(skill, type, Content(EffectContentType.DeclareSelectedSkill, "Self", "Target", "ACT", "0", "Optional", "Unspecified")); return true; }
        m = M(s, @"^そのスキルで与える威力は×([0-9.]+)倍される。?$");
        if (m.Success)
        {
            var declaration = skill.Effects.Where(x => x.Type == type).SelectMany(x => x.Contents).LastOrDefault(x => x.Type == EffectContentType.DeclareSelectedSkill);
            Require(declaration != null && declaration.Parameters[5] == "Unspecified"); declaration.Parameters[5] = m.Groups[1].Value; return true;
        }
        if (s.TrimEnd('。') == "戦闘中、装備している武器と対象武器を入れ替える")
        { AddSkillEffect(skill, type, Content(EffectContentType.SwapWeapon, "Self", "SelectedWeapon", "Battle", "AllowUnarmed")); return true; }
        m = M(s, @"^([0-9]+)ターンの間、対象は《([^》]+)》状態になる。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.ApplyState, "Target", m.Groups[2].Value, m.Groups[1].Value)); return true; }
        m = M(s, @"^対象を(.+?)のスキル値で武器攻撃する。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.WeaponAttack, "Target", m.Groups[1].Value)); return true; }
        if (s != text.Trim()) { ReadSkillLine(skill, type, s); return true; }
        return false;
    }

    private static bool ReadTimedAttackEffects(SkillBody skill, EffectType type, string text)
    {
        string s = RegexReplace(text.Trim(), @"^(?:さらに|更に)[、]?\s*", "");
        if (ReadSemanticRule(skill, null, type, s)) return true;
        var m = M(s, @"^対象は《([^》]+)》スタックを([0-9]+)付与される。（最大([0-9]+)スタックまで）$");
        if (m.Success)
        { Require(type == EffectType.Active); AddSkillEffect(skill, type, Content(EffectContentType.GainStack, "Target", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)); return true; }
        m = M(s, @"^スタックの付与数が([0-9]+)に変化する。?$");
        if (m.Success)
        {
            Require(type == EffectType.Critical);
            var gains = skill.Effects.Where(x => x.Type == EffectType.Active && x.Triggers.Count == 0).SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.GainStack).ToList();
            Require(gains.Count == 1);
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetStackAmount, gains[0].Parameters[0], gains[0].Parameters[1], m.Groups[1].Value)); return true;
        }
        m = M(s, @"^自身は毎ターン《([^》]+)》スタックを([0-9]+)得る。?$");
        if (m.Success)
        { Require(type == EffectType.Passive); AddSkillEffect(skill, type, Content(EffectContentType.GainStack, "Self", m.Groups[1].Value, m.Groups[2].Value), Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Any"))); return true; }
        m = M(s, @"^毎ターン開始時、(.+)$");
        if (m.Success)
        {
            Require(type == EffectType.Passive);
            EffectContent content = ReadBasicEffectContent(m.Groups[1].Value) ?? ReadContent(m.Groups[1].Value);
            Require(content.Type == EffectContentType.GainStack);
            AddSkillEffect(skill, type, content, Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Any"))); return true;
        }
        m = M(s, @"^戦闘開始([0-9]+)ターン目、([A-Z]+)が([+-][0-9]+)される。?$");
        if (m.Success)
        { Require(type == EffectType.Passive); AddSkillEffect(skill, type, Content(EffectContentType.ModifyResource, "Self", m.Groups[2].Value, m.Groups[3].Value), Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnNumber, m.Groups[1].Value))); return true; }
        m = M(s, @"^戦闘開始([0-9]+)ターン目、(自身の[A-Z]+を[+-].+変化させる。)$");
        if (m.Success)
        { Require(type == EffectType.Passive); AddSkillEffect(skill, type, ReadContent(m.Groups[2].Value), Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnNumber, m.Groups[1].Value))); return true; }
        m = M(s, @"^(?:自身の)?防御点(?:は|が)×?([0-9.]+)倍される。?$");
        if (m.Success)
        { Require(type == EffectType.Passive); AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.ModifyStat, "Self", "防御点", "Multiply", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身のあらゆる威力は×([0-9.]+)倍される。?$");
        if (m.Success)
        { Require(type == EffectType.Passive); AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.AttackPower, Condition(ConditionType.ActionSource, "Self"))), Change(OverrideContentType.MultiplyPower, m.Groups[1].Value)); return true; }
        m = M(s, @"^戦闘開始の([0-9]+)ターン目に発動した場合、威力は×([0-9.]+)倍される。?$");
        if (m.Success)
        { Require(type == EffectType.Active); AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.AttackPower, Condition(ConditionType.TurnNumber, m.Groups[1].Value))), Change(OverrideContentType.MultiplyPower, m.Groups[2].Value)); return true; }
        m = M(s, @"^エリアに〈([^〉]+)〉を含む場合、エリアの進行ロールの回数を-([0-9]+)しても良い。?$");
        if (m.Success)
        { Require(type == EffectType.Passive); AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.OptionalAreaProgressReduction, "0", m.Groups[1].Value, m.Groups[2].Value, "Optional")); return true; }
        m = M(s, @"^自身が受ける〈([^〉]+)〉かつ〈([^〉]+)〉による被ダメージは(.+?)減少される。?$");
        if (m.Success)
        {
            Require(type == EffectType.Passive);
            AddSkillEffect(skill, type, Content(EffectContentType.ReduceDamage, "Self", m.Groups[3].Value), Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.ActionCategory, m.Groups[1].Value), Condition(ConditionType.ActionCategory, m.Groups[2].Value))); return true;
        }
        m = M(s, @"^自身が受ける〈([^〉]+)〉かつ〈([^〉]+)〉によるダメージに対して、(自身の被ダメージを.+軽減する。)$");
        if (m.Success)
        {
            Require(type == EffectType.Passive);
            AddSkillEffect(skill, type, ReadContent(m.Groups[3].Value), Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.ActionCategory, m.Groups[1].Value), Condition(ConditionType.ActionCategory, m.Groups[2].Value))); return true;
        }
        m = M(s, @"^この攻撃が命中した時、対象は([0-9]+)ターンの間((?:《[^》]+》)(?:と《[^》]+》)*)状態になる。?$");
        if (m.Success)
        {
            Require(type == EffectType.Active);
            foreach (System.Text.RegularExpressions.Match state in AllMatches(m.Groups[2].Value, @"《([^》]+)》"))
                AddSkillEffect(skill, type, Content(EffectContentType.ApplyState, "Target", state.Groups[1].Value, m.Groups[1].Value), Trigger(TriggerTiming.AttackHit));
            return true;
        }
        return false;
    }

    private static bool ReadBasicSkillStatements(SkillBody skill, EffectType type, string text)
    {
        string s = RegexReplace(text.Trim(), @"^さらに[、]?\s*", "");
        if (type == EffectType.Roleplay)
        {
            AddSkillEffect(skill, type, Content(EffectContentType.RoleplayDescription, Need(s, "ロールプレイ効果"))); return true;
        }
        Match m = M(s, @"^キャラクター作成時に((?:《[^》]+》)(?:及び《[^》]+》)*)習得不能。?$");
        if (m.Success && type == EffectType.Passive)
        {
            AddSkillEffect(skill, type, Content(EffectContentType.ProhibitAcquisition, new[] { "Self", "CharacterCreation" }.Concat(AllMatches(m.Groups[1].Value, @"《([^》]+)》").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray())); return true;
        }
        m = M(s, @"^自身がパーティーを組んでいる時、自身を除く味方の受ける([A-Z]+)ダメージを×(.+?)倍しても良い。?$");
        if (m.Success && type == EffectType.Passive)
        {
            AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.IncomingDamage, Condition(ConditionType.InParty, "Self"))), Change(OverrideContentType.MultiplyResourceDamage, "AllAlliesExceptSelf", m.Groups[1].Value, m.Groups[2].Value, "Optional")); return true;
        }
        m = M(s, @"^自身を〈([^〉]+)〉ではない物として扱うかその都度任意で選択可能。?$");
        if (m.Success && type == EffectType.Passive) { AddSkillEffect(skill, type, Content(EffectContentType.OptionalCategoryExclusion, "Self", m.Groups[1].Value, "PerOccurrence")); return true; }
        m = M(s, @"^自身が食事セット効果を受けていない時、([0-9]+)ターン目に(?:自身は)?《([^》]+)》状態になる。?$");
        if (m.Success && type == EffectType.Passive)
        {
            AddSkillEffect(skill, type, Content(EffectContentType.ApplyState, "Self", m.Groups[2].Value), Trigger(TriggerTiming.TurnStart, Condition(ConditionType.HasMealEffect, "Self", "False"), Condition(ConditionType.TurnNumber, m.Groups[1].Value))); return true;
        }
        m = M(s, @"^自身を除く味方全員の\[([^\]]+)\]による行為判定の達成値が\+(.+?)される。?$");
        if (m.Success && type == EffectType.Passive)
        {
            AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "AllAlliesExceptSelf"), Condition(ConditionType.ActionStat, m.Groups[1].Value))), Change(OverrideContentType.AddActionResult, m.Groups[2].Value)); return true;
        }
        m = M(s, @"^(自身|対象)へ付与する《([^》]+)》スタックの付与が([0-9]+)に変化する。?$");
        if (m.Success && type == EffectType.Critical) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetStackAmount, ActorKey(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value)); return true; }
        m = M(s, @"^このアクティブ効果による威力は(.+?)(?:、?属性威力は(.+?))?に変化する。?$");
        if (m.Success && (type == EffectType.SecondSpike || type == EffectType.ThirdSpike))
        {
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetAttackComponent, "Power", m.Groups[1].Value));
            if (m.Groups[2].Success) AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetAttackComponent, "AttributePower", m.Groups[2].Value)); return true;
        }
        m = M(s, @"^このアクティブ効果による属性威力は(.+?)に変化する。?$");
        if (m.Success && (type == EffectType.SecondSpike || type == EffectType.ThirdSpike)) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetAttackComponent, "AttributePower", m.Groups[1].Value)); return true; }
        // Sentences following a timed state application are separate effects.
        if (s.Contains("。さらに")) { foreach (string part in s.Split(new[] { "。さらに" }, StringSplitOptions.None)) ReadSkillLine(skill, type, part.TrimEnd('。') + "。"); return true; }
        EffectContent content = ReadBasicEffectContent(s);
        if (content != null && IsOrdinary(type))
        {
            if (content.Type == EffectContentType.RemoveSummon)
            {
                Require(type == EffectType.Declaration && skill is SummonedEntityDefinition);
                AddSkillEffect(skill, type, content, Trigger(TriggerTiming.AfterSkillResolution));
            }
            else AddSkillEffect(skill, type, content);
            return true;
        }
        return false;
    }

    private static bool ReadActionResultAndOptionalSpike(SkillBody skill, EffectType type, string text)
    {
        if (type == EffectType.Passive && text.TrimEnd('。') == "無し") return true;
        var m = M(text, @"^自身は(.+?)の威力で攻撃する。?$");
        if (m.Success)
        {
            Require(type == EffectType.Active && skill.DeclarationConditions.And.Any(x => x.Type == ConditionType.SelectCharacters && x.Parameters.SequenceEqual(new[] { "1", "Exact", "Default" })));
            AddSkillEffect(skill, type, Content(EffectContentType.SkillAttack, "Target", m.Groups[1].Value)); return true;
        }
        m = M(text, @"^戦闘中([0-9]+)度だけ、(.+?)及び(.+?)を([0-9]+)消費する事で、自身に《([^》]+)》状態を付与しても良い。?$");
        if (m.Success)
        {
            Require(IsSpikeStage(type));
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.OptionalSpikeState, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value, m.Groups[1].Value)); return true;
        }
        m = M(text, @"^自身が行為判定を行う際、達成値が([+-][0-9]+)される。?$");
        if (m.Success)
        { Require(type == EffectType.Passive); AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"))), Change(OverrideContentType.AddActionResult, m.Groups[1].Value)); return true; }
        m = M(text, @"^((?:〈[^〉]+〉)(?:及び〈[^〉]+〉)*)の達成値が([+-][0-9]+)される。?$");
        if (m.Success)
        {
            Require(type == EffectType.Passive);
            var triggers = AllMatches(m.Groups[1].Value, @"〈([^〉]+)〉").Cast<Match>().Select(x => Trigger(TriggerTiming.ActionResult, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, x.Groups[1].Value))).ToList();
            AddOverride(skill.Overrides, type, triggers, Change(OverrideContentType.AddActionResult, m.Groups[2].Value)); return true;
        }
        return false;
    }

    private static bool ReadCheckAndAcquisitionEffects(SkillBody skill, EffectType type, string body)
    {
        string s = body.Trim();
        bool spike = type == EffectType.SecondSpike || type == EffectType.ThirdSpike;
        if (spike && s.TrimEnd('。') == "無し") return true;
        if (spike && ReadModifierSpike(skill, type, s)) return true;
        if (spike) return false;
        Match m = M(s, @"^能力値ボーナスを\[?([^\[\]]+?B)\]?に置きかえて発動ロールをやり直す。?$");
        if (m.Success && type == EffectType.Counter)
        {
            AddSkillEffect(skill, type, Content(EffectContentType.RerollActivation, "Self", m.Groups[1].Value, "TriggeringRoll")); return true;
        }
        m = M(s, @"^HP([0-9]+)の状態で耐える。?$");
        if (m.Success && type == EffectType.Counter)
        {
            AddSkillEffect(skill, type, Content(EffectContentType.SetResourceValue, "Self", "HP", m.Groups[1].Value)); return true;
        }
        m = M(s, @"^この時、(.+?)ロールは行わなくてもよい。?$");
        if (m.Success && type == EffectType.Counter)
        {
            AddSkillEffect(skill, type, Content(EffectContentType.SkipRoll, "Self", m.Groups[1].Value, "Optional", "ThisResolution")); return true;
        }
        m = M(s, @"^自身に付与されている〈([^〉]+)〉状態を全て解除する。?$");
        if (m.Success && type == EffectType.Active)
        {
            AddSkillEffect(skill, type, Content(EffectContentType.RemoveState, new[] { "Self", "Categories", "All" }.Concat(Split(m.Groups[1].Value, ",")).ToArray())); return true;
        }
        if (type != EffectType.Passive && type != EffectType.Active) return false;

        m = M(s, @"^スキルによって〈([^〉]+)〉で武器攻撃を行う時、発動ロール達成値が×(.+?)倍される。?$");
        if (m.Success) { AddModifierRule(skill, type, m.Groups[2].Value, "WeaponActivation", m.Groups[1].Value); return true; }
        m = M(s, @"^((?:\[[^\]]+\])(?:(?:および|及び)\[[^\]]+\])*)による行為判定を行う際、達成値が×(.+?)倍される。?$");
        if (m.Success)
        {
            AddModifierRule(skill, type, m.Groups[2].Value, new[] { "ActionStats" }.Concat(AllMatches(m.Groups[1].Value, @"\[([^\]]+)\]").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray()); return true;
        }
        m = M(s, @"^環境による行為判定を行う際、達成値が([+-].+?)される。?$");
        if (m.Success) { AddModifierRule(skill, type, m.Groups[1].Value, "EnvironmentCheck"); return true; }
        m = M(s, @"^〈([^〉]+)〉(?:上記の装備|の装備)を制作するとき、目標値が([+-].+?)される。?$");
        if (m.Success) { AddModifierRule(skill, type, m.Groups[2].Value, new[] { "CraftCategories" }.Concat(Split(m.Groups[1].Value, ",")).ToArray()); return true; }
        m = M(s, @"^このターン中、〈([^〉]+)〉の武器威力を参照する〈([^〉]+)〉の発動ロールの達成値が([+-].+?)される。?$");
        if (m.Success) { AddModifierRule(skill, type, m.Groups[3].Value, "TimedWeaponActivation", m.Groups[1].Value, m.Groups[2].Value); return true; }
        m = M(s, @"^スキル習得に必要な消費決意が([+-].+?)される。?$");
        if (m.Success) { AddModifierRule(skill, type, m.Groups[1].Value, "AcquisitionCost", "Add", "AllSkills", "All"); return true; }
        m = M(s, @"^一日の採取回数上限が([+-].+?)追加される。?$");
        if (m.Success) { AddModifierRule(skill, type, m.Groups[1].Value, "DailyGathering"); return true; }
        m = M(s, @"^戦闘開始から([0-9]+)ターンの間、\[([^\]]+)\]による発動ロールの達成値が([+-][0-9]+)(?:される。?|および自身の被ダメージは×(.+?)倍され(?:ます|る)。?)$");
        if (m.Success)
        {
            AddModifierRule(skill, type, m.Groups[3].Value, "EarlyBattleActivation", m.Groups[1].Value, m.Groups[2].Value);
            if (m.Groups[4].Success) AddModifierRule(skill, type, m.Groups[4].Value, "EarlyBattleDamage", m.Groups[1].Value);
            return true;
        }
        m = M(s, @"^戦闘開始から([0-9]+)ターンの間、自身の被ダメージは×(.+?)倍される。?$");
        if (m.Success) { AddModifierRule(skill, type, m.Groups[2].Value, "EarlyBattleDamage", m.Groups[1].Value); return true; }
        m = M(s, @"^([A-Z]+)最大値が×(.+?)倍される。?$");
        if (m.Success) { AddModifierRule(skill, type, m.Groups[2].Value, "ResourceMaximum", m.Groups[1].Value); return true; }
        m = M(s, @"^NPCへの〈([^〉]+)〉を除くアイテムの売値が×(.+?)倍される。?$");
        if (m.Success) { AddModifierRule(skill, type, m.Groups[2].Value, "SalePrice", m.Groups[1].Value); return true; }
        m = M(s, @"^〈([^〉]+)〉による[、,]?\s*威力とリソースの回復量が×(.+?)倍される。?$");
        if (m.Success)
        {
            AddModifierRule(skill, type, m.Groups[2].Value, "CategoryPower", m.Groups[1].Value);
            AddModifierRule(skill, type, m.Groups[2].Value, "CategoryHealing", m.Groups[1].Value); return true;
        }
        m = M(s, @"^〈([^〉]+)〉によるリソースの回復量が×(.+?)倍される。?$");
        if (m.Success) { AddModifierRule(skill, type, m.Groups[2].Value, "CategoryHealing", m.Groups[1].Value); return true; }
        m = M(s, @"^〈([^〉]+)〉による(?:の)?行為判定(?:の)?達成値が([+-].+?)される。?$");
        if (m.Success) { AddModifierRule(skill, type, m.Groups[2].Value, "CategoryCheck", m.Groups[1].Value); return true; }
        m = M(s, @"^クラス《([^》]+)》(?:及び|および)〈([^〉]+)〉の習得決意が×(.+?)倍される。?$");
        if (m.Success)
        {
            AddModifierRule(skill, type, m.Groups[3].Value, "AcquisitionCost", "Multiply", "Class", m.Groups[1].Value);
            AddModifierRule(skill, type, m.Groups[3].Value, "AcquisitionCost", "Multiply", "SkillCategory", m.Groups[2].Value); return true;
        }
        m = M(s, @"^(?:クラス《([^》]+)》|〈([^〉]+)〉)の習得決意が×(.+?)倍される。?$");
        if (m.Success) { AddModifierRule(skill, type, m.Groups[3].Value, "AcquisitionCost", "Multiply", m.Groups[1].Success ? "Class" : "SkillCategory", m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value); return true; }
        if (type != EffectType.Passive) return false;
        m = M(s, @"^(?:〈([^〉]+)〉|([^〈〉]+?))を(一|[0-9]+)つまでしか選択する事が出来ない。?$");
        if (m.Success)
        {
            AddSkillEffect(skill, type, Content(EffectContentType.LimitAcquisition, "Self", "RaceSelection", m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value, m.Groups[3].Value == "一" ? "1" : m.Groups[3].Value)); return true;
        }
        m = M(s, @"^※(?:〈([^〉]+)〉|([^〈〉]+?))は選択不可能。?$");
        if (m.Success)
        {
            AddSkillEffect(skill, type, Content(EffectContentType.LimitAcquisition, "Self", "RaceSelection", m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value, "0")); return true;
        }
        m = M(s, @"^あなたの種族は〈([^〉]+)〉としても扱われる。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.RaceAlias, "Self", m.Groups[1].Value)); return true; }
        return false;
    }

    private static bool ReadStackAndResourceEffects(SkillBody skill, EffectType type, string body)
    {
        string s = RegexReplace(body.Trim(), @"^さらに(?:追加で|、)?\s*", "");
        Match m;
        if (ReadCharacterRule(skill, type, s)) return true;
        if (ReadStackAndCategoryModifier(skill, type, s)) return true;
        // Equivalent spellings are handed to the established grammar.
        m = M(s, @"^([A-Z]+最大値)が([0-9.]+)倍される。?$");
        if (m.Success) { ReadSkillLine(skill, type, m.Groups[1].Value + "が×" + m.Groups[2].Value + "倍される。"); return true; }
        m = M(s, @"^\[([^\]]+)\]による行為判定の達成値が×(.+?)倍される。?$");
        if (m.Success) { ReadSkillLine(skill, type, "[" + m.Groups[1].Value + "]による行為判定を行う際、達成値が×" + m.Groups[2].Value + "倍される。"); return true; }
        m = M(s, @"^一日の採取回数上限が([+-][0-9]+)増加する[、。]?$" );
        if (m.Success) { ReadSkillLine(skill, type, "一日の採取回数上限が" + m.Groups[1].Value + "追加される。"); return true; }
        m = M(s, @"^自身の([A-Z]+最大値)は(-[0-9]+)減少する。?$");
        if (m.Success) { ReadSkillLine(skill, type, m.Groups[1].Value + m.Groups[2].Value); return true; }
        m = M(s, @"^([A-Z]+)を([0-9]+)回復する。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.RestoreResource, "Self", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^〈([^〉]+)〉かつ〈([^〉]+)〉の発動時にHPは(-[0-9]+)される。?$");
        if (m.Success)
        {
            AddSkillEffect(skill, type, Content(EffectContentType.ModifyResource, "Self", "HP", m.Groups[3].Value), Trigger(TriggerTiming.ActionActivated, Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, m.Groups[1].Value), Condition(ConditionType.ActionCategory, m.Groups[2].Value))); return true;
        }
        m = M(s, @"^自身が〈([^〉]+)〉かつ〈([^〉]+)〉を発動した時、自身のHPを(-[0-9]+)変化させる。?$");
        if (m.Success) return ReadStackAndResourceEffects(skill, type, "〈" + m.Groups[1].Value + "〉かつ〈" + m.Groups[2].Value + "〉の発動時にHPは" + m.Groups[3].Value + "される。");
        m = M(s, @"^《([^》]+)》状態を([0-9]+)スタック得る。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.GainStack, "Self", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^スタックが([0-9]+)になった時、HPとMPが全回復して《([^》]+)》状態になる。?$");
        if (m.Success)
        {
            var gain = skill.Effects.SelectMany(e => e.Contents).LastOrDefault(c => c.Type == EffectContentType.GainStack);
            Require(gain != null && gain.Parameters[0] == "Self");
            AddSkillEffect(skill, type, Content(EffectContentType.TransformAtStacks, "Self", gain.Parameters[1], "Equals", m.Groups[1].Value, "HP", "MP", m.Groups[2].Value)); return true;
        }
        m = M(s, @"^〈([^〉]+)〉を発動するたびに《([^》]+)》〈([^〉]+)〉状態を([0-9]+)スタック得る。?$");
        if (m.Success)
        {
            AddSkillEffect(skill, type, Content(EffectContentType.CyclingStateStacks, "Self", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, "Unspecified", "ACT", "Unspecified", "0")); return true;
        }
        m = M(s, @"^([0-9]+)スタック得た時、([A-Z]+)は\+?([0-9]+)回復して、スタックは([0-9]+)になる。?$");
        if (m.Success)
        {
            var cycle = skill.Effects.SelectMany(e => e.Contents).LastOrDefault(c => c.Type == EffectContentType.CyclingStateStacks);
            Require(cycle != null); cycle.Parameters[5] = m.Groups[1].Value; cycle.Parameters[6] = m.Groups[2].Value; cycle.Parameters[7] = m.Groups[3].Value; cycle.Parameters[8] = m.Groups[4].Value; return true;
        }
        m = M(s, @"^《([^》]+)》による(?:の)?採取の結果を一日([0-9]+)回までやり直すことが出来る。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.RerollGathering, m.Groups[1].Value, "Day", m.Groups[2].Value)); return true; }
        if (s.TrimEnd('。') == "装備部位が両手である武器を片手で装備する事が出来る") { AddSkillEffect(skill, type, Content(EffectContentType.EquipSlotSubstitution, "Weapon", "BothHands", "OneHand")); return true; }
        m = M(s, @"^対象に《([^》]+)》状態をスタック([0-9]+)与える。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.GainStack, "Target", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^対象の現在の《([^》]+)》状態は解除され[、。]?$" );
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.RemoveState, "Target", m.Groups[1].Value)); return true; }
        m = M(s, @"^対象の《([^》]+)》状態を解除する。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.RemoveState, "Target", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身と対象は新しい接近グループの《([^》]+)》状態になる。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.CreateMeleeGroup, "Self", "Target", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身を除く、自身と同じ接近グループのキャラクター全てに([0-9]+)ターンの間《([^》]+)》状態を付与する。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.ApplyState, "SameMeleeExceptSelf", m.Groups[2].Value, m.Groups[1].Value)); return true; }
        m = M(s, @"^自身を対象にした達成値([0-9]+)以下の〈([^〉]+)〉を無効にしても良い。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.OptionalInvalidateAction, "Self", m.Groups[2].Value, "AtMost", m.Groups[1].Value)); return true; }
        m = M(s, @"^すべての敵の([A-Z]+)を-(.+?)減少させる。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.DrainResource, "AllEnemies", m.Groups[1].Value, m.Groups[2].Value, "Self", "Unspecified", "Unspecified")); return true; }
        if (s == "その合計値のMPを回復する。" || s == "この回復効果はMPの最大値を超過する事が出来る。")
        {
            var drain = skill.Effects.SelectMany(e => e.Contents).LastOrDefault(c => c.Type == EffectContentType.DrainResource);
            Require(drain != null && drain.Parameters[1] == "MP");
            if (s.StartsWith("その", StringComparison.Ordinal)) drain.Parameters[4] = "ActualTotal"; else drain.Parameters[5] = "AllowOverflow";
            return true;
        }
        if (s.TrimEnd('。') == "食事及び食事セット効果による, リソース減少及びステータス減少を受けない") { AddSkillEffect(skill, type, Content(EffectContentType.PreventMealPenalties, "Self", "MealAndMealSet", "ResourceAndStatDecrease")); return true; }
        m = M(s, @"^キャラクター作成時、自身は次のライフパスを一つ選択して追加で習得する。((?:《[^》]+》)(?:,\s*《[^》]+》)*)$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.GrantCreationChoice, new[] { "LifePath", "1" }.Concat(AllMatches(m.Groups[1].Value, @"《([^》]+)》").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray())); return true; }
        m = M(s, @"^〈([^〉]+)〉の種族特性を一つ選んで追加で習得する。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.GrantRaceTrait, m.Groups[1].Value, "1")); return true; }
        if (s.TrimEnd('。') == "対象のカウンター効果を無効にする") { AddSkillEffect(skill, type, Content(EffectContentType.InvalidateTriggeredEffect, "Target", "Counter")); return true; }
        if (s.TrimEnd('。') == "発動ロールをやり直す事が出来る") { AddSkillEffect(skill, type, Content(EffectContentType.RerollActivation, "Self", "Original", "TriggeringRoll")); return true; }
        if (s != body.Trim()) { ReadSkillLine(skill, type, s); return true; }
        return false;
    }

    private static bool ReadRaceAndRecoveryEffects(SkillBody skill, EffectType type, string text)
    {
        string s = RegexReplace(text.Trim(), @"^さらに[、]?\s*", "");
        var m = M(s, @"^戦闘中の敵と味方全てのキャラクター(?:に対して|を)(.+?)の威力で攻撃する。?$");
        if (m.Success)
        { Require(type == EffectType.Counter); AddSkillEffect(skill, type, Content(EffectContentType.SkillAttack, "AllBattleCharacters", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身のターン開始時、(?:自身の([A-Z]+)は(.+?)回復する|自身は([A-Z]+)を(.+?)回復する)。?$");
        if (m.Success)
        {
            Require(type == EffectType.Passive);
            AddSkillEffect(skill, type, Content(EffectContentType.RestoreResource, "Self", m.Groups[1].Success ? m.Groups[1].Value : m.Groups[3].Value, m.Groups[2].Success ? m.Groups[2].Value : m.Groups[4].Value), Trigger(TriggerTiming.TurnStart, Condition(ConditionType.TurnOwner, "Self"))); return true;
        }
        m = M(s, @"^(?:この)?攻撃が命中した時、対象が((?:《[^》]+》)(?:,\s*《[^》]+》)*)のいずれかなら、《([^》]+)》状態を([0-9]+)ターン付与する。?$");
        if (m.Success)
        { Require(type == EffectType.Active); AddSkillEffect(skill, type, Content(EffectContentType.HitRaceState, RaceNames(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value)); return true; }
        m = M(s, @"^既に対象が《([^》]+)》状態の時、威力は×([0-9.]+)倍される。?$");
        if (m.Success)
        { Require(type == EffectType.Active); AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.PowerAgainstState, m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^(?:この)?攻撃が命中した時、対象に《([^》]+)》状態を([0-9]+)ターン付与する。?$");
        if (m.Success)
        { Require(type == EffectType.Active); AddSkillEffect(skill, type, Content(EffectContentType.ApplyState, "Target", m.Groups[1].Value, m.Groups[2].Value), Trigger(TriggerTiming.AttackHit)); return true; }
        m = M(s, @"^(?:対象に|この攻撃で)与えたダメージの×([0-9.]+)倍の([A-Z]+)(?:と([A-Z]+))?を回復する。?$");
        if (m.Success)
        {
            Require(type == EffectType.Active);
            foreach (string resource in new[] { m.Groups[2].Value, m.Groups[3].Value }.Where(x => x.Length > 0))
                AddSkillEffect(skill, type, Content(EffectContentType.RestoreFromDamage, "Self", resource, m.Groups[1].Value), ThisAttackDamageTriggers().ToArray());
            return true;
        }
        m = M(s, @"^自身が与える([A-Z]+)ダメージは×([0-9.]+)倍される。?$");
        if (m.Success)
        { Require(type == EffectType.Passive); AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.DamageDealt, Condition(ConditionType.ActionSource, "Self"))), Change(OverrideContentType.MultiplyResourceDamage, "Self", m.Groups[1].Value, m.Groups[2].Value, "Automatic")); return true; }
        return false;
    }

    private static bool ReadSelectedSkillAndResourceEffects(SkillBody skill, EffectType type, string s)
    {
        if (ReadStateReferenceModifier(skill, type, s)) return true;
        Match m;
        m = M(s, @"^自身は([A-Z]+が[0-9]+以下になった時、.*)$");
        if (m.Success && s.Contains("状態になる")) { ReadSkillLine(skill, type, m.Groups[1].Value); return true; }
        if (s.TrimEnd('。') == "自身は戦闘を離脱する")
        { AddSkillEffect(skill, type, Content(EffectContentType.LeaveBattle, "Self")); return true; }
        m = M(s, @"^自身は([A-Z]+)が([0-9]+)以下になった時、(.+?)ロールを行う必要がない。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.PreventRollAtResource, "Self", m.Groups[1].Value, "AtMost", m.Groups[2].Value, m.Groups[3].Value)); return true; }
        m = M(s, @"^自身はあらゆる〈([^〉]+)〉からの効果を受けない。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.CategoryImmunity, "Self", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身は((?:〈[^〉]+〉)(?:及び〈[^〉]+〉)*)による効果を受ける事が出来ない。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.CategoryImmunity, new[] { "Self" }.Concat(AllMatches(m.Groups[1].Value, @"〈([^〉]+)〉").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray())); return true; }
        m = M(s, @"^自身は〈([^〉]+)〉を対象にすることはできない。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.ProhibitCategoryTarget, "Self", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身の([A-Z]+)の消費は全て([A-Z]+)の消費に置き換えられる。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.ConvertResourceCost, "Self", m.Groups[1].Value, m.Groups[2].Value, "All")); return true; }
        m = M(s, @"^クエストによって獲得する(.+?)が\+([0-9]+)される。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.AddQuestReward, "Self", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^対象スキルを([0-9]+)ターンの間《([^》]+)》状態にする。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.ApplySelectedSkillState, "SelectedSkill", m.Groups[2].Value, m.Groups[1].Value)); return true; }
        m = M(s, @"^対象に(.+?)の([A-Z]+)ダメージを与える。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.ResourceDamage, "Target", m.Groups[2].Value, m.Groups[1].Value.Replace("×", "*"))); return true; }
        m = M(s, @"^与えた([A-Z]+)ダメージの値の([A-Z]+)と、([0-9.]+)倍の値の([A-Z]+)を回復する。?$");
        if (m.Success)
        {
            AddSkillEffect(skill, type, Content(EffectContentType.RestoreFromResourceDamage, "Self", m.Groups[1].Value, m.Groups[2].Value, "1", "ThisResolution"));
            AddSkillEffect(skill, type, Content(EffectContentType.RestoreFromResourceDamage, "Self", m.Groups[1].Value, m.Groups[4].Value, m.Groups[3].Value, "ThisResolution")); return true;
        }
        m = M(s, @"^与えた([A-Z]+)ダメージの([0-9.]+)倍の値の([A-Z]+)を回復する。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.RestoreFromResourceDamage, "Self", m.Groups[1].Value, m.Groups[3].Value, m.Groups[2].Value, "ThisResolution")); return true; }
        m = M(s, @"^《([^》]+)》状態のキャラクター全てに自身のターン開始時《([^》]+)》スタックを([0-9]+)付与する。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.TurnStartStateStacks, "Self", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, "AllCharacters")); return true; }
        m = M(s, @"^対象に《([^》]+)》スタックを([0-9]+)付与する。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.GainStack, "Target", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^対象の([A-Z]+)を(.+?)の効果量で回復する。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.RestoreResource, "Target", m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(s, @"^自身の《([^》]+)》状態を解除する。?$");
        if (m.Success) { AddSkillEffect(skill, type, Content(EffectContentType.RemoveState, "Self", m.Groups[1].Value)); return true; }
        m = M(s, @"^自身の([A-Z]+最大値)及び([A-Z]+最大値)は×([0-9.]+)倍される。?$");
        if (m.Success)
        { ReadSkillLine(skill, type, m.Groups[1].Value + "が×" + m.Groups[3].Value + "倍される。"); ReadSkillLine(skill, type, m.Groups[2].Value + "が×" + m.Groups[3].Value + "倍される。"); return true; }
        return false;
    }


    private static bool ReadSkillStatement(SkillBody skill, EffectType type, string text)
    {
        return ReadActionResultAndOptionalSpike(skill, type, text) ||
               ReadRaceAndRecoveryEffects(skill, type, text) ||
               ReadTimedAttackEffects(skill, type, text) ||
               ReadEquipmentAndItemEffects(skill, type, text) ||
               ReadSelectedSkillAndResourceEffects(skill, type, text) ||
               ReadStackAndResourceEffects(skill, type, text) ||
               ReadCheckAndAcquisitionEffects(skill, type, text) ||
               ReadBasicSkillStatements(skill, type, text);
    }
}
