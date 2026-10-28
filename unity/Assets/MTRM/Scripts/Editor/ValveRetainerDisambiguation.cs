using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ValveRetainerDisambiguation
{
    private const string PrefabPath =
        "Assets/MTRM/Prefabs/Engine/StateEngineManager.prefab";
    private const string OutputPath =
        "../../docs/valve_retainer_disambiguation.csv";

    [MenuItem("MTRM/Identidade/Desambiguar válvula e prato de mola")]
    public static void Export()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var csv = new StringBuilder();
            csv.AppendLine(
                "representation,cylinder_index,valve_type,sub_index," +
                "serialized_haste_path,serialized_base_path," +
                "haste_tag_id,haste_display_name,haste_educational_description," +
                "base_tag_id,base_display_name,base_educational_description," +
                "spring_tag_id,spring_display_name,spring_educational_description," +
                "axis_x,axis_y,axis_z,candidate_axes_angle_deg," +
                "haste_axial_extent,base_axial_extent,extent_ratio,extent_margin," +
                "haste_distance_to_piston,base_distance_to_piston,piston_margin," +
                "haste_distance_to_camshaft,base_distance_to_camshaft,camshaft_margin," +
                "test_a_valve,test_a_retainer,test_b_valve,test_b_retainer," +
                "test_b_internal_agreement,tests_agree");

            int total = 0;
            int agreements = 0;
            foreach (Transform representation in Representations(root.transform))
            {
                List<ComponentTag> tags = representation
                    .GetComponentsInChildren<ComponentTag>(true)
                    .ToList();
                Dictionary<int, ComponentTag> pistons = tags
                    .Where(tag => tag.Id == EngineComponentId.Pistao)
                    .ToDictionary(tag => tag.InstanceIndex);
                Dictionary<int, ComponentTag> camshafts = tags
                    .Where(tag => tag.Id == EngineComponentId.ArvoreDeCames)
                    .ToDictionary(tag => tag.SubIndex);

                var valves = representation
                    .GetComponentsInChildren<Valvula>(true)
                    .Select(component => ReadValve(component, representation))
                    .OrderBy(item => item.Cylinder)
                    .ThenBy(item => item.Type)
                    .ThenBy(item => Path(item.Haste.transform, representation))
                    .GroupBy(item => (item.Cylinder, item.Type))
                    .SelectMany(group => group.Select((item, index) =>
                        new ValveRow(item, index + 1)))
                    .ToList();

                if (valves.Count != 16 || pistons.Count != 4 ||
                    camshafts.Count != 2)
                    throw new InvalidOperationException(
                        $"Unexpected inventory in {representation.name}: " +
                        $"valves={valves.Count}, pistons={pistons.Count}, " +
                        $"camshafts={camshafts.Count}.");

                foreach (ValveRow row in valves)
                {
                    ComponentTag hasteTag =
                        RequiredTag(row.Data.Haste);
                    ComponentTag baseTag =
                        RequiredTag(row.Data.Base);
                    ComponentTag springTag =
                        RequiredTag(row.Data.Spring);
                    Vector3 hasteAxis = MovementAxis(row.Data, row.Data.Haste);
                    Vector3 baseAxis = MovementAxis(row.Data, row.Data.Base);
                    float axesAngle = Mathf.Min(
                        Vector3.Angle(hasteAxis, baseAxis),
                        Vector3.Angle(hasteAxis, -baseAxis));
                    if (Vector3.Dot(hasteAxis, baseAxis) < 0f)
                        baseAxis = -baseAxis;
                    Vector3 axis = (hasteAxis + baseAxis).normalized;

                    float hasteExtent = AxialExtent(row.Data.Haste, axis);
                    float baseExtent = AxialExtent(row.Data.Base, axis);
                    GameObject testAValve = hasteExtent >= baseExtent
                        ? row.Data.Haste
                        : row.Data.Base;
                    GameObject testARetainer = testAValve == row.Data.Haste
                        ? row.Data.Base
                        : row.Data.Haste;

                    Vector3 pistonCenter = Center(pistons[row.Data.Cylinder].gameObject);
                    int camSubIndex = row.Data.Type == 0 ? 1 : 2;
                    Vector3 camCenter = Center(camshafts[camSubIndex].gameObject);
                    float hastePiston = Vector3.Distance(
                        Center(row.Data.Haste), pistonCenter);
                    float basePiston = Vector3.Distance(
                        Center(row.Data.Base), pistonCenter);
                    float hasteCam = Vector3.Distance(
                        Center(row.Data.Haste), camCenter);
                    float baseCam = Vector3.Distance(
                        Center(row.Data.Base), camCenter);

                    GameObject pistonValve = hastePiston <= basePiston
                        ? row.Data.Haste
                        : row.Data.Base;
                    GameObject camRetainer = hasteCam <= baseCam
                        ? row.Data.Haste
                        : row.Data.Base;
                    bool testBInternal = pistonValve != camRetainer;
                    GameObject testBValve = testBInternal ? pistonValve : null;
                    GameObject testBRetainer = testBInternal ? camRetainer : null;
                    bool agree = testBInternal &&
                                 testAValve == testBValve &&
                                 testARetainer == testBRetainer;
                    total++;
                    if (agree)
                        agreements++;

                    Append(
                        csv,
                        RepresentationName(representation),
                        row.Data.Cylinder.ToString(CultureInfo.InvariantCulture),
                        row.Data.Type == 0 ? "Admissao" : "Escape",
                        row.SubIndex.ToString(CultureInfo.InvariantCulture),
                        Path(row.Data.Haste.transform, representation),
                        Path(row.Data.Base.transform, representation),
                        hasteTag.Id.ToString(),
                        hasteTag.DisplayName,
                        hasteTag.EducationalDescription,
                        baseTag.Id.ToString(),
                        baseTag.DisplayName,
                        baseTag.EducationalDescription,
                        springTag.Id.ToString(),
                        springTag.DisplayName,
                        springTag.EducationalDescription,
                        Format(axis.x), Format(axis.y), Format(axis.z),
                        Format(axesAngle),
                        Format(hasteExtent), Format(baseExtent),
                        Format(Mathf.Max(hasteExtent, baseExtent) /
                               Mathf.Max(0.000001f, Mathf.Min(hasteExtent, baseExtent))),
                        Format(Mathf.Abs(hasteExtent - baseExtent)),
                        Format(hastePiston), Format(basePiston),
                        Format(Mathf.Abs(hastePiston - basePiston)),
                        Format(hasteCam), Format(baseCam),
                        Format(Mathf.Abs(hasteCam - baseCam)),
                        Name(testAValve), Name(testARetainer),
                        Name(testBValve), Name(testBRetainer),
                        testBInternal ? "true" : "false",
                        agree ? "true" : "false");
                }
            }

            string output = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, OutputPath));
            File.WriteAllText(output, csv.ToString(), new UTF8Encoding(false));
            Debug.Log(
                $"MTRM valve/retainer disambiguation: {agreements}/{total} " +
                $"sets agree between independent tests. Output={output}");
            if (total != 32 || agreements != 32)
                throw new InvalidOperationException(
                    $"Valve/retainer tests disagree: {agreements}/{total}.");
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

    private static ValveData ReadValve(
        Valvula component,
        Transform representation)
    {
        var serialized = new SerializedObject(component);
        var data = new ValveData(
            component,
            serialized.FindProperty("cicloValvula").intValue,
            serialized.FindProperty("tipoValvula").intValue,
            serialized.FindProperty("eixoMovimento").enumValueIndex,
            serialized.FindProperty("hasteValvula").objectReferenceValue as GameObject,
            serialized.FindProperty("molaValvula").objectReferenceValue as GameObject,
            serialized.FindProperty("baseValvula").objectReferenceValue as GameObject);
        if (data.Haste == null || data.Spring == null || data.Base == null)
            throw new InvalidOperationException(
                $"Missing candidate at {Path(component.transform, representation)}.");
        return data;
    }

    private static Vector3 MovementAxis(ValveData valve, GameObject candidate)
    {
        Vector3 local = valve.Axis == 0
            ? Vector3.right
            : valve.Axis == 1 ? Vector3.up : Vector3.forward;
        return candidate.transform.TransformDirection(local).normalized;
    }

    private static ComponentTag RequiredTag(GameObject target)
    {
        ComponentTag tag = target.GetComponent<ComponentTag>();
        if (tag == null)
            throw new InvalidOperationException(
                $"ComponentTag missing at {target.name}.");
        return tag;
    }

    private static float AxialExtent(GameObject candidate, Vector3 axis)
    {
        MeshFilter[] filters = candidate.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length == 0)
            throw new InvalidOperationException($"No mesh in {candidate.name}.");
        float minimum = float.PositiveInfinity;
        float maximum = float.NegativeInfinity;
        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null)
                continue;
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                float projection = Vector3.Dot(
                    filter.transform.TransformPoint(vertex),
                    axis);
                minimum = Mathf.Min(minimum, projection);
                maximum = Mathf.Max(maximum, projection);
            }
        }
        if (float.IsInfinity(minimum))
            throw new InvalidOperationException($"No vertices in {candidate.name}.");
        return maximum - minimum;
    }

    private static Vector3 Center(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return target.transform.position;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1))
            bounds.Encapsulate(renderer.bounds);
        return bounds.center;
    }

    private static IEnumerable<Transform> Representations(Transform root)
    {
        Transform expanded = root.Find("4L.blend-8vExpande");
        Transform running = root.Find("4L.blend-8vLigaDesliga");
        if (expanded == null || running == null)
            throw new InvalidOperationException("Engine representations missing.");
        return new[] { expanded, running };
    }

    private static string RepresentationName(Transform representation) =>
        representation.name.Contains("Expande") ? "Expande" : "LigaDesliga";

    private static string Path(Transform current, Transform root)
    {
        var names = new Stack<string>();
        while (current != null && current != root)
        {
            names.Push(current.name);
            current = current.parent;
        }
        return string.Join("/", names);
    }

    private static string Name(GameObject value) =>
        value == null ? string.Empty : value.name;

    private static string Format(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static void Append(StringBuilder csv, params string[] fields) =>
        csv.AppendLine(string.Join(",", fields.Select(Csv)));

    private static string Csv(string value) =>
        "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

    private sealed class ValveData
    {
        public ValveData(
            Valvula component,
            int cylinder,
            int type,
            int axis,
            GameObject haste,
            GameObject spring,
            GameObject @base)
        {
            Component = component;
            Cylinder = cylinder;
            Type = type;
            Axis = axis;
            Haste = haste;
            Spring = spring;
            Base = @base;
        }

        public Valvula Component { get; }
        public int Cylinder { get; }
        public int Type { get; }
        public int Axis { get; }
        public GameObject Haste { get; }
        public GameObject Spring { get; }
        public GameObject Base { get; }
    }

    private sealed class ValveRow
    {
        public ValveRow(ValveData data, int subIndex)
        {
            Data = data;
            SubIndex = subIndex;
        }

        public ValveData Data { get; }
        public int SubIndex { get; }
    }
}
