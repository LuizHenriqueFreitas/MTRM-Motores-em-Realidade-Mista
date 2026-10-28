using UnityEngine;
using System.Collections.Generic;

public class MotorController : MonoBehaviour
{
    private const float RpmRecordIntervalSeconds = 0.5f;
    private const float RpmRecordMinimumDelta = 100f;
    private const float RpmSettleSeconds = 0.75f;

    [Header("Parâmetros de Operação")]
    [SerializeField] private float rpmAtual = 1000f;
    [SerializeField] private float rpmMinimo = 800f;
    [SerializeField] private float rpmMaximo = 7000f;
    
    [Header("Referências")]
    [SerializeField] private List<ComponenteMotor> todosComponentes;
    
    // ACUMULA o ângulo - NUNCA RESETA!
    private float anguloTotalAcumulado = 0f;
    private float ultimoRpmRegistrado = float.NaN;
    private float proximoRegistroRpm;
    private float ultimaAlteracaoRpm;
    private bool possuiRpmPendente;
    
    // Propriedades públicas
    public float RPMAtual => rpmAtual;
    public float RPMMinimo => rpmMinimo;
    public float RPMMaximo => rpmMaximo;
    public float AnguloAtual => anguloTotalAcumulado % 360f; // Apenas para consulta
    public float AnguloTotal => anguloTotalAcumulado;
    
    public void DefinirRPM(float novoRPM)
    {
        float rpmAnterior = rpmAtual;
        rpmAtual = Mathf.Clamp(novoRPM, rpmMinimo, rpmMaximo);

        if (!Mathf.Approximately(rpmAnterior, rpmAtual))
        {
            possuiRpmPendente = true;
            ultimaAlteracaoRpm = Time.unscaledTime;
            TentarRegistrarRpm();
        }
    }
    
    void Start()
    {
        todosComponentes = new List<ComponenteMotor>(GetComponentsInChildren<ComponenteMotor>());
        foreach(var comp in todosComponentes)
            comp.Inicializar(this);
    }
    
    void Update()
    {
        TentarRegistrarRpm();

        if (rpmAtual > 0)
        {
            // Calcula rotação baseada no RPM
            float rotacaoPorFrame = (rpmAtual / 60f) * 360f * Time.deltaTime;
            
            // ACUMULA o ângulo (NUNCA RESETA!)
            anguloTotalAcumulado += rotacaoPorFrame;
            
            // Atualiza todos os componentes com o ângulo TOTAL
            AtualizarComponentes();
        }
    }

    private void TentarRegistrarRpm()
    {
        if (!ExperimentEventRecorder.IsActive)
        {
            possuiRpmPendente = false;
            ultimoRpmRegistrado = float.NaN;
            return;
        }

        if (!possuiRpmPendente || Time.unscaledTime < proximoRegistroRpm)
            return;

        if (!float.IsNaN(ultimoRpmRegistrado) &&
            Mathf.Abs(rpmAtual - ultimoRpmRegistrado) < RpmRecordMinimumDelta &&
            Time.unscaledTime - ultimaAlteracaoRpm < RpmSettleSeconds)
        {
            return;
        }

        ExperimentEventRecorder.Record(
            "rpm_changed",
            new ExperimentEventData
            {
                objectId = gameObject.name,
                numericValue = rpmAtual
            });
        ultimoRpmRegistrado = rpmAtual;
        proximoRegistroRpm = Time.unscaledTime + RpmRecordIntervalSeconds;
        possuiRpmPendente = false;
    }
    
    private void AtualizarComponentes()
    {
        foreach(var comp in todosComponentes)
            comp.AtualizarEstado(anguloTotalAcumulado, rpmAtual);
    }

    public void setRpm(float rpm)
    {
        DefinirRPM(rpm);
    }
}
