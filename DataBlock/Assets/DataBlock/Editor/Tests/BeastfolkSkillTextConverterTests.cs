#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

internal sealed class BeastfolkSkillTextConverterTests
{
    internal static string[] Sources()
    {
        return Regex.Split(File.ReadAllText("Assets/DataBlock/Editor/Tests/Fixtures/BeastfolkSkills.txt").Replace("\r", ""), @"\n\s*\n(?=《)").Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
    [TestCase(10)] [TestCase(11)] [TestCase(12)]
    public void EachEntry_InspectorFourOperationsAndUnityJsonRoundTrip(int index)
    {
        var sources = Sources();
        Assert.That(sources.Length, Is.EqualTo(13));
        var go = new GameObject("SkillDataBlockA_Test");
        Editor editor = null;
        try
        {
            var block = go.AddComponent<DataBlock>();
            editor = Editor.CreateEditor(block);
            Assert.That(editor, Is.TypeOf<DataBlockEditor>());
            var text = typeof(DataBlockEditor).GetField("_text", BindingFlags.Instance | BindingFlags.NonPublic);
            var messageType = typeof(DataBlockEditor).GetField("_messageType", BindingFlags.Instance | BindingFlags.NonPublic);
            var message = typeof(DataBlockEditor).GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic);
            var run = typeof(DataBlockEditor).GetMethod("Run", BindingFlags.Instance | BindingFlags.NonPublic);
            text.SetValue(editor, sources[index]);
            run.Invoke(editor, new object[] { 0 });
            string normalized = (string)text.GetValue(editor);
            Assert.That(messageType.GetValue(editor), Is.EqualTo(MessageType.Info), (string)message.GetValue(editor));
            Assert.That(block.Data.Skill.Name, Is.Empty);
            run.Invoke(editor, new object[] { 1 });
            Assert.That(messageType.GetValue(editor), Is.EqualTo(MessageType.Info), (string)message.GetValue(editor));
            string jsonBefore = JsonUtility.ToJson(block.Data);
            run.Invoke(editor, new object[] { 2 });
            Assert.That((string)text.GetValue(editor), Is.EqualTo(normalized));
            run.Invoke(editor, new object[] { 3 });
            Assert.That(messageType.GetValue(editor), Is.EqualTo(MessageType.Info), (string)message.GetValue(editor));
            string json = (string)text.GetValue(editor);
            Assert.That(json, Does.StartWith("{\n"));
            Assert.That(json, Does.Contain("\"Skill\":").And.Contain("\"Summons\":").And.Contain("\"States\":"));
            var copy = JsonUtility.FromJson<SkillTextData>(json);
            Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(jsonBefore));
            Assert.That(SkillTextConverter.Build(copy), Is.EqualTo(normalized));
            Assert.That(SkillTextConverter.Normalize(normalized), Is.EqualTo(normalized));
            // Renaming content must not change the accepted grammar.
            Assert.DoesNotThrow(() => SkillTextConverter.Normalize(sources[index].Replace("犬人", "任意の種族").Replace("幻惑", "別の状態").Replace("物真似", "別の罠")));
        }
        finally { if (editor != null) UnityEngine.Object.DestroyImmediate(editor); UnityEngine.Object.DestroyImmediate(go); }
    }
    [Test]
    public void SemanticData_PreservesTargetsTriggersAndPowerComponents()
    {
        var data = Sources().Select(SkillTextConverter.Parse).ToDictionary(x => x.Skill.Name);
        var dog = data["犬人"].Skill;
        Assert.That(dog.Effects.Single(x => x.Contents[0].Type == EffectContentType.ProhibitAcquisition).Contents[0].Parameters, Is.EqualTo(new[] { "Self", "CharacterCreation", "人食い狼", "捕食者の愉悦" }));
        Assert.That(dog.Overrides.Single().Contents[0].Parameters, Is.EqualTo(new[] { "AllAlliesExceptSelf", "SAN", "0.86", "Optional" }));
        var wolf = data["人食い狼"];
        Assert.That(wolf.Skill.Effects.Single().Triggers[0].Conditions.And.Select(x => x.Type), Is.EqualTo(new[] { ConditionType.HasMealEffect, ConditionType.TurnNumber }));
        Assert.That(wolf.States[0].Effects[0].Contents[0].Type, Is.EqualTo(EffectContentType.RestoreFromDamage));
        Assert.That(wolf.States[1].Overrides[0].Contents[0].Parameters, Is.EqualTo(new[] { "Self", "攻撃", "1.2", "1.44", "1.72" }));
        var fox = data["狐火"].Skill;
        Assert.That(fox.Effects.First().Contents[0].Parameters, Is.EqualTo(new[] { "Target", "PowerAndAttribute", "2d100*[魔力B]", "50*[火属性B]" }));
        Assert.That(fox.Overrides.Where(x => x.Type == EffectType.SecondSpike).SelectMany(x => x.Contents).Select(x => x.Parameters[1]), Is.EqualTo(new[] { "25+2d100*[魔力B]", "75*[火属性B]" }));
        var trap = data["木葉変化"].Summons.Single();
        Assert.That(trap.DeclarationConditions.And.Single().Parameters, Is.EqualTo(new[] { "攻撃", "Ally", "Receiver", "Automatic" }));
        Assert.That(trap.Effects.First().Triggers.Single().Timing, Is.EqualTo(TriggerTiming.AfterSkillResolution));
        Assert.That(data["隠形の術"].States.Single().Effects.Single(x => x.Contents[0].Type == EffectContentType.RemoveState).Triggers.Select(x => x.Timing), Is.EqualTo(new[] { TriggerTiming.DamageReceived, TriggerTiming.ActionDeclared, TriggerTiming.ActionDeclared }));
        Assert.That(data["ベアタフネス"].Skill.Effects.SelectMany(x => x.Contents).Single(x => x.Type == EffectContentType.RestoreResource).Parameters, Is.EqualTo(new[] { "Self", "HP", "20*[生命B]" }));
        Assert.That(data["熊奔襲"].States.Single().Effects.Single(x => x.Contents[0].Type == EffectContentType.PreventStateApplication).Contents[0].Parameters, Is.EqualTo(new[] { "Self", "Except", "熊猛襲" }));
    }
    [Test]
    public void SingleEntryContract_RejectsBatchAndUnresolvedTrap()
    {
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Parse(string.Join("\n\n", Sources())));
        var data = SkillTextConverter.Parse(Sources()[6]); data.Summons.Clear();
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Build(data));
        data = SkillTextConverter.Parse(Sources()[4]); data.Skill.Effects.RemoveAll(x => x.Contents.Any(c => c.Type == EffectContentType.SkillAttack));
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Build(data));
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void InvalidSerializedExtensions_AreRejected(int scenario)
    {
        SkillTextData data;
        switch (scenario)
        {
            case 0:
                data = SkillTextConverter.Parse(Sources()[3]);
                data.Skill.Effects.RemoveAll(e => e.Contents.Any(c => c.Type == EffectContentType.GainStack));
                break;
            case 1:
                data = SkillTextConverter.Parse(Sources()[6]); data.Summons[0].Categories.Clear();
                break;
            case 2:
                data = SkillTextConverter.Parse(Sources()[6]); data.Summons[0].Effects[0].Triggers.Clear();
                break;
            case 3:
                data = SkillTextConverter.Parse(Sources()[0]); data.Skill.Overrides[0].Contents[0].Parameters[3] = "Forced";
                break;
            case 4:
                data = SkillTextConverter.Parse(Sources()[4]); data.Skill.Overrides.Add(data.Skill.Overrides.First(x => x.Type == EffectType.SecondSpike));
                break;
            default:
                data = SkillTextConverter.Parse(Sources()[2]); data.States[1].Overrides[0].Contents[0].Parameters[2] = "-1";
                break;
        }
        Assert.Throws<InvalidOperationException>(() => SkillTextConverter.Build(data));
    }
}
#endif
