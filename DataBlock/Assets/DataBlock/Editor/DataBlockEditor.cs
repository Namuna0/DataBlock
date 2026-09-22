#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DataBlock))]
public class DataBlockEditor : Editor
{
    // 入力・生成文はEditorだけに置き、ゲームのDataへ重複保存しません。
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
        if (GUILayout.Button("構文統一")) Run(0);
        if (GUILayout.Button("テキストからシリアライズセット")) Run(1);
        if (GUILayout.Button("データブロックテキスト再構築")) Run(2);
        if (GUILayout.Button("JSON出力")) Run(3);
        if (_message.Length > 0) EditorGUILayout.HelpBox(_message, _messageType);
    }
    private void Run(int operation)
    {
        try
        {
            var block = (DataBlock)target;
            if (operation == 0) { _text = SkillTextConverter.Normalize(_text); _message = "構文を統一しました。シリアライズデータは変更していません。"; }
            if (operation == 1)
            {
                SkillTextData parsed = SkillTextConverter.Parse(_text);
                Undo.RecordObject(block, "Set skill data");
                block.Data = parsed;
                if (PrefabUtility.IsPartOfPrefabInstance(block)) PrefabUtility.RecordPrefabInstancePropertyModifications(block);
                EditorUtility.SetDirty(block); serializedObject.Update();
                _message = "スキルと状態をセットしました。入力テキストは変更していません。";
            }
            if (operation == 2) { _text = SkillTextConverter.Build(block.Data); _message = "シリアライズデータからテキストを再構築しました。"; }
            if (operation == 3)
            {
                if (block.Data == null) throw new InvalidOperationException("出力するシリアライズデータがありません。");
                _text = JsonUtility.ToJson(block.Data, true);
                _message = "シリアライズデータをJSONとして出力しました。";
            }
            _messageType = MessageType.Info;
        }
        catch (InvalidOperationException e) { _message = e.Message; _messageType = MessageType.Error; }
        catch (RegexMatchTimeoutException) { _message = "構文解析がタイムアウトしました。入力を1件に分けて確認してください。"; _messageType = MessageType.Error; }
        GUI.FocusControl(null); Repaint();
    }
}
#endif
