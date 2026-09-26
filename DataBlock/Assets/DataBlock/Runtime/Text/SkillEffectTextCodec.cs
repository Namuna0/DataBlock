using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static void ReadSkillLine(SkillBody skill, EffectType type, string text)
    {
        foreach (string part in Split(Unbullet(text), "・"))
        {
            string body = RegexReplace(part.Trim(), @"^さらに\s*", "");
            if (type == EffectType.SecondSpike || type == EffectType.ThirdSpike)
            {
                if (TryReadPassiveSpike(skill, type, body)) continue;
                Match combinedSpike = M(body, @"^このアクティブ効果によるスキル値(?:は)?([^、]+)、属性威力(?:は)?(.+?)に変化する。?$");
                if (!combinedSpike.Success) combinedSpike = M(body, @"^このアクティブ効果によるスキル値(?:は)?(.+?)の武器威力\+(.+?)に変化する。?$");
                if (combinedSpike.Success)
                {
                    AddOverride(skill.Overrides, type, NoTriggers(),
                        Change(OverrideContentType.SetSkillValue, combinedSpike.Groups[1].Value),
                        Change(OverrideContentType.SetAttackComponent, "AttributePower", combinedSpike.Groups[2].Value));
                    continue;
                }
                Match spike = M(body, @"^このアクティブ効果によるスキル値は(.+?)に変化する。?$");
                if (spike.Success)
                {
                    AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetSkillValue, spike.Groups[1].Value));
                    continue;
                }
                spike = M(body, @"^自身が《([^》]+)》状態の時[、,]\s*(?:(?:このアクティブ効果による)?スキル値は)?(.+?)に変化する。?$");
                if (spike.Success)
                {
                    AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.SkillValue, Condition(ConditionType.OwnState, spike.Groups[1].Value))), Change(OverrideContentType.SetSkillValue, spike.Groups[2].Value));
                    continue;
                }
                spike = M(body, @"^この(?:アクティブ|カウンター)効果による軽減は(.+?)に変化する。?$");
                if (spike.Success)
                {
                    AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetDamageReduction, spike.Groups[1].Value));
                    continue;
                }
                spike = M(body, @"^このアクティブ効果による属性威力は(.+?)に変化する。?$");
                if (spike.Success)
                {
                    AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetAttackComponent, "AttributePower", spike.Groups[1].Value));
                    continue;
                }
                spike = M(body, @"^この発動ロールは(.+?)に変化する。?$");
                if (spike.Success)
                {
                    AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetActivationRollFormula, spike.Groups[1].Value));
                    continue;
                }
                throw new InvalidOperationException("スパイクはスキル値・属性威力・被ダメージ軽減値・発動ロールの変更に対応します。");
            }
            if (!IsOrdinary(type)) throw new InvalidOperationException("効果種別を指定してください。");
            if (TryReadPassiveSkillLine(skill, type, body)) continue;
            var triggers = NoTriggers();
            Match branch = M(body, @"^[-－]\s*([0-9]+)[：:]\s*(.+)$");
            if (branch.Success)
            {
                EffectContent roll = skill.Effects.Where(x => x.Type == type).SelectMany(x => x.Contents).LastOrDefault(x => x.Type == EffectContentType.RollDice);
                if (roll == null) throw new InvalidOperationException("出目分岐の前に追加ダイスロールを指定してください。");
                string[] rollArgs = Args(roll.Parameters, 2, "RollDice");
                triggers.Add(Trigger(TriggerTiming.RandomResult, Condition(ConditionType.RollResult, rollArgs[0], branch.Groups[1].Value)));
                body = branch.Groups[2].Value;
            }
            else
            {
                branch = M(body, @"^追加ロール([0-9]+)の出目が([0-9]+)の時[、,]\s*(.+)$");
                if (branch.Success)
                {
                    triggers.Add(Trigger(TriggerTiming.RandomResult, Condition(ConditionType.RollResult, branch.Groups[1].Value, branch.Groups[2].Value)));
                    body = branch.Groups[3].Value;
                }
            }
            if (triggers.Count == 0 && ReadSkillOverride(skill, type, body)) continue;
            if (body.TrimEnd('。') == "防御点はそれぞれに適用される")
            {
                var prior = skill.Effects.LastOrDefault(x => x.Type == type);
                EffectContent attack = prior == null ? null : prior.Contents.LastOrDefault();
                if (attack == null || attack.Type != EffectContentType.WeaponAttack || (attack.Parameters.Count != 4 || attack.Parameters[3] != "Default")) throw new InvalidOperationException("防御点の記述の直前に連続武器攻撃を指定してください。");
                attack.Parameters[3] = "PerHit"; continue;
            }
            Match conditional = M(body, @"^この攻撃で(自身|対象|付与者)を《([^》]+)》状態にした時[、,]\s*(.+)$");
            if (conditional.Success)
            {
                triggers.Add(Trigger(TriggerTiming.AfterWeaponAttack, Condition(ConditionType.AttackAppliedState, ActorKey(conditional.Groups[1].Value), conditional.Groups[2].Value)));
                body = conditional.Groups[3].Value;
            }
            Match risk = M(body, @"^(?:この宣言効果は)?戦闘中([0-9]+)(?:度だけ|回まで)[、,]\s*このスキルによって対象にダメージを与えられなかった場合[、,]\s*(.+)$");
            if (risk.Success)
            {
                if (type != EffectType.Declaration) throw new InvalidOperationException("この戦闘回数制限は宣言効果に指定してください。");
                triggers.Add(Trigger(TriggerTiming.AfterSkillResolution, Condition(ConditionType.BattleEffectLimit, risk.Groups[1].Value), Condition(ConditionType.SkillDealtNoDamage, "Target")));
                body = risk.Groups[2].Value;
            }
            foreach (string atom in Split(body, ","))
            {
                if (M(atom, @"^対象1体を").Success && !skill.DeclarationConditions.And.Any(c => (c.Type == ConditionType.SelectCharacters || c.Type == ConditionType.SelectMeleeCharacters) && c.Parameters.SequenceEqual(new[] { "1", "Exact", "Default" })))
                    throw new InvalidOperationException("対象1体の攻撃には、ちょうど1体を選択する宣言条件を指定してください。");
                var effect = new EffectDefinition { Type = type, Triggers = triggers };
                EffectContent content = ReadContent(atom);
                if (content.Type == EffectContentType.RollDice && content.Parameters.Count == 2 && content.Parameters[0].Length == 0)
                {
                    var usedIds = new HashSet<string>(skill.Effects.SelectMany(x => x.Contents).Where(x => x.Type == EffectContentType.RollDice && x.Parameters.Count == 2).Select(x => x.Parameters[0]), StringComparer.Ordinal);
                    int nextId = 1;
                    while (usedIds.Contains(nextId.ToString())) nextId++;
                    content.Parameters[0] = nextId.ToString();
                }
                effect.Contents.Add(content); skill.Effects.Add(effect);
            }
        }
    }
    private static List<TriggerDefinition> MasteryTriggers(string equipped, string weapon, string action)
    {
        return new List<TriggerDefinition>
        {
            Trigger(TriggerTiming.TargetValue, Condition(ConditionType.EquippedCategory, "Self", equipped), Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.IsWeaponAttack), Condition(ConditionType.WeaponCategory, weapon)),
            Trigger(TriggerTiming.TargetValue, Condition(ConditionType.EquippedCategory, "Self", equipped), Condition(ConditionType.ActionSource, "Self"), Condition(ConditionType.ActionCategory, action))
        };
    }
    private static List<TriggerDefinition> SkillWeaponPowerTriggers(string weapon)
    {
        return On(Trigger(TriggerTiming.AttackPower,
            Condition(ConditionType.ActionSource, "Self"),
            Condition(ConditionType.ActionOrigin, "Skill"),
            Condition(ConditionType.IsWeaponAttack),
            Condition(ConditionType.WeaponCategory, weapon)));
    }
    private static List<TriggerDefinition> SkillValueWhenOwnState(string stateName)
    {
        return On(Trigger(TriggerTiming.SkillValue, Condition(ConditionType.OwnState, stateName)));
    }
    private static bool ReadSkillOverride(SkillBody skill, EffectType type, string body)
    {
        Match m = M(body, @"^属性威力(?:は|が)?×?(.+?)倍(?:される)?。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.MultiplyAttackComponent, "AttributePower", m.Groups[1].Value)); return true; }
        m = M(body, @"^対象のHPが最大値の半分以下なら属性威力(?:は|が)×?(.+?)倍される。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type,
                On(Trigger(TriggerTiming.ElementalPower, Condition(ConditionType.ResourceRatio, "Target", "HP", "AtMost", "0.5"))),
                Change(OverrideContentType.MultiplyAttackComponent, "AttributePower", m.Groups[1].Value)); return true;
        }
        if (body.TrimEnd('。') == "この攻撃は対象の防御点の影響を受けない")
        {
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetAttackRule, "Defense", "Ignore")); return true;
        }
        m = M(body, @"^このスキルを対象に〈([^〉]+)〉を宣言する事は出来ない。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetAttackRule, "Response", m.Groups[1].Value, "Prohibit")); return true;
        }
        m = M(body, @"^威力(?:は|が)?×?(.+?)倍(?:される)?。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.MultiplyPower, m.Groups[1].Value)); return true; }
        m = M(body, @"^自身が《([^》]+)》状態の時[、,]\s*(?:この(?:アクティブ効果による)?)?スキル値は(.+?)に変化する。?$");
        if (m.Success)
        {
            if (type != EffectType.Active) throw new InvalidOperationException("条件付きスキル値変更はアクティブ効果に指定してください。");
            AddOverride(skill.Overrides, type, SkillValueWhenOwnState(m.Groups[1].Value), Change(OverrideContentType.SetSkillValue, m.Groups[2].Value)); return true;
        }
        m = M(body, @"^([A-Z]+)の消費(?:が|は)(.+?)になる。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetResourceCost, m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(body, @"^クールタイム(?:が|は)(.+?)になる。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.SetCooldown, m.Groups[1].Value)); return true; }
        if (body.TrimEnd('。') == "この攻撃の発動ロールに成功した時、確定でクリティカル扱いとなる" || body.TrimEnd('。') == "この攻撃の発動ロールに成功した時、クリティカルとして扱う")
        {
            AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.AfterActivationRoll, Condition(ConditionType.RollSucceeded))), Change(OverrideContentType.SetCritical, "True")); return true;
        }
        m = M(body, @"^(?:選択した)?状態の持続ターン(?:を|は)([+-]?.+?)(?:加算する|される)。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.AddStateDuration, "SelectedState", Signed(m.Groups[1].Value))); return true; }
        m = M(body, @"^自身の消費([A-Z]+)(?:を|は)([+-].+?)(?:する事が出来る|される。?[（(]任意[）)])。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.ResourceCost)), Change(OverrideContentType.AddResourceCost, "Self", m.Groups[1].Value, Signed(m.Groups[2].Value), "Optional")); return true; }
        m = M(body, @"^消費([A-Z]+)([+-].+?)。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.AddResourceCost, "Self", m.Groups[1].Value, Signed(m.Groups[2].Value), "Automatic")); return true; }
        m = M(body, @"^この攻撃は〈([^〉]+)〉によるカウンター効果で(自身|対象|付与者)にダメージを与えることはできない。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(OverrideContentType.PreventCounterDamage, ActorKey(m.Groups[2].Value), m.Groups[1].Value)); return true; }
        m = M(body, @"^スキルによって〈([^〉]+)〉で武器攻撃を行う時[、,]\s*威力(?:が|は)×?(.+?)倍される。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, SkillWeaponPowerTriggers(m.Groups[1].Value), Change(OverrideContentType.MultiplyPower, m.Groups[2].Value)); return true; }
        m = M(body, @"^自身(?:が|は)〈([^〉]+)〉を装備している場合[、,]\s*〈([^〉]+)〉による武器攻撃(?:、?及び|または)〈([^〉]+)〉(?:による)?目標値(?:が|は)([+-].+?)される。?$");
        if (!m.Success) m = M(body, @"^自身(?:が|は)〈([^〉]+)〉を装備している場合[、,]\s*〈([^〉]+)〉による武器攻撃または〈([^〉]+)〉の目標値(?:が|は)([+-].+?)される。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type, MasteryTriggers(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value), Change(OverrideContentType.AddTargetValue, Signed(m.Groups[4].Value))); return true;
        }
        return false;
    }
    private static EffectContent ReadContent(string text)
    {
        string s = RegexReplace(Unbullet(text), @"^さらに\s*", "").Trim();
        Match m = M(s, @"^味方キャラクター全員の([A-Z]+)を(.+?)回復する。?$");
        if (m.Success) return Content(EffectContentType.RestoreResource, "AllAllies", m.Groups[1].Value, m.Groups[2].Value);
        m = M(s, @"^(?:(自身|対象|付与者)(?:は|の))?([A-Z]+)(?:が|を)(.+?)回復する。?$");
        if (m.Success) return Content(EffectContentType.RestoreResource, m.Groups[1].Success ? ActorKey(m.Groups[1].Value) : "Self", m.Groups[2].Value, m.Groups[3].Value);
        m = M(s, @"^([A-Z]+)([0-9]+)回復。?$");
        if (m.Success) return Content(EffectContentType.RestoreResource, "Self", m.Groups[1].Value, m.Groups[2].Value);
        m = M(s, @"^自身を除くすべての敵キャラクターは\s*(?:([0-9]+)ターンの間)?《([^》]+)》状態(?:になる|となる)。?$");
        if (m.Success) return m.Groups[1].Success ? Content(EffectContentType.ApplyState, "AllEnemiesExceptSelf", m.Groups[2].Value, m.Groups[1].Value) : Content(EffectContentType.ApplyState, "AllEnemiesExceptSelf", m.Groups[2].Value);
        m = M(s, @"^(自身|対象|付与者)へ(?:([0-9]+)ターンの間)?《([^》]+)》状態を付与する。?$");
        if (m.Success) return m.Groups[2].Success ? Content(EffectContentType.ApplyState, ActorKey(m.Groups[1].Value), m.Groups[3].Value, m.Groups[2].Value) : Content(EffectContentType.ApplyState, ActorKey(m.Groups[1].Value), m.Groups[3].Value);
        m = M(s, @"^(自身|対象|付与者)は\s*(?:([0-9]+)ターンの間)?《([^》]+)》状態(?:になる|となる)。?$");
        if (m.Success) return m.Groups[2].Success ? Content(EffectContentType.ApplyState, ActorKey(m.Groups[1].Value), m.Groups[3].Value, m.Groups[2].Value) : Content(EffectContentType.ApplyState, ActorKey(m.Groups[1].Value), m.Groups[3].Value);
        m = M(s, @"^([A-Z]+)-(.+?)。?$");
        if (m.Success) return Content(EffectContentType.ConsumeResource, "Self", m.Groups[1].Value, m.Groups[2].Value);
        m = M(s, @"^(自身|対象|付与者)は([A-Z]+)を(.+?)消費する。?$");
        if (m.Success) return Content(EffectContentType.ConsumeResource, ActorKey(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value);
        m = M(s, @"^自身と対象は同じ接近グループの((?:《[^》]+》)(?:と《[^》]+》)*)状態になる。?$");
        if (m.Success) return Content(EffectContentType.JoinMeleeGroup, new[] { "Self", "Target" }.Concat(AllMatches(m.Groups[1].Value, @"《([^》]+)》").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray());
        m = M(s, @"^(自身|対象|付与者)は同じ接近グループの《([^》]+)》状態になる。?$");
        if (m.Success) return Content(EffectContentType.ApplyStateInMeleeGroup, ActorKey(m.Groups[1].Value), m.Groups[2].Value);
        m = M(s, @"^(対象|《([^》]+)》状態の全ての対象)をスキル値(.+?)の武器威力\+(.+?)の(.+?)属性威力で攻撃する。?$");
        if (m.Success)
        {
            string target = m.Groups[2].Success ? StateTarget(m.Groups[2].Value) : "Target";
            string[] elements = Split(m.Groups[5].Value, "かつ").ToArray();
            if (elements.Length < 2) throw new InvalidOperationException("複合属性攻撃には2つ以上の属性を指定してください。");
            return Content(EffectContentType.SkillAttack, new[] { target, "ElementalWeapon", m.Groups[3].Value, m.Groups[4].Value }.Concat(elements).ToArray());
        }
        m = M(s, @"^(自身|対象|付与者)(?:1体)?を(?:([0-9]+)回連続で)?(?:スキル値)?(.+?)で武器攻撃する。?(?:防御点はそれぞれに適用される。?)?$");
        if (m.Success)
        {
            var p = new List<string> { ActorKey(m.Groups[1].Value), m.Groups[3].Value };
            if (m.Groups[2].Success) { p.Add(m.Groups[2].Value); p.Add("Default"); }
            if (s.Contains("防御点はそれぞれに適用される"))
            {
                if (!m.Groups[2].Success) throw new InvalidOperationException("防御点の個別適用には連続攻撃回数が必要です。");
                p[3] = "PerHit";
            }
            return Content(EffectContentType.WeaponAttack, p.ToArray());
        }
        m = M(s, @"^(自身|対象|付与者)(?:1体)?を(.+?)の威力で攻撃する。?$");
        if (m.Success) return Content(EffectContentType.SkillAttack, ActorKey(m.Groups[1].Value), m.Groups[2].Value);
        m = M(s, @"^ターン中に発動した[、,]\s*全てのバフによる効果を([0-9]+)回まで適用しなおす。?$");
        if (m.Success) return Content(EffectContentType.ReapplyEffects, "Turn", "バフ", "All", m.Groups[1].Value);
        m = M(s, @"^このエリアの採取を([0-9]+)回追加で行うことができる。?$");
        if (m.Success) return Content(EffectContentType.AddAreaGathering, Number(m.Groups[1].Value, 1, "追加採取回数").ToString());
        m = M(s, @"^追加で(.+?)のダイスロールを行う。?$");
        if (m.Success) return Content(EffectContentType.RollDice, "", m.Groups[1].Value);
        m = M(s, @"^追加ロール([0-9]+)として(.+?)のダイスロールを行う。?$");
        if (m.Success) return Content(EffectContentType.RollDice, m.Groups[1].Value, m.Groups[2].Value);
        m = M(s, @"^ペット《([^》]+)》を召喚する。?$");
        if (m.Success) return Content(EffectContentType.Summon, m.Groups[1].Value);
        m = M(s, @"^(自身|対象|付与者)の([A-Z]+)を([+-].+?)(?:減少|変化)させる。?$");
        if (m.Success) return Content(EffectContentType.ModifyResource, ActorKey(m.Groups[1].Value), m.Groups[2].Value, Signed(m.Groups[3].Value));
        m = M(s, @"^(自身|対象|付与者)の((?:〈[^〉]+〉)(?:(?:及び|または)〈[^〉]+〉)*)を無効にする。?$");
        if (m.Success)
        {
            bool hasAnd = m.Groups[2].Value.Contains("及び"), hasOr = m.Groups[2].Value.Contains("または");
            if (hasAnd && hasOr) throw new InvalidOperationException("無効にする行動カテゴリーで「及び」と「または」は混在できません。");
            string connector = hasOr ? "Or" : hasAnd ? "And" : "Single";
            return Content(EffectContentType.InvalidateAction, new[] { ActorKey(m.Groups[1].Value), connector }.Concat(AllMatches(m.Groups[2].Value, @"〈([^〉]+)〉").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray());
        }
        m = M(s, @"^(?:(自身|対象|付与者)の)?被ダメ(?:ー|―)?ジを(.+?)軽減する。?$");
        if (m.Success) return Content(EffectContentType.ReduceDamage, m.Groups[1].Success ? ActorKey(m.Groups[1].Value) : "Target", m.Groups[2].Value);
        m = M(s, @"^消費した〈([^〉]+)〉記載の消費([A-Z]+)を支払わずにアクティブ効果を適用する。?$");
        if (m.Success) return Content(EffectContentType.ApplyConsumedItemActive, m.Groups[1].Value, m.Groups[2].Value);
        m = M(s, @"^(自身|対象|付与者)は《([^》]+)》スタックを(.+?)得る。?[（(]最大([0-9]+)スタック[）)]。?$");
        if (m.Success) return Content(EffectContentType.GainStack, ActorKey(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value);
        m = M(s, @"^(自身|対象|付与者)は(.+?)(?:の)?ダメージを受ける。?$");
        if (m.Success) return Content(EffectContentType.Damage, ActorKey(m.Groups[1].Value), m.Groups[2].Value);
        m = M(s, @"^(自身|対象|付与者)への((?:〈[^〉]+〉)(?:(?:及び|または)〈[^〉]+〉)*)を無効にする。?$");
        if (m.Success) return Content(EffectContentType.InvalidateIncomingAction, new[] { ActorKey(m.Groups[1].Value) }.Concat(AllMatches(m.Groups[2].Value, @"〈([^〉]+)〉").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray());
        throw new InvalidOperationException("未対応のスキル効果です：" + s);
    }
    private static bool ValidConditions(ConditionSet c)
    {
        return c != null && c.And != null && c.Or != null && c.And.Concat(c.Or).All(x => x != null && x.Parameters != null);
    }
    private static bool ValidTrigger(TriggerDefinition t) { return t != null && ValidConditions(t.Conditions); }
    private static string[] AttackArgs(EffectContent content)
    {
        string[] p = VariableArgs(content.Parameters, 2, "WeaponAttack");
        if (p.Length != 2 && p.Length != 4) throw new InvalidOperationException("WeaponAttackは対象・スキル値の2個、または対象・スキル値・連続回数・防御指定の4個です。スパイクはOverridesに指定してください。");
        Actor(p[0]);
        if (p.Length >= 3) Number(p[2], 1, "攻撃回数");
        if (p.Length == 4 && p[3] != "PerHit" && p[3] != "Default") throw new InvalidOperationException("防御指定はPerHit（各回適用）/ Default（ゲーム規則に従う）です。");
        return p;
    }
    private static string SkillContentText(EffectContent content)
    {
        if (content == null) throw new InvalidOperationException("効果内容がnullです。");
        string passiveText;
        if (TryPassiveContentText(content, out passiveText)) return passiveText;
        string[] p;
        switch (content.Type)
        {
            case EffectContentType.RestoreResource:
                p = Args(content.Parameters, 3, "RestoreResource");
                if (p[0] == "AllAllies") return "味方キャラクター全員の" + p[1] + "を" + p[2] + "回復する。";
                return Actor(p[0]) + "は" + p[1] + "を" + p[2] + "回復する。";
            case EffectContentType.ApplyState:
                p = VariableArgs(content.Parameters, 2, "ApplyState");
                if (p.Length > 3) throw new InvalidOperationException("ApplyStateは対象・状態名・ターン数（任意）の2～3個です。");
                if (p.Length == 3) Number(p[2], 1, "持続ターン");
                return TargetText(p[0]) + "は" + (p.Length == 3 ? p[2] + "ターンの間" : "") + "《" + p[1] + "》状態になる。";
            case EffectContentType.ConsumeResource:
                p = Args(content.Parameters, 3, "ConsumeResource"); Actor(p[0]); return p[0] == "Self" ? p[1] + "-" + p[2] : Actor(p[0]) + "は" + p[1] + "を" + p[2] + "消費する。";
            case EffectContentType.JoinMeleeGroup:
                p = VariableArgs(content.Parameters, 3, "JoinMeleeGroup");
                if (p[0] != "Self" || p[1] != "Target") throw new InvalidOperationException("今回の接近文型はSelf / Targetの組み合わせです。");
                return "自身と対象は同じ接近グループの" + string.Join("と", p.Skip(2).Select(x => "《" + x + "》")) + "状態になる。";
            case EffectContentType.ApplyStateInMeleeGroup:
                p = Args(content.Parameters, 2, "ApplyStateInMeleeGroup"); return Actor(p[0]) + "は同じ接近グループの《" + p[1] + "》状態になる。";
            case EffectContentType.WeaponAttack:
                p = AttackArgs(content);
                return Actor(p[0]) + "を" + (p.Length >= 3 ? p[2] + "回連続で" : "") + "スキル値" + p[1] + "で武器攻撃する。" + (p.Length == 4 && p[3] == "PerHit" ? "防御点はそれぞれに適用される。" : "");
            case EffectContentType.SkillAttack:
                p = VariableArgs(content.Parameters, 2, "SkillAttack");
                if (p.Length == 2) return Actor(p[0]) + "を" + p[1] + "の威力で攻撃する。";
                if (p.Length >= 6 && p[1] == "ElementalWeapon")
                    return TargetText(p[0]) + "をスキル値" + p[2] + "の武器威力+" + p[3] + "の" + string.Join("かつ", p.Skip(4)) + "属性威力で攻撃する。";
                throw new InvalidOperationException("SkillAttackは対象・威力、または対象・ElementalWeapon・スキル値・属性威力式・2属性以上です。");
            case EffectContentType.ModifyResource:
                p = Args(content.Parameters, 3, "ModifyResource"); return Actor(p[0]) + "の" + p[1] + "を" + Signed(p[2]) + "変化させる。";
            case EffectContentType.InvalidateAction:
                p = VariableArgs(content.Parameters, 3, "InvalidateAction");
                if (p[1] == "Single" && p.Length == 3) return Actor(p[0]) + "の" + CategoryAlternatives(p.Skip(2), "及び") + "を無効にする。";
                if ((p[1] == "And" || p[1] == "Or") && p.Length >= 4) return Actor(p[0]) + "の" + CategoryAlternatives(p.Skip(2), p[1] == "And" ? "及び" : "または") + "を無効にする。";
                throw new InvalidOperationException("InvalidateActionの接続はSingle（1件）/ And / Or（2件以上）です。");
            case EffectContentType.ReduceDamage:
                p = Args(content.Parameters, 2, "ReduceDamage"); Actor(p[0]); return (p[0] == "Target" ? "" : Actor(p[0]) + "の") + "被ダメージを" + p[1] + "軽減する。";
            case EffectContentType.ApplyConsumedItemActive:
                p = Args(content.Parameters, 2, "ApplyConsumedItemActive"); return "消費した〈" + p[0] + "〉記載の消費" + p[1] + "を支払わずにアクティブ効果を適用する。";
            case EffectContentType.GainStack:
                p = Args(content.Parameters, 4, "GainStack"); Number(p[3], 1, "最大スタック数"); return Actor(p[0]) + "は《" + p[1] + "》スタックを" + p[2] + "得る。（最大" + p[3] + "スタック）";
            case EffectContentType.Damage:
                p = Args(content.Parameters, 2, "Damage"); return Actor(p[0]) + "は" + p[1] + "のダメージを受ける。";
            case EffectContentType.InvalidateIncomingAction:
                p = VariableArgs(content.Parameters, 2, "InvalidateIncomingAction"); return Actor(p[0]) + "への" + CategoryAlternatives(p.Skip(1), "及び") + "を無効にする。";
            case EffectContentType.ReapplyEffects:
                p = Args(content.Parameters, 4, "ReapplyEffects"); Number(p[3], 1, "再適用回数");
                if (p[0] != "Turn" || p[1] != "バフ" || p[2] != "All") throw new InvalidOperationException("今回の再適用はTurn / バフ / Allです。");
                return "ターン中に発動した、全てのバフによる効果を" + p[3] + "回まで適用しなおす。";
            case EffectContentType.RollDice:
                p = Args(content.Parameters, 2, "RollDice"); Number(p[0], 1, "追加ロール番号");
                return "追加ロール" + p[0] + "として" + p[1] + "のダイスロールを行う。";
            case EffectContentType.Summon:
                p = Args(content.Parameters, 1, "Summon"); return "ペット《" + p[0] + "》を召喚する。";
            case EffectContentType.AddAreaGathering:
                p = Args(content.Parameters, 1, "AddAreaGathering");
                return "このエリアの採取を" + Number(p[0], 1, "追加採取回数") + "回追加で行うことができる。";
            default: throw new InvalidOperationException("通常のスキル効果には使用できない種別です：" + content.Type);
        }
    }
    private static string SkillTriggerText(EffectType type, List<TriggerDefinition> triggers)
    {
        if (triggers == null) throw new InvalidOperationException("Triggersがnullです。");
        string passiveText;
        if (TryPassiveTriggerText(type, triggers, out passiveText)) return passiveText;
        if (triggers.Count == 0) return "";
        if (triggers.Count != 1 || !ValidTrigger(triggers[0])) throw new InvalidOperationException("今回のスキル効果文は、個別トリガー1個に対応します。");
        TriggerDefinition t = triggers[0]; var c = t.Conditions;
        if (t.Timing == TriggerTiming.RandomResult && c.Or.Count == 0 && c.And.Count == 1 && c.And[0].Type == ConditionType.RollResult)
        {
            string[] p = Args(c.And[0].Parameters, 2, "RollResult"); Number(p[0], 1, "追加ロール番号"); Number(p[1], 1, "出目");
            return "追加ロール" + p[0] + "の出目が" + p[1] + "の時、";
        }
        if (c.Or.Count == 0 && c.And.Count == 1 && c.And[0].Type == ConditionType.AttackAppliedState && type == EffectType.Active && t.Timing == TriggerTiming.AfterWeaponAttack)
        {
            string[] p = Args(c.And[0].Parameters, 2, "AttackAppliedState"); return "この攻撃で" + Actor(p[0]) + "を《" + p[1] + "》状態にした時、";
        }
        if (type == EffectType.Declaration && t.Timing == TriggerTiming.AfterSkillResolution && c.Or.Count == 0 && c.And.Count == 2 && c.And[0].Type == ConditionType.BattleEffectLimit && c.And[1].Type == ConditionType.SkillDealtNoDamage)
        {
            string[] limit = Args(c.And[0].Parameters, 1, "BattleEffectLimit"); Number(limit[0], 1, "戦闘中の適用回数");
            string[] target = Args(c.And[1].Parameters, 1, "SkillDealtNoDamage");
            if (target[0] != "Target") throw new InvalidOperationException("この宣言効果の対象はTargetです。");
            return "この宣言効果は戦闘中" + limit[0] + "回まで、このスキルによって対象にダメージを与えられなかった場合、";
        }
        throw new InvalidOperationException("今回の文型に対応しないスキル効果トリガーです。");
    }
    private static string OverrideText(OverrideDefinition effect, OverrideContent content)
    {
        if (effect.Triggers == null || effect.Triggers.Any(x => !ValidTrigger(x)) || content == null) throw new InvalidOperationException("上書き効果が不正です。");
        string passiveText;
        if (TryPassiveOverrideText(effect, content, out passiveText)) return passiveText;
        var triggers = effect.Triggers; string[] p;
        switch (content.Type)
        {
            case OverrideContentType.SetSkillValue:
                p = Args(content.Parameters, 1, "SetSkillValue");
                if ((effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike) && triggers.Count == 0)
                    return "このアクティブ効果によるスキル値は" + p[0] + "に変化する。";
                if ((effect.Type == EffectType.Active || effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike) && triggers.Count == 1 && triggers[0].Timing == TriggerTiming.SkillValue && triggers[0].Conditions.Or.Count == 0 && triggers[0].Conditions.And.Count == 1 && triggers[0].Conditions.And[0].Type == ConditionType.OwnState)
                {
                    string state = Args(triggers[0].Conditions.And[0].Parameters, 1, "OwnState")[0];
                    if (!Matches(triggers, SkillValueWhenOwnState(state).ToArray())) break;
                    return "自身が《" + state + "》状態の時、" + (effect.Type == EffectType.Active ? "この" : "このアクティブ効果による") + "スキル値は" + p[0] + "に変化する。";
                }
                break;
            case OverrideContentType.MultiplyPower:
                p = Args(content.Parameters, 1, "MultiplyPower");
                if (triggers.Count == 0) return "威力は×" + p[0] + "倍される。";
                if (effect.Type == EffectType.Passive && triggers.Count == 1)
                {
                    ConditionEntry weaponCondition = triggers[0].Conditions.And.FirstOrDefault(x => x.Type == ConditionType.WeaponCategory);
                    if (weaponCondition != null)
                    {
                        string weaponCategory = Args(weaponCondition.Parameters, 1, "WeaponCategory")[0];
                        if (Matches(triggers, SkillWeaponPowerTriggers(weaponCategory).ToArray()))
                            return "スキルによって〈" + weaponCategory + "〉で武器攻撃を行う時、威力が×" + p[0] + "倍される。";
                    }
                }
                break;
            case OverrideContentType.SetResourceCost:
                p = Args(content.Parameters, 2, "SetResourceCost"); if (triggers.Count != 0) break; return p[0] + "の消費は" + p[1] + "になる。";
            case OverrideContentType.SetCooldown:
                p = Args(content.Parameters, 1, "SetCooldown"); if (triggers.Count != 0) break; return "クールタイムは" + p[0] + "になる。";
            case OverrideContentType.SetCritical:
                p = Args(content.Parameters, 1, "SetCritical");
                if (p[0] != "True" || !Matches(triggers, Trigger(TriggerTiming.AfterActivationRoll, Condition(ConditionType.RollSucceeded)))) break;
                return "この攻撃の発動ロールに成功した時、クリティカルとして扱う。";
            case OverrideContentType.AddStateDuration:
                p = Args(content.Parameters, 2, "AddStateDuration"); if (p[0] != "SelectedState" || triggers.Count != 0) break;
                return "選択した状態の持続ターンは" + Signed(p[1]) + "される。";
            case OverrideContentType.AddResourceCost:
                p = Args(content.Parameters, 4, "AddResourceCost");
                if (p[0] != "Self") break;
                if (p[3] == "Optional" && Matches(triggers, Trigger(TriggerTiming.ResourceCost))) return "自身の消費" + p[1] + "は" + Signed(p[2]) + "される。（任意）";
                if (p[3] == "Automatic" && triggers.Count == 0) return "消費" + p[1] + Signed(p[2]) + "。";
                break;
            case OverrideContentType.AddTargetValue:
                p = Args(content.Parameters, 1, "AddTargetValue");
                if (triggers.Count != 2) break;
                var weapon = triggers.FirstOrDefault(t => t.Conditions.And.Any(c => c.Type == ConditionType.WeaponCategory));
                var action = triggers.FirstOrDefault(t => t.Conditions.And.Any(c => c.Type == ConditionType.ActionCategory));
                if (weapon == null || action == null) break;
                var equipped = weapon.Conditions.And.FirstOrDefault(c => c.Type == ConditionType.EquippedCategory);
                if (equipped == null) break;
                string[] e = Args(equipped.Parameters, 2, "EquippedCategory");
                string w = Args(weapon.Conditions.And.First(c => c.Type == ConditionType.WeaponCategory).Parameters, 1, "WeaponCategory")[0];
                string a = Args(action.Conditions.And.First(c => c.Type == ConditionType.ActionCategory).Parameters, 1, "ActionCategory")[0];
                if (e[0] != "Self" || !Matches(triggers, MasteryTriggers(e[1], w, a).ToArray())) break;
                return "自身は〈" + e[1] + "〉を装備している場合、〈" + w + "〉による武器攻撃または〈" + a + "〉の目標値は" + Signed(p[0]) + "される。";
            case OverrideContentType.PreventCounterDamage:
                p = Args(content.Parameters, 2, "PreventCounterDamage");
                if (effect.Type != EffectType.Active || triggers.Count != 0) break;
                return "この攻撃は〈" + p[1] + "〉によるカウンター効果で" + Actor(p[0]) + "にダメージを与えることはできない。";
            case OverrideContentType.SetDamageReduction:
                p = Args(content.Parameters, 1, "SetDamageReduction");
                if ((effect.Type != EffectType.SecondSpike && effect.Type != EffectType.ThirdSpike) || triggers.Count != 0) break;
                return "このカウンター効果による軽減は" + p[0] + "に変化する。";
            case OverrideContentType.SetAttackComponent:
                p = Args(content.Parameters, 2, "SetAttackComponent");
                if (p[0] == "AttributePower" && (effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike) && triggers.Count == 0)
                    return "このアクティブ効果による属性威力は" + p[1] + "に変化する。";
                break;
            case OverrideContentType.SetActivationRollFormula:
                p = Args(content.Parameters, 1, "SetActivationRollFormula");
                if ((effect.Type != EffectType.SecondSpike && effect.Type != EffectType.ThirdSpike) || triggers.Count != 0) break;
                string formula = Need(p[0], "発動ロール式");
                if (M(formula, @"\s+目標値").Success) throw new InvalidOperationException("スパイクの発動ロール式に空白＋「目標値」は使用できません。");
                return "この発動ロールは" + formula + "に変化する。";
            case OverrideContentType.MultiplyAttackComponent:
                p = Args(content.Parameters, 2, "MultiplyAttackComponent");
                if (p[0] != "AttributePower") break;
                if (triggers.Count == 0) return "属性威力は×" + p[1] + "倍される。";
                if (effect.Type == EffectType.Active && Matches(triggers, Trigger(TriggerTiming.ElementalPower, Condition(ConditionType.ResourceRatio, "Target", "HP", "AtMost", "0.5"))))
                    return "対象のHPが最大値の半分以下なら属性威力は×" + p[1] + "倍される。";
                break;
            case OverrideContentType.SetAttackRule:
                p = VariableArgs(content.Parameters, 2, "SetAttackRule");
                if (effect.Type != EffectType.Active || triggers.Count != 0) break;
                if (p.Length == 2 && p[0] == "Defense" && p[1] == "Ignore") return "この攻撃は対象の防御点の影響を受けない。";
                if (p.Length == 3 && p[0] == "Response" && p[2] == "Prohibit") return "このスキルを対象に〈" + p[1] + "〉を宣言する事は出来ない。";
                break;
        }
        throw new InvalidOperationException("未対応のスキル上書き効果の組み合わせです：" + effect.Type + " / " + content.Type);
    }
}
