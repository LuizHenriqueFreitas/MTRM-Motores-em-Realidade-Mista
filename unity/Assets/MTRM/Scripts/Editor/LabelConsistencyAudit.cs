using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class LabelConsistencyAudit
{
    private const string PrefabPath =
        "Assets/MTRM/Prefabs/Engine/StateEngineManager.prefab";
    private const string BindingsPath = "../../docs/component_bindings.csv";
    private const string OutputPath = "../../docs/label_consistency_audit.csv";

    [MenuItem("MTRM/Identidade/Auditar consistência dos textos")]
    public static void Export()
    {
        Dictionary<string, string> rules = LoadRules();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var csv = new StringBuilder();
            csv.AppendLine(
                "representation,engine_component_id,mesh_path," +
                "tooltip_title,display_name,educational_description," +
                "resolution_rule,suspeito");
            int count = 0;
            int suspectCount = 0;
            foreach (Transform representation in new[]
                     {
                         root.transform.Find("4L.blend-8vExpande"),
                         root.transform.Find("4L.blend-8vLigaDesliga")
                     })
            {
                string representationName =
                    representation.name.Contains("Expande")
                        ? "Expande"
                        : "LigaDesliga";
                foreach (ComponentTag tag in representation
                             .GetComponentsInChildren<ComponentTag>(true)
                             .Where(item =>
                                 item.Id != EngineComponentId.CorreiaDentada)
                             .OrderBy(item =>
                                 RelativePath(item.transform, representation)))
                {
                    string path = RelativePath(tag.transform, representation);
                    string key = representationName + "\n" + path;
                    if (!rules.TryGetValue(key, out string rule))
                        throw new InvalidOperationException(
                            $"Binding not found for {representationName}/{path}.");
                    ToolTipTrigger tooltip =
                        tag.GetComponent<ToolTipTrigger>();
                    string title = tooltip != null
                        ? tooltip.TitleText
                        : string.Empty;
                    bool ruleResolved =
                        !string.Equals(
                            rule,
                            "ToolTipTrigger.titleText exact",
                            StringComparison.Ordinal);
                    bool suspect =
                        ruleResolved &&
                        !string.Equals(
                            title,
                            tag.DisplayName,
                            StringComparison.Ordinal);
                    count++;
                    if (suspect)
                        suspectCount++;
                    Append(
                        csv,
                        representationName,
                        tag.Id.ToString(),
                        path,
                        title,
                        tag.DisplayName,
                        tag.EducationalDescription,
                        rule,
                        suspect ? "true" : "false");
                }
            }

            if (count != 296)
                throw new InvalidOperationException(
                    $"Expected 296 labels, found {count}.");
            string output = Path.GetFullPath(
                Path.Combine(Application.dataPath, OutputPath));
            File.WriteAllText(output, csv.ToString(), new UTF8Encoding(false));
            Debug.Log(
                $"MTRM label consistency audit: labels={count}, " +
                $"suspects={suspectCount}, output={output}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void ExportFromCommandLine()
    {
        try
        {
            Export();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static Dictionary<string, string> LoadRules()
    {
        string path = Path.GetFullPath(
            Path.Combine(Application.dataPath, BindingsPath));
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        List<string> header = Parse(lines[0]);
        int representationIndex = header.IndexOf("representation");
        int pathIndex = header.IndexOf("mesh_path");
        int ruleIndex = header.IndexOf("resolution_rule");
        if (representationIndex < 0 || pathIndex < 0 || ruleIndex < 0)
            throw new InvalidOperationException(
                "component_bindings.csv has no required audit columns.");
        return lines.Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(Parse)
            .ToDictionary(
                fields =>
                    fields[representationIndex] + "\n" + fields[pathIndex],
                fields => fields[ruleIndex],
                StringComparer.Ordinal);
    }

    private static List<string> Parse(string line)
    {
        var result = new List<string>();
        var value = new StringBuilder();
        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            char current = line[index];
            if (current == '"')
            {
                if (quoted && index + 1 < line.Length &&
                    line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (current == ',' && !quoted)
            {
                result.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(current);
            }
        }
        result.Add(value.ToString());
        return result;
    }

    private static string RelativePath(Transform current, Transform root)
    {
        var names = new Stack<string>();
        while (current != null && current != root)
        {
            names.Push(current.name);
            current = current.parent;
        }
        return string.Join("/", names);
    }

    private static void Append(StringBuilder csv, params string[] values) =>
        csv.AppendLine(string.Join(",", values.Select(Csv)));

    private static string Csv(string value) =>
        "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
}
