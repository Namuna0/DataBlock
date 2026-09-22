using System;
using System.Collections.Generic;

public static class SkillCatalogSharding
{
    public const int DefaultShardCount = 64;
    private const int MaximumShardCount = 4096;

    public static int GetShardIndex(string id, int shardCount = DefaultShardCount)
    {
        ValidateShardCount(shardCount);
        ValidateId(id);

        // FNV-1aを使い、実行環境に左右されない分割先を算出します。
        uint hash = 2166136261;
        unchecked
        {
            for (int i = 0; i < id.Length; i++)
            {
                hash ^= id[i];
                hash *= 16777619;
            }
        }

        return (int)(hash % (uint)shardCount);
    }

    public static IReadOnlyList<SkillCatalogShard> CreateShards(
        IEnumerable<SkillCatalogRecord> records,
        int shardCount = DefaultShardCount)
    {
        if (records == null) throw new ArgumentNullException(nameof(records));
        ValidateShardCount(shardCount);

        var shards = new SkillCatalogShard[shardCount];
        for (int i = 0; i < shards.Length; i++) shards[i] = new SkillCatalogShard { Index = i };

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (SkillCatalogRecord record in records)
        {
            if (record == null) throw new InvalidOperationException("カタログレコードがnullです。");
            int shardIndex = GetShardIndex(record.Id, shardCount);
            if (!ids.Add(record.Id)) throw new InvalidOperationException("スキルIDが重複しています：" + record.Id);
            shards[shardIndex].Records.Add(record);
        }

        foreach (SkillCatalogShard shard in shards)
            shard.Records.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));

        return shards;
    }

    internal static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("スキルIDが空です。");
        if (!string.Equals(id, id.Trim(), StringComparison.Ordinal) || id.IndexOf('\r') >= 0 || id.IndexOf('\n') >= 0)
            throw new InvalidOperationException("スキルIDの前後や途中に改行を使用できません：" + id);
    }

    private static void ValidateShardCount(int shardCount)
    {
        if (shardCount < 1 || shardCount > MaximumShardCount)
            throw new ArgumentOutOfRangeException(nameof(shardCount), "分割数は1～" + MaximumShardCount + "にしてください。");
    }
}
