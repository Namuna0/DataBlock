using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private enum SkillField { Other, Description, Equipment, Acquisition, Declaration, Costs, Act, Mp, Cooldown, Roll }
    private static readonly Dictionary<string, SkillField> SkillFields = new Dictionary<string, SkillField>(StringComparer.Ordinal)
    {
        { "説明", SkillField.Description }, { "装備条件", SkillField.Equipment }, { "習得条件", SkillField.Acquisition },
        { "宣言条件", SkillField.Declaration }, { "宣言上限", SkillField.Declaration }, { "消費リソース", SkillField.Costs },
        { "消費ACT", SkillField.Act }, { "消費MP", SkillField.Mp }, { "クールダウン", SkillField.Cooldown },
        { "クールタイム", SkillField.Cooldown }, { "発動", SkillField.Roll }, { "発動ロール", SkillField.Roll }
    };

    private static bool IsCategoryLine(string line) => M(line, @"^(?:〈[^〉]+〉)+$").Success;
    private static Match FieldHeader(string line) => M(line, @"^【([^】]+)】(.*)$");

    private static bool StartsChoiceFields(string line) => M(line, @"^【(?:消費リソース|宣言条件|発動ロール)】").Success;

    internal static bool IsSkillBodyStart(string line, string nextLine)
    {
        if (IsCategoryLine(line) || (line.StartsWith("●") && StartsChoiceFields(nextLine))) return true;
        var header = FieldHeader(line);
        if (!header.Success) return false;
        string key = header.Groups[1].Value;
        return SkillFields.ContainsKey(key) || ParseEffectSections(key) != null || RewardTableHeader(line, nextLine).Success;
    }

    private static void ReadSkillEffectLines(SkillBody skill, EffectType[] sections, string text,
        string[] lines, ref int index, int end)
    {
        bool consumed = false;
        foreach (var section in sections)
        {
            int count = skill.Effects.Count;
            ReadSkillLine(skill, section, text);
            if (index < end && skill.Effects.Count > count &&
                CommonEffectTextCodec.TryReadContinuation(skill.Effects.Last().Contents, Unbullet(lines[index + 1])))
                consumed = true;
        }
        if (consumed) index++;
    }

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
        EffectType[] sections = Array.Empty<EffectType>();
        StateDefinition state = null;
        bool inlineStateBody = false;
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
            if (sections.Contains(EffectType.Passive) &&
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
                    data.Summons.Add(summon); current = summon; sections = Array.Empty<EffectType>(); state = null; conditions = ""; continue;
                }
                if (line.StartsWith("●"))
                {
                    if (state != null && ReadStateLine(state, line.Substring(1).Trim())) continue;
                    if (!description && current == data.Skill && current.Effects.Count == 0 && current.Roll.Count == 0 &&
                        i + 1 <= end && StartsChoiceFields(lines[i + 1].Trim())) description = true;
                    if (!description) throw new InvalidOperationException("子スキルの前に選択式の【説明】が必要です。");
                    current = new SkillBody { Name = Need(line.Substring(1), "子スキル名") };
                    data.Skill.Choices.Add(current); sections = Array.Empty<EffectType>(); state = null; conditions = ""; continue;
                }
                if (IsCategoryLine(line) && sections.Length == 0 && conditions.Length == 0)
                {
                    if (current.Categories.Count > 0) throw new InvalidOperationException("カテゴリー行が重複しています。");
                    current.Categories = Categories(line); continue;
                }
                if (TryStateHeader(line, out var definedState, out string stateBody))
                {
                    RegisterState(definedState);
                    state = definedState;
                    conditions = "";
                    inlineStateBody = stateBody.Length > 0;
                    if (inlineStateBody && !ReadTable(stateBody, ref i, state) && !ReadStateText(state, stateBody))
                        throw new InvalidOperationException("未対応の状態効果です：" + stateBody);
                    continue;
                }
                Match header = FieldHeader(line);
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
                    sections = Array.Empty<EffectType>(); state = null; conditions = "";
                    SkillFields.TryGetValue(key, out var field);
                    switch (field)
                    {
                        case SkillField.Description:
                            if (current != data.Skill || body != Description || description) throw new InvalidOperationException("説明は親スキルの選択式1種類に対応します。");
                            description = true; break;
                        case SkillField.Equipment:
                        case SkillField.Acquisition:
                        case SkillField.Declaration:
                            if (key == "宣言上限") key = "宣言条件";
                            conditions = key; AddConditions(key == "習得条件" ? current.AcquisitionConditions : key == "装備条件" ? current.EquipmentConditions : current.DeclarationConditions, body); break;
                        case SkillField.Costs: ReadCosts(current, body); break;
                        case SkillField.Act: ReadCosts(current, "ACT-" + Number(body, 0, "ACT消費")); break;
                        case SkillField.Mp: ReadCosts(current, "MP-" + Number(body, 0, "MP消費")); break;
                        case SkillField.Cooldown:
                            if (M(body, @"^戦闘中[0-9]+度だけ$").Success) { AddConditions(current.DeclarationConditions, body); break; }
                            Match cooldown = M(body, @"^([0-9]+)ターン$");
                            if (!cooldown.Success) throw new InvalidOperationException("クールタイムは1ターンの形式です。");
                            current.CooldownTurns = Number(cooldown.Groups[1].Value, 0, "クールタイム"); break;
                        case SkillField.Roll:
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
                                foreach (string name in statHeader.Groups[1].Value.Split('/'))
                                {
                                    string statName = Need(name, "召喚ステータス名");
                                    if (statOwner.Stats.Any(x => x.Name == statName)) throw new InvalidOperationException(statName + "最大値が重複しています。");
                                    statOwner.Stats.Add(new FormulaStat { Name = statName, Formula = Need(body, statName + "最大値") });
                                }
                                break;
                            }
                            sections = ParseEffectSections(key) ?? throw new InvalidOperationException("未対応の見出しです：" + key);
                            if (body.Length > 0 && !ReadTable(body, ref i, null))
                                ReadSkillEffectLines(current, sections, body, lines, ref i, end);
                            break;
                    }
                    continue;
                }
                line = Unbullet(line);
                bool stateNote = line.StartsWith("*") || line.StartsWith("＊");
                if (ReadTable(line, ref i, state)) continue;
                if (state != null && ReadStateText(state, line, current, sections, sections.Length == 0 || !inlineStateBody || stateNote)) continue;
                if (line.StartsWith("*") || line.StartsWith("＊")) throw new InvalidOperationException("未対応の状態の解除条件です：" + line);
                // 見出しをまたいで続く宣言条件も、条件の文型で読み取る。
                if (conditions.Length > 0 || sections.Length == 0)
                {
                    AddConditions(conditions == "習得条件" ? current.AcquisitionConditions : conditions == "装備条件" ? current.EquipmentConditions : current.DeclarationConditions, line); continue;
                }
                ReadSkillEffectLines(current, sections, line, lines, ref i, end);
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
        foreach (StateDefinition parsedState in data.States) NormalizeStateTrigger(parsedState);
        return data;

        void RegisterState(StateDefinition definition)
        {
            if (data.States.Any(x => x.Name == definition.Name &&
                (stateOwners[x] == current || !data.Skill.Choices.Contains(current) || !data.Skill.Choices.Contains(stateOwners[x]))))
                throw new InvalidOperationException("状態定義が重複しています：" + definition.Name);
            data.States.Add(definition);
            stateOwners.Add(definition, current);
        }

        bool ReadInlineState(string definition)
        {
            if (!TryStateHeader(definition, out var nested, out var body, allowShorthand: true) || !ReadStateText(nested, body)) return false;
            RegisterState(nested);
            return true;
        }

        bool ReadTable(string header, ref int index, StateDefinition owner)
        {
            if (owner == null && (sections.Length == 0 || !sections.All(IsOrdinary))) return false;
            if (!CommonEffectRollTable.TryRead(header, lines, ref index, end, ReadInlineState, out var contents)) return false;
            if (owner != null) AddStateEffect(owner, NoTriggers(), contents.ToArray());
            else foreach (var section in sections) current.Effects.Add(new EffectDefinition { Type = section, Contents = new List<EffectContent>(contents) });
            return true;
        }
    }
    private static EffectType[] ParseEffectSections(string key)
    {
        if (key.EndsWith("効果効果", StringComparison.Ordinal)) key = key.Substring(0, key.Length - 2);
        if (key == "特性") key = "パッシブ効果";
        if (key == "クリティカル") key = "クリティカル効果";
        if (EffectTypesByName.TryGetValue(key, out EffectType single)) return new[] { single };
        if (!key.Contains("/") || !key.EndsWith("効果", StringComparison.Ordinal))
            return null;

        var results = new System.Collections.Generic.List<EffectType>();
        foreach (string part in key.Split('/'))
        {
            string label = part.EndsWith("効果", StringComparison.Ordinal) ? part : part + "効果";
            if (!EffectTypesByName.TryGetValue(label, out EffectType type) ||
                (type != EffectType.Success && type != EffectType.Failure && type != EffectType.Critical && type != EffectType.Fumble) ||
                results.Contains(type)) return null;
            results.Add(type);
        }
        return results.ToArray();
    }

    private static void ReadCosts(SkillBody skill, string body)
    {
        body = RegexReplace(body, @"(?<=\d)\s+(?=[A-Z]+-)", ", ").Replace('、', ',');
        foreach (string part in Split(body, ","))
        {
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
            else if (part.Contains("選択")) { AddConditions(skill.DeclarationConditions, part); continue; }
            else throw new InvalidOperationException("未対応の消費リソースです：" + part);
            skill.Costs.Add(cost);
        }
    }
}
