#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(AreaDataBlock))]
public sealed class AreaDataBlockEditor : TextDataBlockEditor<AreaTextData>
{
    protected override AreaTextData Data
    {
        get => ((AreaDataBlock)target).Data;
        set => ((AreaDataBlock)target).Data = value;
    }

    protected override System.Collections.Generic.IEnumerable<string> Warnings(AreaTextData data) => AreaTextConverter.Warnings(data);
    protected override string RebuildLabel => "エリアデータテキスト再構築";
    protected override string SetMessage => "エリア・遭遇表・施設をセットしました。入力テキストは変更していません。";
    protected override string Normalize(string text) => AreaTextConverter.Normalize(text);
    protected override AreaTextData Parse(string text) => AreaTextConverter.Parse(text);
    protected override string Build(AreaTextData data) => AreaTextConverter.Build(data);
}
#endif
