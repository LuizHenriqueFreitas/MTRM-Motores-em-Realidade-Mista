using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ContentBlock { Alpha, Beta }
public enum ItemForm { A, B }

[Serializable]
public sealed class KnowledgeItem
{
    public string id;
    public ContentBlock contentBlock;
    public ItemForm form;
    public string conceptKey;
    [TextArea(2, 5)] public string stem;
    public List<string> options = new List<string>();
    public int correctOptionIndex;
    [TextArea(2, 4)] public string explanation;
    public float difficultyEstimate;
}

[CreateAssetMenu(
    fileName = "KnowledgeItemBank",
    menuName = "MTRM/Banco de itens")]
public sealed class KnowledgeItemBank : ScriptableObject
{
    public string bankVersion = "0.1.0";
    public List<KnowledgeItem> items = new List<KnowledgeItem>();

    public bool IsValid(out string error) =>
        KnowledgeItemBankValidator.IsValid(this, out error);
}

public static class KnowledgeItemBankValidator
{
    public static bool IsValid(KnowledgeItemBank bank, out string error)
    {
        if (bank == null)
            return Fail("O protocolo não referencia um banco de itens.", out error);
        if (string.IsNullOrWhiteSpace(bank.bankVersion))
            return Fail("O banco de itens está sem versão.", out error);
        if (bank.items == null)
            return Fail("A lista de itens é nula.", out error);

        HashSet<string> ids = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (KnowledgeItem item in bank.items)
        {
            if (item == null)
                return Fail("O banco contém um item nulo.", out error);
            if (string.IsNullOrWhiteSpace(item.id) || !ids.Add(item.id))
                return Fail($"ID ausente ou duplicado: {item.id}.", out error);
            if (string.IsNullOrWhiteSpace(item.conceptKey))
                return Fail($"O item {item.id} está sem conceptKey.", out error);
            if (string.IsNullOrWhiteSpace(item.stem))
                return Fail($"O item {item.id} está sem enunciado.", out error);
            if (item.options == null || item.options.Count < 3 ||
                item.correctOptionIndex < 0 ||
                item.correctOptionIndex >= item.options.Count)
                return Fail(
                    $"O item {item.id} possui alternativas ou resposta inválidas.",
                    out error);
        }

        foreach (ContentBlock block in Enum.GetValues(typeof(ContentBlock)))
        {
            List<IGrouping<string, KnowledgeItem>> concepts = bank.items
                .Where(item => item.contentBlock == block)
                .GroupBy(item => item.conceptKey, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (concepts.Count < 8)
                return Fail(
                    $"O bloco {block} possui {concepts.Count} conceitos; mínimo 8.",
                    out error);

            foreach (IGrouping<string, KnowledgeItem> concept in concepts)
            {
                KnowledgeItem[] pair = concept.ToArray();
                if (pair.Count(item => item.form == ItemForm.A) != 1 ||
                    pair.Count(item => item.form == ItemForm.B) != 1 ||
                    pair.Length != 2)
                    return Fail(
                        $"{block}/{concept.Key} precisa de exatamente uma forma A e uma B.",
                        out error);

                KnowledgeItem a = pair.Single(item => item.form == ItemForm.A);
                KnowledgeItem b = pair.Single(item => item.form == ItemForm.B);
                if (string.Equals(
                    a.stem.Trim(), b.stem.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                    return Fail(
                        $"{block}/{concept.Key} repete o mesmo enunciado nas formas A/B.",
                        out error);
                if (SameOptions(a.options, b.options))
                    return Fail(
                        $"{block}/{concept.Key} repete o mesmo conjunto de alternativas.",
                        out error);
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool SameOptions(
        IEnumerable<string> left,
        IEnumerable<string> right)
    {
        string[] a = left.Select(Normalize).OrderBy(value => value).ToArray();
        string[] b = right.Select(Normalize).OrderBy(value => value).ToArray();
        return a.SequenceEqual(b);
    }

    private static string Normalize(string value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
