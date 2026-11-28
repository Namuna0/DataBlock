using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    private static string SummonName(EffectContent content)
    {
        var p = VariableArgs(content.Parameters, 1, "召喚");
        Require(p.Length == 1 || (content.Type == EffectContentType.Summon && p.Length == 2 && Number(p[1], 1, "召喚数") > 0));
        return Need(p[0], "召喚名");
    }

    private static string NormalizeEffectSentence(string text)
    {
        text = NormalizeCombatEffect(text);
        text = RegexReplace(text, @"^その後、(?=自身は|対象は|対象を)", "");
        text = RegexReplace(text, @"^対象は永続的に《([^》]+)》状態になる。?$", "対象を永続的に《$1》状態にする。");
        text = RegexReplace(text, @"^(?:攻撃)?対象を(?:スキル値)?([0-9]+)の武器威力で攻撃する。?$", "対象をスキル値$1で武器攻撃する。");
        text = RegexReplace(text, @"^([A-Z]+)ダメージを([^。]+)与える。?$", "対象に$2の$1ダメージを与える。");
        text = RegexReplace(text, @"^対象へ《([^》]+)》スタックを([0-9]+)付与する、$", "対象に《$1》スタックを$2付与する。");
        text = RegexReplace(text, @"^対象に《([^》]+)》スタック([0-9]+)を付与する。?$", "対象に《$1》スタックを$2付与する。");
        text = RegexReplace(text, @"^対象へ《([^》]+)》スタック×([0-9]+)を付与する。?$", "対象に《$1》スタックを$2付与する。");
        text = RegexReplace(text, @"^(自身は《[^》]+》スタックを[0-9]+得る。)（最大([0-9]+)つ）$", "$1（最大$2スタック）");
        text = RegexReplace(text, @"^回復量×([0-9.]+)倍。?$", "回復量は×$1倍される。");
        text = RegexReplace(text, @"^種族能力値ボーナスの([^。]+?)(-[0-9]+)%減算される。?$", "種族能力値ボーナスの$1$2%加算される。");
        text = RegexReplace(text, @"^対象を([^。]+)の威力でする。?$", "対象を$1の威力で攻撃する。");
        text = RegexReplace(text, @"^対象(?:へ|に)(" + ArithmeticPattern + @")(?:の)?ダメージを与える。?$", "対象は$1のダメージを受ける。");
        text = RegexReplace(text, @"^攻撃対象を(?=スキル値)", "対象を");
        text = RegexReplace(text, @"^選択対象を(?=スキル値)", "対象を");
        text = RegexReplace(text, @"^(対象を[0-9]+ターンの間)、", "$1");
        text = RegexReplace(text, @"^対象は([0-9]+)ターンの間、対象は", "対象は$1ターンの間");
        text = RegexReplace(text, @"^《([^》]+)》ペットを召喚する。?$", "ペット《$1》を召喚する。");
        text = RegexReplace(text, @"^《([^》]+)》スタックを([0-9]+)得る。?$", "自身に《$1》スタックを$2付与する。");
        text = RegexReplace(text, @"^対象を\(スキル値([0-9]+)\)\*([0-9.]+)の武器威力で([0-9]+)連続攻撃する。?$", "対象を(スキル値$1)*$2の武器威力で$3回連続攻撃する。");
        text = RegexReplace(text, @"^対象に《([^》]+)》を付与する。?$", "対象に《$1》状態を付与する。");
        text = RegexReplace(text, @"^このカウンター効果による被ダメージは([^。]+)軽減に変化する。?$", "このカウンター効果による軽減は$1に変化する。");
        text = RegexReplace(text, @"^このパッシブ効果による威力上昇は×", "このパッシブ効果による威力上昇は");
        text = RegexReplace(text, @"^対象の([A-Z]+)を([+-][0-9]+)する。?$", "対象の$1を$2変化させる。");
        text = RegexReplace(text, @"^対象の([A-Z]+)がさらに(-[0-9]+)減少する。?$", "対象の$1を$2変化させる。");
        text = RegexReplace(text, @"^対象は([A-Z]+)を([0-9]+)失う。?$", "対象の$1を-$2変化させる。");
        text = RegexReplace(text, @"^自身の([A-Z]+)([+-][0-9]+)$", "自身の$1を$2変化させる。");
        text = RegexReplace(text, @"^対象を\(スキル値([0-9]+)\)\*([0-9.]+)で回復する。?$", "対象を[スキル値$1]*$2で回復する。");
        text = RegexReplace(text, @"^その後(?=\[スキル値)", "対象を");
        text = RegexReplace(text, @"^対象をスキル値([0-9]+)の武器威力×([0-9.]+)で攻撃する。?$", "対象を(スキル値$1の武器威力*$2)で攻撃する。");
        text = RegexReplace(text, @"^(対象をスキル値[0-9]+)の武器攻撃\+", "$1の武器威力+");
        text = RegexReplace(text, @"^自身が(〈[^〉]+〉)を宣言するたびに、([A-Z]+)が-([0-9]+)減少する", "自身は$1を宣言するたびに、$2が$3減少する");
        text = RegexReplace(text, @"^続けて対象をスキル値([0-9]+)で([0-9]+)回攻撃連続武器攻撃する", "対象を$2回連続でスキル値$1で武器攻撃する");
        text = RegexReplace(text, @"^自身の次のターン開始時、(《[^》]+》状態になる)", "自身は次のターン開始時$1");
        text = RegexReplace(text, @"^対象の([A-Z]+)を([0-9]+)回復する、。?$", "対象の$1を+$2変化させる。");
        text = RegexReplace(text, @"^([A-Z]+)(?:は|を)([0-9]+)(?:得る|回復する)。(?!.)", "$1を$2回復する。");
        text = RegexReplace(text, @"^(?:さらに[、]?\s*)?自身に付与されている(〈[^〉]+〉)状態は全て解除される。?$", "自身に付与されている$1状態を全て解除する。");
        text = RegexReplace(text, @"^自身に付与されている状態(〈[^〉]+〉)を解除する", "自身に付与されている$1状態を全て解除する");
        text = RegexReplace(text, @"^《([^》]+)》スタックを([0-9]+)付与する。?$", "対象に《$1》スタックを$2付与する。");
        text = text.Replace("このエリアを移動するまでの間", "このエリアを移動するまで");
        text = RegexReplace(text, @"^自身は自身に《([^》]+)》スタックを(.+?)得る。?$", "自身に《$1》スタックを$2付与する。");
        return text;
    }

    private static void ReadSkillLine(SkillBody skill, EffectType type, string text)
    {
        string statement = Unbullet(text);
        if (type != EffectType.Roleplay) statement = NormalizeEffectSentence(statement);
        var inlineRoll = M(statement, @"^(?:さらに)?ダイスロール([1-9][0-9]*d[1-9][0-9]*)を行い、([0-9]+(?:～[0-9]+)?)が出た場合、(.+)$");
        if (IsOrdinary(type) && inlineRoll.Success)
        {
            ReadSkillLine(skill, type, inlineRoll.Groups[1].Value + "のダイスロールを行う。");
            ReadSkillLine(skill, type, "- " + inlineRoll.Groups[2].Value + "：" + inlineRoll.Groups[3].Value);
            return;
        }
        var recover = M(statement, @"^毎ターン開始時、自身の([A-Z]+)と([A-Z]+)は(" + ArithmeticPattern + @")回復する。?$");
        if (IsOrdinary(type) && recover.Success)
        {
            foreach (var resource in new[] { recover.Groups[1].Value, recover.Groups[2].Value })
                ReadSkillLine(skill, type, "毎ターン開始時、自身の" + resource + "を" + recover.Groups[3].Value + "回復する。");
            return;
        }
        if (type == EffectType.Passive && statement.TrimEnd('。') == "自身への状態付与を無効にする")
            statement = "自身の状態を無効にする。";
        var categoryStates = M(statement, @"^〈([^〉]+)〉は直ちに《([^》]+)》かつ《([^》]+)》状態となる。?$");
        if (type == EffectType.Passive && categoryStates.Success)
        {
            foreach (var state in new[] { categoryStates.Groups[2].Value, categoryStates.Groups[3].Value })
                AddSkillEffect(skill, type, Content(EffectContentType.ApplyImmediateStateToCategory, categoryStates.Groups[1].Value, state));
            return;
        }
        var pair = M(statement, @"^対象を《([^》]+)》(?:かつ|と)《([^》]+)》状態にする。?$");
        if (IsOrdinary(type) && pair.Success)
        {
            ReadSkillLine(skill, type, "対象を《" + pair.Groups[1].Value + "》状態にする。");
            ReadSkillLine(skill, type, "対象を《" + pair.Groups[2].Value + "》状態にする。");
            return;
        }
        var timedAndRemoved = M(statement, @"^対象に([1-9][0-9]*)ターンの間《([^》]+)》状態を付与して《([^》]+)》状態を解除させる。?$");
        if (IsOrdinary(type) && timedAndRemoved.Success)
        {
            ReadSkillLine(skill, type, "対象に" + timedAndRemoved.Groups[1].Value + "ターンの間《" + timedAndRemoved.Groups[2].Value + "》状態を付与する。");
            ReadSkillLine(skill, type, "対象の《" + timedAndRemoved.Groups[3].Value + "》状態を解除する。");
            return;
        }
        if (type == EffectType.Passive && M(statement, @"^[A-Z]+最大値[+-][0-9]+,\s*自身は").Success)
        {
            foreach (string clause in Split(statement, ",")) ReadSkillLine(skill, type, clause);
            return;
        }
        if (IsSpikeStage(type))
        {
            Match cost = M(statement, @"^このスキルによる消費リソースは-([0-9]+)に変化する。?$");
            if (cost.Success)
            {
                if (skill.Costs.Count != 1 || skill.Costs[0].Kind != CostKind.Resource)
                    throw new InvalidOperationException("消費リソースを省略できるのは通常リソースのコストが1つだけの時です。");
                statement = skill.Costs[0].Resource + "の消費が" + cost.Groups[1].Value + "になる。";
            }
        }
        // Some full rule sentences begin with さらに; read those before interpreting it as a conjunction.
        if (statement.StartsWith("さらに", StringComparison.Ordinal) && IsOrdinary(type) &&
            CommonEffectTextCodec.TryReadRuleSentence(statement, out var wholeRule))
        {
            AddSkillEffect(skill, type, wholeRule);
            return;
        }
        if (ReadSkillStatement(skill, type, statement)) return;
        if (type != EffectType.Roleplay && statement.TrimEnd('。').Contains("。"))
        {
            if (IsOrdinary(type) && ReadSharedEffect(skill, type, statement, NoTriggers())) return;
            var sentences = Split(statement, "。");
            if (sentences.Count > 1)
            {
                foreach (string sentence in sentences) ReadSkillLine(skill, type, sentence);
                return;
            }
        }
        foreach (string part in Split(statement, "・"))
        {
            string body = RegexReplace(part.Trim(), @"^さらに\s*", "");
            if (type == EffectType.SecondSpike || type == EffectType.ThirdSpike)
            {
                body = RegexReplace(body, @"^対象をスキル値([^。]+)で武器攻撃する。?$", "このアクティブ効果によるスキル値は$1に変化する。");
                body = RegexReplace(body, @"^このアクティブ効果に(?:よる)?武器威力は(?:スキル値)?([^。]+?)力?に変化する。?$", "このアクティブ効果によるスキル値は$1に変化する。");
                body = RegexReplace(body, @"(によるスキル値は[0-9]+)変化する。?$", "$1に変化する。");
                if (TryReadPassiveSpike(skill, type, body)) continue;
                Match combinedSpike = M(body, @"^このアクティブ効果によるスキル値(?:は)?([^、]+)、属性威力(?:は)?(.+?)に変化する。?$");
                if (!combinedSpike.Success) combinedSpike = M(body, @"^このアクティブ効果によるスキル値(?:は)?(.+?)の武器威力\+(.+?)に変化する。?$");
                if (combinedSpike.Success)
                {
                    AddOverride(skill.Overrides, type, NoTriggers(),
                        Change(EffectContentType.SetSkillValue, combinedSpike.Groups[1].Value),
                        Change(EffectContentType.SetAttackComponent, "AttributePower", combinedSpike.Groups[2].Value));
                    continue;
                }
                Match spike = M(body, @"^このアクティブ効果によるスキル値は(.+?)に変化する。?$");
                if (spike.Success)
                {
                    AddOverride(skill.Overrides, type, NoTriggers(), Change(EffectContentType.SetSkillValue, spike.Groups[1].Value));
                    continue;
                }
                spike = M(body, @"^自身が《([^》]+)》状態の時[、,]\s*(?:(?:このアクティブ効果による)?スキル値は)?(.+?)に変化する。?$");
                if (spike.Success)
                {
                    AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.SkillValue, Condition(ConditionType.OwnState, spike.Groups[1].Value))), Change(EffectContentType.SetSkillValue, spike.Groups[2].Value));
                    continue;
                }
                spike = M(body, @"^この(?:アクティブ|カウンター)効果による軽減は(.+?)に変化する。?$");
                if (spike.Success)
                {
                    AddOverride(skill.Overrides, type, NoTriggers(), Change(EffectContentType.SetDamageReduction, spike.Groups[1].Value));
                    continue;
                }
                spike = M(body, @"^このアクティブ効果による属性威力は(.+?)に変化する。?$");
                if (spike.Success)
                {
                    AddOverride(skill.Overrides, type, NoTriggers(), Change(EffectContentType.SetAttackComponent, "AttributePower", spike.Groups[1].Value));
                    continue;
                }
                spike = M(body, @"^この発動ロールは(.+?)に変化する。?$");
                if (spike.Success)
                {
                    AddOverride(skill.Overrides, type, NoTriggers(), Change(EffectContentType.SetActivationRollFormula, spike.Groups[1].Value));
                    continue;
                }
                if (skill.Effects.Any(e => e.Type == EffectType.Roleplay) &&
                    M(body, @"(?:にまで及ぶ(?:こともある|かもしれない)|(?:知識|洞察)を得る|分析を行う|深い造詣を持つ|並ぶものは居ない)。$").Success)
                {
                    AddOverride(skill.Overrides, type, NoTriggers(), Change(EffectContentType.SetRoleplayDescription, body));
                    continue;
                }
                throw new InvalidOperationException("スパイクはスキル値・属性威力・被ダメージ軽減値・発動ロールの変更に対応します。");
            }
            if (!IsOrdinary(type)) throw new InvalidOperationException("効果種別を指定してください。");
            if (TryReadPassiveSkillLine(skill, type, body)) continue;
            var triggers = NoTriggers();
            Match branch = M(body, @"^[-－]\s*([0-9]+(?:[～〜][0-9]+)?)[：:]\s*(.+)$");
            if (branch.Success)
            {
                EffectContent roll = skill.Effects.Where(x => x.Type == type).SelectMany(x => x.Contents).LastOrDefault(x => x.Type == EffectContentType.RollDice);
                if (roll == null) throw new InvalidOperationException("出目分岐の前に追加ダイスロールを指定してください。");
                string[] rollArgs = Args(roll.Parameters, 2, "RollDice");
                triggers.Add(Trigger(TriggerTiming.RandomResult, Condition(ConditionType.RollResult, rollArgs[0], branch.Groups[1].Value.Replace('〜', '～'))));
                body = branch.Groups[2].Value;
            }
            else
            {
                branch = M(body, @"^追加ロール([0-9]+)の出目が([0-9]+(?:～[0-9]+)?)の時[、,]\s*(.+)$");
                if (branch.Success)
                {
                    triggers.Add(Trigger(TriggerTiming.RandomResult, Condition(ConditionType.RollResult, branch.Groups[1].Value, branch.Groups[2].Value)));
                    body = branch.Groups[3].Value;
                }
            }
            if (triggers.Count > 0)
            {
                int before = skill.Effects.Count;
                ReadSkillLine(skill, type, body);
                var added = skill.Effects.Skip(before).ToList();
                if (added.Count == 0 || added.Any(effect => effect.Triggers.Count != 0))
                    throw new InvalidOperationException("追加ロールの出目分岐には通常の効果を指定してください。");
                foreach (var effect in added) effect.Triggers = new List<TriggerDefinition>(triggers);
                continue;
            }
            if (ReadSkillOverride(skill, type, body)) continue;
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
            var atoms = Split(body, ",");
            var direct = atoms.Count == 1 ? ReadDirectEffectContent(body) : null;
            if (direct == null && ReadSharedEffect(skill, type, body, triggers)) continue;
            foreach (string atom in atoms)
            {
                if (atoms.Count > 1 && triggers.Count == 0 && ReadSkillStatement(skill, type, atom)) continue;
                if (M(atom, @"^対象1体を").Success && skill.DeclarationConditions.And.Any(c => c.Type == ConditionType.SelectCharacters || c.Type == ConditionType.SelectMeleeCharacters) && !skill.DeclarationConditions.And.Any(c => (c.Type == ConditionType.SelectCharacters || c.Type == ConditionType.SelectMeleeCharacters) && c.Parameters.SequenceEqual(new[] { "1", "Exact", "Default" })))
                    throw new InvalidOperationException("対象1体の攻撃には、ちょうど1体を選択する宣言条件を指定してください。");
                var effect = new EffectDefinition { Type = type, Triggers = triggers };
                EffectContent content = direct ?? ReadContent(atom);
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
    private static bool ReadSharedEffect(SkillBody skill, EffectType type, string body, List<TriggerDefinition> triggers)
    {
        if (!CommonEffectTextCodec.TryReadRuleContents(body, out var contents)) return false;
        if (type == EffectType.Passive && triggers.Count == 0 && contents.All(c => c.Type == EffectContentType.ModifyStat))
            AddOverride(skill.Overrides, type, triggers, contents.ToArray());
        else
            skill.Effects.Add(new EffectDefinition { Type = type, Triggers = triggers, Contents = contents });
        return true;
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
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(EffectContentType.MultiplyAttackComponent, "AttributePower", m.Groups[1].Value)); return true; }
        m = M(body, @"^対象のHPが最大値の半分以下なら属性威力(?:は|が)×?(.+?)倍される。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type,
                On(Trigger(TriggerTiming.ElementalPower, Condition(ConditionType.ResourceRatio, "Target", "HP", "AtMost", "0.5"))),
                Change(EffectContentType.MultiplyAttackComponent, "AttributePower", m.Groups[1].Value)); return true;
        }
        if (body.TrimEnd('。') == "この攻撃は対象の防御点の影響を受けない")
        {
            AddOverride(skill.Overrides, type, NoTriggers(), Change(EffectContentType.SetAttackRule, "Defense", "Ignore")); return true;
        }
        m = M(body, @"^このスキルを対象に〈([^〉]+)〉を宣言する事は出来ない。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type, NoTriggers(), Change(EffectContentType.SetAttackRule, "Response", m.Groups[1].Value, "Prohibit")); return true;
        }
        m = M(body, @"^威力(?:は|が)?×?(.+?)倍(?:される)?。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(EffectContentType.MultiplyPower, m.Groups[1].Value)); return true; }
        m = M(body, @"^自身が《([^》]+)》状態の時[、,]\s*(?:この(?:アクティブ効果による)?)?スキル値は(.+?)に変化する。?$");
        if (m.Success)
        {
            if (type != EffectType.Active) throw new InvalidOperationException("条件付きスキル値変更はアクティブ効果に指定してください。");
            AddOverride(skill.Overrides, type, SkillValueWhenOwnState(m.Groups[1].Value), Change(EffectContentType.SetSkillValue, m.Groups[2].Value)); return true;
        }
        m = M(body, @"^([A-Z]+)の消費(?:が|は)(.+?)になる。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(EffectContentType.SetResourceCost, m.Groups[1].Value, m.Groups[2].Value)); return true; }
        m = M(body, @"^クールタイム(?:が|は)(.+?)になる。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(EffectContentType.SetCooldown, m.Groups[1].Value)); return true; }
        if (body.TrimEnd('。') == "この攻撃の発動ロールに成功した時、確定でクリティカル扱いとなる" || body.TrimEnd('。') == "この攻撃の発動ロールに成功した時、クリティカルとして扱う")
        {
            AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.AfterActivationRoll, Condition(ConditionType.RollSucceeded))), Change(EffectContentType.SetCritical, "True")); return true;
        }
        m = M(body, @"^(?:選択した)?状態の持続ターン(?:を|は)([+-]?.+?)(?:加算する|される)。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(EffectContentType.AddStateDuration, "SelectedState", Signed(m.Groups[1].Value))); return true; }
        m = M(body, @"^自身の消費([A-Z]+)(?:を|は)([+-].+?)(?:する事が出来る|される。?[（(]任意[）)])。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, On(Trigger(TriggerTiming.ResourceCost)), Change(EffectContentType.AddResourceCost, "Self", m.Groups[1].Value, Signed(m.Groups[2].Value), "Optional")); return true; }
        m = M(body, @"^消費([A-Z]+)([+-].+?)。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(EffectContentType.AddResourceCost, "Self", m.Groups[1].Value, Signed(m.Groups[2].Value), "Automatic")); return true; }
        m = M(body, @"^この攻撃は〈([^〉]+)〉によるカウンター効果で(自身|対象|付与者)にダメージを与えることはできない。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, NoTriggers(), Change(EffectContentType.PreventCounterDamage, ActorKey(m.Groups[2].Value), m.Groups[1].Value)); return true; }
        m = M(body, @"^スキルによって〈([^〉]+)〉で武器攻撃を行う時[、,]\s*威力(?:が|は)×?(.+?)倍される。?$");
        if (m.Success) { AddOverride(skill.Overrides, type, SkillWeaponPowerTriggers(m.Groups[1].Value), Change(EffectContentType.MultiplyPower, m.Groups[2].Value)); return true; }
        m = M(body, @"^自身(?:が|は)〈([^〉]+)〉を装備している場合[、,]\s*〈([^〉]+)〉による武器攻撃(?:、?及び|または)〈([^〉]+)〉(?:による)?目標値(?:が|は)([+-].+?)される。?$");
        if (!m.Success) m = M(body, @"^自身(?:が|は)〈([^〉]+)〉を装備している場合[、,]\s*〈([^〉]+)〉による武器攻撃または〈([^〉]+)〉の目標値(?:が|は)([+-].+?)される。?$");
        if (m.Success)
        {
            AddOverride(skill.Overrides, type, MasteryTriggers(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value), Change(EffectContentType.AddTargetValue, Signed(m.Groups[4].Value))); return true;
        }
        return false;
    }
    private static EffectContent ReadContent(string text)
    {
        return ReadDirectEffectContent(text) ?? throw new InvalidOperationException("未対応の効果です：" + text);
    }

    internal static EffectContent ReadDirectEffectContent(string text)
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
        m = M(s, @"^(自身|対象|付与者)(?:へ|に|を)(?:([0-9]+)ターンの間)?《([^》]+)》(?>状態を付与する|状態にする|にする)。?$");
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
        m = M(s, @"^(対象|《([^》]+)》状態の全ての対象)をスキル値(.+?)の武器威力\+(.+?)の(.*?)属性威力で攻撃する。?$");
        if (m.Success)
        {
            string target = m.Groups[2].Success ? StateTarget(m.Groups[2].Value) : "Target";
            string[] elements = Split(m.Groups[5].Value, "かつ").ToArray();
            // No explicit element means the text only specifies an attribute formula.
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
        m = M(s, @"^(自身|対象|付与者)(?:1体)?(?:を|へ)(.+?)の威力で攻撃する。?$");
        if (m.Success) return Content(EffectContentType.SkillAttack, ActorKey(m.Groups[1].Value), m.Groups[2].Value);
        m = M(s, @"^自身に付与されている〈([^〉]+)〉の効果を無視して、(対象|自身|付与者)を(.+?)の威力で攻撃する。?$");
        if (m.Success) return Content(EffectContentType.SkillAttack, ActorKey(m.Groups[2].Value), "IgnoreOwnStateCategory", m.Groups[1].Value, m.Groups[3].Value);
        m = M(s, @"^ターン中に発動した[、,]\s*全てのバフによる効果を([0-9]+)回まで適用しなおす。?$");
        if (m.Success) return Content(EffectContentType.ReapplyEffects, "Turn", "バフ", "All", m.Groups[1].Value);
        m = M(s, @"^このエリアの採取を([0-9]+)回追加で行うことができる。?$");
        if (m.Success) return Content(EffectContentType.AddAreaGathering, Number(m.Groups[1].Value, 1, "追加採取回数").ToString());
        m = M(s, @"^(?:追加で(.+?)|([0-9]+d[0-9]+))のダイスロールを行う。?$");
        if (m.Success) return Content(EffectContentType.RollDice, "", m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
        m = M(s, @"^追加ロール([0-9]+)として(.+?)のダイスロールを行う。?$");
        if (m.Success) return Content(EffectContentType.RollDice, m.Groups[1].Value, m.Groups[2].Value);
        m = M(s, @"^《([^》]+)》×([1-9][0-9]*)を召喚する。?$");
        if (m.Success) return Content(EffectContentType.Summon, m.Groups[1].Value, m.Groups[2].Value);
        m = M(s, @"^ペット《([^》]+)》を(?:([1-9][0-9]*)体)?召喚する。?$");
        if (m.Success) return m.Groups[2].Success ? Content(EffectContentType.Summon, m.Groups[1].Value, m.Groups[2].Value) : Content(EffectContentType.Summon, m.Groups[1].Value);
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
        m = M(s, @"^(自身|対象|付与者)は(?!.*ターン開始時)(.+?)(?:の)?ダメージを受ける。?$");
        if (m.Success) return Content(EffectContentType.Damage, ActorKey(m.Groups[1].Value), m.Groups[2].Value);
        m = M(s, @"^(対象|自身|付与者)に([0-9]+)のダメージを与える。?$");
        if (m.Success) return Content(EffectContentType.Damage, ActorKey(m.Groups[1].Value), m.Groups[2].Value);
        m = M(s, @"^(自身|対象|付与者)への((?:〈[^〉]+〉)(?:(?:及び|または)〈[^〉]+〉)*)を無効にする。?$");
        if (m.Success) return Content(EffectContentType.InvalidateIncomingAction, new[] { ActorKey(m.Groups[1].Value) }.Concat(AllMatches(m.Groups[2].Value, @"〈([^〉]+)〉").Cast<Match>().Select(x => x.Groups[1].Value)).ToArray());
        return null;
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
        return WriteCommonSkillEffect(content) ?? CommonEffectTextCodec.WriteRuleContent(content)
            ?? throw new InvalidOperationException("通常効果には使用できない種別です：" + content.Type);
    }

    // Direct operations carry their target and arguments. Stage-dependent
    // replacements and triggers are interpreted by the owning block.
    internal static bool TryReadCommonSkillEffect(string text, out List<EffectContent> contents)
    {
        string statement = NormalizeEffectSentence(Unbullet(text));
        EffectContent content = ReadCommonSentenceRule(statement) ?? ReadDirectItemEffect(statement) ??
            ReadDirectResourceEffect(statement) ?? ReadBasicEffectContent(statement) ?? ReadDirectEffectContent(statement);
        if (content == null || content.Type == EffectContentType.RemoveSummon ||
            (content.Type == EffectContentType.RollDice && string.IsNullOrEmpty(content.Parameters[0])))
        {
            contents = null;
            return false;
        }
        contents = new List<EffectContent> { content };
        return true;
    }

    internal static string WriteCommonSkillEffect(EffectContent content)
    {
        string raceRecovery = RaceAndRecoveryContentText(content);
        if (raceRecovery != null) return raceRecovery;
        string semantic = SemanticRuleContentText(content, false) ?? SemanticRuleContentText(content, true);
        if (semantic != null) return semantic;
        string equipmentItem = EquipmentAndItemContentText(content);
        if (equipmentItem != null) return equipmentItem;
        string skillResource = SelectedSkillAndResourceContentText(content);
        if (skillResource != null) return skillResource;
        string stackResource = StackAndResourceContentText(content);
        if (stackResource != null) return stackResource;
        string checkAcquisition = CheckAndAcquisitionContentText(content);
        if (checkAcquisition != null) return checkAcquisition;
        string basic = BasicEffectContentText(content);
        if (basic != null) return basic;
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
                if (p.Length == 2) return TargetText(p[0]) + "を" + p[1] + "の威力で攻撃する。";
                if (p.Length == 4 && p[1] == "IgnoreOwnStateCategory")
                    return "自身に付与されている〈" + p[2] + "〉の効果を無視して、" + Actor(p[0]) + "を" + p[3] + "の威力で攻撃する。";
                if (p.Length >= 4 && p[1] == "ElementalWeapon")
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
                return "ペット《" + SummonName(content) + "》を" + (content.Parameters.Count == 2 ? content.Parameters[1] + "体" : "") + "召喚する。";
            case EffectContentType.AddAreaGathering:
                p = Args(content.Parameters, 1, "AddAreaGathering");
                return "このエリアの採取を" + Number(p[0], 1, "追加採取回数") + "回追加で行うことができる。";
            default: return null;
        }
    }
    private static string SkillTriggerText(EffectType type, List<TriggerDefinition> triggers)
    {
        string damageRecovery = DamageRecoveryTriggerText(type, triggers);
        if (damageRecovery != null) return damageRecovery;
        string timedAttack = TimedAttackTriggerText(type, triggers);
        if (timedAttack != null) return timedAttack;
        string categoryActivation = CategoryActivationTriggerText(triggers);
        if (categoryActivation != null) return categoryActivation;
        string basic = BasicEffectTriggerText(triggers);
        if (basic != null) return basic;
        if (triggers == null) throw new InvalidOperationException("Triggersがnullです。");
        string passiveText;
        if (TryPassiveTriggerText(type, triggers, out passiveText)) return passiveText;
        if (triggers.Count == 0) return "";
        if (triggers.Count != 1 || !ValidTrigger(triggers[0])) throw new InvalidOperationException("今回のスキル効果文は、個別トリガー1個に対応します。");
        TriggerDefinition t = triggers[0]; var c = t.Conditions;
        if (t.Timing == TriggerTiming.RandomResult && c.Or.Count == 0 && c.And.Count == 1 && c.And[0].Type == ConditionType.RollResult)
        {
            string[] p = Args(c.And[0].Parameters, 2, "RollResult"); Number(p[0], 1, "追加ロール番号"); RollRangeEnd(p[1]);
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
    private static string OverrideText(SkillBody skill, EffectDefinition effect, EffectContent content)
    {
        if (content?.Type == EffectContentType.SetRoleplayDescription)
        {
            Require(IsSpikeStage(effect.Type) && effect.Triggers != null && effect.Triggers.Count == 0);
            return Need(Args(content.Parameters, 1, "ロールプレイ効果")[0], "ロールプレイ効果");
        }
        string actionResult = ActionResultModifierText(effect, content);
        if (actionResult != null) return actionResult;
        string damageRecovery = DamageRecoveryModifierText(effect, content);
        if (damageRecovery != null) return damageRecovery;
        string timedAttack = TimedAttackModifierText(effect, content);
        if (timedAttack != null) return timedAttack;
        string equipmentItem = EquipmentAndItemModifierText(effect, content);
        if (equipmentItem != null) return equipmentItem;
        string stateReference = StateReferenceModifierText(effect, content);
        if (stateReference != null) return stateReference;
        string stackCategory = StackAndCategoryModifierText(effect, content);
        if (stackCategory != null) return stackCategory;
        string modifierRule = ModifierRuleOverrideText(skill, effect, content);
        if (modifierRule != null) return modifierRule;
        string basic = BasicModifierText(effect, content);
        if (basic != null) return basic;
        if (effect.Triggers == null || effect.Triggers.Any(x => !ValidTrigger(x)) || content == null) throw new InvalidOperationException("上書き効果が不正です。");
        string passiveText;
        if (TryPassiveOverrideText(effect, content, out passiveText)) return passiveText;
        var triggers = effect.Triggers; string[] p;
        switch (content.Type)
        {
            case EffectContentType.SetSkillValue:
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
            case EffectContentType.MultiplyPower:
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
            case EffectContentType.SetResourceCost:
                p = Args(content.Parameters, 2, "SetResourceCost"); if (triggers.Count != 0) break; return p[0] + "の消費は" + p[1] + "になる。";
            case EffectContentType.SetCooldown:
                p = Args(content.Parameters, 1, "SetCooldown"); if (triggers.Count != 0) break; return "クールタイムは" + p[0] + "になる。";
            case EffectContentType.SetCritical:
                p = Args(content.Parameters, 1, "SetCritical");
                if (p[0] != "True" || !Matches(triggers, Trigger(TriggerTiming.AfterActivationRoll, Condition(ConditionType.RollSucceeded)))) break;
                return "この攻撃の発動ロールに成功した時、クリティカルとして扱う。";
            case EffectContentType.AddStateDuration:
                p = Args(content.Parameters, 2, "AddStateDuration"); if (p[0] != "SelectedState" || triggers.Count != 0) break;
                return "選択した状態の持続ターンは" + Signed(p[1]) + "される。";
            case EffectContentType.AddResourceCost:
                p = Args(content.Parameters, 4, "AddResourceCost");
                if (p[0] != "Self") break;
                if (p[3] == "Optional" && Matches(triggers, Trigger(TriggerTiming.ResourceCost))) return "自身の消費" + p[1] + "は" + Signed(p[2]) + "される。（任意）";
                if (p[3] == "Automatic" && triggers.Count == 0) return "消費" + p[1] + Signed(p[2]) + "。";
                break;
            case EffectContentType.AddTargetValue:
                p = Args(content.Parameters, 1, "AddTargetValue");
                if (IsSpikeStage(effect.Type) && triggers.Count == 0 && M(p[0], @"^[+-][0-9]+$").Success)
                    return "このアクティブ効果による目標値は" + p[0] + "される。";
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
            case EffectContentType.PreventCounterDamage:
                p = Args(content.Parameters, 2, "PreventCounterDamage");
                if (effect.Type != EffectType.Active || triggers.Count != 0) break;
                return "この攻撃は〈" + p[1] + "〉によるカウンター効果で" + Actor(p[0]) + "にダメージを与えることはできない。";
            case EffectContentType.SetDamageReduction:
                p = Args(content.Parameters, 1, "SetDamageReduction");
                if ((effect.Type != EffectType.SecondSpike && effect.Type != EffectType.ThirdSpike) || triggers.Count != 0) break;
                return "このカウンター効果による軽減は" + p[0] + "に変化する。";
            case EffectContentType.SetAttackComponent:
                p = Args(content.Parameters, 2, "SetAttackComponent");
                if (p[0] == "AttributePower" && (effect.Type == EffectType.SecondSpike || effect.Type == EffectType.ThirdSpike) && triggers.Count == 0)
                    return "このアクティブ効果による属性威力は" + p[1] + "に変化する。";
                break;
            case EffectContentType.SetActivationRollFormula:
                p = Args(content.Parameters, 1, "SetActivationRollFormula");
                if ((effect.Type != EffectType.SecondSpike && effect.Type != EffectType.ThirdSpike) || triggers.Count != 0) break;
                string formula = Need(p[0], "発動ロール式");
                if (M(formula, @"\s+目標値").Success) throw new InvalidOperationException("スパイクの発動ロール式に空白＋「目標値」は使用できません。");
                return "この発動ロールは" + formula + "に変化する。";
            case EffectContentType.MultiplyAttackComponent:
                p = Args(content.Parameters, 2, "MultiplyAttackComponent");
                if (p[0] != "AttributePower") break;
                if (triggers.Count == 0) return "属性威力は×" + p[1] + "倍される。";
                if (effect.Type == EffectType.Active && Matches(triggers, Trigger(TriggerTiming.ElementalPower, Condition(ConditionType.ResourceRatio, "Target", "HP", "AtMost", "0.5"))))
                    return "対象のHPが最大値の半分以下なら属性威力は×" + p[1] + "倍される。";
                break;
            case EffectContentType.SetAttackRule:
                p = VariableArgs(content.Parameters, 2, "SetAttackRule");
                if (effect.Type != EffectType.Active || triggers.Count != 0) break;
                if (p.Length == 2 && p[0] == "Defense" && p[1] == "Ignore") return "この攻撃は対象の防御点の影響を受けない。";
                if (p.Length == 3 && p[0] == "Response" && p[2] == "Prohibit") return "このスキルを対象に〈" + p[1] + "〉を宣言する事は出来ない。";
                break;
        }
        throw new InvalidOperationException("未対応のスキル上書き効果の組み合わせです：" + effect.Type + " / " + content.Type);
    }
}
