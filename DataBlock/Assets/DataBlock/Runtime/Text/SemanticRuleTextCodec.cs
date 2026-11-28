using System;
using System.Collections.Generic;
using System.Linq;

public static partial class SkillTextConverter
{
    [Flags]
    private enum AttackParts { None = 0, Power = 1, SkillValue = 2, AttributePower = 4, Weapon = Power | SkillValue, ElementalWeapon = Weapon | AttributePower }

    private sealed class SkillTextRule : DataBlockSentencePattern
    {
        public readonly EffectContentType Type;
        public readonly AttackParts Attack;
        public readonly bool IsOverride;
        public readonly ConditionType ConditionKind;
        public readonly bool State;
        public readonly EffectType Section;

        public SkillTextRule(EffectContentType type, bool state, EffectType section, AttackParts attack,
            bool isOverride, string template, params string[] parameters) : base(type.ToString(), template, parameters)
        {
            Type = type;
            State = state;
            Section = section;
            Attack = attack;
            IsOverride = isOverride;
        }

        public SkillTextRule(ConditionType condition, string template, params string[] parameters)
            : base(condition.ToString(), template, parameters)
        {
            ConditionKind = condition;
        }

        public string Write(List<string> parameters)
        {
            string[] p = Args(parameters, ParameterCount, Key);
            string result = base.Write(p);
            if (Type == EffectContentType.ThisSkillCriticalRange)
                Require(Number(p[0], 1, "下限") <= Number(p[1], 1, "上限") && Number(p[1], 1, "上限") <= 100);
            if (Type == EffectContentType.ExecuteAfterAttackDamage) Require(Number(p[0], 0, "HP割合") <= 100);
            if (Type == EffectContentType.ExtraCountersPerSkill) Require(Number(p[2], 1, "同一対象回数") <= Number(p[1], 1, "宣言回数"));
            if (Type == EffectContentType.UnmodifiableMaximumResourceDamage) Require(Number(p[1], 1, "最大値割合") <= 100);
            return result;
        }
    }

    private const string RuleNamePattern = @"[^〈〉《》\r\n。]+?";
    private const string RuleFormulaPattern = @"[^〈〉《》\r\n。]+?";
    // The unbracketed tokens in a numerical expression are dice, numbers and operators.
    private const string ArithmeticPattern = @"(?:[0-9]+(?:\.[0-9]+)?(?:d[1-9][0-9]*)?|\[[^\[\]。]+\]|\()(?:[0-9d.*+()/\-]|\[[^\[\]。]+\])*";
    private const string RulePositivePattern = @"[1-9][0-9]*";
    private const string RuleFactorPattern = @"(?:[1-9][0-9]*(?:\.[0-9]+)?|0\.[0-9]*[1-9][0-9]*)";
    private const string RewardRollNamePattern = @"[^【】〈〉《》\s。、,]+ロール";
    private const string RuleResourcePattern = @"[A-Z]+";
    private const string RuleSignedPattern = @"[+-][0-9]+";

    private sealed class RuleSection
    {
        private readonly List<SkillTextRule> rules;
        private readonly bool state;
        private readonly EffectType[] sections;

        public RuleSection(List<SkillTextRule> rules, bool state, params EffectType[] sections)
        {
            this.rules = rules;
            this.state = state;
            this.sections = sections;
        }
        public RuleSection Including(params EffectType[] otherSections)
        {
            return new RuleSection(rules, state, sections.Concat(otherSections).ToArray());
        }
        public void Add(EffectContentType type, string template, params string[] parameters)
            => Add(type, AttackParts.None, template, parameters);

        public void Add(EffectContentType type, AttackParts attack, string template, params string[] parameters)
        {
            foreach (var section in sections) rules.Add(new SkillTextRule(type, state, section, attack, false, template, parameters));
        }
        public void AddOverride(EffectContentType type, string template, params string[] parameters)
        {
            foreach (var section in sections) rules.Add(new SkillTextRule(type, state, section, AttackParts.None, true, template, parameters));
        }
    }

    private static List<SkillTextRule> BuildSemanticRules()
    {
        var rules = new List<SkillTextRule>();
        RegisterActiveRules(new RuleSection(rules, false, EffectType.Active));
        RegisterStateRules(new RuleSection(rules, true, EffectType.Passive));
        RegisterCriticalRules(new RuleSection(rules, false, EffectType.Critical));
        RegisterSecondSpikeRules(new RuleSection(rules, false, EffectType.SecondSpike));
        RegisterSuccessRules(new RuleSection(rules, false, EffectType.Success));
        RegisterFumbleRules(new RuleSection(rules, false, EffectType.Fumble));
        RegisterDeclarationRules(new RuleSection(rules, false, EffectType.Declaration));
        RegisterCounterRules(new RuleSection(rules, false, EffectType.Counter));
        RegisterPassiveRules(new RuleSection(rules, false, EffectType.Passive));
        RegisterFailureRules(new RuleSection(rules, false, EffectType.Failure));
        RegisterCombatActionRules(new RuleSection(rules, false, EffectType.Active, EffectType.Counter));
        RegisterCombatOutcomeRules(new RuleSection(rules, false, EffectType.Success, EffectType.Failure, EffectType.Critical));
        RegisterCombatStateRules(new RuleSection(rules, true, EffectType.Passive));
        RegisterConsumableActions(new RuleSection(rules, false, EffectType.Active));
        RegisterConsumableStates(new RuleSection(rules, true, EffectType.Passive));
        rules.Add(new SkillTextRule(EffectContentType.TreatAsNamedItem, false, EffectType.Passive, AttackParts.None, false,
            "このアイテムは《{0}》として扱うことができる。", RuleNamePattern));
        RegisterRecipeRules(rules);
        RegisterItemActiveRules(new RuleSection(rules, false, EffectType.Active));
        RegisterItemCounterRules(new RuleSection(rules, false, EffectType.Counter));
        RegisterItemPassiveRules(new RuleSection(rules, false, EffectType.Passive));
        RegisterItemStateRules(new RuleSection(rules, true, EffectType.Passive));
        return rules;
    }

    private static readonly List<SkillTextRule> SemanticTextRules = BuildSemanticRules();
    private static readonly ILookup<(bool, EffectType), SkillTextRule> RulesBySection = SemanticTextRules.ToLookup(r => (r.State, r.Section));
    private static readonly ILookup<(bool, EffectContentType), SkillTextRule> ContentRules = SemanticTextRules
        .Where(r => !r.IsOverride).ToLookup(r => (r.State, r.Type));
    private static readonly Dictionary<EffectContentType, AttackParts> ActiveAttackParts = SemanticTextRules
        .Where(r => !r.State && r.Section == EffectType.Active && r.Attack != AttackParts.None).ToDictionary(r => r.Type, r => r.Attack);
    private static readonly ILookup<(bool, EffectType, EffectContentType), SkillTextRule> ModifierRules = SemanticTextRules
        .Where(r => r.IsOverride).ToLookup(r => (r.State, r.Section, r.Type));

    private static EffectType RuleStage(EffectType type) => type == EffectType.ThirdSpike ? EffectType.SecondSpike : type;
    private static bool IsRuleSpike(EffectContentType type) => ModifierRules.Contains((false, EffectType.SecondSpike, type));

    private static readonly SkillTextRule[] CommonSentenceRules = SemanticTextRules
        .Where(rule => !rule.IsOverride && !IsSpikeStage(rule.Section))
        .ToArray();

    private static EffectContent ReadCommonSentenceRule(string text)
    {
        foreach (SkillTextRule rule in CommonSentenceRules)
        {
            string[] parameters = rule.Read(text);
            if (parameters == null) continue;
            rule.Write(parameters.ToList());
            return Content(rule.Type, parameters);
        }
        return null;
    }

    private static bool ReadSemanticRule(SkillBody skill, StateDefinition state, EffectType type, string text)
    {
        foreach (SkillTextRule rule in RulesBySection[(state != null, RuleStage(type))])
        {
            string[] p = rule.Read(text);
            if (p == null) continue;
            rule.Write(p.ToList());
            if (!rule.IsOverride)
            {
                if (state != null) AddStateEffect(state, NoTriggers(), Content(rule.Type, p));
                else AddSkillEffect(skill, type, Content(rule.Type, p));
            }
            else AddOverride(state != null ? state.Overrides : skill.Overrides, type, NoTriggers(), Change(rule.Type, p));
            return true;
        }
        return false;
    }
    private static string SemanticRuleContentText(EffectContent content, bool state)
    {
        if (content == null) return null;
        SkillTextRule rule = ContentRules[(state, content.Type)].FirstOrDefault();
        return rule == null ? null : rule.Write(content.Parameters);
    }
    private static string SemanticRuleOverrideText(EffectDefinition effect, EffectContent content, bool state)
    {
        if (content == null || effect == null) return null;
        SkillTextRule rule = ModifierRules[(state, RuleStage(effect.Type), content.Type)].FirstOrDefault();
        if (rule == null) return null;
        Require((effect.Type == rule.Section || (effect.Type == EffectType.ThirdSpike && rule.Section == EffectType.SecondSpike)) && (state ? Matches(effect.Triggers, Trigger(TriggerTiming.Always)) : effect.Triggers.Count == 0));
        return rule.Write(content.Parameters);
    }
}
