using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class CommonEffectTextCodec
{
    private const string SelectedPrefix = "この武器の選択を条件にスキルを発動する場合、";
    internal const string BonusName = @"[^〈〉《》\[\]【】。、,×＋+\-\s]+";
    internal const string TagName = @"[^〈〉《》【】。\r\n]+?";
    private const string Factor = @"(?:[1-9][0-9]*(?:\.[0-9]+)?|0\.[0-9]*[1-9][0-9]*)";
    internal const string Quantity = @"[1-9][0-9]*";
    private const string Amount = @"[+-](?:[0-9]+(?:\.[0-9]+)?(?:d[1-9][0-9]*)?|\[[^\[\]。]+\])";
    private const string MathFormula = @"(?:[0-9d.*+()/×\-]|\[[^\[\]。]+\])+";
    internal const string ResourceCode = @"[A-Z]+";

    // A named effect contains its own clauses, including their trigger scope and operands.
    private static readonly DataBlockSentencePattern[] RulePatterns =
    {
        new DataBlockSentencePattern("IgnoreDefense", "攻撃対象の防御点を{0}点無視する", Quantity),
        new DataBlockSentencePattern("StatResultFactor", "自身の[{0}]による達成値は×{1}倍", BonusName, Factor),
        new DataBlockSentencePattern("StatFactor", "自身の[{0}]は×{1}倍", BonusName, Factor),
        new DataBlockSentencePattern("TargetCategoryPower", "効果対象が〈{0}〉である場合〈{1}〉の威力は×{2}倍", TagName, TagName, Factor),
        new DataBlockSentencePattern("CategoryPower", "効果対象への〈{0}〉の威力は×{1}倍", TagName, Factor),
        new DataBlockSentencePattern("CategoryReduction", "自身の〈{0}〉による被ダメージ軽減は×{1}倍", TagName, Factor),
        new DataBlockSentencePattern("CounterResult", "自身のカウンター効果の達成値は×{0}倍", Factor),
        new DataBlockSentencePattern("ExcludedTargetCategoriesResult", "効果対象が〈{0}〉及び〈{1}〉を含まないなら行為判定の達成値は×{2}倍", TagName, TagName, Factor),
        new DataBlockSentencePattern("CategoryTarget", "〈{0}〉の目標値は×{1}倍", TagName, Factor),
        new DataBlockSentencePattern("CategoryResult", "〈{0}〉の達成値は×{1}倍", TagName, Factor),
        new DataBlockSentencePattern("SameMeleeIncomingTarget", "同じ接近グループからの〈{0}〉の目標値は×{1}倍", TagName, Factor),
        new DataBlockSentencePattern("FirstTurnActionPower", "自身のターン最初の〈{0}〉かつ〈{1}〉の威力は×{2}倍", TagName, TagName, Factor),
        new DataBlockSentencePattern("ResourceCostFactor", "消費{0}は×{1}倍", ResourceCode, Factor),
        new DataBlockSentencePattern("ResourceCostDelta", "{0}消費{1}", ResourceCode, Amount),
        new DataBlockSentencePattern("IncomingDamageFactor", "被ダメージ×{0}倍", Factor),
        new DataBlockSentencePattern("TargetResourceFloor", "対象の{0}は{1}を下回らない", ResourceCode, Quantity),
        new DataBlockSentencePattern("PowerFactor", "威力は×{0}倍", MathFormula),
        // The category qualifies the user’s own activation roll. Incoming actions have a separate target condition.
        new DataBlockSentencePattern("CategoryTargetDelta", "自身の〈{0}〉による目標値は{1}される", TagName, Amount),
        new DataBlockSentencePattern("SkillResultDelta", "自身の《{0}》による達成値は{1}される", TagName, Amount),
        new DataBlockSentencePattern("IncomingElementPowerDamage", "自身の{0}属性威力による被ダメージは×{1}倍", BonusName, Factor),
        new DataBlockSentencePattern("IncomingElementDamage", "自身の{0}属性による被ダメージは×{1}倍", BonusName, Factor),
        new DataBlockSentencePattern("RollResult", "{0}ロールによる達成値は×{1}倍", BonusName, Factor),
        new DataBlockSentencePattern("BothHandsUnarmed", "このアイテムを装備している時、両手は素手として扱われる"),
        new DataBlockSentencePattern("CategoryStackFactor", "自身の〈{0}〉による〈{1}〉スタックの付与量は{2}倍", TagName, TagName, Factor),
        new DataBlockSentencePattern("CategoryIgnoreDefense", "自身の〈{0}〉によるダメージは防御点を{1}無視する", TagName, Quantity),
        new DataBlockSentencePattern("CategoryPairPower", "〈{0}〉及び〈{1}〉による〈{2}〉の威力は×{3}倍", TagName, TagName, TagName, Factor),
        new DataBlockSentencePattern("ExtraDamageToTargetCategories", "さらに〈{0}〉の対象が〈{1}〉及び〈{2}〉の場合、追加で{3}のダメージを与える", TagName, TagName, TagName, Quantity),
        new DataBlockSentencePattern("GatheringSkillResult", "《{0}》による採取達成値{1}", TagName, Amount),
        new DataBlockSentencePattern("EquippedSkillResult", "装備時《{0}》の達成値が{1}される", TagName, Amount),
        new DataBlockSentencePattern("TaggedDiceResult", "自身の〈{0}〉によるダイスロールの達成値は{1}される", TagName, Amount),
        new DataBlockSentencePattern("UseItemCategoryAsGrantedAction", "自身は〈{0}〉アイテムを以下の効果として扱うことができる", TagName),
        new DataBlockSentencePattern("InventoryCapacity", "アイテムを所持している場合、所持出来る〈{0}〉の{1}が{2}増加する", TagName, "本数|個数", Amount),
        new DataBlockSentencePattern("HeldEnemyStandbyResult", "アイテムを所持している場合、スタンバイフェーズの敵の{0}ロールの達成値合計は{1}される", BonusName, Amount),
        new DataBlockSentencePattern("IgnoreRollState", "自身は{0}ロールによる《{1}》を無効にする", BonusName, TagName),
        new DataBlockSentencePattern("ResourceDamageFactor", "自身の与{0}ダメージは×{1}倍", ResourceCode, Factor),
        new DataBlockSentencePattern("ElementalCategoryPower", "自身の{0}属性威力を含む〈{1}〉の威力は×{2}倍", BonusName, TagName, Factor),
        new DataBlockSentencePattern("CategoryResistance", "自身は〈{0}〉からの被ダメージを×{1}倍する", TagName, Factor),
        new DataBlockSentencePattern("TaggedElementRoll", "自身の〈{0}〉による[{1}]による発動ロールは×{2}倍", TagName, BonusName, Factor),
        new DataBlockSentencePattern("TwoCategoryRoll", "〈{0}〉及び〈{1}〉による発動ロールの達成値が{2}される", TagName, TagName, Amount),
    };
    private static readonly Dictionary<string, DataBlockSentencePattern> ClausesByKey =
        RulePatterns.ToDictionary(rule => rule.Key, StringComparer.Ordinal);

    private static bool TryReadWeaponClauses(string text, List<EffectContent> effects)
    {
        string prefix = text.StartsWith(SelectedPrefix, StringComparison.Ordinal) ? SelectedPrefix :
            text.StartsWith("この武器の選択を条件にスキルを発動する時、", StringComparison.Ordinal) ? "この武器の選択を条件にスキルを発動する時、" : null;
        if (prefix == null) return false;
        string body = text.Substring(prefix.Length).Replace("含まないなら、", "含まないなら");
        body = RegexReplace(body, @"(?<!〉)かつ", "、");
        foreach (string part in Split(body.TrimEnd('。'), "、"))
        {
            string clause = part;
            var previous = effects.LastOrDefault();
            if (clause.StartsWith("達成値は", StringComparison.Ordinal) && previous?.Parameters[0] == "CategoryTarget")
                clause = "〈" + previous.Parameters[1] + "〉の" + clause;
            EffectContent effect;
            if (!TryReadRule(clause, EffectContentType.SelectedWeaponRule, out effect))
                return false;
            effects.Add(effect);
        }
        return effects.Count > 0;
    }

    internal static bool TryReadRule(string text, EffectContentType type, out EffectContent effect)
    {
        text = text.Trim().TrimEnd('。').Replace("攻撃対象への防御点", "攻撃対象の防御点")
            .Replace("自身与える", "自身の与");
        text = RegexReplace(text, @"^(?:自身の)?〈([^〉]+)〉(?:の発動ロール|による)の?目標値(?:が|は)([+-].+?)される$",
            "自身の〈$1〉による目標値は$2される");
        text = RegexReplace(text, @"^自身のあらゆる消費([A-Z]+)は([+-][0-9]+)される$", "$1消費$2");
        text = RegexReplace(text, @"(倍)(?:される|され)$", "$1");
        text = RegexReplace(text, @"(?<=〈)([^〉]+)〉(目標値|威力)", "$1〉の$2");
        text = RegexReplace(text, @"(達成値|目標値|被ダメージ軽減|消費[A-Z]+|\])(×)", "$1は$2");
        text = RegexReplace(text, @"^自身の([^\[〈《、]+?B)による", "自身の[$1]による");
        text = RegexReplace(text, @"^自身の〈([^〉]+)〉(?:の|による)(達成値|目標値)は×", "〈$1〉の$2は×");
        text = RegexReplace(text, @"^自身の〈([^〉]+)〉(?:の|による)被ダメージ軽減(?:効果|量)?(?:は)?×?", "自身の〈$1〉による被ダメージ軽減は×");
        text = RegexReplace(text, @"^〈([^〉]+)〉の威力は×", "効果対象への〈$1〉の威力は×");
        text = RegexReplace(text, @"^被ダメージ(?!×)", "被ダメージ×");
        text = RegexReplace(text, @"^自身の(〈[^〉]+〉及び〈)", "$1");
        foreach (var rule in RulePatterns)
        {
            string[] parameters = rule.Read(text);
            if (parameters == null) continue;
            effect = type == EffectContentType.RuleModifier && rule.Key == "StatResultFactor"
                ? Effect(EffectContentType.CheckByAbilityFactor, parameters)
                : Effect(type, new[] { rule.Key }.Concat(parameters).ToArray());
            return true;
        }
        effect = null;
        return false;
    }

    private static string RuleText(EffectContent effect)
    {
        DataBlockSentencePattern rule;
        if (effect.Parameters == null || effect.Parameters.Count == 0 ||
            !ClausesByKey.TryGetValue(Need(effect.Parameters[0], "装備効果の種類"), out rule))
            throw new InvalidOperationException("装備効果の種類が不正です。");
        string clause = rule.Write(effect.Parameters.Skip(1).ToList());
        return clause.EndsWith("倍", StringComparison.Ordinal) ? clause + "される" : clause;
    }
}
