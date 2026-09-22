using System;
using System.Collections.Generic;

[Serializable]
public class StateDefinition
{
    public string Name = "";
    public List<string> Categories = new List<string>();
    public TriggerDefinition Trigger = new TriggerDefinition();
    public List<EffectDefinition> Effects = new List<EffectDefinition>();
    // 通常効果とは分離。発動条件・効果種別に従ってゲーム側で評価します。
    public List<OverrideDefinition> Overrides = new List<OverrideDefinition>();
}
