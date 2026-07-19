using UnityEngine;

[ExecuteInEditMode]
public class TrilhoVirabrequim : MonoBehaviour
{
    [Header("Configuração")]
    public float raio = 0.05f;
    public int segmentos = 36;
    public Color corTrilho = Color.yellow;
    
    [Header("Referência para Posicionamento")]
    public Transform pontoBaseBiela; // O ponto onde a biela se conecta ao virabrequim
    public Transform paiDoTrilho; // Virabrequim
    
    [Header("Ajustes de Posição")]
    public Vector3 offsetLocal = Vector3.zero; // Ajuste fino
    public bool alinharComPai = true;
    
    private GameObject trilhoVisual;
    private LineRenderer lineRenderer;
    
    void Start()
    {
        CriarTrilho();
    }
    
    void Update()
    {
        // Atualiza a posição do trilho se estiver em modo de edição
        if (!Application.isPlaying)
        {
            AtualizarPosicaoTrilho();
        }
    }
    
    [ContextMenu("Criar Trilho Visual")]
    public void CriarTrilho()
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
        trilhoVisual.transform.SetParent(paiDoTrilho != null ? paiDoTrilho : transform.parent);
        trilhoVisual.transform.localRotation = Quaternion.identity;
        
        // Adiciona o LineRenderer
        lineRenderer = trilhoVisual.AddComponent<LineRenderer>();
        lineRenderer.positionCount = segmentos + 1;
        lineRenderer.startWidth = 0.002f;
        lineRenderer.endWidth = 0.002f;
        lineRenderer.startColor = corTrilho;
        lineRenderer.endColor = corTrilho;
        lineRenderer.useWorldSpace = false;
        
        // Material
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            lineRenderer.material = new Material(shader);
        }
        
        // Define os pontos do círculo
        AtualizarPontosCirculo();
        
        // Posiciona o trilho
        AtualizarPosicaoTrilho();
        
        Debug.Log($"✅ Trilho criado com raio {raio:F3}m");
    }
    
    private void AtualizarPontosCirculo()
    {
        if (lineRenderer == null) return;
        
        Vector3[] pontos = new Vector3[segmentos + 1];
        
        // O círculo é criado no plano XY (local)
        for (int i = 0; i <= segmentos; i++)
        {
            float angulo = (i / (float)segmentos) * 360f * Mathf.Deg2Rad;
            float x = Mathf.Sin(angulo) * raio;
            float y = Mathf.Cos(angulo) * raio;
            pontos[i] = new Vector3(x, y, 0);
        }
        
        lineRenderer.SetPositions(pontos);
    }
    
    private void AtualizarPosicaoTrilho()
    {
        if (trilhoVisual == null) return;
        
        // Se tem um ponto base, usa ele para posicionar
        if (pontoBaseBiela != null)
        {
            // Posiciona o trilho no ponto base
            trilhoVisual.transform.position = pontoBaseBiela.position;
            
            // Se o pai for o virabrequim, mantém a rotação local zerada
            if (alinharComPai && paiDoTrilho != null)
            {
                trilhoVisual.transform.rotation = paiDoTrilho.rotation;
            }
            
            // Aplica offset local
            trilhoVisual.transform.localPosition += offsetLocal;
        }
        else
        {
            // Se não tem ponto base, usa a posição do pai
            trilhoVisual.transform.localPosition = Vector3.zero;
        }
    }
    
    // Método para atualizar o trilho manualmente
    [ContextMenu("Atualizar Posição do Trilho")]
    public void AtualizarPosicao()
    {
        AtualizarPosicaoTrilho();
        Debug.Log("✅ Posição do trilho atualizada");
    }
    
    [ContextMenu("Remover Trilho")]
    public void RemoverTrilho()
    {
        if (trilhoVisual != null)
        {
            if (Application.isPlaying)
                Destroy(trilhoVisual);
            else
                DestroyImmediate(trilhoVisual);
            trilhoVisual = null;
            lineRenderer = null;
            Debug.Log("✅ Trilho removido");
        }
    }
    
    void OnDestroy()
    {
        if (trilhoVisual != null)
        {
            if (Application.isPlaying)
                Destroy(trilhoVisual);
            else
                DestroyImmediate(trilhoVisual);
        }
    }
}
