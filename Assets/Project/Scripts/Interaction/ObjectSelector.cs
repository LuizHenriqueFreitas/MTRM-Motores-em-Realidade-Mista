using UnityEngine;

public class ObjectSelector : MonoBehaviour
{
    [Header("Configurações de Seleção")]
    [SerializeField] private LayerMask layerPecas = ~0;
    [SerializeField] private float maxClickDistance = 100f;
    [SerializeField] private KeyCode selectionKey = KeyCode.Mouse0;

    private Camera mainCamera;
    private SimpleCameraController cameraController;
    private Transform currentSelection;

    public Transform CurrentSelection => currentSelection;

    private void Awake()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera não encontrada!");
            enabled = false;
            return;
        }

        cameraController = FindObjectOfType<SimpleCameraController>();
        if (cameraController == null)
        {
            Debug.LogError("SimpleCameraController não encontrado!");
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        // Usando Input.GetMouseButtonDown para melhor compatibilidade
        if (Input.GetMouseButtonDown(0))
        {
            HandleSelection();
        }
    }

    private void HandleSelection()
    {
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        
        // Debug: Desenha o ray para visualização
        Debug.DrawRay(ray.origin, ray.direction * maxClickDistance, Color.red, 2f);

        // Usando Physics.Raycast com camadas específicas
        if (Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, layerPecas))
        {
            // Verifica se o objeto tem um collider ativo
            if (hit.collider != null && hit.collider.enabled)
            {
                Transform selected = hit.transform;
                
                // Toggle: se clicar na mesma peça, deseleciona
                if (currentSelection == selected)
                {
                    ClearSelection();
                }
                else
                {
                    SetSelection(selected);
                }
            }
        }
    }

    public void SetSelection(Transform target)
    {
        if (target == null) return;

        currentSelection = target;
        
        // Atualiza a câmera
        if (cameraController != null)
        {
            cameraController.SetOverrideTarget(target);
        }

        Debug.Log($"Peça selecionada: {target.name}");
    }

    public void ClearSelection()
    {
        currentSelection = null;
        
        // Limpa a câmera
        if (cameraController != null)
        {
            cameraController.ClearOverrideTarget();
        }

        // Esconde tooltip
        ToolTip tooltip = FindObjectOfType<ToolTip>();
        if (tooltip != null)
        {
            tooltip.Hide();
        }

        Debug.Log("Seleção limpa");
    }

    // Método para forçar seleção via código
    public void ForceSelection(Transform target)
    {
        if (target != null)
        {
            SetSelection(target);
        }
        else
        {
            ClearSelection();
        }
    }

    private void OnDrawGizmos()
    {
        if (mainCamera == null) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(mainCamera.transform.position, maxClickDistance);
    }
}