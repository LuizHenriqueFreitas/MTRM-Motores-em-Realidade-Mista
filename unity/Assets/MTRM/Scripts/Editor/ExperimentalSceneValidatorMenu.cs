using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExperimentalSceneValidatorMenu
{
    private const string DesktopScenePath =
        "Assets/MTRM/Scenes/DesktopScene.unity";

    [MenuItem("MTRM/Validar cena experimental")]
    public static void Validate()
    {
        ExperimentProtocol protocol =
            Resources.Load<ExperimentProtocol>(
                "Experiment/DefaultProtocol");
        ExperimentalSceneValidationResult result =
            ExperimentalSceneValidator.ValidateActiveScene(protocol);
        if (result.IsValid)
        {
            Debug.Log("MTRM scene validation: success.");
            EditorUtility.DisplayDialog(
                "MTRM — validação da cena",
                result.Message,
                "OK");
            return;
        }

        Debug.LogError(
            "MTRM scene validation failed:" +
            System.Environment.NewLine +
            result.Message);
        EditorUtility.DisplayDialog(
            "MTRM — cena inválida",
            result.Message,
            "OK");
    }

    public static void ValidateFromCommandLine()
    {
        try
        {
            EditorSceneManager.OpenScene(
                DesktopScenePath,
                OpenSceneMode.Single);
            ExperimentProtocol protocol =
                Resources.Load<ExperimentProtocol>(
                    "Experiment/DefaultProtocol");
            ComponentTag[] tags =
                Object.FindObjectsOfType<ComponentTag>(true);
            MeshRenderer[] meshes =
                Object.FindObjectsOfType<MeshRenderer>(true);
            int directlyTaggedMeshes = System.Linq.Enumerable.Count(
                meshes,
                mesh => mesh.GetComponent<ComponentTag>() != null);
            Collider[] colliders =
                Object.FindObjectsOfType<Collider>(true);
            int renderedColliders = System.Linq.Enumerable.Count(
                colliders,
                collider => collider.GetComponent<Renderer>() != null);
            int taggedRenderedColliders = System.Linq.Enumerable.Count(
                colliders,
                collider =>
                    collider.GetComponent<Renderer>() != null &&
                    collider.GetComponentInParent<ComponentTag>(true) != null);
            Debug.Log(
                $"MTRM scene inventory: tags={tags.Length}, " +
                $"meshes={meshes.Length}, directly_tagged_meshes=" +
                $"{directlyTaggedMeshes}, colliders={colliders.Length}, " +
                $"rendered_colliders={renderedColliders}, " +
                $"tagged_rendered_colliders={taggedRenderedColliders}.");
            ExperimentalSceneValidationResult result =
                ExperimentalSceneValidator.ValidateActiveScene(protocol);
            if (!result.IsValid)
            {
                Debug.LogError(
                    "MTRM scene validation failed:" +
                    System.Environment.NewLine +
                    result.Message);
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("MTRM scene validation: success.");
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
