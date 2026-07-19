using UnityEngine;
using System.Collections.Generic;

public class Valvula : ComponenteMotor
{
    [Header("Partes da Válvula")]
    [SerializeField] private GameObject molaValvula;
    [SerializeField] private GameObject baseValvula;
    [SerializeField] private GameObject hasteValvula;
    
    [Header("Configuração da Válvula")]
    [SerializeField] private int cicloValvula = 1; // 1-4
    [SerializeField] private int tipoValvula = 0; // 0 = Admissao | 1 = Exaustao
    
    [Header("Parâmetros de Animação")]
    [SerializeField] private float levantamentoMaximo = 0.05f;
    [SerializeField] private float compressaoMola = 0.03f;
    
    [Header("Configuração do Eixo")]
    [SerializeField] private EixoMovimento eixoMovimento = EixoMovimento.Z;
    
    public enum EixoMovimento { X, Y, Z }
    
    // Estado interno
    private float posicaoValvula = 0f;
    private List<ArvoreComando> arvoresComando = new List<ArvoreComando>();
    private ArvoreComando arvoreCorreta = null;
    
    // Posições iniciais
    private Vector3 posInicialBaseLocal;
    private Vector3 posInicialMolaLocal;
    private Vector3 escalaInicialMolaLocal;
    private Vector3 posInicialHasteLocal;
    
    void Awake()
    {
        if (baseValvula != null)
            posInicialBaseLocal = baseValvula.transform.localPosition;
        
        if (molaValvula != null)
        {
            posInicialMolaLocal = molaValvula.transform.localPosition;
            escalaInicialMolaLocal = molaValvula.transform.localScale;
        }
        
        if (hasteValvula != null)
            posInicialHasteLocal = hasteValvula.transform.localPosition;
    }
    
    public override void Inicializar(MotorController controller)
    {
        base.Inicializar(controller);
        
        // Encontra TODAS as árvores de comando
        arvoresComando = new List<ArvoreComando>(FindObjectsOfType<ArvoreComando>());
        
        // Encontra a árvore correta para este tipo de válvula
        foreach (var arvore in arvoresComando)
        {
            if (arvore.GetLadoComando() == tipoValvula)
            {
                arvoreCorreta = arvore;
                break;
            }
        }
        
        if (arvoreCorreta != null)
        {
            Debug.Log($"Válvula {GetTipoString()} do cilindro {cicloValvula} " +
                     $"sincronizada com árvore de {arvoreCorreta.GetNomeLado()}");
        }
        else
        {
            Debug.LogWarning($"Válvula {GetTipoString()} do cilindro {cicloValvula} " +
                           $"em modo independente (sem árvore correspondente)");
        }
    }
    
    public override void AtualizarEstado(float anguloVirabrequimTotal, float rpm)
    {
        if (arvoreCorreta != null)
        {
            // USA A ÁRVORE DE COMANDO CORRETA
            posicaoValvula = arvoreCorreta.GetAberturaValvula(cicloValvula, tipoValvula);
        }
        else
        {
            // MODO INDEPENDENTE
            posicaoValvula = CalcularAberturaIndependente(anguloVirabrequimTotal);
        }
        
        AnimarValvula();
    }
    
    private float CalcularAberturaIndependente(float anguloTotal)
    {
        if (rpmAtual <= 0) return 0f;
        
        float anguloAtual = anguloTotal % 360f;
        if (anguloAtual < 0) anguloAtual += 360f;
        
        // Calcula ângulo do came baseado no cilindro e tipo
        int[] ordemFogo = {1, 3, 2, 4};
        int indiceCilindro = System.Array.IndexOf(ordemFogo, cicloValvula);
        float defasagem = indiceCilindro * 180f;
        
        float anguloBase;
        if (tipoValvula == 0) // Admissão
        {
            anguloBase = -10f;
        }
        else // Exaustão
        {
            anguloBase = 140f; // 180° - 40°
        }
        
        float anguloCame = (defasagem + anguloBase) % 360f;
        if (anguloCame < 0) anguloCame += 360f;
        
        // Calcula abertura usando o mesmo método da árvore
        float metadeDuracao = 100f;
        float inicioAbertura = anguloCame - metadeDuracao;
        float fimAbertura = anguloCame + metadeDuracao;
        
        bool dentroJanela = false;
        float progresso = 0f;
        
        if (inicioAbertura < 0)
        {
            float inicioNormalizado = inicioAbertura + 360f;
            if (anguloAtual >= 0 && anguloAtual <= fimAbertura)
            {
                dentroJanela = true;
                progresso = (anguloAtual - 0) / (fimAbertura - 0);
            }
            else if (anguloAtual >= inicioNormalizado && anguloAtual <= 360f)
            {
                dentroJanela = true;
                progresso = (anguloAtual - inicioNormalizado) / (360f - inicioNormalizado);
                progresso = 1f - progresso;
            }
        }
        else if (fimAbertura > 360f)
        {
            float fimNormalizado = fimAbertura - 360f;
            if (anguloAtual >= inicioAbertura && anguloAtual <= 360f)
            {
                dentroJanela = true;
                progresso = (anguloAtual - inicioAbertura) / (360f - inicioAbertura);
            }
            else if (anguloAtual >= 0 && anguloAtual <= fimNormalizado)
            {
                dentroJanela = true;
                progresso = 1f + (anguloAtual - 0) / (fimNormalizado - 0);
            }
        }
        else
        {
            if (anguloAtual >= inicioAbertura && anguloAtual <= fimAbertura)
            {
                dentroJanela = true;
                progresso = (anguloAtual - inicioAbertura) / 200f;
            }
        }
        
        if (dentroJanela)
        {
            return Mathf.Sin(Mathf.Clamp01(progresso) * Mathf.PI);
        }
        
        return 0f;
    }
    
    private void AnimarValvula()
    {
        float elevacaoAtual = posicaoValvula * levantamentoMaximo;
        float compressaoAtual = posicaoValvula * compressaoMola;
        
        Vector3 direcao = Vector3.zero;
        switch (eixoMovimento)
        {
            case EixoMovimento.X: direcao = Vector3.right; break;
            case EixoMovimento.Y: direcao = Vector3.up; break;
            case EixoMovimento.Z: direcao = Vector3.forward; break;
        }
        
        if (baseValvula != null)
        {
            baseValvula.transform.localPosition = posInicialBaseLocal;
            baseValvula.transform.Translate(direcao * (-elevacaoAtual), Space.Self);
        }
        
        if (molaValvula != null)
        {
            molaValvula.transform.localPosition = posInicialMolaLocal;
            molaValvula.transform.Translate(direcao * (-compressaoAtual), Space.Self);
            
            float fatorCompressao = 1f - (posicaoValvula * 0.35f);
            Vector3 escala = escalaInicialMolaLocal;
            
            switch (eixoMovimento)
            {
                case EixoMovimento.X: escala.x *= fatorCompressao; break;
                case EixoMovimento.Y: escala.y *= fatorCompressao; break;
                case EixoMovimento.Z: escala.z *= fatorCompressao; break;
            }
            
            molaValvula.transform.localScale = escala;
        }
        
        if (hasteValvula != null)
        {
            hasteValvula.transform.localPosition = posInicialHasteLocal;
            hasteValvula.transform.Translate(direcao * (-elevacaoAtual * 0.8f), Space.Self);
        }
    }
    
    private string GetTipoString()
    {
        return tipoValvula == 0 ? "Admissão" : "Exaustão";
    }
    
    public float GetPosicaoValvula() => posicaoValvula;
    
    public string GetInfo()
    {
        return $"Cilindro {cicloValvula} | {GetTipoString()} | Abertura: {posicaoValvula:P0}";
    }
}