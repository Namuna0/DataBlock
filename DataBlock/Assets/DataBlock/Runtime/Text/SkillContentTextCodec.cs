using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static string EquipmentAndItemContentText(EffectContent content)
    {
        if (content == null) return null;
        string[] p;
        switch (content.Type)
        {
            case EffectContentType.EnvironmentImmunity:
                p = VariableArgs(content.Parameters, 2, "EnvironmentImmunity"); Require(p[0] == "Self");
                return "環境" + string.Join("および", p.Skip(1).Select(x => "《" + x + "》")) + "による環境効果を受けない。";
            case EffectContentType.RestoreOnAppliedState:
                p = Args(content.Parameters, 7, "RestoreOnAppliedState"); Require(p[0] == "Self" && p[2] == "Target" && p[5] == "MaximumPercent"); Number(p[6], 1, "回復率");
                return "自身の〈" + p[1] + "〉によって相手を《" + p[3] + "》状態にした時、" + p[4] + "を" + p[4] + "最大値の" + p[6] + "%回復する。";
            case EffectContentType.ChooseCheckOutcome:
                Require(Args(content.Parameters, 4, "ChooseCheckOutcome").SequenceEqual(new[] { "Target", "Critical", "Fumble", "Optional" }));
                return "対象の行為判定をクリティカル、もしくはファンブルに変える事が出来る。";
            case EffectContentType.GrantItem:
                p = Args(content.Parameters, 4, "GrantItem"); Require(p[0] == "Self" && p[3] != "Unspecified"); Number(p[2], 1, "個数");
                return "《" + p[1] + "》×" + p[2] + "を獲得。\nランクは☆×" + p[3] + "となる。";
            case EffectContentType.RemoveStatesByOrigin:
                p = Args(content.Parameters, 3, "RemoveStatesByOrigin"); Require(p[0] == "Self" && p[1] == "Roll"); return p[2] + "ロールによる状態を解除する。";
            case EffectContentType.DeclareSelectedSkill:
                p = Args(content.Parameters, 6, "DeclareSelectedSkill"); Require(p.Take(5).SequenceEqual(new[] { "Self", "Target", "ACT", "0", "Optional" })); PositiveModifierFactor(p[5]);
                return "対象へ選択したスキルをACT消費無しで宣言してもよい。\nそのスキルで与える威力は×" + p[5] + "倍される。";
            case EffectContentType.SwapWeapon:
                Require(Args(content.Parameters, 4, "SwapWeapon").SequenceEqual(new[] { "Self", "SelectedWeapon", "Battle", "AllowUnarmed" }));
                return "戦闘中、装備している武器と対象武器を入れ替える。";
            default: return null;
        }
    }

    private static EffectContent ReadBasicEffectContent(string s)
    {
        Match m = M(s, @"^(自身|対象)(?:に|は)《([^》]+)》スタックを([0-9]+)(?:付与する|獲得する)。?$");
        if (m.Success) return Content(EffectContentType.GainStack, ActorKey(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value);
        m = M(s, @"^対象を(.+?)の威力\+(.+?)の属性威力で攻撃する。?$");
        if (m.Success) return Content(EffectContentType.SkillAttack, "Target", "PowerAndAttribute", m.Groups[1].Value, m.Groups[2].Value);
        m = M(s, @"^対象の《([^》]+)》スタックをすべて消費して、被ダメージを×(.+?)倍増加する。?$");
        if (m.Success) return Content(EffectContentType.ConsumeStacksForDamage, "Target", m.Groups[1].Value, "All", m.Groups[2].Value, "ThisAttack");
        m = M(s, @"^《([^》]+)》罠を設置する。?$");
        if (m.Success) return Content(EffectContentType.PlaceTrap, m.Groups[1].Value);
        if (s.TrimEnd('。') == "スキルの処理後にこの罠は除去される") return Content(EffectContentType.RemoveSummon, "Self");
        m = M(s, @"^HP(.+?)回復。?$");
        if (m.Success) return Content(EffectContentType.RestoreResource, "Self", "HP", m.Groups[1].Value);
        return null;
    }

    private static string BasicEffectContentText(EffectContent content)
    {
        if (content == null) return null;
        string[] p;
        switch (content.Type)
        {
            case EffectContentType.RoleplayDescription: return Args(content.Parameters, 1, "RoleplayDescription")[0];
            case EffectContentType.ProhibitAcquisition:
                p = VariableArgs(content.Parameters, 3, "ProhibitAcquisition"); Require(p[0] == "Self" && p[1] == "CharacterCreation");
                return "キャラクター作成時に" + string.Join("及び", p.Skip(2).Select(x => "《" + x + "》")) + "習得不能。";
            case EffectContentType.OptionalCategoryExclusion:
                p = Args(content.Parameters, 3, "OptionalCategoryExclusion"); Require(p[0] == "Self" && p[2] == "PerOccurrence"); return "自身を〈" + p[1] + "〉ではない物として扱うかその都度任意で選択可能。";
            case EffectContentType.GainStack:
                if (content.Parameters == null || content.Parameters.Count != 3) return null;
                p = Args(content.Parameters, 3, "GainStack"); Number(p[2], 1, "スタック数"); return Actor(p[0]) + "に《" + p[1] + "》スタックを" + p[2] + "付与する。";
            case EffectContentType.SkillAttack:
                if (content.Parameters == null || content.Parameters.Count != 4 || content.Parameters[1] != "PowerAndAttribute") return null;
                p = Args(content.Parameters, 4, "SkillAttack"); Require(p[0] == "Target"); return "対象を" + p[2] + "の威力+" + p[3] + "の属性威力で攻撃する。";
            case EffectContentType.ConsumeStacksForDamage:
                p = Args(content.Parameters, 5, "ConsumeStacksForDamage"); Require(p[0] == "Target" && p[2] == "All" && p[4] == "ThisAttack"); return "対象の《" + p[1] + "》スタックをすべて消費して、被ダメージを×" + p[3] + "倍増加する。";
            case EffectContentType.PlaceTrap: return "《" + Args(content.Parameters, 1, "PlaceTrap")[0] + "》罠を設置する。";
            case EffectContentType.RemoveSummon: Require(Args(content.Parameters, 1, "RemoveSummon")[0] == "Self"); return "スキルの処理後にこの罠は除去される。";
        }
        return null;
    }

    private static string CheckAndAcquisitionContentText(EffectContent content)
    {
        if (content == null) return null;
        string[] p;
        switch (content.Type)
        {
            case EffectContentType.RerollActivation:
                p = Args(content.Parameters, 3, "RerollActivation"); Require(p[0] == "Self" && p[2] == "TriggeringRoll");
                return "能力値ボーナスを" + p[1] + "に置きかえて発動ロールをやり直す。";
            case EffectContentType.SetResourceValue:
                p = Args(content.Parameters, 3, "SetResourceValue"); Require(p[0] == "Self" && p[1] == "HP"); Number(p[2], 1, "耐えるHP");
                return "HP" + p[2] + "の状態で耐える。";
            case EffectContentType.SkipRoll:
                p = Args(content.Parameters, 4, "SkipRoll"); Require(p[0] == "Self" && p[2] == "Optional" && p[3] == "ThisResolution");
                return "この時、" + p[1] + "ロールは行わなくてもよい。";
            case EffectContentType.RemoveState:
                if (content.Parameters == null || content.Parameters.Count < 4 || content.Parameters[1] != "Categories") return null;
                p = VariableArgs(content.Parameters, 4, "RemoveState/Categories"); Require(p[0] == "Self" && p[2] == "All");
                return "自身に付与されている〈" + string.Join(", ", p.Skip(3)) + "〉状態を全て解除する。";
            case EffectContentType.LimitAcquisition:
                p = Args(content.Parameters, 4, "LimitAcquisition"); Require(p[0] == "Self" && p[1] == "RaceSelection");
                return Number(p[3], 0, "選択上限") == 0 ? "※〈" + p[2] + "〉は選択不可能。" : "〈" + p[2] + "〉を" + p[3] + "つまでしか選択する事が出来ない。";
            case EffectContentType.RaceAlias:
                p = Args(content.Parameters, 2, "RaceAlias"); Require(p[0] == "Self");
                return "あなたの種族は〈" + p[1] + "〉としても扱われる。";
            default: return null;
        }
    }

    private static string StackAndResourceContentText(EffectContent content)
    {
        if (content == null) return null;
        string[] p;
        switch (content.Type)
        {
            case EffectContentType.RerollGathering:
                p = Args(content.Parameters, 3, "RerollGathering"); Require(p[1] == "Day"); Number(p[2], 1, "使用回数"); return "《" + p[0] + "》による採取の結果を一日" + p[2] + "回までやり直すことが出来る。";
            case EffectContentType.EquipSlotSubstitution:
                Require(Args(content.Parameters, 3, "EquipSlotSubstitution").SequenceEqual(new[] { "Weapon", "BothHands", "OneHand" })); return "装備部位が両手である武器を片手で装備する事が出来る。";
            case EffectContentType.TransformAtStacks:
                p = Args(content.Parameters, 7, "TransformAtStacks"); Require(p[0] == "Self" && p[2] == "Equals" && p[4] == "HP" && p[5] == "MP"); Number(p[3], 1, "必要スタック"); return "スタックが" + p[3] + "になった時、HPとMPが全回復して《" + p[6] + "》状態になる。";
            case EffectContentType.CyclingStateStacks:
                p = Args(content.Parameters, 9, "CyclingStateStacks"); Require(p[0] == "Self"); Number(p[4], 1, "スタック獲得数"); Number(p[5], 1, "閾値"); Number(p[7], 1, "回復量"); Number(p[8], 0, "リセット値");
                return "〈" + p[1] + "〉を発動するたびに《" + p[2] + "》〈" + p[3] + "〉状態を" + p[4] + "スタック得る。\n" + p[5] + "スタック得た時、" + p[6] + "は+" + p[7] + "回復して、スタックは" + p[8] + "になる。";
            case EffectContentType.DrainResource:
                p = Args(content.Parameters, 6, "DrainResource"); Require(p[0] == "AllEnemies" && p[1] == "MP" && p[3] == "Self" && p[4] == "ActualTotal" && p[5] == "AllowOverflow");
                return "すべての敵のMPを-" + p[2] + "減少させる。\nさらに、その合計値のMPを回復する。\nこの回復効果はMPの最大値を超過する事が出来る。";
            case EffectContentType.CreateMeleeGroup:
                p = Args(content.Parameters, 3, "CreateMeleeGroup"); Require(p[0] == "Self" && p[1] == "Target"); return "自身と対象は新しい接近グループの《" + p[2] + "》状態になる。";
            case EffectContentType.PreventMealPenalties:
                Require(Args(content.Parameters, 3, "PreventMealPenalties").SequenceEqual(new[] { "Self", "MealAndMealSet", "ResourceAndStatDecrease" })); return "食事及び食事セット効果による, リソース減少及びステータス減少を受けない。";
            case EffectContentType.GrantCreationChoice:
                p = VariableArgs(content.Parameters, 3, "GrantCreationChoice"); Require(p[0] == "LifePath" && p[1] == "1"); return "キャラクター作成時、自身は次のライフパスを一つ選択して追加で習得する。\n" + string.Join(", ", p.Skip(2).Select(x => "《" + x + "》"));
            case EffectContentType.GrantRaceTrait:
                p = Args(content.Parameters, 2, "GrantRaceTrait"); Require(p[1] == "1"); return "〈" + p[0] + "〉の種族特性を一つ選んで追加で習得する。";
            case EffectContentType.OptionalInvalidateAction:
                p = Args(content.Parameters, 4, "OptionalInvalidateAction"); Require(p[0] == "Self" && p[2] == "AtMost"); Number(p[3], 0, "達成値"); return "自身を対象にした達成値" + p[3] + "以下の〈" + p[1] + "〉を無効にしても良い。";
            case EffectContentType.RemoveState:
                if (content.Parameters.Count == 2 && content.Parameters[0] == "Target") return "対象の《" + content.Parameters[1] + "》状態を解除する。"; return null;
            case EffectContentType.ApplyState:
                if (content.Parameters.Count == 3 && content.Parameters[0] == "SameMeleeExceptSelf") return "自身を除く、自身と同じ接近グループのキャラクター全てに" + content.Parameters[2] + "ターンの間《" + content.Parameters[1] + "》状態を付与する。"; return null;
            case EffectContentType.InvalidateTriggeredEffect:
                if (content.Parameters.SequenceEqual(new[] { "Target", "Counter" })) return "対象のカウンター効果を無効にする。"; return null;
            case EffectContentType.RerollActivation:
                if (content.Parameters.SequenceEqual(new[] { "Self", "Original", "TriggeringRoll" })) return "発動ロールをやり直す事が出来る。"; return null;
            case EffectContentType.CharacterRule: return CharacterRuleText(content);
            default: return null;
        }
    }

    private static string RaceNames(string text)
    { return string.Join(", ", AllMatches(text, @"《([^》]+)》").Cast<Match>().Select(x => x.Groups[1].Value)); }

    private static string RaceAndRecoveryContentText(EffectContent content)
    {
        if (content == null || content.Type != EffectContentType.RestoreFromDamage) return null;
        var p = Args(content.Parameters, 3, "RestoreFromDamage"); Require(p[0] == "Self"); PositiveModifierFactor(p[2]);
        return "この攻撃で与えたダメージの×" + p[2] + "倍の" + p[1] + "を回復する。";
    }

    private static string SelectedSkillAndResourceContentText(EffectContent content)
    {
        if (content == null) return null;
        string[] p;
        switch (content.Type)
        {
            case EffectContentType.RemoveState:
                if (content.Parameters.Count != 2 || content.Parameters[0] != "Self") return null;
                return "自身の《" + content.Parameters[1] + "》状態を解除する。";
            case EffectContentType.LeaveBattle:
                p = Args(content.Parameters, 1, "LeaveBattle"); Require(p[0] == "Self"); return "自身は戦闘を離脱する。";
            case EffectContentType.PreventRollAtResource:
                p = Args(content.Parameters, 5, "PreventRollAtResource"); Require(p[0] == "Self" && p[2] == "AtMost"); Number(p[3], 0, "閾値");
                return "自身は" + p[1] + "が" + p[3] + "以下になった時、" + p[4] + "ロールを行う必要がない。";
            case EffectContentType.CategoryImmunity:
                p = VariableArgs(content.Parameters, 2, "CategoryImmunity"); Require(p[0] == "Self");
                return "自身は" + string.Join("及び", p.Skip(1).Select(x => "〈" + x + "〉")) + "による効果を受ける事が出来ない。";
            case EffectContentType.ProhibitCategoryTarget:
                p = Args(content.Parameters, 2, "ProhibitCategoryTarget"); Require(p[0] == "Self"); return "自身は〈" + p[1] + "〉を対象にすることはできない。";
            case EffectContentType.ConvertResourceCost:
                p = Args(content.Parameters, 4, "ConvertResourceCost"); Require(p[0] == "Self" && p[3] == "All" && p[1] != p[2]);
                return "自身の" + p[1] + "の消費は全て" + p[2] + "の消費に置き換えられる。";
            case EffectContentType.AddQuestReward:
                p = Args(content.Parameters, 3, "AddQuestReward"); Require(p[0] == "Self"); Number(p[2], 1, "報酬加算"); return "クエストによって獲得する" + p[1] + "が+" + p[2] + "される。";
            case EffectContentType.ApplySelectedSkillState:
                p = Args(content.Parameters, 3, "ApplySelectedSkillState"); Require(p[0] == "SelectedSkill"); Number(p[2], 1, "持続ターン"); return "対象スキルを" + p[2] + "ターンの間《" + p[1] + "》状態にする。";
            case EffectContentType.ResourceDamage:
                p = Args(content.Parameters, 3, "ResourceDamage"); Require(p[0] == "Target"); return "対象に" + p[2] + "の" + p[1] + "ダメージを与える。";
            case EffectContentType.RestoreFromResourceDamage:
                p = Args(content.Parameters, 5, "RestoreFromResourceDamage"); Require(p[0] == "Self" && p[4] == "ThisResolution"); PositiveModifierFactor(p[3]);
                return "与えた" + p[1] + "ダメージの" + p[3] + "倍の値の" + p[2] + "を回復する。";
            case EffectContentType.TurnStartStateStacks:
                p = Args(content.Parameters, 5, "TurnStartStateStacks"); Require(p[0] == "Self" && p[4] == "AllCharacters"); Number(p[3], 1, "スタック数");
                return "《" + p[1] + "》状態のキャラクター全てに自身のターン開始時《" + p[2] + "》スタックを" + p[3] + "付与する。";
            default: return null;
        }
    }

}
