using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ExperimentalSceneValidatorTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in created)
        {
            if (item != null)
                Object.DestroyImmediate(item);
        }
        created.Clear();
    }

    [Test]
    public void FailsWhenProtocolComponentIsMissing()
    {
        Baseline(
            out ExperimentProtocol protocol,
            out List<ComponentTag> tags,
            out List<Transform> interactive);
        protocol.tasks[0].acceptedComponents[0] = EngineComponentId.Biela;

        ExperimentalSceneValidationResult result =
            ExperimentalSceneValidator.Validate(protocol, tags, interactive);

        AssertError(result, "PROTOCOL_COMPONENT_MISSING");
    }

    [Test]
    public void FailsWhenInteractiveObjectHasNoTag()
    {
        Baseline(
            out ExperimentProtocol protocol,
            out List<ComponentTag> tags,
            out List<Transform> interactive);
        interactive.Add(CreateObject("sem-etiqueta").transform);

        ExperimentalSceneValidationResult result =
            ExperimentalSceneValidator.Validate(protocol, tags, interactive);

        AssertError(result, "INTERACTIVE_WITHOUT_TAG");
    }

    [Test]
    public void FailsWhenIdentifiedTagHasNoDisplayName()
    {
        Baseline(
            out ExperimentProtocol protocol,
            out List<ComponentTag> tags,
            out List<Transform> interactive);
        SetField(tags[0], "displayName", "");

        ExperimentalSceneValidationResult result =
            ExperimentalSceneValidator.Validate(protocol, tags, interactive);

        AssertError(result, "TAG_WITHOUT_DISPLAY_NAME");
    }

    [Test]
    public void FailsWhenIntentionalExclusionHasNoJustification()
    {
        Baseline(
            out ExperimentProtocol protocol,
            out List<ComponentTag> tags,
            out List<Transform> interactive);
        ComponentTag exclusion = CreateTag(
            "exclusão",
            EngineComponentId.None,
            EngineRepresentation.Expande,
            "",
            true,
            "");
        tags.Add(exclusion);
        interactive.Add(exclusion.transform);

        ExperimentalSceneValidationResult result =
            ExperimentalSceneValidator.Validate(protocol, tags, interactive);

        AssertError(result, "UNIDENTIFIED_WITHOUT_JUSTIFICATION");
    }

    [Test]
    public void FailsWhenRepresentationCountsDiffer()
    {
        Baseline(
            out ExperimentProtocol protocol,
            out List<ComponentTag> tags,
            out List<Transform> interactive);
        ComponentTag extra = CreateTag(
            "pistão-extra",
            EngineComponentId.Pistao,
            EngineRepresentation.Expande,
            "Pistão");
        tags.Add(extra);
        interactive.Add(extra.transform);

        ExperimentalSceneValidationResult result =
            ExperimentalSceneValidator.Validate(protocol, tags, interactive);

        AssertError(result, "REPRESENTATION_COUNT_MISMATCH");
    }

    [Test]
    public void FailsWhenSameComponentIdHasDifferentDisplayNames()
    {
        Baseline(
            out ExperimentProtocol protocol,
            out List<ComponentTag> tags,
            out List<Transform> interactive);
        SetField(tags[1], "displayName", "Nome divergente");

        ExperimentalSceneValidationResult result =
            ExperimentalSceneValidator.Validate(protocol, tags, interactive);

        AssertError(result, "COMPONENT_DISPLAY_NAME_MISMATCH");
    }

    [Test]
    public void ValidBaselinePassesAndAllowsSessionStart()
    {
        Baseline(
            out ExperimentProtocol protocol,
            out List<ComponentTag> tags,
            out List<Transform> interactive);

        ExperimentalSceneValidationResult result =
            ExperimentalSceneValidator.Validate(protocol, tags, interactive);
        bool canStart = DesktopExperimentController.CanStartSession(
            protocol,
            "",
            result.IsValid ? "" : result.Message,
            out string error);

        Assert.That(result.IsValid, Is.True, result.Message);
        Assert.That(canStart, Is.True, error);
    }

    [Test]
    public void InvalidSceneBlocksSessionStart()
    {
        ExperimentProtocol protocol = CreateProtocol();

        bool canStart = DesktopExperimentController.CanStartSession(
            protocol,
            "",
            "INTERACTIVE_WITHOUT_TAG: motor/mesh",
            out string error);

        Assert.That(canStart, Is.False);
        Assert.That(error, Does.Contain("Sessão bloqueada"));
        Assert.That(error, Does.Contain("INTERACTIVE_WITHOUT_TAG"));
    }

    private void Baseline(
        out ExperimentProtocol protocol,
        out List<ComponentTag> tags,
        out List<Transform> interactive)
    {
        protocol = CreateProtocol();
        ComponentTag expanded = CreateTag(
            "pistão-expandido",
            EngineComponentId.Pistao,
            EngineRepresentation.Expande,
            "Pistão");
        ComponentTag running = CreateTag(
            "pistão-funcionando",
            EngineComponentId.Pistao,
            EngineRepresentation.LigaDesliga,
            "Pistão");
        tags = new List<ComponentTag> { expanded, running };
        interactive = new List<Transform>
        {
            expanded.transform,
            running.transform
        };
    }

    private ExperimentProtocol CreateProtocol()
    {
        ExperimentProtocol protocol =
            ScriptableObject.CreateInstance<ExperimentProtocol>();
        created.Add(protocol);
        protocol.tasks = new List<ExperimentTaskDefinition>
        {
            new ExperimentTaskDefinition
            {
                id = "T01",
                prompt = "Selecione o pistão",
                acceptedComponents = new List<EngineComponentId>
                {
                    EngineComponentId.Pistao
                }
            }
        };
        return protocol;
    }

    private ComponentTag CreateTag(
        string name,
        EngineComponentId id,
        EngineRepresentation representation,
        string displayName,
        bool intentionallyUnidentified = false,
        string justification = "não se aplica")
    {
        GameObject gameObject = CreateObject(name);
        ComponentTag tag = gameObject.AddComponent<ComponentTag>();
        SetField(tag, "id", id);
        SetField(tag, "representation", representation);
        SetField(tag, "displayName", displayName);
        SetField(
            tag,
            "intentionallyUnidentified",
            intentionallyUnidentified);
        SetField(tag, "justification", justification);
        return tag;
    }

    private GameObject CreateObject(string name)
    {
        var gameObject = new GameObject(name);
        created.Add(gameObject);
        return gameObject;
    }

    private static void SetField(
        ComponentTag tag,
        string fieldName,
        object value)
    {
        typeof(ComponentTag)
            .GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(tag, value);
    }

    private static void AssertError(
        ExperimentalSceneValidationResult result,
        string code)
    {
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Message, Does.Contain(code));
    }
}
