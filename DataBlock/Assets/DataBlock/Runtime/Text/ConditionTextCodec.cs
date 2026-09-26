using System;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static void AddConditions(ConditionSet set, string text)
    {
        text = text.Trim().Replace("選択して宣言可能。", "選択").Replace("宣言可能。", "");
        if (text.TrimEnd('。') == "毎ターン開始時、ランダムな敵キャラクターを対象に自動発動")
        {
            set.And.Add(Condition(ConditionType.AutomaticActivation, "EveryTurn", "Start", "Random", "Enemy", "1"));
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
            else set.And.Add(ReadCondition(part));
        }
    }
    private static ConditionEntry ReadCondition(string text)
    {
        text = text.Trim();
        if (text == "種族選択時に任意選択") return Condition(ConditionType.OptionalAtRaceSelection);
        if (text == "種族選択時に自動習得") return Condition(ConditionType.AutomaticAtRaceSelection);
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
        throw new InvalidOperationException("未対応の条件です：" + text);
    }
    private static string ConditionText(ConditionEntry item)
    {
        if (item == null) throw new InvalidOperationException("条件がnullです。");
        string[] p;
        switch (item.Type)
        {
            case ConditionType.OptionalAtRaceSelection: Args(item.Parameters, 0, "OptionalAtRaceSelection"); return "種族選択時に任意選択";
            case ConditionType.AutomaticAtRaceSelection: Args(item.Parameters, 0, "AutomaticAtRaceSelection"); return "種族選択時に自動習得";
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
                throw new InvalidOperationException("ActivationLimitの単位はTurn / Dayです。");
            case ConditionType.DeclarationTiming:
                p = Args(item.Parameters, 2, "DeclarationTiming");
                if (p[0] != "EnemyOrAlly" || p[1] != "End") throw new InvalidOperationException("今回の宣言タイミングはEnemyOrAlly / Endです。");
                return "敵または味方のターンの終了時に";
            case ConditionType.AutomaticActivation:
                p = Args(item.Parameters, 5, "AutomaticActivation"); Number(p[4], 1, "対象数");
                if (!p.SequenceEqual(new[] { "EveryTurn", "Start", "Random", "Enemy", "1" })) throw new InvalidOperationException("今回の自動発動は毎ターン開始時のランダムな敵1体です。");
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
