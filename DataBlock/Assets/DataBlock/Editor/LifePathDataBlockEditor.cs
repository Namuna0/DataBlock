#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(LifePathDataBlock))]
public sealed class LifePathDataBlockEditor : TextDataBlockEditor<LifePathTextData>
{
    protected override LifePathTextData Data
    {
        get => ((LifePathDataBlock)target).Data;
        set => ((LifePathDataBlock)target).Data = value;
    }

    protected override string RebuildLabel => "ライフパスデータテキスト再構築";
    protected override string SetMessage => "ライフパス・能力値・効果をセットしました。入力テキストは変更していません。";
    protected override string Normalize(string text) => LifePathTextConverter.Normalize(text);
    protected override LifePathTextData Parse(string text) => LifePathTextConverter.Parse(text);
    protected override string Build(LifePathTextData data) => LifePathTextConverter.Build(data);
}
#endif
