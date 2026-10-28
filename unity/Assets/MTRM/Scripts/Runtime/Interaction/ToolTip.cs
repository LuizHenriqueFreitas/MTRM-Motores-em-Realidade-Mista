using TMPro; 
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class ToolTip : MonoBehaviour 
{
    [Header("Textos")]
    [SerializeField] private TextMeshProUGUI titleText;       
    [SerializeField] private TextMeshProUGUI descriptionText; 

    [Header("Configuração de Distância (0 a 1)")]
    [Range(0f, 0.5f)] [SerializeField] private float margemEsquerda = 0.6f;
    [Range(0.5f, 1f)] [SerializeField] private float margemDireita = 0.9f;
    [Range(0f, 0.5f)] [SerializeField] private float margemInferior = 0.2f;
    [Range(0.5f, 1f)] [SerializeField] private float margemSuperior = 0.8f;

    public static ToolTip Instance { get; private set; }

    private void Awake() 
    {
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject);     
            return;
        }
        Instance = this;             
        
        ConfigurarLayoutFixo();
        gameObject.SetActive(false); 
    }

    // Agora este método usa as variáveis configuráveis do Inspector
    public void ConfigurarLayoutFixo()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        
        // Aplica os valores definidos no Inspector
        rectTransform.anchorMin = new Vector2(margemEsquerda, margemInferior);
        rectTransform.anchorMax = new Vector2(margemDireita, margemSuperior);
        
        rectTransform.offsetMin = Vector2.zero; 
        rectTransform.offsetMax = Vector2.zero; 
    }

    // Caso você queira ajustar as margens em tempo real via código:
    public void AtualizarMargens(float esq, float dir, float inf, float sup)
    {
        margemEsquerda = esq;
        margemDireita = dir;
        margemInferior = inf;
        margemSuperior = sup;
        ConfigurarLayoutFixo();
    }

    public void Show(string title, string description) 
    {
        titleText.text = title;                                           
        descriptionText.text = description;                               
        gameObject.SetActive(true);                                       
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}