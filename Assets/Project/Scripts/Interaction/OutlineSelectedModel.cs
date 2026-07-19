using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Renderer))]
public class OutlineSelectedModel : MonoBehaviour
{
    [Header("Configurações")]
    [SerializeField] private Color highlightColor = Color.yellow;

    private Renderer objectRenderer;
    private Color originalColor;
    private bool isSelected = false;
    private static OutlineSelectedModel lastSelected = null;

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        originalColor = objectRenderer.material.color;
    }

    public void OnMouseDown()
    {
        // Ao clicar, decidimos se selecionamos ou deselecionamos
        if (isSelected)
        {
            Deselect();
        }
        else
        {
            if (lastSelected != null)
            {
                lastSelected.Deselect();
            }
            Select();
            lastSelected = this;
        }
    }

    void Select()
    {
        isSelected = true;
        objectRenderer.material.color = highlightColor;
    }

    public void Deselect()
    {
        isSelected = false;
        objectRenderer.material.color = originalColor;
        
        if (lastSelected == this)
            lastSelected = null;
    }
}