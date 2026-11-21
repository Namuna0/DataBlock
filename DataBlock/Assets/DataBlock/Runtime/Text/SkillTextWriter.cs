using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public static partial class SkillTextConverter
{
    private static string CostText(ResourceCost cost)
    {
        Require(cost != null && cost.Amount >= 0);
        string n = Need(cost.Resource, "消費名");
        Require(!M(n, @"[\r\n〈〉《》\[\]、]").Success && (!n.Contains(",") || (cost.Kind == CostKind.ItemName && n.Split(',').All(v => !string.IsNullOrWhiteSpace(v)))));
        int a = cost.Amount;
        Require(cost.Quantity == CostQuantity.Formula || string.IsNullOrEmpty(cost.Formula));
        Require(cost.Quantity == CostQuantity.DistinctNameRange || cost.Maximum == 0);
        if (cost.Quantity == CostQuantity.DistinctNameRange)
        {
            Require(cost.Kind == CostKind.ItemCategory && a > 0 && cost.Maximum >= a);
            return "重複しない名前の〈" + n + "〉×" + a + "～" + cost.Maximum + "個";
        }
        if (cost.Quantity == CostQuantity.Formula)
        {
            Require(cost.Kind == CostKind.Resource && a == 0 && M(cost.Formula ?? "", @"^\([0-9]+\+\[[^\[\]。]+\]\*[0-9]+\)$").Success);
            return n + "-" + cost.Formula;
        }
        if (cost.Quantity == CostQuantity.LeaveRemaining) { Require(cost.Kind == CostKind.Resource); return n + "を" + a + "残して全消費"; }
        if (cost.Quantity == CostQuantity.All || cost.Quantity == CostQuantity.Chosen)
        {
            Require(a == 0);
            if (cost.Kind == CostKind.Resource) return (cost.Quantity == CostQuantity.All ? "全ての" : "任意の") + n;
            if (cost.Quantity == CostQuantity.Chosen) { Require(cost.Kind == CostKind.NamedStack); return "任意の数の《" + n + "》"; }
            Require(cost.Kind == CostKind.NamedStack || cost.Kind == CostKind.CategoryStack);
            return (cost.Kind == CostKind.NamedStack ? "《" + n + "》" : "〈" + n + "〉") + "全スタック消費";
        }
        Require(cost.Quantity == CostQuantity.Fixed && (cost.Kind == CostKind.Resource || a > 0));
        switch (cost.Kind)
        {
            case CostKind.Resource: return n + "-" + a;
            case CostKind.ItemName: return "《" + n + "》×" + a;
            case CostKind.ItemCategory: return "〈" + n + "〉×" + a;
            case CostKind.NamedStack: return "《" + n + "》スタック-" + a;
            case CostKind.CategoryStack: return "〈" + n + "〉スタック-" + a;
            case CostKind.SelectedItem: Require(n == "選択された所持品"); return "[" + n + "]-" + a;
            case CostKind.SelectedCategoryItem: return "選択する〈" + n + "〉-" + a;
            case CostKind.Currency: Require(n == "ジルダ"); return a + n;
            default: throw new InvalidOperationException("未対応の消費種別です。");
        }
    }

    private static void WriteConditions(StringBuilder sb, string label, ConditionSet set, bool declaration)
    {
        if (!ValidConditions(set)) throw new InvalidOperationException("条件集合が不正です。");
        if (set.And.Any(x => x.Type == ConditionType.IncapacitatedCheckReaction || x.Type == ConditionType.SelectCarriedWeapon)) Require(set.And.Count == 1 && set.Or.Count == 0);
        ConditionEntry meleeSelection;
        string exceptionState;
        if (declaration && TryMeleeStateException(set, out meleeSelection, out exceptionState))
        {
            var exceptionTexts = set.And.Select(x => x == meleeSelection ? "自身と同じ接近グループの" + ConditionText(x) : ConditionText(x)).ToList();
            string exceptionResult = string.Join(", ", exceptionTexts);
            const string distinct = "（重複不可）";
            exceptionResult = exceptionResult.EndsWith(distinct) ? exceptionResult.Substring(0, exceptionResult.Length - distinct.Length) + "して宣言可能。" + distinct : exceptionResult + "して宣言可能。";
            sb.AppendLine("【" + label + "】" + exceptionResult);
            sb.AppendLine("自身が《" + exceptionState + "》状態なら、接近は不要。");
            return;
        }
        var texts = set.And.Select(ConditionText).ToList();
        if (set.Or.Count > 0) texts.Add("（" + string.Join(" または ", set.Or.Select(ConditionText)) + "）");
        if (texts.Count == 0) return;
        string result = string.Join(", ", texts);
        if (declaration)
        {
            ConditionType last = set.And.Count == 0 ? ConditionType.None : set.And.Last().Type;
            if (last == ConditionType.SelectCarriedWeapon || last == ConditionType.AutomaticActivation || last == ConditionType.AutomaticEnemyAction || (last == ConditionType.ReactionTarget && set.And.Last().Parameters.Count == 4))
            {
                if (set.Or.Count != 0 || set.And.Count != 1) throw new InvalidOperationException("自動発動条件は他の宣言条件と併用できません。");
            }
            else if (set.Or.Count == 0 && (last == ConditionType.SelectCharactersExceptSelf || last == ConditionType.SelectRaceWithTrait || last == ConditionType.SelectCharactersWithState || last == ConditionType.SelectCharacterSkill || last == ConditionType.ExcludeTargetRaces || last == ConditionType.TargetStackMinimum || last == ConditionType.SelectCharacters || last == ConditionType.SelectMeleeCharacters || last == ConditionType.SelectEquippedWeapon || last == ConditionType.SelectEquippedWeaponFromCategories || last == ConditionType.SelectConsumedItem || last == ConditionType.SelectOwnState))
            {
                result = M(result, @"（重複(?:不可|可能)）$").Success
                    ? RegexReplace(result, @"（(重複(?:不可|可能))）$", "して宣言可能。（$1）") : result + "して宣言可能。";
            }
            else if (set.Or.Count > 0 || last != ConditionType.SelectedItemConditionsMet) result += "宣言可能。";
        }
        sb.AppendLine("【" + label + "】" + result);
    }
    private static void WriteEffectGroup(StringBuilder sb, SkillBody skill, EffectType type)
    {
        var texts = new List<string>();
        var effects = skill.Effects.Where(x => x.Type == type).ToList();
        var deferred = type == EffectType.Passive ? effects.Where(x => IsMappedStackRestore(x, null)).ToList() : new List<EffectDefinition>();
        if (deferred.Count > 1)
        {
            var ruleOrder = skill.Overrides.Where(x => x.Type == EffectType.Passive).SelectMany(x => x.Contents)
                .Where(x => x.Type == OverrideContentType.ReduceResourceCostPerMappedStacks)
                .Select((x, i) => new { Rule = Args(x.Parameters, 6, "ReduceResourceCostPerMappedStacks")[2], Index = i })
                .ToDictionary(x => x.Rule, x => x.Index, StringComparer.Ordinal);
            deferred = deferred.OrderBy(x => ruleOrder[Args(
                x.Triggers[0].Conditions.And[0].Parameters, 4, "MappedStateStackInterval")[1]]).ToList();
        }
        foreach (var effect in effects.Except(deferred))
            texts.AddRange(effect.Contents.Select(c => AppendPassiveEffectNotes(effect, SkillTriggerText(effect.Type, effect.Triggers) + SkillContentText(c))));
        IEnumerable<OverrideDefinition> overrides = skill.Overrides.Where(x => x.Type == type);
        if (type == EffectType.SecondSpike || type == EffectType.ThirdSpike) overrides = overrides.OrderBy(x => x.Triggers.Count == 0 ? 0 : 1);
        foreach (var effect in overrides) texts.AddRange(effect.Contents.Select(c => OverrideText(skill, effect, c)));
        foreach (var effect in deferred)
            texts.AddRange(effect.Contents.Select(c => AppendPassiveEffectNotes(effect, SkillTriggerText(effect.Type, effect.Triggers) + SkillContentText(c))));
        if (texts.Count == 0)
        {
            if (type == EffectType.Passive && skill.Overrides.SelectMany(x => x.Contents).Any(x => x.Type == OverrideContentType.OptionalSpikeState)) sb.AppendLine("【パッシブ効果】無し。");
            return;
        }
        string effectName;
        if (!EffectNames.TryGetValue(type, out effectName)) throw new InvalidOperationException("未対応の効果種別です：" + type);
        string header = "【" + effectName + "】";
        if (texts.Count == 1) sb.AppendLine(header + texts[0]);
        else { sb.AppendLine(header); foreach (string text in texts) sb.AppendLine(Bullet + text); }
    }
    private static void WriteSkill(StringBuilder sb, SkillBody skill, List<FormulaStat> stats = null)
    {
        string categories = CategoryText(skill.Categories);
        if (categories.Length > 0) sb.AppendLine(categories);
        WriteConditions(sb, "習得条件", skill.AcquisitionConditions, false);
        if (stats != null)
        {
            if (stats.Any(x => x == null) || stats.GroupBy(x => x.Name, StringComparer.Ordinal).Any(x => x.Count() > 1)) throw new InvalidOperationException("召喚ステータスがnullまたは重複しています。");
            foreach (FormulaStat stat in stats) sb.AppendLine("【" + Need(stat.Name, "召喚ステータス名") + "最大値】" + Need(stat.Formula, "召喚ステータス式"));
        }
        WriteConditions(sb, "装備条件", skill.EquipmentConditions, false);
        WriteConditions(sb, "宣言条件", skill.DeclarationConditions, true);
        if (skill.Costs == null || skill.Roll == null) throw new InvalidOperationException("消費リソースまたは発動ロールがnullです。");
        if (skill.Costs.Count > 0)
        {
            sb.AppendLine("【消費リソース】" + string.Join(", ", skill.Costs.Select(CostText)));
        }
        if (skill.CooldownTurns < 0 || skill.Roll.Count < 0) throw new InvalidOperationException("クールタイムと回数は0以上です。");
        if (skill.CooldownTurns > 0) sb.AppendLine("【クールタイム】" + skill.CooldownTurns + "ターン");
        bool automaticRoll = skill.Roll.Count == 1 && skill.Roll.Formula == "自動成功" && string.IsNullOrWhiteSpace(skill.Roll.Target);
        var sources = skill.Roll.SourceItemCategories;
        Require(sources != null && sources.Count <= 2);
        bool itemRoll = sources.Count > 0;
        if (itemRoll)
        {
            var selector = sources.Count == 1 ? ConditionType.SelectOwnedCategory : ConditionType.SelectOwnedEitherCategory;
            Require(skill.Roll.Count == 1 && string.IsNullOrEmpty(skill.Roll.Formula) && string.IsNullOrEmpty(skill.Roll.Target) &&
                skill.DeclarationConditions.And.Any(c => c.Type == selector && c.Parameters.Take(sources.Count).SequenceEqual(sources)));
        }
        Require(skill.Roll.RepresentativeCount >= 0 && (skill.Roll.RepresentativeCount == 0 || (!automaticRoll && !itemRoll && skill.Roll.Count > 0)));
        Require(!skill.Roll.UseSummonerResult || (automaticRoll && skill is SummonedEntityDefinition && skill.Roll.FixedResult < 0));
        if (skill.Roll.FixedResult < -1 || (!automaticRoll && skill.Roll.FixedResult != -1)) throw new InvalidOperationException("固定達成値は自動成功の発動ロールに指定してください。");
        if (skill.Roll.Count == 0 && (!string.IsNullOrWhiteSpace(skill.Roll.Formula) || !string.IsNullOrWhiteSpace(skill.Roll.Target))) throw new InvalidOperationException("発動ロールが未使用なのに式・目標値が入力されています。回数を設定してください。");
        if (skill.Roll.Count > 0 && !automaticRoll && !itemRoll)
        {
            string formula = Need(skill.Roll.Formula, "式");
            Need(skill.Roll.Target, "目標値");
            if (M(formula, @"\s+目標値").Success) throw new InvalidOperationException("発動ロール式に空白＋「目標値」は使用できません。");
        }
        ValidateSkillEffects(skill);
        WriteEffectGroup(sb, skill, EffectType.Declaration);
        if (itemRoll) sb.AppendLine("【発動ロール】" + string.Join("または", sources.Select(c => "〈" + c + "〉")) + "に記載");
        else if (automaticRoll) sb.AppendLine("【発動ロール】自動成功" + (skill.Roll.UseSummonerResult ? "、達成値は召喚者と同値として扱われる。" : skill.Roll.FixedResult >= 0 ? "、達成値は" + skill.Roll.FixedResult + "となる。" : ""));
        else if (skill.Roll.Count > 0) sb.AppendLine("【発動ロール】" + (skill.Roll.RepresentativeCount > 0 ? "代表" + skill.Roll.RepresentativeCount + "名が" : "") + (skill.Roll.Count > 1 ? skill.Roll.Count + "回：" : "") + Need(skill.Roll.Formula, "式") + " 目標値" + Need(skill.Roll.Target, "目標値"));
        foreach (EffectType type in DisplayOrder.Where(x => IsOrdinary(x) && x != EffectType.Declaration)) WriteEffectGroup(sb, skill, type);
        if (skill.Overrides.Any(x => x.Type == EffectType.SecondSpike || x.Type == EffectType.ThirdSpike))
        {
            sb.AppendLine();
            if (!skill.Overrides.Any(x => x.Type == EffectType.SecondSpike))
                sb.AppendLine("【セカンドスパイク】無し");
            else
                WriteEffectGroup(sb, skill, EffectType.SecondSpike);
            WriteEffectGroup(sb, skill, EffectType.ThirdSpike);
        }
    }
    public static string Normalize(string text) { return string.Join("\n\n", InputBlocks(text).Select(x => Build(ParseOne(x)))); }
    public static string Build(SkillTextData data)
    {
        if (data == null || data.Skill == null || data.Summons == null || data.States == null || data.Skill.Choices == null || data.Skill.Choices.Any(x => x == null) || data.Summons.Any(x => x == null) || data.States.Any(x => x == null)) throw new InvalidOperationException("スキル・子スキル・召喚・状態がnullです。");
        if (data.Skill.Choices.GroupBy(x => x.Name).Any(x => x.Count() > 1) || data.Summons.GroupBy(x => x.Name).Any(x => x.Count() > 1) || data.States.GroupBy(x => x.Name).Any(x => x.Count() > 1)) throw new InvalidOperationException("子スキル・召喚・状態の名前が重複しています。");
        ValidateSummons(data);
        ValidateStackStateReferences(data);
        ValidateStateSkillReferences(data);
        ValidateAppliedStateModifiers(data);
        ValidateOptionalSpikeStateReferences(data);
        var sb = new StringBuilder();
        sb.AppendLine("```"); sb.AppendLine("《" + Need(data.Skill.Name, "スキル名") + "》"); WriteSkill(sb, data.Skill);
        if (data.Skill.Choices.Count > 0)
        {
            sb.AppendLine("【説明】" + Description);
            foreach (SkillBody child in data.Skill.Choices) { sb.AppendLine(); sb.AppendLine("●" + Need(child.Name, "子スキル名")); WriteSkill(sb, child); }
        }
        foreach (SummonedEntityDefinition summon in data.Summons)
        {
            sb.AppendLine(); sb.AppendLine("《" + Need(summon.Name, "召喚名") + "》"); WriteSkill(sb, summon, summon.Stats);
        }
        foreach (StateDefinition state in data.States)
        {
            string categories = CategoryText(state.Categories);
            List<string> texts = StateTexts(state);
            if (texts.Count == 0) throw new InvalidOperationException("状態の効果が空です：" + state.Name);
            sb.AppendLine(); sb.AppendLine(Need(state.Name, "状態名") + "効果" + categories + "：");
            foreach (string text in texts) sb.AppendLine((texts.Count > 1 ? Bullet : "") + text);
        }
        if (!string.IsNullOrEmpty(data.Skill.Flavor)) { sb.AppendLine(Separator); sb.AppendLine(data.Skill.Flavor.Replace("\r\n", "\n").Replace("\r", "\n")); }
        sb.Append("```"); return sb.ToString().Replace("\r\n", "\n");
    }

    private static void ValidateSummons(SkillTextData data)
    {
        var names = new HashSet<string>(data.Summons.Select(x => Need(x.Name, "召喚名")), StringComparer.Ordinal);
        foreach (SummonedEntityDefinition summon in data.Summons)
        {
            if (summon.Stats == null) throw new InvalidOperationException("召喚ステータスがnullです：" + summon.Name);
            if (summon.Categories == null) throw new InvalidOperationException("召喚カテゴリーがnullです：" + summon.Name);
            if (summon.Categories.Contains("ペット") == summon.Categories.Contains("罠")) throw new InvalidOperationException("召喚定義には〈ペット〉または〈罠〉の一方を指定してください：" + summon.Name);
            if (summon.Categories.Contains("ペット") && summon.Stats.Count(x => x != null && x.Name == "HP") != 1) throw new InvalidOperationException("ペットにはHP最大値を1件指定してください：" + summon.Name);
            if (summon.Effects != null && summon.Effects.Where(x => x != null && x.Contents != null).SelectMany(x => x.Contents).Any(x => x != null && (x.Type == EffectContentType.Summon || x.Type == EffectContentType.PlaceTrap)))
                throw new InvalidOperationException("召喚定義から別の召喚は発動できません：" + summon.Name);
        }
        var bodies = new List<SkillBody> { data.Skill };
        bodies.AddRange(data.Skill.Choices);
        foreach (var reference in bodies.SelectMany(x => x.Effects ?? new List<EffectDefinition>())
            .Where(x => x != null && x.Contents != null).SelectMany(x => x.Contents)
            .Where(x => x != null && (x.Type == EffectContentType.Summon || x.Type == EffectContentType.PlaceTrap)))
        {
            string name = SummonName(reference);
            var definition = data.Summons.SingleOrDefault(x => x.Name == name);
            string category = reference.Type == EffectContentType.PlaceTrap ? "罠" : "ペット";
            if (definition == null || !definition.Categories.Contains(category))
                throw new InvalidOperationException("参照先の" + category + "定義がありません：" + name);
        }
        var references = bodies.SelectMany(x => x.Effects ?? new List<EffectDefinition>())
            .Where(x => x != null && x.Contents != null).SelectMany(x => x.Contents)
            .Where(x => x != null && (x.Type == EffectContentType.Summon || x.Type == EffectContentType.PlaceTrap))
            .Select(SummonName).ToList();
        foreach (string reference in references)
            if (!names.Contains(reference)) throw new InvalidOperationException("召喚定義がありません：" + reference);
        foreach (string name in names)
            if (!references.Contains(name)) throw new InvalidOperationException("参照されていない召喚定義です：" + name);
    }
}
