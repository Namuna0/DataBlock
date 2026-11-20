#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(EnemyDataBlock))]
public sealed class EnemyDataBlockEditor : TextDataBlockEditor<EnemyTextData>
{
    protected override EnemyTextData Data
    {
        get => ((EnemyDataBlock)target).Data;
        set => ((EnemyDataBlock)target).Data = value;
    }

    protected override string RebuildLabel => "エネミーデータテキスト再構築";
    protected override string SetMessage => "エネミー・行動・ドロップをセットしました。入力テキストは変更していません。";
    protected override string Normalize(string text) => EnemyTextConverter.Normalize(text);
    protected override EnemyTextData Parse(string text) => EnemyTextConverter.Parse(text);
    protected override string Build(EnemyTextData data) => EnemyTextConverter.Build(data);
}
#endif
