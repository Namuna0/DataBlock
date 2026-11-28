using System;
using System.Collections.Generic;
using UnityEngine;

// 同じ場所にある進行表と行動を記載順にまとめる。
// 行動の後に別の進行表がある場合は、次の区画に続く。
[Serializable]
public sealed class AreaSection
{
    // 最初の無見出し区画はエリア全体、その後の無見出し区画は直前の場所に続く。
    public string Facility = "";
    public string FacilityMarker = "";
    public List<AreaRollTable> Encounters = new List<AreaRollTable>();
    public List<AreaActionDefinition> Actions = new List<AreaActionDefinition>();
}

[Serializable]
public sealed class AreaDefinition
{
    public string Region = "";
    public string Name = "";
    public List<string> Categories = new List<string>();
    public List<AreaSection> Sections = new List<AreaSection>();
    [TextArea(3, 10)] public string Flavor = "";
}

[Serializable]
public sealed class AreaTextData
{
    public AreaDefinition Area = new AreaDefinition();
    // イベントと内包するスキルは、各出目からIDで参照する。
    public List<AreaEncounterEvent> Events = new List<AreaEncounterEvent>();
    public List<StateDefinition> States = new List<StateDefinition>();
}
