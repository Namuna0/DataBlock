using System;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static bool ReadBeastRaceSkill(SkillBody skill, EffectType type, string text)
    {
        string s = RegexReplace(text.Trim(), @"^(?:さらに|更に)[、]?\s*", "");
        if (ReadBeastRaceModifier(skill, type, s)) return true;
        Match m = M(s, @"^環境((?:《[^》]+》)(?:および《[^》]+》)*)による環境効果を受けない。?$");
        if (m.Success)
        { ExtendedEffect(skill, type, Content(EffectContentType.EnvironmentImmunity, new[] { "Self" }.Concat(AllMatches(m.Groups[1].Value, @"《([^》]+)》").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray())); return true; }
        m = M(s, @"^自身の〈([^〉]+)〉によって相手を《([^》]+)》状態にした時、([A-Z]+)を\3最大値の([0-9]+)%回復する。?$");
        if (m.Success)
        { ExtendedEffect(skill, type, Content(EffectContentType.RestoreOnAppliedState, "Self", m.Groups[1].Value, "Target", m.Groups[2].Value, m.Groups[3].Value, "MaximumPercent", m.Groups[4].Value)); return true; }
        if (s.TrimEnd('。') == "対象の行為判定をクリティカル、もしくはファンブルに変える事が出来る")
        { ExtendedEffect(skill, type, Content(EffectContentType.ChooseCheckOutcome, "Target", "Critical", "Fumble", "Optional")); return true; }
        m = M(s, @"^《([^》]+)》×([0-9]+)を獲得。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.GrantItem, "Self", m.Groups[1].Value, m.Groups[2].Value, "Unspecified")); return true; }
        m = M(s, @"^ランクは☆×(.+?)となる。?$");
        if (m.Success)
        {
            var item = skill.Effects.Where(x => x.Type == type).SelectMany(x => x.Contents).LastOrDefault(x => x.Type == EffectContentType.GrantItem);
            Require(item != null && item.Parameters[3] == "Unspecified"); item.Parameters[3] = m.Groups[1].Value; return true;
        }
        m = M(s, @"^(.+?)ロールによる状態を解除する。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.RemoveStatesByOrigin, "Self", "Roll", m.Groups[1].Value)); return true; }
        if (s.TrimEnd('。') == "対象へ選択したスキルをACT消費無しで宣言してもよい")
        { ExtendedEffect(skill, type, Content(EffectContentType.DeclareSelectedSkill, "Self", "Target", "ACT", "0", "Optional", "Unspecified")); return true; }
        m = M(s, @"^そのスキルで与える威力は×([0-9.]+)倍される。?$");
        if (m.Success)
        {
            var declaration = skill.Effects.Where(x => x.Type == type).SelectMany(x => x.Contents).LastOrDefault(x => x.Type == EffectContentType.DeclareSelectedSkill);
            Require(declaration != null && declaration.Parameters[5] == "Unspecified"); declaration.Parameters[5] = m.Groups[1].Value; return true;
        }
        if (s.TrimEnd('。') == "戦闘中、装備している武器と対象武器を入れ替える")
        { ExtendedEffect(skill, type, Content(EffectContentType.SwapWeapon, "Self", "SelectedWeapon", "Battle", "AllowUnarmed")); return true; }
        m = M(s, @"^([0-9]+)ターンの間、対象は《([^》]+)》状態になる。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.ApplyState, "Target", m.Groups[2].Value, m.Groups[1].Value)); return true; }
        m = M(s, @"^対象を(.+?)のスキル値で武器攻撃する。?$");
        if (m.Success) { ExtendedEffect(skill, type, Content(EffectContentType.WeaponAttack, "Target", m.Groups[1].Value)); return true; }
        if (s != text.Trim()) { ReadSkillLine(skill, type, s); return true; }
        return false;
    }

    private static string BeastRaceContentText(EffectContent content)
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
                p = Args(content.Parameters, 6, "DeclareSelectedSkill"); Require(p.Take(5).SequenceEqual(new[] { "Self", "Target", "ACT", "0", "Optional" })); PositiveHumanFactor(p[5]);
                return "対象へ選択したスキルをACT消費無しで宣言してもよい。\nそのスキルで与える威力は×" + p[5] + "倍される。";
            case EffectContentType.SwapWeapon:
                Require(Args(content.Parameters, 4, "SwapWeapon").SequenceEqual(new[] { "Self", "SelectedWeapon", "Battle", "AllowUnarmed" }));
                return "戦闘中、装備している武器と対象武器を入れ替える。";
            default: return null;
        }
    }
}
