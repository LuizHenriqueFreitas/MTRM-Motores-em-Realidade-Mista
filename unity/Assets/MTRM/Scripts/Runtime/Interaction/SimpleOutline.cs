using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Renderer))]
public class SimpleOutline : MonoBehaviour
{
    [Header("Configurações")]
    [SerializeField] private Color outlineColor = Color.yellow;
    [Range(0.001f, 0.1f)]
    [SerializeField] private float outlineScale = 0.02f;
    
    private GameObject outlineObject;
    private Renderer objectRenderer;
    private bool isSelected = false;
    private static SimpleOutline lastSelected = null;

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        CreateOutlineObject();
        
        // Começa desativado
        if (outlineObject != null)
            outlineObject.SetActive(false);
    }

    void CreateOutlineObject()
    {
        // Cria um clone do objeto apenas para o outline
        outlineObject = Instantiate(gameObject, transform.position, transform.rotation);
        outlineObject.name = gameObject.name + "_Outline";
        outlineObject.transform.SetParent(transform);
        
        // Remove scripts e colliders do clone
        Destroy(outlineObject.GetComponent<Collider>());
        Destroy(outlineObject.GetComponent<SimpleOutline>());
        
        // Configura o material do outline
        Renderer outlineRenderer = outlineObject.GetComponent<Renderer>();
        if (outlineRenderer != null)
        {
            Material mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = outlineColor;
            outlineRenderer.material = mat;
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        
        // Escala o objeto para criar o efeito de outline
        outlineObject.transform.localScale += Vector3.one * outlineScale;
    }

    void OnMouseDown()
    {
        if (isSelected)
        {
            Deselect();
            return;
        }
        
        if (lastSelected != null)
        {
            lastSelected.Deselect();
        }
        
        Select();
        lastSelected = this;
        
        Debug.Log($"Selecionado: {gameObject.name}");
    }

    void Select()
    {
        isSelected = true;
        if (outlineObject != null)
            outlineObject.SetActive(true);
    }

    void Deselect()
    {
        isSelected = false;
        if (outlineObject != null)
            outlineObject.SetActive(false);
        if (lastSelected == this)
            lastSelected = null;
    }

    void OnDestroy()
    {
        if (outlineObject != null)
            Destroy(outlineObject);
    }
}