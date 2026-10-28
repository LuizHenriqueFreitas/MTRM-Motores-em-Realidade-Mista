using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ExpansionCoverageExporter
{
    private const string ScenePath = "Assets/MTRM/Scenes/DesktopScene.unity";
    private const string ClipPath =
        "Assets/MTRM/Animations/Engine/EngineStimulus_expand.anim";
    private const string CoveragePath = "../../docs/expansion_coverage.csv";
    private const string BindingPath =
        "../../docs/expansion_binding_audit.csv";
    private const string CylinderPath =
        "../../docs/cylinder_geometry_audit.csv";
    private const int SampleCount = 121;
    private const float MovementEpsilon = 0.00001f;

    [MenuItem("MTRM/Auditar cobertura da expansão")]
    public static void Export()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject stimulus = scene.GetRootGameObjects()
            .First(item => item.name == "StateEngineManager");
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
            throw new InvalidOperationException($"Animation clip not found: {ClipPath}");

        Transform[] representations =
        {
            stimulus.transform.Find("4L.blend-8vExpande"),
            stimulus.transform.Find("4L.blend-8vLigaDesliga")
        };
        if (representations.Any(item => item == null))
            throw new InvalidOperationException("Both engine representations are required.");

        WriteBindingAudit(clip, representations);
        WriteCoverageAndCylinderAudit(clip, representations);
        Debug.Log("MTRM expansion audit completed.");
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

    private static void WriteBindingAudit(
        AnimationClip clip,
        IReadOnlyList<Transform> representations)
    {
        EditorCurveBinding[] floatBindings =
            AnimationUtility.GetCurveBindings(clip);
        EditorCurveBinding[] objectBindings =
            AnimationUtility.GetObjectReferenceCurveBindings(clip);
        EditorCurveBinding[] bindings = floatBindings
            .Concat(objectBindings)
            .ToArray();
        var csv = new StringBuilder();
        csv.AppendLine(
            "representation,binding_kind,path,component_type,property_name,resolves");

        foreach (Transform representation in representations)
        {
            foreach (EditorCurveBinding binding in bindings)
            {
                Transform target = string.IsNullOrEmpty(binding.path)
                    ? representation
                    : representation.Find(binding.path);
                bool resolves = target != null &&
                    (binding.type == typeof(Transform) ||
                     binding.type == typeof(GameObject) ||
                     target.GetComponent(binding.type) != null);
                csv.Append(Csv(RepresentationName(representation)));
                csv.Append(',');
                csv.Append(Csv(floatBindings.Contains(binding) ? "float" : "object"));
                csv.Append(',');
                csv.Append(Csv(binding.path));
                csv.Append(',');
                csv.Append(Csv(binding.type.FullName));
                csv.Append(',');
                csv.Append(Csv(binding.propertyName));
                csv.Append(',');
                csv.AppendLine(resolves ? "true" : "false");
            }
        }

        Write(CoverageAbsolutePath(BindingPath), csv);
    }

    private static void WriteCoverageAndCylinderAudit(
        AnimationClip clip,
        IReadOnlyList<Transform> representations)
    {
        var coverageCsv = new StringBuilder();
        coverageCsv.AppendLine(
            "representation,mesh_path,tooltip_title,is_animated," +
            "maximum_displacement");
        var cylinderCsv = new StringBuilder();
        cylinderCsv.AppendLine(
            "representation,candidate_path,piston_path,contained_samples," +
            "total_samples,course_containment_fraction,kinematic_result," +
            "diagnostic_note");

        foreach (Transform representation in representations)
        {
            List<MeshRenderer> meshes = representation
                .GetComponentsInChildren<MeshRenderer>(true)
                .OrderBy(item => PathFrom(item.transform, representation))
                .ToList();
            if (meshes.Count != 148)
                throw new InvalidOperationException(
                    $"{RepresentationName(representation)}: expected 148 meshes, " +
                    $"found {meshes.Count}.");

            var probes = meshes.ToDictionary(
                renderer => renderer,
                renderer => MeshProbe.Create(renderer));
            List<MeshRenderer> cylinders = meshes
                .Where(item =>
                    item.name == "Cylinder" &&
                    item.GetComponent<ToolTipTrigger>() == null)
                .ToList();
            List<MeshRenderer> pistons = meshes
                .Where(item => TooltipTitle(item) == "Pistão")
                .ToList();
            if (cylinders.Count != 4 || pistons.Count != 4)
                throw new InvalidOperationException(
                    $"{RepresentationName(representation)}: expected four " +
                    $"candidates and four pistons, found {cylinders.Count} and " +
                    $"{pistons.Count}.");

            int[,] containment = new int[cylinders.Count, pistons.Count];
            AnimationMode.StartAnimationMode();
            try
            {
                for (int sample = 0; sample < SampleCount; sample++)
                {
                    float time = clip.length * sample / (SampleCount - 1f);
                    AnimationMode.SampleAnimationClip(
                        representation.gameObject,
                        clip,
                        time);
                    foreach (MeshProbe probe in probes.Values)
                        probe.Capture();
                    for (int candidate = 0; candidate < cylinders.Count; candidate++)
                    {
                        for (int piston = 0; piston < pistons.Count; piston++)
                        {
                            if (PointInsideMesh(
                                    cylinders[candidate],
                                    pistons[piston].bounds.center))
                            {
                                containment[candidate, piston]++;
                            }
                        }
                    }
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }

            foreach (MeshRenderer mesh in meshes)
            {
                MeshProbe probe = probes[mesh];
                coverageCsv.Append(Csv(RepresentationName(representation)));
                coverageCsv.Append(',');
                coverageCsv.Append(Csv(PathFrom(mesh.transform, representation)));
                coverageCsv.Append(',');
                coverageCsv.Append(Csv(TooltipTitle(mesh)));
                coverageCsv.Append(',');
                coverageCsv.Append(probe.MaximumDisplacement > MovementEpsilon
                    ? "true"
                    : "false");
                coverageCsv.Append(',');
                coverageCsv.AppendLine(Format(probe.MaximumDisplacement));
            }

            int[] assignment = BestAssignment(containment);
            for (int candidate = 0; candidate < cylinders.Count; candidate++)
            {
                int piston = assignment[candidate];
                float fraction =
                    containment[candidate, piston] / (float)SampleCount;
                string result = fraction >= 0.999999f
                    ? "contencao_completa"
                    : "contencao_incompleta";
                cylinderCsv.Append(Csv(RepresentationName(representation)));
                cylinderCsv.Append(',');
                cylinderCsv.Append(Csv(PathFrom(
                    cylinders[candidate].transform,
                    representation)));
                cylinderCsv.Append(',');
                cylinderCsv.Append(Csv(PathFrom(
                    pistons[piston].transform,
                    representation)));
                cylinderCsv.Append(',');
                cylinderCsv.Append(containment[candidate, piston]);
                cylinderCsv.Append(',');
                cylinderCsv.Append(SampleCount);
                cylinderCsv.Append(',');
                cylinderCsv.Append(Format(fraction));
                cylinderCsv.Append(',');
                cylinderCsv.Append(Csv(result));
                cylinderCsv.Append(',');
                cylinderCsv.AppendLine(Csv(
                    "Teste cinemático por centro do pistão dentro da malha " +
                    "fechada; emparelhamento global maximiza a contenção sem " +
                    "usar nome, posição estática ou ordem de exportação."));
            }
        }

        Write(CoverageAbsolutePath(CoveragePath), coverageCsv);
        Write(CoverageAbsolutePath(CylinderPath), cylinderCsv);
    }

    private static int[] BestAssignment(int[,] counts)
    {
        int size = counts.GetLength(0);
        int[] best = null;
        int bestScore = -1;
        foreach (int[] permutation in Permutations(
                     Enumerable.Range(0, size).ToArray(),
                     0))
        {
            int score = 0;
            for (int index = 0; index < size; index++)
                score += counts[index, permutation[index]];
            if (score > bestScore)
            {
                bestScore = score;
                best = (int[])permutation.Clone();
            }
        }
        return best;
    }

    private static IEnumerable<int[]> Permutations(int[] values, int index)
    {
        if (index == values.Length)
        {
            yield return values;
            yield break;
        }
        for (int current = index; current < values.Length; current++)
        {
            Swap(values, index, current);
            foreach (int[] permutation in Permutations(values, index + 1))
                yield return permutation;
            Swap(values, index, current);
        }
    }

    private static void Swap(int[] values, int first, int second)
    {
        int value = values[first];
        values[first] = values[second];
        values[second] = value;
    }

    private static bool PointInsideMesh(
        MeshRenderer container,
        Vector3 worldPoint)
    {
        MeshFilter filter = container.GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (mesh == null)
            return false;
        Vector3 point = container.transform.InverseTransformPoint(worldPoint);
        if (!mesh.bounds.Contains(point))
            return false;

        Vector3 direction = new Vector3(1f, 0.371f, 0.127f).normalized;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        var distances = new List<float>();
        for (int index = 0; index < triangles.Length; index += 3)
        {
            if (RayTriangle(
                    point,
                    direction,
                    vertices[triangles[index]],
                    vertices[triangles[index + 1]],
                    vertices[triangles[index + 2]],
                    out float distance) &&
                !distances.Any(value => Mathf.Abs(value - distance) < 0.00001f))
            {
                distances.Add(distance);
            }
        }
        return distances.Count % 2 == 1;
    }

    private static bool RayTriangle(
        Vector3 origin,
        Vector3 direction,
        Vector3 first,
        Vector3 second,
        Vector3 third,
        out float distance)
    {
        const float epsilon = 0.0000001f;
        Vector3 edgeOne = second - first;
        Vector3 edgeTwo = third - first;
        Vector3 perpendicular = Vector3.Cross(direction, edgeTwo);
        float determinant = Vector3.Dot(edgeOne, perpendicular);
        if (Mathf.Abs(determinant) < epsilon)
        {
            distance = 0f;
            return false;
        }
        float inverse = 1f / determinant;
        Vector3 offset = origin - first;
        float u = inverse * Vector3.Dot(offset, perpendicular);
        if (u < 0f || u > 1f)
        {
            distance = 0f;
            return false;
        }
        Vector3 cross = Vector3.Cross(offset, edgeOne);
        float v = inverse * Vector3.Dot(direction, cross);
        if (v < 0f || u + v > 1f)
        {
            distance = 0f;
            return false;
        }
        distance = inverse * Vector3.Dot(edgeTwo, cross);
        return distance > epsilon;
    }

    private static string TooltipTitle(MeshRenderer renderer)
    {
        ToolTipTrigger tooltip = renderer.GetComponent<ToolTipTrigger>();
        return tooltip != null ? tooltip.TitleText : string.Empty;
    }

    private static string PathFrom(Transform current, Transform root)
    {
        var names = new Stack<string>();
        for (Transform cursor = current;
             cursor != null && cursor != root;
             cursor = cursor.parent)
        {
            names.Push(cursor.name);
        }
        return string.Join("/", names);
    }

    private static string RepresentationName(Transform representation)
    {
        return representation.name.Contains("Expande")
            ? "Expande"
            : "LigaDesliga";
    }

    private static string CoverageAbsolutePath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, relativePath));
    }

    private static void Write(string path, StringBuilder content)
    {
        File.WriteAllText(path, content.ToString(), new UTF8Encoding(false));
    }

    private static string Format(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string Csv(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }

    private sealed class MeshProbe
    {
        private readonly MeshRenderer renderer;
        private readonly Vector3[] localPoints;
        private readonly Vector3[] initialWorldPoints;

        private MeshProbe(MeshRenderer renderer)
        {
            this.renderer = renderer;
            Bounds bounds = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
            localPoints = BoundsPoints(bounds).ToArray();
            initialWorldPoints = localPoints
                .Select(renderer.transform.TransformPoint)
                .ToArray();
        }

        public float MaximumDisplacement { get; private set; }

        public static MeshProbe Create(MeshRenderer renderer)
        {
            return new MeshProbe(renderer);
        }

        public void Capture()
        {
            for (int index = 0; index < localPoints.Length; index++)
            {
                MaximumDisplacement = Mathf.Max(
                    MaximumDisplacement,
                    Vector3.Distance(
                        initialWorldPoints[index],
                        renderer.transform.TransformPoint(localPoints[index])));
            }
        }

        private static IEnumerable<Vector3> BoundsPoints(Bounds bounds)
        {
            yield return bounds.center;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                yield return bounds.center + Vector3.Scale(
                    bounds.extents,
                    new Vector3(x, y, z));
            }
        }
    }
}
