using UnityEngine;

public class CilindroVisual : ComponenteMotor
{
    [Header("Configuração Visual do Cilindro")]
    [SerializeField] private GameObject paredeCilindro; // Mesh transparente
    [SerializeField] private float alturaMaxima = 0.2f;
    [SerializeField] private float alturaMinima = 0.05f;
    [SerializeField] private float raioCilindro = 0.041f; // metade do diâmetro
    
    [Header("Cores por Fase")]
    [SerializeField] private Color corAdmissao = new Color(0, 1, 0, 0.3f); // Verde transparente
    [SerializeField] private Color corCompressao = new Color(0, 0, 1, 0.3f); // Azul transparente
    [SerializeField] private Color corExplosao = new Color(1, 0.5f, 0, 0.5f); // Laranja mais opaco
    [SerializeField] private Color corEscape = new Color(0.5f, 0.5f, 0.5f, 0.3f); // Cinza
    
    private Renderer rendererCilindro;
    private float alturaAtual;
    private Vector3 escalaOriginal;
    
    protected override void Awake()
    {
        base.Awake();
        if (paredeCilindro != null)
        {
            rendererCilindro = paredeCilindro.GetComponent<Renderer>();
            escalaOriginal = paredeCilindro.transform.localScale;
        }
    }
    
    public override void AtualizarEstado(float anguloVirabrequim, float rpm)
    {
        if (paredeCilindro == null) return;
        
        // Calcula fase do ciclo (0-720 graus)
        float anguloAjustado = (anguloVirabrequim) % 720f;
        float fase = anguloAjustado / 720f; // 0 a 1
        
        // Altura do cilindro varia com a posição do pistão
        // Usa seno para acompanhar o pistão
        float radianos = (anguloAjustado % 360f) * Mathf.Deg2Rad;
        float alturaNormalizada = (Mathf.Sin(radianos) + 1f) / 2f; // 0 a 1
        
        // Mapeia para altura visual
        alturaAtual = Mathf.Lerp(alturaMinima, alturaMaxima, alturaNormalizada);
        
        // Aplica escala mantendo o raio constante
        Vector3 novaEscala = escalaOriginal;
        novaEscala.y = alturaAtual / (alturaMaxima / escalaOriginal.y);
        paredeCilindro.transform.localScale = novaEscala;
        
        // Atualiza cor baseada na fase
        if (rendererCilindro != null)
        {
            Color corAtual = ObterCorPorFase(fase);
            rendererCilindro.material.color = corAtual;
        }
    }
    
    private Color ObterCorPorFase(float fase)
    {
        if (fase < 0.25f) return corAdmissao;
        if (fase < 0.5f) return corCompressao;
        if (fase < 0.75f) return corExplosao;
        return corEscape;
    }
}