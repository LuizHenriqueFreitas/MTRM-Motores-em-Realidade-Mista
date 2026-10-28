using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ComponentTagPrefabTests
{
    private const string PrefabPath =
        "Assets/MTRM/Prefabs/Engine/StateEngineManager.prefab";

    [Test]
    public void EveryInteractiveMeshHasExactlyOneTag()
    {
        GameObject prefab = LoadPrefab();
        MeshRenderer[] meshes =
            prefab.GetComponentsInChildren<MeshRenderer>(true);
        ComponentTag[] tags =
            prefab.GetComponentsInChildren<ComponentTag>(true);

        Assert.That(meshes, Has.Length.EqualTo(298));
        Assert.That(tags, Has.Length.EqualTo(298));
        Assert.That(
            meshes.All(mesh =>
                mesh.GetComponents<ComponentTag>().Length == 1),
            Is.True);
    }

    [Test]
    public void GeneratedTimingBeltsAreSymmetricTaggedAndClickable()
    {
        ComponentTag[] belts = LoadPrefab()
            .GetComponentsInChildren<ComponentTag>(true)
            .Where(tag => tag.Id == EngineComponentId.CorreiaDentada)
            .OrderBy(tag => tag.Representation)
            .ToArray();

        Assert.That((int)EngineComponentId.CorreiaDentada, Is.EqualTo(310));
        Assert.That(belts, Has.Length.EqualTo(2));
        Assert.That(
            belts.Select(tag => tag.Representation),
            Is.EqualTo(new[]
            {
                EngineRepresentation.Expande,
                EngineRepresentation.LigaDesliga
            }));
        Assert.That(
            belts.All(tag =>
                tag.GetComponent<MeshFilter>().sharedMesh != null &&
                tag.GetComponent<MeshCollider>().sharedMesh != null),
            Is.True);
        Assert.That(
            belts.Single(tag =>
                tag.Representation == EngineRepresentation.Expande)
                .gameObject.activeSelf,
            Is.True);
        Assert.That(
            belts.Single(tag =>
                tag.Representation == EngineRepresentation.LigaDesliga)
                .gameObject.activeSelf,
            Is.True);
    }

    [Test]
    public void IntentionalExclusionsAreExplicitAndJustified()
    {
        ComponentTag[] tags =
            LoadPrefab().GetComponentsInChildren<ComponentTag>(true);
        ComponentTag[] exclusions = tags
            .Where(tag => tag.IntentionallyUnidentified)
            .ToArray();

        Assert.That(exclusions, Has.Length.EqualTo(8));
        Assert.That(
            exclusions.All(tag =>
                tag.Id == EngineComponentId.None &&
                !string.IsNullOrWhiteSpace(tag.Justification)),
            Is.True);
        Assert.That(
            tags.Where(tag => !tag.IntentionallyUnidentified)
                .All(tag => tag.Id != EngineComponentId.None),
            Is.True);
    }

    [Test]
    public void RepresentationsAreSymmetricAndValvesAreStrictlyResolved()
    {
        ComponentTag[] tags =
            LoadPrefab().GetComponentsInChildren<ComponentTag>(true);

        foreach (IGrouping<EngineComponentId, ComponentTag> group in tags
                     .Where(tag => tag.Id != EngineComponentId.None)
                     .GroupBy(tag => tag.Id))
        {
            int expanded = group.Count(tag =>
                tag.Representation == EngineRepresentation.Expande);
            int running = group.Count(tag =>
                tag.Representation == EngineRepresentation.LigaDesliga);
            Assert.That(
                expanded,
                Is.EqualTo(running),
                $"Asymmetric identity: {group.Key}");
        }

        Assert.That(tags.Any(tag =>
            tag.Id == EngineComponentId.Valvula), Is.False);
        Assert.That(tags.Count(tag =>
            tag.Id == EngineComponentId.ValvulaAdmissao), Is.EqualTo(16));
        Assert.That(tags.Count(tag =>
            tag.Id == EngineComponentId.ValvulaEscape), Is.EqualTo(16));
        Assert.That(tags.Count(tag =>
            tag.Id == EngineComponentId.MolaValvula), Is.EqualTo(32));
        Assert.That(tags.Count(tag =>
            tag.Id == EngineComponentId.PratoMola), Is.EqualTo(32));
        Assert.That(tags.Where(tag =>
                tag.Id == EngineComponentId.ValvulaAdmissao)
            .All(tag => tag.DisplayName == "Válvula de Admissão"),
            Is.True);
        Assert.That(tags.Where(tag =>
                tag.Id == EngineComponentId.ValvulaEscape)
            .All(tag => tag.DisplayName == "Válvula de Escape"),
            Is.True);
        Assert.That(tags.Where(tag =>
                tag.Id == EngineComponentId.PratoMola)
            .All(tag => tag.DisplayName == "Prato da Mola"),
            Is.True);
    }

    [Test]
    public void MechanicalSubassembliesHaveDistinctAuditableIdentities()
    {
        ComponentTag[] tags =
            LoadPrefab().GetComponentsInChildren<ComponentTag>(true);

        Assert.That(tags.Count(tag =>
            tag.Id == EngineComponentId.Virabrequim), Is.EqualTo(2));
        Assert.That(tags.Count(tag =>
            tag.Id == EngineComponentId.EngrenagemVirabrequim), Is.EqualTo(2));
        Assert.That(tags.Where(tag =>
                tag.Id == EngineComponentId.EngrenagemVirabrequim)
            .All(tag => tag.DisplayName == "Engrenagem do Virabrequim"),
            Is.True);
        Assert.That(tags.Count(tag =>
            tag.Id == EngineComponentId.CaboVela &&
            tag.InstanceIndex == 0), Is.EqualTo(2));
        Assert.That(tags.Count(tag =>
            tag.Id == EngineComponentId.ConectorVela), Is.EqualTo(8));
        Assert.That(tags.Where(tag =>
                tag.Id == EngineComponentId.ConectorVela)
            .All(tag => tag.DisplayName == "Conector da Vela"),
            Is.True);

        foreach (EngineRepresentation representation in new[]
                 {
                     EngineRepresentation.Expande,
                     EngineRepresentation.LigaDesliga
                 })
        {
            int[] connectorIndices = tags
                .Where(tag =>
                    tag.Representation == representation &&
                    tag.Id == EngineComponentId.ConectorVela)
                .Select(tag => tag.InstanceIndex)
                .OrderBy(index => index)
                .ToArray();
            Assert.That(connectorIndices, Is.EqualTo(new[] { 1, 2, 3, 4 }));

            int[] camshaftIndices = tags
                .Where(tag =>
                    tag.Representation == representation &&
                    tag.Id == EngineComponentId.ArvoreDeCames)
                .Select(tag => tag.SubIndex)
                .OrderBy(index => index)
                .ToArray();
            Assert.That(camshaftIndices, Is.EqualTo(new[] { 1, 2 }));

            ComponentTag[] camshaftGears = tags
                .Where(tag =>
                    tag.Representation == representation &&
                    tag.Id == EngineComponentId.EngrenagemDeComando)
                .ToArray();
            Assert.That(camshaftGears, Has.Length.EqualTo(2));
            Assert.That(
                camshaftGears.Select(tag => tag.SubIndex)
                    .OrderBy(index => index)
                    .ToArray(),
                Is.EqualTo(new[] { 1, 2 }));
        }
    }

    [Test]
    public void SpringRetainersHaveReproducibleVisualAccessibilityCue()
    {
        GameObject prefab = LoadPrefab();
        ComponentTag[] retainers = prefab
            .GetComponentsInChildren<ComponentTag>(true)
            .Where(tag => tag.Id == EngineComponentId.PratoMola)
            .ToArray();

        Assert.That(retainers, Has.Length.EqualTo(32));
        Assert.That(
            retainers.All(tag =>
                tag.GetComponent<ComponentVisualAccessibility>() != null),
            Is.True);
        Assert.That(
            prefab.GetComponentsInChildren<ComponentVisualAccessibility>(
                true),
            Has.Length.EqualTo(32));
    }

    private static GameObject LoadPrefab()
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        return prefab;
    }
}
