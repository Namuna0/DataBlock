using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SkillTextData
{
    public SkillDefinition Skill = new SkillDefinition();
    public List<SummonedEntityDefinition> Summons = new List<SummonedEntityDefinition>();
    public List<StateDefinition> States = new List<StateDefinition>();
}
// 子スキルはSkillBody。Choicesを持たないため、再帰的なネストはできません。
[Serializable]
public class SkillBody
{
    public string Name = "";
    public List<string> Categories = new List<string>();
    public ConditionSet AcquisitionConditions = new ConditionSet();
    public ConditionSet EquipmentConditions = new ConditionSet();
    public ConditionSet DeclarationConditions = new ConditionSet();
    public List<ResourceCost> Costs = new List<ResourceCost>();
    /// <summary>再使用までのターン数。0 は指定なし、-1 は無期限。</summary>
    [Min(-1)] public int CooldownTurns;
    public DiceRollDefinition Roll = new DiceRollDefinition();
    public List<EffectDefinition> Effects = new List<EffectDefinition>();
    // 通常効果とは分離。発動条件・効果種別に従ってゲーム側で評価します。
    public List<EffectDefinition> Overrides = new List<EffectDefinition>();
}
[Serializable]
public class SkillDefinition : SkillBody
{
    public List<SkillBody> Choices = new List<SkillBody>();
    [TextArea(3, 10)] public string Flavor = "";
}

[Serializable]
public class FormulaStat
{
    public string Name = "";
    public string Formula = "";
}

// A summon has the same action grammar as a skill body, plus arbitrary
// formula-backed stats. It intentionally has no Choices or nested summons.
[Serializable]
public class SummonedEntityDefinition : SkillBody
{
    public List<FormulaStat> Stats = new List<FormulaStat>();
}
