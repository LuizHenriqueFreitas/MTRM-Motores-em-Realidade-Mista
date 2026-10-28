using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ComponentTagMigration
{
    private const long IgnitionHarnessMeshId = 3992596592512620447;
    private static readonly IReadOnlyDictionary<long, int>
        ConnectorCylinderByMeshId = new Dictionary<long, int>
        {
            [1453210845648247155] = 1,
            [8594105022408324517] = 2,
            [8580322386318007229] = 3,
            [494593188588993580] = 4
        };
    private const string PrefabPath =
        "Assets/MTRM/Prefabs/Engine/StateEngineManager.prefab";
    private const string MappingPath = "../../docs/component_mapping.csv";
    private const string BindingsPath = "../../docs/component_bindings.csv";
    private const string UnidentifiedJustification =
        "Malha sem tooltip e sem identidade mecânica conclusiva. O teste " +
        "cinemático pistão–volume rejeitou a hipótese de camisa (contenção " +
        "de 34% a 44%, contra 100% exigidos). É opaca, visível, clicável e " +
        "deslocada pela expansão; permanece como alvo não experimental e " +
        "não pontuado.";

    [MenuItem("MTRM/Identidade/Gerar ComponentTag")]
    public static void Generate()
    {
        Dictionary<string, Mapping> mappings = LoadMappings();
        Dictionary<EngineComponentId, TextContent> texts =
            BuildTextCatalog(mappings);
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform[] representations =
            {
                root.transform.Find("4L.blend-8vExpande"),
                root.transform.Find("4L.blend-8vLigaDesliga")
            };
            if (representations.Any(item => item == null))
                throw new InvalidOperationException(
                    "Both engine representations must exist.");

            var bindings = new List<Binding>();
            foreach (Transform representationRoot in representations)
            {
                EngineRepresentation representation =
                    representationRoot.name.Contains("Expande")
                        ? EngineRepresentation.Expande
                        : EngineRepresentation.LigaDesliga;
                Dictionary<GameObject, ValveIdentity> valveIdentities =
                    BuildValveIdentities(representationRoot);
                Dictionary<GameObject, MechanicalIdentity>
                    mechanicalIdentities =
                        BuildMechanicalIdentities(representationRoot);
                List<MeshRenderer> meshes = representationRoot
                    .GetComponentsInChildren<MeshRenderer>(true)
                    .Where(item => item.name != "MTRM_TimingBelt")
                    .OrderBy(item => RelativePath(
                        item.transform,
                        representationRoot))
                    .ToList();
                if (meshes.Count != 148)
                    throw new InvalidOperationException(
                        $"{representation}: expected 148 meshes, " +
                        $"found {meshes.Count}.");

                foreach (MeshRenderer mesh in meshes)
                {
                    Binding binding = Resolve(
                        mesh,
                        representationRoot,
                        representation,
                        mappings,
                        texts,
                        valveIdentities,
                        mechanicalIdentities);
                    ConfigureTag(mesh.gameObject, binding);
                    bindings.Add(binding);
                }
            }

            Validate(root, bindings, mappings);
            if (!PrefabUtility.SaveAsPrefabAsset(root, PrefabPath))
                throw new InvalidOperationException(
                    $"Could not save prefab: {PrefabPath}");
            WriteBindings(bindings);
            Debug.Log(
                "MTRM ComponentTag migration: tags=296, " +
                $"intentionally_unidentified=" +
                $"{bindings.Count(item => item.IntentionallyUnidentified)}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void GenerateFromCommandLine()
    {
        try
        {
            Generate();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static Binding Resolve(
        MeshRenderer mesh,
        Transform representationRoot,
        EngineRepresentation representation,
        IReadOnlyDictionary<string, Mapping> mappings,
        IReadOnlyDictionary<EngineComponentId, TextContent> texts,
        IReadOnlyDictionary<GameObject, ValveIdentity> valveIdentities,
        IReadOnlyDictionary<GameObject, MechanicalIdentity>
            mechanicalIdentities)
    {
        ToolTipTrigger tooltip = mesh.GetComponent<ToolTipTrigger>();
        string title = tooltip != null ? tooltip.TitleText : string.Empty;
        string path = RelativePath(mesh.transform, representationRoot);
        if (string.IsNullOrWhiteSpace(title))
        {
            return new Binding(
                representation,
                path,
                string.Empty,
                EngineComponentId.None,
                0,
                0,
                string.Empty,
                string.Empty,
                "intentionally_unidentified",
                true,
                UnidentifiedJustification);
        }

        if (!mappings.TryGetValue(title, out Mapping mapping))
            throw new InvalidOperationException(
                $"No mapping for tooltip '{title}' at {path}.");

        bool isValveRule =
            mapping.ResolutionRule.StartsWith(
                "Valvula.",
                StringComparison.Ordinal);
        if (isValveRule)
        {
            if (!valveIdentities.TryGetValue(mesh.gameObject, out ValveIdentity valve))
            {
                return new Binding(
                    representation,
                    path,
                    title,
                    EngineComponentId.None,
                    0,
                    0,
                    mapping.DisplayName,
                    mapping.Description,
                    mapping.ResolutionRule + " -> unresolved",
                    false,
                    string.Empty);
            }
            return new Binding(
                representation,
                path,
                title,
                valve.Id,
                valve.CylinderIndex,
                valve.SubIndex,
                TextFor(valve.Id, texts).DisplayName,
                TextFor(valve.Id, texts).Description,
                valve.ResolutionRule,
                false,
                string.Empty);
        }

        if (mechanicalIdentities.TryGetValue(
                mesh.gameObject,
                out MechanicalIdentity mechanical))
        {
            return new Binding(
                representation,
                path,
                title,
                mechanical.Id,
                mechanical.InstanceIndex,
                mechanical.SubIndex,
                TextFor(mechanical.Id, texts).DisplayName,
                TextFor(mechanical.Id, texts).Description,
                mechanical.ResolutionRule,
                false,
                string.Empty);
        }

        if (!Enum.TryParse(
                mapping.ComponentId,
                false,
                out EngineComponentId id) ||
            id == EngineComponentId.None)
        {
            throw new InvalidOperationException(
                $"Invalid EngineComponentId '{mapping.ComponentId}' " +
                $"for tooltip '{title}'.");
        }

        return new Binding(
            representation,
            path,
            title,
            id,
            0,
            0,
            TextFor(id, texts).DisplayName,
            TextFor(id, texts).Description,
            string.IsNullOrWhiteSpace(mapping.ResolutionRule)
                ? "ToolTipTrigger.titleText exact"
                : mapping.ResolutionRule,
            false,
            string.Empty);
    }

    private static Dictionary<GameObject, MechanicalIdentity>
        BuildMechanicalIdentities(Transform representationRoot)
    {
        var result = new Dictionary<GameObject, MechanicalIdentity>();
        AddPistonIdentities(representationRoot, result);
        AddAxialCylinderComponentIdentities(
            representationRoot,
            result);

        foreach (MeshRenderer mesh in TitledMeshes(
                     representationRoot,
                     "Virabrequim"))
        {
            bool crankshaft = mesh.GetComponent<Virabrequim>() != null;
            result.Add(
                mesh.gameObject,
                new MechanicalIdentity(
                    crankshaft
                        ? EngineComponentId.Virabrequim
                        : EngineComponentId.EngrenagemVirabrequim,
                    0,
                    0,
                    "Virabrequim component m_GameObject reference",
                    crankshaft ? null : "Engrenagem do Virabrequim",
                    crankshaft
                        ? null
                        : "A engrenagem do virabrequim transmite a rotação " +
                          "do eixo para as engrenagens dos comandos, " +
                          "mantendo válvulas e pistões sincronizados."));
        }

        foreach (ArvoreComando camshaft in representationRoot
                     .GetComponentsInChildren<ArvoreComando>(true))
        {
            int side = camshaft.GetLadoComando();
            if (side != 0 && side != 1)
                throw new InvalidOperationException(
                    $"Unexpected ArvoreComando.ladoComando={side}.");
            MeshRenderer mesh = camshaft.GetComponent<MeshRenderer>();
            if (mesh == null)
                throw new InvalidOperationException(
                    "ArvoreComando has no MeshRenderer on its GameObject.");
            result.Add(
                mesh.gameObject,
                new MechanicalIdentity(
                    EngineComponentId.ArvoreDeCames,
                    0,
                    side + 1,
                    "ArvoreComando.ladoComando"));
        }

        List<ArvoreComando> classifiedCamshafts = representationRoot
            .GetComponentsInChildren<ArvoreComando>(true)
            .ToList();
        foreach (MeshRenderer gear in TitledMeshes(
                     representationRoot,
                     "Engrenagem de Comando"))
        {
            var distances = classifiedCamshafts
                .Select(camshaft => new
                {
                    Camshaft = camshaft,
                    Distance = PerpendicularDistanceToCamshaftAxis(
                        gear.bounds.center,
                        camshaft.GetComponent<MeshRenderer>())
                })
                .OrderBy(item => item.Distance)
                .ToList();
            if (distances.Count != 2 ||
                distances[0].Distance >= distances[1].Distance * 0.8f)
            {
                throw new InvalidOperationException(
                    "Camshaft gear axis resolution is ambiguous.");
            }
            result.Add(
                gear.gameObject,
                new MechanicalIdentity(
                    EngineComponentId.EngrenagemDeComando,
                    0,
                    distances[0].Camshaft.GetLadoComando() + 1,
                    "Perpendicular distance to axis classified by " +
                    "ArvoreComando.ladoComando"));
        }

        foreach (MeshRenderer mesh in TitledMeshes(
                     representationRoot,
                     "Cabos de Vela"))
        {
            long meshId = SharedMeshLocalId(mesh);
            if (meshId == IgnitionHarnessMeshId)
            {
                result.Add(
                    mesh.gameObject,
                    new MechanicalIdentity(
                        EngineComponentId.CaboVela,
                        0,
                        0,
                        $"Mesh reference localFileId={meshId}"));
                continue;
            }
            if (!ConnectorCylinderByMeshId.TryGetValue(
                    meshId,
                    out int cylinder))
            {
                throw new InvalidOperationException(
                    $"Unknown ignition mesh reference localFileId={meshId}.");
            }
            result.Add(
                mesh.gameObject,
                new MechanicalIdentity(
                        EngineComponentId.ConectorVela,
                        cylinder,
                        0,
                        $"Mesh reference localFileId={meshId}",
                        "Conector da Vela",
                        "O conector da vela liga o chicote elétrico a uma " +
                        "vela de ignição específica."));
        }

        return result;
    }

    private static void AddPistonIdentities(
        Transform representationRoot,
        IDictionary<GameObject, MechanicalIdentity> identities)
    {
        MeshRenderer crankshaft = SingleTaggedRenderer(
            representationRoot,
            EngineComponentId.Virabrequim);
        MeshRenderer flywheel = SingleTaggedRenderer(
            representationRoot,
            EngineComponentId.Volante);
        MeshRenderer gear = TitledMeshes(
                representationRoot,
                "Virabrequim")
            .Single(renderer =>
                renderer.GetComponent<Virabrequim>() == null);
        MeshRenderer[] pistons = TitledMeshes(
                representationRoot,
                "Pistão")
            .ToArray();
        if (pistons.Length != 4)
            throw new InvalidOperationException(
                $"Expected four pistons, found {pistons.Length}.");

        Vector3 axis = PrincipalWorldAxis(crankshaft);
        Vector3 origin = gear.bounds.center;
        if (Vector3.Dot(flywheel.bounds.center - origin, axis) < 0f)
            axis = -axis;

        List<MeshRenderer> ordered = pistons
            .OrderBy(renderer =>
                Vector3.Dot(renderer.bounds.center - origin, axis))
            .ToList();
        MeshRenderer farthestFromFlywheel = pistons
            .OrderByDescending(renderer => Vector3.Distance(
                renderer.bounds.center,
                flywheel.bounds.center))
            .First();
        MeshRenderer nearestToGear = pistons
            .OrderBy(renderer => Vector3.Distance(
                renderer.bounds.center,
                gear.bounds.center))
            .First();
        if (ordered[0] != farthestFromFlywheel ||
            ordered[0] != nearestToGear)
        {
            throw new InvalidOperationException(
                "Piston landmarks disagree on cylinder 1.");
        }

        for (int index = 0; index < ordered.Count; index++)
        {
            identities.Add(
                ordered[index].gameObject,
                new MechanicalIdentity(
                    EngineComponentId.Pistao,
                    index + 1,
                    0,
                    "Projection on crankshaft axis, ordered from " +
                    "crankshaft gear toward flywheel"));
        }
    }

    private static MeshRenderer SingleTaggedRenderer(
        Transform representationRoot,
        EngineComponentId id)
    {
        ComponentTag[] tags = representationRoot
            .GetComponentsInChildren<ComponentTag>(true)
            .Where(tag => tag.Id == id)
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

    private static void AddAxialCylinderComponentIdentities(
        Transform representationRoot,
        IDictionary<GameObject, MechanicalIdentity> identities)
    {
        MeshRenderer crankshaft = SingleTaggedRenderer(
            representationRoot,
            EngineComponentId.Virabrequim);
        MeshRenderer flywheel = SingleTaggedRenderer(
            representationRoot,
            EngineComponentId.Volante);
        MeshRenderer gear = TitledMeshes(
                representationRoot,
                "Virabrequim")
            .Single(renderer =>
                renderer.GetComponent<Virabrequim>() == null);
        Vector3 axis = PrincipalWorldAxis(crankshaft);
        Vector3 origin = gear.bounds.center;
        if (Vector3.Dot(flywheel.bounds.center - origin, axis) < 0f)
            axis = -axis;

        List<(MeshRenderer Renderer, int Index, float Coordinate)> pistons =
            TitledMeshes(representationRoot, "Pistão")
                .Select(renderer => (
                    renderer,
                    identities[renderer.gameObject].InstanceIndex,
                    Vector3.Dot(
                        renderer.bounds.center - origin,
                        axis)))
                .ToList();

        foreach ((string Title, EngineComponentId Id) definition in new[]
                 {
                     ("Biela", EngineComponentId.Biela),
                     ("Pino do Pistão", EngineComponentId.PinoPistao),
                     ("Vela de ignição", EngineComponentId.VelaIgnicao)
                 })
        {
            foreach (MeshRenderer renderer in TitledMeshes(
                         representationRoot,
                         definition.Title))
            {
                float coordinate = Vector3.Dot(
                    renderer.bounds.center - origin,
                    axis);
                var nearest = pistons
                    .Select(piston => new
                    {
                        piston.Index,
                        Distance = Mathf.Abs(
                            coordinate - piston.Coordinate)
                    })
                    .OrderBy(item => item.Distance)
                    .ToList();
                identities.Add(
                    renderer.gameObject,
                    new MechanicalIdentity(
                        definition.Id,
                        nearest[0].Index,
                        0,
                        "Nearest piston by projected coordinate on " +
                        "crankshaft axis"));
            }
        }

        foreach (EngineComponentId id in new[]
                 {
                     EngineComponentId.PinoPistao,
                     EngineComponentId.VelaIgnicao
                 })
        {
            int[] indices = identities
                .Where(item => item.Value.Id == id)
                .Select(item => item.Value.InstanceIndex)
                .OrderBy(value => value)
                .ToArray();
            if (!indices.SequenceEqual(new[] { 1, 2, 3, 4 }))
                throw new InvalidOperationException(
                    $"{id} axial indices are not 1..4.");
        }

        foreach (int cylinder in Enumerable.Range(1, 4))
        {
            GameObject[] rods = identities
                .Where(item =>
                    item.Value.Id == EngineComponentId.Biela &&
                    item.Value.InstanceIndex == cylinder)
                .Select(item => item.Key)
                .ToArray();
            if (rods.Length != 2 ||
                rods.Count(item => item.name.StartsWith(
                    "biela parte de baixo",
                    StringComparison.OrdinalIgnoreCase)) != 1)
            {
                throw new InvalidOperationException(
                    $"Biela cylinder {cylinder} must contain one body " +
                    "and one cap.");
            }
        }
    }

    private static Vector3 PrincipalWorldAxis(MeshRenderer renderer)
    {
        Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null)
            throw new InvalidOperationException(
                $"{renderer.name} has no referenced mesh.");
        Vector3 size = mesh.bounds.size;
        Vector3 localAxis = size.x >= size.y && size.x >= size.z
            ? Vector3.right
            : size.y >= size.z ? Vector3.up : Vector3.forward;
        return renderer.transform.TransformDirection(localAxis).normalized;
    }

    private static float PerpendicularDistanceToCamshaftAxis(
        Vector3 point,
        MeshRenderer camshaft)
    {
        MeshFilter filter = camshaft.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            throw new InvalidOperationException(
                "Camshaft has no referenced mesh.");

        Bounds localBounds = filter.sharedMesh.bounds;
        Vector3 size = localBounds.size;
        Vector3 localAxis = size.x >= size.y && size.x >= size.z
            ? Vector3.right
            : size.y >= size.z
                ? Vector3.up
                : Vector3.forward;
        Vector3 worldAxis = camshaft.transform
            .TransformDirection(localAxis)
            .normalized;
        Vector3 axisOrigin = camshaft.transform.TransformPoint(
            localBounds.center);
        return Vector3.Cross(point - axisOrigin, worldAxis).magnitude;
    }

    private static long SharedMeshLocalId(MeshRenderer renderer)
    {
        Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null ||
            !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                mesh,
                out _,
                out long localId))
        {
            throw new InvalidOperationException(
                $"Could not resolve shared mesh for {renderer.name}.");
        }
        return localId;
    }

    private static IEnumerable<MeshRenderer> TitledMeshes(
        Transform representationRoot,
        string title)
    {
        return representationRoot
            .GetComponentsInChildren<MeshRenderer>(true)
            .Where(item =>
            {
                ToolTipTrigger tooltip = item.GetComponent<ToolTipTrigger>();
                return tooltip != null && tooltip.TitleText == title;
            });
    }

    private static Dictionary<GameObject, ValveIdentity> BuildValveIdentities(
        Transform representationRoot)
    {
        var result = new Dictionary<GameObject, ValveIdentity>();
        var valves = representationRoot
            .GetComponentsInChildren<Valvula>(true)
            .Select(component =>
            {
                var serialized = new SerializedObject(component);
                return new ValveReferences(
                    serialized.FindProperty("cicloValvula").intValue,
                    serialized.FindProperty("tipoValvula").intValue,
                    serialized.FindProperty("hasteValvula")
                        .objectReferenceValue as GameObject,
                    serialized.FindProperty("molaValvula")
                        .objectReferenceValue as GameObject,
                    serialized.FindProperty("baseValvula")
                        .objectReferenceValue as GameObject);
            })
            .OrderBy(item => item.CylinderIndex)
            .ThenBy(item => item.ValveType)
            .ThenBy(item => RelativePath(
                item.Head.transform,
                representationRoot))
            .ToList();

        if (valves.Count != 16)
            throw new InvalidOperationException(
                $"Expected 16 Valvula components, found {valves.Count}.");

        foreach (IGrouping<(int Cylinder, int Type), ValveReferences> group in
                 valves.GroupBy(item =>
                     (item.CylinderIndex, item.ValveType)))
        {
            int subIndex = 0;
            foreach (ValveReferences valve in group)
            {
                subIndex++;
                if (valve.CylinderIndex < 1 || valve.CylinderIndex > 4)
                    throw new InvalidOperationException(
                        $"Unexpected cicloValvula={valve.CylinderIndex}.");
                if (valve.ValveType != 0 && valve.ValveType != 1)
                    throw new InvalidOperationException(
                        $"Unexpected tipoValvula={valve.ValveType}.");
                EngineComponentId headId = valve.ValveType == 0
                    ? EngineComponentId.ValvulaAdmissao
                    : EngineComponentId.ValvulaEscape;
                AddValveIdentity(
                    result,
                    valve.Head,
                    new ValveIdentity(
                        headId,
                        valve.CylinderIndex,
                        subIndex,
                        "Valvula.hasteValvula + Valvula.tipoValvula"));
                AddValveIdentity(
                    result,
                    valve.Spring,
                    new ValveIdentity(
                        EngineComponentId.MolaValvula,
                        valve.CylinderIndex,
                        subIndex,
                        "Valvula.molaValvula"));
                AddValveIdentity(
                    result,
                    valve.Base,
                    new ValveIdentity(
                        EngineComponentId.PratoMola,
                        valve.CylinderIndex,
                        subIndex,
                        "Valvula.baseValvula"));
            }
        }
        return result;
    }

    private static void AddValveIdentity(
        IDictionary<GameObject, ValveIdentity> identities,
        GameObject target,
        ValveIdentity identity)
    {
        if (target == null)
            throw new InvalidOperationException(
                $"{identity.ResolutionRule} is empty.");
        if (identities.ContainsKey(target))
            throw new InvalidOperationException(
                $"Valve mesh referenced more than once: {target.name}.");
        identities.Add(target, identity);
    }

    private static void ConfigureTag(GameObject target, Binding binding)
    {
        ComponentTag[] existing = target.GetComponents<ComponentTag>();
        if (existing.Length > 1)
            throw new InvalidOperationException(
                $"More than one ComponentTag at {binding.MeshPath}.");
        ComponentTag tag = existing.Length == 1
            ? existing[0]
            : Undo.AddComponent<ComponentTag>(target);
        var serialized = new SerializedObject(tag);
        serialized.FindProperty("id").intValue =
            (int)binding.Id;
        serialized.FindProperty("instanceIndex").intValue =
            binding.InstanceIndex;
        serialized.FindProperty("subIndex").intValue =
            binding.SubIndex;
        serialized.FindProperty("representation").intValue =
            (int)binding.Representation;
        serialized.FindProperty("displayName").stringValue =
            binding.DisplayName;
        serialized.FindProperty("educationalDescription").stringValue =
            binding.Description;
        serialized.FindProperty("intentionallyUnidentified").boolValue =
            binding.IntentionallyUnidentified;
        serialized.FindProperty("justification").stringValue =
            binding.Justification;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tag);

        ComponentVisualAccessibility visual =
            target.GetComponent<ComponentVisualAccessibility>();
        if (binding.Id == EngineComponentId.PratoMola)
        {
            if (visual == null)
                visual = Undo.AddComponent<ComponentVisualAccessibility>(
                    target);
            EditorUtility.SetDirty(visual);
        }
        else if (visual != null)
        {
            Undo.DestroyObjectImmediate(visual);
        }
    }

    private static void Validate(
        GameObject root,
        IReadOnlyList<Binding> bindings,
        IReadOnlyDictionary<string, Mapping> mappings)
    {
        if (bindings.Count != 296)
            throw new InvalidOperationException(
                $"Expected 296 bindings, found {bindings.Count}.");
        if (root.GetComponentsInChildren<ComponentTag>(true)
                .Count(tag => tag.Id != EngineComponentId.CorreiaDentada) != 296)
            throw new InvalidOperationException(
                "Every interactive mesh must have exactly one ComponentTag.");

        List<Binding> unresolved = bindings
            .Where(item =>
                item.Id == EngineComponentId.None &&
                !item.IntentionallyUnidentified)
            .ToList();
        if (unresolved.Count > 0)
            throw new InvalidOperationException(
                "Strict resolution failed: " +
                string.Join(", ", unresolved.Select(item => item.MeshPath)));

        List<Binding> invalidExclusions = bindings
            .Where(item =>
                item.IntentionallyUnidentified &&
                (item.Id != EngineComponentId.None ||
                 string.IsNullOrWhiteSpace(item.Justification)))
            .ToList();
        if (invalidExclusions.Count > 0)
            throw new InvalidOperationException(
                "Intentional exclusions require id=None and justification.");

        int intentionalCount = bindings.Count(item =>
            item.IntentionallyUnidentified);
        if (intentionalCount != 8)
            throw new InvalidOperationException(
                $"Expected 8 intentional exclusions, found {intentionalCount}.");

        foreach (IGrouping<EngineComponentId, Binding> idGroup in bindings
                     .Where(item => item.Id != EngineComponentId.None)
                     .GroupBy(item => item.Id))
        {
            int expanded = idGroup.Count(item =>
                item.Representation == EngineRepresentation.Expande);
            int running = idGroup.Count(item =>
                item.Representation == EngineRepresentation.LigaDesliga);
            if (expanded != running)
                throw new InvalidOperationException(
                    $"Asymmetric count for {idGroup.Key}: " +
                    $"{expanded} vs {running}.");

            string[] displayNames = idGroup
                .Select(item => item.DisplayName)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (displayNames.Length != 1)
                throw new InvalidOperationException(
                    $"Inconsistent displayName for {idGroup.Key}: " +
                    string.Join(" | ", displayNames));

            string[] descriptions = idGroup
                .Select(item => item.Description)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (descriptions.Length != 1)
                throw new InvalidOperationException(
                    $"Inconsistent educationalDescription for {idGroup.Key}.");
        }

        if (bindings.Any(item =>
                item.Id == EngineComponentId.Valvula))
        {
            throw new InvalidOperationException(
                "Generic Valvula is forbidden after strict resolution.");
        }

        var knownTitles = new HashSet<string>(mappings.Keys);
        if (bindings.Any(item =>
                !string.IsNullOrWhiteSpace(item.TooltipTitle) &&
                !knownTitles.Contains(item.TooltipTitle)))
        {
            throw new InvalidOperationException(
                "A tagged tooltip is absent from component_mapping.csv.");
        }
    }

    private static Dictionary<string, Mapping> LoadMappings()
    {
        string path = Path.GetFullPath(
            Path.Combine(Application.dataPath, MappingPath));
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        List<string> header = ParseCsvLine(lines[0]);
        int titleIndex = header.IndexOf("tooltip_title");
        int idIndex = header.IndexOf("engine_component_id");
        int displayIndex = header.IndexOf("display_name");
        int descriptionIndex = header.IndexOf("educational_description");
        int ruleIndex = header.IndexOf("resolution_rule");
        if (new[]
            {
                titleIndex,
                idIndex,
                displayIndex,
                descriptionIndex,
                ruleIndex
            }.Any(index => index < 0))
        {
            throw new InvalidOperationException(
                "component_mapping.csv is missing a required column.");
        }

        var result = new Dictionary<string, Mapping>(
            StringComparer.Ordinal);
        foreach (string line in lines.Skip(1).Where(item =>
                     !string.IsNullOrWhiteSpace(item)))
        {
            List<string> fields = ParseCsvLine(line);
            while (fields.Count < header.Count)
                fields.Add(string.Empty);
            var mapping = new Mapping(
                fields[idIndex],
                fields[displayIndex],
                fields[descriptionIndex],
                fields[ruleIndex]);
            if (!result.TryAdd(fields[titleIndex], mapping))
                throw new InvalidOperationException(
                    $"Duplicate tooltip title: {fields[titleIndex]}.");
        }
        if (result.Count != 35)
            throw new InvalidOperationException(
                $"Expected 35 mapping rows, found {result.Count}.");
        return result;
    }

    private static Dictionary<EngineComponentId, TextContent>
        BuildTextCatalog(IReadOnlyDictionary<string, Mapping> mappings)
    {
        var result = new Dictionary<EngineComponentId, TextContent>();
        foreach (Mapping mapping in mappings.Values)
        {
            if (!Enum.TryParse(
                    mapping.ComponentId,
                    false,
                    out EngineComponentId id) ||
                id == EngineComponentId.None)
            {
                throw new InvalidOperationException(
                    $"Invalid text catalog id: {mapping.ComponentId}.");
            }

            if (id == EngineComponentId.Valvula)
            {
                AddText(
                    result,
                    EngineComponentId.ValvulaAdmissao,
                    "Válvula de Admissão",
                    mapping.Description);
                AddText(
                    result,
                    EngineComponentId.ValvulaEscape,
                    "Válvula de Escape",
                    mapping.Description);
                continue;
            }
            AddText(result, id, mapping.DisplayName, mapping.Description);
        }

        AddText(
            result,
            EngineComponentId.EngrenagemVirabrequim,
            "Engrenagem do Virabrequim",
            "A engrenagem do virabrequim transmite a rotação do eixo para " +
            "as engrenagens dos comandos, mantendo válvulas e pistões " +
            "sincronizados.");
        AddText(
            result,
            EngineComponentId.ConectorVela,
            "Conector da Vela",
            "O conector da vela liga o chicote elétrico a uma vela de " +
            "ignição específica.");
        return result;
    }

    private static void AddText(
        IDictionary<EngineComponentId, TextContent> catalog,
        EngineComponentId id,
        string displayName,
        string description)
    {
        var candidate = new TextContent(displayName, description);
        if (catalog.TryGetValue(id, out TextContent existing))
        {
            if (existing.DisplayName != candidate.DisplayName ||
                existing.Description != candidate.Description)
            {
                throw new InvalidOperationException(
                    $"Conflicting instructional text for {id}.");
            }
            return;
        }
        catalog.Add(id, candidate);
    }

    private static TextContent TextFor(
        EngineComponentId id,
        IReadOnlyDictionary<EngineComponentId, TextContent> catalog)
    {
        if (!catalog.TryGetValue(id, out TextContent text))
            throw new InvalidOperationException(
                $"Instructional text missing for {id}.");
        return text;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (character == '"')
            {
                if (quoted &&
                    index + 1 < line.Length &&
                    line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }
        fields.Add(current.ToString());
        return fields;
    }

    private static void WriteBindings(IEnumerable<Binding> bindings)
    {
        var csv = new StringBuilder();
        csv.AppendLine(
            "representation,mesh_path,tooltip_title,engine_component_id," +
            "instance_index,sub_index,resolution_rule," +
            "intentionally_unidentified,justification");
        foreach (Binding binding in bindings
                     .OrderBy(item => item.Representation)
                     .ThenBy(item => item.MeshPath))
        {
            csv.Append(Csv(binding.Representation.ToString()));
            csv.Append(',');
            csv.Append(Csv(binding.MeshPath));
            csv.Append(',');
            csv.Append(Csv(binding.TooltipTitle));
            csv.Append(',');
            csv.Append(Csv(binding.Id.ToString()));
            csv.Append(',');
            csv.Append(binding.InstanceIndex);
            csv.Append(',');
            csv.Append(binding.SubIndex);
            csv.Append(',');
            csv.Append(Csv(binding.ResolutionRule));
            csv.Append(',');
            csv.Append(binding.IntentionallyUnidentified ? "true" : "false");
            csv.Append(',');
            csv.AppendLine(Csv(binding.Justification));
        }
        string path = Path.GetFullPath(
            Path.Combine(Application.dataPath, BindingsPath));
        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
    }

    private static string RelativePath(Transform current, Transform root)
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

    private static string Csv(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }

    private sealed class Mapping
    {
        public Mapping(
            string componentId,
            string displayName,
            string description,
            string resolutionRule)
        {
            ComponentId = componentId;
            DisplayName = displayName;
            Description = description;
            ResolutionRule = resolutionRule;
        }

        public string ComponentId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string ResolutionRule { get; }
    }

    private sealed class TextContent
    {
        public TextContent(string displayName, string description)
        {
            DisplayName = displayName;
            Description = description;
        }

        public string DisplayName { get; }
        public string Description { get; }
    }

    private sealed class ValveReferences
    {
        public ValveReferences(
            int cylinderIndex,
            int valveType,
            GameObject head,
            GameObject spring,
            GameObject valveBase)
        {
            CylinderIndex = cylinderIndex;
            ValveType = valveType;
            Head = head;
            Spring = spring;
            Base = valveBase;
        }

        public int CylinderIndex { get; }
        public int ValveType { get; }
        public GameObject Head { get; }
        public GameObject Spring { get; }
        public GameObject Base { get; }
    }

    private sealed class ValveIdentity
    {
        public ValveIdentity(
            EngineComponentId id,
            int cylinderIndex,
            int subIndex,
            string resolutionRule)
        {
            Id = id;
            CylinderIndex = cylinderIndex;
            SubIndex = subIndex;
            ResolutionRule = resolutionRule;
        }

        public EngineComponentId Id { get; }
        public int CylinderIndex { get; }
        public int SubIndex { get; }
        public string ResolutionRule { get; }
    }

    private sealed class Binding
    {
        public Binding(
            EngineRepresentation representation,
            string meshPath,
            string tooltipTitle,
            EngineComponentId id,
            int instanceIndex,
            int subIndex,
            string displayName,
            string description,
            string resolutionRule,
            bool intentionallyUnidentified,
            string justification)
        {
            Representation = representation;
            MeshPath = meshPath;
            TooltipTitle = tooltipTitle;
            Id = id;
            InstanceIndex = instanceIndex;
            SubIndex = subIndex;
            DisplayName = displayName;
            Description = description;
            ResolutionRule = resolutionRule;
            IntentionallyUnidentified = intentionallyUnidentified;
            Justification = justification;
        }

        public EngineRepresentation Representation { get; }
        public string MeshPath { get; }
        public string TooltipTitle { get; }
        public EngineComponentId Id { get; }
        public int InstanceIndex { get; }
        public int SubIndex { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string ResolutionRule { get; }
        public bool IntentionallyUnidentified { get; }
        public string Justification { get; }
    }

    private sealed class MechanicalIdentity
    {
        public MechanicalIdentity(
            EngineComponentId id,
            int instanceIndex,
            int subIndex,
            string resolutionRule,
            string displayName = null,
            string description = null)
        {
            Id = id;
            InstanceIndex = instanceIndex;
            SubIndex = subIndex;
            ResolutionRule = resolutionRule;
            DisplayName = displayName;
            Description = description;
        }

        public EngineComponentId Id { get; }
        public int InstanceIndex { get; }
        public int SubIndex { get; }
        public string ResolutionRule { get; }
        public string DisplayName { get; }
        public string Description { get; }
    }
}
