using System;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static SkillTextData ParseOne(string source)
    {
        string[] lines = source.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
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
        for (int i = start; i <= end; i++)
        {
            // Markdown transport sometimes escapes multiplication signs. Flavor text
            // is copied from the original lines below and is intentionally untouched.
            string line = lines[i].Trim().Replace("\\*", "*");
            if (line.Length == 0) continue;
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
                    if (!description) throw new InvalidOperationException("子スキルの前に選択式の【説明】が必要です。");
                    current = new SkillBody { Name = Need(line.Substring(1), "子スキル名") };
                    data.Skill.Choices.Add(current); section = EffectType.None; state = null; conditions = ""; continue;
                }
                if (M(line, @"^(?:〈[^〉]+〉)+$").Success && section == EffectType.None && conditions.Length == 0)
                {
                    if (current.Categories.Count > 0) throw new InvalidOperationException("カテゴリー行が重複しています。");
                    current.Categories = Categories(line); continue;
                }
                Match stateHeader = M(line, @"^(.+?)効果((?:〈[^〉]+〉)+)[：:](.*)$");
                if (stateHeader.Success)
                {
                    state = new StateDefinition { Name = Need(stateHeader.Groups[1].Value, "状態名"), Categories = Categories(stateHeader.Groups[2].Value) };
                    if (data.States.Any(x => x.Name == state.Name)) throw new InvalidOperationException("状態定義が重複しています：" + state.Name);
                    data.States.Add(state); conditions = "";
                    string body = stateHeader.Groups[3].Value.Trim();
                    if (body.Length > 0 && !ReadStateText(state, body)) throw new InvalidOperationException("未対応の状態効果です：" + body);
                    continue;
                }
                Match header = M(line, @"^【([^】]+)】(.*)$");
                if (header.Success)
                {
                    string key = header.Groups[1].Value, body = header.Groups[2].Value.Trim();
                    if (key == "クリティカル") key = "クリティカル効果";
                    section = EffectType.None; state = null; conditions = "";
                    switch (key)
                    {
                        case "説明":
                            if (current != data.Skill || body != Description || description) throw new InvalidOperationException("説明は親スキルの選択式1種類に対応します。");
                            description = true; break;
                        case "習得条件":
                        case "宣言条件":
                            conditions = key; AddConditions(key == "習得条件" ? current.AcquisitionConditions : current.DeclarationConditions, body); break;
                        case "消費リソース": ReadCosts(current, body); break;
                        case "クールタイム":
                            Match cooldown = M(body, @"^([0-9]+)ターン$");
                            if (!cooldown.Success) throw new InvalidOperationException("クールタイムは1ターンの形式です。");
                            current.CooldownTurns = Number(cooldown.Groups[1].Value, 0, "クールタイム"); break;
                        case "発動ロール":
                            Match roll = M(body, @"^(?:([0-9]+)回[：:]\s*)?(.+?)\s+目標値(.+)$");
                            if (!roll.Success) throw new InvalidOperationException("発動ロールは「式 目標値30」の形式です。");
                            current.Roll = new DiceRollDefinition { Count = roll.Groups[1].Success ? Number(roll.Groups[1].Value, 1, "回数") : 1, Formula = roll.Groups[2].Value.Trim(), Target = roll.Groups[3].Value.Trim() }; break;
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
                    AddConditions(conditions == "習得条件" ? current.AcquisitionConditions : current.DeclarationConditions, line); continue;
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
        if (description && data.Skill.Choices.Count == 0) throw new InvalidOperationException("説明に対応する子スキルがありません。");
        return data;
    }
    private static void ReadCosts(SkillBody skill, string body)
    {
        foreach (string part in Split(body, ","))
        {
            Match item = M(part, @"^(?:〈([^〉]+)〉|《([^》]+)》)\s*[×x]\s*([0-9]+)$");
            if (item.Success)
            {
                skill.Costs.Add(new ResourceCost { Kind = item.Groups[1].Success ? CostKind.ItemCategory : CostKind.ItemName, Resource = item.Groups[1].Success ? item.Groups[1].Value : item.Groups[2].Value, Amount = Number(item.Groups[3].Value, 1, "アイテム数") }); continue;
            }
            Match cost = M(part, @"^([^\s-]+)-([0-9]+)$");
            if (!cost.Success) throw new InvalidOperationException("消費はACT-6 / 〈魔法薬〉×1の形式です。");
            skill.Costs.Add(new ResourceCost { Resource = cost.Groups[1].Value, Amount = Number(cost.Groups[2].Value, 0, "消費量") });
        }
    }
}
