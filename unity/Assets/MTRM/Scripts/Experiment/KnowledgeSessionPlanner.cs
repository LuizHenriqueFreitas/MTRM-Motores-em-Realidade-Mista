using System;
using System.Collections.Generic;
using System.Linq;

public sealed class KnowledgeSessionPlan
{
    public int seed;
    public int counterbalanceCell;
    public ContentBlock selectedBlock;
    public ItemForm preForm;
    public ItemForm postForm;
    public ItemForm alphaPreForm;
    public ItemForm alphaPostForm;
    public ItemForm betaPreForm;
    public ItemForm betaPostForm;
    public List<ExperimentTaskDefinition> tasks =
        new List<ExperimentTaskDefinition>();
}

public static class KnowledgeSessionPlanner
{
    public static KnowledgeSessionPlan Build(
        KnowledgeItemBank bank,
        IEnumerable<ExperimentTaskDefinition> protocolTasks,
        int counterbalanceCell,
        int seed)
    {
        if (!KnowledgeItemBankValidator.IsValid(bank, out string error))
            throw new ArgumentException(error, nameof(bank));
        if (counterbalanceCell < 0 || counterbalanceCell > 3)
            throw new ArgumentOutOfRangeException(nameof(counterbalanceCell));

        ContentBlock selectedBlock = counterbalanceCell < 2
            ? ContentBlock.Alpha : ContentBlock.Beta;
        ItemForm preForm = (counterbalanceCell & 1) == 0
            ? ItemForm.A : ItemForm.B;
        KnowledgeSessionPlan plan = new KnowledgeSessionPlan
        {
            seed = seed,
            counterbalanceCell = counterbalanceCell,
            selectedBlock = selectedBlock,
            preForm = preForm,
            postForm = Opposite(preForm),
            alphaPreForm = selectedBlock == ContentBlock.Alpha
                ? preForm : ItemForm.A,
            alphaPostForm = selectedBlock == ContentBlock.Alpha
                ? Opposite(preForm) : ItemForm.B,
            betaPreForm = selectedBlock == ContentBlock.Beta
                ? preForm : ItemForm.A,
            betaPostForm = selectedBlock == ContentBlock.Beta
                ? Opposite(preForm) : ItemForm.B
        };
        Random random = new Random(seed);
        AddItems(plan.tasks, bank, selectedBlock, preForm,
            ExperimentPhase.PreTest, random);
        plan.tasks.AddRange(protocolTasks.Where(task =>
            task.phase != ExperimentPhase.PreTest &&
            task.phase != ExperimentPhase.PostTest &&
            task.contentBlock == selectedBlock));
        AddItems(plan.tasks, bank, selectedBlock, Opposite(preForm),
            ExperimentPhase.PostTest, random);
        return plan;
    }

    public static int ToOriginalIndex(
        ExperimentTaskDefinition task,
        int displayedIndex) =>
        task.optionPermutation[displayedIndex];

    private static void AddItems(
        ICollection<ExperimentTaskDefinition> destination,
        KnowledgeItemBank bank,
        ContentBlock block,
        ItemForm form,
        ExperimentPhase phase,
        Random random)
    {
        foreach (KnowledgeItem item in bank.items.Where(item =>
                     item.contentBlock == block && item.form == form))
        {
            List<int> permutation =
                Enumerable.Range(0, item.options.Count).ToList();
            for (int index = permutation.Count - 1; index > 0; index--)
            {
                int swap = random.Next(index + 1);
                (permutation[index], permutation[swap]) =
                    (permutation[swap], permutation[index]);
            }
            destination.Add(new ExperimentTaskDefinition
            {
                id = $"{phase}-{item.id}",
                type = ExperimentTaskType.MultipleChoice,
                phase = phase,
                prompt = item.stem,
                options = permutation.Select(index => item.options[index]).ToList(),
                correctOptionIndex =
                    permutation.IndexOf(item.correctOptionIndex),
                explanation = item.explanation,
                allowRetry = false,
                knowledgeItem = item,
                optionPermutation = permutation,
                originalCorrectOptionIndex = item.correctOptionIndex
            });
        }
    }

    private static ItemForm Opposite(ItemForm form) =>
        form == ItemForm.A ? ItemForm.B : ItemForm.A;
}
