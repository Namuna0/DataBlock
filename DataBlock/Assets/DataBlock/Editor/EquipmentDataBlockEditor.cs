#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EquipmentDataBlock))]
public sealed class EquipmentDataBlockEditor : Editor
{
    private enum Operation
    {
        Normalize,
        Deserialize,
        Rebuild,
        Json
    }

    [SerializeField] private string _text = "";
    private string _message = "";
    private MessageType _messageType;
    private Vector2 _scroll;
    private GUIStyle _style;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("入力 / 出力テキスト", EditorStyles.boldLabel);
        if (_style == null) _style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(300));
        string edited = EditorGUILayout.TextArea(_text, _style, GUILayout.MinHeight(280), GUILayout.ExpandHeight(true));
        if (edited != _text) { _text = edited; _message = ""; }
        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("構文統一")) Run(Operation.Normalize);
        if (GUILayout.Button("テキストからシリアライズセット")) Run(Operation.Deserialize);
        if (GUILayout.Button("装備データテキスト再構築")) Run(Operation.Rebuild);
        if (GUILayout.Button("JSON出力")) Run(Operation.Json);
        if (_message.Length > 0) EditorGUILayout.HelpBox(_message, _messageType);
    }

    private void Run(Operation operation)
    {
        try
        {
            var block = (EquipmentDataBlock)target;
            switch (operation)
            {
                case Operation.Normalize:
                    _text = EquipmentTextConverter.Normalize(_text);
                    _message = "構文を統一しました。シリアライズデータは変更していません。";
                    break;
                case Operation.Deserialize:
                    EquipmentTextData parsed = EquipmentTextConverter.Parse(_text);
                    Undo.RecordObject(block, "Set equipment data");
                    block.Data = parsed;
                    if (PrefabUtility.IsPartOfPrefabInstance(block)) PrefabUtility.RecordPrefabInstancePropertyModifications(block);
                    EditorUtility.SetDirty(block);
                    serializedObject.Update();
                    _message = "装備・装備効果・グレード補正・付与スキルをセットしました。入力テキストは変更していません。";
                    break;
                case Operation.Rebuild:
                    _text = EquipmentTextConverter.Build(block.Data);
                    _message = "シリアライズデータから装備テキストを再構築しました。";
                    break;
                case Operation.Json:
                    if (block.Data == null) throw new InvalidOperationException("出力するシリアライズデータがありません。");
                    _text = JsonUtility.ToJson(block.Data, true);
                    _message = "シリアライズデータをJSONとして出力しました。";
                    break;
                default:
                    throw new InvalidOperationException("未対応の操作です。");
            }
            _messageType = MessageType.Info;
        }
        catch (InvalidOperationException exception)
        {
            _message = exception.Message;
            _messageType = MessageType.Error;
        }
        catch (RegexMatchTimeoutException)
        {
            _message = "構文解析がタイムアウトしました。入力を1件に分けて確認してください。";
            _messageType = MessageType.Error;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            _message = "予期しないエラーが発生しました。Consoleを確認してください。";
            _messageType = MessageType.Error;
        }
        finally
        {
            GUI.FocusControl(null);
            Repaint();
        }
    }
}
#endif
