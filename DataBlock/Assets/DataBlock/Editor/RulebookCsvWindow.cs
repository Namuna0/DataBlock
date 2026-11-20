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
    private int selected, page, successCount;
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
        if (fileName.Length == 0) return;
        EditorGUILayout.LabelField(fileName, "変換成功 " + successCount + "件 / 要確認 " + (cells.Count - successCount) + "件");
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
        using (new EditorGUI.DisabledScope(successCount == 0))
            if (GUILayout.Button("変換成功分 " + successCount + "件をJSON出力")) Export();
    }

    private void LoadFile(string path)
    {
        try
        {
            List<RulebookCell> parsed;
            using (var reader = new StreamReader(path, new UTF8Encoding(false, true), true))
                parsed = RulebookCsv.Read(reader).ToList();
            cells.Clear();
            cells.AddRange(parsed);
            successCount = cells.Count(c => c.Error == null);
            fileName = Path.GetFileName(path);
            page = selected = 0;
            preview = cells.Count == 0 ? "" : cells[0].Error ?? cells[0].BuildText();
        }
        catch (Exception e) { EditorUtility.DisplayDialog("CSVの読込に失敗しました", e.Message, "閉じる"); }
    }

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
