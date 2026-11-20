using System;
using System.Collections.Generic;

[Serializable]
public sealed class ProfileBonus
{
    public string Stat = "";
    public int Amount;
}

[Serializable]
public sealed class CharacterProfileData
{
    // Shares identity, categories, acquisition conditions, passive effects and flavor with the skill grammar.
    public SkillTextData Definition = new SkillTextData();
    public int Rank;
    public List<ProfileBonus> AbilityBonuses = new List<ProfileBonus>();
    public List<ProfileBonus> ElementBonuses = new List<ProfileBonus>();
    public List<ProfileBonus> ClassStats = new List<ProfileBonus>();
    public List<string> Skills = new List<string>();
    public List<string> Traits = new List<string>();
    public List<string> ProhibitedTraits = new List<string>();
    public int RequiredTraitChoices;
}

[Serializable]
public sealed class SiegeWeaponData
{
    public SkillTextData Action = new SkillTextData();
    public string Rarity = "";
    public int Grade;
    public string Size = "";
    public int RequiredSpace;
}
