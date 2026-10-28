using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class NamingHygieneValidator
{
    private const string BeforePath = "../../docs/naming_binding_before.csv";
    private const string AfterPath = "../../docs/naming_binding_after.csv";

    private static readonly Entry[] Entries =
    {
        new Entry(
            "Assets/MTRM/Scripts/Runtime/Desktop/CameraController.cs",
            "Assets/MTRM/Prefabs/Interactions/InputManager.prefab"),
        new Entry(
            "Assets/MTRM/Scripts/Runtime/Engine/Utils/MedidorBiela.cs",
            "Assets/MTRM/Prefabs/Engine/Engine4L_16v_Stimulus.prefab"),
        new Entry(
            "Assets/MTRM/Scripts/Runtime/Interaction/SoundController.cs",
            "Assets/MTRM/Prefabs/Engine/StateEngineManager.prefab")
    };

    [MenuItem("MTRM/Validar nomenclatura/Registrar antes")]
    public static void ExportBefore()
    {
        Export("before", BeforePath);
    }

    [MenuItem("MTRM/Validar nomenclatura/Registrar depois")]
    public static void ExportAfter()
    {
        Export("after", AfterPath);
    }

    public static void ExportBeforeFromCommandLine()
    {
        RunCommandLine(ExportBefore);
    }

    public static void ExportAfterFromCommandLine()
    {
        RunCommandLine(ExportAfter);
    }

    private static void RunCommandLine(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Export(string phase, string relativeOutputPath)
    {
        var csv = new StringBuilder();
        csv.AppendLine(
            "phase,script_path,script_guid,resolved_class,prefab_path," +
            "missing_scripts_in_prefab,prefab_sha256,serialized_fields");

        foreach (Entry entry in Entries)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                entry.ScriptPath);
            if (script == null)
                throw new InvalidOperationException(
                    $"MonoScript not found: {entry.ScriptPath}");

            GameObject root = PrefabUtility.LoadPrefabContents(entry.PrefabPath);
            try
            {
                int missingCount = root
                    .GetComponentsInChildren<Transform>(true)
                    .Sum(transform =>
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                            transform.gameObject));
                Type resolvedClass = script.GetClass();
                string serializedFields = resolvedClass == null
                    ? string.Empty
                    : ReadSerializedFields(root, resolvedClass);

                csv.Append(Csv(phase));
                csv.Append(',');
                csv.Append(Csv(entry.ScriptPath));
                csv.Append(',');
                csv.Append(Csv(AssetDatabase.AssetPathToGUID(entry.ScriptPath)));
                csv.Append(',');
                csv.Append(Csv(resolvedClass != null
                    ? resolvedClass.FullName
                    : string.Empty));
                csv.Append(',');
                csv.Append(Csv(entry.PrefabPath));
                csv.Append(',');
                csv.Append(missingCount);
                csv.Append(',');
                csv.Append(Csv(FileSha256(entry.PrefabPath)));
                csv.Append(',');
                csv.AppendLine(Csv(serializedFields));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        string outputPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, relativeOutputPath));
        File.WriteAllText(outputPath, csv.ToString(), new UTF8Encoding(false));
        Debug.Log($"MTRM naming binding audit ({phase}) written to {outputPath}");
    }

    private static string ReadSerializedFields(GameObject root, Type type)
    {
        Component component = root
            .GetComponentsInChildren(type, true)
            .Cast<Component>()
            .FirstOrDefault();
        if (component == null)
            return string.Empty;

        var serialized = new SerializedObject(component);
        SerializedProperty property = serialized.GetIterator();
        var values = new List<string>();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.propertyPath == "m_Script")
                continue;
            values.Add($"{property.propertyPath}={PropertyValue(property)}");
        }
        return string.Join(";", values);
    }

    private static string PropertyValue(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Integer:
                return property.longValue.ToString();
            case SerializedPropertyType.Float:
                return property.doubleValue.ToString("R");
            case SerializedPropertyType.String:
                return property.stringValue;
            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue != null
                    ? property.objectReferenceValue.name
                    : "null";
            case SerializedPropertyType.LayerMask:
                return property.intValue.ToString();
            default:
                return property.propertyType.ToString();
        }
    }

    private static string FileSha256(string assetPath)
    {
        string absolutePath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", assetPath));
        using (SHA256 algorithm = SHA256.Create())
        using (FileStream stream = File.OpenRead(absolutePath))
        {
            return BitConverter.ToString(algorithm.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }

    private static string Csv(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }

    private sealed class Entry
    {
        public Entry(string scriptPath, string prefabPath)
        {
            ScriptPath = scriptPath;
            PrefabPath = prefabPath;
        }

        public string ScriptPath { get; }
        public string PrefabPath { get; }
    }
}
