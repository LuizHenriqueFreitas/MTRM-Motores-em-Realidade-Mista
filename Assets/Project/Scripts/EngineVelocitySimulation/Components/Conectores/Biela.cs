using UnityEngine;

public class Biela : ComponenteMotor
{
    public enum TipoPivo { Centro, ExtremidadeVirabrequim, ExtremidadePistao }

    [Header("Referências")]
    public Transform pontoVirabrequim;
    public Transform pontoPistao;

    [Header("Pivô do mesh")]
    [Tooltip("Onde fica a origem (pivô) do mesh da biela. Isso define em relação a qual ponto " +
             "a escala/posição vai se ancorar para garantir que AMBAS as pontas encostem exatamente " +
             "nos pontos de interesse.")]
    public TipoPivo pivo = TipoPivo.Centro;

    [Header("Eixo de COMPRIMENTO do mesh")]
    [Tooltip("Eixo local do mesh que representa o comprimento da biela (a direção que vai até o pistão). " +
             "Precisa ser um eixo puro (X, Y ou Z) para a escala funcionar corretamente.")]
    public Vector3 eixoComprimento = Vector3.up;

    [Header("Comprimento real do mesh")]
    [Tooltip("Comprimento do mesh, em unidades locais, medido ao longo de 'eixoComprimento', " +
             "com localScale = 1. É a régua usada para calcular o fator de escala a cada frame.")]
    public float comprimentoReferencia = 0.145f;
    [Tooltip("Se true, escala o comprimento a cada frame para bater exatamente com a distância real " +
             "entre os dois pontos. Desative só se tiver certeza que a distância nunca varia.")]
    public bool ajustarEscala = false;
    [Tooltip("Ao esticar/encolher o comprimento, afina/engrossa a largura e espessura proporcionalmente " +
             "(1/sqrt da escala), pra não parecer elástico.")]
    public bool manterEspessuraProporcional = false;

    [Header("Controle de giro (twist) — resolve orientação paralela/perpendicular")]
    [Tooltip("Eixo local do mesh que representa a LARGURA/face da biela (perpendicular ao comprimento).")]
    public Vector3 eixoLargura = Vector3.right;
    [Tooltip("Eixo de ROTAÇÃO do virabrequim, em espaço MUNDIAL.")]
    public Vector3 eixoRotacaoVirabrequim = Vector3.forward;
    [Tooltip("Se true, o eixoLargura fica PERPENDICULAR ao eixo do virabrequim. Se false, fica PARALELO.")]
    public bool larguraPerpendicularAoEixo = true;

    [Tooltip("Inverte a direção do comprimento, caso a biela fique apontando de ponta-cabeça")]
    public bool inverterDirecao = false;

    [Header("Offset de posição (espaço local, acompanha a rotação)")]
    public Vector3 offsetPosicao = Vector3.zero;

    private Vector3 escalaInicial;

    void Awake()
    {
        escalaInicial = transform.localScale;
    }

    public override void AtualizarEstado(float anguloTotal, float rpm)
    {
        if (pontoVirabrequim == null || pontoPistao == null)
            return;

        Vector3 inferior = pontoVirabrequim.position;
        Vector3 superior = pontoPistao.position;

        Vector3 direcaoBruta = superior - inferior;
        float distancia = direcaoBruta.magnitude;
        if (distancia < 0.0000001f) return; // evita rotação/escala com vetor zero

        Vector3 direcao = inverterDirecao ? -direcaoBruta : direcaoBruta;
        direcao.Normalize();

        Vector3 comp = eixoComprimento.sqrMagnitude > 0.0001f ? eixoComprimento.normalized : Vector3.up;
        Vector3 larg = eixoLargura.sqrMagnitude > 0.0001f ? eixoLargura.normalized : Vector3.right;

        // ===== ROTAÇÃO (comprimento + correção de giro) =====
        Quaternion rotBase = Quaternion.FromToRotation(comp, direcao);
        Vector3 larguraAtual = rotBase * larg;

        Vector3 eixoVirabrequimNorm = eixoRotacaoVirabrequim.sqrMagnitude > 0.0001f
            ? eixoRotacaoVirabrequim.normalized
            : Vector3.forward;

        Vector3 alvo = Vector3.ProjectOnPlane(eixoVirabrequimNorm, direcao);
        if (larguraPerpendicularAoEixo)
            alvo = Vector3.Cross(direcao, alvo);

        Quaternion rotacaoFinal;
        if (alvo.sqrMagnitude < 0.0001f)
        {
            rotacaoFinal = rotBase; // caso degenerado: eixo do virabrequim quase paralelo à biela
        }
        else
        {
            alvo.Normalize();
            Quaternion correcaoTwist = Quaternion.FromToRotation(larguraAtual, alvo);
            rotacaoFinal = correcaoTwist * rotBase;
        }

        transform.rotation = rotacaoFinal;

        // ===== ESCALA (garante que o COMPRIMENTO bata exatamente com a distância real) =====
        if (ajustarEscala && comprimentoReferencia > 0f)
        {
            float fatorComprimento = Mathf.Max(distancia / comprimentoReferencia, 0.0001f);

            Vector3 novaEscala = escalaInicial;
            // Descobre qual componente local (x/y/z) corresponde ao eixoComprimento e escala só esse
            int eixoIndex = MaiorComponente(comp);

            if (manterEspessuraProporcional)
            {
                float fatorFino = 1f / Mathf.Sqrt(fatorComprimento);
                novaEscala = escalaInicial * fatorFino;
            }

            novaEscala[eixoIndex] = escalaInicial[eixoIndex] * fatorComprimento;
            transform.localScale = novaEscala;
        }

        // ===== POSIÇÃO (ancorada de acordo com o pivô do mesh, para as pontas baterem exatas) =====
        Vector3 posBase;
        switch (pivo)
        {
            case TipoPivo.ExtremidadeVirabrequim:
                posBase = inferior;
                break;
            case TipoPivo.ExtremidadePistao:
                posBase = superior;
                break;
            default: // Centro
                posBase = (inferior + superior) * 0.5f;
                break;
        }

        transform.position = posBase;

        // ===== OFFSET (espaço local, aplicado por último) =====
        if (offsetPosicao.sqrMagnitude > 0.0000001f)
        {
            transform.position += rotacaoFinal * offsetPosicao;
        }
    }

    private static int MaiorComponente(Vector3 v)
    {
        float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
        if (ax >= ay && ax >= az) return 0;
        if (ay >= ax && ay >= az) return 1;
        return 2;
    }
}