using UnityEngine;
using System.Collections.Generic;

public class MotorController : MonoBehaviour
{
    [Header("Parâmetros de Operação")]
    [SerializeField] private float rpmAtual = 1000f;
    [SerializeField] private float rpmMinimo = 800f;
    [SerializeField] private float rpmMaximo = 7000f;
    
    [Header("Referências")]
    [SerializeField] private List<ComponenteMotor> todosComponentes;
    
    // ACUMULA o ângulo - NUNCA RESETA!
    private float anguloTotalAcumulado = 0f;
    
    // Propriedades públicas
    public float RPMAtual => rpmAtual;
    public float AnguloAtual => anguloTotalAcumulado % 360f; // Apenas para consulta
    public float AnguloTotal => anguloTotalAcumulado;
    
    public void DefinirRPM(float novoRPM)
    {
        rpmAtual = Mathf.Clamp(novoRPM, rpmMinimo, rpmMaximo);
    }
    
    void Start()
    {
        todosComponentes = new List<ComponenteMotor>(GetComponentsInChildren<ComponenteMotor>());
        foreach(var comp in todosComponentes)
            comp.Inicializar(this);
    }
    
    void Update()
    {
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
    
    private void AtualizarComponentes()
    {
        foreach(var comp in todosComponentes)
            comp.AtualizarEstado(anguloTotalAcumulado, rpmAtual);
    }

    public void setRpm(float rpm)
    {
        rpmAtual = rpm;
    }
}