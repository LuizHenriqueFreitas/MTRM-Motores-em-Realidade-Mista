using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ComponentNameBaselineTests
{
    [Test]
    public void ValveTask_RejectsMolaAsNamedDistractor()
    {
        var task = ValveTask();

        bool correct = task.EvaluateComponent(
            EngineComponentId.MolaValvula,
            out bool namedDistractor);

        Assert.That(correct, Is.False);
        Assert.That(namedDistractor, Is.True);
    }

    [Test]
    public void ValidationDependsOnIdentityNotObjectName()
    {
        var task = ValveTask();

        bool firstResult = task.EvaluateComponent(
            EngineComponentId.ValvulaAdmissao,
            out bool firstDistractor);
        bool renamedObjectResult = task.EvaluateComponent(
            EngineComponentId.ValvulaAdmissao,
            out bool renamedObjectDistractor);

        Assert.That(firstResult, Is.True);
        Assert.That(renamedObjectResult, Is.EqualTo(firstResult));
        Assert.That(firstDistractor, Is.False);
        Assert.That(renamedObjectDistractor, Is.False);
    }

    [Test]
    public void RightClickNameUsesComponentTagInsteadOfLegacyTooltip()
    {
        var gameObject = new GameObject("baseValvula");
        try
        {
            ComponentTag tag = gameObject.AddComponent<ComponentTag>();
            SetField(tag, "displayName", "Válvula de Escape");
            ToolTipTrigger tooltip = gameObject.AddComponent<ToolTipTrigger>();
            typeof(ToolTipTrigger)
                .GetField(
                    "titleText",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(tooltip, "Base da Valvula");

            string result = (string)typeof(CameraController)
                .GetMethod(
                    "GetDisplayName",
                    BindingFlags.Static | BindingFlags.NonPublic)
                ?.Invoke(null, new object[] { gameObject.transform });

            Assert.That(result, Is.EqualTo("Válvula de Escape"));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void LeftClickTooltipUsesComponentTagInsteadOfLegacyText()
    {
        var gameObject = new GameObject("cabecaValvula");
        try
        {
            ComponentTag tag = gameObject.AddComponent<ComponentTag>();
            SetField(tag, "displayName", "Prato de Mola");
            SetField(
                tag,
                "educationalDescription",
                "Descrição física revisada.");
            ToolTipTrigger tooltip = gameObject.AddComponent<ToolTipTrigger>();
            typeof(ToolTipTrigger)
                .GetField(
                    "titleText",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(tooltip, "Cabeça de Válvula");

            Assert.That(tooltip.EffectiveTitle, Is.EqualTo("Prato de Mola"));
            Assert.That(
                tooltip.EffectiveDescription,
                Is.EqualTo("Descrição física revisada."));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ObjectWithoutTagIsInvalidAndDoesNotCountAttempt()
    {
        ComponentSelectionDecision decision =
            ComponentSelectionEvaluator.Evaluate(ValveTask(), null);

        Assert.That(decision.IsValidTarget, Is.False);
        Assert.That(decision.CountsAttempt, Is.False);
        Assert.That(decision.IsCorrect, Is.Null);
    }

    [Test]
    public void IntentionallyUnidentifiedObjectIsInvalidAndDoesNotCountAttempt()
    {
        GameObject gameObject = new GameObject("indeterminado");
        try
        {
            ComponentTag tag = gameObject.AddComponent<ComponentTag>();
            SetField(tag, "intentionallyUnidentified", true);

            ComponentSelectionDecision decision =
                ComponentSelectionEvaluator.Evaluate(ValveTask(), tag);

            Assert.That(decision.IsValidTarget, Is.False);
            Assert.That(decision.CountsAttempt, Is.False);
            Assert.That(decision.IsCorrect, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ValidationIgnoresRepresentationButPreservesIdentityFields()
    {
        ComponentSelectionDecision expanded = EvaluateValve(
            EngineRepresentation.Expande,
            1,
            1,
            out ComponentTag expandedTag,
            out GameObject expandedObject);
        ComponentSelectionDecision running = EvaluateValve(
            EngineRepresentation.LigaDesliga,
            4,
            2,
            out ComponentTag runningTag,
            out GameObject runningObject);
        try
        {
            Assert.That(expanded.IsCorrect, Is.True);
            Assert.That(running.IsCorrect, Is.True);
            Assert.That(expandedTag.InstanceIndex, Is.EqualTo(1));
            Assert.That(expandedTag.SubIndex, Is.EqualTo(1));
            Assert.That(
                expandedTag.Representation,
                Is.EqualTo(EngineRepresentation.Expande));
            Assert.That(runningTag.InstanceIndex, Is.EqualTo(4));
            Assert.That(runningTag.SubIndex, Is.EqualTo(2));
            Assert.That(
                runningTag.Representation,
                Is.EqualTo(EngineRepresentation.LigaDesliga));
        }
        finally
        {
            Object.DestroyImmediate(expandedObject);
            Object.DestroyImmediate(runningObject);
        }
    }

    private static ComponentSelectionDecision EvaluateValve(
        EngineRepresentation representation,
        int instanceIndex,
        int subIndex,
        out ComponentTag tag,
        out GameObject gameObject)
    {
        gameObject = new GameObject("nome irrelevante");
        tag = gameObject.AddComponent<ComponentTag>();
        SetField(tag, "id", EngineComponentId.ValvulaAdmissao);
        SetField(tag, "representation", representation);
        SetField(tag, "instanceIndex", instanceIndex);
        SetField(tag, "subIndex", subIndex);
        return ComponentSelectionEvaluator.Evaluate(ValveTask(), tag);
    }

    private static void SetField(
        ComponentTag tag,
        string name,
        object value)
    {
        typeof(ComponentTag)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(tag, value);
    }

    private static ExperimentTaskDefinition ValveTask()
    {
        return new ExperimentTaskDefinition
        {
            acceptedComponents = new List<EngineComponentId>
            {
                EngineComponentId.ValvulaAdmissao,
                EngineComponentId.ValvulaEscape
            },
            rejectedComponents = new List<EngineComponentId>
            {
                EngineComponentId.MolaValvula,
                EngineComponentId.PratoMola
            }
        };
    }
}
