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

public static class ComponentReviewRenderer
{
    private const string ScenePath = "Assets/MTRM/Scenes/DesktopScene.unity";
    private const string OutputDirectory = "../../docs/component_review/cylinder_candidates";
    private const string ValvePositionsPath = "../../docs/valve_mesh_positions.csv";
    private const string ValveSemanticsPath = "../../docs/valve_semantic_audit.csv";
    private const string CylinderAuditPath = "../../docs/cylinder_geometry_audit.csv";
    private const string ValveKinematicsPath =
        "../../docs/valve_base_kinematics.csv";

    [MenuItem("MTRM/Renderizar candidatos sem identidade")]
    public static void Render()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject root = scene.GetRootGameObjects()
            .First(item => item.name == "StateEngineManager");
        List<MeshRenderer> cylinderCandidates = root
            .GetComponentsInChildren<MeshRenderer>(true)
            .Where(renderer =>
                renderer.name == "Cylinder" &&
                renderer.GetComponent<ToolTipTrigger>() == null &&
                GetRepresentation(renderer.transform, root.transform).Length > 0)
            .OrderBy(renderer => GetPath(renderer.transform, root.transform))
            .ToList();

        if (cylinderCandidates.Count != 8)
            throw new InvalidOperationException(
                $"Expected 8 untitled Cylinder meshes, found {cylinderCandidates.Count}.");

        string absoluteOutputDirectory = Path.GetFullPath(
            Path.Combine(Application.dataPath, OutputDirectory));
        Directory.CreateDirectory(absoluteOutputDirectory);

        foreach (MeshRenderer sourceRenderer in cylinderCandidates)
        {
            string representation = GetRepresentation(
                sourceRenderer.transform,
                root.transform);
            string cylinder = sourceRenderer.transform.parent.name;
            string filename = $"{representation}_{cylinder}_Cylinder.png";
            RenderMesh(sourceRenderer, Path.Combine(absoluteOutputDirectory, filename));
        }

        WriteValvePositions(root.transform);
        WriteValveSemantics(root.transform);
        WriteCylinderAudit(root.transform, cylinderCandidates);
        WriteValveBaseKinematics(root.transform);
        Debug.Log(
            $"MTRM component review: rendered={cylinderCandidates.Count}, " +
            $"output={absoluteOutputDirectory}");
    }

    public static void RenderFromCommandLine()
    {
        try
        {
            Render();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void RenderMesh(MeshRenderer source, string outputPath)
    {
        MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
        var preview = new GameObject("Component Preview");
        MeshFilter previewFilter = preview.AddComponent<MeshFilter>();
        MeshRenderer previewRenderer = preview.AddComponent<MeshRenderer>();
        previewFilter.sharedMesh = sourceFilter.sharedMesh;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                        Shader.Find("Unlit/Color") ??
                        Shader.Find("Standard");
        var previewMaterial = new Material(shader)
        {
            color = new Color(0.2f, 0.65f, 0.95f)
        };
        previewRenderer.sharedMaterial = previewMaterial;

        var cameraObject = new GameObject("Preview Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.16f, 0.17f, 0.19f);
        camera.orthographic = true;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 1000f;

        var lightObject = new GameObject("Preview Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        lightObject.transform.rotation = Quaternion.Euler(35f, -35f, 0f);

        Bounds bounds = previewRenderer.bounds;
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        if (radius < 0.001f)
            radius = 1f;
        camera.orthographicSize = radius * 1.35f;
        Vector3 direction = new Vector3(1f, 0.8f, -1f).normalized;
        camera.transform.position = bounds.center + direction * radius * 5f;
        camera.transform.LookAt(bounds.center);

        var renderTexture = new RenderTexture(1024, 1024, 24);
        var image = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
            image.Apply();
            File.WriteAllBytes(outputPath, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(lightObject);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(previewMaterial);
            UnityEngine.Object.DestroyImmediate(preview);
        }
    }

    private static void WriteValvePositions(Transform root)
    {
        var csv = new StringBuilder();
        csv.AppendLine(
            "representation,mesh_name,hierarchy_path,local_x,local_y,local_z," +
            "world_x,world_y,world_z");

        foreach (MeshRenderer renderer in root
                     .GetComponentsInChildren<MeshRenderer>(true)
                     .Where(item => item.name.StartsWith(
                         "cabecaValvula",
                         StringComparison.Ordinal))
                     .OrderBy(item => GetPath(item.transform, root)))
        {
            Transform transform = renderer.transform;
            csv.Append(Csv(GetRepresentation(transform, root)));
            csv.Append(',');
            csv.Append(Csv(transform.name));
            csv.Append(',');
            csv.Append(Csv(GetPath(transform, root)));
            AppendVector(csv, transform.localPosition);
            AppendVector(csv, transform.position);
            csv.AppendLine();
        }

        string outputPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, ValvePositionsPath));
        File.WriteAllText(outputPath, csv.ToString(), new UTF8Encoding(false));
    }

    private static void WriteValveSemantics(Transform root)
    {
        var csv = new StringBuilder();
        csv.AppendLine(
            "representation,cylinder_index,valve_type,sub_index," +
            "valve_object_path,head_mesh_path,spring_mesh_path,base_mesh_path");

        var valves = root.GetComponentsInChildren<Valvula>(true)
            .Select(component =>
            {
                var serialized = new SerializedObject(component);
                return new
                {
                    Component = component,
                    Cylinder = serialized.FindProperty("cicloValvula").intValue,
                    Type = serialized.FindProperty("tipoValvula").intValue,
                    Head = serialized.FindProperty("hasteValvula").objectReferenceValue as GameObject,
                    Spring = serialized.FindProperty("molaValvula").objectReferenceValue as GameObject,
                    Base = serialized.FindProperty("baseValvula").objectReferenceValue as GameObject
                };
            })
            .OrderBy(item => GetRepresentation(item.Component.transform, root))
            .ThenBy(item => item.Cylinder)
            .ThenBy(item => item.Type)
            .ThenBy(item => item.Head != null ? item.Head.transform.localPosition.z : 0f)
            .ToList();

        foreach (var representationGroup in valves.GroupBy(
                     item => GetRepresentation(item.Component.transform, root)))
        {
            foreach (var cylinderGroup in representationGroup.GroupBy(
                         item => new { item.Cylinder, item.Type }))
            {
                int subIndex = 0;
                foreach (var item in cylinderGroup)
                {
                    subIndex++;
                    csv.Append(Csv(representationGroup.Key));
                    csv.Append(',');
                    csv.Append(item.Cylinder);
                    csv.Append(',');
                    csv.Append(Csv(item.Type == 0 ? "Admissao" : "Escape"));
                    csv.Append(',');
                    csv.Append(subIndex);
                    csv.Append(',');
                    csv.Append(Csv(GetPath(item.Component.transform, root)));
                    csv.Append(',');
                    csv.Append(Csv(GetPath(item.Head.transform, root)));
                    csv.Append(',');
                    csv.Append(Csv(GetPath(item.Spring.transform, root)));
                    csv.Append(',');
                    csv.Append(Csv(GetPath(item.Base.transform, root)));
                    csv.AppendLine();
                }
            }
        }

        string outputPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, ValveSemanticsPath));
        File.WriteAllText(outputPath, csv.ToString(), new UTF8Encoding(false));
    }

    private static void WriteCylinderAudit(
        Transform root,
        IReadOnlyList<MeshRenderer> cylinderCandidates)
    {
        var csv = new StringBuilder();
        csv.AppendLine(
            "representation,candidate_path,axis_angle_to_first_deg," +
            "spacing_from_previous,mean_spacing,max_spacing_deviation," +
            "axes_mutually_parallel,equally_spaced,material_name,shader," +
            "render_queue,material_alpha,is_opaque,center_inside_block," +
            "diagnostic_result,diagnostic_note");

        foreach (IGrouping<string, MeshRenderer> representationGroup in
                 cylinderCandidates.GroupBy(candidate =>
                     GetRepresentation(candidate.transform, root)))
        {
            List<MeshRenderer> candidates = representationGroup.ToList();
            Vector3 referenceAxis = GetMeshPrincipalAxis(candidates[0]);
            Vector3 blockAxis = GetFarthestCenterDirection(candidates);
            List<MeshRenderer> ordered = candidates
                .OrderBy(candidate =>
                    Vector3.Dot(candidate.bounds.center, blockAxis))
                .ToList();
            var spacings = new List<float>();
            for (int index = 1; index < ordered.Count; index++)
            {
                spacings.Add(Vector3.Distance(
                    ordered[index - 1].bounds.center,
                    ordered[index].bounds.center));
            }

            float meanSpacing = spacings.Average();
            float maxDeviation = spacings.Max(value => Mathf.Abs(value - meanSpacing));
            bool axesParallel = candidates.All(candidate =>
                Vector3.Angle(referenceAxis, GetMeshPrincipalAxis(candidate)) < 0.1f ||
                Vector3.Angle(referenceAxis, -GetMeshPrincipalAxis(candidate)) < 0.1f);
            bool equallySpaced = maxDeviation <= meanSpacing * 0.01f;

            MeshRenderer block = root.GetComponentsInChildren<MeshRenderer>(true)
                .FirstOrDefault(renderer =>
                    GetRepresentation(renderer.transform, root) ==
                    representationGroup.Key &&
                    renderer.name == "Bloco");

            for (int index = 0; index < ordered.Count; index++)
            {
                MeshRenderer candidate = ordered[index];
                Vector3 axis = GetMeshPrincipalAxis(candidate);
                float angle = Mathf.Min(
                    Vector3.Angle(referenceAxis, axis),
                    Vector3.Angle(referenceAxis, -axis));
                Material material = candidate.sharedMaterial;
                float alpha = GetMaterialAlpha(material);
                int renderQueue = material != null ? material.renderQueue : -1;
                bool isOpaque =
                    material != null && alpha >= 0.95f && renderQueue < 3000;
                bool centerInsideBlock =
                    block != null && block.bounds.Contains(candidate.bounds.center);

                csv.Append(Csv(representationGroup.Key));
                csv.Append(',');
                csv.Append(Csv(GetPath(candidate.transform, root)));
                csv.Append(',');
                csv.Append(Format(angle));
                csv.Append(',');
                csv.Append(index == 0 ? string.Empty : Format(spacings[index - 1]));
                csv.Append(',');
                csv.Append(Format(meanSpacing));
                csv.Append(',');
                csv.Append(Format(maxDeviation));
                csv.Append(',');
                csv.Append(axesParallel ? "true" : "false");
                csv.Append(',');
                csv.Append(equallySpaced ? "true" : "false");
                csv.Append(',');
                csv.Append(Csv(material != null ? material.name : string.Empty));
                csv.Append(',');
                csv.Append(Csv(material != null ? material.shader.name : string.Empty));
                csv.Append(',');
                csv.Append(renderQueue);
                csv.Append(',');
                csv.Append(Format(alpha));
                csv.Append(',');
                csv.Append(isOpaque ? "true" : "false");
                csv.Append(',');
                csv.Append(centerInsideBlock ? "true" : "false");
                csv.Append(',');
                csv.Append(Csv(
                    axesParallel && equallySpaced
                        ? "geometria_regular"
                        : "inconclusivo"));
                csv.Append(',');
                csv.Append(Csv(
                    "O teste anterior malha-pistão foi inconclusivo por provável " +
                    "limitação de instrumentação; não caracteriza o modelo."));
                csv.AppendLine();
            }
        }

        string outputPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, CylinderAuditPath));
        File.WriteAllText(outputPath, csv.ToString(), new UTF8Encoding(false));
    }

    private static void WriteValveBaseKinematics(Transform root)
    {
        var clips = new[]
        {
            new
            {
                Label = "Anim_engine_explosion",
                Path = "Assets/MTRM/Art/Animations/Engine/Anim_engine_explosion.anim"
            },
            new
            {
                Label = "EngineStimulus_expand",
                Path =
                    "Assets/MTRM/Animations/Engine/EngineStimulus_expand.anim"
            }
        };
        var csv = new StringBuilder();
        csv.AppendLine(
            "clip,representation,cylinder_index,valve_type,sub_index," +
            "head_total_displacement,spring_total_displacement," +
            "base_total_displacement,base_head_correlation," +
            "base_relative_to_spring_lower_upper,interpretation");

        foreach (var clipDefinition in clips)
        {
            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(clipDefinition.Path);
            if (clip == null)
                throw new InvalidOperationException(
                    $"Animation clip not found: {clipDefinition.Path}");

            foreach (Transform representationRoot in new[]
                     {
                         root.Find("4L.blend-8vExpande"),
                         root.Find("4L.blend-8vLigaDesliga")
                     })
            {
                List<ValveSampleSet> sets =
                    CreateValveSampleSets(representationRoot, root);
                AnimationMode.StartAnimationMode();
                try
                {
                    const int sampleCount = 61;
                    for (int sample = 0; sample < sampleCount; sample++)
                    {
                        float time = clip.length * sample / (sampleCount - 1f);
                        AnimationMode.SampleAnimationClip(
                            representationRoot.gameObject,
                            clip,
                            time);
                        foreach (ValveSampleSet set in sets)
                            set.Capture();
                    }
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                }

                foreach (ValveSampleSet set in sets)
                {
                    float headDisplacement = TotalDisplacement(set.HeadPositions);
                    float springDisplacement =
                        TotalDisplacement(set.SpringPositions);
                    float baseDisplacement = TotalDisplacement(set.BasePositions);
                    float correlation = PositionCorrelation(
                        set.BasePositions,
                        set.HeadPositions);
                    float relativePosition = set.BaseRelativeToSpring.Average();
                    string interpretation;
                    if (headDisplacement < 0.000001f &&
                        springDisplacement < 0.000001f &&
                        baseDisplacement < 0.000001f)
                    {
                        interpretation =
                            "sem_binding_compativel_ou_sem_movimento_no_clip";
                    }
                    else if (baseDisplacement > 0.000001f &&
                             correlation >= 0.95f)
                    {
                        interpretation = "movel_correlacionado_candidato_PratoMola";
                    }
                    else if (baseDisplacement <= 0.000001f &&
                             relativePosition <= 0.25f)
                    {
                        interpretation = "fixo_extremidade_inferior_candidato_AssentoMola";
                    }
                    else
                    {
                        interpretation =
                            "fixo_ou_inconclusivo_candidato_SedeValvula";
                    }

                    csv.Append(Csv(clipDefinition.Label));
                    csv.Append(',');
                    csv.Append(Csv(set.Representation));
                    csv.Append(',');
                    csv.Append(set.CylinderIndex);
                    csv.Append(',');
                    csv.Append(Csv(set.ValveType));
                    csv.Append(',');
                    csv.Append(set.SubIndex);
                    csv.Append(',');
                    csv.Append(Format(headDisplacement));
                    csv.Append(',');
                    csv.Append(Format(springDisplacement));
                    csv.Append(',');
                    csv.Append(Format(baseDisplacement));
                    csv.Append(',');
                    csv.Append(Format(correlation));
                    csv.Append(',');
                    csv.Append(Format(relativePosition));
                    csv.Append(',');
                    csv.Append(Csv(interpretation));
                    csv.AppendLine();
                }
            }
        }

        string outputPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, ValveKinematicsPath));
        File.WriteAllText(outputPath, csv.ToString(), new UTF8Encoding(false));
    }

    private static List<ValveSampleSet> CreateValveSampleSets(
        Transform representationRoot,
        Transform stimulusRoot)
    {
        var valves = representationRoot.GetComponentsInChildren<Valvula>(true)
            .Select(component =>
            {
                var serialized = new SerializedObject(component);
                return new
                {
                    Component = component,
                    Cylinder = serialized.FindProperty("cicloValvula").intValue,
                    Type = serialized.FindProperty("tipoValvula").intValue,
                    Head = serialized.FindProperty("hasteValvula")
                        .objectReferenceValue as GameObject,
                    Spring = serialized.FindProperty("molaValvula")
                        .objectReferenceValue as GameObject,
                    Base = serialized.FindProperty("baseValvula")
                        .objectReferenceValue as GameObject
                };
            })
            .OrderBy(item => item.Cylinder)
            .ThenBy(item => item.Type)
            .ThenBy(item => item.Head.transform.localPosition.z)
            .ToList();

        var result = new List<ValveSampleSet>();
        foreach (var group in valves.GroupBy(item => new
                 {
                     item.Cylinder,
                     item.Type
                 }))
        {
            int subIndex = 0;
            foreach (var item in group)
            {
                subIndex++;
                result.Add(new ValveSampleSet(
                    GetRepresentation(item.Component.transform, stimulusRoot),
                    item.Cylinder,
                    item.Type == 0 ? "Admissao" : "Escape",
                    subIndex,
                    item.Head.transform,
                    item.Spring.transform,
                    item.Base.transform));
            }
        }
        return result;
    }

    private static float TotalDisplacement(IReadOnlyList<Vector3> positions)
    {
        float maximum = 0f;
        for (int first = 0; first < positions.Count; first++)
        {
            for (int second = first + 1; second < positions.Count; second++)
                maximum = Mathf.Max(
                    maximum,
                    Vector3.Distance(positions[first], positions[second]));
        }
        return maximum;
    }

    private static float PositionCorrelation(
        IReadOnlyList<Vector3> first,
        IReadOnlyList<Vector3> second)
    {
        Vector3 direction = FindMotionDirection(second);
        float[] firstValues = first.Select(value => Vector3.Dot(value, direction)).ToArray();
        float[] secondValues = second.Select(value => Vector3.Dot(value, direction)).ToArray();
        float firstMean = firstValues.Average();
        float secondMean = secondValues.Average();
        float numerator = 0f;
        float firstVariance = 0f;
        float secondVariance = 0f;
        for (int index = 0; index < firstValues.Length; index++)
        {
            float firstDelta = firstValues[index] - firstMean;
            float secondDelta = secondValues[index] - secondMean;
            numerator += firstDelta * secondDelta;
            firstVariance += firstDelta * firstDelta;
            secondVariance += secondDelta * secondDelta;
        }
        float denominator = Mathf.Sqrt(firstVariance * secondVariance);
        return denominator > 0.000000001f ? numerator / denominator : 0f;
    }

    private static Vector3 FindMotionDirection(IReadOnlyList<Vector3> positions)
    {
        float maximum = -1f;
        Vector3 direction = Vector3.forward;
        for (int first = 0; first < positions.Count; first++)
        {
            for (int second = first + 1; second < positions.Count; second++)
            {
                Vector3 delta = positions[second] - positions[first];
                if (delta.sqrMagnitude > maximum)
                {
                    maximum = delta.sqrMagnitude;
                    direction = delta.sqrMagnitude > 0f
                        ? delta.normalized
                        : Vector3.forward;
                }
            }
        }
        return direction;
    }

    private static Vector3 GetMeshPrincipalAxis(MeshRenderer renderer)
    {
        Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
        Vector3 size = mesh.bounds.size;
        Vector3 localAxis = size.x >= size.y && size.x >= size.z
            ? Vector3.right
            : size.y >= size.z
                ? Vector3.up
                : Vector3.forward;
        return renderer.transform.TransformDirection(localAxis).normalized;
    }

    private static Vector3 GetFarthestCenterDirection(
        IReadOnlyList<MeshRenderer> renderers)
    {
        float greatestDistance = -1f;
        Vector3 direction = Vector3.forward;
        for (int first = 0; first < renderers.Count; first++)
        {
            for (int second = first + 1; second < renderers.Count; second++)
            {
                Vector3 delta =
                    renderers[second].bounds.center -
                    renderers[first].bounds.center;
                if (delta.sqrMagnitude > greatestDistance)
                {
                    greatestDistance = delta.sqrMagnitude;
                    direction = delta.normalized;
                }
            }
        }
        return direction;
    }

    private static float GetMaterialAlpha(Material material)
    {
        if (material == null)
            return 1f;
        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor").a;
        if (material.HasProperty("_Color"))
            return material.color.a;
        return 1f;
    }

    private static string Format(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static void AppendVector(StringBuilder csv, Vector3 value)
    {
        csv.Append(',');
        csv.Append(value.x.ToString("R", CultureInfo.InvariantCulture));
        csv.Append(',');
        csv.Append(value.y.ToString("R", CultureInfo.InvariantCulture));
        csv.Append(',');
        csv.Append(value.z.ToString("R", CultureInfo.InvariantCulture));
    }

    private static string GetRepresentation(Transform current, Transform root)
    {
        Transform cursor = current;
        while (cursor != null && cursor.parent != root)
            cursor = cursor.parent;
        if (cursor == null || cursor == root)
            return string.Empty;
        if (cursor.name == "4L.blend-8vExpande")
            return "Expande";
        if (cursor.name == "4L.blend-8vLigaDesliga")
            return "LigaDesliga";
        return string.Empty;
    }

    private static string GetPath(Transform current, Transform root)
    {
        var names = new Stack<string>();
        for (Transform cursor = current; cursor != null; cursor = cursor.parent)
        {
            names.Push(cursor.name);
            if (cursor == root)
                break;
        }
        return string.Join("/", names);
    }

    private static string Csv(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }

    private sealed class ValveSampleSet
    {
        private readonly Transform head;
        private readonly Transform spring;
        private readonly Transform valveBase;
        private readonly MeshRenderer springRenderer;

        public ValveSampleSet(
            string representation,
            int cylinderIndex,
            string valveType,
            int subIndex,
            Transform head,
            Transform spring,
            Transform valveBase)
        {
            Representation = representation;
            CylinderIndex = cylinderIndex;
            ValveType = valveType;
            SubIndex = subIndex;
            this.head = head;
            this.spring = spring;
            this.valveBase = valveBase;
            springRenderer = spring.GetComponent<MeshRenderer>();
        }

        public string Representation { get; }
        public int CylinderIndex { get; }
        public string ValveType { get; }
        public int SubIndex { get; }
        public List<Vector3> HeadPositions { get; } = new List<Vector3>();
        public List<Vector3> SpringPositions { get; } = new List<Vector3>();
        public List<Vector3> BasePositions { get; } = new List<Vector3>();
        public List<float> BaseRelativeToSpring { get; } = new List<float>();

        public void Capture()
        {
            HeadPositions.Add(head.position);
            SpringPositions.Add(spring.position);
            BasePositions.Add(valveBase.position);

            Vector3 axis = GetMeshPrincipalAxis(springRenderer);
            Bounds bounds = springRenderer.bounds;
            float centerProjection = Vector3.Dot(bounds.center, axis);
            float projectedExtent =
                Mathf.Abs(axis.x) * bounds.extents.x +
                Mathf.Abs(axis.y) * bounds.extents.y +
                Mathf.Abs(axis.z) * bounds.extents.z;
            float lower = centerProjection - projectedExtent;
            float upper = centerProjection + projectedExtent;
            float baseProjection = Vector3.Dot(valveBase.position, axis);
            BaseRelativeToSpring.Add(
                Mathf.InverseLerp(lower, upper, baseProjection));
        }
    }
}
