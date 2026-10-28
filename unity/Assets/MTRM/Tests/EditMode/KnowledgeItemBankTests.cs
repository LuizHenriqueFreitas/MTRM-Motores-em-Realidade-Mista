using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class KnowledgeItemBankTests
{
    private const string Path =
        "Assets/Resources/Experiment/DefaultKnowledgeItemBank.asset";

    [Test]
    public void DefaultBank_HasThirtyTwoReviewedItemsAndCompletePairs()
    {
        KnowledgeItemBank bank =
            AssetDatabase.LoadAssetAtPath<KnowledgeItemBank>(Path);
        Assert.That(bank.IsValid(out string error), Is.True, error);
        Assert.That(bank.items.Count, Is.EqualTo(32));
        Assert.That(
            bank.items.Select(item => item.conceptKey).Distinct().Count(),
            Is.EqualTo(16));
        Assert.That(bank.items.All(item =>
            !item.stem.Contains("[RASCUNHO") &&
            item.difficultyEstimate == 0f), Is.True);
    }

    [TestCase(0, ContentBlock.Alpha, ItemForm.A)]
    [TestCase(1, ContentBlock.Alpha, ItemForm.B)]
    [TestCase(2, ContentBlock.Beta, ItemForm.A)]
    [TestCase(3, ContentBlock.Beta, ItemForm.B)]
    public void FourCells_AssignOneBlockAndOppositeFormsWithoutDuplicateItems(
        int cell, ContentBlock block, ItemForm preForm)
    {
        KnowledgeSessionPlan plan = KnowledgeSessionPlanner.Build(
            Load(), new List<ExperimentTaskDefinition>(), cell, 20260728);
        Assert.That(plan.selectedBlock, Is.EqualTo(block));
        Assert.That(plan.preForm, Is.EqualTo(preForm));
        Assert.That(plan.postForm, Is.Not.EqualTo(preForm));
        Assert.That(plan.tasks.All(task =>
            task.knowledgeItem.contentBlock == block), Is.True);
        Assert.That(
            plan.tasks.Select(task => task.knowledgeItem.id).Distinct().Count(),
            Is.EqualTo(16));
    }

    [Test]
    public void FixedSeed_ReconstructsDisplayedOptionsAndOriginalAnswer()
    {
        KnowledgeSessionPlan first = KnowledgeSessionPlanner.Build(
            Load(), new List<ExperimentTaskDefinition>(), 0, 20260728);
        KnowledgeSessionPlan second = KnowledgeSessionPlanner.Build(
            Load(), new List<ExperimentTaskDefinition>(), 0, 20260728);
        ExperimentTaskDefinition a = first.tasks[0];
        ExperimentTaskDefinition b = second.tasks[0];
        Assert.That(a.optionPermutation, Is.EqualTo(b.optionPermutation));
        for (int displayed = 0; displayed < a.options.Count; displayed++)
        {
            int original =
                KnowledgeSessionPlanner.ToOriginalIndex(a, displayed);
            Assert.That(a.options[displayed],
                Is.EqualTo(a.knowledgeItem.options[original]));
        }
        Assert.That(
            KnowledgeSessionPlanner.ToOriginalIndex(
                a, a.correctOptionIndex),
            Is.EqualTo(a.originalCorrectOptionIndex));
    }

    [Test]
    public void ValidatorRejectsDuplicateId()
    {
        KnowledgeItemBank bank = Clone();
        bank.items[1].id = bank.items[0].id;
        AssertInvalid(bank, "duplicado");
    }

    [Test]
    public void ValidatorRejectsInvalidOptions()
    {
        KnowledgeItemBank bank = Clone();
        bank.items[0].options.RemoveAt(0);
        bank.items[0].options.RemoveAt(0);
        AssertInvalid(bank, "alternativas");
    }

    [Test]
    public void ValidatorRejectsMissingPair()
    {
        KnowledgeItemBank bank = Clone();
        bank.items.RemoveAt(0);
        AssertInvalid(bank, "forma A");
    }

    [Test]
    public void ValidatorRejectsBlockWithFewerThanEightConcepts()
    {
        KnowledgeItemBank bank = Clone();
        bank.items.RemoveAll(item =>
            item.contentBlock == ContentBlock.Alpha &&
            item.conceptKey == "funcao_valvula_admissao");
        AssertInvalid(bank, "mínimo 8");
    }

    [Test]
    public void ValidatorRejectsIdenticalStem()
    {
        KnowledgeItemBank bank = Clone();
        KnowledgeItem a = bank.items[0];
        KnowledgeItem b = bank.items.Single(item =>
            item.contentBlock == a.contentBlock &&
            item.conceptKey == a.conceptKey &&
            item.form != a.form);
        b.stem = a.stem;
        AssertInvalid(bank, "mesmo enunciado");
    }

    [Test]
    public void ValidatorRejectsIdenticalOptionSet()
    {
        KnowledgeItemBank bank = Clone();
        KnowledgeItem a = bank.items[0];
        KnowledgeItem b = bank.items.Single(item =>
            item.contentBlock == a.contentBlock &&
            item.conceptKey == a.conceptKey &&
            item.form != a.form);
        b.options = new List<string>(a.options);
        AssertInvalid(bank, "mesmo conjunto");
    }

    private static KnowledgeItemBank Load() =>
        AssetDatabase.LoadAssetAtPath<KnowledgeItemBank>(Path);

    private static KnowledgeItemBank Clone()
    {
        KnowledgeItemBank clone =
            ScriptableObject.CreateInstance<KnowledgeItemBank>();
        clone.bankVersion = Load().bankVersion;
        clone.items = Load().items.Select(item => new KnowledgeItem
        {
            id = item.id,
            contentBlock = item.contentBlock,
            form = item.form,
            conceptKey = item.conceptKey,
            stem = item.stem,
            options = new List<string>(item.options),
            correctOptionIndex = item.correctOptionIndex,
            explanation = item.explanation,
            difficultyEstimate = item.difficultyEstimate
        }).ToList();
        return clone;
    }

    private static void AssertInvalid(
        KnowledgeItemBank bank, string expected)
    {
        Assert.That(bank.IsValid(out string error), Is.False);
        Assert.That(error, Does.Contain(expected).IgnoreCase);
    }
}
