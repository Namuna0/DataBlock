using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SkillCatalogRecord
{
    [Tooltip("表示名を変更しても変わらない一意のID")]
    public string Id = "";

    public SkillTextData Data = new SkillTextData();
}

[Serializable]
public sealed class SkillCatalogShard
{
    [Min(0)]
    public int Index;

    public List<SkillCatalogRecord> Records = new List<SkillCatalogRecord>();
}
