using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DesktopExperimentController : MonoBehaviour
{
    private const int ExperimentWindowId = 617203;
    private const string ProtocolResourcePath = "Experiment/DefaultProtocol";

    private enum ExperimentState
    {
        Setup,
        TaskActive,
        TaskComplete,
        SessionComplete
    }

    private ExperimentProtocol protocol;
    private readonly List<ExperimentTaskDefinition> taskQueue = new List<ExperimentTaskDefinition>();
    private ExperimentState state = ExperimentState.Setup;
    private string participantId = string.Empty;
    private string feedback = "Informe um código pseudonimizado. Não digite nome, e-mail ou matrícula.";
    private string protocolError = string.Empty;
    private string sceneValidationError = string.Empty;
    private string currentExplanation = string.Empty;
    private string queriedComponentName = string.Empty;
    private float queriedComponentVisibleUntil;
    private Vector2 queriedComponentPosition;
    private int attempts;
    private int totalAttempts;
    private int completedTasks;
    private int skippedTasks;
    private int timedOutTasks;
    private int successfulTasks;
    private int identificationScore;
    private int preTestScore;
    private int preTestCompleted;
    private int postTestScore;
    private int postTestCompleted;
    private int currentTaskIndex;
    private int counterbalanceCell;
    private int sessionSeed;
    private int pendingOptionIndex = -1;
    private int alphaPreTestScore;
    private int alphaPostTestScore;
    private int betaPreTestScore;
    private int betaPostTestScore;
    private float alphaPreConfidence;
    private float alphaPostConfidence;
    private float betaPreConfidence;
    private float betaPostConfidence;
    private KnowledgeSessionPlan knowledgePlan;
    private long taskStartedAt;
    private long sessionDurationMilliseconds;
    private bool confirmEarlyExit;
    private bool confirmSkipTask;
    private bool taskTimeoutHandled;
    private Rect windowRect = new Rect(18f, 18f, 560f, 480f);
    private Vector2 windowScrollPosition;
    private CameraController experimentCameraController;
    private StateModelManager stimulusManager;
    private GameObject legacyCloseButton;
    private GUIStyle titleStyle;
    private GUIStyle wrappedLabelStyle;
    private GUIStyle promptStyle;
    private GUIStyle inputStyle;
    private GUIStyle buttonStyle;
    private GUIStyle windowStyle;
    private GUIStyle queryOverlayStyle;
    private GUIStyle successStyle;
    private GUIStyle warningStyle;

    private ExperimentTaskDefinition CurrentTask =>
        currentTaskIndex >= 0 && currentTaskIndex < taskQueue.Count ? taskQueue[currentTaskIndex] : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!string.Equals(SceneManager.GetActiveScene().name, "DesktopScene", StringComparison.Ordinal))
            return;

        if (FindObjectOfType<DesktopExperimentController>() != null)
            return;

        GameObject controller = new GameObject("Desktop Experiment Controller");
        DontDestroyOnLoad(controller);
        controller.AddComponent<DesktopExperimentController>();
    }

    private void Awake()
    {
        LoadProtocol();
        ValidateExperimentalScene();
        HideLegacyNavigationButtons();
        experimentCameraController = FindObjectOfType<CameraController>();
        stimulusManager = FindObjectOfType<StateModelManager>(true);
        legacyCloseButton = GameObject.Find("Close_Button");
    }

    private void OnEnable()
    {
        CameraController.ObjectSelected += HandleObjectSelected;
        CameraController.ObjectNameQueried += HandleObjectNameQueried;
    }

    private void OnDisable()
    {
        CameraController.ObjectSelected -= HandleObjectSelected;
        CameraController.ObjectNameQueried -= HandleObjectNameQueried;
        if (experimentCameraController != null)
            experimentCameraController.SetHorizontalComposition(0.5f);
    }

    private void OnApplicationQuit()
    {
        if (ExperimentEventRecorder.IsActive)
            ExperimentEventRecorder.EndSession("application_quit");
    }

    private void OnGUI()
    {
        EnsureStyles();
        float interfaceScale = Mathf.Clamp(Screen.height / 720f, 1f, 2f);
        float logicalScreenWidth = Screen.width / interfaceScale;
        float logicalScreenHeight = Screen.height / interfaceScale;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(interfaceScale, interfaceScale, 1f));

        windowRect.width = Mathf.Min(520f, logicalScreenWidth - 36f);
        windowRect.height = Mathf.Min(
            state == ExperimentState.Setup ? 360f : 540f,
            logicalScreenHeight - 36f);
        windowRect.x = Mathf.Clamp(windowRect.x, 18f, logicalScreenWidth - windowRect.width - 18f);
        windowRect.y = Mathf.Clamp(windowRect.y, 18f, logicalScreenHeight - windowRect.height - 18f);
        windowRect = GUI.Window(
            ExperimentWindowId,
            windowRect,
            DrawWindow,
            "MTRM — Modo experimental",
            windowStyle);

        UpdateCameraComposition();
        DrawQueriedComponentOverlay(logicalScreenWidth, logicalScreenHeight);
        GUI.matrix = previousMatrix;
    }

    private void DrawWindow(int id)
    {
        GUILayout.Space(4f);
        windowScrollPosition = GUILayout.BeginScrollView(
            windowScrollPosition,
            false,
            true);

        switch (state)
        {
            case ExperimentState.Setup:
                DrawSetup();
                break;
            case ExperimentState.TaskActive:
                DrawActiveTask();
                break;
            case ExperimentState.TaskComplete:
                DrawCompletedTask();
                break;
            case ExperimentState.SessionComplete:
                DrawCompletedSession();
                break;
        }

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 28f));
    }

    private void Update()
    {
        ExperimentTaskDefinition task = CurrentTask;
        if (taskTimeoutHandled ||
            state != ExperimentState.TaskActive ||
            task == null ||
            task.type != ExperimentTaskType.ComponentIdentification ||
            task.maximumDurationSeconds <= 0)
        {
            return;
        }

        long elapsed =
            ExperimentEventRecorder.ElapsedMilliseconds - taskStartedAt;
        if (elapsed >= task.maximumDurationSeconds * 1000L)
            TimeoutCurrentTask(elapsed);
    }

    private void UpdateCameraComposition()
    {
        if (experimentCameraController == null)
            experimentCameraController = FindObjectOfType<CameraController>();
        if (experimentCameraController == null)
            return;

        // Mantém a câmera em tela cheia e desloca a composição do alvo para a
        // área livre à direita. Assim não há imagem residual fora do viewport.
        float panelFraction =
            (windowRect.x + windowRect.width + 18f) /
            Mathf.Max(1f, Screen.width /
                Mathf.Clamp(Screen.height / 720f, 1f, 2f));
        experimentCameraController.SetHorizontalComposition(
            Mathf.Lerp(0.5f, 0.73f, Mathf.Clamp01(panelFraction * 1.8f)));
    }

    private void DrawSetup()
    {
        GUILayout.Label("Protocolo experimental configurável", titleStyle);

        if (!string.IsNullOrEmpty(protocolError))
        {
            GUILayout.Label(protocolError, warningStyle);
            GUILayout.Label(
                "Crie o asset pelo menu MTRM → Criar ou atualizar protocolo padrão.",
                wrappedLabelStyle);
            return;
        }

        if (!string.IsNullOrEmpty(sceneValidationError))
        {
            GUILayout.Label(
                "A sessão está bloqueada porque a cena experimental é inválida:",
                warningStyle);
            GUILayout.TextArea(
                sceneValidationError,
                wrappedLabelStyle,
                GUILayout.MinHeight(120f));
            return;
        }

        GUILayout.Label(
            $"{protocol.protocolId} • versão {protocol.version} • {taskQueue.Count} tarefas",
            wrappedLabelStyle);
        GUILayout.Label(protocol.participantInstructions, wrappedLabelStyle);
        GUILayout.Space(12f);
        GUILayout.Label("1. Digite abaixo o código pseudonimizado do participante:", promptStyle);
        participantId = GUILayout.TextField(
            participantId,
            32,
            inputStyle,
            GUILayout.Height(46f));
        GUILayout.Label($"Condição: {protocol.condition}");
        GUILayout.Label("Célula de contrabalanceamento (0–3):");
        counterbalanceCell = GUILayout.SelectionGrid(
            counterbalanceCell,
            new[]
            {
                "0: α A→B",
                "1: α B→A",
                "2: β A→B",
                "3: β B→A"
            },
            4,
            buttonStyle);
        GUILayout.Space(12f);

        if (GUILayout.Button("2. Iniciar sessão", buttonStyle, GUILayout.Height(48f)))
            StartSession();

        GUILayout.Space(8f);
        GUILayout.Label(feedback, warningStyle);
    }

    private void DrawActiveTask()
    {
        ExperimentTaskDefinition task = CurrentTask;
        GUILayout.Label(
            $"{GetPhaseLabel(task.phase)} — etapa {currentTaskIndex + 1} de {taskQueue.Count}\n" +
            GetTaskTypeLabel(task.type),
            titleStyle);
        GUILayout.Label(task.prompt, promptStyle);
        GUILayout.Space(12f);

        if (task.type == ExperimentTaskType.ComponentIdentification)
        {
            GUILayout.Label(
                "COMO CONCLUIR: clique com o botão esquerdo na peça solicitada. " +
                "Um acerto conclui a tarefa automaticamente; depois use “Próxima tarefa”.\n" +
                "CONSULTAR NOME: clique com o botão direito sem arrastar. Para girar, arraste com o botão direito.",
                wrappedLabelStyle);
            GUILayout.Label($"Tentativas: {attempts}");
            GUILayout.Space(8f);
            string skipCaption = confirmSkipTask
                ? "Confirmar: registrar como não respondida"
                : "Pular esta tarefa";
            if (GUILayout.Button(
                    skipCaption,
                    buttonStyle,
                    GUILayout.Height(42f)))
            {
                if (confirmSkipTask)
                    SkipCurrentTask();
                else
                {
                    confirmSkipTask = true;
                    feedback =
                        "A tarefa será registrada como não respondida, sem " +
                        "acerto e sem acrescentar tentativa. Clique novamente " +
                        "para confirmar.";
                }
            }
        }
        else
        {
            DrawMultipleChoiceOptions(task);
        }

        GUILayout.Space(8f);
        GUILayout.Label(feedback, warningStyle);
        GUILayout.FlexibleSpace();

        string exitCaption = confirmEarlyExit
            ? "Confirmar encerramento sem concluir"
            : "Encerrar antes de concluir";
        if (GUILayout.Button(exitCaption, buttonStyle, GUILayout.Height(42f)))
        {
            if (confirmEarlyExit)
                FinishSession("cancelled_by_operator");
            else
            {
                confirmEarlyExit = true;
                feedback = "A sessão ainda não terminou. Clique novamente somente se deseja encerrá-la incompleta.";
            }
        }
    }

    private void DrawMultipleChoiceOptions(ExperimentTaskDefinition task)
    {
        if (pendingOptionIndex >= 0)
        {
            GUILayout.Label(
                "Qual é sua confiança nesta resposta?",
                promptStyle);
            string[] labels = { "1 — Chutei", "2", "3", "4 — Certeza" };
            for (int value = 1; value <= 4; value++)
            {
                int confidence = value;
                if (GUILayout.Button(labels[value - 1], buttonStyle))
                    SubmitKnowledgeConfidence(confidence);
            }
            return;
        }
        for (int index = 0; index < task.options.Count; index++)
        {
            int selectedIndex = index;
            string marker = ((char)('A' + index)).ToString();
            if (GUILayout.Button(
                    $"{marker}. {task.options[index]}",
                    buttonStyle,
                    GUILayout.Height(44f)))
                SubmitMultipleChoice(selectedIndex);
        }
    }

    private void DrawCompletedTask()
    {
        bool isLastTask = currentTaskIndex >= taskQueue.Count - 1;
        GUILayout.Label($"Tarefa {currentTaskIndex + 1} concluída", titleStyle);
        GUILayout.Label(feedback, successStyle);

        if (!string.IsNullOrWhiteSpace(currentExplanation))
            GUILayout.Label(currentExplanation, wrappedLabelStyle);

        GUILayout.Label($"Tentativas: {attempts}");
        GUILayout.Label(
            $"Tempo da tarefa: {(ExperimentEventRecorder.ElapsedMilliseconds - taskStartedAt) / 1000f:F1} s");
        if (CurrentTask.phase == ExperimentPhase.PreTest)
            GUILayout.Label("A pontuação do pré-teste será exibida somente ao final.");
        else
            GUILayout.Label(
                $"Acertos de identificação na primeira tentativa: " +
                $"{identificationScore}/{CountIdentificationTasks()}");
        GUILayout.Space(10f);

        if (isLastTask)
        {
            if (GUILayout.Button("Finalizar e salvar", buttonStyle, GUILayout.Height(48f)))
                FinishSession("completed");
        }
        else if (GUILayout.Button(GetNextButtonLabel(), buttonStyle, GUILayout.Height(48f)))
        {
            currentTaskIndex++;
            BeginCurrentTask();
        }
    }

    private void DrawCompletedSession()
    {
        float selectionPrecision = totalAttempts > 0
            ? (float)successfulTasks / totalAttempts * 100f
            : 0f;
        int identificationTaskCount = CountIdentificationTasks();
        float scorePercentage = identificationTaskCount > 0
            ? (float)identificationScore / identificationTaskCount * 100f
            : 0f;

        GUILayout.Label("Sessão salva", titleStyle);
        GUILayout.Label(
            $"Tarefas concluídas: {completedTasks}/{taskQueue.Count}\n" +
            $"Tarefas puladas: {skippedTasks}\n" +
            $"Tarefas expiradas: {timedOutTasks}\n" +
            $"Respostas corretas: {successfulTasks}\n" +
            $"Identificação na primeira tentativa: " +
            $"{identificationScore}/{identificationTaskCount} " +
            $"({scorePercentage:F1}%)\n" +
            $"Pré-teste: {preTestScore}/{CountTasksInPhase(ExperimentPhase.PreTest)}\n" +
            $"Pós-teste: {postTestScore}/{CountTasksInPhase(ExperimentPhase.PostTest)}\n" +
            $"Ganho pré/pós: {postTestScore - preTestScore:+#;-#;0}\n" +
            $"Tentativas totais: {totalAttempts}\n" +
            $"Precisão geral: {selectionPrecision:F1}%\n" +
            $"Duração: {sessionDurationMilliseconds / 1000f:F1} s",
            wrappedLabelStyle);
        GUILayout.Label("Os dados foram gravados em:", successStyle);
        GUILayout.TextArea(ExperimentEventRecorder.OutputDirectory, GUILayout.Height(65f));
        GUILayout.Label("Arquivos: session.json, events.jsonl e events.csv", wrappedLabelStyle);
    }

    private void LoadProtocol()
    {
        protocol = Resources.Load<ExperimentProtocol>(ProtocolResourcePath);
        taskQueue.Clear();

        if (protocol == null)
        {
            protocolError = $"Protocolo não encontrado em Resources/{ProtocolResourcePath}.";
            return;
        }

        if (!protocol.IsValid(out protocolError))
            return;

        taskQueue.AddRange(
            KnowledgeSessionPlanner.Build(
                protocol.knowledgeItemBank,
                protocol.tasks,
                counterbalanceCell,
                1).tasks);
    }

    private void StartSession()
    {
        ValidateExperimentalScene();
        if (!CanStartSession(
                protocol,
                protocolError,
                sceneValidationError,
                out string validationError))
        {
            feedback = validationError;
            return;
        }

        string protocolJson = ExperimentProtocolSnapshot.Serialize(protocol);
        string protocolHash =
            ExperimentProtocolSnapshot.ComputeHash(protocolJson);
        sessionSeed = StableSeed(participantId, protocol.knowledgeItemBank.bankVersion);
        knowledgePlan = KnowledgeSessionPlanner.Build(
            protocol.knowledgeItemBank,
            protocol.tasks,
            counterbalanceCell,
            sessionSeed);
        taskQueue.Clear();
        taskQueue.AddRange(knowledgePlan.tasks);
        if (!ExperimentEventRecorder.StartSession(
                participantId,
                protocol.condition,
                out string error,
                protocol.protocolId,
                protocol.version,
                protocolHash,
                protocolJson,
                sessionSeed,
                counterbalanceCell,
                knowledgePlan.selectedBlock.ToString(),
                knowledgePlan.preForm.ToString(),
                knowledgePlan.postForm.ToString(),
                knowledgePlan.alphaPreForm.ToString(),
                knowledgePlan.alphaPostForm.ToString(),
                knowledgePlan.betaPreForm.ToString(),
                knowledgePlan.betaPostForm.ToString()))
        {
            feedback = error;
            return;
        }

        totalAttempts = 0;
        completedTasks = 0;
        skippedTasks = 0;
        timedOutTasks = 0;
        successfulTasks = 0;
        identificationScore = 0;
        preTestScore = 0;
        preTestCompleted = 0;
        postTestScore = 0;
        postTestCompleted = 0;
        alphaPreTestScore = alphaPostTestScore = 0;
        betaPreTestScore = betaPostTestScore = 0;
        alphaPreConfidence = alphaPostConfidence = 0f;
        betaPreConfidence = betaPostConfidence = 0f;
        currentTaskIndex = 0;
        ExperimentEventRecorder.Record(
            "protocol_loaded",
            new ExperimentEventData
            {
                objectId = protocol.protocolId,
                textValue = protocol.version,
                phase = "setup"
            });
        BeginCurrentTask();
    }

    private void BeginCurrentTask()
    {
        ExperimentTaskDefinition task = CurrentTask;
        ApplyStimulusAccess(task.phase);
        ExperimentEventRecorder.CurrentPhase = task.phase.ToString();
        attempts = 0;
        pendingOptionIndex = -1;
        confirmEarlyExit = false;
        confirmSkipTask = false;
        taskTimeoutHandled = false;
        taskStartedAt = ExperimentEventRecorder.ElapsedMilliseconds;
        currentExplanation = string.Empty;
        feedback = "Tarefa iniciada.";
        state = ExperimentState.TaskActive;
        ExperimentEventRecorder.Record(
            "task_started",
            TaskEvent(task, new ExperimentEventData
            {
                componentId = task.type ==
                              ExperimentTaskType.ComponentIdentification
                    ? task.acceptedComponents[0]
                    : EngineComponentId.None,
                textValue = task.prompt,
                phase = task.phase.ToString(),
                taskId = task.id
            }));
        if (task.knowledgeItem != null)
        {
            ExperimentEventRecorder.Record(
                "item_presented",
                TaskEvent(task, new ExperimentEventData
                {
                    textValue = task.prompt,
                    phase = task.phase.ToString(),
                    taskId = task.id
                }));
        }
    }

    private void ApplyStimulusAccess(ExperimentPhase phase)
    {
        bool available =
            phase != ExperimentPhase.PreTest &&
            phase != ExperimentPhase.PostTest;
        if (stimulusManager == null)
            stimulusManager = FindObjectOfType<StateModelManager>(true);
        if (stimulusManager != null)
            stimulusManager.SetExperimentAccess(available);
        if (experimentCameraController == null)
            experimentCameraController = FindObjectOfType<CameraController>(true);
        if (experimentCameraController != null)
            experimentCameraController.enabled = available;
        if (legacyCloseButton != null)
            legacyCloseButton.SetActive(available);
    }

    private void HandleObjectSelected(Transform selected)
    {
        ExperimentTaskDefinition task = CurrentTask;
        if (state != ExperimentState.TaskActive ||
            task == null ||
            task.type != ExperimentTaskType.ComponentIdentification ||
            selected == null)
        {
            return;
        }

        string hierarchyPath = BuildHierarchyPath(selected);
        ComponentTag tag = selected.GetComponentInParent<ComponentTag>();
        ComponentSelectionDecision decision =
            ComponentSelectionEvaluator.Evaluate(task, tag);
        if (!decision.IsValidTarget)
        {
            ExperimentEventRecorder.Record(
                "component_selected",
                SelectionEvent(
                    hierarchyPath,
                    tag,
                    task,
                    false,
                    false,
                    0,
                    null));
            feedback = "Objeto sem identidade experimental; a tentativa não foi contada.";
            return;
        }

        attempts++;
        totalAttempts++;
        bool correct = decision.IsCorrect == true;
        ExperimentEventRecorder.Record(
            "component_selected",
            SelectionEvent(
                hierarchyPath,
                tag,
                task,
                true,
                decision.IsNamedDistractor,
                attempts,
                correct));

        if (correct)
        {
            CompleteCurrentTask(true, attempts == 1);
            return;
        }

        bool attemptsExhausted = task.maximumAttempts > 0 && attempts >= task.maximumAttempts;
        if (!task.allowRetry || attemptsExhausted)
        {
            CompleteCurrentTask(false, false);
        }
        else
        {
            feedback = $"“{selected.name}” não corresponde ao componente solicitado. Tente novamente.";
        }
    }

    private void HandleObjectNameQueried(Transform selected, string displayName)
    {
        if (state != ExperimentState.TaskActive || selected == null)
            return;

        ExperimentTaskDefinition task = CurrentTask;
        queriedComponentName = displayName;
        queriedComponentVisibleUntil = Time.unscaledTime + 3f;
        float interfaceScale = Mathf.Clamp(Screen.height / 720f, 1f, 2f);
        queriedComponentPosition = new Vector2(
            Input.mousePosition.x / interfaceScale,
            (Screen.height - Input.mousePosition.y) / interfaceScale);

        ExperimentEventRecorder.Record(
            "component_name_queried",
            new ExperimentEventData
            {
                objectId = BuildHierarchyPath(selected),
                componentId = selected
                    .GetComponentInParent<ComponentTag>()?.Id ??
                    EngineComponentId.None,
                textValue = displayName,
                phase = task != null
                    ? task.phase.ToString()
                    : "interaction",
                taskId = task != null ? task.id : string.Empty
            });
    }

    private void DrawQueriedComponentOverlay(float logicalWidth, float logicalHeight)
    {
        if (Time.unscaledTime > queriedComponentVisibleUntil ||
            string.IsNullOrWhiteSpace(queriedComponentName))
        {
            return;
        }

        const float overlayWidth = 300f;
        const float overlayHeight = 76f;
        float x = Mathf.Clamp(
            queriedComponentPosition.x + 18f,
            12f,
            logicalWidth - overlayWidth - 12f);
        float y = Mathf.Clamp(
            queriedComponentPosition.y + 18f,
            12f,
            logicalHeight - overlayHeight - 12f);

        GUI.Box(
            new Rect(x, y, overlayWidth, overlayHeight),
            $"Peça consultada:\n{queriedComponentName}",
            queryOverlayStyle);
    }

    private void SubmitMultipleChoice(int selectedIndex)
    {
        ExperimentTaskDefinition task = CurrentTask;
        if (state != ExperimentState.TaskActive ||
            task == null ||
            task.type != ExperimentTaskType.MultipleChoice)
        {
            return;
        }

        if (task.knowledgeItem != null)
        {
            pendingOptionIndex = selectedIndex;
            feedback = "Resposta selecionada. Informe sua confiança.";
            return;
        }
        SubmitMultipleChoiceFinal(selectedIndex, -1f);
    }

    private void SubmitKnowledgeConfidence(int confidence)
    {
        SubmitMultipleChoiceFinal(pendingOptionIndex, confidence);
        pendingOptionIndex = -1;
    }

    private void SubmitMultipleChoiceFinal(
        int displayedIndex,
        float confidence)
    {
        ExperimentTaskDefinition task = CurrentTask;
        int originalIndex = task.knowledgeItem != null
            ? KnowledgeSessionPlanner.ToOriginalIndex(task, displayedIndex)
            : displayedIndex;
        attempts = 1;
        totalAttempts++;
        bool correct = originalIndex ==
            (task.knowledgeItem != null
                ? task.originalCorrectOptionIndex
                : task.correctOptionIndex);
        string selectedText = task.options[displayedIndex];

        ExperimentEventRecorder.Record(
            "answer_submitted",
            TaskEvent(task, new ExperimentEventData
            {
                objectId = task.id,
                optionIndex = originalIndex,
                textValue = selectedText,
                confidence = confidence,
                rtMs = ExperimentEventRecorder.ElapsedMilliseconds - taskStartedAt,
                phase = task.phase.ToString(),
                attempt = attempts,
                isCorrect = correct,
                taskId = task.id
            }));

        CompleteCurrentTask(correct, correct, confidence);
    }

    private void CompleteCurrentTask(
        bool correct,
        bool awardedPoint,
        float confidence = -1f)
    {
        ExperimentTaskDefinition task = CurrentTask;
        long duration = ExperimentEventRecorder.ElapsedMilliseconds - taskStartedAt;
        completedTasks++;

        if (correct)
            successfulTasks++;
        if (awardedPoint &&
            task.type == ExperimentTaskType.ComponentIdentification)
        {
            identificationScore++;
        }

        if (task.phase == ExperimentPhase.PreTest)
        {
            preTestCompleted++;
            if (correct)
                preTestScore++;
            AccumulateKnowledge(task, correct, confidence, true);
            currentExplanation = string.Empty;
            feedback = "Resposta registrada. O resultado será apresentado somente ao final.";
        }
        else
        {
            currentExplanation = task.explanation;
            feedback = correct
                ? "Resposta correta."
                : "Resposta registrada como incorreta.";

            if (task.phase == ExperimentPhase.PostTest)
            {
                postTestCompleted++;
                if (correct)
                    postTestScore++;
                AccumulateKnowledge(task, correct, confidence, false);
            }
        }

        ExperimentEventRecorder.Record(
            "task_finished",
            new ExperimentEventData
            {
                componentId = task.type ==
                              ExperimentTaskType.ComponentIdentification
                    ? task.acceptedComponents[0]
                    : EngineComponentId.None,
                durationMs = duration,
                numericValue = awardedPoint ? 1d : 0d,
                phase = task.phase.ToString(),
                attempt = attempts,
                isCorrect = correct,
                taskId = task.id
            });

        state = ExperimentState.TaskComplete;
    }

    private void SkipCurrentTask()
    {
        ExperimentTaskDefinition task = CurrentTask;
        if (state != ExperimentState.TaskActive ||
            task == null ||
            task.type != ExperimentTaskType.ComponentIdentification)
        {
            return;
        }

        long duration =
            ExperimentEventRecorder.ElapsedMilliseconds - taskStartedAt;
        skippedTasks++;
        completedTasks++;
        currentExplanation = string.Empty;
        feedback =
            "Tarefa registrada como não respondida. Nenhuma tentativa foi " +
            "acrescentada.";
        ExperimentEventRecorder.Record(
            "task_skipped",
            new ExperimentEventData
            {
                componentId = task.acceptedComponents[0],
                durationMs = duration,
                textValue = "participant_skipped",
                phase = task.phase.ToString(),
                attempt = attempts,
                taskId = task.id
            });
        ExperimentEventRecorder.Record(
            "task_finished",
            new ExperimentEventData
            {
                componentId = task.acceptedComponents[0],
                durationMs = duration,
                textValue = "skipped",
                phase = task.phase.ToString(),
                attempt = attempts,
                taskId = task.id
            });
        state = ExperimentState.TaskComplete;
    }

    private void TimeoutCurrentTask(long duration)
    {
        ExperimentTaskDefinition task = CurrentTask;
        if (taskTimeoutHandled ||
            state != ExperimentState.TaskActive ||
            task == null)
        {
            return;
        }

        taskTimeoutHandled = true;
        timedOutTasks++;
        completedTasks++;
        currentExplanation = string.Empty;
        feedback =
            "Tempo máximo atingido. A tarefa foi encerrada sem acrescentar " +
            "tentativa.";
        ExperimentEventRecorder.Record(
            "task_timeout",
            new ExperimentEventData
            {
                componentId = task.acceptedComponents[0],
                durationMs = duration,
                textValue = "maximum_duration_reached",
                phase = task.phase.ToString(),
                attempt = attempts,
                taskId = task.id
            });
        ExperimentEventRecorder.Record(
            "task_finished",
            new ExperimentEventData
            {
                componentId = task.acceptedComponents[0],
                durationMs = duration,
                textValue = "timeout",
                phase = task.phase.ToString(),
                attempt = attempts,
                taskId = task.id
            });
        state = ExperimentState.TaskComplete;
    }

    private void FinishSession(string reason)
    {
        string outputDirectory = ExperimentEventRecorder.OutputDirectory;
        sessionDurationMilliseconds = ExperimentEventRecorder.ElapsedMilliseconds;
        ExperimentEventRecorder.EndSession(
            reason,
            new ExperimentSessionSummary
            {
                completedTasks = completedTasks,
                taskCount = taskQueue.Count,
                skippedTasks = skippedTasks,
                timedOutTasks = timedOutTasks,
                identificationScore = identificationScore,
                totalAttempts = totalAttempts,
                preTestScore = preTestScore,
                preTestCompleted = preTestCompleted,
                postTestScore = postTestScore,
                postTestCompleted = postTestCompleted,
                gain = postTestScore - preTestScore,
                alphaPreTestScore = alphaPreTestScore,
                alphaPostTestScore = alphaPostTestScore,
                betaPreTestScore = betaPreTestScore,
                betaPostTestScore = betaPostTestScore,
                confidenceWeightedGain =
                    (alphaPostConfidence + betaPostConfidence) -
                    (alphaPreConfidence + betaPreConfidence),
                durationMs = sessionDurationMilliseconds
            });
        feedback = outputDirectory;
        state = ExperimentState.SessionComplete;
    }

    private static ExperimentEventData SelectionEvent(
        string hierarchyPath,
        ComponentTag tag,
        ExperimentTaskDefinition task,
        bool validTarget,
        bool namedDistractor,
        int attempt,
        bool? correct)
    {
        return new ExperimentEventData
        {
            objectId = hierarchyPath,
            componentId = tag != null ? tag.Id : EngineComponentId.None,
            instanceIndex = tag != null ? tag.InstanceIndex : 0,
            subIndex = tag != null ? tag.SubIndex : 0,
            representation = tag != null
                ? tag.Representation
                : default,
            isValidTarget = validTarget,
            isNamedDistractor = namedDistractor,
            attempt = attempt,
            isCorrect = correct,
            phase = task.phase.ToString(),
            taskId = task.id
        };
    }

    private static ExperimentEventData TaskEvent(
        ExperimentTaskDefinition task,
        ExperimentEventData data)
    {
        if (task.knowledgeItem == null)
            return data;
        data.itemId = task.knowledgeItem.id;
        data.conceptKey = task.knowledgeItem.conceptKey;
        data.form = task.knowledgeItem.form.ToString();
        data.optionPermutation = string.Join(",", task.optionPermutation);
        return data;
    }

    private void AccumulateKnowledge(
        ExperimentTaskDefinition task,
        bool correct,
        float confidence,
        bool preTest)
    {
        if (task.knowledgeItem == null)
            return;
        float weighted = correct ? confidence / 4f : 0f;
        if (task.knowledgeItem.contentBlock == ContentBlock.Alpha)
        {
            if (preTest)
            {
                if (correct) alphaPreTestScore++;
                alphaPreConfidence += weighted;
            }
            else
            {
                if (correct) alphaPostTestScore++;
                alphaPostConfidence += weighted;
            }
        }
        else if (preTest)
        {
            if (correct) betaPreTestScore++;
            betaPreConfidence += weighted;
        }
        else
        {
            if (correct) betaPostTestScore++;
            betaPostConfidence += weighted;
        }
    }

    private static int StableSeed(string participant, string bankVersion)
    {
        unchecked
        {
            int hash = 17;
            string value = participant + "|" + bankVersion;
            for (int index = 0; index < value.Length; index++)
                hash = hash * 31 + value[index];
            return hash & int.MaxValue;
        }
    }

    private static string BuildHierarchyPath(Transform selected)
    {
        StringBuilder path = new StringBuilder(selected.name);
        Transform current = selected.parent;
        while (current != null)
        {
            path.Insert(0, current.name + "/");
            current = current.parent;
        }
        return path.ToString();
    }

    private static string GetTaskTypeLabel(ExperimentTaskType type)
    {
        return type == ExperimentTaskType.ComponentIdentification
            ? "Identificação"
            : "Compreensão funcional";
    }

    private static string GetPhaseLabel(ExperimentPhase phase)
    {
        switch (phase)
        {
            case ExperimentPhase.PreTest:
                return "Pré-teste";
            case ExperimentPhase.PostTest:
                return "Pós-teste";
            case ExperimentPhase.Training:
                return "Treinamento";
            case ExperimentPhase.Questionnaire:
                return "Questionário";
            default:
                return "Exploração do motor";
        }
    }

    private string GetNextButtonLabel()
    {
        ExperimentTaskDefinition nextTask = currentTaskIndex + 1 < taskQueue.Count
            ? taskQueue[currentTaskIndex + 1]
            : null;

        if (nextTask == null || CurrentTask == null || nextTask.phase == CurrentTask.phase)
            return "Próxima etapa";

        return $"Iniciar {GetPhaseLabel(nextTask.phase)}";
    }

    private int CountTasksInPhase(ExperimentPhase phase)
    {
        int count = 0;
        for (int index = 0; index < taskQueue.Count; index++)
        {
            if (taskQueue[index].phase == phase)
                count++;
        }
        return count;
    }

    private int CountIdentificationTasks()
    {
        int count = 0;
        for (int index = 0; index < taskQueue.Count; index++)
        {
            if (taskQueue[index].type ==
                ExperimentTaskType.ComponentIdentification)
            {
                count++;
            }
        }
        return count;
    }

    private void ValidateExperimentalScene()
    {
        if (protocol == null)
        {
            sceneValidationError = string.Empty;
            return;
        }

        ExperimentalSceneValidationResult result =
            ExperimentalSceneValidator.ValidateActiveScene(protocol);
        sceneValidationError = result.IsValid
            ? string.Empty
            : result.Message;
    }

    public static bool CanStartSession(
        ExperimentProtocol candidateProtocol,
        string candidateProtocolError,
        string candidateSceneValidationError,
        out string error)
    {
        if (candidateProtocol == null ||
            !string.IsNullOrEmpty(candidateProtocolError))
        {
            error = string.IsNullOrEmpty(candidateProtocolError)
                ? "Protocolo experimental não carregado."
                : candidateProtocolError;
            return false;
        }

        if (!string.IsNullOrEmpty(candidateSceneValidationError))
        {
            error =
                "Sessão bloqueada: a cena experimental é inválida." +
                Environment.NewLine +
                candidateSceneValidationError;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static void HideLegacyNavigationButtons()
    {
        string[] legacyObjectNames = { "Tutorial_button", "About_button" };
        foreach (string objectName in legacyObjectNames)
        {
            GameObject legacyButton = GameObject.Find(objectName);
            if (legacyButton != null)
                legacyButton.SetActive(false);
        }
    }

    private static void Shuffle<T>(IList<T> items)
    {
        for (int index = items.Count - 1; index > 0; index--)
        {
            int swapIndex = UnityEngine.Random.Range(0, index + 1);
            T temporary = items[index];
            items[index] = items[swapIndex];
            items[swapIndex] = temporary;
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        wrappedLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            wordWrap = true
        };
        promptStyle = new GUIStyle(wrappedLabelStyle)
        {
            fontSize = 19,
            fontStyle = FontStyle.Bold
        };
        inputStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 22,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(12, 12, 6, 6)
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 17,
            wordWrap = true
        };
        windowStyle = new GUIStyle(GUI.skin.window)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        queryOverlayStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 19,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            padding = new RectOffset(12, 12, 8, 8)
        };
        successStyle = new GUIStyle(wrappedLabelStyle);
        successStyle.normal.textColor = new Color(0.15f, 0.65f, 0.25f);
        warningStyle = new GUIStyle(wrappedLabelStyle);
        warningStyle.normal.textColor = new Color(0.95f, 0.65f, 0.15f);
    }
}
