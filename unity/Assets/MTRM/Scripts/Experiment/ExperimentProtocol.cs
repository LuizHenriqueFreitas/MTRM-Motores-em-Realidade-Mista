using System;
using System.Collections.Generic;
using UnityEngine;

public enum ExperimentTaskType
{
    ComponentIdentification,
    MultipleChoice
}

public enum ExperimentPhase
{
    Training,
    MainTasks,
    PreTest,
    PostTest,
    Questionnaire
}

public readonly struct ComponentSelectionDecision
{
    public ComponentSelectionDecision(
        bool isValidTarget,
        bool countsAttempt,
        bool? isCorrect,
        bool isNamedDistractor)
    {
        IsValidTarget = isValidTarget;
        CountsAttempt = countsAttempt;
        IsCorrect = isCorrect;
        IsNamedDistractor = isNamedDistractor;
    }

    public bool IsValidTarget { get; }
    public bool CountsAttempt { get; }
    public bool? IsCorrect { get; }
    public bool IsNamedDistractor { get; }
}

public static class ComponentSelectionEvaluator
{
    public static ComponentSelectionDecision Evaluate(
        ExperimentTaskDefinition task,
        ComponentTag tag)
    {
        if (task == null ||
            tag == null ||
            tag.Id == EngineComponentId.None ||
            tag.IntentionallyUnidentified)
        {
            return new ComponentSelectionDecision(
                false,
                false,
                null,
                false);
        }

        bool correct = task.EvaluateComponent(
            tag.Id,
            out bool namedDistractor);
        return new ComponentSelectionDecision(
            true,
            true,
            correct,
            namedDistractor);
    }
}

[Serializable]
public sealed class ExperimentTaskDefinition
{
    public string id = "T01";
    public ExperimentTaskType type = ExperimentTaskType.ComponentIdentification;
    public ExperimentPhase phase = ExperimentPhase.MainTasks;
    public ContentBlock contentBlock = ContentBlock.Alpha;
    [TextArea(2, 5)] public string prompt;

    [Header("Identificação de componente")]
    public List<EngineComponentId> acceptedComponents =
        new List<EngineComponentId>();
    public List<EngineComponentId> rejectedComponents =
        new List<EngineComponentId>();
    public bool allowRetry = true;
    [Min(0)] public int maximumAttempts;
    [Min(0)] public int maximumDurationSeconds = 120;

    [Header("Múltipla escolha")]
    public List<string> options = new List<string>();
    [Min(0)] public int correctOptionIndex;
    [TextArea(2, 4)] public string explanation;

    [NonSerialized] public KnowledgeItem knowledgeItem;
    [NonSerialized] public List<int> optionPermutation = new List<int>();
    [NonSerialized] public int originalCorrectOptionIndex = -1;

    public bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "Uma tarefa está sem ID.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            error = $"A tarefa {id} está sem enunciado.";
            return false;
        }

        if (type == ExperimentTaskType.ComponentIdentification)
        {
            if (acceptedComponents == null || acceptedComponents.Count == 0)
            {
                error = $"A tarefa {id} precisa de componente aceito.";
                return false;
            }
            if (acceptedComponents.Contains(EngineComponentId.None) ||
                (rejectedComponents != null &&
                 rejectedComponents.Contains(EngineComponentId.None)))
            {
                error = $"A tarefa {id} não pode referenciar o componente None.";
                return false;
            }
            if (maximumDurationSeconds <= 0)
            {
                error =
                    $"A tarefa {id} precisa de duração máxima positiva.";
                return false;
            }
        }
        else if (options.Count < 2 || correctOptionIndex < 0 || correctOptionIndex >= options.Count)
        {
            error = $"A tarefa {id} possui alternativas ou resposta correta inválidas.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool EvaluateComponent(
        EngineComponentId componentId,
        out bool isNamedDistractor)
    {
        isNamedDistractor =
            rejectedComponents != null &&
            rejectedComponents.Contains(componentId);
        return acceptedComponents != null &&
               acceptedComponents.Contains(componentId);
    }
}

[CreateAssetMenu(fileName = "ExperimentProtocol", menuName = "MTRM/Protocolo experimental")]
public sealed class ExperimentProtocol : ScriptableObject
{
    public string protocolId = "mtrm_desktop";
    public string version = "2.0.0";
    public string condition = "desktop_3d";
    [TextArea(2, 5)] public string participantInstructions;
    public bool randomizeMainTasks;
    public KnowledgeItemBank knowledgeItemBank;
    public List<ExperimentTaskDefinition> tasks = new List<ExperimentTaskDefinition>();

    public bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(protocolId) || string.IsNullOrWhiteSpace(version))
        {
            error = "O protocolo precisa de ID e versão.";
            return false;
        }

        if (tasks == null || tasks.Count == 0)
        {
            error = "O protocolo não possui tarefas.";
            return false;
        }

        if (!KnowledgeItemBankValidator.IsValid(knowledgeItemBank, out error))
            return false;

        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ExperimentTaskDefinition task in tasks)
        {
            if (task == null)
            {
                error = "O protocolo contém uma tarefa nula.";
                return false;
            }

            if (!task.IsValid(out error))
                return false;

            if (!ids.Add(task.id))
            {
                error = $"ID de tarefa duplicado: {task.id}.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
