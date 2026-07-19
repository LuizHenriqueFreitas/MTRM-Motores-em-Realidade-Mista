using UnityEngine;

[CreateAssetMenu(fileName = "MotorSettings", menuName = "Motor/Settings")]
public class MotorSettings : ScriptableObject
{
    [Header("Cilindrada")]
    public float cursoPistao = 0.086f; // metros
    public float diametroCilindro = 0.082f;
    
    [Header("Relações")]
    public float comprimentoBiela = 0.145f;
    public float raioVirabrequim = 0.043f; // metade do curso
    
    [Header("Ângulos de Fase")]
    public float offsetVirabrequim = 0f;
    public float offsetArvoreComando = 0f;
}