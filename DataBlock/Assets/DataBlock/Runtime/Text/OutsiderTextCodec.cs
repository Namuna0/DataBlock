using System;
using System.Linq;

public static partial class SkillTextConverter
{
    // This deliberately narrow ruleset is the user-authorized monster/outsider mode.
    // Each entry retains its own mechanical parameters instead of an opaque prose payload.
    private sealed class OutsiderTextRule
    {
        public readonly string Text;
        public readonly string[] Parameters;
        public OutsiderTextRule(string text, params string[] parameters) { Text = text; Parameters = parameters; }
    }
    private static readonly OutsiderTextRule[] OutsiderRules =
    {
        new OutsiderTextRule("自身は〈町エリア〉に進入する事が出来ない。", "ForbiddenArea", "町エリア"),
        new OutsiderTextRule("代りに〈戦闘エリア〉で〈町エリア〉と同等の行動を行う事が出来る。", "AreaActions", "戦闘エリア", "町エリア"),
        new OutsiderTextRule("キャラクター作成時、自身はエリア《アルゼーペン近郊》に移動する。", "StartingArea", "CharacterCreation", "アルゼーペン近郊"),
        new OutsiderTextRule("任意の種類の種族能力値ボーナスを合計+40%まで選んで振り分ける事が出来る。", "AllocateRaceBonuses", "Any", "0.4"),
        new OutsiderTextRule("※各能力値毎に振り分けられる種族能力値ボーナスは最大+20%の加算まで。", "RaceBonusCap", "PerStat", "0.2"),
        new OutsiderTextRule("種族選択時に、危険度☆4までの〈一般エネミー〉かつ〈魔法生物〉の中から一つのモンスターを選ぶ。", "SelectMonster", "RaceSelection", "1", "AtMost", "4", "And", "一般エネミー", "魔法生物"),
        new OutsiderTextRule("自身は選択したモンスターの行動の条件に従ってスキルを宣言する事が出来る。", "MonsterActionConditions", "SelectedMonster"),
        new OutsiderTextRule("行動に記載されていないスキルは1ターンに1度のみ宣言可能。", "OtherSkillLimit", "Turn", "1", "Shared"),
        new OutsiderTextRule("選択したモンスターと同じ特性を得る。", "InheritTraits", "SelectedMonster"),
        new OutsiderTextRule("エリア移動時に全てのリソースを20%回復する。", "AreaMoveRecovery", "AllResources", "MaxRatio", "0.2"),
        new OutsiderTextRule("全滅時、経験値と決意を2消費して、全リソースの回復及び《戦闘不能》と《発狂》を含むすべての状態を解除する。", "PartyWipeRecovery", "経験値", "2", "決意", "2", "AllResources", "Full", "AllStatesIncludingIncapacitationAndInsanity"),
        new OutsiderTextRule("種族《モンスター》以外とパーティーを組むことはできない。", "PartyRaceRestriction", "モンスター", "Only"),
        new OutsiderTextRule("☆3以上の敵を倒した時、クエスト扱いで経験値と決意を1獲得する。（1日6点まで）", "DefeatReward", "AtLeast", "3", "Quest", "経験値", "1", "決意", "1", "Day", "6")
    };
    private static bool ReadOutsiderRule(SkillBody skill, EffectType type, string text)
    {
        string normalized = text.Replace("意外とパーティー", "以外とパーティー").TrimEnd('。');
        var rule = OutsiderRules.FirstOrDefault(r => r.Text.TrimEnd('。') == normalized);
        if (rule == null) return false;
        Require(type == EffectType.Passive);
        ExtendedEffect(skill, type, Content(EffectContentType.OutsiderRule, rule.Parameters)); return true;
    }
    private static string OutsiderRuleText(EffectContent content)
    {
        var p = VariableArgs(content.Parameters, 2, "OutsiderRule");
        var rule = OutsiderRules.FirstOrDefault(r => r.Parameters.SequenceEqual(p));
        if (rule == null) throw new InvalidOperationException("人外の固有ルールのパラメーターが未対応です。");
        return rule.Text;
    }
}
