using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class EnemyTextConverter
{
    internal static List<EnemyActionRule> ParseActions(IEnumerable<string> lines)
    {
        var actions = new List<EnemyActionRule>();
        var group = new List<string>();
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            bool bullet = line.StartsWith("●", StringComparison.Ordinal) || line.StartsWith("・", StringComparison.Ordinal);
            if (bullet) line = line.Substring(1).Trim();
            bool named = TryNameHeader(line, out string name);
            if ((bullet || named) && group.Count > 0)
            {
                actions.Add(ReadActionGroup(group));
                group.Clear();
            }
            if (named)
            {
                actions.Add(new EnemyActionRule { Timing = EnemyActionTiming.Listed, SkillName = name });
                continue;
            }
            group.Add(line);
        }
        if (group.Count > 0) actions.Add(ReadActionGroup(group));
        return actions;
    }

    private static EnemyActionRule ReadActionGroup(List<string> lines)
    {
        var rule = new EnemyActionRule();
        string head = lines[0];
        int firstBranch = 1;
        if (lines.Count > 1 && head == "スタンバイフェーズ" && lines[1].StartsWith("《", StringComparison.Ordinal))
        {
            head += "、" + lines[1];
            firstBranch++;
        }
        ReadActionStep(rule, head, rule);
        for (int i = firstBranch; i < lines.Count; i++)
        {
            string line = lines[i].TrimStart('-', ' ');
            var branch = new EnemyActionBranch();
            Match range = Match(line, @"^([0-9]+)(?:[～~]([0-9]+))?[：:]\s*");
            if (range.Success)
            {
                branch.MinimumRoll = Number(range.Groups[1].Value, 1, "分岐の出目");
                branch.MaximumRoll = range.Groups[2].Success ? Number(range.Groups[2].Value, branch.MinimumRoll, "分岐の出目") : branch.MinimumRoll;
                line = line.Substring(range.Length);
            }
            ReadActionStep(branch, line, null);
            rule.Branches.Add(branch);
        }
        ValidateActionBranches(rule);
        return rule;
    }

    private static void ReadActionStep(EnemyActionStep step, string text, EnemyActionRule rule)
    {
        text = text.Trim().Replace("》　状態", "》状態").Replace("のからの、自身への", "からの自身への");
        if (text.EndsWith("（重複しない）", StringComparison.Ordinal))
        {
            step.DistinctTargets = true;
            text = text.Substring(0, text.Length - "（重複しない）".Length);
        }
        var skill = Match(text, @"(?:([0-9]+)回連続で)?《([^《》]+)》(アイテム)?を?(?:([0-9]+)回)?発動する。?$");
        var idle = Match(text, @"(?:何もしない|行動なし)。?$");
        var roll = Match(text, @"ダイスロール([1-9][0-9]*d[1-9][0-9]*)$");
        if (skill.Success)
        {
            step.SkillName = Token(skill.Groups[2].Value, "行動スキル名", "《》");
            step.UseItem = skill.Groups[3].Success;
            step.Consecutive = skill.Groups[1].Success;
            if (step.Consecutive || skill.Groups[4].Success)
                step.RepeatCount = Number(skill.Groups[step.Consecutive ? 1 : 4].Value, 1, "繰り返し回数");
            text = text.Substring(0, skill.Index);
        }
        else if (idle.Success) { step.DoNothing = true; text = text.Substring(0, idle.Index); }
        else if (rule != null && roll.Success) { rule.BranchRoll = roll.Groups[1].Value; text = text.Substring(0, roll.Index); }
        else if (rule == null) throw new InvalidOperationException("分岐に行動がありません：" + text);

        if (rule != null) text = ReadActionTiming(rule, text);
        text = text.Replace("または同じ接近グループに選択可能な敵が居ない場合、接近状態ではない時",
                "接近状態ではない時、または同じ接近グループに選択可能な敵が居ない場合")
            .Replace("時または同じ", "時、または同じ").Replace("状態かつ《", "状態の時、かつ《")
            .Replace("減少しているまたは〈", "減少している、または〈");
        text = Replace(text, @"(居ない場合は|味方が居ない時は|に対して)(?=選択可能な|ランダムな)", "$1、");
        string[] clauses = text.Split(new[] { '、' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < clauses.Length; i++)
        {
            string clause = clauses[i].Trim();
            bool any = clause.StartsWith("または", StringComparison.Ordinal) ||
                (i + 1 < clauses.Length && clauses[i + 1].TrimStart().StartsWith("または", StringComparison.Ordinal));
            clause = Replace(clause, "^(または|かつ)", "");
            if (TryActionCondition(clause, out var condition)) (any ? step.AnyConditions : step.AllConditions).Add(condition);
            else if (i == clauses.Length - 1) ReadActionTarget(step, clause);
            else throw new InvalidOperationException("未対応の行動条件です：" + clause);
        }
        if (rule != null)
        {
            if (rule.AllConditions.Concat(rule.AnyConditions).Any(IsReaction) && (rule.Timing == EnemyActionTiming.Conditional || rule.Timing == EnemyActionTiming.Turn))
                rule.Timing = EnemyActionTiming.Reaction;
            if (rule.DoNothing && rule.Timing == EnemyActionTiming.Conditional && clauses.Length == 0) rule.Timing = EnemyActionTiming.None;
        }
    }

    private static string ReadActionTiming(EnemyActionRule rule, string text)
    {
        rule.Timing = EnemyActionTiming.Conditional;
        var turn = Match(text, @"(?:^|、)(?:チームで)?毎ターン([1-9][0-9]*(?:d[1-9][0-9]*(?:\+[0-9]+)?)?)回(まで)?(?:、|,\s*)?");
        if (!turn.Success && text.StartsWith("スタンバイフェーズ及び", StringComparison.Ordinal))
            turn = Match(text, @"毎ターン([1-9][0-9]*)回(まで)?、?");
        if (turn.Success)
        {
            rule.Timing = EnemyActionTiming.Turn;
            rule.TeamLimit = turn.Value.Contains("チームで");
            rule.UpToUsesPerTurn = turn.Groups[2].Success;
            if (turn.Groups[1].Value.Contains("d")) rule.UsesPerTurnFormula = turn.Groups[1].Value;
            else rule.UsesPerTurn = Number(turn.Groups[1].Value, 1, "毎ターンの発動回数");
            text = text.Remove(turn.Index, turn.Length).Insert(turn.Index, "、");
        }
        if (text.StartsWith("スタンバイフェーズ", StringComparison.Ordinal))
        {
            rule.Timing = text.StartsWith("スタンバイフェーズ及び", StringComparison.Ordinal) ? EnemyActionTiming.StandbyAndTurn : EnemyActionTiming.Standby;
            text = Replace(text, "^スタンバイフェーズ(?:及び)?", "");
        }
        else if (text.StartsWith("エンカウントフェーズ", StringComparison.Ordinal))
        {
            rule.Timing = EnemyActionTiming.Encounter;
            text = Replace(text, "^エンカウントフェーズ(?:で)?", "");
        }
        var battle = Match(text, @"^(?:戦闘開始から)?([1-9][0-9]*)ターン目(以降|開始時)?");
        if (battle.Success)
        {
            rule.BattleTurn = Number(battle.Groups[1].Value, 1, "戦闘ターン");
            if (battle.Groups[2].Value != "以降") rule.Timing = battle.Groups[2].Value == "開始時" ? EnemyActionTiming.BattleTurnStart : EnemyActionTiming.BattleTurn;
            text = text.Substring(battle.Length);
        }
        var limit = Match(text, @"^戦闘中([1-9][0-9]*)度だけ");
        if (limit.Success)
        {
            rule.UsesPerBattle = Number(limit.Groups[1].Value, 1, "戦闘中の制限回数");
            text = text.Substring(limit.Length);
        }
        return text.Trim('、', ' ');
    }

    private static IEnumerable<string> ActionSkillNames(EnemyActionRule action)
    {
        if (action == null || action.Branches == null) throw new InvalidOperationException("行動または行動分岐が空です。");
        return new EnemyActionStep[] { action }.Concat(action.Branches)
            .Where(step => step != null && !step.UseItem && !string.IsNullOrEmpty(step.SkillName)).Select(step => step.SkillName);
    }

    private static void ValidateActionBranches(EnemyActionRule rule)
    {
        if (rule.Branches == null) throw new InvalidOperationException("行動分岐が空です。");
        bool rolled = !string.IsNullOrEmpty(rule.BranchRoll);
        bool hasAction = !string.IsNullOrEmpty(rule.SkillName) || rule.DoNothing;
        if (!hasAction && rule.Branches.Count == 0 || hasAction && rolled)
            throw new InvalidOperationException("行動または行動分岐を指定してください。");
        if (rolled && !Match(rule.BranchRoll, @"^[1-9][0-9]*d[1-9][0-9]*$").Success) throw new InvalidOperationException("行動分岐のダイス式が不正です。");
        int min = 0;
        long max = 0;
        if (rolled)
        {
            string[] dice = rule.BranchRoll.Split('d');
            min = Number(dice[0], 1, "ダイス数");
            max = (long)min * Number(dice[1], 1, "ダイス面数");
        }
        for (int i = 0; i < rule.Branches.Count; i++)
        {
            var branch = rule.Branches[i];
            if (branch == null || (string.IsNullOrEmpty(branch.SkillName) && !branch.DoNothing) || (branch.MinimumRoll != 0 && (!rolled || branch.MinimumRoll < min || branch.MaximumRoll > max)) ||
                (rolled && i == 0 && branch.MinimumRoll == 0) || branch.MaximumRoll < branch.MinimumRoll ||
                (branch.MinimumRoll == 0 && branch.MaximumRoll != 0)) throw new InvalidOperationException("行動分岐の出目範囲が不正です。");
            if (branch.MinimumRoll > 0 && rule.Branches.Take(i).Any(other => other.MinimumRoll > 0 && other.MinimumRoll <= branch.MaximumRoll && branch.MinimumRoll <= other.MaximumRoll))
                throw new InvalidOperationException("行動分岐の出目が重複しています。");
        }
    }

    internal static string ActionText(EnemyActionRule action)
    {
        if (action == null) throw new InvalidOperationException("行動がnullです。");
        ValidateActionBranches(action);
        var prefix = new List<string>();
        string quota = action.UsesPerTurn > 0 ? action.UsesPerTurn.ToString() : action.UsesPerTurnFormula ?? "";
        if (action.UsesPerTurn < 0 || action.UsesPerBattle < 0 || action.BattleTurn < 0 || (action.UsesPerTurn > 0 && !string.IsNullOrEmpty(action.UsesPerTurnFormula)) ||
            (quota.Length > 0 && !Match(quota, @"^[1-9][0-9]*(?:d[1-9][0-9]*(?:\+[0-9]+)?)?$").Success))
            throw new InvalidOperationException("行動の回数指定が不正です。");
        bool turnQuota = quota.Length > 0;
        bool hasTrigger = action.AllConditions != null && action.AnyConditions != null &&
            action.AllConditions.Concat(action.AnyConditions).Any(IsReaction);
        bool expectedQuota = action.Timing == EnemyActionTiming.Turn || action.Timing == EnemyActionTiming.StandbyAndTurn;
        bool fixedTurn = action.Timing == EnemyActionTiming.BattleTurn || action.Timing == EnemyActionTiming.BattleTurnStart;
        if (!Enum.IsDefined(typeof(EnemyActionTiming), action.Timing) || (expectedQuota && !turnQuota) ||
            (turnQuota && !expectedQuota && action.Timing != EnemyActionTiming.Reaction) ||
            ((action.UpToUsesPerTurn || action.TeamLimit) && !turnQuota) ||
            (fixedTurn && action.BattleTurn == 0) || (action.BattleTurn > 0 && !fixedTurn && !turnQuota) ||
            ((action.Timing == EnemyActionTiming.Conditional || action.Timing == EnemyActionTiming.Turn || action.Timing == EnemyActionTiming.Reaction)
                && (action.Timing == EnemyActionTiming.Reaction) != hasTrigger))
            throw new InvalidOperationException("行動のタイミング・回数と条件が一致しません。");
        if (action.Timing == EnemyActionTiming.Listed || action.Timing == EnemyActionTiming.None)
        {
            if (action.AllConditions == null || action.AllConditions.Count > 0 || action.AnyConditions == null || action.AnyConditions.Count > 0 ||
                action.UsesPerBattle != 0 || action.Branches.Count > 0 || action.UseItem || action.RepeatCount != 1 || action.Consecutive ||
                ActionTargetText(action).Length > 0 || action.DoNothing != (action.Timing == EnemyActionTiming.None) ||
                action.DoNothing != string.IsNullOrEmpty(action.SkillName))
                throw new InvalidOperationException("名前のみ・行動なしの記述には追加条件を指定できません。");
            return action.DoNothing ? "何もしない。" : "《" + Token(action.SkillName, "行動スキル名", "《》") + "》";
        }
        if (action.UsesPerBattle > 0) prefix.Add("戦闘中" + action.UsesPerBattle + "度だけ");
        if (action.BattleTurn > 0)
            prefix.Add("戦闘開始から" + action.BattleTurn + "ターン目" +
                (action.Timing == EnemyActionTiming.BattleTurnStart ? "開始時" : action.Timing == EnemyActionTiming.BattleTurn ? "" : "以降"));
        if (action.Timing == EnemyActionTiming.Encounter) prefix.Add("エンカウントフェーズ");
        if (action.Timing == EnemyActionTiming.Standby) prefix.Add("スタンバイフェーズ");
        if (quota.Length > 0)
            prefix.Add((action.Timing == EnemyActionTiming.StandbyAndTurn ? "スタンバイフェーズ及び" : "") + (action.TeamLimit ? "チームで" : "") +
                "毎ターン" + quota + "回" + (action.UpToUsesPerTurn ? "まで" : ""));
        string result = ActionStepText(action, prefix, !string.IsNullOrEmpty(action.BranchRoll) ? "ダイスロール" + action.BranchRoll : "");
        foreach (var branch in action.Branches)
        {
            string range = branch.MinimumRoll == 0 ? "" : branch.MinimumRoll +
                (branch.MaximumRoll == branch.MinimumRoll ? "" : "～" + branch.MaximumRoll) + "：";
            result += "\n" + (range.Length > 0 || string.IsNullOrEmpty(action.BranchRoll) ? "- " : "") + range + ActionStepText(branch, new List<string>(), "");
        }
        return result;
    }

    private static string ActionStepText(EnemyActionStep step, List<string> prefix, string execution)
    {
        if (step.AllConditions == null || step.AnyConditions == null || step.AnyConditions.Count == 1 || step.RepeatCount < 1)
            throw new InvalidOperationException("行動の条件または回数が不正です。");
        if (string.IsNullOrEmpty(step.SkillName) && (step.UseItem || step.RepeatCount != 1 || step.Consecutive) ||
            step.DoNothing && !string.IsNullOrEmpty(step.SkillName)) throw new InvalidOperationException("実行する行動の指定が不正です。");
        prefix.AddRange(step.AllConditions.Select(ActionConditionText));
        if (step.AnyConditions.Count > 0) prefix.Add(string.Join("、または", step.AnyConditions.Select(ActionConditionText)));
        string target = ActionTargetText(step);
        if (target.Length > 0) prefix.Add(target);
        if (step.DoNothing) execution = "何もしない。";
        else if (!string.IsNullOrEmpty(step.SkillName))
            execution = (step.Consecutive ? step.RepeatCount + "回連続で" : "") + "《" + Token(step.SkillName, "行動スキル名", "《》") + "》" +
                (step.UseItem ? "アイテム" : "") + "を" + (!step.Consecutive && step.RepeatCount != 1 ? step.RepeatCount + "回" : "") + "発動する。";
        if (prefix.Count == 0) return execution;
        string last = prefix[prefix.Count - 1];
        bool connect = !execution.StartsWith("ダイスロール", StringComparison.Ordinal) &&
            (target.Length > 0 || last.EndsWith("に対して", StringComparison.Ordinal));
        return string.Join("、", prefix) + (connect ? "" : "、") + execution;
    }
}
