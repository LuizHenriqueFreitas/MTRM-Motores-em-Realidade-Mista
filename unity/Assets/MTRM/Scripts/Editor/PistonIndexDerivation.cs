using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PistonIndexDerivation
{
    private const string PrefabPath =
        "Assets/MTRM/Prefabs/Engine/StateEngineManager.prefab";
    private const string OutputPath =
        "../../docs/piston_index_derivation.csv";

    [MenuItem("MTRM/Identidade/Derivar índices dos pistões")]
    public static void Export()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var csv = new StringBuilder();
            csv.AppendLine(
                "representation,mesh_path,projected_position,order," +
                "proposed_instance_index,distance_to_flywheel," +
                "distance_to_crankshaft_gear,landmarks_agree," +
                "blender_suffix,suffix_matches_proposed_index,method_note");

            foreach (Transform representation in new[]
                     {
                         root.transform.Find("4L.blend-8vExpande"),
                         root.transform.Find("4L.blend-8vLigaDesliga")
                     })
            {
                if (representation == null)
                    throw new InvalidOperationException(
                        "Engine representation is missing.");
                DeriveRepresentation(representation, csv);
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

    private static void DeriveRepresentation(
        Transform representation,
        StringBuilder csv)
    {
        MeshRenderer crankshaft = FindSingleTaggedRenderer(
            representation,
            EngineComponentId.Virabrequim);
        MeshRenderer flywheel = FindSingleTaggedRenderer(
            representation,
            EngineComponentId.Volante);
        MeshRenderer gear = FindSingleTaggedRenderer(
            representation,
            EngineComponentId.EngrenagemVirabrequim);
        MeshRenderer[] pistons = FindTaggedRenderers(
            representation,
            EngineComponentId.Pistao);
        if (pistons.Length != 4)
            throw new InvalidOperationException(
                $"Expected four pistons, found {pistons.Length}.");

        Vector3 axis = PrincipalWorldAxis(crankshaft);
        Vector3 origin = gear.bounds.center;
        if (Vector3.Dot(flywheel.bounds.center - origin, axis) < 0f)
            axis = -axis;

        List<PistonProjection> projections = pistons
            .Select(renderer => new PistonProjection(
                renderer,
                Vector3.Dot(renderer.bounds.center - origin, axis),
                Vector3.Distance(
                    renderer.bounds.center,
                    flywheel.bounds.center),
                Vector3.Distance(
                    renderer.bounds.center,
                    gear.bounds.center)))
            .OrderBy(item => item.ProjectedPosition)
            .ToList();

        PistonProjection farthestFromFlywheel = projections
            .OrderByDescending(item => item.DistanceToFlywheel)
            .First();
        PistonProjection nearestToGear = projections
            .OrderBy(item => item.DistanceToGear)
            .First();
        bool landmarksAgree =
            farthestFromFlywheel.Renderer == nearestToGear.Renderer;
        if (!landmarksAgree)
        {
            throw new InvalidOperationException(
                $"{RepresentationName(representation)}: flywheel and " +
                "crankshaft gear disagree on cylinder 1.");
        }

        for (int index = 0; index < projections.Count; index++)
        {
            PistonProjection piston = projections[index];
            Append(
                csv,
                RepresentationName(representation),
                HierarchyPath(piston.Renderer.transform, representation),
                Format(piston.ProjectedPosition),
                (index + 1).ToString(CultureInfo.InvariantCulture),
                (index + 1).ToString(CultureInfo.InvariantCulture),
                Format(piston.DistanceToFlywheel),
                Format(piston.DistanceToGear),
                "sim",
                BlenderSuffix(piston.Renderer.name),
                BlenderSuffixMatches(
                    piston.Renderer.name,
                    index + 1)
                    ? "sim"
                    : "nao",
                "O sufixo do Blender coincide com o índice proposto; " +
                "a coincidência foi observada depois e não foi usada " +
                "na derivação geométrica.");
        }
    }

    private static MeshRenderer FindSingleTaggedRenderer(
        Transform representation,
        EngineComponentId id)
    {
        MeshRenderer[] renderers = FindTaggedRenderers(representation, id);
        if (renderers.Length != 1)
            throw new InvalidOperationException(
                $"Expected one {id}, found {renderers.Length}.");
        return renderers[0];
    }

    private static MeshRenderer[] FindTaggedRenderers(
        Transform representation,
        EngineComponentId id)
    {
        return representation
            .GetComponentsInChildren<ComponentTag>(true)
            .Where(tag => tag.Id == id)
            .Select(tag => tag.GetComponent<MeshRenderer>())
            .Where(renderer => renderer != null)
            .ToArray();
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

    private static string BlenderSuffix(string name)
    {
        int separator = name.LastIndexOf('.');
        return separator >= 0
            ? name.Substring(separator)
            : "(sem sufixo)";
    }

    private static bool BlenderSuffixMatches(
        string name,
        int proposedIndex)
    {
        if (proposedIndex == 1)
            return !name.Contains(".");
        return name.EndsWith(
            "." + (proposedIndex - 1).ToString("000"),
            StringComparison.Ordinal);
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
        Debug.Log($"MTRM piston index derivation written to {path}");
    }

    private sealed class PistonProjection
    {
        public PistonProjection(
            MeshRenderer renderer,
            float projectedPosition,
            float distanceToFlywheel,
            float distanceToGear)
        {
            Renderer = renderer;
            ProjectedPosition = projectedPosition;
            DistanceToFlywheel = distanceToFlywheel;
            DistanceToGear = distanceToGear;
        }

        public MeshRenderer Renderer { get; }
        public float ProjectedPosition { get; }
        public float DistanceToFlywheel { get; }
        public float DistanceToGear { get; }
    }
}
