using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static CommonEffectTextCodec;

public static partial class ItemTextConverter
{
    private const string Separator = "――――――――――――――――";
    private static bool IsRecipe(ItemDefinition item) => item.Categories.Contains("レシピ") || item.Categories.Contains("レシピアイテム");
    private static bool IsDivider(string line) => M(line.Trim(), @"^―{5,}[。.]?$").Success;
    private static bool IsGrantedSkill(string line) => M(line, @"^●スキル[：:]\s*《[^》]+》$").Success;

    public static ItemTextData Parse(string text) => Parse(text, null);

    internal static ItemTextData Parse(string text, Action<string, string> reportSkipped)
    {
        var blocks = Blocks(text);
        if (blocks.Count != 1) throw new InvalidOperationException("アイテムは1件ずつセットしてください。");
        var data = ParseOne(blocks[0], reportSkipped);
        foreach (var entry in data.Item.OwnSkills.Concat(data.Item.Skills).Concat(data.Item.Traits))
        {
            SkillTextConverter.OrderEmbeddedEffects(entry.Skill);
            foreach (var choice in entry.Skill.Choices) SkillTextConverter.OrderEmbeddedEffects(choice);
            foreach (var summon in entry.Summons) SkillTextConverter.OrderEmbeddedEffects(summon);
        }
        return data;
    }

    public static string Normalize(string text) => string.Join("\n\n", Blocks(text).Select(block => Build(ParseOne(block))));

    private static List<string> Blocks(string text)
    {
        string source = DataBlockText.NormalizeBreaks(text).Trim();
        // 表からコピーした閉じフェンスには余分な ] が付く場合がある。
        source = DataBlockText.GetRegex(@"(?m)^(`{3,})\][ \t]*$").Replace(source, "$1");
        string[] lines = source.Split('\n');
        var opening = DataBlockText.OpeningFence(DataBlockText.NormalizeFenceLine(lines[0]));
        if (opening.Success && !lines.Skip(1).Any(l => DataBlockText.NormalizeFenceLine(l).StartsWith("```", StringComparison.Ordinal)))
            source += "\n" + opening.Groups["ticks"].Value;
        return DataBlockText.ReadBlocks(source, "アイテム", keepFences: false);
    }

    private static ItemTextData ParseOne(string source, Action<string, string> reportSkipped = null)
    {
        string[] lines = source.Trim().Split('\n');
        int index = 0;
        string Next()
        {
            while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index])) index++;
            return index < lines.Length ? lines[index++].Trim() : "";
        }
        var name = M(Next(), @"^《([^《》]+)》(?:☆([1-9][0-9]*))?$");
        if (!name.Success) throw new InvalidOperationException("先頭に《アイテム名》を指定してください。");
        var data = new ItemTextData();
        var item = data.Item;
        item.Name = Need(name.Groups[1].Value, "アイテム名");
        if (name.Groups[2].Success) item.VariantGrade = Number(name.Groups[2].Value, 1, "選択グレード");
        item.Categories = EquipmentTextConverter.Categories(Next());
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (index < lines.Length)
        {
            string line = lines[index].Trim();
            if (line.Length == 0) { index++; continue; }
            if (!line.StartsWith("【", StringComparison.Ordinal)) break;
            var headers = DataBlockText.GetRegex(@"【([^】]+)】([^【]*)").Matches(line);
            if (headers.Count == 0 || !IsMetadata(headers[0].Groups[1].Value)) break;
            foreach (System.Text.RegularExpressions.Match field in headers)
            {
                string key = field.Groups[1].Value, value = field.Groups[2].Value.Trim();
                if (!seen.Add(key)) throw new InvalidOperationException("重複したアイテム項目です：" + key);
                if (!IsMetadata(key)) throw new InvalidOperationException("未対応のアイテム項目です：" + key);
                switch (key)
                {
                    case "レアリティ": item.Rarity = value; break;
                    case "サイズ": item.Size = Need(value, key); break;
                    case "価値": item.Value = EquipmentTextConverter.ParseValue(value); break;
                    case "グレード":
                        if (!M(value, @"^(?:☆+|★+☆*)$").Success) throw new InvalidOperationException("星によるグレードを指定してください。");
                        if (value[0] == '★') item.SpecialGrade = value; else item.Grade = value.Length;
                        break;
                    case "所持条件":
                        var limit = M(value, @"^〈([^〉]+)〉は([1-9][0-9]*)つまで所持可能。?$");
                        if (!limit.Success) throw new InvalidOperationException("未対応の所持条件です：" + value);
                        item.HoldingLimitCategory = limit.Groups[1].Value;
                        item.HoldingLimit = Number(limit.Groups[2].Value, 1, key);
                        break;
                    default: item.Stats.Add(new FormulaStat { Name = key, Formula = Need(value, key) }); break;
                }
            }
            index++;
        }
        if (!seen.Contains("レアリティ")) throw new InvalidOperationException("アイテムのレアリティ欄がありません。");
        if (IsRecipe(item))
        {
            while (index < lines.Length)
            {
                string line = lines[index].Trim();
                if (line.Length == 0) { index++; continue; }
                if (!line.StartsWith("【宣言条件】", StringComparison.Ordinal)) break;
                SkillTextConverter.AddConditions(item.DeclarationConditions, line.Substring("【宣言条件】".Length));
                index++;
            }
            item.Skills = ParseRecipeChoices(string.Join("\n", lines.Skip(index)).Trim(), out item.Flavor, reportSkipped);
            return data;
        }
        int separator = Array.FindLastIndex(lines, IsDivider);
        int end = lines.Length;
        if (separator >= index)
        {
            end = separator;
            item.Flavor = string.Join("\n", lines.Skip(end + 1)).TrimEnd();
        }
        int grade = Array.FindIndex(lines, index, end - index, l => l.Trim() == "【グレード補正】");
        if (grade >= 0)
        {
            foreach (string row in lines.Skip(grade + 1).Take(end - grade - 1).Where(l => !string.IsNullOrWhiteSpace(l)))
                item.GradeAdjustments.Add(ParseGradeAdjustment(row));
            end = grade;
        }
        ParseAbilities(item, lines.Skip(index).Take(end - index).ToArray());
        return data;
    }

    private static bool IsMetadata(string key)
    {
        switch (key)
        {
            case "レアリティ": case "サイズ": case "価値": case "グレード": case "所持条件":
            case "捕獲HP": case "必要敷地": case "COST": case "LIFE": case "POWER":
            case "HP最大値": case "SAN最大値": case "防御点": return true;
            default: return key.EndsWith("B", StringComparison.Ordinal);
        }
    }

    private static void ParseAbilities(ItemDefinition item, string[] lines)
    {
        int actions = Array.FindIndex(lines, l => l.Trim() == "【行動】");
        int grants = Array.FindIndex(lines, l => IsGrantedSkill(l.Trim()));
        int end = new[] { lines.Length, actions < 0 ? lines.Length : actions, grants < 0 ? lines.Length : grants }.Min();
        int traits = actions < 0 ? -1 : Array.FindIndex(lines, 0, end, l => M(l.Trim(), @"^《[^》]+》[：:]").Success);
        if (traits >= 0)
        {
            EnemyTextConverter.AddTraits(item.Traits, lines.Skip(traits).Take(end - traits));
            end = traits;
        }
        string use = string.Join("\n", lines.Take(end)).Trim();
        if (use.StartsWith("【設置効果】", StringComparison.Ordinal))
        {
            item.InstalledEffect = true;
            string effect = use.Substring("【設置効果】".Length).Trim();
            if (effect.StartsWith("保留\n", StringComparison.Ordinal) || effect == "保留" ||
                M(effect, @"、\s*\n\s*を").Success)
            {
                item.DeferredInstalledEffect = effect.StartsWith("保留", StringComparison.Ordinal) ? effect.Substring(2).TrimStart() : effect;
                use = "";
            }
            else
            {
                var install = effect.Split('\n');
                int stock = Array.FindIndex(install, l => l.Trim() == "・設定するアイテム");
                if (stock >= 0)
                {
                    foreach (string line in install.Skip(stock + 1).Where(l => !string.IsNullOrWhiteSpace(l)))
                    {
                        var row = M(line.Trim(), @"^-\s*《([^》]+)》[,、]\s*価格([0-9]+)ジルダ[,、]\s*在庫([0-9]+)$");
                        if (!row.Success) throw new InvalidOperationException("店舗の品目・価格・在庫数が不正です：" + line);
                        item.Stock.Add(new ItemStockEntry { Name = row.Groups[1].Value, Price = Number(row.Groups[2].Value, 0, "価格"),
                            Quantity = Number(row.Groups[3].Value, 0, "在庫数") });
                    }
                    effect = string.Join("\n", install.Take(stock)).Trim();
                }
                use = "【パッシブ効果】" + effect;
            }
        }
        if (use.Length != 0)
        {
            item.OwnSkills.Add(SkillTextConverter.Parse("《" + item.Name + "》\n" + EquipmentTextConverter.CategoryText(item.Categories) + "\n" + use));
        }
        if (actions >= 0)
        {
            int first = actions + 1;
            while (first < lines.Length && !EnemyTextConverter.TrySkillBoundary(lines, first)) first++;
            item.Actions = EnemyTextConverter.ParseActions(lines.Skip(actions + 1).Take(first - actions - 1));
            while (first < lines.Length)
            {
                int next = first + 1;
                while (next < lines.Length && (!EnemyTextConverter.TrySkillBoundary(lines, next) || EnemyTextConverter.IsEmbeddedSummon(lines, first, next))) next++;
                item.Skills.Add(SkillTextConverter.Parse(string.Join("\n", lines, first, next - first).TrimEnd()));
                first = next;
            }
        }
        if (grants >= 0)
        {
            int first = grants;
            while (first < lines.Length)
            {
                int next = first + 1;
                while (next < lines.Length && !IsGrantedSkill(lines[next].Trim())) next++;
                string name = M(lines[first].Trim(), @"^●スキル[：:]\s*(《[^》]+》)$").Groups[1].Value;
                item.Skills.Add(SkillTextConverter.Parse(name + "\n" + string.Join("\n", lines, first + 1, next - first - 1)));
                first = next;
            }
        }
    }

    public static SkillTextData GetOwnSkill(ItemDefinition item)
    {
        if (item.OwnSkills == null || item.OwnSkills.Count > 1 ||
            (item.OwnSkills.Count == 1 && (item.OwnSkills[0]?.Skill == null || IsRecipe(item))))
            throw new InvalidOperationException("レシピ書の本体効果は空欄にし、他のアイテムの本体効果は1件まで指定してください。");
        return item.OwnSkills.FirstOrDefault();
    }

    public static string Build(ItemTextData data)
    {
        var item = data?.Item ?? throw new InvalidOperationException("アイテムデータがありません。");
        var primary = GetOwnSkill(item);
        var sb = new StringBuilder();
        sb.AppendLine("```");
        sb.AppendLine("《" + Need(item.Name, "アイテム名") + "》" + (item.VariantGrade > 0 ? "☆" + item.VariantGrade : ""));
        sb.AppendLine(EquipmentTextConverter.CategoryText(item.Categories));
        sb.AppendLine("【レアリティ】" + item.Rarity);
        if (!string.IsNullOrEmpty(item.SpecialGrade) || item.Grade > 0)
            sb.AppendLine("【グレード】" + (!string.IsNullOrEmpty(item.SpecialGrade) ? item.SpecialGrade : new string('☆', item.Grade)));
        if (item.Value.Kind != EquipmentValueKind.Unspecified) sb.AppendLine("【価値】" + EquipmentTextConverter.EquipmentValueText(item.Value));
        if (!string.IsNullOrEmpty(item.Size)) sb.AppendLine("【サイズ】" + item.Size);
        if (item.HoldingLimit > 0)
            sb.AppendLine("【所持条件】〈" + Need(item.HoldingLimitCategory, "所持カテゴリー") + "〉は" + item.HoldingLimit + "つまで所持可能。");
        foreach (var stat in item.Stats) sb.AppendLine("【" + Need(stat.Name, "項目名") + "】" + Need(stat.Formula, stat.Name));
        if (IsRecipe(item))
        {
            SkillTextConverter.WriteConditions(sb, "宣言条件", item.DeclarationConditions, true);
            sb.AppendLine(BuildRecipeChoices(item.Skills, item.Flavor).TrimEnd());
        }
        else
        {
            bool shop = item.InstalledEffect && primary != null && primary.Skill.Effects.SelectMany(e => e.Contents)
                .Any(c => c.Type == EffectContentType.ShopInHousing);
            if (item.Stock.Count > 0 && !shop) throw new InvalidOperationException("在庫を設定する店舗の設置効果がありません。");
            if (primary != null)
            {
                string text = SkillTextConverter.Build(primary, includeHeading: false).Trim();
                if (item.InstalledEffect && text.StartsWith("【パッシブ効果】", StringComparison.Ordinal))
                    text = "【設置効果】" + text.Substring("【パッシブ効果】".Length);
                sb.AppendLine(text);
                if (shop)
                {
                    sb.AppendLine("・設定するアイテム");
                    foreach (var row in item.Stock)
                    {
                        if (row == null || row.Price < 0 || row.Quantity < 0) throw new InvalidOperationException("店舗の価格と在庫は0以上です。");
                        sb.AppendLine("- 《" + Need(row.Name, "在庫の品目") + "》, 価格" + row.Price + "ジルダ, 在庫" + row.Quantity);
                    }
                }
            }
            else if (item.InstalledEffect) sb.AppendLine("【設置効果】保留\n" + item.DeferredInstalledEffect);
            foreach (var trait in item.Traits) sb.AppendLine(EnemyTextConverter.TraitBodyText(trait));
            if (item.Actions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("【行動】");
                foreach (var rule in item.Actions) sb.AppendLine("●" + EnemyTextConverter.ActionText(rule));
            }
            foreach (var skill in item.Skills)
            {
                sb.AppendLine();
                if (item.Actions.Count == 0) sb.Append("●スキル：");
                sb.AppendLine(EnemyTextConverter.SkillBodyText(skill));
            }
            if (item.GradeAdjustments.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("【グレード補正】");
                foreach (var adjustment in item.GradeAdjustments) sb.AppendLine(GradeAdjustmentText(adjustment));
            }
            if (!string.IsNullOrEmpty(item.Flavor) || item.Actions.Count > 0)
            {
                sb.AppendLine(Separator);
                sb.AppendLine(item.Flavor);
            }
        }
        sb.Append("```");
        return sb.ToString().Replace("\r\n", "\n");
    }
}
