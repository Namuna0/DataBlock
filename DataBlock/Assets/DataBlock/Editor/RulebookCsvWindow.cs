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
    internal int ExclusionCount { get; private set; }
    internal bool CanExport => import == null && SuccessCount > 0;
    private string fileName = "";
    private Vector2 scroll;
    private string preview = "";
    private DataBlockJsonPreview jsonPreview;

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
        EditorGUILayout.LabelField(fileName, "変換成功 " + SuccessCount + "件 / 要確認 " + (cells.Count - SuccessCount - ExclusionCount) + "件 / 除外 " + ExclusionCount + "件");
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
            if (GUILayout.Button(cell.Row + "行 " + cell.Column + "列　" + cell.Name + (cell.ExclusionReason != null ? "　除外" : cell.Error == null ? "" : "　要確認")))
            {
                selected = i;
                jsonPreview = null;
                preview = cell.ExclusionReason ?? cell.Error ?? cell.BuildText();
                scroll = Vector2.zero;
            }
        }
        var current = cells[selected];
        GUILayout.Label("《" + current.Name + "》", EditorStyles.boldLabel);
        if (current.Data is AreaTextData area)
            foreach (string warning in AreaTextConverter.Warnings(area))
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        if (current.Data is EnemyTextData enemy)
            foreach (string warning in EnemyTextConverter.ReferenceWarnings(enemy.Enemy))
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        using (new EditorGUI.DisabledScope(current.Data == null))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("テキスト再構築")) { preview = current.BuildText(); jsonPreview = null; }
            if (GUILayout.Button("JSON表示")) jsonPreview = new DataBlockJsonPreview(current.Data);
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (jsonPreview != null) jsonPreview.Draw();
        else EditorGUILayout.SelectableLabel(preview, EditorStyles.textArea, GUILayout.ExpandHeight(true),
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

    private IEnumerable<RulebookCell> ReadFile(string path)
    {
        using (var reader = new StreamReader(path, new UTF8Encoding(false, true), true))
            foreach (var cell in RulebookCsv.Read(reader, yieldSkippedFields: true, onExcluded: AddCell)) yield return cell;
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
                if (cell != null) AddCell(cell);
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

    private void AddCell(RulebookCell cell)
    {
        cells.Add(cell);
        if (cell.ExclusionReason != null) ExclusionCount++;
        else if (cell.Data != null) SuccessCount++;
        if (cells.Count == 1) preview = cell.ExclusionReason ?? cell.Error ?? cell.BuildText();
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
        SuccessCount = ExclusionCount = page = selected = 0;
        fileName = preview = "";
        jsonPreview = null;
        scroll = Vector2.zero;
        importError = null;
    }

    private void OnDisable() => ClearImport();

    private void Export()
    {
        string parent = EditorUtility.OpenFolderPanel("本体・サモン・状態のJSONを保存するフォルダー", "", "");
        if (parent.Length == 0) return;
        try
        {
            var export = new DataBlockJsonExport();
            string folder = ExportFiles(parent, Path.GetFileNameWithoutExtension(fileName), cells, export);
            var conflicts = export.Summons.Conflicts.Concat(export.States.Conflicts).ToArray();
            string note = conflicts.Length == 0 ? "" : "\n同名で内容が異なる定義（出典付き）\n" + string.Join("、", conflicts);
            EditorUtility.DisplayDialog("JSONの出力が完了しました", folder + note, "閉じる");
        }
        catch (Exception e) { EditorUtility.DisplayDialog("JSONの出力に失敗しました", e.Message, "閉じる"); }
    }

    internal static string ExportFiles(string parent, string stem, IEnumerable<RulebookCell> entries, DataBlockJsonExport export)
    {
        string staging = Path.Combine(parent, "." + Guid.NewGuid().ToString("N") + ".tmp");
        Directory.CreateDirectory(staging);
        try
        {
            var utf8 = new UTF8Encoding(false);
            using (var main = new StreamWriter(Path.Combine(staging, "data.json"), false, utf8))
            using (var summons = new StreamWriter(Path.Combine(staging, "summons.json"), false, utf8))
            using (var states = new StreamWriter(Path.Combine(staging, "states.json"), false, utf8))
                export.WriteCsv(main, summons, states, entries);

            // Publish all three completed files together, including on repeated exports.
            for (int n = 1; ; n++)
            {
                string name = stem + "_JSON" + (n == 1 ? "" : "_" + n);
                string folder = Path.Combine(parent, name);
                try { Directory.Move(staging, folder); return folder; }
                catch (IOException) when (Directory.Exists(folder) || File.Exists(folder)) { }
            }
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
}
