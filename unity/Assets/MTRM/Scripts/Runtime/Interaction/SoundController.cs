using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SoundController : MonoBehaviour
{
    [Header("Áudios do Motor")]
    public AudioSource audioAlta;   // Pico em altas rotações
    public AudioSource audioMedia;  // Pico em médias rotações
    public AudioSource audioBaixa;  // Pico em baixas rotações
    
    [Header("Configurações")]
    public Slider sliderRPM;
    public GameObject verifyOn;
    
    [Header("Efeitos Suaves")]
    [Range(1f, 20f)] public float velocidadeTransicao = 8f; // Suavidade da troca de volume
    [Range(1f, 2f)] public float pitchMaximoAlta = 1.3f;    // O quão agudo fica em 3000 RPM

    private float rpmAtual = 0f;
    
    void Start()
    {
        if (sliderRPM == null)
        {
            enabled = false;
            Debug.LogWarning("SoundController: slider de RPM não configurado.");
            return;
        }
        
        audioAlta.loop = true;
        audioMedia.loop = true;
        audioBaixa.loop = true;
        
        // Inicia zerado para não dar estouro no fone ao dar Play
        audioAlta.volume = 0f;
        audioMedia.volume = 0f;
        audioBaixa.volume = 0f;
        
        audioAlta.Play();
        audioMedia.Play();
        audioBaixa.Play();
    }
    
    void Update()
    {
        AtualizarSom(sliderRPM.value);
    }
    
    public void AtualizarSom(float rpm)
    {
        float rpmMaximo = Mathf.Max(sliderRPM.maxValue, 1f);
        float rpmNormalizado = Mathf.Clamp01(rpm / rpmMaximo);

        // Variáveis para definirmos o "objetivo" do volume de cada áudio neste frame
        float alvoAlta = 0f;
        float alvoMedia = 0f;
        float alvoBaixa = 0f;

        // Só calculamos o volume normal se o verifyOn estiver inativo
        if (verifyOn == null || !verifyOn.activeSelf)
        {
            rpmAtual = rpm;

            // CROSSFADE BASEADO EM RPM (Mistura suave de canais)
            // Mathf.InverseLerp retorna 0 a 1 dependendo de onde o RPM está entre dois valores.

            // 1. Inicia mudo no 0, atinge volume no 100
            float mudoInicial = Mathf.InverseLerp(0f, 0.04f, rpmNormalizado);

            // 2. Baixa toca no máximo até 400. Entre 400 e 600, vai caindo até 0.
            alvoBaixa = (1f - Mathf.InverseLerp(0.13f, 0.20f, rpmNormalizado)) * mudoInicial;

            // 3. Média sobe de 400 a 600, fica no máximo, e cai entre 1000 e 1300.
            alvoMedia = Mathf.InverseLerp(0.13f, 0.20f, rpmNormalizado)
                - Mathf.InverseLerp(0.33f, 0.43f, rpmNormalizado);

            // 4. Alta sobe entre 1000 e 1300, e fica no máximo até o final.
            alvoAlta = Mathf.InverseLerp(0.33f, 0.43f, rpmNormalizado);

            // --- SISTEMA DE PITCH PARA O ÁUDIO ALTA (> 2500 RPM) ---
            // Se o RPM está entre 2500 e 3000, o tom sobe do normal (1f) até o máximo configurado
            float pitchAlvo = Mathf.Lerp(1f, pitchMaximoAlta, Mathf.InverseLerp(0.83f, 1f, rpmNormalizado));
            audioAlta.pitch = pitchAlvo;
        }

        // APLICANDO A TRANSIÇÃO NO TEMPO
        // Em vez de jogar o volume seco, o Mathf.Lerp puxa o volume atual até o alvo aos poucos
        audioAlta.volume = Mathf.Lerp(audioAlta.volume, alvoAlta, Time.deltaTime * velocidadeTransicao);
        audioMedia.volume = Mathf.Lerp(audioMedia.volume, alvoMedia, Time.deltaTime * velocidadeTransicao);
        audioBaixa.volume = Mathf.Lerp(audioBaixa.volume, alvoBaixa, Time.deltaTime * velocidadeTransicao);
    }
}
