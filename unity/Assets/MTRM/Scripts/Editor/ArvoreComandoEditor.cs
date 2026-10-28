#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ArvoreComando))]
public class ArvoreComandoEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        ArvoreComando script = (ArvoreComando)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Informações", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Lado", script.GetNomeLado());
        EditorGUILayout.LabelField("Ângulo Atual", $"{script.GetAnguloArvore():F1}°");
        
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Rotação Total", $"{script.GetAnguloArvoreTotal():F1}°");
        }
        
        EditorGUILayout.Space();
        
        // Botão para testar inversão
        if (GUILayout.Button("Inverter Rotação"))
        {
            script.inverterRotacao = !script.inverterRotacao;
            Debug.Log($"Inversão alterada para: {script.inverterRotacao}");
        }
    }
}
#endif