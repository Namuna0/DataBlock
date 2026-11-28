using System;
using System.Collections.Generic;
using System.Linq;

using static CommonEffectTextCodec;

internal static class CommonEffectRollTable
{
    private static readonly DataBlockSentencePattern[] TableTriggers =
    {
        new DataBlockSentencePattern("TargetStateFromAchievement", "達成値が次の時、対象に{0}ターン状態を付与する。", Quantity),
        new DataBlockSentencePattern("TargetStateFromTargetRoll", "対象はダイスロール1d{0}を行い、{1}ターンの間次の状態が付与される。", Quantity, Quantity),
        new DataBlockSentencePattern("EnemyDrop", "自身がエネミーのドロップロールを行う際、1体につきダイスロールを行いアイテムを獲得する。"),
        new DataBlockSentencePattern("FullRecoveryInArea", "自身が〈{0}〉で{1}を全回復した時、1d{2}のダイスロールを行い以下の効果を適用しても良い。", TagName, ResourceCode, Quantity),
        new DataBlockSentencePattern("PhaseState", "{0}フェーズ時、1d{1}のダイスロールの結果に応じて自身は[xx]状態になる。", BonusName, Quantity),
        new DataBlockSentencePattern("AreaChange", "自身はエリアを変更する時、1d{0}のダイスロールを行ってもよい。", Quantity)
    };
    private static readonly DataBlockSentencePattern[] TableOutcomes =
    {
        new DataBlockSentencePattern("Item", "《{0}》〈{1}〉×{2}", TagName, TagName, Quantity),
        new DataBlockSentencePattern("State", "《{0}》", TagName),
        new DataBlockSentencePattern("PermanentState", "自身は永続的に《{0}》状態になる。", TagName),
        new DataBlockSentencePattern("ClearPermanent", "自身に付与されている永続効果を一つ選んで解除する。"),
        new DataBlockSentencePattern("None", "何もなし。")
    };

    internal static bool TryRead(string header, string[] lines, ref int index, int end,
        Func<string, bool> readState, out List<EffectContent> effects)
    {
        effects = null;
        header = header.Replace("行ってもいよい", "行ってもよい");
        if (header.StartsWith("さらに", StringComparison.Ordinal)) header = header.Substring(3);
        foreach (var trigger in TableTriggers)
        {
            string[] args = trigger.Read(header);
            if (args == null) continue;
            effects = new List<EffectContent> { Effect(EffectContentType.RollTable, new[] { trigger.Key }.Concat(args).ToArray()) };
            break;
        }
        if (effects == null) return false;

        for (int next = index + 1; next <= end; next++)
        {
            string line = lines[next].Trim().Replace("\\*", "*");
            if (line.Length == 0) continue;
            var tier = M(line, @"^☆([1-9][0-9]*)(?:～([1-9][0-9]*)|(以上))?のエネミー[：:]1d100$");
            var row = M(line, @"^(?:-\s*)?([1-9][0-9]*)(?:～([1-9][0-9]*))?[：:]\s*(.+)$");
            if (tier.Success)
            {
                string max = tier.Groups[3].Success ? "Unlimited" : tier.Groups[tier.Groups[2].Success ? 2 : 1].Value;
                effects.Add(Effect(EffectContentType.RollTable, "Danger", tier.Groups[1].Value, max));
            }
            else if (row.Success)
            {
                string body = row.Groups[3].Value;
                int split = body.IndexOf('。');
                if (split >= 0 && split + 1 < body.Length)
                {
                    if (!readState(body.Substring(split + 1)))
                        throw new InvalidOperationException("ロール結果に続く状態定義が不正です：" + body);
                    body = body.Substring(0, split + 1);
                }
                string[] values = null;
                var outcome = TableOutcomes.FirstOrDefault(t => (values = t.Read(body)) != null);
                if (outcome == null) throw new InvalidOperationException("未対応のロール表の結果です：" + body);
                string min = row.Groups[1].Value, max = row.Groups[2].Success ? row.Groups[2].Value : min;
                effects.Add(Effect(EffectContentType.RollTable,
                    new[] { outcome.Key, min, max }.Concat(values).ToArray()));
            }
            else
            {
                var template = M(line, @"^xx([^〈【】]+)((?:〈[^〉]+〉)+)[：:](.*\[xxB\].+)$");
                if (!template.Success || effects[0].Parameters[0] != "PhaseState") break;
                string suffix = template.Groups[1].Value;
                foreach (var state in effects.Skip(1).Where(e => e.Parameters[0] == "State"))
                {
                    string stateName = state.Parameters[3];
                    if (!stateName.EndsWith(suffix, StringComparison.Ordinal) || stateName.Length <= suffix.Length)
                        throw new InvalidOperationException("状態名がロール表のひな型と一致しません：" + stateName);
                    string stat = stateName.Substring(0, stateName.Length - suffix.Length);
                    string definition = stateName + "効果" + template.Groups[2].Value + "：" +
                        template.Groups[3].Value.Replace("[xxB]", "[" + stat + "B]");
                    if (!readState(definition)) throw new InvalidOperationException("未対応の状態定義です：" + definition);
                }
                index = next;
                break;
            }
            index = next;
        }
        Write(effects);
        return true;
    }

    internal static string Write(IReadOnlyList<EffectContent> effects, HashSet<string> referencedStates = null)
    {
        if (effects == null || effects.Count == 0 || effects[0]?.Type != EffectContentType.RollTable)
            throw new InvalidOperationException("ロール表の発動条件がありません。");
        var header = effects[0].Parameters;
        var trigger = TableTriggers.FirstOrDefault(t => header != null && header.Count > 0 && t.Key == header[0]);
        if (trigger == null || header.Count != 1 + trigger.ParameterCount) throw new InvalidOperationException("ロール表の発動条件が不正です。");
        string first = trigger.Write(header.Skip(1).ToArray());
        var result = new List<string> { first };
        bool dropped = trigger.Key == "EnemyDrop";
        bool fromAchievement = trigger.Key == "TargetStateFromAchievement";
        bool targetRoll = trigger.Key == "TargetStateFromTargetRoll";
        bool stateOnly = fromAchievement || targetRoll || trigger.Key == "PhaseState";
        int die = fromAchievement ? int.MaxValue : dropped ? 100 : Number(targetRoll ? header[1] : header.Last(), 1, "ダイスの面数");
        var tiers = new List<(int Min, int Max)>();
        var ranges = new List<(int Min, int Max)>();
        foreach (var effect in effects.Skip(1))
        {
            var p = effect?.Parameters;
            if (effect == null || effect.Type != EffectContentType.RollTable || p == null || p.Count < 3)
                throw new InvalidOperationException("ロール表の項目が不正です。");
            int min = Number(p[1], 1, "範囲の下限");
            if (p[0] == "Danger")
            {
                if (!dropped || p.Count != 3 || (tiers.Count > 0 && ranges.Count == 0))
                    throw new InvalidOperationException("危険度に対応するドロップ行を指定してください。");
                int max = p[2] == "Unlimited" ? int.MaxValue : Number(p[2], min, "危険度の上限");
                CheckTableRange(tiers, min, max);
                ranges.Clear();
                result.Add("☆" + p[1] + (p[2] == "Unlimited" ? "以上" : max == min ? "" : "～" + p[2]) + "のエネミー：1d100");
                continue;
            }
            int upper = Number(p[2], min, "出目の上限");
            if (upper > die || (dropped && tiers.Count == 0))
                throw new InvalidOperationException("ダイスまたは危険度の範囲に対応しない結果です。");
            CheckTableRange(ranges, min, upper);
            var outcome = TableOutcomes.FirstOrDefault(t => t.Key == p[0]);
            if (outcome == null || (dropped && p[0] != "Item" && p[0] != "None") ||
                (stateOnly && p[0] != "State" && p[0] != "None"))
                throw new InvalidOperationException("ロール表の結果の種類が不正です。");
            string body = outcome.Write(p.Skip(3).ToArray());
            if (p[0] == "PermanentState" || p[0] == "State") referencedStates?.Add(p[3]);
            result.Add("- " + p[1] + (upper == min ? "" : "～" + p[2]) + "：" + body);
        }
        if (ranges.Count == 0) throw new InvalidOperationException("ロール表に結果がありません。");
        return string.Join("\n", result);
    }

    private static void CheckTableRange(List<(int Min, int Max)> prior, int min, int max)
    {
        if (prior.Any(range => range.Min <= max && min <= range.Max))
            throw new InvalidOperationException("ロール表に重複する範囲があります。");
        prior.Add((min, max));
    }
}
