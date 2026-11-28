#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(ItemDataBlock))]
public sealed class ItemDataBlockEditor : TextDataBlockEditor<ItemTextData>
{
    protected override ItemTextData Data
    {
        get => ((ItemDataBlock)target).Data;
        set => ((ItemDataBlock)target).Data = value;
    }

    protected override string RebuildLabel => "アイテムデータテキスト再構築";
    protected override string SetMessage => "アイテム・効果・付与スキルをセットしました。入力テキストは変更していません。";
    protected override string Normalize(string text) => ItemTextConverter.Normalize(text);
    protected override ItemTextData Parse(string text) => ItemTextConverter.Parse(text);
    protected override string Build(ItemTextData data) => ItemTextConverter.Build(data);
}
#endif
