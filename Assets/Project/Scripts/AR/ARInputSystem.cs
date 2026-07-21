using UnityEngine;
using Cinemachine;

public class ARInputSystem : MonoBehaviour
{
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

    [Header("Seleção de Peças (Click/Touch)")]
    [SerializeField] private LayerMask layerPecas = ~0; 
    [SerializeField] private float maxClickDistance = 100f; 

    [Header("Rotação")]
    [SerializeField] private float rotationSpeed = 0.1f;
    [SerializeField] private float mouseSensitivity = 0.5f;
    [SerializeField] private float touchSensitivity = 0.3f; // Sensibilidade para touch
    [SerializeField] private bool invertX;
    [SerializeField] private bool invertY;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 2f;
    [SerializeField] private float maxZoom = 20f;

    [Header("Targets dos Rigs")]
    [SerializeField] private RingTargets[] ringTargets = new RingTargets[3];

    [Header("Ajustes de Sensibilidade")]
    [SerializeField] private float yAxisSensitivityMultiplier = 0.15f; 
    [SerializeField] private float yAxisClampSpeed = 0.02f; 

    // Controle de Touch
    private float currentZoom;
    private Vector2 lastTouchPosition;
    private Vector2 lastMousePosition;
    private bool isRotating;
    private bool isPinching;
    private float currentYAxisValue;
    private float initialPinchDistance;
    private float initialZoom;

    private readonly Transform[] currentLookTargets = new Transform[3];
    private readonly Transform[] currentFollowTargets = new Transform[3];
    private Transform overrideTarget;

    // Detecção de plataforma
    private bool isMobilePlatform;

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

        // Detecta se é plataforma móvel
        #if UNITY_ANDROID || UNITY_IOS
        isMobilePlatform = true;
        #else
        isMobilePlatform = false;
        #endif
    }

    private void Start()
    {
        // Desativa input names do Cinemachine para controle manual
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
        HandleSelection(); 
        HandleRotation();
        HandleZoom();
        RefreshRigTargets();
    }

    #endregion

    #region Seleção (Click/Touch)

    private void HandleSelection()
    {
        // Para mobile: toque único sem movimento
        if (isMobilePlatform)
        {
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == UnityEngine.TouchPhase.Began)
                {
                    ProcessSelection(touch.position);
                }
            }
        }
        // Para desktop/editor: clique do mouse
        else
        {
            if (Input.GetMouseButtonDown(0))
            {
                ProcessSelection(Input.mousePosition);
            }
        }
    }

    private void ProcessSelection(Vector2 screenPosition)
    {
        if (mainCamera != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, layerPecas))
            {
                if (overrideTarget == hit.transform)
                {
                    ClearOverrideTarget();
                }
                else
                {
                    SetOverrideTarget(hit.transform);
                }
            }
        }
    }

    #endregion

    #region Rotação

    private void HandleRotation()
    {
        if (isMobilePlatform)
        {
            HandleTouchRotation();
        }
        else
        {
            HandleMouseRotation();
        }
    }

    private void HandleTouchRotation()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            // Ignora se o toque começou como seleção (evita conflito)
            if (touch.phase == UnityEngine.TouchPhase.Began)
            {
                // Verifica se clicou em algo antes de iniciar rotação
                Ray ray = mainCamera.ScreenPointToRay(touch.position);
                if (Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, layerPecas))
                {
                    // Se clicou em uma peça, não inicia rotação
                    return;
                }

                isRotating = true;
                lastTouchPosition = touch.position;
            }
            else if (touch.phase == UnityEngine.TouchPhase.Moved && isRotating)
            {
                Vector2 delta = touch.position - lastTouchPosition;
                lastTouchPosition = touch.position;

                ApplyRotation(delta, touchSensitivity);
            }
            else if (touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled)
            {
                isRotating = false;
            }
        }
        else
        {
            // Se tem mais de um toque, para a rotação
            isRotating = false;
        }
    }

    private void HandleMouseRotation()
    {
        if (Input.GetMouseButtonDown(1))
        {
            isRotating = true;
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(1))
        {
            isRotating = false;
        }

        if (!isRotating)
            return;

        Vector2 currentMouse = Input.mousePosition;
        Vector2 delta = currentMouse - lastMousePosition;
        lastMousePosition = currentMouse;

        ApplyRotation(delta, mouseSensitivity);
    }

    private void ApplyRotation(Vector2 delta, float sensitivity)
    {
        // Calcula multiplicador baseado no zoom (perto = sensibilidade baixa)
        float zoomFactor = Mathf.InverseLerp(minZoom, maxZoom, currentZoom);
        float dynamicSensitivity = Mathf.Lerp(0.2f, 1.0f, zoomFactor);
        float finalSensitivity = sensitivity * dynamicSensitivity;

        // Rotação Horizontal (X)
        float deltaX = delta.x * rotationSpeed * finalSensitivity;
        if (invertX)
            deltaX *= -1f;
        freeLook.m_XAxis.Value += deltaX;

        // Rotação Vertical (Y)
        float rawDeltaY = delta.y * rotationSpeed * yAxisSensitivityMultiplier * finalSensitivity;
        if (!invertY)
            rawDeltaY *= -1f;
        rawDeltaY = Mathf.Clamp(rawDeltaY, -yAxisClampSpeed, yAxisClampSpeed);
        
        currentYAxisValue += rawDeltaY;
        currentYAxisValue = Mathf.Clamp01(currentYAxisValue);
        freeLook.m_YAxis.Value = currentYAxisValue;
    }

    #endregion

    #region Zoom

    private void HandleZoom()
    {
        if (isMobilePlatform)
        {
            HandleTouchZoom();
        }
        else
        {
            HandleMouseZoom();
        }
    }

    private void HandleTouchZoom()
    {
        if (Input.touchCount == 2)
        {
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);

            // Início do pinch
            if (touch1.phase == UnityEngine.TouchPhase.Began || touch2.phase == UnityEngine.TouchPhase.Began)
            {
                isPinching = true;
                initialPinchDistance = Vector2.Distance(touch1.position, touch2.position);
                initialZoom = currentZoom;
            }
            // Movimento do pinch
            else if (isPinching && (touch1.phase == UnityEngine.TouchPhase.Moved || touch2.phase == UnityEngine.TouchPhase.Moved))
            {
                float currentPinchDistance = Vector2.Distance(touch1.position, touch2.position);
                float deltaPinch = currentPinchDistance - initialPinchDistance;
                
                // Quanto maior o delta, mais zoom
                float zoomDelta = -deltaPinch * 0.02f; // Ajuste do fator de zoom
                float newZoom = Mathf.Clamp(initialZoom + zoomDelta, minZoom, maxZoom);
                SetZoom(newZoom);
            }
            // Fim do pinch
            else if (touch1.phase == UnityEngine.TouchPhase.Ended || touch2.phase == UnityEngine.TouchPhase.Ended ||
                     touch1.phase == UnityEngine.TouchPhase.Canceled || touch2.phase == UnityEngine.TouchPhase.Canceled)
            {
                isPinching = false;
            }
        }
        else
        {
            isPinching = false;
        }
    }

    private void HandleMouseZoom()
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
        
        // Oculta o tooltip ao deselecionar (se existir)
        // ToolTip.Instance.Hide(); // Comente se não tiver ToolTip
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

    // Para depuração - mostrar status no editor
    private void OnGUI()
    {
        if (Application.isEditor)
        {
            GUILayout.Label($"Touch Count: {Input.touchCount}");
            GUILayout.Label($"Is Rotating: {isRotating}");
            GUILayout.Label($"Is Pinching: {isPinching}");
            GUILayout.Label($"Zoom: {currentZoom:F2}");
            GUILayout.Label($"Y Axis: {currentYAxisValue:F2}");
        }
    }

    #endregion
}