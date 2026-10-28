using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ExperimentProtocolTests
{
    private const string DefaultProtocolPath = "Assets/Resources/Experiment/DefaultProtocol.asset";

    [Test]
    public void DefaultProtocol_IsValid()
    {
        ExperimentProtocol protocol = AssetDatabase.LoadAssetAtPath<ExperimentProtocol>(DefaultProtocolPath);

        Assert.That(protocol, Is.Not.Null);
        Assert.That(protocol.IsValid(out string error), Is.True, error);
    }

    [Test]
    public void IsValid_RejectsProtocolWithoutTasks()
    {
        ExperimentProtocol protocol = CreateProtocol();
        protocol.tasks.Clear();

        Assert.That(protocol.IsValid(out _), Is.False);
    }

    [Test]
    public void IsValid_RejectsDuplicateTaskId()
    {
        ExperimentProtocol protocol = CreateProtocol();
        protocol.tasks.Add(CreateMultipleChoiceTask("T01"));

        Assert.That(protocol.IsValid(out _), Is.False);
    }

    [Test]
    public void IsValid_RejectsTaskWithoutPrompt()
    {
        ExperimentProtocol protocol = CreateProtocol();
        protocol.tasks[0].prompt = "";

        Assert.That(protocol.IsValid(out _), Is.False);
    }

    [Test]
    public void IsValid_RejectsCorrectOptionOutsideOptions()
    {
        ExperimentProtocol protocol = CreateProtocol();
        protocol.tasks[0].correctOptionIndex = 2;

        Assert.That(protocol.IsValid(out _), Is.False);
    }

    [Test]
    public void DefaultProtocol_HasExpectedTaskCountPerPhase()
    {
        ExperimentProtocol protocol = AssetDatabase.LoadAssetAtPath<ExperimentProtocol>(DefaultProtocolPath);
        KnowledgeSessionPlan plan = KnowledgeSessionPlanner.Build(
            protocol.knowledgeItemBank,
            protocol.tasks,
            0,
            20260728);
        Dictionary<ExperimentPhase, int> counts = new Dictionary<ExperimentPhase, int>();
        foreach (ExperimentTaskDefinition task in plan.tasks)
            counts[task.phase] = counts.TryGetValue(task.phase, out int count) ? count + 1 : 1;

        Assert.That(protocol.version, Is.EqualTo("4.0.0"));
        Assert.That(counts[ExperimentPhase.PreTest], Is.EqualTo(8));
        Assert.That(counts[ExperimentPhase.MainTasks], Is.EqualTo(4));
        Assert.That(counts[ExperimentPhase.PostTest], Is.EqualTo(8));
        Assert.That(plan.tasks.Count, Is.EqualTo(20));
        Assert.That(
            protocol.tasks
                .FindAll(task =>
                    task.type == ExperimentTaskType.ComponentIdentification)
                .TrueForAll(task => task.maximumDurationSeconds == 120),
            Is.True);
    }

    [Test]
    public void AlphaThirdIdentificationUsesVisibleTimingBelt()
    {
        ExperimentProtocol protocol =
            AssetDatabase.LoadAssetAtPath<ExperimentProtocol>(
                DefaultProtocolPath);
        ExperimentTaskDefinition task =
            protocol.tasks.Find(item => item.id == "ALPHA-T03");

        Assert.That(task, Is.Not.Null);
        Assert.That(
            task.acceptedComponents,
            Is.EqualTo(new[] { EngineComponentId.CorreiaDentada }));
        Assert.That(
            protocol.tasks.Exists(item =>
                item.acceptedComponents.Contains(
                    EngineComponentId.PratoMola)),
            Is.False);
    }

    private static ExperimentProtocol CreateProtocol()
    {
        ExperimentProtocol protocol = ScriptableObject.CreateInstance<ExperimentProtocol>();
        protocol.knowledgeItemBank =
            AssetDatabase.LoadAssetAtPath<KnowledgeItemBank>(
                "Assets/Resources/Experiment/DefaultKnowledgeItemBank.asset");
        protocol.tasks = new List<ExperimentTaskDefinition> { CreateMultipleChoiceTask("T01") };
        return protocol;
    }

    private static ExperimentTaskDefinition CreateMultipleChoiceTask(string id)
    {
        return new ExperimentTaskDefinition
        {
            id = id,
            type = ExperimentTaskType.MultipleChoice,
            prompt = "Enunciado",
            options = new List<string> { "A", "B" },
            correctOptionIndex = 1
        };
    }
}
