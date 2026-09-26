using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public static partial class SkillTextConverter
{
    private static void WriteConditions(StringBuilder sb, string label, ConditionSet set, bool declaration)
    {
        if (!ValidConditions(set)) throw new InvalidOperationException("条件集合が不正です。");
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
            if (last == ConditionType.AutomaticActivation)
            {
                if (set.Or.Count != 0 || set.And.Count != 1) throw new InvalidOperationException("自動発動条件は他の宣言条件と併用できません。");
            }
            else if (set.Or.Count == 0 && (last == ConditionType.SelectCharacters || last == ConditionType.SelectMeleeCharacters || last == ConditionType.SelectEquippedWeapon || last == ConditionType.SelectEquippedWeaponFromCategories || last == ConditionType.SelectConsumedItem || last == ConditionType.SelectOwnState))
            {
                const string distinct = "（重複不可）";
                result = result.EndsWith(distinct) ? result.Substring(0, result.Length - distinct.Length) + "して宣言可能。" + distinct : result + "して宣言可能。";
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
        foreach (var effect in overrides) texts.AddRange(effect.Contents.Select(c => OverrideText(effect, c)));
        foreach (var effect in deferred)
            texts.AddRange(effect.Contents.Select(c => AppendPassiveEffectNotes(effect, SkillTriggerText(effect.Type, effect.Triggers) + SkillContentText(c))));
        if (texts.Count == 0) return;
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
        WriteConditions(sb, "宣言条件", skill.DeclarationConditions, true);
        if (skill.Costs == null || skill.Roll == null) throw new InvalidOperationException("消費リソースまたは発動ロールがnullです。");
        if (skill.Costs.Count > 0)
        {
            var costs = new List<string>();
            foreach (var cost in skill.Costs)
            {
                if (cost == null || cost.Amount < 0) throw new InvalidOperationException("消費リソースが不正です。");
                string name = Need(cost.Resource, "消費リソース名");
                if (cost.Kind == CostKind.Resource) costs.Add(name + "-" + cost.Amount.ToString(CultureInfo.InvariantCulture));
                else if (cost.Kind == CostKind.ItemCategory || cost.Kind == CostKind.ItemName)
                {
                    if (cost.Amount == 0) throw new InvalidOperationException("消費アイテム数は1以上です。");
                    costs.Add((cost.Kind == CostKind.ItemCategory ? "〈" + name + "〉" : "《" + name + "》") + "×" + cost.Amount.ToString(CultureInfo.InvariantCulture));
                }
                else throw new InvalidOperationException("未対応の消費種別です。");
            }
            sb.AppendLine("【消費リソース】" + string.Join(", ", costs));
        }
        if (skill.CooldownTurns < 0 || skill.Roll.Count < 0) throw new InvalidOperationException("クールタイムと回数は0以上です。");
        if (skill.CooldownTurns > 0) sb.AppendLine("【クールタイム】" + skill.CooldownTurns + "ターン");
        bool automaticRoll = skill.Roll.Count == 1 && skill.Roll.Formula == "自動成功" && string.IsNullOrWhiteSpace(skill.Roll.Target);
        if (skill.Roll.Count == 0 && (!string.IsNullOrWhiteSpace(skill.Roll.Formula) || !string.IsNullOrWhiteSpace(skill.Roll.Target))) throw new InvalidOperationException("発動ロールが未使用なのに式・目標値が入力されています。回数を設定してください。");
        if (skill.Roll.Count > 0 && !automaticRoll)
        {
            string formula = Need(skill.Roll.Formula, "式");
            Need(skill.Roll.Target, "目標値");
            if (M(formula, @"\s+目標値").Success) throw new InvalidOperationException("発動ロール式に空白＋「目標値」は使用できません。");
        }
        ValidateSkillEffects(skill);
        WriteEffectGroup(sb, skill, EffectType.Declaration);
        if (automaticRoll) sb.AppendLine("【発動ロール】自動成功");
        else if (skill.Roll.Count > 0) sb.AppendLine("【発動ロール】" + (skill.Roll.Count > 1 ? skill.Roll.Count + "回：" : "") + Need(skill.Roll.Formula, "式") + " 目標値" + Need(skill.Roll.Target, "目標値"));
        foreach (EffectType type in DisplayOrder.Where(x => IsOrdinary(x) && x != EffectType.Declaration)) WriteEffectGroup(sb, skill, type);
        if (skill.Overrides.Any(x => x.Type == EffectType.SecondSpike || x.Type == EffectType.ThirdSpike))
        {
            sb.AppendLine(); WriteEffectGroup(sb, skill, EffectType.SecondSpike); WriteEffectGroup(sb, skill, EffectType.ThirdSpike);
        }
    }
    public static string Normalize(string text) { return string.Join("\n\n", InputBlocks(text).Select(x => Build(ParseOne(x)))); }
    public static string Build(SkillTextData data)
    {
        if (data == null || data.Skill == null || data.Summons == null || data.States == null || data.Skill.Choices == null || data.Skill.Choices.Any(x => x == null) || data.Summons.Any(x => x == null) || data.States.Any(x => x == null)) throw new InvalidOperationException("スキル・子スキル・召喚・状態がnullです。");
        if (data.Skill.Choices.GroupBy(x => x.Name).Any(x => x.Count() > 1) || data.Summons.GroupBy(x => x.Name).Any(x => x.Count() > 1) || data.States.GroupBy(x => x.Name).Any(x => x.Count() > 1)) throw new InvalidOperationException("子スキル・召喚・状態の名前が重複しています。");
        ValidateSummons(data);
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
            if (categories.Length == 0) throw new InvalidOperationException("状態のカテゴリーがありません。");
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
            if (!summon.Categories.Contains("ペット")) throw new InvalidOperationException("現在の召喚定義には〈ペット〉カテゴリーが必要です：" + summon.Name);
            if (summon.Stats.Count(x => x != null && x.Name == "HP") != 1) throw new InvalidOperationException("ペットにはHP最大値を1件指定してください：" + summon.Name);
            if (summon.Effects != null && summon.Effects.Where(x => x != null && x.Contents != null).SelectMany(x => x.Contents).Any(x => x != null && x.Type == EffectContentType.Summon))
                throw new InvalidOperationException("召喚定義から別の召喚は発動できません：" + summon.Name);
        }
        var bodies = new List<SkillBody> { data.Skill };
        bodies.AddRange(data.Skill.Choices);
        var references = bodies.SelectMany(x => x.Effects ?? new List<EffectDefinition>())
            .Where(x => x != null && x.Contents != null).SelectMany(x => x.Contents)
            .Where(x => x != null && x.Type == EffectContentType.Summon)
            .Select(x => Args(x.Parameters, 1, "Summon")[0]).ToList();
        foreach (string reference in references)
            if (!names.Contains(reference)) throw new InvalidOperationException("召喚定義がありません：" + reference);
        foreach (string name in names)
            if (!references.Contains(name)) throw new InvalidOperationException("参照されていない召喚定義です：" + name);
    }
}
