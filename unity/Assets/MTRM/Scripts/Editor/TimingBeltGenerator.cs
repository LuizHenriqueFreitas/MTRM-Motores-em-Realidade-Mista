using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class TimingBeltGenerator
{
    private const string PrefabPath =
        "Assets/MTRM/Prefabs/Engine/StateEngineManager.prefab";
    private const string AssetPath =
        "Assets/MTRM/Generated/TimingBelt.asset";
    private const string ObjectName = "MTRM_TimingBelt";
    private const int CircleSegments = 64;
    private const float Clearance = 0.035f;
    private const float BeltWidth = 0.18f;
    private const float BeltThickness = 0.055f;
    private const string Description =
        "A correia dentada liga a engrenagem do virabrequim às engrenagens " +
        "das duas árvores de cames. É ela que mantém a abertura das válvulas " +
        "sincronizada com a posição dos pistões. Como a árvore de cames " +
        "precisa girar à metade da rotação do virabrequim, suas engrenagens " +
        "têm o dobro de dentes. O rompimento da correia é uma das falhas " +
        "mais graves de um motor: as válvulas param abertas enquanto os " +
        "pistões continuam subindo.";

    [MenuItem("MTRM/Correia/Gerar correia dentada")]
    public static void Generate()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Dictionary<EngineRepresentation, Gear[]> groups =
                root.GetComponentsInChildren<ComponentTag>(true)
                    .Where(tag =>
                        tag.Id == EngineComponentId.EngrenagemVirabrequim ||
                        tag.Id == EngineComponentId.EngrenagemDeComando)
                    .Select(tag => new Gear(tag))
                    .GroupBy(gear => gear.Tag.Representation)
                    .ToDictionary(group => group.Key, group => group.ToArray());
            if (groups.Count != 2 || groups.Any(pair => pair.Value.Length != 3))
                throw new InvalidOperationException(
                    "A geração exige três engrenagens em cada representação.");

            EnsureAssetFolder();
            Mesh runningMesh = GetOrCreateMesh("TimingBelt_LigaDesliga", true);
            Mesh expandedMesh = GetOrCreateMesh("TimingBelt_Expande", false);
            Material material = GetOrCreateMaterial();

            foreach (KeyValuePair<EngineRepresentation, Gear[]> group in groups)
            {
                Transform representationRoot =
                    FindRepresentationRoot(root.transform, group.Value[0].Tag.transform);
                Transform existing = representationRoot.Find(ObjectName);
                Mesh mesh = group.Key == EngineRepresentation.Expande
                    ? expandedMesh : runningMesh;
                BuildMesh(mesh, group.Value, representationRoot);
                GameObject belt;
                if (existing == null)
                {
                    belt = new GameObject(ObjectName);
                    belt.transform.SetParent(representationRoot, false);
                }
                else
                {
                    belt = existing.gameObject;
                    belt.transform.SetLocalPositionAndRotation(
                        Vector3.zero, Quaternion.identity);
                    belt.transform.localScale = Vector3.one;
                }

                MeshFilter filter = GetOrAdd<MeshFilter>(belt);
                filter.sharedMesh = mesh;
                MeshRenderer renderer = GetOrAdd<MeshRenderer>(belt);
                renderer.sharedMaterial = material;
                MeshCollider collider = GetOrAdd<MeshCollider>(belt);
                collider.sharedMesh = mesh;
                ComponentTag tag = GetOrAdd<ComponentTag>(belt);
                SetTag(tag, group.Key);

                // Cada representação recebe sua própria geometria procedural,
                // derivada das engrenagens daquela hierarquia. Assim, a
                // correia permanece observável tanto montada quanto expandida
                // sem depender de curva adicional no clipe de animação.
                belt.SetActive(true);
            }

            EditorUtility.SetDirty(runningMesh);
            EditorUtility.SetDirty(expandedMesh);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("MTRM_TIMING_BELT_GENERATED");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void GenerateFromCommandLine()
    {
        try { Generate(); EditorApplication.Exit(0); }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void BuildMesh(
        Mesh mesh, IReadOnlyList<Gear> gears, Transform parent)
    {
        Vector3 axis = AlignedAverageAxis(gears);
        Vector3 helper = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.9f
            ? Vector3.up : Vector3.right;
        Vector3 u = Vector3.Cross(axis, helper).normalized;
        Vector3 v = Vector3.Cross(axis, u).normalized;
        Vector3 origin = gears[0].Center;
        List<PathPoint> samples = new List<PathPoint>();
        for (int gearIndex = 0; gearIndex < gears.Count; gearIndex++)
        {
            Gear gear = gears[gearIndex];
            Vector3 relative = gear.Center - origin;
            float cx = Vector3.Dot(relative, u);
            float cy = Vector3.Dot(relative, v);
            float axial = Vector3.Dot(relative, axis);
            float radius = gear.Radius + Clearance;
            for (int index = 0; index < CircleSegments; index++)
            {
                float angle = 2f * Mathf.PI * index / CircleSegments;
                samples.Add(new PathPoint(
                    new Vector2(
                        cx + Mathf.Cos(angle) * radius,
                        cy + Mathf.Sin(angle) * radius),
                    axial,
                    gearIndex));
            }
        }

        List<PathPoint> hull = ConvexHull(samples);
        List<PathPoint> path = Densify(hull);
        List<Vector3> vertices = new List<Vector3>(path.Count * 4);
        List<int> triangles = new List<int>(path.Count * 24);
        for (int index = 0; index < path.Count; index++)
        {
            PathPoint previous = path[(index - 1 + path.Count) % path.Count];
            PathPoint next = path[(index + 1) % path.Count];
            Vector2 tangent2 = (next.Point - previous.Point).normalized;
            Vector2 outward2 = new Vector2(tangent2.y, -tangent2.x);
            Vector3 outward = u * outward2.x + v * outward2.y;
            Vector3 centerWorld = origin + u * path[index].Point.x +
                                  v * path[index].Point.y +
                                  axis * path[index].Axial;
            Vector3[] corners =
            {
                centerWorld - axis * (BeltWidth * 0.5f) -
                    outward * (BeltThickness * 0.5f),
                centerWorld + axis * (BeltWidth * 0.5f) -
                    outward * (BeltThickness * 0.5f),
                centerWorld + axis * (BeltWidth * 0.5f) +
                    outward * (BeltThickness * 0.5f),
                centerWorld - axis * (BeltWidth * 0.5f) +
                    outward * (BeltThickness * 0.5f)
            };
            vertices.AddRange(corners.Select(parent.InverseTransformPoint));
        }

        for (int index = 0; index < path.Count; index++)
        {
            int next = (index + 1) % path.Count;
            for (int side = 0; side < 4; side++)
            {
                int sideNext = (side + 1) % 4;
                AddQuad(triangles,
                    index * 4 + side,
                    next * 4 + side,
                    next * 4 + sideNext,
                    index * 4 + sideNext);
            }
        }
        mesh.Clear();
        mesh.name = mesh.name;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private static List<PathPoint> ConvexHull(List<PathPoint> points)
    {
        List<PathPoint> sorted = points
            .OrderBy(point => point.Point.x)
            .ThenBy(point => point.Point.y)
            .ToList();
        List<PathPoint> hull = new List<PathPoint>();
        foreach (PathPoint point in sorted)
        {
            while (hull.Count >= 2 && Cross(
                       hull[hull.Count - 1].Point - hull[hull.Count - 2].Point,
                       point.Point - hull[hull.Count - 1].Point) <= 0f)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(point);
        }
        int lowerCount = hull.Count;
        for (int index = sorted.Count - 2; index >= 0; index--)
        {
            PathPoint point = sorted[index];
            while (hull.Count > lowerCount && Cross(
                       hull[hull.Count - 1].Point - hull[hull.Count - 2].Point,
                       point.Point - hull[hull.Count - 1].Point) <= 0f)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(point);
        }
        hull.RemoveAt(hull.Count - 1);
        return hull;
    }

    private static List<PathPoint> Densify(IReadOnlyList<PathPoint> hull)
    {
        List<PathPoint> result = new List<PathPoint>();
        for (int index = 0; index < hull.Count; index++)
        {
            PathPoint first = hull[index];
            PathPoint second = hull[(index + 1) % hull.Count];
            float distance = Vector2.Distance(first.Point, second.Point);
            int segments = first.GearIndex != second.GearIndex
                ? Mathf.Max(2, Mathf.CeilToInt(distance / 0.2f))
                : 1;
            for (int step = 0; step < segments; step++)
            {
                float t = step / (float)segments;
                result.Add(new PathPoint(
                    Vector2.Lerp(first.Point, second.Point, t),
                    Mathf.Lerp(first.Axial, second.Axial, t),
                    step == 0 ? first.GearIndex : -1));
            }
        }
        return result;
    }

    private static float Cross(Vector2 first, Vector2 second) =>
        first.x * second.y - first.y * second.x;

    private static void AddQuad(
        ICollection<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static Vector3 AlignedAverageAxis(IEnumerable<Gear> gears)
    {
        Gear first = gears.First();
        Vector3 sum = Vector3.zero;
        foreach (Gear gear in gears)
            sum += Vector3.Dot(gear.Axis, first.Axis) < 0f
                ? -gear.Axis : gear.Axis;
        return sum.normalized;
    }

    private static Transform FindRepresentationRoot(
        Transform prefabRoot, Transform descendant)
    {
        Transform current = descendant;
        while (current.parent != prefabRoot)
            current = current.parent;
        return current;
    }

    private static void SetTag(
        ComponentTag tag, EngineRepresentation representation)
    {
        SerializedObject serialized = new SerializedObject(tag);
        serialized.FindProperty("id").intValue =
            (int)EngineComponentId.CorreiaDentada;
        serialized.FindProperty("instanceIndex").intValue = 0;
        serialized.FindProperty("subIndex").intValue = 0;
        serialized.FindProperty("representation").intValue =
            (int)representation;
        serialized.FindProperty("displayName").stringValue =
            "Correia Dentada";
        serialized.FindProperty("educationalDescription").stringValue =
            Description;
        serialized.FindProperty("intentionallyUnidentified").boolValue = false;
        serialized.FindProperty("justification").stringValue = string.Empty;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Mesh GetOrCreateMesh(string name, bool primary)
    {
        Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(AssetPath)
            .OfType<Mesh>().FirstOrDefault(item => item.name == name);
        if (mesh != null)
            return mesh;
        mesh = new Mesh { name = name };
        if (primary && !System.IO.File.Exists(AssetPath))
            AssetDatabase.CreateAsset(mesh, AssetPath);
        else
            AssetDatabase.AddObjectToAsset(mesh, AssetPath);
        return mesh;
    }

    private static Material GetOrCreateMaterial()
    {
        Material material = AssetDatabase.LoadAllAssetsAtPath(AssetPath)
            .OfType<Material>().FirstOrDefault();
        if (material != null)
            return material;
        material = new Material(Shader.Find("Standard"))
        {
            name = "TimingBelt_Dark",
            color = new Color(0.055f, 0.06f, 0.065f, 1f)
        };
        material.SetFloat("_Metallic", 0.05f);
        material.SetFloat("_Glossiness", 0.2f);
        AssetDatabase.AddObjectToAsset(material, AssetPath);
        return material;
    }

    private static void EnsureAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/MTRM/Generated"))
            AssetDatabase.CreateFolder("Assets/MTRM", "Generated");
    }

    private readonly struct PathPoint
    {
        public PathPoint(Vector2 point, float axial, int gearIndex)
        {
            Point = point;
            Axial = axial;
            GearIndex = gearIndex;
        }
        public Vector2 Point { get; }
        public float Axial { get; }
        public int GearIndex { get; }
    }

    private sealed class Gear
    {
        public Gear(ComponentTag tag)
        {
            Tag = tag;
            Renderer renderer = tag.GetComponent<Renderer>();
            MeshFilter filter = tag.GetComponent<MeshFilter>();
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
        }
        public ComponentTag Tag { get; }
        public Vector3 Center { get; }
        public Vector3 Axis { get; }
        public float Radius { get; }
    }
}
