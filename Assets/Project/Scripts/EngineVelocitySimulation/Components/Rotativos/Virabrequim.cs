using UnityEngine;

public class Virabrequim : ComponenteMotor
{
    [Header("Configurações do Virabrequim")]
    [SerializeField] private Vector3 eixoRotacao = Vector3.forward;
    [SerializeField] private float fatorRotacao = 1f;
    
    private Quaternion rotacaoInicial;
    
    protected override void Awake()
    {
        base.Awake();
        rotacaoInicial = meuTransform.localRotation;
    }
    
    public override void AtualizarEstado(float anguloVirabrequimTotal, float rpm)
    {
        // Rotação contínua baseada no ângulo TOTAL do virabrequim
        float anguloRotacao = anguloVirabrequimTotal * fatorRotacao;
        Quaternion rotacaoAngulo = Quaternion.AngleAxis(anguloRotacao, eixoRotacao);
        
        meuTransform.localRotation = rotacaoInicial * rotacaoAngulo;
    }
}