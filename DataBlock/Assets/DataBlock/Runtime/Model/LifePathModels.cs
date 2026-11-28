using System;
using System.Collections.Generic;

[Serializable]
public sealed class LifePathTextData
{
    public string Group = "";
    public SkillTextData Definition = new SkillTextData();
    public List<ProfileBonus> AbilityBonuses = new List<ProfileBonus>();
    // 各段階で記載された能力値の補正をこの値に置き換える。0の指定も保持する。
    public List<ProfileBonus> SecondSpikeBonuses = new List<ProfileBonus>();
    public List<ProfileBonus> ThirdSpikeBonuses = new List<ProfileBonus>();
}
