using System;
using System.Collections.Generic;
using System.Linq;
using static CommonEffectTextCodec;

public static partial class ItemTextConverter
{
    private static readonly Dictionary<ItemGradeEffect, DataBlockSentencePattern> GradePatterns =
        new Dictionary<ItemGradeEffect, DataBlockSentencePattern>
        {
            { ItemGradeEffect.None, new DataBlockSentencePattern("None", "なにもなし。") },
            { ItemGradeEffect.RecoveryPercentagePoints, new DataBlockSentencePattern("RecoveryPercentagePoints", "回復値{0}%", @"[+-][0-9]+") },
            { ItemGradeEffect.ElementBonusMultiplier, new DataBlockSentencePattern("ElementBonusMultiplier",
                "このアクティブ効果による属性値B上昇は×{0}倍に変化。", @"[0-9]+(?:\.[0-9]+)?") },
            { ItemGradeEffect.OptionalRemoveState, new DataBlockSentencePattern("OptionalRemoveState",
                "対象の《{0}》状態を解除する事が出来る。", @"[^《》]+") },
            { ItemGradeEffect.OptionalRemoveStateOnCheck, new DataBlockSentencePattern("OptionalRemoveStateOnCheck",
                "対象は行為判定{0} 目標値{1}を行い、成功で《{2}》状態を解除する事が出来る。", @"[^。\s]+", @"[0-9]+", @"[^《》]+") }
        };

    private static ItemGradeAdjustment ParseGradeAdjustment(string line)
    {
        var row = M(line.Trim(), @"^-\s*☆([1-9][0-9]*)([～〜]?)\s*[：:]\s*(.+)$");
        if (!row.Success) throw new InvalidOperationException("グレード補正の行が不正です：" + line);
        foreach (var entry in GradePatterns)
        {
            var args = entry.Value.Read(row.Groups[3].Value);
            if (args != null) return new ItemGradeAdjustment {
                Grade = Number(row.Groups[1].Value, 1, "グレード"), AndAbove = row.Groups[2].Length != 0,
                Type = entry.Key, Parameters = args.ToList()
            };
        }
        throw new InvalidOperationException("未対応のグレード補正です：" + line);
    }

    private static string GradeAdjustmentText(ItemGradeAdjustment adjustment)
    {
        if (adjustment == null || adjustment.Grade < 1 || !GradePatterns.TryGetValue(adjustment.Type, out var rule))
            throw new InvalidOperationException("グレード補正の対象または効果が不正です。");
        return "- ☆" + adjustment.Grade + (adjustment.AndAbove ? "～" : "") + "：" + rule.Write(adjustment.Parameters);
    }

    /// <summary>選択グレードに適用する補正を列挙する。「☆N～」の効果はN以上で累積する。</summary>
    public static IEnumerable<ItemGradeAdjustment> ApplicableGradeAdjustments(ItemDefinition item)
    {
        int grade = item.VariantGrade > 0 ? item.VariantGrade : item.Grade;
        return item.GradeAdjustments.Where(x => x.AndAbove ? grade >= x.Grade : grade == x.Grade);
    }
}
