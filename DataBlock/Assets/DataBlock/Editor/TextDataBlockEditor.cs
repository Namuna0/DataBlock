#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public abstract class TextDataBlockEditor<TData> : Editor where TData : class
{
    protected enum Operation
    {
        Normalize,
        Deserialize,
        Rebuild,
        Json
    }

    protected abstract TData Data { get; set; }
    protected virtual System.Collections.Generic.IEnumerable<string> Warnings(TData data) => Array.Empty<string>();
    protected abstract string RebuildLabel { get; }
    protected abstract string SetMessage { get; }
    protected abstract string Normalize(string text);
    protected abstract TData Parse(string text);
    protected abstract string Build(TData data);

    // 入力・生成文はEditorだけに置き、ゲームのDataへ重複保存しません。
    [SerializeField] protected string _text = "";
    protected string _message = "";
    protected MessageType _messageType;
    private Vector2 _scroll;
    private GUIStyle _style;
    internal DataBlockJsonPreview JsonPreview { get; private set; }

    public sealed override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        if (serializedObject.ApplyModifiedProperties()) JsonPreview = null;
        foreach (string warning in Warnings(Data)) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("入力 / 出力テキスト", EditorStyles.boldLabel);
        if (_style == null) _style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(300));
        string edited = EditorGUILayout.TextArea(_text, _style, GUILayout.MinHeight(280), GUILayout.ExpandHeight(true));
        if (edited != _text) { _text = edited; _message = ""; JsonPreview = null; }
        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("構文統一")) Run(Operation.Normalize);
        if (GUILayout.Button("テキストからシリアライズセット")) Run(Operation.Deserialize);
        if (GUILayout.Button(RebuildLabel)) Run(Operation.Rebuild);
        if (GUILayout.Button("JSON出力")) Run(Operation.Json);
        JsonPreview?.Draw();
        if (_message.Length > 0) EditorGUILayout.HelpBox(_message, _messageType);
    }

    protected void Run(Operation operation)
    {
        JsonPreview = null;
        try
        {
            switch (operation)
            {
                case Operation.Normalize:
                    _text = Normalize(_text);
                    _message = "構文を統一しました。シリアライズデータは変更していません。";
                    break;
                case Operation.Deserialize:
                    TData parsed = Parse(_text);
                    Undo.RecordObject(target, "Set " + target.GetType().Name);
                    Data = parsed;
                    if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                    EditorUtility.SetDirty(target);
                    serializedObject.Update();
                    _message = SetMessage;
                    break;
                case Operation.Rebuild:
                    _text = Build(Data);
                    _message = "シリアライズデータからテキストを再構築しました。";
                    break;
                case Operation.Json:
                    if (Data == null) throw new InvalidOperationException("出力するシリアライズデータがありません。");
                    JsonPreview = new DataBlockJsonPreview(Data);
                    _message = "本体・サモン・状態のJSONを出力しました。";
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
internal sealed class DataBlockJsonPreview
{
    internal readonly string Main, Summons, States;
    private Vector2 mainScroll, summonsScroll, statesScroll;

    internal DataBlockJsonPreview(object data)
    {
        var export = new DataBlockJsonExport();
        Main = DataBlockJsonExport.Format(export.Main(data), true);
        Summons = DataBlockJsonExport.Format(export.Summons.ToArray(), true);
        States = DataBlockJsonExport.Format(export.States.ToArray(), true);
    }

    internal void Draw()
    {
        DrawField("本体 JSON", Main, ref mainScroll);
        DrawField("サモン JSON", Summons, ref summonsScroll);
        DrawField("状態 JSON", States, ref statesScroll);
    }

    private static void DrawField(string label, string json, ref Vector2 scroll)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (GUILayout.Button("コピー", GUILayout.Width(60))) EditorGUIUtility.systemCopyBuffer = json;
        }
        float height = EditorStyles.textArea.CalcHeight(new GUIContent(json), Mathf.Max(80, EditorGUIUtility.currentViewWidth - 48));
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Clamp(height + 8, 38, 150)));
        EditorGUILayout.SelectableLabel(json, EditorStyles.textArea, GUILayout.Height(Mathf.Max(24, height)));
        EditorGUILayout.EndScrollView();
    }
}
#endif
