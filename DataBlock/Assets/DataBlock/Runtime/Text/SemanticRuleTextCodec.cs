using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class SkillTextConverter
{
    // Each grammar maps to a distinct semantic enum. Parameters contain selectors,
    // numbers or formulas, never an uninterpreted combat sentence or a skill-name key.
    private sealed class SkillTextRule
    {
        public readonly EffectContentType Content;
        public readonly OverrideContentType Modifier;
        public readonly ConditionType ConditionKind;
        public readonly bool State;
        public readonly EffectType Section;
        public readonly string Template;
        public readonly string Pattern;
        private readonly string prefix;
        public readonly int Count;

        public SkillTextRule(EffectContentType content, bool state, EffectType section, string template, params string[] parameters)
            : this(content, OverrideContentType.None, state, section, template, parameters) { }
        public SkillTextRule(OverrideContentType modifier, bool state, EffectType section, string template, params string[] parameters)
            : this(EffectContentType.None, modifier, state, section, template, parameters) { }
        public SkillTextRule(ConditionType condition, string template, params string[] parameters)
            : this(EffectContentType.None, false, EffectType.None, template, parameters) { ConditionKind = condition; }

        public string[] Read(string text)
        {
            if (!text.StartsWith(prefix, StringComparison.Ordinal)) return null;
            Match match = M(text, Pattern);
            return match.Success ? Enumerable.Range(0, Count).Select(i => match.Groups["p" + i].Value).ToArray() : null;
        }

        private SkillTextRule(EffectContentType content, OverrideContentType modifier, bool state, EffectType section, string template, string[] parameters)
        {
            Content = content; Modifier = modifier; State = state; Section = section; Template = template; Count = parameters.Length;
            string literal = template.TrimEnd('。');
            int index = literal.IndexOf('{');
            prefix = index < 0 ? literal : literal.Substring(0, index);
            string pattern = Regex.Escape(literal);
            for (int i = 0; i < parameters.Length; i++)
                pattern = pattern.Replace(Regex.Escape("{" + i + "}"), "(?<p" + i + ">" + parameters[i] + ")");
            Pattern = "^" + pattern + "。?$";
        }
        public string Write(List<string> parameters)
        {
            string[] p = Args(parameters, Count, Content != EffectContentType.None ? Content.ToString() : Modifier.ToString());
            string result = string.Format(CultureInfo.InvariantCulture, Template, p.Cast<object>().ToArray());
            Match match = M(result, Pattern);
            Require(match.Success && Enumerable.Range(0, Count).All(i => match.Groups["p" + i].Value == p[i]));
            if (Modifier == OverrideContentType.ThisSkillCriticalRange)
                Require(Number(p[0], 1, "下限") <= Number(p[1], 1, "上限") && Number(p[1], 1, "上限") <= 100);
            if (Content == EffectContentType.ExecuteAfterAttackDamage) Require(Number(p[0], 0, "HP割合") <= 100);
            if (Content == EffectContentType.ExtraCountersPerSkill) Require(Number(p[2], 1, "同一対象回数") <= Number(p[1], 1, "宣言回数"));
            if (Content == EffectContentType.UnmodifiableMaximumResourceDamage) Require(Number(p[1], 1, "最大値割合") <= 100);
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
        {
            foreach (var section in sections) rules.Add(new SkillTextRule(type, state, section, template, parameters));
        }
        public void Add(OverrideContentType type, string template, params string[] parameters)
        {
            foreach (var section in sections) rules.Add(new SkillTextRule(type, state, section, template, parameters));
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
        return rules;
    }

    private static readonly List<SkillTextRule> SemanticTextRules = BuildSemanticRules();
    private static readonly ILookup<(bool, EffectType), SkillTextRule> RulesBySection = SemanticTextRules.ToLookup(r => (r.State, r.Section));
    private static readonly ILookup<(bool, EffectContentType), SkillTextRule> ContentRules = SemanticTextRules
        .Where(r => r.Content != EffectContentType.None).ToLookup(r => (r.State, r.Content));
    private static readonly ILookup<(bool, EffectType, OverrideContentType), SkillTextRule> ModifierRules = SemanticTextRules
        .Where(r => r.Modifier != OverrideContentType.None).ToLookup(r => (r.State, r.Section, r.Modifier));

    private static EffectType RuleStage(EffectType type) => type == EffectType.ThirdSpike ? EffectType.SecondSpike : type;
    private static bool IsRuleSpike(OverrideContentType type) => ModifierRules.Contains((false, EffectType.SecondSpike, type));

    private static bool ReadSemanticRule(SkillBody skill, StateDefinition state, EffectType type, string text)
    {
        foreach (SkillTextRule rule in RulesBySection[(state != null, RuleStage(type))])
        {
            string[] p = rule.Read(text);
            if (p == null) continue;
            rule.Write(p.ToList());
            if (rule.Content != EffectContentType.None)
            {
                if (state != null) AddStateEffect(state, NoTriggers(), Content(rule.Content, p));
                else AddSkillEffect(skill, type, Content(rule.Content, p));
            }
            else AddOverride(state != null ? state.Overrides : skill.Overrides, type, NoTriggers(), Change(rule.Modifier, p));
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
    private static string SemanticRuleOverrideText(OverrideDefinition effect, OverrideContent content, bool state)
    {
        if (content == null || effect == null) return null;
        SkillTextRule rule = ModifierRules[(state, RuleStage(effect.Type), content.Type)].FirstOrDefault();
        if (rule == null) return null;
        Require((effect.Type == rule.Section || (effect.Type == EffectType.ThirdSpike && rule.Section == EffectType.SecondSpike)) && (state ? Matches(effect.Triggers, Trigger(TriggerTiming.Always)) : effect.Triggers.Count == 0));
        return rule.Write(content.Parameters);
    }
}
