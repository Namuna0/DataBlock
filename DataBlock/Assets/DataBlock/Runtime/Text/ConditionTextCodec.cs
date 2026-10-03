using System;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static void AddConditions(ConditionSet set, string text)
    {
        if (ReadCombinedReactionConditions(set, text)) return;
        if (ReadCheckReactionConditions(set, text)) return;
        text = RegexReplace(text, @"自身が《([^》]+)》を受けた時", "自身が〈$1〉を受けた時");
        text = text.Replace("を受けた時に宣言可能", "を受けた時宣言可能");
        if (ReadReactionCondition(set, text)) return;
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
        ConditionEntry excludedSelf = ReadExcludedSelfAndTraitCondition(text);
        if (excludedSelf != null) return excludedSelf;
        ConditionEntry stateSelection = ReadStateSelectionCondition(text);
        if (stateSelection != null) return stateSelection;
        ConditionEntry areaAutomatic = ReadAreaAndAutomaticCondition(text);
        if (areaAutomatic != null) return areaAutomatic;
        ConditionEntry equipmentReaction = ReadEquipmentAndReactionCondition(text);
        if (equipmentReaction != null) return equipmentReaction;
        ConditionEntry skillSelection = ReadSkillSelectionCondition(text);
        if (skillSelection != null) return skillSelection;
        if (text == "自身のアクティブ効果に対してカウンター効果を発動された時に") return Condition(ConditionType.CounterToOwnActive);
        ConditionEntry check = ReadCheckCondition(text);
        if (check != null) return check;
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
        m = M(text, @"^(自身と同じ接近グループの)?キャラクターを([0-9]+)体(まで)?選択(?:[（(](重複不可|重複可能)[）)])?$");
        if (m.Success) return Condition(m.Groups[1].Success ? ConditionType.SelectMeleeCharacters : ConditionType.SelectCharacters, m.Groups[2].Value, m.Groups[3].Success ? "UpTo" : "Exact", m.Groups[4].Success ? (m.Groups[4].Value == "重複不可" ? "Distinct" : "AllowDuplicates") : "Default");
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
        string excludedSelf = ExcludedSelfAndTraitConditionText(item);
        if (excludedSelf != null) return excludedSelf;
        string areaAutomatic = AreaAndAutomaticConditionText(item);
        if (areaAutomatic != null) return areaAutomatic;
        string equipmentReaction = EquipmentAndReactionConditionText(item);
        if (equipmentReaction != null) return equipmentReaction;
        string skillSelection = SkillSelectionConditionText(item);
        if (skillSelection != null) return skillSelection;
        if (item != null && item.Type == ConditionType.CounterToOwnActive) { Args(item.Parameters, 0, "CounterToOwnActive"); return "自身のアクティブ効果に対してカウンター効果を発動された時に"; }
        string check = CheckConditionText(item);
        if (check != null) return check;
        if (item != null && item.Type == ConditionType.ReactionTarget) return ReactionConditionText(item);
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
                if (p[2] != "Distinct" && p[2] != "Default" && p[2] != "AllowDuplicates") throw new InvalidOperationException("重複指定はDistinct / AllowDuplicates / Defaultです。");
                return (item.Type == ConditionType.SelectMeleeCharacters ? "自身と同じ接近グループの" : "") + "キャラクターを" + p[0] + "体" + (p[1] == "UpTo" ? "まで" : "") + "選択" + (p[2] == "Distinct" ? "（重複不可）" : p[2] == "AllowDuplicates" ? "（重複可能）" : "");
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

    private static bool ReadCheckReactionConditions(ConditionSet set, string text)
    {
        Match m = M(text.Trim(), @"^戦闘中、自身が《([^》]+)》状態の時に一度だけ、あらゆるキャラクターの行為判定に対して(?:（〈([^〉]+)〉を受けた場合回数リセット）)?宣言可能。?$");
        if (m.Success)
        {
            set.And.Add(Condition(ConditionType.IncapacitatedCheckReaction, m.Groups[1].Value, "Battle", "1", "AnyCharacterAction", m.Groups[2].Success ? m.Groups[2].Value : "Unspecified")); return true;
        }
        m = M(text.Trim(), @"^〈([^〉]+)〉を受けた場合回数はリセットされる。?$");
        if (m.Success)
        {
            var reaction = set.And.SingleOrDefault(x => x.Type == ConditionType.IncapacitatedCheckReaction);
            Require(reaction != null && reaction.Parameters[4] == "Unspecified"); reaction.Parameters[4] = m.Groups[1].Value; return true;
        }
        m = M(text.Trim(), @"^自身が〈([^〉]+)〉を受けた時、その対象へ発動。?$");
        if (m.Success) { set.And.Add(Condition(ConditionType.ReactionTarget, m.Groups[1].Value, "Self", "Source")); return true; }
        if (text.Trim() == "所持している武器を1つ選択して宣言可能。または素手を選択。")
        { set.And.Add(Condition(ConditionType.SelectCarriedWeapon, "1", "AllowUnarmed")); return true; }
        return false;
    }

    private static ConditionEntry ReadEquipmentAndReactionCondition(string text)
    {
        text = text.TrimEnd('。');
        if (text == "種族選択時") return Condition(ConditionType.AtRaceSelection);
        Match m = M(text, @"^自身が《([^》]+)》状態ではない時$");
        if (m.Success) return Condition(ConditionType.WithoutOwnState, m.Groups[1].Value);
        m = M(text, @"^1日([0-9]+)度のみ$");
        if (m.Success) return Condition(ConditionType.ActivationLimit, "Day", m.Groups[1].Value);
        m = M(text, @"^([A-Z]+)が([0-9]+)以下になる〈([^〉]+)〉を受けた時(?:に)?$");
        if (m.Success) return Condition(ConditionType.LethalIncomingAction, m.Groups[1].Value, "AtMost", m.Groups[2].Value, m.Groups[3].Value);
        m = M(text, @"^消費([A-Z]+)が([0-9]+)以下かつ接近状態を条件としない自身の装備スキルを([0-9]+)つ選択$");
        if (m.Success) return Condition(ConditionType.SelectEquipmentSkill, m.Groups[3].Value, m.Groups[1].Value, "AtMost", m.Groups[2].Value, "NoMeleeRequirement");
        m = M(text, @"^自身へ〈([^〉]+)〉を発動した対象に対して$");
        if (m.Success) return Condition(ConditionType.ActionTowardsSelf, m.Groups[1].Value, "Source");
        return null;
    }

    private static string EquipmentAndReactionConditionText(ConditionEntry condition)
    {
        if (condition == null) return null;
        string[] p;
        switch (condition.Type)
        {
            case ConditionType.WithoutOwnState:
                return "自身が《" + Args(condition.Parameters, 1, "WithoutOwnState")[0] + "》状態ではない時";
            case ConditionType.AtRaceSelection: Args(condition.Parameters, 0, "AtRaceSelection"); return "種族選択時";
            case ConditionType.IncapacitatedCheckReaction:
                p = Args(condition.Parameters, 5, "IncapacitatedCheckReaction"); Require(p[1] == "Battle" && p[2] == "1" && p[3] == "AnyCharacterAction" && p[4] != "Unspecified");
                return "戦闘中、自身が《" + p[0] + "》状態の時に一度だけ、あらゆるキャラクターの行為判定に対して（〈" + p[4] + "〉を受けた場合回数リセット）";
            case ConditionType.LethalIncomingAction:
                p = Args(condition.Parameters, 4, "LethalIncomingAction"); Require(p[1] == "AtMost"); Number(p[2], 0, "閾値");
                return p[0] + "が" + p[2] + "以下になる〈" + p[3] + "〉を受けた時に";
            case ConditionType.SelectEquipmentSkill:
                p = Args(condition.Parameters, 5, "SelectEquipmentSkill"); Number(p[0], 1, "選択数"); Number(p[3], 0, "消費上限"); Require(p[2] == "AtMost" && p[4] == "NoMeleeRequirement");
                return "消費" + p[1] + "が" + p[3] + "以下かつ接近状態を条件としない自身の装備スキルを" + p[0] + "つ選択";
            case ConditionType.ActionTowardsSelf:
                p = Args(condition.Parameters, 2, "ActionTowardsSelf"); Require(p[1] == "Source"); return "自身へ〈" + p[0] + "〉を発動した対象に対して";
            case ConditionType.SelectCarriedWeapon:
                p = Args(condition.Parameters, 2, "SelectCarriedWeapon"); Require(p[0] == "1" && p[1] == "AllowUnarmed"); return "所持している武器を1つ選択して宣言可能。または素手を選択。";
            default: return null;
        }
    }

    private static bool ReadCombinedReactionConditions(ConditionSet set, string text)
    {
        var m = M(text.Trim(), @"^自身が〈([^〉]+)状態〉かつ〈([^〉]+)〉を受けた時、その対象へ宣言可能。?$");
        if (m.Success)
        {
            set.And.Add(Condition(ConditionType.OwnState, m.Groups[1].Value));
            set.And.Add(Condition(ConditionType.ReactionTarget, m.Groups[2].Value, "Self", "Source"));
            return true;
        }
        // The canonical combined condition retains the reaction's comma.
        m = M(text.Trim(), @"^自身が《([^》]+)》状態の時[,、]\s*(自身が〈[^〉]+〉を受けた時、その対象へ(?:宣言可能。)?)$");
        if (m.Success)
        {
            set.And.Add(Condition(ConditionType.OwnState, m.Groups[1].Value));
            return ReadReactionCondition(set, m.Groups[2].Value);
        }
        return false;
    }

    private static ConditionEntry ReadAreaAndAutomaticCondition(string text)
    {
        var own = M(text.TrimEnd('。'), @"^自身が《([^》]+)》状態の時$");
        if (own.Success) return Condition(ConditionType.OwnState, own.Groups[1].Value);
        var m = M(text.TrimEnd('。'), @"^エリアに〈([^〉]+)〉が(?:含まれない事|含まれていない時)$");
        if (m.Success) return Condition(ConditionType.AreaWithoutCategory, m.Groups[1].Value);
        m = M(text.TrimEnd('。'), @"^《([^》]+)》状態のキャラクターを([0-9]+)体選択$");
        if (m.Success) return Condition(ConditionType.SelectCharactersWithState, m.Groups[1].Value, m.Groups[2].Value);
        m = M(text.TrimEnd('。'), @"^〈([^〉]+)〉を発動した敵キャラクターを対象に自動発動$");
        if (m.Success) return Condition(ConditionType.AutomaticEnemyAction, m.Groups[1].Value);
        return null;
    }

    private static string AreaAndAutomaticConditionText(ConditionEntry item)
    {
        if (item == null) return null;
        switch (item.Type)
        {
            case ConditionType.OwnStateApplied: return "自身が《" + Args(item.Parameters, 1, "OwnStateApplied")[0] + "》状態になった時";
            case ConditionType.OwnState: return "自身が《" + Args(item.Parameters, 1, "OwnState")[0] + "》状態の時";
            case ConditionType.AreaWithoutCategory: return "エリアに〈" + Args(item.Parameters, 1, "AreaWithoutCategory")[0] + "〉が含まれない事";
            case ConditionType.SelectCharactersWithState:
                var p = VariableArgs(item.Parameters, 2, "SelectCharactersWithState"); Number(p[1], 1, "人数");
                Require(p.Length == 2 || (p.Length == 3 && (p[2] == "Exact" || p[2] == "UpTo")));
                return "《" + p[0] + "》状態のキャラクターを" + p[1] + "体" + (p.Length == 3 && p[2] == "UpTo" ? "まで" : "") + "選択";
            case ConditionType.AutomaticEnemyAction: return "〈" + Args(item.Parameters, 1, "AutomaticEnemyAction")[0] + "〉を発動した敵キャラクターを対象に自動発動";
            default: return null;
        }
    }

    private static bool ReadReactionCondition(ConditionSet set, string text)
    {
        Match m = M(text, @"^自身が[《〈]([^》〉]+)[》〉]を受けた時、その対象へ(?:宣言可能。)?$");
        if (m.Success) { set.And.Add(Condition(ConditionType.ReactionTarget, m.Groups[1].Value, "Self", "Source")); return true; }
        m = M(text, @"^〈([^〉]+)〉の対象になった味方キャラクターを対象に自動発動。?$");
        if (m.Success) { set.And.Add(Condition(ConditionType.ReactionTarget, m.Groups[1].Value, "Ally", "Receiver", "Automatic")); return true; }
        return false;
    }

    private static string ReactionConditionText(ConditionEntry item)
    {
        string[] p = VariableArgs(item.Parameters, 3, "ReactionTarget");
        if (p.SequenceEqual(new[] { p[0], "Self", "Source" })) return "自身が〈" + p[0] + "〉を受けた時、その対象へ";
        if (p.SequenceEqual(new[] { p[0], "Ally", "Receiver", "Automatic" })) return "〈" + p[0] + "〉の対象になった味方キャラクターを対象に自動発動";
        throw new InvalidOperationException("反応対象の指定が不正です。");
    }

    private static ConditionEntry ReadExcludedSelfAndTraitCondition(string text)
    {
        var m = M(text, @"^自身を除くキャラクターを([0-9]+)体選択$");
        if (m.Success) return Condition(ConditionType.SelectCharactersExceptSelf, m.Groups[1].Value);
        m = M(text, @"^特性《([^》]+)》を習得している(.+?)キャラクターを([0-9]+)体選択$");
        if (m.Success) return Condition(ConditionType.SelectRaceWithTrait, m.Groups[2].Value, m.Groups[1].Value, m.Groups[3].Value);
        return null;
    }

    private static string ExcludedSelfAndTraitConditionText(ConditionEntry item)
    {
        if (item == null) return null;
        if (item.Type == ConditionType.SelectCharactersExceptSelf)
            return "自身を除くキャラクターを" + Number(Args(item.Parameters, 1, "SelectCharactersExceptSelf")[0], 1, "人数") + "体選択";
        if (item.Type == ConditionType.SelectRaceWithTrait)
        {
            var p = Args(item.Parameters, 3, "SelectRaceWithTrait"); Number(p[2], 1, "人数");
            return "特性《" + p[1] + "》を習得している" + p[0] + "キャラクターを" + p[2] + "体選択";
        }
        return null;
    }

    private static ConditionEntry ReadCheckCondition(string text)
    {
        Match m = M(text, @"^戦闘中([0-9]+)(?:度だけ|回まで)$");
        if (m.Success) return Condition(ConditionType.ActivationLimit, "Battle", m.Groups[1].Value);
        if (text == "発動ロールに通常失敗した時に") return Condition(ConditionType.RollResult, "Activation", "NormalFailure");
        m = M(text, @"^([A-Z]+)が(.+?)以下になった時に$");
        if (m.Success) return Condition(ConditionType.ResourceValue, "Self", m.Groups[1].Value, "AtMost", m.Groups[2].Value);
        m = M(text, @"^自身に〈([^〉]+)〉状態が付与されている時[、,]?$" );
        if (m.Success) return Condition(ConditionType.OwnStateCategories, new[] { "Any" }.Concat(Split(m.Groups[1].Value, ",")).ToArray());
        return null;
    }

    private static string CheckConditionText(ConditionEntry condition)
    {
        if (condition == null) return null;
        string[] p;
        if (condition.Type == ConditionType.ActivationLimit && condition.Parameters != null && condition.Parameters.Count == 2 && condition.Parameters[0] == "Battle")
            return "戦闘中" + Number(condition.Parameters[1], 1, "発動回数") + "回まで";
        if (condition.Type == ConditionType.RollResult)
        {
            p = Args(condition.Parameters, 2, "RollResult"); Require(p.SequenceEqual(new[] { "Activation", "NormalFailure" }));
            return "発動ロールに通常失敗した時に";
        }
        if (condition.Type == ConditionType.ResourceValue)
        {
            p = Args(condition.Parameters, 4, "ResourceValue"); Require(p[0] == "Self" && p[2] == "AtMost");
            return p[1] + "が" + p[3] + "以下になった時に";
        }
        if (condition.Type == ConditionType.OwnStateCategories)
        {
            p = VariableArgs(condition.Parameters, 2, "OwnStateCategories"); Require(p[0] == "Any");
            return "自身に〈" + string.Join(", ", p.Skip(1)) + "〉状態が付与されている時";
        }
        return null;
    }

    private static ConditionEntry ReadStateSelectionCondition(string text)
    {
        var m = M(text.TrimEnd('。'), @"^自身が《([^》]+)》状態になった時$");
        if (m.Success) return Condition(ConditionType.OwnStateApplied, m.Groups[1].Value);
        m = M(text.TrimEnd('。'), @"^《([^》]+)》状態(?:の)?キャラクターを([0-9]+)体まで選択$");
        if (m.Success) return Condition(ConditionType.SelectCharactersWithState, m.Groups[1].Value, m.Groups[2].Value, "UpTo");
        return null;
    }

    private static ConditionEntry ReadSkillSelectionCondition(string text)
    {
        text = text.TrimEnd('。');
        if (text == "エンカウントフェーズ") return Condition(ConditionType.EncounterPhase);
        Match m = M(text, @"^キャラクター([0-9]+)体のアクティブ効果を持つスキルを([0-9]+)つ選択$");
        if (m.Success) return Condition(ConditionType.SelectCharacterSkill, m.Groups[1].Value, "Active", m.Groups[2].Value);
        m = M(text, @"^任意の対象を([0-9]+)体指定(?:して)?$");
        if (m.Success) return Condition(ConditionType.SelectAnyTarget, m.Groups[1].Value);
        m = M(text, @"^((?:《[^》]+》)(?:及び《[^》]+》)*)を除くキャラクターを([0-9]+)体選択$");
        if (m.Success) return Condition(ConditionType.ExcludeTargetRaces, new[] { m.Groups[2].Value }.Concat(AllMatches(m.Groups[1].Value, @"《([^》]+)》").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray());
        m = M(text, @"^《([^》]+)》スタックが([0-9]+)以上のキャラクターを([0-9]+)体選択$");
        if (m.Success) return Condition(ConditionType.TargetStackMinimum, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
        return null;
    }

    private static string SkillSelectionConditionText(ConditionEntry item)
    {
        if (item == null) return null;
        string[] p;
        switch (item.Type)
        {
            case ConditionType.EncounterPhase: Args(item.Parameters, 0, "EncounterPhase"); return "エンカウントフェーズ";
            case ConditionType.SelectCharacterSkill:
                p = Args(item.Parameters, 3, "SelectCharacterSkill"); Number(p[0], 1, "人数"); Number(p[2], 1, "スキル数"); Require(p[1] == "Active");
                return "キャラクター" + p[0] + "体のアクティブ効果を持つスキルを" + p[2] + "つ選択";
            case ConditionType.SelectAnyTarget:
                p = Args(item.Parameters, 1, "SelectAnyTarget"); Number(p[0], 1, "対象数"); return "任意の対象を" + p[0] + "体指定して";
            case ConditionType.ExcludeTargetRaces:
                p = VariableArgs(item.Parameters, 2, "ExcludeTargetRaces"); Number(p[0], 1, "人数");
                return string.Join("及び", p.Skip(1).Select(x => "《" + x + "》")) + "を除くキャラクターを" + p[0] + "体選択";
            case ConditionType.TargetStackMinimum:
                p = Args(item.Parameters, 3, "TargetStackMinimum"); Number(p[1], 1, "スタック下限"); Number(p[2], 1, "人数");
                return "《" + p[0] + "》スタックが" + p[1] + "以上のキャラクターを" + p[2] + "体選択";
            default: return null;
        }
    }

}
