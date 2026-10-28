using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CylinderIndexVerification
{
    private const string PrefabPath =
        "Assets/MTRM/Prefabs/Engine/StateEngineManager.prefab";
    private const string ComponentOutputPath =
        "../../docs/component_index_derivation.csv";
    private const string ValveOutputPath =
        "../../docs/valve_cylinder_verification.csv";

    [MenuItem("MTRM/Identidade/Verificar índices por cilindro")]
    public static void Export()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var componentCsv = new StringBuilder();
            componentCsv.AppendLine(
                "representation,component_id,part_role,mesh_path," +
                "proposed_instance_index,nearest_piston_path," +
                "axial_distance_to_nearest," +
                "axial_distance_to_second_nearest,margin");
            var valveCsv = new StringBuilder();
            valveCsv.AppendLine(
                "representation,valve_path,component_id," +
                "declared_instance_index,declared_sub_index," +
                "nearest_piston_index,axial_distance_to_piston_1," +
                "axial_distance_to_piston_2,axial_distance_to_piston_3," +
                "axial_distance_to_piston_4,margin_to_second_nearest," +
                "concordancia");

            foreach (Transform representation in new[]
                     {
                         root.transform.Find("4L.blend-8vExpande"),
                         root.transform.Find("4L.blend-8vLigaDesliga")
                     })
            {
                if (representation == null)
                    throw new InvalidOperationException(
                        "Engine representation is missing.");
                VerifyRepresentation(
                    representation,
                    componentCsv,
                    valveCsv);
            }

            Write(ComponentOutputPath, componentCsv);
            Write(ValveOutputPath, valveCsv);
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

    private static void VerifyRepresentation(
        Transform representation,
        StringBuilder componentCsv,
        StringBuilder valveCsv)
    {
        AxialReference axial = BuildAxialReference(representation);
        List<TaggedRenderer> pistons = TaggedRenderers(
                representation,
                EngineComponentId.Pistao)
            .OrderBy(item => item.Tag.InstanceIndex)
            .ToList();
        if (pistons.Count != 4 ||
            !pistons.Select(item => item.Tag.InstanceIndex)
                .SequenceEqual(new[] { 1, 2, 3, 4 }))
        {
            throw new InvalidOperationException(
                "Pistons must be indexed 1..4 before verification.");
        }

        foreach (EngineComponentId id in new[]
                 {
                     EngineComponentId.Biela,
                     EngineComponentId.PinoPistao,
                     EngineComponentId.VelaIgnicao
                 })
        {
            foreach (TaggedRenderer component in TaggedRenderers(
                         representation,
                         id))
            {
                List<DistanceToPiston> distances = Distances(
                    component.Renderer.bounds.center,
                    pistons,
                    axial);
                Append(
                    componentCsv,
                    RepresentationName(representation),
                    id.ToString(),
                    PartRole(component, id),
                    HierarchyPath(
                        component.Renderer.transform,
                        representation),
                    distances[0].Piston.Tag.InstanceIndex.ToString(
                        CultureInfo.InvariantCulture),
                    HierarchyPath(
                        distances[0].Piston.Renderer.transform,
                        representation),
                    Format(distances[0].Distance),
                    Format(distances[1].Distance),
                    Format(
                        distances[1].Distance -
                        distances[0].Distance));
            }
        }
        ValidateComponentDistribution(
            representation,
            pistons,
            axial);

        List<TaggedRenderer> valves = representation
            .GetComponentsInChildren<ComponentTag>(true)
            .Where(tag =>
                tag.Id == EngineComponentId.ValvulaAdmissao ||
                tag.Id == EngineComponentId.ValvulaEscape)
            .Select(ToTaggedRenderer)
            .OrderBy(item => item.Tag.Id)
            .ThenBy(item => item.Tag.InstanceIndex)
            .ThenBy(item => item.Tag.SubIndex)
            .ToList();
        if (valves.Count != 16)
            throw new InvalidOperationException(
                $"Expected 16 valves, found {valves.Count}.");

        var uniqueKeys = new HashSet<string>();
        foreach (TaggedRenderer valve in valves)
        {
            List<DistanceToPiston> distances = Distances(
                valve.Renderer.bounds.center,
                pistons,
                axial);
            int nearestIndex = distances[0].Piston.Tag.InstanceIndex;
            bool agrees = nearestIndex == valve.Tag.InstanceIndex;
            Append(
                valveCsv,
                RepresentationName(representation),
                HierarchyPath(valve.Renderer.transform, representation),
                valve.Tag.Id.ToString(),
                valve.Tag.InstanceIndex.ToString(
                    CultureInfo.InvariantCulture),
                valve.Tag.SubIndex.ToString(CultureInfo.InvariantCulture),
                nearestIndex.ToString(CultureInfo.InvariantCulture),
                Format(DistanceForIndex(distances, 1)),
                Format(DistanceForIndex(distances, 2)),
                Format(DistanceForIndex(distances, 3)),
                Format(DistanceForIndex(distances, 4)),
                Format(
                    distances[1].Distance -
                    distances[0].Distance),
                agrees ? "sim" : "nao");

            string key = string.Join(
                "|",
                valve.Tag.Id,
                valve.Tag.InstanceIndex,
                valve.Tag.SubIndex,
                valve.Tag.Representation);
            if (!uniqueKeys.Add(key))
                throw new InvalidOperationException(
                    $"Duplicate valve identity: {key}.");
        }

        foreach (IGrouping<(EngineComponentId Id, int Cylinder),
                     TaggedRenderer> group in valves.GroupBy(item =>
                     (item.Tag.Id, item.Tag.InstanceIndex)))
        {
            int[] subIndices = group
                .Select(item => item.Tag.SubIndex)
                .OrderBy(value => value)
                .ToArray();
            if (!subIndices.SequenceEqual(new[] { 1, 2 }))
            {
                throw new InvalidOperationException(
                    $"{group.Key} must have subIndex 1 and 2.");
            }
        }
    }

    private static void ValidateComponentDistribution(
        Transform representation,
        IReadOnlyCollection<TaggedRenderer> pistons,
        AxialReference axial)
    {
        foreach (EngineComponentId id in new[]
                 {
                     EngineComponentId.PinoPistao,
                     EngineComponentId.VelaIgnicao
                 })
        {
            int[] indices = TaggedRenderers(representation, id)
                .Select(component => Distances(
                    component.Renderer.bounds.center,
                    pistons,
                    axial)[0].Piston.Tag.InstanceIndex)
                .OrderBy(value => value)
                .ToArray();
            if (!indices.SequenceEqual(new[] { 1, 2, 3, 4 }))
                throw new InvalidOperationException(
                    $"{id} axial indices are not 1..4.");
        }

        List<(TaggedRenderer Component, int Index)> rods =
            TaggedRenderers(representation, EngineComponentId.Biela)
                .Select(component => (
                    component,
                    Distances(
                        component.Renderer.bounds.center,
                        pistons,
                        axial)[0].Piston.Tag.InstanceIndex))
                .ToList();
        foreach (int cylinder in Enumerable.Range(1, 4))
        {
            var group = rods
                .Where(item => item.Index == cylinder)
                .ToList();
            if (group.Count != 2 ||
                group.Count(item =>
                    PartRole(
                        item.Component,
                        EngineComponentId.Biela) == "corpo") != 1 ||
                group.Count(item =>
                    PartRole(
                        item.Component,
                        EngineComponentId.Biela) == "capa") != 1)
            {
                throw new InvalidOperationException(
                    $"Biela cylinder {cylinder} must contain one body " +
                    "and one cap with the same axial index.");
            }
        }
    }

    private static string PartRole(
        TaggedRenderer component,
        EngineComponentId id)
    {
        if (id != EngineComponentId.Biela)
            return "componente";
        return component.Renderer.name.StartsWith(
            "biela parte de baixo",
            StringComparison.OrdinalIgnoreCase)
            ? "capa"
            : "corpo";
    }

    private static IEnumerable<TaggedRenderer> TaggedRenderers(
        Transform representation,
        EngineComponentId id)
    {
        return representation
            .GetComponentsInChildren<ComponentTag>(true)
            .Where(tag => tag.Id == id)
            .Select(ToTaggedRenderer);
    }

    private static TaggedRenderer ToTaggedRenderer(ComponentTag tag)
    {
        MeshRenderer renderer = tag.GetComponent<MeshRenderer>();
        if (renderer == null)
            throw new InvalidOperationException(
                $"{tag.name} has no MeshRenderer.");
        return new TaggedRenderer(tag, renderer);
    }

    private static List<DistanceToPiston> Distances(
        Vector3 center,
        IEnumerable<TaggedRenderer> pistons,
        AxialReference axial)
    {
        float coordinate = axial.Project(center);
        return pistons
            .Select(piston => new DistanceToPiston(
                piston,
                Mathf.Abs(
                    coordinate -
                    axial.Project(piston.Renderer.bounds.center))))
            .OrderBy(item => item.Distance)
            .ToList();
    }

    private static AxialReference BuildAxialReference(
        Transform representation)
    {
        MeshRenderer crankshaft = TaggedRenderers(
                representation,
                EngineComponentId.Virabrequim)
            .Single().Renderer;
        MeshRenderer gear = TaggedRenderers(
                representation,
                EngineComponentId.EngrenagemVirabrequim)
            .Single().Renderer;
        MeshRenderer flywheel = TaggedRenderers(
                representation,
                EngineComponentId.Volante)
            .Single().Renderer;
        Mesh mesh = crankshaft.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null)
            throw new InvalidOperationException(
                "Crankshaft mesh is missing.");
        Vector3 size = mesh.bounds.size;
        Vector3 localAxis = size.x >= size.y && size.x >= size.z
            ? Vector3.right
            : size.y >= size.z ? Vector3.up : Vector3.forward;
        Vector3 axis = crankshaft.transform
            .TransformDirection(localAxis)
            .normalized;
        Vector3 origin = gear.bounds.center;
        if (Vector3.Dot(
                flywheel.bounds.center - origin,
                axis) < 0f)
        {
            axis = -axis;
        }
        return new AxialReference(origin, axis);
    }

    private static float DistanceForIndex(
        IEnumerable<DistanceToPiston> distances,
        int instanceIndex)
    {
        return distances.Single(item =>
            item.Piston.Tag.InstanceIndex == instanceIndex).Distance;
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

    private static void Write(
        string relativePath,
        StringBuilder csv)
    {
        string path = Path.GetFullPath(
            Path.Combine(Application.dataPath, relativePath));
        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
        Debug.Log($"MTRM cylinder index verification written to {path}");
    }

    private sealed class TaggedRenderer
    {
        public TaggedRenderer(ComponentTag tag, MeshRenderer renderer)
        {
            Tag = tag;
            Renderer = renderer;
        }

        public ComponentTag Tag { get; }
        public MeshRenderer Renderer { get; }
    }

    private sealed class DistanceToPiston
    {
        public DistanceToPiston(
            TaggedRenderer piston,
            float distance)
        {
            Piston = piston;
            Distance = distance;
        }

        public TaggedRenderer Piston { get; }
        public float Distance { get; }
    }

    private sealed class AxialReference
    {
        private readonly Vector3 origin;
        private readonly Vector3 axis;

        public AxialReference(Vector3 origin, Vector3 axis)
        {
            this.origin = origin;
            this.axis = axis;
        }

        public float Project(Vector3 point)
        {
            return Vector3.Dot(point - origin, axis);
        }
    }
}
