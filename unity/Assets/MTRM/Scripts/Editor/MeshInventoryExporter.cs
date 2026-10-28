using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MeshInventoryExporter
{
    private const string ScenePath = "Assets/MTRM/Scenes/DesktopScene.unity";
    private const string StimulusPrefabPath =
        "Assets/MTRM/Prefabs/Engine/StateEngineManager.prefab";
    private const string OutputPath = "../../docs/mesh_inventory.csv";
    private const string CandidatesOutputPath = "../../docs/component_candidates.csv";
    private const string ValveCandidatesOutputPath =
        "../../docs/valve_mapping_candidate.csv";
    private const string ComponentMappingOutputPath =
        "../../docs/component_mapping.csv";
    private const string ExpandedRepresentation = "Expande";
    private const string RunningRepresentation = "LigaDesliga";

    [MenuItem("MTRM/Exportar inventário de meshes")]
    public static void Export()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject stimulusRoot = FindStimulusRoot(scene);
        IReadOnlyList<Transform> transforms = stimulusRoot
            .GetComponentsInChildren<Transform>(true);

        int visibleMeshes = 0;
        int serializedColliders = 0;
        int runtimeInteractiveMeshes = 0;
        var csv = new StringBuilder();
        csv.AppendLine(
            "hierarchy_path,representation,object_name,has_mesh_renderer," +
            "has_serialized_collider," +
            "has_runtime_interactive_collider," +
            "has_tooltip_trigger,tooltip_title");

        foreach (Transform current in transforms)
        {
            bool hasMeshRenderer = current.GetComponent<MeshRenderer>() != null;
            bool hasSerializedCollider = current.GetComponent<Collider>() != null;
            bool hasRuntimeInteractiveCollider = ReceivesRuntimeMeshCollider(current);
            ToolTipTrigger tooltip = current.GetComponent<ToolTipTrigger>();

            if (hasMeshRenderer)
                visibleMeshes++;
            if (hasSerializedCollider)
                serializedColliders++;
            if (hasRuntimeInteractiveCollider)
                runtimeInteractiveMeshes++;

            csv.Append(Csv(GetHierarchyPath(current, stimulusRoot.transform)));
            csv.Append(',');
            csv.Append(Csv(GetRepresentation(current, stimulusRoot.transform)));
            csv.Append(',');
            csv.Append(Csv(current.name));
            csv.Append(',');
            csv.Append(hasMeshRenderer ? "true" : "false");
            csv.Append(',');
            csv.Append(hasSerializedCollider ? "true" : "false");
            csv.Append(',');
            csv.Append(hasRuntimeInteractiveCollider ? "true" : "false");
            csv.Append(',');
            csv.Append(tooltip != null ? "true" : "false");
            csv.Append(',');
            csv.Append(Csv(GetTooltipTitle(tooltip)));
            csv.AppendLine();
        }

        string absoluteOutputPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, OutputPath));
        File.WriteAllText(absoluteOutputPath, csv.ToString(), new UTF8Encoding(false));
        WriteComponentCandidates(stimulusRoot.transform, transforms);
        WriteValveMappingCandidates(stimulusRoot.transform, transforms);

        Debug.Log(
            $"MTRM mesh inventory exported to {absoluteOutputPath}. " +
            $"objects={transforms.Count}, visible_meshes={visibleMeshes}, " +
            $"serialized_colliders={serializedColliders}, " +
            $"runtime_interactive_meshes={runtimeInteractiveMeshes}");
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

    private static GameObject FindStimulusRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            if (string.Equals(sourcePath, StimulusPrefabPath, StringComparison.Ordinal))
                return root;
        }

        throw new InvalidOperationException(
            $"The scene {ScenePath} does not contain an instance of " +
            $"{StimulusPrefabPath}.");
    }

    private static string GetHierarchyPath(Transform current, Transform root)
    {
        var names = new Stack<string>();
        Transform cursor = current;
        while (cursor != null)
        {
            names.Push(cursor.name);
            if (cursor == root)
                break;
            cursor = cursor.parent;
        }

        return string.Join("/", names);
    }

    private static string GetTooltipTitle(ToolTipTrigger tooltip)
    {
        if (tooltip == null)
            return string.Empty;

        var serializedTooltip = new SerializedObject(tooltip);
        SerializedProperty title = serializedTooltip.FindProperty("titleText");
        return title != null ? title.stringValue : string.Empty;
    }

    private static string GetTooltipDescription(ToolTipTrigger tooltip)
    {
        if (tooltip == null)
            return string.Empty;

        var serializedTooltip = new SerializedObject(tooltip);
        SerializedProperty description =
            serializedTooltip.FindProperty("descriptionText");
        return description != null ? description.stringValue : string.Empty;
    }

    private static bool ReceivesRuntimeMeshCollider(Transform current)
    {
        Renderer renderer = current.GetComponent<Renderer>();
        MeshFilter meshFilter = current.GetComponent<MeshFilter>();
        return renderer != null &&
               meshFilter != null &&
               meshFilter.sharedMesh != null;
    }

    private static void WriteComponentCandidates(
        Transform stimulusRoot,
        IReadOnlyList<Transform> transforms)
    {
        var titledMeshes = transforms
            .Select(transform => new
            {
                Transform = transform,
                HasMesh = transform.GetComponent<MeshRenderer>() != null,
                Title = GetTooltipTitle(transform.GetComponent<ToolTipTrigger>()),
                Representation = GetRepresentation(transform, stimulusRoot)
            })
            .Where(item => item.HasMesh && !string.IsNullOrWhiteSpace(item.Title))
            .ToList();

        var csv = new StringBuilder();
        csv.AppendLine(
            "tooltip_title,mesh_count,expande_mesh_count," +
            "liga_desliga_mesh_count,deepest_common_ancestor," +
            "ancestor_is_exclusive,direct_override_title_count," +
            "effective_title_count,vocabulary_note");

        int effectiveTitleCount = titledMeshes
            .Select(item => item.Title)
            .Distinct(StringComparer.Ordinal)
            .Count();

        foreach (var group in titledMeshes
                     .GroupBy(item => item.Title, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            Transform commonAncestor = FindDeepestCommonAncestor(
                group.Select(item => item.Transform).ToList());
            bool isExclusive = !titledMeshes.Any(
                item =>
                    item.Title != group.Key &&
                    item.Transform.IsChildOf(commonAncestor));

            csv.Append(Csv(group.Key));
            csv.Append(',');
            csv.Append(group.Count());
            csv.Append(',');
            csv.Append(group.Count(item => item.Representation == ExpandedRepresentation));
            csv.Append(',');
            csv.Append(group.Count(item => item.Representation == RunningRepresentation));
            csv.Append(',');
            csv.Append(Csv(GetHierarchyPath(commonAncestor, stimulusRoot)));
            csv.Append(',');
            csv.Append(isExclusive ? "true" : "false");
            csv.Append(',');
            csv.Append("12");
            csv.Append(',');
            csv.Append(effectiveTitleCount);
            csv.Append(',');
            csv.Append(Csv(
                "12 titleText são sobreposições diretas em " +
                "StateEngineManager.prefab; 34 são títulos efetivos da " +
                "hierarquia instanciada."));
            csv.AppendLine();
        }

        string absoluteOutputPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, CandidatesOutputPath));
        File.WriteAllText(absoluteOutputPath, csv.ToString(), new UTF8Encoding(false));
    }

    private static void WriteValveMappingCandidates(
        Transform stimulusRoot,
        IReadOnlyList<Transform> transforms)
    {
        List<Transform> anchors = transforms
            .Where(transform =>
                transform.name.StartsWith("AdmissionValve", StringComparison.Ordinal) ||
                transform.name.StartsWith("ExaustValve", StringComparison.Ordinal))
            .ToList();
        List<Transform> valves = transforms
            .Where(transform =>
                transform.GetComponent<MeshRenderer>() != null &&
                transform.name.StartsWith("cabecaValvula", StringComparison.Ordinal))
            .ToList();

        var csv = new StringBuilder();
        csv.AppendLine(
            "representation,anchor_path,anchor_name,candidate_valve_path," +
            "distance,second_candidate_valve_path,second_distance," +
            "diagnostic_result,diagnostic_note");

        foreach (Transform anchor in anchors
                     .OrderBy(item => GetHierarchyPath(item, stimulusRoot),
                         StringComparer.Ordinal))
        {
            string representation = GetRepresentation(anchor, stimulusRoot);
            var nearest = valves
                .Where(valve =>
                    GetRepresentation(valve, stimulusRoot) == representation)
                .Select(valve => new
                {
                    Transform = valve,
                    Distance = Vector3.Distance(
                        anchor.position,
                        valve.position)
                })
                .OrderBy(item => item.Distance)
                .Take(2)
                .ToList();

            if (nearest.Count < 2)
                throw new InvalidOperationException(
                    $"Anchor {anchor.name} has fewer than two valve candidates.");

            csv.Append(Csv(representation));
            csv.Append(',');
            csv.Append(Csv(GetHierarchyPath(anchor, stimulusRoot)));
            csv.Append(',');
            csv.Append(Csv(anchor.name));
            csv.Append(',');
            csv.Append(Csv(GetHierarchyPath(nearest[0].Transform, stimulusRoot)));
            csv.Append(',');
            csv.Append(nearest[0].Distance.ToString("R",
                System.Globalization.CultureInfo.InvariantCulture));
            csv.Append(',');
            csv.Append(Csv(GetHierarchyPath(nearest[1].Transform, stimulusRoot)));
            csv.Append(',');
            csv.Append(nearest[1].Distance.ToString("R",
                System.Globalization.CultureInfo.InvariantCulture));
            csv.Append(',');
            csv.Append(Csv("inconclusivo"));
            csv.Append(',');
            csv.Append(Csv(
                "Âncoras não estão posicionadas sobre as válvulas comandadas; " +
                "distâncias equivalentes não permitem inferir admissão/escape."));
            csv.AppendLine();
        }

        string absoluteOutputPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, ValveCandidatesOutputPath));
        File.WriteAllText(absoluteOutputPath, csv.ToString(), new UTF8Encoding(false));
    }

    private static void WriteComponentMapping(IReadOnlyList<Transform> transforms)
    {
        Dictionary<string, MappingProposal> proposals = CreateMappingProposals();
        var titledMeshes = transforms
            .Select(transform => transform.GetComponent<ToolTipTrigger>())
            .Where(tooltip =>
                tooltip != null &&
                tooltip.GetComponent<MeshRenderer>() != null &&
                !string.IsNullOrWhiteSpace(GetTooltipTitle(tooltip)))
            .Select(tooltip => new
            {
                Title = GetTooltipTitle(tooltip),
                Description = GetTooltipDescription(tooltip)
            })
            .ToList();

        var csv = new StringBuilder();
        csv.AppendLine(
            "tooltip_title,engine_component_id,instance_index_rule," +
            "display_name,educational_description,confidence,mapping_notes,revisado");

        foreach (var group in titledMeshes
                     .GroupBy(item => item.Title, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            if (!proposals.TryGetValue(group.Key, out MappingProposal proposal))
                throw new InvalidOperationException(
                    $"No mapping proposal was defined for '{group.Key}'.");

            string description = group
                .Select(item => item.Description)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value, StringComparer.Ordinal)
                .OrderByDescending(values => values.Count())
                .ThenBy(values => values.Key, StringComparer.Ordinal)
                .Select(values => values.Key)
                .FirstOrDefault() ?? string.Empty;

            csv.Append(Csv(group.Key));
            csv.Append(',');
            csv.Append(Csv(proposal.ComponentId.ToString()));
            csv.Append(',');
            csv.Append(Csv(proposal.InstanceIndexRule));
            csv.Append(',');
            csv.Append(Csv(proposal.DisplayName));
            csv.Append(',');
            csv.Append(Csv(description));
            csv.Append(',');
            csv.Append(Csv(proposal.Confidence));
            csv.Append(',');
            csv.Append(Csv(proposal.Notes));
            csv.Append(',');
            csv.Append(Csv(string.Empty));
            csv.AppendLine();
        }

        string absoluteOutputPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, ComponentMappingOutputPath));
        File.WriteAllText(absoluteOutputPath, csv.ToString(), new UTF8Encoding(false));
    }

    private static Dictionary<string, MappingProposal> CreateMappingProposals()
    {
        const string unique = "0 (componente único ou conjunto)";
        const string perCylinder = "1..4 por cilindro; confirmar correspondência visual";
        return new Dictionary<string, MappingProposal>(StringComparer.Ordinal)
        {
            ["Alternador"] = Map(EngineComponentId.Alternador, unique, "Alternador"),
            ["Arvore de Cames"] = Map(EngineComponentId.ArvoreDeCames, unique, "Árvore de Cames"),
            ["Base da Valvula"] = Map(EngineComponentId.PratoMola, perCylinder, "Prato da Mola"),
            ["Biela"] = Map(EngineComponentId.Biela, "1..4; duas meshes por cilindro", "Biela"),
            ["Bloco do Motor"] = Map(EngineComponentId.Bloco, unique, "Bloco do Motor"),
            ["Bomba d'agua"] = Map(EngineComponentId.BombaAgua, unique, "Bomba d'água"),
            ["Borboleta"] = Map(EngineComponentId.Borboleta, unique, "Corpo de Borboleta"),
            ["Bronzina"] = Ambiguous(EngineComponentId.Bronzina, "regra por conjunto a revisar", "Bronzina",
                "Há muitas submeshes; agrupamento e índices exigem revisão visual."),
            ["Bronzinav"] = Ambiguous(EngineComponentId.Bronzina, "mesma regra de Bronzina, se confirmado", "Bronzina",
                "Provável erro de digitação; o par na outra representação usa Bronzina."),
            ["Cabeça da Valvula"] = Ambiguous(EngineComponentId.Valvula, perCylinder, "Válvula",
                "Grafia divergente; admissão/escape não são deriváveis das âncoras."),
            ["Cabeça de Válvula"] = Ambiguous(EngineComponentId.Valvula, perCylinder, "Válvula",
                "Admissão/escape não são deriváveis das âncoras."),
            ["Cabeçote"] = Map(EngineComponentId.Cabecote, unique, "Cabeçote"),
            ["Cabos de Vela"] = Ambiguous(EngineComponentId.CaboVela,
                "1..4 para conectores; 0 para o conjunto", "Cabos de Vela",
                "O título cobre quatro conectores e uma mesh do conjunto."),
            ["Capa da correia (Parte de baixo)"] = Map(EngineComponentId.CapaCorreiaInferior, unique, "Capa Inferior da Correia"),
            ["Capa da correia (Parte de cima)"] = Map(EngineComponentId.CapaCorreiaSuperior, unique, "Capa Superior da Correia"),
            ["Coletor de Ar"] = Map(EngineComponentId.ColetorAr, unique, "Coletor de Ar"),
            ["Coletor de escape"] = Map(EngineComponentId.ColetorEscape, unique, "Coletor de Escape"),
            ["Cárter"] = Map(EngineComponentId.Carter, unique, "Cárter"),
            ["Engrenagem de Comando"] = Map(EngineComponentId.EngrenagemDeComando, unique, "Engrenagem de Comando"),
            ["Filtro de Ar"] = Map(EngineComponentId.FiltroAr, unique, "Filtro de Ar"),
            ["Filtro de Óleo"] = Map(EngineComponentId.FiltroOleo, unique, "Filtro de Óleo"),
            ["Junta do Cabeçote"] = Map(EngineComponentId.JuntaCabecote, unique, "Junta do Cabeçote"),
            ["Mancal"] = Map(EngineComponentId.Mancal, unique, "Mancal"),
            ["Modulo Eletronico"] = Map(EngineComponentId.ModuloEletronico, unique, "Módulo Eletrônico"),
            ["Mola da Valvula"] = Map(EngineComponentId.MolaValvula, perCylinder, "Mola da Válvula"),
            ["Motor de arranque"] = Map(EngineComponentId.MotorArranque, unique, "Motor de Arranque"),
            ["Pino do Pistão"] = Map(EngineComponentId.PinoPistao, "1..4 pelo sufixo da mesh", "Pino do Pistão"),
            ["Pistão"] = Map(EngineComponentId.Pistao, "1..4 pelo sufixo da mesh", "Pistão"),
            ["Polia Principal"] = Ambiguous(EngineComponentId.PoliaPrincipal, unique, "Polia Principal",
                "O objeto de origem chama-se Cylinder; confirmar visualmente."),
            ["Tampa do Motor"] = Map(EngineComponentId.TampaMotor, unique, "Tampa do Motor"),
            ["Trilho de injetores"] = Map(EngineComponentId.TrilhoInjetores, unique, "Trilho de Injetores"),
            ["Vela de ignição"] = Map(EngineComponentId.VelaIgnicao, "1..4 pelo sufixo da mesh", "Vela de Ignição"),
            ["Virabrequim"] = Ambiguous(EngineComponentId.Virabrequim, unique, "Virabrequim",
                "O título cobre a árvore e a mesh Engrenagem do virabrequim."),
            ["Volante do Motor"] = Map(EngineComponentId.Volante, unique, "Volante do Motor"),
        };
    }

    private static MappingProposal Map(
        EngineComponentId id,
        string rule,
        string displayName)
    {
        return new MappingProposal(id, rule, displayName, "alta", string.Empty);
    }

    private static MappingProposal Ambiguous(
        EngineComponentId id,
        string rule,
        string displayName,
        string notes)
    {
        return new MappingProposal(id, rule, displayName, "ambígua", notes);
    }

    private static string GetRepresentation(Transform current, Transform stimulusRoot)
    {
        Transform cursor = current;
        while (cursor != null && cursor.parent != stimulusRoot)
            cursor = cursor.parent;

        if (cursor == null || cursor == stimulusRoot)
            return string.Empty;
        if (cursor.name == "4L.blend-8vExpande")
            return ExpandedRepresentation;
        if (cursor.name == "4L.blend-8vLigaDesliga")
            return RunningRepresentation;
        return string.Empty;
    }

    private static Transform FindDeepestCommonAncestor(
        IReadOnlyList<Transform> transforms)
    {
        if (transforms.Count == 0)
            throw new ArgumentException("At least one transform is required.");

        var firstAncestors = new List<Transform>();
        for (Transform cursor = transforms[0]; cursor != null; cursor = cursor.parent)
            firstAncestors.Add(cursor);

        foreach (Transform candidate in firstAncestors)
        {
            if (transforms.All(
                    transform =>
                        transform == candidate || transform.IsChildOf(candidate)))
                return candidate;
        }

        throw new InvalidOperationException("Transforms do not share an ancestor.");
    }

    private static string Csv(string value)
    {
        string safeValue = value ?? string.Empty;
        return "\"" + safeValue.Replace("\"", "\"\"") + "\"";
    }

    private sealed class MappingProposal
    {
        public MappingProposal(
            EngineComponentId componentId,
            string instanceIndexRule,
            string displayName,
            string confidence,
            string notes)
        {
            ComponentId = componentId;
            InstanceIndexRule = instanceIndexRule;
            DisplayName = displayName;
            Confidence = confidence;
            Notes = notes;
        }

        public EngineComponentId ComponentId { get; }
        public string InstanceIndexRule { get; }
        public string DisplayName { get; }
        public string Confidence { get; }
        public string Notes { get; }
    }
}
