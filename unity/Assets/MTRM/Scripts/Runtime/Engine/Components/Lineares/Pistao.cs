using UnityEngine;

public class Pistao : ComponenteMotor
{
    public enum BancoCilindro { Grupo1e4, Grupo2e3 }

    [Header("Referências geométricas")]
    public Transform centroVirabrequim;   // Centro do virabrequim (ponto fixo, só gira)
    public Transform pontoVirabrequim;    // Pino que ORBITA o centro (usado só p/ medir a proporção r/L)
    [Tooltip("Eixo de deslizamento do cilindro, em espaço LOCAL do pai deste pistão")]
    public Vector3 eixoDeslizamento = Vector3.up;

    [Header("Proporção da biela-manivela (só define a FORMA da curva, não a amplitude)")]
    public float comprimentoBiela = 0.145f; // L
    [Tooltip("Se 0, é calculado automaticamente em Start() a partir da distância centro->pino")]
    public float raioVirabrequim = 0f;      // r

    [Header("Curso real (calibrado em espaço LOCAL — isso é que define o quanto o pistão se move)")]
    [Tooltip("Posição axial local do FUNDO do curso (PMI). Por padrão 0.")]
    public float alturaFundoLocal = 0f;
    [Tooltip("Se true, usa a posição local inicial do objeto no editor como TOPO do curso (PMS). " +
             "Se false, use 'alturaTopoLocalManual'.")]
    public bool usarPosicaoInicialComoTopo = true;
    public float alturaTopoLocalManual = 0.05f;

    [Header("Sincronismo")]
    public BancoCilindro banco = BancoCilindro.Grupo1e4;
    public float defasagemAngulo = 0f;

    [Header("Offsets (espaço local do pai)")]
    public Vector3 offsetPosicao = Vector3.zero;

    // Estado interno — tudo em espaço LOCAL do pai, calibrado nos pontos que você já posicionou
    private Vector3 posInicialLocal;
    private Vector3 perpInicialLocal;
    private Vector3 eixoLocal;
    private float alturaTopoLocal;
    private float alturaAtualLocal = 0f;
    private float anguloAtual = 0f;
    private float anguloEfetivo = 0f;

    // PMS/PMI em "metros de referência" — usados SÓ para calcular a proporção t (0 a 1) da curva,
    // nunca aplicados como distância absoluta na posição final
    private float PMS_ref;
    private float PMI_ref;

    void Awake()
    {
        posInicialLocal = transform.localPosition;
        eixoLocal = eixoDeslizamento.normalized;
        perpInicialLocal = posInicialLocal - Vector3.Dot(posInicialLocal, eixoLocal) * eixoLocal;
    }

    void Start()
    {
        alturaTopoLocal = usarPosicaoInicialComoTopo
            ? Vector3.Dot(posInicialLocal, eixoLocal)
            : alturaTopoLocalManual;

        if (raioVirabrequim <= 0f && centroVirabrequim != null && pontoVirabrequim != null)
        {
            Transform espacoReferencia = centroVirabrequim.parent;
            if (espacoReferencia != null)
            {
                Vector3 centroLocal = espacoReferencia.InverseTransformPoint(centroVirabrequim.position);
                Vector3 pontoLocal = espacoReferencia.InverseTransformPoint(pontoVirabrequim.position);
                raioVirabrequim = Vector3.Distance(centroLocal, pontoLocal);
            }
            else
            {
                raioVirabrequim = Vector3.Distance(centroVirabrequim.position, pontoVirabrequim.position);
            }
        }

        comprimentoBiela = Mathf.Max(comprimentoBiela, 0.0001f);

        if (raioVirabrequim >= comprimentoBiela)
        {
            float raioMedido = raioVirabrequim;
            raioVirabrequim = comprimentoBiela * 0.95f;
            Debug.LogWarning(
                $"[{name}] raio do virabrequim ({raioMedido:F4}) incompatível com a biela " +
                $"({comprimentoBiela:F4}). O raio foi limitado a {raioVirabrequim:F4} " +
                "para manter a simulação numericamente estável.");
        }

        PMS_ref = comprimentoBiela + raioVirabrequim;
        PMI_ref = comprimentoBiela - raioVirabrequim;
    }

    public override void AtualizarEstado(float anguloTotal, float rpm)
    {
        float angulo = anguloTotal % 360f;
        if (angulo < 0f) angulo += 360f;
        anguloAtual = angulo;

        float anguloComDefasagem = (angulo + defasagemAngulo) % 360f;
        if (anguloComDefasagem < 0f) anguloComDefasagem += 360f;

        float anguloFase = (banco == BancoCilindro.Grupo2e3)
            ? (anguloComDefasagem + 180f) % 360f
            : anguloComDefasagem;

        anguloEfetivo = anguloFase;

        // 1. Calcula a posição "de referência" em metros (só para achar a forma da curva).
        float rad = anguloFase * Mathf.Deg2Rad;
        float xPino = Mathf.Sin(rad) * raioVirabrequim;
        float yPino = Mathf.Cos(rad) * raioVirabrequim;

        float lateral = Mathf.Min(Mathf.Abs(xPino), comprimentoBiela * 0.999f);
        float offsetAxialBiela = Mathf.Sqrt(Mathf.Max(comprimentoBiela * comprimentoBiela - lateral * lateral, 0f));

        float yPistaoRef = yPino + offsetAxialBiela; // entre PMI_ref e PMS_ref

        // 2. Normaliza (0 = fundo, 1 = topo) — essa é a forma real da curva biela-manivela
        float t = Mathf.InverseLerp(PMI_ref, PMS_ref, yPistaoRef);

        // 3. Aplica esse "t" nos pontos LOCAIS reais que você já posicionou na cena
        alturaAtualLocal = Mathf.Lerp(alturaFundoLocal, alturaTopoLocal, t);

        Vector3 pos = perpInicialLocal + alturaAtualLocal * eixoLocal + offsetPosicao;
        transform.localPosition = pos;
    }

    public float GetAlturaAtual() => alturaAtualLocal;
    public float GetAnguloAtual() => anguloAtual;
    public float GetAnguloEfetivo() => anguloEfetivo;

    public float GetPosicaoNormalizada() => Mathf.InverseLerp(alturaFundoLocal, alturaTopoLocal, alturaAtualLocal);

    public string GetEstado()
    {
        float a = anguloEfetivo;
        if (a >= 350f || a < 10f) return "🔝 TOPO (PMS)";
        if (a >= 170f && a < 190f) return "🔽 FUNDO (PMI)";
        if (a >= 10f && a < 170f) return "⬇️ DESCENDO";
        return "⬆️ SUBINDO";
    }

    public string GetInfo()
    {
        return $"Pistão | Ângulo: {anguloAtual:F1}° (efetivo {anguloEfetivo:F1}°) | {GetEstado()} | Altura local: {alturaAtualLocal:F3} | Normalizada: {GetPosicaoNormalizada():P0}";
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Transform pai = transform.parent;
        Vector3 fundoLocalPos = perpInicialLocal + alturaFundoLocal * eixoLocal;
        Vector3 topoLocalPos = perpInicialLocal + alturaTopoLocal * eixoLocal;

        Vector3 fundoMundo = pai != null ? pai.TransformPoint(fundoLocalPos) : fundoLocalPos;
        Vector3 topoMundo = pai != null ? pai.TransformPoint(topoLocalPos) : topoLocalPos;

        Gizmos.color = Color.gray;
        Gizmos.DrawLine(fundoMundo - Vector3.right * 0.05f, fundoMundo + Vector3.right * 0.05f);
        Gizmos.DrawLine(topoMundo - Vector3.right * 0.05f, topoMundo + Vector3.right * 0.05f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(fundoMundo, topoMundo);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(transform.position, new Vector3(0.06f, 0.04f, 0.06f));

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.1f,
            $"Ângulo: {anguloAtual:F1}° (ef {anguloEfetivo:F1}°)\n{GetEstado()}"
        );
#endif
    }
}
