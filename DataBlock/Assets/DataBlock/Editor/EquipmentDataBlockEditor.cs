#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(EquipmentDataBlock))]
public sealed class EquipmentDataBlockEditor : TextDataBlockEditor<EquipmentTextData>
{
    protected override EquipmentTextData Data
    {
        get => ((EquipmentDataBlock)target).Data;
        set => ((EquipmentDataBlock)target).Data = value;
    }

    protected override string RebuildLabel => "装備データテキスト再構築";
    protected override string SetMessage => "装備・装備効果・グレード補正・付与スキルをセットしました。入力テキストは変更していません。";
    protected override string Normalize(string text) => EquipmentTextConverter.Normalize(text);
    protected override EquipmentTextData Parse(string text) => EquipmentTextConverter.Parse(text);
    protected override string Build(EquipmentTextData data) => EquipmentTextConverter.Build(data);
}
#endif
