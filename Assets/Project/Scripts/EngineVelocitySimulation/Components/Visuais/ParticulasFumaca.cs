using UnityEngine;
using UnityEngine.UI;

public class ParticulasFumaca : MonoBehaviour
{
    public Slider slider;
    private ParticleSystem ps;
    private ParticleSystem.MainModule mainModule;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        mainModule = ps.main;
        
        // Configura o slider para chamar a função APENAS quando o valor mudar
        slider.onValueChanged.AddListener(delegate { OtimizarVelocidade(); });
        
        // Executa uma vez no início
        OtimizarVelocidade();
    }

    private void OtimizarVelocidade()
    {
        float novoValue = slider.value;

        if (novoValue >= 2500)
        {
            mainModule.startSpeed = 20;
        }
        else if (novoValue >= 1200)
        {
            mainModule.startSpeed = 15;
        }
        else if (novoValue >= 800) // Corrigido: antes estava repetido 1200
        {
            mainModule.startSpeed = 10;
        }
        else if (novoValue >= 500)
        {
            mainModule.startSpeed = 7;
        }
        else if (novoValue >= 100)
        {
            mainModule.startSpeed = 4;
        }
        else
        {
            mainModule.startSpeed = 1; 
        }
    }
}
