using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class KnowledgeItemBankAssetCreator
{
    public const string AssetPath =
        "Assets/Resources/Experiment/DefaultKnowledgeItemBank.asset";
    private const string CsvPath = "../docs/knowledge_items.csv";

    [MenuItem("MTRM/Importar banco de itens revisado")]
    public static void CreateOrUpdate()
    {
        KnowledgeItemBank bank =
            AssetDatabase.LoadAssetAtPath<KnowledgeItemBank>(AssetPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<KnowledgeItemBank>();
            AssetDatabase.CreateAsset(bank, AssetPath);
        }

        bank.bankVersion = "1.0.0";
        bank.items = Import(File.ReadAllText(CsvPath, Encoding.UTF8));
        if (!bank.IsValid(out string error))
            throw new InvalidDataException(error);
        EditorUtility.SetDirty(bank);

        ExperimentProtocol protocol =
            AssetDatabase.LoadAssetAtPath<ExperimentProtocol>(
                ExperimentProtocolAssetCreator.AssetPath);
        protocol.knowledgeItemBank = bank;
        EditorUtility.SetDirty(protocol);
        AssetDatabase.SaveAssets();
        Debug.Log($"Banco importado: {bank.items.Count} itens, versão {bank.bankVersion}.");
    }

    public static void CreateFromCommandLine()
    {
        try
        {
            CreateOrUpdate();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static List<KnowledgeItem> Import(string csv)
    {
        List<string[]> rows = ParseCsv(csv);
        if (rows.Count != 33)
            throw new InvalidDataException(
                $"Esperadas 32 linhas de itens; encontradas {rows.Count - 1}.");

        List<KnowledgeItem> items = new List<KnowledgeItem>();
        for (int row = 1; row < rows.Count; row++)
        {
            string[] value = rows[row];
            if (value.Length != 13)
                throw new InvalidDataException(
                    $"Linha {row + 1}: esperadas 13 colunas; encontradas {value.Length}.");
            items.Add(new KnowledgeItem
            {
                id = value[0],
                contentBlock = ParseEnum<ContentBlock>(value[1], row),
                form = ParseEnum<ItemForm>(value[2], row),
                conceptKey = value[3],
                stem = value[4],
                options = new List<string>
                {
                    value[5], value[6], value[7], value[8]
                },
                correctOptionIndex = int.Parse(
                    value[9], CultureInfo.InvariantCulture),
                explanation = value[10],
                difficultyEstimate = float.Parse(
                    value[11], CultureInfo.InvariantCulture)
            });
        }
        return items;
    }

    private static T ParseEnum<T>(string value, int row) where T : struct
    {
        if (Enum.TryParse(value, true, out T parsed))
            return parsed;
        throw new InvalidDataException(
            $"Linha {row + 1}: valor inválido para {typeof(T).Name}: {value}.");
    }

    private static List<string[]> ParseCsv(string source)
    {
        source = source.TrimStart('\uFEFF');
        List<string[]> rows = new List<string[]>();
        List<string> row = new List<string>();
        StringBuilder field = new StringBuilder();
        bool quoted = false;
        for (int index = 0; index < source.Length; index++)
        {
            char character = source[index];
            if (character == '"')
            {
                if (quoted && index + 1 < source.Length &&
                    source[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                row.Add(field.ToString());
                field.Length = 0;
            }
            else if ((character == '\r' || character == '\n') && !quoted)
            {
                if (character == '\r' && index + 1 < source.Length &&
                    source[index + 1] == '\n')
                    index++;
                row.Add(field.ToString());
                field.Length = 0;
                if (row.Count > 1 || row[0].Length > 0)
                    rows.Add(row.ToArray());
                row = new List<string>();
            }
            else
            {
                field.Append(character);
            }
        }
        row.Add(field.ToString());
        if (row.Count > 1 || row[0].Length > 0)
            rows.Add(row.ToArray());
        return rows;
    }
}
