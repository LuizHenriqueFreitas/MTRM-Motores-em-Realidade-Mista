using UnityEngine;

public class ArvoreComando : ComponenteMotor
{
    [Header("Configurações Gerais")]
    [SerializeField] private Vector3 eixoRotacao = Vector3.up;
    [SerializeField] private float fatorRotacao = 0.5f;
    
    [Header("Configuração do Came")]
    [SerializeField] private int ladoComando = 0; // 0 = Admissão | 1 = Exaustão
    [SerializeField] private int cilindros = 4;
    [SerializeField] private float duracaoAberturaCame = 200f;
    [SerializeField] private float anguloAberturaCame = -10f;
    
    [Header("Inversão de Rotação")]
    [SerializeField] public bool inverterRotacao = false; // NOVO!
    
    [Header("Referência Visual")]
    [SerializeField] private GameObject cameVisual;
    
    // Estado interno
    private Quaternion rotacaoInicial;
    private float anguloArvoreAtual = 0f;
    private float anguloArvoreTotal = 0f;
    
    // Posições iniciais do came visual
    private Vector3 posInicialCame;
    private Quaternion rotInicialCame;
    
    void Awake()
    {
        rotacaoInicial = transform.localRotation;
        
        if (cameVisual != null)
        {
            posInicialCame = cameVisual.transform.localPosition;
            rotInicialCame = cameVisual.transform.localRotation;
        }
    }
    
    public override void AtualizarEstado(float anguloTotal, float rpm)
    {
        // Aplica inversão se necessário
        float fator = fatorRotacao;
        if (inverterRotacao) fator = -fatorRotacao;
        
        // A árvore gira na METADE da velocidade do virabrequim
        anguloArvoreTotal = anguloTotal * fator;
        
        // Ângulo atual para consulta (0-360)
        anguloArvoreAtual = anguloArvoreTotal % 360f;
        if (anguloArvoreAtual < 0) anguloArvoreAtual += 360f;
        
        // Aplica rotação na árvore principal
        Quaternion rotacaoAngulo = Quaternion.AngleAxis(anguloArvoreTotal, eixoRotacao);
        transform.localRotation = rotacaoInicial * rotacaoAngulo;
        
        // Atualiza o came visual
        AtualizarCameVisual();
    }
    
    private void AtualizarCameVisual()
    {
        if (cameVisual == null) return;
        
        // O came visual gira junto com a árvore
        Quaternion rotCame = Quaternion.AngleAxis(anguloArvoreTotal, eixoRotacao);
        cameVisual.transform.localPosition = posInicialCame;
        cameVisual.transform.localRotation = rotInicialCame * rotCame;
    }
    
    public float GetAberturaValvula(int cilindro, int tipoValvula)
    {
        if (tipoValvula != ladoComando) return 0f;
        if (cilindro < 1 || cilindro > cilindros) return 0f;
        
        float anguloCame = CalcularAnguloCame(cilindro);
        
        // Se inverteu a rotação, inverte o ângulo do came também
        if (inverterRotacao)
        {
            anguloCame = (360f - anguloCame) % 360f;
        }
        
        return CalcularAbertura(anguloArvoreAtual, anguloCame, duracaoAberturaCame);
    }
    
    private float CalcularAnguloCame(int cilindro)
    {
        int[] ordemFogo = {1, 3, 4, 2};
        int indiceCilindro = System.Array.IndexOf(ordemFogo, cilindro);
        if (indiceCilindro == -1) return 0f;
        
        // 180° de virabrequim entre ignições equivalem a 90° na árvore,
        // que gira à metade da velocidade.
        float defasagem = indiceCilindro * 90f;
        
        float anguloBase;
        if (ladoComando == 0) // Admissão
        {
            anguloBase = -10f + anguloAberturaCame;
        }
        else // Exaustão
        {
            anguloBase = 180f - 40f + anguloAberturaCame;
        }
        
        float anguloCame = (defasagem + anguloBase) % 360f;
        if (anguloCame < 0) anguloCame += 360f;
        
        return anguloCame;
    }
    
    private float CalcularAbertura(float anguloAtual, float anguloAbertura, float duracao)
    {
        float metadeDuracao = duracao / 2f;
        float distanciaAoCentro = Mathf.DeltaAngle(
            anguloAbertura,
            anguloAtual);
        if (Mathf.Abs(distanciaAoCentro) > metadeDuracao)
            return 0f;

        float progresso =
            (distanciaAoCentro + metadeDuracao) / duracao;
        return Mathf.Sin(Mathf.Clamp01(progresso) * Mathf.PI);
    }
    
    // Métodos públicos
    public float GetAnguloArvore() => anguloArvoreAtual;
    public float GetAnguloArvoreTotal() => anguloArvoreTotal;
    public int GetLadoComando() => ladoComando;
    public string GetNomeLado()
    {
        return ladoComando == 0 ? "Admissão" : "Exaustão";
    }
    
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        
        for (int i = 0; i < cilindros; i++)
        {
            int cilindro = i + 1;
            float anguloCame = CalcularAnguloCame(cilindro);
            
            if (inverterRotacao)
            {
                anguloCame = (360f - anguloCame) % 360f;
            }
            
            float anguloFinal = (anguloArvoreAtual + anguloCame) % 360f;
            
            Vector3 pos = transform.position + transform.up * (0.5f + i * 0.3f);
            
            Quaternion rotCame = Quaternion.AngleAxis(anguloFinal, eixoRotacao);
            Vector3 dirCame = rotCame * transform.forward;
            
            Gizmos.color = (ladoComando == 0) ? Color.yellow : Color.red;
            Gizmos.DrawRay(pos, dirCame * 0.4f);
            Gizmos.DrawSphere(pos + dirCame * 0.4f, 0.05f);
        }
    }
}
