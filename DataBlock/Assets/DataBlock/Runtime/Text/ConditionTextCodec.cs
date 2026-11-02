using System;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static void AddConditions(ConditionSet set, string text)
    {
        string original = text.Trim();
        Match learnedSkillAlternatives = M(original,
            @"^(.+?)[,、]\s*((?:《[^》]+》\s*[,、]\s*)+《[^》]+》)\s*の(?:(?:内|うち|中から)?)(?:いずれか|どれか)(?:1つ)?を習得。?$");
        if (learnedSkillAlternatives.Success)
        {
            AddConditions(set, learnedSkillAlternatives.Groups[1].Value.Trim());
            if (set.Or.Count != 0) throw new InvalidOperationException("習得スキルのいずれか条件は、既存のOR条件と同時に指定できません。");
            string[] skillNames = AllMatches(learnedSkillAlternatives.Groups[2].Value, @"《([^》]+)》")
                .Cast<Match>().Select(x => Need(x.Groups[1].Value, "前提スキル名")).ToArray();
            if (skillNames.Length < 2) throw new InvalidOperationException("いずれかの習得条件には2つ以上の候補スキルが必要です。");
            foreach (string skillName in skillNames) set.Or.Add(Condition(ConditionType.HasLearnedSkill, skillName));
            return;
        }
        if (IsKnownRuleTextCondition(original))
        {
            set.And.Add(Condition(ConditionType.RuleText, original));
            return;
        }
        text = text.Trim().Replace("指定して宣言可能。", "指定").Replace("選択して宣言可能。", "選択").Replace("宣言可能。", "");
        text = text.Trim().TrimEnd('、', ',');
        if (text.TrimEnd('。') == "毎ターン開始時、ランダムな敵キャラクターを対象に自動発動")
        {
            set.And.Add(Condition(ConditionType.AutomaticActivation, "EveryTurn", "Start", "Random", "Enemy", "1"));
            return;
        }
        if (text.TrimEnd('。') == "自身のターン開始時ランダムな敵キャラクターを対象に自動発動")
        {
            set.And.Add(Condition(ConditionType.AutomaticActivation, "SelfTurn", "Start", "Random", "Enemy", "1"));
            return;
        }
        var parts = Split(text, ",");
        bool meleeException = M(text, @"^自身が《([^》]+)》状態なら[、,]\s*接近は不要。?$").Success;
        if (parts.Count == 1 && text.IndexOf(',') < 0 && !meleeException) parts = Split(text, "、");
        foreach (string part in parts)
        {
            Match exception = M(part, @"^自身が《([^》]+)》状態なら[、,]\s*接近は不要。?$");
            if (exception.Success)
            {
                ConditionEntry selection = set.And.LastOrDefault(x => x.Type == ConditionType.SelectMeleeCharacters);
                if (selection == null || set.And.Count(x => x.Type == ConditionType.SelectMeleeCharacters) != 1 || set.Or.Count != 0)
                    throw new InvalidOperationException("接近不要の例外には、直前までに同じ接近グループからの対象選択を1件指定してください。");
                selection.Type = ConditionType.SelectCharacters;
                set.Or.Add(Condition(ConditionType.RequiresSameMeleeGroup));
                set.Or.Add(Condition(ConditionType.OwnState, exception.Groups[1].Value));
                continue;
            }
            if (part.StartsWith("（") && part.EndsWith("）"))
                foreach (string p in Split(part.Substring(1, part.Length - 2), " または ")) set.Or.Add(ReadCondition(p));
            else if (IsKnownRuleTextCondition(part)) set.And.Add(Condition(ConditionType.RuleText, part));
            else
            {
                Match equippedAlternatives = M(part, @"^((?:〈[^〉]+〉)(?:または〈[^〉]+〉)+)を装備$");
                if (equippedAlternatives.Success)
                {
                    if (set.Or.Count != 0) throw new InvalidOperationException("複数のOR条件グループは同時に指定できません。");
                    string[] categories = AllMatches(equippedAlternatives.Groups[1].Value, @"〈([^〉]+)〉")
                        .Cast<Match>().Select(x => Need(x.Groups[1].Value, "装備カテゴリー")).ToArray();
                    if (categories.Length < 2 || categories.Distinct(StringComparer.Ordinal).Count() != categories.Length)
                        throw new InvalidOperationException("装備カテゴリーの候補を2つ以上、重複なく指定してください。");
                    foreach (string category in categories) set.Or.Add(Condition(ConditionType.EquippedCategory, "Self", category));
                }
                else set.And.Add(ReadCondition(part));
            }
        }
    }
    private static bool IsKnownRuleTextCondition(string text)
    {
        if (text == "エンカウントフェーズ" || text == "スタンバイフェーズ時" ||
            text == "〈隠密〉を発動したキャラクターに対して宣言可能。" ||
            text == "所持している武器を1つ選択して宣言可能。または素手を選択。" ||
            text == "〈移動〉を宣言した〈小型〉でも〈大型〉でもない敵キャラクターを対象に自動発動。" ||
            M(text, @"^《[^》]+》クラス記載$").Success ||
            text == "自身の宣言したスキルの消費SPが現在のSPを上回っている時に" ||
            text == "SANが0以下になった時に")
            return true;
        if (M(text, @"^自身が《[^》]+》状態$").Success) return true;
        if (M(text, @"^スタンバイフェーズ時[、,]\s*〈森〉エリアで宣言可能。$").Success) return true;
        if (M(text, @"^自身と同じ接近グループの〈大型〉ではないキャラクターを1体選択して宣言可能。?$").Success ||
            M(text, @"^自身と同じ接近グループの《戦闘不能》状態のキャラクターが〈蘇生〉の対象になった時。?$").Success)
            return true;
        return M(text, @"^味方キャラクターを[0-9]+体まで選択して宣言可能。(?:（重複不可）)?このスキルは戦闘中パーティー全体で1回のみ使用できる。$").Success;
    }
    private static ConditionEntry ReadCondition(string text)
    {
        text = text.Trim();
        if (text == "片手が素手") return Condition(ConditionType.HandUnarmed);
        if (text == "両手が素手") return Condition(ConditionType.HandUnarmed, "Both");
        if (text == "種族選択時に任意選択") return Condition(ConditionType.OptionalAtRaceSelection);
        if (text == "種族選択時に自動習得") return Condition(ConditionType.AutomaticAtRaceSelection);
        Match lifePath = M(text, @"^ライフパス《([^》]+)》を習得。?$");
        if (lifePath.Success) return Condition(ConditionType.HasLearnedLifePath, Need(lifePath.Groups[1].Value, "ライフパス名"));
        Match learnedSkill = M(text, @"^《([^》]+)》(?:を?習得済?)。?$");
        if (learnedSkill.Success) return Condition(ConditionType.HasLearnedSkill, Need(learnedSkill.Groups[1].Value, "前提スキル名"));
        Match race = M(text, @"^自身の種族が〈([^〉]+)〉である(?:こと|事)$");
        if (race.Success) return Condition(ConditionType.SelfRace, "Is", Need(race.Groups[1].Value, "種族名"));
        race = M(text, @"^自身が〈([^〉]+)〉である(?:こと|事)$");
        if (race.Success) return Condition(ConditionType.SelfRace, "Is", Need(race.Groups[1].Value, "種族名"));
        race = M(text, @"^自身の種族が《([^》]+)》の(?:内|うち)?いずれかである(?:こと|事)$");
        if (race.Success)
        {
            string[] races = Split(race.Groups[1].Value, ",").Select(x => Need(x, "種族名")).ToArray();
            if (races.Length < 2 || races.Distinct(StringComparer.Ordinal).Count() != races.Length) throw new InvalidOperationException("種族の候補を2つ以上、重複なく指定してください。");
            return Condition(ConditionType.SelfRace, new[] { "IsAnyOf" }.Concat(races).ToArray());
        }
        race = M(text, @"^《([^》]+)》ではない(?:こと|事)$");
        if (race.Success) return Condition(ConditionType.SelfRace, "IsNot", Need(race.Groups[1].Value, "種族名"));
        Match m = M(text, @"^装備から([0-9]+)日以上経過している事$");
        if (m.Success) return Condition(ConditionType.MinimumEquippedDays, Number(m.Groups[1].Value, 1, "装備経過日数").ToString());
        m = M(text, @"^(決意|[A-Z]+)([0-9]+)消費$");
        if (m.Success) return Condition(ConditionType.SpendResource, m.Groups[1].Value, m.Groups[2].Value);
        m = M(text, @"^(.+?)属性([0-9]+)以上$");
        if (m.Success) return Condition(ConditionType.MinimumStat, m.Groups[1].Value + "属性", m.Groups[2].Value);
        m = M(text, @"^1ターンに([0-9]+)回まで$");
        if (m.Success) return Condition(ConditionType.ActivationLimit, "Turn", m.Groups[1].Value);
        m = M(text, @"^1日([0-9]+)回まで$");
        if (m.Success) return Condition(ConditionType.ActivationLimit, "Day", Number(m.Groups[1].Value, 1, "1日の発動回数").ToString());
        m = M(text, @"^戦闘中([0-9]+)度だけ$");
        if (m.Success) return Condition(ConditionType.ActivationLimit, "Battle", Number(m.Groups[1].Value, 1, "戦闘中の発動回数").ToString());
        if (text == "敵または味方のターンの終了時に") return Condition(ConditionType.DeclarationTiming, "EnemyOrAlly", "End");
        m = M(text, @"^装備している((?:〈[^〉]+〉)(?:または〈[^〉]+〉)+)を([0-9]+)つ選択$");
        if (m.Success) return Condition(ConditionType.SelectEquippedWeaponFromCategories, new[] { m.Groups[2].Value }.Concat(AllMatches(m.Groups[1].Value, @"〈([^〉]+)〉").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray());
        m = M(text, @"^装備している〈([^〉]+)〉を([0-9]+)つ選択$");
        if (m.Success) return Condition(ConditionType.SelectEquippedWeapon, m.Groups[1].Value, m.Groups[2].Value);
        m = M(text, @"^〈([^〉]+)〉を装備$");
        if (m.Success) return Condition(ConditionType.EquippedCategory, "Self", m.Groups[1].Value);
        m = M(text, @"^消費する〈([^〉]+)〉を([0-9]+)個選択$");
        if (m.Success) return Condition(ConditionType.SelectConsumedItem, m.Groups[1].Value, m.Groups[2].Value);
        if (text.TrimEnd('。') == "そのアイテムの宣言条件を満たしていること") return Condition(ConditionType.SelectedItemConditionsMet);
        m = M(text, @"^(自身と同じ接近グループの)?キャラクターを([0-9]+)体(まで)?選択(?:[（(](重複不可)[）)])?$");
        if (m.Success) return Condition(m.Groups[1].Success ? ConditionType.SelectMeleeCharacters : ConditionType.SelectCharacters, m.Groups[2].Value, m.Groups[3].Success ? "UpTo" : "Exact", m.Groups[4].Success ? "Distinct" : "Default");
        m = M(text, @"^(?:任意の対象|任意のキャラクター)を([0-9]+)体指定$");
        if (m.Success) return Condition(ConditionType.SelectCharacters, m.Groups[1].Value, "Exact", "Default");
        m = M(text, @"^自身が既に受けている〈([^〉]+)〉状態を([0-9]+)つ選択$");
        if (m.Success) return Condition(ConditionType.SelectOwnState, new[] { m.Groups[2].Value }.Concat(Split(m.Groups[1].Value, ",")).ToArray());
        m = M(text, @"^〈([^〉]+)〉を発動した自身と同じ接近グループのキャラクターに対して$");
        if (m.Success) return Condition(ConditionType.ActionTarget, "Source", m.Groups[1].Value, "SameMeleeGroup");
        m = M(text, @"^〈([^〉]+)〉を受けた対象に対して$");
        if (m.Success) return Condition(ConditionType.ActionTarget, "Receiver", m.Groups[1].Value, "Any");
        m = M(text, @"^自身が〈([^〉]+)〉を受けた時$");
        if (m.Success) return Condition(ConditionType.ActionTarget, "Receiver", m.Groups[1].Value, "Self");
        m = M(text, @"^自身と(同じ接近グループの|接近していない)キャラクターから((?:〈[^〉]+〉)(?:または〈[^〉]+〉)*)を受けた時$");
        if (m.Success) return Condition(ConditionType.ReceiveFromCharacter, new[] { m.Groups[1].Value == "同じ接近グループの" ? "SameMeleeGroup" : "NotEngaged" }.Concat(AllMatches(m.Groups[2].Value, @"〈([^〉]+)〉").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray());
        // Unknown condition clauses remain available in JSON and round-trip through the writer.
        return Condition(ConditionType.RuleText, Need(text, "条件ルール"));
    }
    private static string ConditionText(ConditionEntry item)
    {
        if (item == null) throw new InvalidOperationException("条件がnullです。");
        string[] p;
        switch (item.Type)
        {
            case ConditionType.OptionalAtRaceSelection: Args(item.Parameters, 0, "OptionalAtRaceSelection"); return "種族選択時に任意選択";
            case ConditionType.AutomaticAtRaceSelection: Args(item.Parameters, 0, "AutomaticAtRaceSelection"); return "種族選択時に自動習得";
            case ConditionType.HandUnarmed:
                if (item.Parameters == null || item.Parameters.Count == 0) return "片手が素手";
                p = Args(item.Parameters, 1, "HandUnarmed");
                if (p[0] == "Both") return "両手が素手";
                throw new InvalidOperationException("HandUnarmedはパラメーターなし（片手）またはBoth（両手）です。");
            case ConditionType.RuleText: return Need(Args(item.Parameters, 1, "RuleText")[0], "条件ルール");
            case ConditionType.HasLearnedSkill: return "《" + Need(Args(item.Parameters, 1, "HasLearnedSkill")[0], "前提スキル名") + "》を習得";
            case ConditionType.HasLearnedLifePath: return "ライフパス《" + Need(Args(item.Parameters, 1, "HasLearnedLifePath")[0], "ライフパス名") + "》を習得";
            case ConditionType.SelfRace:
                p = VariableArgs(item.Parameters, 2, "SelfRace");
                if (p[0] == "Is" && p.Length == 2) return "自身の種族が〈" + Need(p[1], "種族名") + "〉であること";
                if (p[0] == "IsNot" && p.Length == 2) return "《" + Need(p[1], "種族名") + "》ではないこと";
                if (p[0] == "IsAnyOf" && p.Length >= 3 && p.Skip(1).Distinct(StringComparer.Ordinal).Count() == p.Length - 1)
                    return "自身の種族が《" + string.Join(", ", p.Skip(1).Select(x => Need(x, "種族名"))) + "》のいずれかであること";
                throw new InvalidOperationException("SelfRaceの演算子はIs / IsNot / IsAnyOfです。");
            case ConditionType.MinimumEquippedDays:
                p = Args(item.Parameters, 1, "MinimumEquippedDays");
                return "装備から" + Number(p[0], 1, "装備経過日数") + "日以上経過している事";
            case ConditionType.SpendResource: p = Args(item.Parameters, 2, "SpendResource"); Number(p[1], 0, "消費量"); return p[0] + p[1] + "消費";
            case ConditionType.MinimumStat:
                p = Args(item.Parameters, 2, "MinimumStat"); Number(p[1], 0, "必要値"); return p[0] + p[1] + "以上";
            case ConditionType.ActivationLimit:
                p = Args(item.Parameters, 2, "ActivationLimit"); Number(p[1], 1, "回数");
                if (p[0] == "Turn") return "1ターンに" + Number(p[1], 1, "回数") + "回まで";
                if (p[0] == "Day") return "1日" + Number(p[1], 1, "回数") + "回まで";
                if (p[0] == "Battle") return "戦闘中" + Number(p[1], 1, "回数") + "度だけ";
                throw new InvalidOperationException("ActivationLimitの単位はTurn / Day / Battleです。");
            case ConditionType.DeclarationTiming:
                p = Args(item.Parameters, 2, "DeclarationTiming");
                if (p[0] != "EnemyOrAlly" || p[1] != "End") throw new InvalidOperationException("今回の宣言タイミングはEnemyOrAlly / Endです。");
                return "敵または味方のターンの終了時に";
            case ConditionType.AutomaticActivation:
                p = Args(item.Parameters, 5, "AutomaticActivation"); Number(p[4], 1, "対象数");
                if (p[0] == "SelfTurn" && p.Skip(1).SequenceEqual(new[] { "Start", "Random", "Enemy", "1" })) return "自身のターン開始時ランダムな敵キャラクターを対象に自動発動。";
                if (!p.SequenceEqual(new[] { "EveryTurn", "Start", "Random", "Enemy", "1" })) throw new InvalidOperationException("今回の自動発動はターン開始時のランダムな敵1体です。");
                return "毎ターン開始時、ランダムな敵キャラクターを対象に自動発動。";
            case ConditionType.SelectEquippedWeapon: p = Args(item.Parameters, 2, "SelectEquippedWeapon"); Number(p[1], 1, "個数"); return "装備している〈" + p[0] + "〉を" + p[1] + "つ選択";
            case ConditionType.SelectEquippedWeaponFromCategories:
                p = VariableArgs(item.Parameters, 3, "SelectEquippedWeaponFromCategories"); Number(p[0], 1, "個数");
                if (p.Skip(1).Distinct(StringComparer.Ordinal).Count() != p.Length - 1) throw new InvalidOperationException("装備候補カテゴリーが重複しています。");
                return "装備している" + CategoryAlternatives(p.Skip(1), "または") + "を" + p[0] + "つ選択";
            case ConditionType.EquippedCategory:
                p = Args(item.Parameters, 2, "EquippedCategory");
                if (p[0] != "Self") throw new InvalidOperationException("宣言条件の装備者はSelfです。");
                return "〈" + p[1] + "〉を装備";
            case ConditionType.SelectConsumedItem: p = Args(item.Parameters, 2, "SelectConsumedItem"); Number(p[1], 1, "個数"); return "消費する〈" + p[0] + "〉を" + p[1] + "個選択";
            case ConditionType.SelectedItemConditionsMet: Args(item.Parameters, 0, "SelectedItemConditionsMet"); return "そのアイテムの宣言条件を満たしていること。";
            case ConditionType.SelectCharacters:
            case ConditionType.SelectMeleeCharacters:
                p = Args(item.Parameters, 3, item.Type.ToString()); Number(p[0], 1, "人数");
                if (p[1] != "Exact" && p[1] != "UpTo") throw new InvalidOperationException("選択数はExact / UpToです。");
                if (p[2] != "Distinct" && p[2] != "Default") throw new InvalidOperationException("重複指定はDistinct / Defaultです。");
                return (item.Type == ConditionType.SelectMeleeCharacters ? "自身と同じ接近グループの" : "") + "キャラクターを" + p[0] + "体" + (p[1] == "UpTo" ? "まで" : "") + "選択" + (p[2] == "Distinct" ? "（重複不可）" : "");
            case ConditionType.SelectOwnState:
                p = VariableArgs(item.Parameters, 2, "SelectOwnState"); Number(p[0], 1, "個数"); return "自身が既に受けている〈" + string.Join(", ", p.Skip(1)) + "〉状態を" + p[0] + "つ選択";
            case ConditionType.ReceiveFromCharacter:
                p = VariableArgs(item.Parameters, 2, "ReceiveFromCharacter");
                if (p[0] != "SameMeleeGroup" && p[0] != "NotEngaged") throw new InvalidOperationException("攻撃元との関係はSameMeleeGroup / NotEngagedです。");
                return "自身と" + (p[0] == "SameMeleeGroup" ? "同じ接近グループの" : "接近していない") + "キャラクターから" + CategoryAlternatives(p.Skip(1), "または") + "を受けた時";
            case ConditionType.ActionTarget:
                p = Args(item.Parameters, 3, "ActionTarget");
                if (p[0] == "Source" && p[2] == "SameMeleeGroup") return "〈" + p[1] + "〉を発動した自身と同じ接近グループのキャラクターに対して";
                if (p[0] == "Receiver" && p[2] == "Any") return "〈" + p[1] + "〉を受けた対象に対して";
                if (p[0] == "Receiver" && p[2] == "Self") return "自身が〈" + p[1] + "〉を受けた時";
                throw new InvalidOperationException("ActionTargetはSource/SameMeleeGroup、Receiver/Any、Receiver/Selfに対応します。");
            default: throw new InvalidOperationException("習得・宣言条件には対応していない条件種別です：" + item.Type);
        }
    }

    private static bool TryMeleeStateException(ConditionSet set, out ConditionEntry selection, out string stateName)
    {
        selection = null;
        stateName = null;
        if (set == null || set.And == null || set.Or == null || set.Or.Count != 2) return false;
        ConditionEntry relation = set.Or.SingleOrDefault(x => x != null && x.Type == ConditionType.RequiresSameMeleeGroup);
        ConditionEntry state = set.Or.SingleOrDefault(x => x != null && x.Type == ConditionType.OwnState);
        if (relation == null || state == null || relation.Parameters == null || relation.Parameters.Count != 0 || set.And.Count(x => x != null && x.Type == ConditionType.SelectCharacters) != 1) return false;
        selection = set.And.Single(x => x.Type == ConditionType.SelectCharacters);
        stateName = Args(state.Parameters, 1, "OwnState")[0];
        return true;
    }
}
