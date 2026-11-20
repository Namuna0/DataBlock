using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

internal sealed class RulebookCsvWindow : EditorWindow
{
    private readonly List<RulebookCell> cells = new List<RulebookCell>();
    private int selected, page;
    private IEnumerator<RulebookCell> import;
    private string importError;
    internal int SuccessCount { get; private set; }
    internal bool CanExport => import == null && SuccessCount > 0;
    private string fileName = "";
    private Vector2 scroll;
    private string preview = "";

    [MenuItem("Tools/DataBlock/ルールブックCSV")]
    private static void Open() { GetWindow<RulebookCsvWindow>("ルールブックCSV"); }

    private void OnGUI()
    {
        if (GUILayout.Button("CSVを開く"))
        {
            string path = EditorUtility.OpenFilePanel("ルールブックCSV", "", "csv");
            if (path.Length != 0) LoadFile(path);
        }
        if (importError != null) EditorGUILayout.HelpBox(importError, MessageType.Error);
        if (fileName.Length == 0) return;
        if (import != null)
        {
            EditorGUILayout.LabelField("読み込み中", cells.Count + "件処理済み");
            if (GUILayout.Button("読み込みを中止")) { ClearImport(); return; }
        }
        EditorGUILayout.LabelField(fileName, "変換成功 " + SuccessCount + "件 / 要確認 " + (cells.Count - SuccessCount) + "件");
        if (cells.Count == 0) return;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(page == 0))
                if (GUILayout.Button("前へ")) page--;
            GUILayout.Label((page + 1) + " / " + ((cells.Count + 9) / 10));
            using (new EditorGUI.DisabledScope((page + 1) * 10 >= cells.Count))
                if (GUILayout.Button("次へ")) page++;
        }
        for (int i = page * 10; i < Math.Min(cells.Count, (page + 1) * 10); i++)
        {
            var cell = cells[i];
            if (GUILayout.Button(cell.Row + "行 " + cell.Column + "列　" + cell.Name + (cell.Error == null ? "" : "　要確認")))
            {
                selected = i;
                preview = cell.Error ?? cell.BuildText();
                scroll = Vector2.zero;
            }
        }
        var current = cells[selected];
        GUILayout.Label("《" + current.Name + "》", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(current.Data == null))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("テキスト再構築")) preview = current.BuildText();
            if (GUILayout.Button("JSON表示")) preview = JsonUtility.ToJson(current.Data, true);
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.SelectableLabel(preview, EditorStyles.textArea, GUILayout.ExpandHeight(true),
            GUILayout.MinHeight(Mathf.Max(60, EditorStyles.textArea.CalcHeight(new GUIContent(preview), Mathf.Max(100, position.width - 24)))));
        EditorGUILayout.EndScrollView();
        using (new EditorGUI.DisabledScope(!CanExport))
            if (GUILayout.Button("変換成功分 " + SuccessCount + "件をJSON出力")) Export();
    }

    internal void LoadFile(string path)
    {
        ClearImport();
        fileName = Path.GetFileName(path);
        import = ReadFile(path).GetEnumerator();
        EditorApplication.update += ReadNextBatch;
    }

    private static IEnumerable<RulebookCell> ReadFile(string path)
    {
        using (var reader = new StreamReader(path, new UTF8Encoding(false, true), true))
            foreach (var cell in RulebookCsv.Read(reader, yieldSkippedFields: true)) yield return cell;
    }

    internal void ReadNextBatch()
    {
        if (import == null) return;
        double deadline = EditorApplication.timeSinceStartup + 0.008;
        try
        {
            for (int i = 0; i < 32; i++)
            {
                if (!import.MoveNext()) { StopReading(); break; }
                var cell = import.Current;
                if (cell != null)
                {
                    cells.Add(cell);
                    if (cell.Error == null) SuccessCount++;
                    if (cells.Count == 1) preview = cell.Error ?? cell.BuildText();
                }
                if (EditorApplication.timeSinceStartup >= deadline) break;
            }
        }
        catch (Exception e)
        {
            string message = fileName + " の読み込みに失敗しました。\n" + e.Message;
            ClearImport();
            importError = message;
        }
        Repaint();
    }

    private void StopReading()
    {
        EditorApplication.update -= ReadNextBatch;
        import?.Dispose();
        import = null;
    }

    private void ClearImport()
    {
        StopReading();
        cells.Clear();
        SuccessCount = page = selected = 0;
        fileName = preview = "";
        scroll = Vector2.zero;
        importError = null;
    }

    private void OnDisable() => ClearImport();

    internal static void WriteJson(TextWriter writer, IEnumerable<RulebookCell> entries)
    {
        writer.Write("[");
        bool first = true;
        foreach (var cell in entries.Where(c => c.Data != null))
        {
            if (!first) writer.Write(",");
            string kind = cell.Data is CharacterProfileData ? "profile" : cell.Data is SiegeWeaponData ? "siegeWeapon" : "skill";
            writer.Write("\n{\"row\":" + cell.Row + ",\"column\":" + cell.Column + ",\"kind\":\"" + kind + "\",\"data\":");
            writer.Write(JsonUtility.ToJson(cell.Data));
            writer.Write("}");
            first = false;
        }
        writer.Write("\n]");
    }

    private void Export()
    {
        string path = EditorUtility.SaveFilePanel("変換成功分をJSON出力", "", Path.GetFileNameWithoutExtension(fileName), "json");
        if (path.Length == 0) return;
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var writer = new StreamWriter(temp, false, new UTF8Encoding(false)))
            {
                WriteJson(writer, cells);
            }
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        catch (Exception e) { EditorUtility.DisplayDialog("JSONの出力に失敗しました", e.Message, "閉じる"); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
