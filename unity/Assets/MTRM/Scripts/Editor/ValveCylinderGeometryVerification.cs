using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ValveCylinderGeometryVerification
{
    private const string PrefabPath =
        "Assets/MTRM/Prefabs/Engine/StateEngineManager.prefab";
    private const string OutputPath =
        "../../docs/valve_cylinder_verification.csv";

    [MenuItem("MTRM/Identidade/Verificar cilindros das válvulas")]
    public static void Export()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var csv = new StringBuilder();
            csv.AppendLine(
                "representation,valve_path,component_id," +
                "declared_instance_index,nearest_piston_index," +
                "axial_distance_to_piston_1,axial_distance_to_piston_2," +
                "axial_distance_to_piston_3,axial_distance_to_piston_4," +
                "margin_to_second_nearest,concordancia");

            int agreements = 0;
            int total = 0;
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
                    csv,
                    ref agreements,
                    ref total);
            }

            string path = Path.GetFullPath(
                Path.Combine(Application.dataPath, OutputPath));
            File.WriteAllText(
                path,
                csv.ToString(),
                new UTF8Encoding(false));

            if (total != 32 || agreements != 32)
                throw new InvalidOperationException(
                    $"Valve cylinder geometry: {agreements}/{total} agree.");

            Debug.Log(
                "MTRM valve cylinder geometry verification: 32/32 " +
                "agreements (100%).");
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
        StringBuilder csv,
        ref int agreements,
        ref int total)
    {
        ComponentTag gear = Tags(representation)
            .Single(tag =>
                tag.Id == EngineComponentId.EngrenagemVirabrequim);
        ComponentTag flywheel = Tags(representation)
            .Single(tag => tag.Id == EngineComponentId.Volante);
        Vector3 axis =
            (flywheel.transform.position - gear.transform.position)
            .normalized;
        Vector3 origin = gear.transform.position;

        List<ComponentTag> pistons = Tags(representation)
            .Where(tag => tag.Id == EngineComponentId.Pistao)
            .OrderBy(tag => Project(tag, origin, axis))
            .ToList();
        if (pistons.Count != 4)
            throw new InvalidOperationException(
                $"Expected four pistons in {representation.name}.");

        List<ComponentTag> valves = Tags(representation)
            .Where(tag =>
                tag.Id == EngineComponentId.ValvulaAdmissao ||
                tag.Id == EngineComponentId.ValvulaEscape)
            .OrderBy(tag => tag.Id)
            .ThenBy(tag => tag.InstanceIndex)
            .ThenBy(tag => tag.SubIndex)
            .ToList();
        if (valves.Count != 16)
            throw new InvalidOperationException(
                $"Expected 16 valves in {representation.name}.");

        foreach (ComponentTag valve in valves)
        {
            float coordinate = Project(valve, origin, axis);
            float[] distances = pistons
                .Select(piston =>
                    Mathf.Abs(
                        coordinate - Project(piston, origin, axis)))
                .ToArray();
            int[] ordered = Enumerable.Range(0, 4)
                .OrderBy(index => distances[index])
                .ToArray();
            int nearest = ordered[0] + 1;
            bool agrees = valve.InstanceIndex == nearest;
            total++;
            if (agrees)
                agreements++;

            Append(
                csv,
                RepresentationName(representation),
                RelativePath(valve.transform, representation),
                valve.Id.ToString(),
                valve.InstanceIndex.ToString(CultureInfo.InvariantCulture),
                nearest.ToString(CultureInfo.InvariantCulture),
                Format(distances[0]),
                Format(distances[1]),
                Format(distances[2]),
                Format(distances[3]),
                Format(distances[ordered[1]] - distances[ordered[0]]),
                agrees ? "sim" : "nao");
        }
    }

    private static IEnumerable<ComponentTag> Tags(Transform root) =>
        root.GetComponentsInChildren<ComponentTag>(true);

    private static float Project(
        ComponentTag tag,
        Vector3 origin,
        Vector3 axis)
    {
        MeshRenderer renderer = tag.GetComponent<MeshRenderer>();
        Vector3 center = renderer != null
            ? renderer.bounds.center
            : tag.transform.position;
        return Vector3.Dot(center - origin, axis);
    }

    private static string RepresentationName(Transform representation) =>
        representation.name.Contains("Expande")
            ? "Expande"
            : "LigaDesliga";

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

    private static string Format(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static void Append(StringBuilder csv, params string[] fields) =>
        csv.AppendLine(string.Join(",", fields.Select(Csv)));

    private static string Csv(string value) =>
        "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
}
