using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class ExperimentEventData
{
    public string objectId = "";
    public EngineComponentId componentId = EngineComponentId.None;
    public int instanceIndex;
    public int subIndex;
    public EngineRepresentation? representation;
    public bool? isValidTarget;
    public bool? isNamedDistractor;
    public long? durationMs;
    public long? rtMs;
    public double? numericValue;
    public string textValue = "";
    public int optionIndex = -1;
    public float? confidence;
    public int attempt;
    public bool? isCorrect;
    public string phase = "interaction";
    public string taskId = "";
    public string itemId = "";
    public string conceptKey = "";
    public string form = "";
    public string optionPermutation = "";
}

[Serializable]
public sealed class ExperimentEventRecord
{
    public int schemaVersion;
    public string protocolHash;
    public string timestampUtc;
    public long elapsedMilliseconds;
    public string participantId;
    public string sessionId;
    public string condition;
    public string phase;
    public string taskId;
    public string itemId;
    public string conceptKey;
    public string form;
    public string optionPermutation;
    public string eventName;
    public string objectId;
    public string componentId;
    public int componentIdValue;
    public int instanceIndex;
    public int subIndex;
    public string representation;
    public string isValidTarget;
    public string isNamedDistractor;
    public long durationMs;
    public long rtMs;
    public double numericValue;
    public string textValue;
    public int optionIndex;
    public float confidence;
    public int attempt;
    public string isCorrect;
}

[Serializable]
public sealed class ExperimentSessionSummary
{
    public int completedTasks;
    public int taskCount;
    public int skippedTasks;
    public int timedOutTasks;
    public int identificationScore;
    public int totalAttempts;
    public int preTestScore;
    public int preTestCompleted;
    public int postTestScore;
    public int postTestCompleted;
    public int gain;
    public int alphaPreTestScore;
    public int alphaPostTestScore;
    public int betaPreTestScore;
    public int betaPostTestScore;
    public float confidenceWeightedGain;
    public long durationMs;
}

[Serializable]
public sealed class ExperimentSessionMetadata
{
    public int schemaVersion;
    public string protocolHash;
    public string participantId;
    public string sessionId;
    public string condition;
    public string protocolId;
    public string protocolVersion;
    public string startedAtUtc;
    public string finishedAtUtc;
    public string applicationVersion;
    public string unityVersion;
    public string outputDirectory;
    public int randomSeed;
    public int counterbalanceCell;
    public string selectedContentBlock;
    public string preForm;
    public string postForm;
    public string alphaPreForm;
    public string alphaPostForm;
    public string betaPreForm;
    public string betaPostForm;
    public ExperimentSessionSummary summary;
}

public static class ExperimentEventRecorder
{
    public const int SchemaVersion = 5;
    private const string CsvHeader =
        "schema_version,protocol_hash,timestamp_utc,elapsed_ms," +
        "participant_id,session_id,condition,phase,task_id,item_id," +
        "concept_key,form,option_permutation,event,object_id," +
        "component_id,component_id_value,instance_index,sub_index," +
        "representation,is_valid_target,is_named_distractor,duration_ms," +
        "rt_ms,numeric_value,text_value,option_index,confidence,attempt," +
        "is_correct";

    private static readonly Stopwatch SessionClock = new Stopwatch();
    private static ExperimentSessionMetadata metadata;
    private static string jsonlPath;
    private static string csvPath;
    private static bool active;

    public static bool IsActive => active;
    public static string CurrentPhase { get; set; } = "setup";
    public static string SessionId =>
        metadata != null ? metadata.sessionId : string.Empty;
    public static string OutputDirectory =>
        metadata != null ? metadata.outputDirectory : string.Empty;
    public static long ElapsedMilliseconds =>
        SessionClock.IsRunning ? SessionClock.ElapsedMilliseconds : 0L;

    public static bool StartSession(
        string participantId,
        string condition,
        out string error,
        string protocolId,
        string protocolVersion,
        string protocolHash,
        string protocolSnapshotJson,
        int randomSeed = 0,
        int counterbalanceCell = 0,
        string selectedContentBlock = "",
        string preForm = "",
        string postForm = "",
        string alphaPreForm = "",
        string alphaPostForm = "",
        string betaPreForm = "",
        string betaPostForm = "")
    {
        error = string.Empty;
        if (active)
        {
            error = "Já existe uma sessão experimental ativa.";
            return false;
        }
        participantId = SanitizeIdentifier(participantId);
        if (participantId.Length < 2)
        {
            error = "Informe um código com pelo menos dois caracteres.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(protocolHash) ||
            string.IsNullOrWhiteSpace(protocolSnapshotJson))
        {
            error = "O snapshot e o hash do protocolo são obrigatórios.";
            return false;
        }

        condition = SanitizeIdentifier(condition);
        string timestamp = DateTime.UtcNow.ToString(
            "yyyyMMdd'T'HHmmss'Z'",
            CultureInfo.InvariantCulture);
        string sessionId = $"{participantId}_{condition}_{timestamp}";
        string outputDirectory = ExperimentSessionPath.Resolve(
            Application.persistentDataPath,
            sessionId);
        try
        {
            Directory.CreateDirectory(outputDirectory);
            jsonlPath = Path.Combine(outputDirectory, "events.jsonl");
            csvPath = Path.Combine(outputDirectory, "events.csv");
            metadata = new ExperimentSessionMetadata
            {
                schemaVersion = SchemaVersion,
                protocolHash = protocolHash,
                participantId = participantId,
                sessionId = sessionId,
                condition = condition,
                protocolId = protocolId ?? string.Empty,
                protocolVersion = protocolVersion ?? string.Empty,
                startedAtUtc = DateTime.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture),
                finishedAtUtc = string.Empty,
                applicationVersion = Application.version,
                unityVersion = Application.unityVersion,
                outputDirectory = outputDirectory,
                randomSeed = randomSeed,
                counterbalanceCell = counterbalanceCell,
                selectedContentBlock = selectedContentBlock,
                preForm = preForm,
                postForm = postForm,
                alphaPreForm = alphaPreForm,
                alphaPostForm = alphaPostForm,
                betaPreForm = betaPreForm,
                betaPostForm = betaPostForm,
                summary = new ExperimentSessionSummary()
            };
            File.WriteAllText(
                Path.Combine(outputDirectory, "protocol_snapshot.json"),
                protocolSnapshotJson,
                new UTF8Encoding(false));
            File.WriteAllText(
                csvPath,
                CsvHeader + Environment.NewLine,
                new UTF8Encoding(true));
            SessionClock.Restart();
            active = true;
            SaveMetadata();
            Record("session_started", new ExperimentEventData
            {
                phase = "setup"
            });
            return true;
        }
        catch (Exception exception)
        {
            active = false;
            metadata = null;
            error = "Não foi possível criar os arquivos da sessão: " +
                    exception.Message;
            UnityEngine.Debug.LogException(exception);
            return false;
        }
    }

    public static void Record(
        string eventName,
        ExperimentEventData data = null)
    {
        if (!active || metadata == null)
            return;
        data ??= new ExperimentEventData();
        var record = new ExperimentEventRecord
        {
            schemaVersion = SchemaVersion,
            protocolHash = metadata.protocolHash,
            timestampUtc = DateTime.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture),
            elapsedMilliseconds = SessionClock.ElapsedMilliseconds,
            participantId = metadata.participantId,
            sessionId = metadata.sessionId,
            condition = metadata.condition,
            phase = string.IsNullOrWhiteSpace(data.phase) ||
                    data.phase == "interaction"
                ? CurrentPhase
                : data.phase,
            taskId = data.taskId ?? string.Empty,
            itemId = data.itemId ?? string.Empty,
            conceptKey = data.conceptKey ?? string.Empty,
            form = data.form ?? string.Empty,
            optionPermutation = data.optionPermutation ?? string.Empty,
            eventName = eventName ?? string.Empty,
            objectId = data.objectId ?? string.Empty,
            componentId = data.componentId.ToString(),
            componentIdValue = (int)data.componentId,
            instanceIndex = data.instanceIndex,
            subIndex = data.subIndex,
            representation = data.representation.HasValue
                ? data.representation.Value.ToString()
                : string.Empty,
            isValidTarget = NullableBool(data.isValidTarget),
            isNamedDistractor = NullableBool(data.isNamedDistractor),
            durationMs = data.durationMs ?? -1,
            rtMs = data.rtMs ?? -1,
            numericValue = data.numericValue ?? -1d,
            textValue = data.textValue ?? string.Empty,
            optionIndex = data.optionIndex,
            confidence = data.confidence ?? -1f,
            attempt = data.attempt,
            isCorrect = NullableBool(data.isCorrect)
        };
        try
        {
            File.AppendAllText(
                jsonlPath,
                JsonUtility.ToJson(record) + Environment.NewLine,
                new UTF8Encoding(false));
            File.AppendAllText(
                csvPath,
                ToCsv(record) + Environment.NewLine,
                new UTF8Encoding(false));
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError(
                "Falha ao registrar evento experimental: " +
                exception.Message);
        }
    }

    public static void EndSession(
        string reason = "completed",
        ExperimentSessionSummary summary = null)
    {
        if (!active || metadata == null)
            return;
        Record("session_finished", new ExperimentEventData
        {
            phase = "finish",
            textValue = reason
        });
        metadata.finishedAtUtc = DateTime.UtcNow.ToString(
            "O",
            CultureInfo.InvariantCulture);
        metadata.summary = summary ?? new ExperimentSessionSummary();
        SaveMetadata();
        SessionClock.Stop();
        active = false;
    }

    private static void SaveMetadata()
    {
        if (metadata == null)
            return;
        File.WriteAllText(
            Path.Combine(metadata.outputDirectory, "session.json"),
            JsonUtility.ToJson(metadata, true),
            new UTF8Encoding(false));
    }

    private static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var sanitized = new StringBuilder();
        foreach (char character in value.Trim())
        {
            if (char.IsLetterOrDigit(character) ||
                character == '-' ||
                character == '_')
            {
                sanitized.Append(character);
            }
        }
        return sanitized.Length > 32
            ? sanitized.ToString(0, 32)
            : sanitized.ToString();
    }

    private static string NullableBool(bool? value)
    {
        return value.HasValue
            ? value.Value.ToString().ToLowerInvariant()
            : string.Empty;
    }

    private static string ToCsv(ExperimentEventRecord record)
    {
        return string.Join(",",
            record.schemaVersion.ToString(CultureInfo.InvariantCulture),
            Csv(record.protocolHash),
            Csv(record.timestampUtc),
            record.elapsedMilliseconds.ToString(CultureInfo.InvariantCulture),
            Csv(record.participantId),
            Csv(record.sessionId),
            Csv(record.condition),
            Csv(record.phase),
            Csv(record.taskId),
            Csv(record.itemId),
            Csv(record.conceptKey),
            Csv(record.form),
            Csv(record.optionPermutation),
            Csv(record.eventName),
            Csv(record.objectId),
            Csv(record.componentId),
            record.componentIdValue.ToString(CultureInfo.InvariantCulture),
            record.instanceIndex.ToString(CultureInfo.InvariantCulture),
            record.subIndex.ToString(CultureInfo.InvariantCulture),
            Csv(record.representation),
            record.isValidTarget,
            record.isNamedDistractor,
            record.durationMs.ToString(CultureInfo.InvariantCulture),
            record.rtMs.ToString(CultureInfo.InvariantCulture),
            record.numericValue.ToString("R", CultureInfo.InvariantCulture),
            Csv(record.textValue),
            record.optionIndex.ToString(CultureInfo.InvariantCulture),
            record.confidence.ToString("R", CultureInfo.InvariantCulture),
            record.attempt.ToString(CultureInfo.InvariantCulture),
            record.isCorrect);
    }

    private static string Csv(string value)
    {
        string safe = value ?? string.Empty;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
