#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(DataBlock))]
public class DataBlockEditor : TextDataBlockEditor<SkillTextData>
{
    protected override SkillTextData Data
    {
        get => ((DataBlock)target).Data;
        set => ((DataBlock)target).Data = value;
    }

    protected override string RebuildLabel => "データブロックテキスト再構築";
    protected override string SetMessage => "スキルと状態をセットしました。入力テキストは変更していません。";
    protected override string Normalize(string text) => SkillTextConverter.Normalize(text);
    protected override SkillTextData Parse(string text) => SkillTextConverter.Parse(text);
    protected override string Build(SkillTextData data) => SkillTextConverter.Build(data);
}
#endif
