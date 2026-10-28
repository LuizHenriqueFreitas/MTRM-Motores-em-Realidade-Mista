using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ExperimentalSceneValidationResult
{
    public readonly List<string> errors = new List<string>();
    public bool IsValid => errors.Count == 0;
    public string Message => IsValid
        ? "Cena experimental válida."
        : string.Join(Environment.NewLine, errors);
}

public static class ExperimentalSceneValidator
{
    public static ExperimentalSceneValidationResult ValidateActiveScene(
        ExperimentProtocol protocol)
    {
        return ValidateScene(SceneManager.GetActiveScene(), protocol);
    }

    public static ExperimentalSceneValidationResult ValidateScene(
        Scene scene,
        ExperimentProtocol protocol)
    {
        var tags = new List<ComponentTag>();
        var interactiveObjects = new HashSet<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            tags.AddRange(root.GetComponentsInChildren<ComponentTag>(true));
            foreach (Collider collider in
                     root.GetComponentsInChildren<Collider>(true))
            {
                if (collider.GetComponent<Renderer>() != null)
                    interactiveObjects.Add(collider.transform);
            }

            foreach (AddMeshColliderComponent source in
                     root.GetComponentsInChildren<AddMeshColliderComponent>(
                         true))
            {
                foreach (MeshRenderer mesh in
                         source.GetComponentsInChildren<MeshRenderer>(true))
                {
                    interactiveObjects.Add(mesh.transform);
                }
            }
        }

        return Validate(protocol, tags, interactiveObjects);
    }

    public static ExperimentalSceneValidationResult Validate(
        ExperimentProtocol protocol,
        IReadOnlyCollection<ComponentTag> tags,
        IReadOnlyCollection<Transform> interactiveObjects)
    {
        var result = new ExperimentalSceneValidationResult();
        tags ??= Array.Empty<ComponentTag>();
        interactiveObjects ??= Array.Empty<Transform>();

        if (protocol == null)
        {
            result.errors.Add(
                "PROTOCOL_MISSING: protocolo experimental não carregado.");
            return result;
        }

        HashSet<EngineComponentId> present = tags
            .Where(tag => tag != null && tag.Id != EngineComponentId.None)
            .Select(tag => tag.Id)
            .ToHashSet();
        HashSet<EngineComponentId> referenced = protocol.tasks
            .Where(task => task != null)
            .SelectMany(task =>
                (task.acceptedComponents ??
                 new List<EngineComponentId>())
                .Concat(task.rejectedComponents ??
                        new List<EngineComponentId>()))
            .Where(id => id != EngineComponentId.None)
            .ToHashSet();
        foreach (EngineComponentId id in referenced.OrderBy(id => (int)id))
        {
            if (!present.Contains(id))
            {
                result.errors.Add(
                    $"PROTOCOL_COMPONENT_MISSING: {id} não existe na cena.");
            }
        }

        foreach (Transform interactive in interactiveObjects
                     .Where(item => item != null)
                     .OrderBy(HierarchyPath))
        {
            if (interactive.GetComponentInParent<ComponentTag>(true) == null)
            {
                result.errors.Add(
                    "INTERACTIVE_WITHOUT_TAG: " +
                    $"{HierarchyPath(interactive)} não possui ComponentTag.");
            }
        }

        foreach (ComponentTag tag in tags.Where(item => item != null))
        {
            string path = HierarchyPath(tag.transform);
            if (tag.Id != EngineComponentId.None &&
                string.IsNullOrWhiteSpace(tag.DisplayName))
            {
                result.errors.Add(
                    $"TAG_WITHOUT_DISPLAY_NAME: {path} ({tag.Id}).");
            }

            if (tag.IntentionallyUnidentified &&
                string.IsNullOrWhiteSpace(tag.Justification))
            {
                result.errors.Add(
                    $"UNIDENTIFIED_WITHOUT_JUSTIFICATION: {path}.");
            }
        }

        foreach (IGrouping<EngineComponentId, ComponentTag> group in tags
                     .Where(tag =>
                         tag != null &&
                         tag.Id != EngineComponentId.None)
                     .GroupBy(tag => tag.Id))
        {
            string[] displayNames = group
                .Select(tag => tag.DisplayName)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (displayNames.Length > 1)
            {
                result.errors.Add(
                    "COMPONENT_DISPLAY_NAME_MISMATCH: " +
                    $"{group.Key} possui nomes divergentes: " +
                    string.Join(" | ", displayNames) + ".");
            }
        }

        Dictionary<EngineComponentId, int> expanded = Counts(
            tags,
            EngineRepresentation.Expande);
        Dictionary<EngineComponentId, int> running = Counts(
            tags,
            EngineRepresentation.LigaDesliga);
        foreach (EngineComponentId id in expanded.Keys
                     .Union(running.Keys)
                     .OrderBy(id => (int)id))
        {
            expanded.TryGetValue(id, out int expandedCount);
            running.TryGetValue(id, out int runningCount);
            if (expandedCount != runningCount)
            {
                result.errors.Add(
                    "REPRESENTATION_COUNT_MISMATCH: " +
                    $"{id} Expande={expandedCount}, " +
                    $"LigaDesliga={runningCount}.");
            }
        }

        return result;
    }

    private static Dictionary<EngineComponentId, int> Counts(
        IEnumerable<ComponentTag> tags,
        EngineRepresentation representation)
    {
        return tags
            .Where(tag =>
                tag != null &&
                tag.Id != EngineComponentId.None &&
                tag.Representation == representation)
            .GroupBy(tag => tag.Id)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private static string HierarchyPath(Transform transform)
    {
        if (transform == null)
            return "<null>";
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
