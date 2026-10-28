using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public sealed class ComponentVisualAccessibility : MonoBehaviour
{
    [SerializeField] private Color restingColor =
        new Color(0.82f, 0.52f, 0.16f, 1f);
    [SerializeField] private Color hoverColor =
        new Color(1f, 0.82f, 0.24f, 1f);

    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int BaseColorProperty =
        Shader.PropertyToID("_BaseColor");

    private Renderer targetRenderer;
    private MaterialPropertyBlock properties;

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        properties = new MaterialPropertyBlock();
        Apply(restingColor);
    }

    private void OnMouseEnter()
    {
        Apply(hoverColor);
    }

    private void OnMouseExit()
    {
        Apply(restingColor);
    }

    private void Apply(Color color)
    {
        if (targetRenderer == null)
            return;

        targetRenderer.GetPropertyBlock(properties);
        properties.SetColor(ColorProperty, color);
        properties.SetColor(BaseColorProperty, color);
        targetRenderer.SetPropertyBlock(properties);
    }
}
