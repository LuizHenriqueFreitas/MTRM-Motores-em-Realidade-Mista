using UnityEngine;

public class SistemaCombustao : ComponenteMotor
{
    [Header("Efeitos de Combustão")]
    [SerializeField] private ParticleSystem particulasCombustao;
    [SerializeField] private ParticleSystem particulasEscape;
    [SerializeField] private Light luzCombustao;
    [SerializeField] private float intensidadeMaxima = 3f;
    
    [Header("Animação da Chama")]
    [SerializeField] private GameObject chama3D;
    [SerializeField] private float escalaMaximaChama = 2f;
    
    public override void AtualizarEstado(float anguloVirabrequim, float rpm)
    {
        float anguloAjustado = (anguloVirabrequim) % 720f;
        float fase = anguloAjustado / 720f;
        
        // Fase de explosão: 0.5 a 0.75 do ciclo (360° a 540°)
        if (fase >= 0.5f && fase < 0.75f)
        {
            float progressoExplosao = (fase - 0.5f) / 0.25f; // 0 a 1
            
            // Chama cresce e depois diminui
            float intensidadeChama = Mathf.Sin(progressoExplosao * Mathf.PI);
            
            // Ativa e escala a chama
            if (chama3D != null)
            {
                chama3D.SetActive(true);
                chama3D.transform.localScale = Vector3.one * (1 + intensidadeChama * escalaMaximaChama);
            }
            
            // Luz da combustão
            if (luzCombustao != null)
            {
                luzCombustao.intensity = intensidadeChama * intensidadeMaxima;
                luzCombustao.color = Color.Lerp(Color.red, Color.yellow, intensidadeChama);
            }
            
            // Partículas de combustão
            if (particulasCombustao != null && !particulasCombustao.isPlaying)
            {
                particulasCombustao.Play();
                var emission = particulasCombustao.emission;
                emission.rateOverTime = intensidadeChama * 100f;
            }
        }
        else
        {
            // Desativa chama fora da explosão
            if (chama3D != null)
                chama3D.SetActive(false);
            
            if (luzCombustao != null)
                luzCombustao.intensity = 0;
            
            if (particulasCombustao != null && particulasCombustao.isPlaying)
                particulasCombustao.Stop();
        }
        
        // Fase de escape: 0.75 a 1.0 do ciclo
        if (fase >= 0.75f)
        {
            if (particulasEscape != null && !particulasEscape.isPlaying)
                particulasEscape.Play();
        }
        else
        {
            if (particulasEscape != null && particulasEscape.isPlaying)
                particulasEscape.Stop();
        }
    }
}