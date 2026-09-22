using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

// 保存DTOとは別に、ゲーム中の検索をO(1)にする読み取り索引です。
public sealed class SkillCatalog
{
    private static readonly ReadOnlyCollection<SkillCatalogRecord> EmptyRecords = Array.AsReadOnly(new SkillCatalogRecord[0]);

    private readonly ReadOnlyCollection<SkillCatalogRecord> _records;
    private readonly Dictionary<string, SkillCatalogRecord> _byId;
    private readonly Dictionary<string, ReadOnlyCollection<SkillCatalogRecord>> _byName;
    private readonly Dictionary<string, ReadOnlyCollection<SkillCatalogRecord>> _byCategory;

    public int Count { get { return _records.Count; } }
    public IReadOnlyList<SkillCatalogRecord> Records { get { return _records; } }

    public SkillCatalog(IEnumerable<SkillCatalogRecord> records)
    {
        if (records == null) throw new ArgumentNullException(nameof(records));

        var all = new List<SkillCatalogRecord>();
        var names = new Dictionary<string, List<SkillCatalogRecord>>(StringComparer.Ordinal);
        var categories = new Dictionary<string, List<SkillCatalogRecord>>(StringComparer.Ordinal);
        _byId = new Dictionary<string, SkillCatalogRecord>(StringComparer.Ordinal);

        foreach (SkillCatalogRecord record in records)
        {
            ValidateRecord(record);
            if (_byId.ContainsKey(record.Id)) throw new InvalidOperationException("スキルIDが重複しています：" + record.Id);
            _byId.Add(record.Id, record);

            all.Add(record);
            AddToIndex(names, record.Data.Skill.Name, record);

            var seenCategories = new HashSet<string>(StringComparer.Ordinal);
            foreach (string category in record.Data.Skill.Categories)
            {
                if (string.IsNullOrWhiteSpace(category)) throw new InvalidOperationException("カテゴリーが空です：" + record.Id);
                if (seenCategories.Add(category)) AddToIndex(categories, category, record);
            }
        }

        _records = all.AsReadOnly();
        _byName = Freeze(names);
        _byCategory = Freeze(categories);
    }

    public static SkillCatalog FromShards(IEnumerable<SkillCatalogShard> shards)
    {
        if (shards == null) throw new ArgumentNullException(nameof(shards));
        return new SkillCatalog(EnumerateRecords(shards));
    }

    public bool TryGetById(string id, out SkillCatalogRecord record)
    {
        if (id == null)
        {
            record = null;
            return false;
        }

        return _byId.TryGetValue(id, out record);
    }

    public IReadOnlyList<SkillCatalogRecord> FindByName(string name)
    {
        return Find(_byName, name);
    }

    public IReadOnlyList<SkillCatalogRecord> FindByCategory(string category)
    {
        return Find(_byCategory, category);
    }

    private static IEnumerable<SkillCatalogRecord> EnumerateRecords(IEnumerable<SkillCatalogShard> shards)
    {
        foreach (SkillCatalogShard shard in shards)
        {
            if (shard == null || shard.Records == null) throw new InvalidOperationException("カタログ分割がnullです。");
            foreach (SkillCatalogRecord record in shard.Records) yield return record;
        }
    }

    private static void ValidateRecord(SkillCatalogRecord record)
    {
        if (record == null) throw new InvalidOperationException("カタログレコードがnullです。");
        SkillCatalogSharding.ValidateId(record.Id);
        if (record.Data == null || record.Data.Skill == null) throw new InvalidOperationException("スキルデータがnullです：" + record.Id);
        if (string.IsNullOrWhiteSpace(record.Data.Skill.Name)) throw new InvalidOperationException("スキル名が空です：" + record.Id);
        if (record.Data.Skill.Categories == null) throw new InvalidOperationException("Categoriesがnullです：" + record.Id);
    }

    private static void AddToIndex(
        Dictionary<string, List<SkillCatalogRecord>> index,
        string key,
        SkillCatalogRecord record)
    {
        List<SkillCatalogRecord> values;
        if (!index.TryGetValue(key, out values))
        {
            values = new List<SkillCatalogRecord>();
            index.Add(key, values);
        }

        values.Add(record);
    }

    private static Dictionary<string, ReadOnlyCollection<SkillCatalogRecord>> Freeze(
        Dictionary<string, List<SkillCatalogRecord>> source)
    {
        var result = new Dictionary<string, ReadOnlyCollection<SkillCatalogRecord>>(source.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<SkillCatalogRecord>> pair in source)
            result.Add(pair.Key, pair.Value.AsReadOnly());
        return result;
    }

    private static IReadOnlyList<SkillCatalogRecord> Find(
        Dictionary<string, ReadOnlyCollection<SkillCatalogRecord>> index,
        string key)
    {
        ReadOnlyCollection<SkillCatalogRecord> result;
        return key != null && index.TryGetValue(key, out result) ? result : EmptyRecords;
    }
}
