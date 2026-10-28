using UnityEngine;
using Cinemachine;
using System;

public class CameraController : MonoBehaviour
{
    public static event Action<Transform> ObjectSelected;
    public static event Action<Transform, string> ObjectNameQueried;

    [System.Serializable]
    public class RingTargets
    {
        [Header("Look At")]
        public Transform primaryLookAt;
        public Transform fallbackLookAt;

        [Header("Follow")]
        public Transform primaryFollow;
        public Transform fallbackFollow;
    }

    [Header("Referências")]
    [SerializeField] private CinemachineFreeLook freeLook;
    private Camera mainCamera; 

    [Header("Seleção de Peças (Click)")]
    [SerializeField] private LayerMask layerPecas = ~0; 
    [SerializeField] private float maxClickDistance = 100f; 

    [Header("Rotação")]
    [SerializeField] private float rotationSpeed = 0.1f;
    [SerializeField] private float mouseSensitivity = 0.5f;
    [SerializeField] private bool invertX;
    [SerializeField] private bool invertY;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 2f;
    [SerializeField] private float maxZoom = 20f;

    [Header("Targets dos Rigs")]
    [SerializeField]
    private RingTargets[] ringTargets = new RingTargets[3];

    [Header("Ajustes de Sensibilidade")]
    [SerializeField] private float yAxisSensitivityMultiplier = 0.15f; 
    [SerializeField] private float yAxisClampSpeed = 0.02f; 

    private float currentZoom;
    private Vector2 lastMousePosition;
    private bool isRotating;
    private Vector2 rightMouseDownPosition;
    private Transform rightClickTarget;
    private float currentYAxisValue;

    private readonly Transform[] currentLookTargets = new Transform[3];
    private readonly Transform[] currentFollowTargets = new Transform[3];

    private Transform overrideTarget;

    #region Unity

    private void Awake()
    {
        if (freeLook == null)
            freeLook = FindObjectOfType<CinemachineFreeLook>();

        if (freeLook == null)
        {
            Debug.LogError("Cinemachine FreeLook não encontrada.");
            enabled = false;
            return;
        }

        mainCamera = Camera.main;
    }

    private void Start()
    {
        freeLook.m_XAxis.m_InputAxisName = "";
        freeLook.m_YAxis.m_InputAxisName = "";

        freeLook.m_XAxis.m_MaxSpeed = 0f;
        freeLook.m_YAxis.m_MaxSpeed = 0f;
        freeLook.m_XAxis.m_AccelTime = 0f;
        freeLook.m_XAxis.m_DecelTime = 0f;
        freeLook.m_YAxis.m_AccelTime = 0f;
        freeLook.m_YAxis.m_DecelTime = 0f;
        
        freeLook.m_YAxis.m_MaxSpeed = 0.01f;
        
        currentYAxisValue = freeLook.m_YAxis.Value;
        currentZoom = freeLook.m_Orbits[1].m_Radius;

        RefreshRigTargets(true);
    }

    private void Update()
    {
        HandleClickSelection(); 
        HandleRotation();
        HandleZoom();
        RefreshRigTargets();
    }

    #endregion

    #region Seleção por Clique

    private void HandleClickSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (mainCamera != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, layerPecas))
                {
                    ObjectSelected?.Invoke(hit.transform);

                    // Se clicar na mesma peça que já está em foco, cancela o foco (Toggle)
                    if (overrideTarget == hit.transform)
                    {
                        ClearOverrideTarget();
                    }
                    // Se for uma peça diferente, muda o foco para ela
                    else
                    {
                        SetOverrideTarget(hit.transform);
                    }
                }
            }
        }
    }

    #endregion

    #region Rotação

    private void HandleRotation()
    {
        if (Input.GetMouseButtonDown(1))
        {
            isRotating = true;
            lastMousePosition = Input.mousePosition;
            rightMouseDownPosition = Input.mousePosition;
            rightClickTarget = RaycastPartUnderMouse();
        }

        if (Input.GetMouseButtonUp(1))
        {
            isRotating = false;
            if (rightClickTarget != null &&
                Vector2.Distance(rightMouseDownPosition, Input.mousePosition) <= 8f)
            {
                ObjectNameQueried?.Invoke(rightClickTarget, GetDisplayName(rightClickTarget));
            }
            rightClickTarget = null;
        }

        if (!isRotating)
            return;

        // --- NOVO: Cálculo do multiplicador baseado no zoom ---
        // Quanto menor o zoom (mais perto), menor o valor. 
        // Invertemos para que perto = sensibilidade baixa, longe = sensibilidade normal.
        float zoomFactor = Mathf.InverseLerp(minZoom, maxZoom, currentZoom); 
        // Se minZoom é 2 e maxZoom é 20: 
        // Com zoom 2, zoomFactor é 0. Com zoom 20, zoomFactor é 1.
        
        // Ajuste a sensibilidade dinamicamente (0.2f é a sensibilidade mínima quando muito perto)
        float dynamicSensitivity = Mathf.Lerp(0.2f, 1.0f, zoomFactor);

        Vector2 currentMouse = Input.mousePosition;
        Vector2 delta = currentMouse - lastMousePosition;
        lastMousePosition = currentMouse;

        // Aplicamos o dynamicSensitivity aqui
        float deltaX = delta.x * mouseSensitivity * rotationSpeed * dynamicSensitivity;
        if (invertX)
            deltaX *= -1f;
        
        freeLook.m_XAxis.Value += deltaX;

        // Aplicamos o dynamicSensitivity aqui também
        float rawDeltaY = delta.y * mouseSensitivity * rotationSpeed * yAxisSensitivityMultiplier * dynamicSensitivity;
        
        if (!invertY)
            rawDeltaY *= -1f;
        
        rawDeltaY = Mathf.Clamp(rawDeltaY, -yAxisClampSpeed, yAxisClampSpeed);
        
        currentYAxisValue += rawDeltaY;
        currentYAxisValue = Mathf.Clamp01(currentYAxisValue);
        
        freeLook.m_YAxis.Value = currentYAxisValue;
    }

    private Transform RaycastPartUnderMouse()
    {
        if (mainCamera == null)
            return null;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, layerPecas)
            ? hit.transform
            : null;
    }

    private static string GetDisplayName(Transform selected)
    {
        ComponentTag tag = selected.GetComponentInParent<ComponentTag>();
        if (tag != null && !string.IsNullOrWhiteSpace(tag.DisplayName))
            return tag.DisplayName;

        ToolTipTrigger tooltip = selected.GetComponentInParent<ToolTipTrigger>();
        if (tooltip != null && !string.IsNullOrWhiteSpace(tooltip.TitleText))
            return tooltip.TitleText;

        string displayName = selected.name;
        int dotIndex = displayName.IndexOf('.');
        if (dotIndex > 0)
            displayName = displayName.Substring(0, dotIndex);

        int copyIndex = displayName.IndexOf(" (", StringComparison.Ordinal);
        if (copyIndex > 0)
            displayName = displayName.Substring(0, copyIndex);

        return displayName.Replace('_', ' ').Trim();
    }

    #endregion

    #region Zoom

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) < 0.0001f)
            return;

        SetZoom(currentZoom - scroll * zoomSpeed);
    }

    public void SetZoom(float zoom)
    {
        currentZoom = Mathf.Clamp(zoom, minZoom, maxZoom);

        for (int i = 0; i < freeLook.m_Orbits.Length; i++)
        {
            var orbit = freeLook.m_Orbits[i];
            orbit.m_Radius = currentZoom;
            freeLook.m_Orbits[i] = orbit;
        }
    }

    #endregion

    #region Targets

    private void RefreshRigTargets(bool force = false)
    {
        if (ringTargets == null)
            return;

        for (int i = 0; i < Mathf.Min(3, ringTargets.Length); i++)
        {
            CinemachineVirtualCamera rig = freeLook.GetRig(i);

            if (rig == null)
                continue;

            Transform newLook;
            Transform newFollow;

            if (overrideTarget != null && overrideTarget.gameObject.activeInHierarchy)
            {
                newLook = overrideTarget;
                newFollow = overrideTarget;
            }
            else
            {
                newLook = GetBestTarget(ringTargets[i].primaryLookAt, ringTargets[i].fallbackLookAt);
                newFollow = GetBestTarget(ringTargets[i].primaryFollow, ringTargets[i].fallbackFollow);
            }

            if (force || currentLookTargets[i] != newLook)
            {
                currentLookTargets[i] = newLook;
                rig.LookAt = newLook;
            }

            if (force || currentFollowTargets[i] != newFollow)
            {
                currentFollowTargets[i] = newFollow;
                rig.Follow = newFollow;
            }
        }
    }

    private Transform GetBestTarget(Transform primary, Transform fallback)
    {
        if (IsValidTarget(primary))
            return primary;

        if (IsValidTarget(fallback))
            return fallback;

        return null;
    }

    private bool IsValidTarget(Transform target)
    {
        return target != null && target.gameObject.activeInHierarchy;
    }

    #endregion

    #region Público

    public void SetOverrideTarget(Transform newTarget)
    {
        overrideTarget = newTarget;
        ForceRefreshTargets();
    }

    public void ClearOverrideTarget()
    {
        overrideTarget = null;
        ForceRefreshTargets();
        
        // Oculta o tooltip ao deselecionar
        if (ToolTip.Instance != null)
            ToolTip.Instance.Hide();
    }

    public void ResetCamera()
    {
        freeLook.m_XAxis.Value = 0f;
        currentYAxisValue = 0.5f;
        freeLook.m_YAxis.Value = currentYAxisValue;
        SetZoom(10f);
    }

    public bool IsRotating()
    {
        return isRotating;
    }

    public void ForceRefreshTargets()
    {
        RefreshRigTargets(true);
    }

    public void SetYSensitivity(float multiplier)
    {
        yAxisSensitivityMultiplier = Mathf.Clamp(multiplier, 0.01f, 1f);
    }

    public void SetYClampSpeed(float clampSpeed)
    {
        yAxisClampSpeed = Mathf.Clamp(clampSpeed, 0.001f, 0.1f);
    }

    public void SetHorizontalComposition(float screenX)
    {
        float clampedScreenX = Mathf.Clamp(screenX, 0.1f, 0.9f);
        for (int index = 0; index < freeLook.m_Orbits.Length; index++)
        {
            CinemachineVirtualCamera rig = freeLook.GetRig(index);
            CinemachineComposer composer =
                rig != null
                    ? rig.GetCinemachineComponent<CinemachineComposer>()
                    : null;
            if (composer != null)
                composer.m_ScreenX = clampedScreenX;
        }
    }

    #endregion
}
