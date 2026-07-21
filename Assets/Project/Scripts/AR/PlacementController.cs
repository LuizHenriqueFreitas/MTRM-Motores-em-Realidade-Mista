using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class PlacementController : MonoBehaviour
{
    [Header("Motor")]
    [SerializeField]
    private GameObject motor;

    [Header("Pivôs do Modelo")]
    [SerializeField]
    private GameObject pivoA; // Pivô principal
    [SerializeField]
    private GameObject pivoB; // Pivô alternativo

    [Header("AR Managers")]
    [SerializeField]
    private ARPlaneManager planeManager;

    private ARRaycastManager arRaycastManager;

    private bool placementMode = true;

    private Vector3 offsetPosition = new Vector3(0f, 0.02f, 0f);

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    // Cache do pivô ativo atual
    private GameObject currentActivePivot;

    void Awake()
    {
        arRaycastManager = GetComponent<ARRaycastManager>();
        
        // Inicializa com o pivô ativo
        UpdateActivePivot();
    }

    void Start()
    {
        // Se o motor já estiver ativo, atualiza os pivôs
        if (motor != null && motor.activeSelf)
        {
            UpdateActivePivot();
        }
    }

    void Update()
    {
        if (!placementMode)
            return;

        if (Input.touchCount == 0)
            return;

        Touch touch = Input.GetTouch(0);

        if (touch.phase != TouchPhase.Began)
            return;

        if (arRaycastManager.Raycast(touch.position, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            if (!motor.activeSelf)
                motor.SetActive(true);

            // Posiciona o motor com a rotação correta (em pé)
            motor.transform.position = hitPose.position + offsetPosition;
            motor.transform.rotation = Quaternion.identity;
            
            // Define o tamanho para 1/4 do original
            motor.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);

            // Atualiza qual pivô está ativo
            UpdateActivePivot();

            placementMode = false;

            // Desativa visualização dos planos
            planeManager.enabled = false;

            foreach (ARPlane plane in planeManager.trackables)
            {
                plane.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Verifica qual pivô está ativo e atualiza o cache
    /// </summary>
    private void UpdateActivePivot()
    {
        // Verifica se o Pivô A está ativo
        if (pivoA != null && pivoA.activeSelf)
        {
            currentActivePivot = pivoA;
            Debug.Log("Pivô A está ativo");
        }
        // Se o Pivô A não está ativo, usa o Pivô B
        else if (pivoB != null)
        {
            currentActivePivot = pivoB;
            Debug.Log("Pivô B está ativo (Pivô A inativo)");
        }
        else
        {
            currentActivePivot = null;
            Debug.LogWarning("Nenhum pivô disponível!");
        }

        // Se o motor estiver ativo, aplica a posição do pivô atual
        if (motor != null && motor.activeSelf && currentActivePivot != null)
        {
            // Opcional: Atualiza a posição do motor baseado no pivô ativo
            // motor.transform.position = currentActivePivot.transform.position;
        }
    }

    /// <summary>
    /// Método público para trocar o pivô ativo programaticamente
    /// </summary>
    public void SwitchPivot(bool usePivotA)
    {
        if (usePivotA && pivoA != null)
        {
            pivoA.SetActive(true);
            if (pivoB != null) pivoB.SetActive(false);
        }
        else if (!usePivotA && pivoB != null)
        {
            pivoB.SetActive(true);
            if (pivoA != null) pivoA.SetActive(false);
        }

        UpdateActivePivot();
    }

    /// <summary>
    /// Retorna o pivô ativo atual
    /// </summary>
    public GameObject GetCurrentPivot()
    {
        return currentActivePivot;
    }

    /// <summary>
    /// Verifica se o Pivô A está ativo
    /// </summary>
    public bool IsPivotAActive()
    {
        return pivoA != null && pivoA.activeSelf;
    }

    /// <summary>
    /// Verifica se o Pivô B está ativo
    /// </summary>
    public bool IsPivotBActive()
    {
        return pivoB != null && pivoB.activeSelf;
    }

    public void EnablePlacement()
    {
        placementMode = true;

        // Volta a mostrar os planos
        planeManager.enabled = true;

        foreach (ARPlane plane in planeManager.trackables)
        {
            plane.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Força a atualização dos pivôs (útil quando os pivôs mudam em runtime)
    /// </summary>
    public void RefreshPivots()
    {
        UpdateActivePivot();
    }

    #region Debug

    private void OnGUI()
    {
        if (Application.isEditor)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Placement Mode: {placementMode}");
            GUILayout.Label($"Pivô Ativo: {(currentActivePivot != null ? currentActivePivot.name : "Nenhum")}");
            GUILayout.Label($"Pivô A Ativo: {IsPivotAActive()}");
            GUILayout.Label($"Pivô B Ativo: {IsPivotBActive()}");
            GUILayout.EndArea();
        }
    }

    #endregion
}