using UnityEngine;

public class VolanteMotor : ComponenteMotor
{
    [Header("Configurações do Volante")]
    [SerializeField] private Vector3 eixoRotacao = Vector3.forward;
    [SerializeField] private float fatorRotacao = 1f;
    [SerializeField] private bool usarInercia = true;
    [SerializeField] private float fatorInercia = 0.95f; // Suaviza rotação
    
    private Quaternion rotacaoInicial;
    private float anguloSuavizado = 0f;
    
    protected override void Awake()
    {
        base.Awake();
        rotacaoInicial = meuTransform.localRotation;
    }
    
    public override void AtualizarEstado(float anguloVirabrequim, float rpm)
    {
        float anguloAlvo = anguloVirabrequim * fatorRotacao;
        
        if (usarInercia)
        {
            // Suaviza a rotação para simular inércia
            anguloSuavizado = Mathf.Lerp(anguloSuavizado, anguloAlvo, 1f - fatorInercia);
            anguloSuavizado = anguloSuavizado % 360f;
            
            Quaternion rotacaoAngulo = Quaternion.AngleAxis(anguloSuavizado, eixoRotacao);
            meuTransform.localRotation = rotacaoInicial * rotacaoAngulo;
        }
        else
        {
            Quaternion rotacaoAngulo = Quaternion.AngleAxis(anguloAlvo, eixoRotacao);
            meuTransform.localRotation = rotacaoInicial * rotacaoAngulo;
        }
    }
}