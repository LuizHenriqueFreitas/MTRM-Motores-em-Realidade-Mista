using UnityEngine;

[ExecuteInEditMode]
public class CriadorTrilhoVirabrequim : MonoBehaviour
{
    [Header("Configuração")]
    public float raio = 0.05f;
    public int segmentos = 36;
    public Color corTrilho = Color.yellow;
    
    [Header("Referência")]
    public Transform paiDoTrilho; // Onde o trilho será criado
    
    private GameObject trilhoVisual;
    
    [ContextMenu("Criar Trilho Visual")]
    void CriarTrilhoVisual()
    {
        // Remove trilho antigo
        if (trilhoVisual != null)
        {
            if (Application.isPlaying)
                Destroy(trilhoVisual);
            else
                DestroyImmediate(trilhoVisual);
        }
        
        // Cria o GameObject do trilho
        trilhoVisual = new GameObject("Trilho_Virabrequim");
        trilhoVisual.transform.SetParent(paiDoTrilho != null ? paiDoTrilho : transform);
        trilhoVisual.transform.localPosition = Vector3.zero;
        
        // Cria a linha do círculo
        LineRenderer line = trilhoVisual.AddComponent<LineRenderer>();
        line.positionCount = segmentos + 1;
        line.startWidth = 0.002f;
        line.endWidth = 0.002f;
        line.startColor = corTrilho;
        line.endColor = corTrilho;
        line.material = new Material(Shader.Find("Sprites/Default"));
        
        // Define os pontos do círculo
        Vector3[] pontos = new Vector3[segmentos + 1];
        for (int i = 0; i <= segmentos; i++)
        {
            float angulo = (i / (float)segmentos) * 360f * Mathf.Deg2Rad;
            float x = Mathf.Sin(angulo) * raio;
            float y = Mathf.Cos(angulo) * raio;
            pontos[i] = new Vector3(x, y, 0);
        }
        line.SetPositions(pontos);
        
        Debug.Log($"Trilho do virabrequim criado com raio {raio:F3}m");
    }
    
    [ContextMenu("Remover Trilho")]
    void RemoverTrilho()
    {
        if (trilhoVisual != null)
        {
            if (Application.isPlaying)
                Destroy(trilhoVisual);
            else
                DestroyImmediate(trilhoVisual);
            trilhoVisual = null;
        }
    }
}