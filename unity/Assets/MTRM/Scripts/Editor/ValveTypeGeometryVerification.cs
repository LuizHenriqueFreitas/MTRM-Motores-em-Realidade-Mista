using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ValveTypeGeometryVerification
{
    private const string PrefabPath =
        "Assets/MTRM/Prefabs/Engine/StateEngineManager.prefab";
    private const string ValveOutputPath =
        "../../docs/valve_type_verification.csv";
    private const string CamshaftOutputPath =
        "../../docs/camshaft_type_verification.csv";

    [MenuItem("MTRM/Identidade/Verificar tipos por geometria")]
    public static void Export()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var valveCsv = new StringBuilder();
            valveCsv.AppendLine(
                "representation,valve_path,tipo_valvula_declared," +
                "nearest_manifold,distance_to_intake_manifold," +
                "distance_to_exhaust_manifold,concordancia");
            var camshaftCsv = new StringBuilder();
            camshaftCsv.AppendLine(
                "representation,camshaft_path,lado_comando_declared," +
                "nearest_valve_group,mean_perpendicular_distance_to_intake," +
                "mean_perpendicular_distance_to_exhaust,concordancia");

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
                    valveCsv,
                    camshaftCsv);
            }

            Write(ValveOutputPath, valveCsv);
            Write(CamshaftOutputPath, camshaftCsv);
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
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void VerifyRepresentation(
        Transform representation,
        StringBuilder valveCsv,
        StringBuilder camshaftCsv)
    {
        MeshRenderer intake = FindTaggedRenderer(
            representation,
            EngineComponentId.ColetorAr);
        MeshRenderer exhaust = FindTaggedRenderer(
            representation,
            EngineComponentId.ColetorEscape);

        List<ValveGeometry> valves = representation
            .GetComponentsInChildren<Valvula>(true)
            .Select(component =>
            {
                var serialized = new SerializedObject(component);
                int type = serialized.FindProperty("tipoValvula").intValue;
                GameObject head = serialized.FindProperty("hasteValvula")
                    .objectReferenceValue as GameObject;
                if (head == null)
                    throw new InvalidOperationException(
                        "Valvula.hasteValvula is empty.");
                MeshRenderer renderer = head.GetComponent<MeshRenderer>();
                if (renderer == null)
                    throw new InvalidOperationException(
                        "Referenced valve has no MeshRenderer.");
                return new ValveGeometry(type, renderer);
            })
            .OrderBy(item => HierarchyPath(
                item.Renderer.transform,
                representation))
            .ToList();
        if (valves.Count != 16)
            throw new InvalidOperationException(
                $"Expected 16 valves, found {valves.Count}.");

        string representationName = RepresentationName(representation);
        foreach (ValveGeometry valve in valves)
        {
            float intakeDistance = Vector3.Distance(
                valve.Renderer.bounds.center,
                intake.bounds.center);
            float exhaustDistance = Vector3.Distance(
                valve.Renderer.bounds.center,
                exhaust.bounds.center);
            int nearestType = intakeDistance < exhaustDistance ? 0 : 1;
            Append(
                valveCsv,
                representationName,
                HierarchyPath(valve.Renderer.transform, representation),
                TypeName(valve.DeclaredType),
                nearestType == 0 ? "ColetorAr" : "ColetorEscape",
                Format(intakeDistance),
                Format(exhaustDistance),
                nearestType == valve.DeclaredType ? "sim" : "nao");
        }

        foreach (ArvoreComando camshaft in representation
                     .GetComponentsInChildren<ArvoreComando>(true)
                     .OrderBy(item => item.GetLadoComando()))
        {
            MeshRenderer renderer = camshaft.GetComponent<MeshRenderer>();
            float intakeDistance = valves
                .Where(item => item.DeclaredType == 0)
                .Average(item => PerpendicularDistance(
                    item.Renderer.bounds.center,
                    renderer));
            float exhaustDistance = valves
                .Where(item => item.DeclaredType == 1)
                .Average(item => PerpendicularDistance(
                    item.Renderer.bounds.center,
                    renderer));
            int nearestType = intakeDistance < exhaustDistance ? 0 : 1;
            Append(
                camshaftCsv,
                representationName,
                HierarchyPath(renderer.transform, representation),
                TypeName(camshaft.GetLadoComando()),
                TypeName(nearestType),
                Format(intakeDistance),
                Format(exhaustDistance),
                nearestType == camshaft.GetLadoComando() ? "sim" : "nao");
        }
    }

    private static MeshRenderer FindTaggedRenderer(
        Transform representation,
        EngineComponentId id)
    {
        ComponentTag[] tags = representation
            .GetComponentsInChildren<ComponentTag>(true)
            .Where(item => item.Id == id)
            .ToArray();
        if (tags.Length != 1)
            throw new InvalidOperationException(
                $"Expected one {id}, found {tags.Length}.");
        MeshRenderer renderer = tags[0].GetComponent<MeshRenderer>();
        if (renderer == null)
            throw new InvalidOperationException(
                $"{id} tag has no MeshRenderer.");
        return renderer;
    }

    private static float PerpendicularDistance(
        Vector3 point,
        MeshRenderer axisRenderer)
    {
        Mesh mesh = axisRenderer.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null)
            throw new InvalidOperationException("Axis mesh is missing.");
        Bounds bounds = mesh.bounds;
        Vector3 size = bounds.size;
        Vector3 localAxis = size.x >= size.y && size.x >= size.z
            ? Vector3.right
            : size.y >= size.z ? Vector3.up : Vector3.forward;
        Vector3 worldAxis = axisRenderer.transform
            .TransformDirection(localAxis)
            .normalized;
        Vector3 origin = axisRenderer.transform.TransformPoint(bounds.center);
        return Vector3.Cross(point - origin, worldAxis).magnitude;
    }

    private static string RepresentationName(Transform representation)
    {
        return representation.name.Contains("Expande")
            ? "Expande"
            : "LigaDesliga";
    }

    private static string TypeName(int type)
    {
        if (type == 0)
            return "Admissao";
        if (type == 1)
            return "Escape";
        throw new InvalidOperationException($"Unexpected valve type {type}.");
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

    private static void Write(string relativePath, StringBuilder csv)
    {
        string path = Path.GetFullPath(
            Path.Combine(Application.dataPath, relativePath));
        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
        Debug.Log($"MTRM geometry verification written to {path}");
    }

    private sealed class ValveGeometry
    {
        public ValveGeometry(int declaredType, MeshRenderer renderer)
        {
            DeclaredType = declaredType;
            Renderer = renderer;
        }

        public int DeclaredType { get; }
        public MeshRenderer Renderer { get; }
    }
}
