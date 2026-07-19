using UnityEngine;

[ExecuteInEditMode]
public class MedidorBielaSimples : MonoBehaviour
{
    [Header("Configuração")]
    public bool mostrarNoInspector = true;
    public bool mostrarNoGizmos = true;
    
    [Header("Medidas (Read Only)")]
    [SerializeField] private float comprimento = 0f;
    [SerializeField] private float largura = 0f;
    [SerializeField] private float altura = 0f;
    
    void Update()
    {
        if (mostrarNoInspector)
        {
            Medir();
        }
    }
    
    [ContextMenu("Medir Agora")]
    void Medir()
    {
        // Pega o Renderer
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null) renderer = GetComponentInChildren<Renderer>();
        
        if (renderer != null)
        {
            // Pega o tamanho em unidades do mundo
            Vector3 size = renderer.bounds.size;
            
            // Divide pela escala para ter o tamanho local
            size.x /= transform.localScale.x;
            size.y /= transform.localScale.y;
            size.z /= transform.localScale.z;
            
            largura = size.x;
            altura = size.y;
            comprimento = size.z;
        }
        else
        {
            // Se não tiver renderer, tenta usar a escala
            largura = transform.localScale.x;
            altura = transform.localScale.y;
            comprimento = transform.localScale.z;
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (!mostrarNoGizmos) return;
        
        // Desenha a biela com as medidas
        Gizmos.color = Color.cyan;
        Vector3 centro = transform.position;
        Gizmos.DrawWireCube(centro, new Vector3(largura, altura, comprimento));
        
        // Mostra o comprimento
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(
            centro + Vector3.up * (altura / 2f + 0.05f),
            $"Comprimento: {comprimento:F3}m\nAltura: {altura:F3}m\nLargura: {largura:F3}m"
        );
        #endif
    }
}