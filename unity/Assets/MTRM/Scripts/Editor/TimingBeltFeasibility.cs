using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TimingBeltFeasibility
{
    private const string ScenePath =
        "Assets/MTRM/Scenes/DesktopScene.unity";
    private const string OutputPath = "../docs/belt_feasibility.csv";

    [MenuItem("MTRM/Correia/Auditar viabilidade")]
    public static void Export()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        List<Gear> gears = UnityEngine.Object
            .FindObjectsOfType<ComponentTag>(true)
            .Where(tag =>
                tag.Id == EngineComponentId.EngrenagemVirabrequim ||
                tag.Id == EngineComponentId.EngrenagemDeComando)
            .Select(tag => new Gear(tag))
            .OrderBy(gear => gear.Representation)
            .ThenBy(gear => gear.Tag.Id)
            .ThenBy(gear => gear.Tag.SubIndex)
            .ToList();
        if (gears.Count != 6)
            throw new InvalidOperationException(
                $"Esperadas seis engrenagens etiquetadas; encontradas {gears.Count}.");

        StringBuilder csv = new StringBuilder();
        csv.AppendLine(
            "representation,component_id,sub_index,path," +
            "center_x,center_y,center_z,axis_x,axis_y,axis_z," +
            "estimated_radius,distance_to_common_plane," +
            "max_axis_angle_deg,coplanarity_limit,parallel_pass," +
            "coplanar_pass,belt_span,twist_angle_deg,twist_pass");
        foreach (IGrouping<EngineRepresentation, Gear> group in
                 gears.GroupBy(gear => gear.Representation))
        {
            Gear[] set = group.ToArray();
            float minimumRadius = set.Min(gear => gear.Radius);
            float limit = minimumRadius * 0.1f;
            foreach (Gear gear in set)
            {
                Gear[] others = set.Where(item => item != gear).ToArray();
                Vector3 commonAxis = AlignedAverageAxis(set, gear.Axis);
                Vector3 otherCenter =
                    (others[0].Center + others[1].Center) * 0.5f;
                float planeDistance = Mathf.Abs(Vector3.Dot(
                    gear.Center - otherCenter, commonAxis));
                float maxAngle = set.Where(item => item != gear)
                    .Max(item => AxisAngle(gear.Axis, item.Axis));
                Gear crank = set.Single(item =>
                    item.Tag.Id == EngineComponentId.EngrenagemVirabrequim);
                Gear[] pairTargets = gear == crank
                    ? set.Where(item => item != crank).ToArray()
                    : new[] { crank };
                float beltSpan = pairTargets.Min(item =>
                    PerpendicularSpan(gear.Center, item.Center, commonAxis));
                float pairAxial = pairTargets.Max(item =>
                    Mathf.Abs(Vector3.Dot(
                        gear.Center - item.Center, commonAxis)));
                float twistAngle = Mathf.Atan2(pairAxial, beltSpan) *
                                   Mathf.Rad2Deg;
                csv.AppendLine(string.Join(",",
                    gear.Representation,
                    gear.Tag.Id,
                    gear.Tag.SubIndex,
                    Quote(gear.Path),
                    F(gear.Center.x), F(gear.Center.y), F(gear.Center.z),
                    F(gear.Axis.x), F(gear.Axis.y), F(gear.Axis.z),
                    F(gear.Radius), F(planeDistance), F(maxAngle), F(limit),
                    maxAngle <= 2f ? "true" : "false",
                    planeDistance < limit ? "true" : "false",
                    F(beltSpan), F(twistAngle),
                    twistAngle <= 2f ? "true" : "false"));
            }
        }
        File.WriteAllText(OutputPath, csv.ToString(), new UTF8Encoding(true));
        AssetDatabase.Refresh();
        Debug.Log($"MTRM_BELT_FEASIBILITY={OutputPath}");
    }

    public static void ExportFromCommandLine()
    {
        try { Export(); EditorApplication.Exit(0); }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static Vector3 AlignedAverageAxis(
        IEnumerable<Gear> gears, Vector3 reference)
    {
        Vector3 sum = Vector3.zero;
        foreach (Gear gear in gears)
            sum += Vector3.Dot(gear.Axis, reference) < 0f
                ? -gear.Axis : gear.Axis;
        return sum.normalized;
    }

    private static float AxisAngle(Vector3 first, Vector3 second) =>
        Mathf.Min(
            Vector3.Angle(first, second),
            Vector3.Angle(first, -second));

    private static float PerpendicularSpan(
        Vector3 first, Vector3 second, Vector3 axis)
    {
        Vector3 delta = second - first;
        return Vector3.ProjectOnPlane(delta, axis).magnitude;
    }

    private static string F(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);
    private static string Quote(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";

    private sealed class Gear
    {
        public Gear(ComponentTag tag)
        {
            Tag = tag;
            Representation = tag.Representation;
            Renderer renderer = tag.GetComponent<Renderer>();
            MeshFilter filter = tag.GetComponent<MeshFilter>();
            if (renderer == null || filter == null || filter.sharedMesh == null)
                throw new InvalidOperationException(
                    $"Engrenagem sem Renderer/MeshFilter: {tag.name}");
            Center = renderer.bounds.center;
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 size = bounds.size;
            int axisIndex = size.x <= size.y && size.x <= size.z ? 0 :
                size.y <= size.z ? 1 : 2;
            Vector3 localAxis = axisIndex == 0 ? Vector3.right :
                axisIndex == 1 ? Vector3.up : Vector3.forward;
            Axis = tag.transform.TransformDirection(localAxis).normalized;
            Vector3 scale = tag.transform.lossyScale;
            float[] dimensions =
            {
                size.x * Mathf.Abs(scale.x),
                size.y * Mathf.Abs(scale.y),
                size.z * Mathf.Abs(scale.z)
            };
            Radius = dimensions.Where((_, index) => index != axisIndex)
                .Max() * 0.5f;
            Path = BuildPath(tag.transform);
        }

        public ComponentTag Tag { get; }
        public EngineRepresentation Representation { get; }
        public Vector3 Center { get; }
        public Vector3 Axis { get; }
        public float Radius { get; }
        public string Path { get; }
    }

    private static string BuildPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
