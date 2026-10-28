using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class Pr3cIntegrationPlayModeTests
{
    [UnityTest]
    public IEnumerator CompleteSessionProducesValidatedSchemaV2Artifacts()
    {
        SceneManager.LoadScene("DesktopScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        DesktopExperimentController controller =
            Object.FindObjectOfType<DesktopExperimentController>();
        if (controller == null)
        {
            controller = new GameObject("PR3C Integration Controller")
                .AddComponent<DesktopExperimentController>();
            yield return null;
        }
        Assert.That(controller, Is.Not.Null);
        SetField(controller, "participantId", "P_PR3C_INTEGRATION");
        Invoke(controller, "StartSession");
        Assert.That(ExperimentEventRecorder.IsActive, Is.True);
        StateModelManager stimulus =
            Object.FindObjectOfType<StateModelManager>(true);
        Assert.That(stimulus, Is.Not.Null);
        Assert.That(stimulus.gameObject.activeSelf, Is.False);

        List<ExperimentTaskDefinition> tasks =
            (List<ExperimentTaskDefinition>)GetField(controller, "taskQueue");
        bool invalidClicked = false;
        bool springClicked = false;
        bool eventsContainRpm = false;
        for (int index = 0; index < tasks.Count; index++)
        {
            ExperimentTaskDefinition task = tasks[index];
            if (task.type == ExperimentTaskType.ComponentIdentification)
            {
                Assert.That(stimulus.gameObject.activeSelf, Is.True);
                if (!eventsContainRpm)
                {
                    MotorController motor =
                        Object.FindObjectOfType<MotorController>(true);
                    Assert.That(motor, Is.Not.Null);
                    motor.DefinirRPM(2000f);
                    yield return null;
                    eventsContainRpm = true;
                }
                if (!invalidClicked)
                {
                    ComponentTag invalid = AllTags()
                        .First(tag => tag.Id == EngineComponentId.None);
                    ActivateHierarchy(invalid.transform);
                    Invoke(controller, "HandleObjectSelected", invalid.transform);
                    invalidClicked = true;
                }

                if (!springClicked &&
                    task.rejectedComponents.Contains(
                        EngineComponentId.MolaValvula))
                {
                    ComponentTag spring = AllTags()
                        .First(tag =>
                            tag.Id == EngineComponentId.MolaValvula &&
                            tag.gameObject.activeInHierarchy);
                    Invoke(controller, "HandleObjectSelected", spring.transform);
                    springClicked = true;
                }

                ComponentTag correct = AllTags()
                    .First(tag =>
                        task.acceptedComponents.Contains(tag.Id) &&
                        tag.gameObject.activeInHierarchy);
                Invoke(controller, "HandleObjectSelected", correct.transform);
            }
            else
            {
                if (task.phase == ExperimentPhase.PostTest)
                    Assert.That(stimulus.gameObject.activeSelf, Is.False);
                Invoke(
                    controller,
                    "SubmitMultipleChoice",
                    task.correctOptionIndex);
                if (task.knowledgeItem != null)
                {
                    Invoke(
                        controller,
                        "SubmitKnowledgeConfidence",
                        4);
                }
            }

            if (index < tasks.Count - 1)
            {
                SetField(controller, "currentTaskIndex", index + 1);
                Invoke(controller, "BeginCurrentTask");
            }
        }

        string output = ExperimentEventRecorder.OutputDirectory;
        Invoke(controller, "FinishSession", "completed");
        yield return null;

        string sessionPath = Path.Combine(output, "session.json");
        string eventsPath = Path.Combine(output, "events.jsonl");
        string csvPath = Path.Combine(output, "events.csv");
        string snapshotPath = Path.Combine(
            output,
            "protocol_snapshot.json");
        Assert.That(File.Exists(sessionPath), Is.True);
        Assert.That(File.Exists(eventsPath), Is.True);
        Assert.That(File.Exists(csvPath), Is.True);
        Assert.That(File.Exists(snapshotPath), Is.True);

        string sessionJson = File.ReadAllText(sessionPath);
        ExperimentSessionMetadata metadata =
            JsonUtility.FromJson<ExperimentSessionMetadata>(sessionJson);
        string snapshotJson = File.ReadAllText(snapshotPath);
        Assert.That(metadata.schemaVersion, Is.EqualTo(5));
        Assert.That(metadata.protocolVersion, Is.EqualTo("4.0.0"));
        Assert.That(metadata.selectedContentBlock, Is.EqualTo("Alpha"));
        Assert.That(metadata.preForm, Is.EqualTo("A"));
        Assert.That(metadata.postForm, Is.EqualTo("B"));
        Assert.That(metadata.alphaPreForm, Is.EqualTo("A"));
        Assert.That(metadata.alphaPostForm, Is.EqualTo("B"));
        Assert.That(metadata.betaPreForm, Is.EqualTo("A"));
        Assert.That(metadata.betaPostForm, Is.EqualTo("B"));
        Assert.That(
            metadata.protocolHash,
            Is.EqualTo(ExperimentProtocolSnapshot.ComputeHash(snapshotJson)));
        Assert.That(sessionJson, Does.Contain("\"identificationScore\""));
        Assert.That(sessionJson, Does.Not.Contain("\"score\""));

        byte[] csv = File.ReadAllBytes(csvPath);
        Assert.That(csv.Take(3), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));

        ExperimentEventRecord[] events = File.ReadAllLines(eventsPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(JsonUtility.FromJson<ExperimentEventRecord>)
            .ToArray();
        Assert.That(
            events.Any(item =>
                item.eventName == "rpm_changed" &&
                item.numericValue == 2000d),
            Is.True);
        Assert.That(
            events.Count(item => item.eventName == "item_presented"),
            Is.EqualTo(16));
        Assert.That(
            events.Where(item => item.eventName == "answer_submitted")
                .All(item =>
                    item.confidence == 4f &&
                    !string.IsNullOrEmpty(item.optionPermutation) &&
                    !string.IsNullOrEmpty(item.itemId)),
            Is.True);
        Assert.That(
            events.Any(item =>
                item.eventName == "component_selected" &&
                item.componentId == EngineComponentId.MolaValvula.ToString() &&
                item.isNamedDistractor == "true" &&
                item.isValidTarget == "true" &&
                item.attempt > 0),
            Is.True);
        Assert.That(
            events.Any(item =>
                item.eventName == "component_selected" &&
                item.isValidTarget == "false" &&
                item.isCorrect == "" &&
                item.attempt == 0),
            Is.True);
    }

    [UnityTest]
    public IEnumerator InverseCounterbalanceCellProducesBPreAndAPost()
    {
        SceneManager.LoadScene("DesktopScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        DesktopExperimentController controller =
            Object.FindObjectOfType<DesktopExperimentController>();
        if (controller == null)
        {
            controller = new GameObject("PR4 Inverse Controller")
                .AddComponent<DesktopExperimentController>();
            yield return null;
        }
        SetField(controller, "participantId", "P_PR4_INVERSE");
        SetField(controller, "counterbalanceCell", 3);
        Invoke(controller, "StartSession");
        Assert.That(ExperimentEventRecorder.IsActive, Is.True);

        List<ExperimentTaskDefinition> tasks =
            (List<ExperimentTaskDefinition>)GetField(controller, "taskQueue");
        for (int index = 0; index < tasks.Count; index++)
        {
            ExperimentTaskDefinition task = tasks[index];
            if (task.type == ExperimentTaskType.ComponentIdentification)
            {
                ComponentTag correct = AllTags().First(tag =>
                    task.acceptedComponents.Contains(tag.Id) &&
                    tag.gameObject.activeInHierarchy);
                Invoke(controller, "HandleObjectSelected", correct.transform);
            }
            else
            {
                Invoke(controller, "SubmitMultipleChoice",
                    task.correctOptionIndex);
                if (task.knowledgeItem != null)
                    Invoke(controller, "SubmitKnowledgeConfidence", 3);
            }
            if (index < tasks.Count - 1)
            {
                SetField(controller, "currentTaskIndex", index + 1);
                Invoke(controller, "BeginCurrentTask");
            }
        }

        string output = ExperimentEventRecorder.OutputDirectory;
        Invoke(controller, "FinishSession", "completed");
        yield return null;
        ExperimentSessionMetadata metadata =
            JsonUtility.FromJson<ExperimentSessionMetadata>(
                File.ReadAllText(Path.Combine(output, "session.json")));
        Assert.That(metadata.selectedContentBlock, Is.EqualTo("Beta"));
        Assert.That(metadata.preForm, Is.EqualTo("B"));
        Assert.That(metadata.postForm, Is.EqualTo("A"));
        Assert.That(metadata.alphaPreForm, Is.EqualTo("A"));
        Assert.That(metadata.alphaPostForm, Is.EqualTo("B"));
        Assert.That(metadata.betaPreForm, Is.EqualTo("B"));
        Assert.That(metadata.betaPostForm, Is.EqualTo("A"));
        Assert.That(metadata.randomSeed, Is.Not.EqualTo(0));
        Assert.That(metadata.summary.alphaPreTestScore, Is.EqualTo(0));
        Assert.That(metadata.summary.alphaPostTestScore, Is.EqualTo(0));
        Assert.That(metadata.summary.betaPreTestScore, Is.EqualTo(8));
        Assert.That(metadata.summary.betaPostTestScore, Is.EqualTo(8));
    }

    private static ComponentTag[] AllTags() =>
        Object.FindObjectsOfType<ComponentTag>(true);

    private static void ActivateHierarchy(Transform transform)
    {
        while (transform != null)
        {
            transform.gameObject.SetActive(true);
            transform = transform.parent;
        }
    }

    private static object GetField(object target, string name) =>
        target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(target);

    private static void SetField(
        object target,
        string name,
        object value)
    {
        target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }

    private static object Invoke(
        object target,
        string name,
        params object[] arguments)
    {
        return target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(target, arguments);
    }
}
