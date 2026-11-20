using System;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static SkillTextData ParseOne(string source)
    {
        string[] lines = PrepareSkillLines(source.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'));
        int start = 0, end = lines.Length - 1;
        while (start <= end && string.IsNullOrWhiteSpace(lines[start])) start++;
        while (end >= start && string.IsNullOrWhiteSpace(lines[end])) end--;
        if (start <= end && lines[start].Trim().StartsWith("```"))
        {
            if (start == end || lines[end].Trim() != "```") throw new InvalidOperationException("末尾のコードフェンスがありません。");
            start++; end--;
        }
        var data = new SkillTextData();
        SkillBody current = data.Skill;
        EffectType section = EffectType.None;
        StateDefinition state = null;
        string conditions = "";
        bool named = false, description = false;
        var stateOwners = new System.Collections.Generic.Dictionary<StateDefinition, SkillBody>();
        for (int i = start; i <= end; i++)
        {
            // Markdown transport sometimes escapes multiplication signs. Flavor text
            // is copied from the original lines below and is intentionally untouched.
            string line = lines[i].Trim().Replace("\\*", "*");
            if (line.Length == 0) continue;
            // These are wrapped effect sentences, not additional category headers.
            if (section == EffectType.Passive &&
                ((M(line, @"^〈[^〉]+〉$").Success && i < end && lines[i + 1].TrimStart().StartsWith("上記の装備", StringComparison.Ordinal)) ||
                 line.EndsWith("および", StringComparison.Ordinal)))
            {
                if (i < end) line += lines[++i].Trim();
            }
            try
            {
                if (M(line, @"^―{8,}$").Success)
                {
                    data.Skill.Flavor = string.Join("\n", lines.Skip(i + 1).Take(end - i)); break;
                }
                if (!named)
                {
                    Match name = M(line, @"^《([^《》]+)》$");
                    if (!name.Success) throw new InvalidOperationException("先頭に《スキル名》を指定してください。");
                    data.Skill.Name = Need(name.Groups[1].Value, "スキル名"); named = true; continue;
                }
                Match summonHeader = M(line, @"^《([^《》]+)》$");
                if (summonHeader.Success)
                {
                    var summon = new SummonedEntityDefinition { Name = Need(summonHeader.Groups[1].Value, "召喚名") };
                    if (data.Summons.Any(x => x.Name == summon.Name)) throw new InvalidOperationException("召喚定義が重複しています：" + summon.Name);
                    data.Summons.Add(summon); current = summon; section = EffectType.None; state = null; conditions = ""; continue;
                }
                if (line.StartsWith("●"))
                {
                    if (!description && current == data.Skill && current.Effects.Count == 0 && current.Roll.Count == 0 &&
                        i + 1 <= end && M(lines[i + 1].Trim(), @"^【(?:消費リソース|宣言条件|発動ロール)】").Success) description = true;
                    if (!description) throw new InvalidOperationException("子スキルの前に選択式の【説明】が必要です。");
                    current = new SkillBody { Name = Need(line.Substring(1), "子スキル名") };
                    data.Skill.Choices.Add(current); section = EffectType.None; state = null; conditions = ""; continue;
                }
                if (M(line, @"^(?:〈[^〉]+〉)+$").Success && section == EffectType.None && conditions.Length == 0)
                {
                    if (current.Categories.Count > 0) throw new InvalidOperationException("カテゴリー行が重複しています。");
                    current.Categories = Categories(line); continue;
                }
                Match stateHeader = M(Unbullet(line), @"^([^・⚫【].*?)効果((?:〈[^〉]+〉)*)[：:](.*)$");
                if (stateHeader.Success)
                {
                    state = new StateDefinition { Name = Need(stateHeader.Groups[1].Value, "状態名"), Categories = stateHeader.Groups[2].Value.Length == 0 ? new System.Collections.Generic.List<string>() : Categories(stateHeader.Groups[2].Value) };
                    if (data.States.Any(x => x.Name == state.Name &&
                        (stateOwners[x] == current || !data.Skill.Choices.Contains(current) || !data.Skill.Choices.Contains(stateOwners[x]))))
                        throw new InvalidOperationException("状態定義が重複しています：" + state.Name);
                    stateOwners.Add(state, current);
                    data.States.Add(state); conditions = "";
                    string body = stateHeader.Groups[3].Value.Trim();
                    if (body.Length > 0 && !ReadStateText(state, body)) throw new InvalidOperationException("未対応の状態効果です：" + body);
                    continue;
                }
                Match header = M(line, @"^【([^】]+)】(.*)$");
                if (header.Success)
                {
                    // A trap can be defined between its placing attack and that attack's
                    // rank upgrades. Route the upgrade only when its target is unique.
                    if (current is SummonedEntityDefinition && M(header.Groups[1].Value, @"^(?:セカンド|サード)スパイク$").Success &&
                        header.Groups[2].Value.Contains("このアクティブ効果によるスキル値") &&
                        !current.Effects.Where(e => e.Type == EffectType.Active).SelectMany(e => e.Contents).Any(HasRuleSkillValue))
                    {
                        var owners = new[] { (SkillBody)data.Skill }.Concat(data.Skill.Choices)
                            .Where(s => s.Effects.SelectMany(e => e.Contents).Any(c => c.Type == EffectContentType.PlaceTrap && SummonName(c) == current.Name) &&
                                s.Effects.Where(e => e.Type == EffectType.Active).SelectMany(e => e.Contents).Count(HasRuleSkillValue) == 1).ToList();
                        if (owners.Count == 1) current = owners[0];
                    }
                    string key = header.Groups[1].Value, body = header.Groups[2].Value.Trim();
                    if (key == "クリティカル") key = "クリティカル効果";
                    section = EffectType.None; state = null; conditions = "";
                    switch (key)
                    {
                        case "説明":
                            if (current != data.Skill || body != Description || description) throw new InvalidOperationException("説明は親スキルの選択式1種類に対応します。");
                            description = true; break;
                        case "装備条件":
                        case "習得条件":
                        case "宣言条件":
                        case "宣言上限":
                            if (key == "宣言上限") key = "宣言条件";
                            conditions = key; AddConditions(key == "習得条件" ? current.AcquisitionConditions : key == "装備条件" ? current.EquipmentConditions : current.DeclarationConditions, body); break;
                        case "消費リソース": ReadCosts(current, body); break;
                        case "消費ACT": ReadCosts(current, "ACT-" + Number(body, 0, "ACT消費")); break;
                        case "消費MP": ReadCosts(current, "MP-" + Number(body, 0, "MP消費")); break;
                        case "クールダウン":
                        case "クールタイム":
                            if (M(body, @"^戦闘中[0-9]+度だけ$").Success) { AddConditions(current.DeclarationConditions, body); break; }
                            Match cooldown = M(body, @"^([0-9]+)ターン$");
                            if (!cooldown.Success) throw new InvalidOperationException("クールタイムは1ターンの形式です。");
                            current.CooldownTurns = Number(cooldown.Groups[1].Value, 0, "クールタイム"); break;
                        case "発動":
                        case "発動ロール":
                            if (current.Roll.Count != 0) throw new InvalidOperationException("同じスキルの発動ロールが重複しています。別の処理は子スキルへ分けてください。");
                            if (body.TrimEnd('、', '。') == "自動成功、達成値は召喚者と同値として扱われる")
                            {
                                current.Roll = new DiceRollDefinition { Count = 1, Formula = "自動成功", UseSummonerResult = true };
                                break;
                            }
                            Match automatic = M(body, @"^自動成功、達成値は([0-9]+)となる。?$");
                            if (automatic.Success)
                            {
                                current.Roll = new DiceRollDefinition { Count = 1, Formula = "自動成功", FixedResult = Number(automatic.Groups[1].Value, 0, "固定達成値") };
                                break;
                            }
                            Match items = M(body, @"^〈([^〉]+)〉(?:または〈([^〉]+)〉)?に記載$");
                            if (body == "レシピ記載" || items.Success)
                            {
                                current.Roll = new DiceRollDefinition { Count = 1 };
                                current.Roll.SourceItemCategories.Add(items.Success ? items.Groups[1].Value : "レシピ");
                                if (items.Success && items.Groups[2].Success) current.Roll.SourceItemCategories.Add(items.Groups[2].Value);
                                break;
                            }
                            var representative = M(body, @"^代表([1-9][0-9]*)名が(.+)$");
                            if (representative.Success) body = representative.Groups[2].Value;
                            if (body == "自動成功")
                            {
                                current.Roll = new DiceRollDefinition { Count = 1, Formula = "自動成功" };
                                break;
                            }
                            Match roll = M(body, @"^(?:([0-9]+)回(?:連続)?[：:]\s*)?(.+?)\s+目標値(.+)$");
                            if (!roll.Success) throw new InvalidOperationException("発動ロールは「式 目標値30」の形式です。");
                            current.Roll = new DiceRollDefinition { Count = roll.Groups[1].Success ? Number(roll.Groups[1].Value, 1, "回数") : 1, Formula = roll.Groups[2].Value.Trim(), Target = roll.Groups[3].Value.Trim(),
                                RepresentativeCount = representative.Success ? Number(representative.Groups[1].Value, 1, "代表人数") : 0 }; break;
                        default:
                            Match statHeader = M(key, @"^(.+)最大値$");
                            if (statHeader.Success)
                            {
                                SummonedEntityDefinition statOwner = current as SummonedEntityDefinition;
                                if (statOwner == null) throw new InvalidOperationException("最大値は召喚定義に指定してください。");
                                string statName = Need(statHeader.Groups[1].Value, "召喚ステータス名");
                                if (statOwner.Stats.Any(x => x.Name == statName)) throw new InvalidOperationException(statName + "最大値が重複しています。");
                                statOwner.Stats.Add(new FormulaStat { Name = statName, Formula = Need(body, statName + "最大値") }); break;
                            }
                            EffectType parsedType;
                            if (!EffectTypesByName.TryGetValue(key, out parsedType)) throw new InvalidOperationException("未対応の見出しです：" + key);
                            section = parsedType;
                            if (body.Length > 0) ReadSkillLine(current, section, body); break;
                    }
                    continue;
                }
                line = Unbullet(line);
                if (state != null && ReadStateText(state, line)) continue;
                if (line.StartsWith("*") || line.StartsWith("＊")) throw new InvalidOperationException("未対応の状態の解除条件です：" + line);
                if (conditions.Length > 0)
                {
                    AddConditions(conditions == "習得条件" ? current.AcquisitionConditions : conditions == "装備条件" ? current.EquipmentConditions : current.DeclarationConditions, line); continue;
                }
                if (section == EffectType.None && line.TrimEnd().EndsWith("宣言可能。", StringComparison.Ordinal))
                {
                    AddConditions(current.DeclarationConditions, line); continue;
                }
                if (section == EffectType.None) throw new InvalidOperationException("効果の見出しがありません。");
                ReadSkillLine(current, section, line);
            }
            catch (InvalidOperationException e) { throw new InvalidOperationException((i + 1) + "行目：" + e.Message + "\n原文：" + lines[i], e); }
        }
        if (!named) throw new InvalidOperationException("スキル名がありません。");
        MergeChoiceStateDefinitions(data);
        CompleteStackStateReferences(data);
        CompleteDelayedAttackModifiers(data);
        CompleteSelectedSkillModifiers(data);
        CompleteAreaProgressModifiers(data);
        if (description && data.Skill.Choices.Count == 0) throw new InvalidOperationException("説明に対応する子スキルがありません。");
        // Preserve the established common-trigger representation for a state
        // containing only a damage/power modifier. Mixed states use local triggers.
        foreach (StateDefinition parsedState in data.States)
        {
            if (parsedState.Effects.Count != 0 || parsedState.Overrides.Count != 1 ||
                parsedState.Trigger.Timing != TriggerTiming.Always || !Empty(parsedState.Trigger.Conditions)) continue;
            OverrideDefinition modifier = parsedState.Overrides[0];
            if (modifier.Triggers.Count != 1) continue;
            TriggerDefinition trigger = modifier.Triggers[0];
            if (trigger.Timing != TriggerTiming.IncomingDamage && trigger.Timing != TriggerTiming.AttackPower) continue;
            parsedState.Trigger = trigger;
            modifier.Triggers.Clear();
        }
        return data;
    }
    private static void ReadCosts(SkillBody skill, string body)
    {
        body = RegexReplace(body, @"(?<=\d)\s+(?=[A-Z]+-)", ", ").Replace('、', ',');
        foreach (string part in Split(body, ","))
        {
            if (part.Contains("選択") && (part.Contains("宣言可能") || part.StartsWith("装備している")))
            {
                AddConditions(skill.DeclarationConditions, part); continue;
            }
            var cost = new ResourceCost();
            Match m = M(part, @"^(全ての|任意の)([A-Z]+)$");
            if (m.Success) { cost.Resource = m.Groups[2].Value; cost.Quantity = m.Groups[1].Value == "全ての" ? CostQuantity.All : CostQuantity.Chosen; }
            else if ((m = M(part, @"^([A-Z]+)を([0-9]+)残して全消費$")).Success)
            { cost.Resource = m.Groups[1].Value; cost.Quantity = CostQuantity.LeaveRemaining; cost.Amount = Number(m.Groups[2].Value, 0, "残す量"); }
            else if ((m = M(part, @"^([A-Z]+)-(\([0-9]+\+\[[^\[\]。]+\]\*[0-9]+\))$")).Success)
            { cost.Resource = m.Groups[1].Value; cost.Quantity = CostQuantity.Formula; cost.Formula = m.Groups[2].Value; }
            else if ((m = M(part, @"^(?:〈([^〉]+)〉|《([^》]+)》)(?:スタック-([0-9]+)|(全スタック消費))$")).Success)
            {
                cost.Kind = m.Groups[1].Success ? CostKind.CategoryStack : CostKind.NamedStack;
                cost.Resource = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                if (m.Groups[4].Success) cost.Quantity = CostQuantity.All;
                else cost.Amount = Number(m.Groups[3].Value, 1, "スタック");
            }
            else if ((m = M(part, @"^任意の数の《([^》]+)》$")).Success)
            { cost.Kind = CostKind.NamedStack; cost.Quantity = CostQuantity.Chosen; cost.Resource = m.Groups[1].Value; }
            else if ((m = M(part, @"^重複しない名前の〈([^〉]+)〉×([0-9]+)～([0-9]+)個$")).Success)
            {
                cost.Kind = CostKind.ItemCategory; cost.Quantity = CostQuantity.DistinctNameRange; cost.Resource = m.Groups[1].Value;
                cost.Amount = Number(m.Groups[2].Value, 1, "最小数"); cost.Maximum = Number(m.Groups[3].Value, cost.Amount, "最大数");
            }
            else if ((m = M(part, @"^選択する〈([^〉]+)〉-([0-9]+)$")).Success)
            { cost.Kind = CostKind.SelectedCategoryItem; cost.Resource = m.Groups[1].Value; cost.Amount = Number(m.Groups[2].Value, 1, "アイテム数"); }
            else if ((m = M(part, @"^〈([^〉]+)〉-([0-9]+)$")).Success)
            { cost.Kind = CostKind.ItemCategory; cost.Resource = m.Groups[1].Value; cost.Amount = Number(m.Groups[2].Value, 1, "アイテム数"); }
            else if ((m = M(part, @"^(?:〈([^〉]+)〉|《([^》]+)》)\s*[×x]\s*([0-9]+)$")).Success)
            {
                cost.Kind = m.Groups[1].Success ? CostKind.ItemCategory : CostKind.ItemName;
                cost.Resource = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                cost.Amount = Number(m.Groups[3].Value, 1, "アイテム数");
            }
            else if ((m = M(part, @"^([0-9]+)(ジルダ)$")).Success)
            { cost.Kind = CostKind.Currency; cost.Resource = m.Groups[2].Value; cost.Amount = Number(m.Groups[1].Value, 1, "通貨"); }
            else if ((m = M(part, @"^\[(選択された所持品)\]-([0-9]+)$")).Success)
            { cost.Kind = CostKind.SelectedItem; cost.Resource = m.Groups[1].Value; cost.Amount = Number(m.Groups[2].Value, 1, "アイテム数"); }
            else if ((m = M(part, @"^([^\s\-〈〉《》\[\]]+)-([0-9]+)$")).Success)
            { cost.Resource = m.Groups[1].Value; cost.Amount = Number(m.Groups[2].Value, 0, "消費量"); }
            else throw new InvalidOperationException("未対応の消費リソースです：" + part);
            skill.Costs.Add(cost);
        }
    }
}
