using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public static partial class SkillTextConverter
{
    // Rules describe character creation, areas and rewards. Display names and
    // numeric values are arguments; no skill or race name selects a rule.
    private sealed class CharacterTextRule
    {
        public readonly string Key;
        private readonly string template;
        private readonly string[] shape;
        private readonly string pattern;

        public CharacterTextRule(string key, string text, params string[] parameters)
        {
            Key = key; template = text; shape = parameters;
            string escaped = System.Text.RegularExpressions.Regex.Escape(text.TrimEnd('。'));
            for (int i = 0; text.Contains("{" + i + "}"); i++)
                escaped = escaped.Replace(System.Text.RegularExpressions.Regex.Escape("{" + i + "}"),
                    "(?<p" + i + ">[^〈〉《》\\r\\n。]+?)");
            pattern = "^" + escaped + "。?$";
        }

        public string[] Read(string text)
        {
            var match = M(text, pattern);
            if (!match.Success) return null;
            var result = new List<string> { Key };
            foreach (string item in shape)
            {
                var field = M(item, @"^(Percent|Count)?\{([0-9]+)\}$");
                if (!field.Success) { result.Add(item); continue; }
                string value = match.Groups["p" + field.Groups[2].Value].Value;
                if (field.Groups[1].Value == "Percent")
                    value = (CharacterNumber(value) / 100m).ToString(CultureInfo.InvariantCulture);
                else if (field.Groups[1].Value == "Count" && value == "一") value = "1";
                result.Add(value);
            }
            ValidateCharacterRule(result.ToArray());
            return result.ToArray();
        }

        public string Write(string[] parameters)
        {
            if (parameters.Length != shape.Length + 1 || parameters[0] != Key) return null;
            var values = new Dictionary<int, string>();
            for (int i = 0; i < shape.Length; i++)
            {
                var field = M(shape[i], @"^(Percent|Count)?\{([0-9]+)\}$");
                string value = parameters[i + 1];
                if (!field.Success) { if (value != shape[i]) return null; continue; }
                int index = Number(field.Groups[2].Value, 0, "文型引数");
                if (field.Groups[1].Value == "Percent")
                    value = (CharacterNumber(value) * 100m).ToString("0.############################", CultureInfo.InvariantCulture);
                else if (field.Groups[1].Value == "Count" && value == "1") value = "一";
                string previous;
                if (values.TryGetValue(index, out previous) && previous != value) return null;
                values[index] = value;
            }
            var arguments = Enumerable.Range(0, values.Count).Select(i => (object)values[i]).ToArray();
            string result = string.Format(CultureInfo.InvariantCulture, template, arguments);
            if (!M(result, pattern).Success) throw new InvalidOperationException("キャラクタールールの引数に構文記号は使用できません。");
            return result;
        }
    }

    private static readonly CharacterTextRule[] CharacterRules =
    {
        new CharacterTextRule("ForbiddenArea", "自身は〈{0}〉に進入する事が出来ない。", "{0}"),
        new CharacterTextRule("AreaActions", "代りに〈{0}〉で〈{1}〉と同等の行動を行う事が出来る。", "{0}", "{1}"),
        new CharacterTextRule("StartingArea", "キャラクター作成時、自身はエリア《{0}》に移動する。", "CharacterCreation", "{0}"),
        new CharacterTextRule("AllocateRaceBonuses", "任意の種類の種族能力値ボーナスを合計+{0}%まで選んで振り分ける事が出来る。", "Any", "Percent{0}"),
        new CharacterTextRule("RaceBonusCap", "※各能力値毎に振り分けられる種族能力値ボーナスは最大+{0}%の加算まで。", "PerStat", "Percent{0}"),
        new CharacterTextRule("SelectMonster", "種族選択時に、危険度☆{0}までの〈{1}〉かつ〈{2}〉の中から{3}つのモンスターを選ぶ。",
            "RaceSelection", "Count{3}", "AtMost", "{0}", "And", "{1}", "{2}"),
        new CharacterTextRule("MonsterActionConditions", "自身は選択したモンスターの行動の条件に従ってスキルを宣言する事が出来る。", "SelectedMonster"),
        new CharacterTextRule("OtherSkillLimit", "行動に記載されていないスキルは1ターンに{0}度のみ宣言可能。", "Turn", "{0}", "Shared"),
        new CharacterTextRule("InheritTraits", "選択したモンスターと同じ特性を得る。", "SelectedMonster"),
        new CharacterTextRule("AreaMoveRecovery", "エリア移動時に全てのリソースを{0}%回復する。", "AllResources", "MaxRatio", "Percent{0}"),
        new CharacterTextRule("PartyWipeRecovery", "全滅時、{0}と{1}を{2}消費して、全リソースの回復及び《戦闘不能》と《発狂》を含むすべての状態を解除する。",
            "{0}", "{2}", "{1}", "{2}", "AllResources", "Full", "AllStatesIncludingIncapacitationAndInsanity"),
        new CharacterTextRule("PartyWipeRecovery", "全滅時、{0}を{1}及び{2}を{3}消費して、全リソースの回復及び《戦闘不能》と《発狂》を含むすべての状態を解除する。",
            "{0}", "{1}", "{2}", "{3}", "AllResources", "Full", "AllStatesIncludingIncapacitationAndInsanity"),
        new CharacterTextRule("PartyRaceRestriction", "種族《{0}》以外とパーティーを組むことはできない。", "{0}", "Only"),
        new CharacterTextRule("DefeatReward", "☆{0}以上の敵を倒した時、クエスト扱いで{1}と{2}を{3}獲得する。（1日{4}点まで）",
            "AtLeast", "{0}", "Quest", "{1}", "{3}", "{2}", "{3}", "Day", "{4}"),
        new CharacterTextRule("DefeatReward", "☆{0}以上の敵を倒した時、クエスト扱いで{1}を{2}及び{3}を{4}獲得する。（1日{5}点まで）",
            "AtLeast", "{0}", "Quest", "{1}", "{2}", "{3}", "{4}", "Day", "{5}")
    };

    private static bool ReadCharacterRule(SkillBody skill, EffectType type, string text)
    {
        string normalized = text.Replace("意外とパーティー", "以外とパーティー");
        foreach (var rule in CharacterRules)
        {
            var parameters = rule.Read(normalized);
            if (parameters == null) continue;
            Require(type == EffectType.Passive);
            AddSkillEffect(skill, type, Content(EffectContentType.CharacterRule, parameters));
            return true;
        }
        return false;
    }

    private static string CharacterRuleText(EffectContent content)
    {
        string[] parameters = VariableArgs(content.Parameters, 2, "CharacterRule");
        ValidateCharacterRule(parameters);
        foreach (var rule in CharacterRules.Where(x => x.Key == parameters[0]))
        {
            string text = rule.Write(parameters);
            if (text != null) return text;
        }
        throw new InvalidOperationException("キャラクタールールのパラメーターが不正です。");
    }

    private static decimal CharacterNumber(string value)
    {
        decimal result;
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result) || result < 0)
            throw new InvalidOperationException("割合には0以上の数値を指定してください。");
        return result;
    }

    private static void ValidateCharacterRule(string[] p)
    {
        foreach (string part in p) Need(part, "キャラクタールールの引数");
        switch (p[0])
        {
            case "ForbiddenArea": Require(p.Length == 2); break;
            case "AreaActions": Require(p.Length == 3); break;
            case "StartingArea": Require(p.Length == 3 && p[1] == "CharacterCreation"); break;
            case "AllocateRaceBonuses":
            case "RaceBonusCap":
                Require(p.Length == 3 && p[1] == (p[0] == "AllocateRaceBonuses" ? "Any" : "PerStat"));
                Require(CharacterNumber(p[2]) > 0); break;
            case "SelectMonster":
                Require(p.Length == 8 && p[1] == "RaceSelection" && p[3] == "AtMost" && p[5] == "And");
                Number(p[2], 1, "選択数"); Number(p[4], 1, "危険度"); break;
            case "MonsterActionConditions":
            case "InheritTraits": Require(p.Length == 2 && p[1] == "SelectedMonster"); break;
            case "OtherSkillLimit":
                Require(p.Length == 4 && p[1] == "Turn" && p[3] == "Shared"); Number(p[2], 1, "宣言回数"); break;
            case "AreaMoveRecovery":
                Require(p.Length == 4 && p[1] == "AllResources" && p[2] == "MaxRatio");
                Require(CharacterNumber(p[3]) <= 1); break;
            case "PartyWipeRecovery":
                Require(p.Length == 8 && p[1] != p[3] && p[5] == "AllResources" && p[6] == "Full" && p[7] == "AllStatesIncludingIncapacitationAndInsanity");
                Number(p[2], 0, "消費量"); Number(p[4], 0, "消費量"); break;
            case "PartyRaceRestriction": Require(p.Length == 3 && p[2] == "Only"); break;
            case "DefeatReward":
                Require(p.Length == 10 && p[1] == "AtLeast" && p[3] == "Quest" && p[4] != p[6] && p[8] == "Day");
                Number(p[2], 1, "危険度"); Number(p[5], 1, "報酬量"); Number(p[7], 1, "報酬量"); Number(p[9], 1, "日上限"); break;
            default: throw new InvalidOperationException("未対応のキャラクタールールです：" + p[0]);
        }
    }
}
