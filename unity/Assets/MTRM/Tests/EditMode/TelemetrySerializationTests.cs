using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TelemetrySerializationTests
{
    [Test]
    public void EventRecord_JsonRoundTripPreservesEveryField()
    {
        int sceneCountBefore = SceneManager.sceneCount;
        ExperimentEventRecord source = new ExperimentEventRecord
        {
            schemaVersion = 5,
            protocolHash = "abcdef123456",
            timestampUtc = "2026-07-26T15:19:58.9236042Z",
            elapsedMilliseconds = 123456,
            participantId = "P003",
            sessionId = "sessão, três",
            condition = "desktop_3d",
            phase = "pré-teste",
            taskId = "T,01",
            itemId = "ALFA-A-01",
            conceptKey = "conceito",
            form = "A",
            optionPermutation = "2,0,3,1",
            eventName = "consulta_peça",
            objectId = "válvula; admissão",
            componentId = EngineComponentId.ValvulaAdmissao.ToString(),
            componentIdValue = (int)EngineComponentId.ValvulaAdmissao,
            instanceIndex = 2,
            subIndex = 1,
            representation = EngineRepresentation.Expande.ToString(),
            isValidTarget = "true",
            isNamedDistractor = "false",
            durationMs = 2500,
            rtMs = 800,
            numericValue = 1250.5,
            textValue = "acentuação, vírgulas e \"aspas\"",
            optionIndex = 2,
            confidence = 0.75f,
            attempt = 7,
            isCorrect = "true"
        };

        string json = JsonUtility.ToJson(source);
        ExperimentEventRecord restored = JsonUtility.FromJson<ExperimentEventRecord>(json);

        Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(source)));
        Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCountBefore), "O teste não deve abrir cena.");
    }

    [Test]
    public void CsvEscaping_PreservesThirtyColumnsWithQuotesCommaAndSemicolon()
    {
        int sceneCountBefore = SceneManager.sceneCount;
        ExperimentEventRecord record = new ExperimentEventRecord
        {
            timestampUtc = "2026-07-26T15:19:58Z",
            elapsedMilliseconds = 5,
            participantId = "P003",
            sessionId = "S",
            condition = "desktop",
            phase = "interaction",
            taskId = "T01",
            eventName = "query",
            objectId = "peça",
            componentId = EngineComponentId.ValvulaAdmissao.ToString(),
            componentIdValue = (int)EngineComponentId.ValvulaAdmissao,
            representation = EngineRepresentation.Expande.ToString(),
            textValue = "texto \"entre aspas\", com vírgula; e ponto e vírgula",
            attempt = 1,
            isCorrect = ""
        };

        MethodInfo toCsv = typeof(ExperimentEventRecorder).GetMethod(
            "ToCsv",
            BindingFlags.NonPublic | BindingFlags.Static);
        string line = (string)toCsv.Invoke(null, new object[] { record });
        IReadOnlyList<string> columns = ParseCsv(line);

        Assert.That(columns.Count, Is.EqualTo(30));
        Assert.That(columns[25], Is.EqualTo(record.textValue));
        Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCountBefore), "O teste não deve abrir cena.");
    }

    private static IReadOnlyList<string> ParseCsv(string line)
    {
        List<string> columns = new List<string>();
        System.Text.StringBuilder current = new System.Text.StringBuilder();
        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
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
                columns.Add(current.ToString());
                current.Length = 0;
            }
            else
            {
                current.Append(character);
            }
        }

        columns.Add(current.ToString());
        return columns;
    }
}
