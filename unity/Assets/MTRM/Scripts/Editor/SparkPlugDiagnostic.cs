using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SparkPlugDiagnostic
{
    private const string PrefabPath =
        "Assets/MTRM/Prefabs/Engine/StateEngineManager.prefab";
    private const string OutputPath =
        "../../docs/spark_plug_diagnostic.csv";

    [MenuItem("MTRM/Identidade/Diagnosticar posições das velas")]
    public static void Export()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var csv = new StringBuilder();
            csv.AppendLine(
                "representation,spark_plug_path,world_x,world_y,world_z," +
                "projected_position,order,spacing_from_previous");
            foreach (Transform representation in new[]
                     {
                         root.transform.Find("4L.blend-8vExpande"),
                         root.transform.Find("4L.blend-8vLigaDesliga")
                     })
            {
                if (representation == null)
                    throw new InvalidOperationException(
                        "Engine representation is missing.");
                ExportRepresentation(representation, csv);
            }
            Write(csv);
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

    private static void ExportRepresentation(
        Transform representation,
        StringBuilder csv)
    {
        MeshRenderer crankshaft = SingleRenderer(
            representation,
            EngineComponentId.Virabrequim);
        MeshRenderer flywheel = SingleRenderer(
            representation,
            EngineComponentId.Volante);
        MeshRenderer gear = SingleRenderer(
            representation,
            EngineComponentId.EngrenagemVirabrequim);
        Vector3 axis = PrincipalWorldAxis(crankshaft);
        Vector3 origin = gear.bounds.center;
        if (Vector3.Dot(flywheel.bounds.center - origin, axis) < 0f)
            axis = -axis;

        List<SparkPosition> sparks = TaggedRenderers(
                representation,
                EngineComponentId.VelaIgnicao)
            .Select(renderer => new SparkPosition(
                renderer,
                renderer.bounds.center,
                Vector3.Dot(renderer.bounds.center - origin, axis)))
            .OrderBy(item => item.ProjectedPosition)
            .ToList();
        if (sparks.Count != 4)
            throw new InvalidOperationException(
                $"Expected four spark plugs, found {sparks.Count}.");

        for (int index = 0; index < sparks.Count; index++)
        {
            SparkPosition spark = sparks[index];
            Append(
                csv,
                RepresentationName(representation),
                HierarchyPath(spark.Renderer.transform, representation),
                Format(spark.Center.x),
                Format(spark.Center.y),
                Format(spark.Center.z),
                Format(spark.ProjectedPosition),
                (index + 1).ToString(CultureInfo.InvariantCulture),
                index == 0
                    ? string.Empty
                    : Format(
                        spark.ProjectedPosition -
                        sparks[index - 1].ProjectedPosition));
        }
    }

    private static MeshRenderer SingleRenderer(
        Transform representation,
        EngineComponentId id)
    {
        MeshRenderer[] renderers = TaggedRenderers(representation, id)
            .ToArray();
        if (renderers.Length != 1)
            throw new InvalidOperationException(
                $"Expected one {id}, found {renderers.Length}.");
        return renderers[0];
    }

    private static IEnumerable<MeshRenderer> TaggedRenderers(
        Transform representation,
        EngineComponentId id)
    {
        return representation
            .GetComponentsInChildren<ComponentTag>(true)
            .Where(tag => tag.Id == id)
            .Select(tag => tag.GetComponent<MeshRenderer>())
            .Where(renderer => renderer != null);
    }

    private static Vector3 PrincipalWorldAxis(MeshRenderer renderer)
    {
        Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null)
            throw new InvalidOperationException(
                "Crankshaft mesh is missing.");
        Vector3 size = mesh.bounds.size;
        Vector3 localAxis = size.x >= size.y && size.x >= size.z
            ? Vector3.right
            : size.y >= size.z ? Vector3.up : Vector3.forward;
        return renderer.transform.TransformDirection(localAxis).normalized;
    }

    private static string RepresentationName(Transform representation)
    {
        return representation.name.Contains("Expande")
            ? "Expande"
            : "LigaDesliga";
    }

    private static string HierarchyPath(Transform current, Transform root)
    {
        var names = new Stack<string>();
        while (current != null && current != root)
        {
            names.Push(current.name);
            current = current.parent;
        }
        return string.Join("/", names);
    }

    private static string Format(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static void Append(StringBuilder csv, params string[] fields)
    {
        csv.AppendLine(string.Join(",", fields.Select(Csv)));
    }

    private static string Csv(string value)
    {
        return "\"" + (value ?? string.Empty)
            .Replace("\"", "\"\"") + "\"";
    }

    private static void Write(StringBuilder csv)
    {
        string path = Path.GetFullPath(
            Path.Combine(Application.dataPath, OutputPath));
        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
        Debug.Log($"MTRM spark plug diagnostic written to {path}");
    }

    private sealed class SparkPosition
    {
        public SparkPosition(
            MeshRenderer renderer,
            Vector3 center,
            float projectedPosition)
        {
            Renderer = renderer;
            Center = center;
            ProjectedPosition = projectedPosition;
        }

        public MeshRenderer Renderer { get; }
        public Vector3 Center { get; }
        public float ProjectedPosition { get; }
    }
}
