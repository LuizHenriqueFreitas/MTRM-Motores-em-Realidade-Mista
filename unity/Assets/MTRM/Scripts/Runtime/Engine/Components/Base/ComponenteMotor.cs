using UnityEngine;

public abstract class ComponenteMotor : MonoBehaviour
{
    protected MotorController motorController;
    protected Transform meuTransform;
    protected float anguloAtual;
    protected float rpmAtual;
    
    protected virtual void Awake()
    {
        meuTransform = transform;
    }
    
    public virtual void Inicializar(MotorController controller)
    {
        motorController = controller;
    }
    
    // Agora recebe o ângulo TOTAL acumulado
    public abstract void AtualizarEstado(float anguloVirabrequimTotal, float rpm);
}