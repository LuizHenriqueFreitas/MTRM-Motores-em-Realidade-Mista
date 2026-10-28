#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ExperimentProtocolAssetCreator
{
    public const string AssetPath = "Assets/Resources/Experiment/DefaultProtocol.asset";

    [MenuItem("MTRM/Criar ou atualizar protocolo padrão")]
    public static void CreateOrUpdateDefaultProtocol()
    {
        ExperimentProtocol protocol = AssetDatabase.LoadAssetAtPath<ExperimentProtocol>(AssetPath);
        if (protocol == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            protocol = ScriptableObject.CreateInstance<ExperimentProtocol>();
            AssetDatabase.CreateAsset(protocol, AssetPath);
        }

        protocol.protocolId = "mtrm_desktop";
        protocol.version = "4.0.0";
        protocol.condition = "desktop_3d";
        protocol.participantInstructions =
            "Responda primeiro ao pré-teste, explore o motor e conclua o pós-teste. " +
            "Clique com o botão direito para consultar o nome de uma peça; arraste esse botão para girar a câmera. " +
            "Use a roda do mouse para aplicar zoom.";
        protocol.randomizeMainTasks = false;
        protocol.knowledgeItemBank =
            AssetDatabase.LoadAssetAtPath<KnowledgeItemBank>(
                KnowledgeItemBankAssetCreator.AssetPath);
        protocol.tasks = new List<ExperimentTaskDefinition>
        {
            Identification(
                "ALPHA-T01",
                ContentBlock.Alpha,
                "Selecione uma VÁLVULA de admissão ou de escape.",
                new[] { EngineComponentId.ValvulaAdmissao, EngineComponentId.ValvulaEscape },
                new[] { EngineComponentId.MolaValvula, EngineComponentId.PratoMola }),
            Identification(
                "ALPHA-T02",
                ContentBlock.Alpha,
                "Selecione uma MOLA DE VÁLVULA.",
                new[] { EngineComponentId.MolaValvula },
                new[] { EngineComponentId.ValvulaAdmissao, EngineComponentId.ValvulaEscape, EngineComponentId.PratoMola }),
            Identification(
                "ALPHA-T03",
                ContentBlock.Alpha,
                "Selecione a CORREIA DENTADA.",
                new[] { EngineComponentId.CorreiaDentada }),
            Identification(
                "ALPHA-T04",
                ContentBlock.Alpha,
                "Selecione uma ÁRVORE DE CAMES.",
                new[] { EngineComponentId.ArvoreDeCames }),
            Identification(
                "BETA-T01",
                ContentBlock.Beta,
                "Selecione o PISTÃO no modelo do motor.",
                new[] { EngineComponentId.Pistao }),
            Identification(
                "BETA-T02",
                ContentBlock.Beta,
                "Selecione a BIELA. Se necessário, use Expandir para acessar as peças internas.",
                new[] { EngineComponentId.Biela }),
            Identification(
                "BETA-T03",
                ContentBlock.Beta,
                "Selecione o VIRABREQUIM.",
                new[] { EngineComponentId.Virabrequim }),
            Identification(
                "BETA-T04",
                ContentBlock.Beta,
                "Selecione o VOLANTE DO MOTOR.",
                new[] { EngineComponentId.Volante })
        };

        EditorUtility.SetDirty(protocol);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("MTRM_PROTOCOL_CREATED=" + AssetPath);
    }

    private static ExperimentTaskDefinition Identification(
        string id,
        ContentBlock contentBlock,
        string prompt,
        IEnumerable<EngineComponentId> accepted,
        IEnumerable<EngineComponentId> rejected = null)
    {
        return new ExperimentTaskDefinition
        {
            id = id,
            type = ExperimentTaskType.ComponentIdentification,
            phase = ExperimentPhase.MainTasks,
            contentBlock = contentBlock,
            prompt = prompt,
            acceptedComponents = new List<EngineComponentId>(accepted),
            rejectedComponents = rejected == null
                ? new List<EngineComponentId>()
                : new List<EngineComponentId>(rejected),
            allowRetry = true
        };
    }

    private static ExperimentTaskDefinition MultipleChoice(
        string id,
        ExperimentPhase phase,
        string prompt,
        int correctIndex,
        string explanation,
        params string[] options)
    {
        return new ExperimentTaskDefinition
        {
            id = id,
            type = ExperimentTaskType.MultipleChoice,
            phase = phase,
            prompt = prompt,
            options = new List<string>(options),
            correctOptionIndex = correctIndex,
            explanation = explanation,
            allowRetry = false
        };
    }
}
#endif
