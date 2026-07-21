using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using Cinemachine;
using System.Collections;
using TMPro;
using UnityEngine.XR.ARCore;

public class CameraManager : MonoBehaviour
{
    [Header("AR Components")]
    public ARSession arSession;
    public ARCameraManager arCameraManager;
    public GameObject arOrigin; // XR Origin
    public GameObject MoverMotorButton;
    
    [Header("Cinemachine Components")]
    public GameObject cinemachineBrain;
    public GameObject mainCamera;
    public GameObject freelookCamera;
    public GameObject inputManager;
    public GameObject cinemachineLight; // NOVO: Luz da câmera Cinemachine
    
    [Header("UI")]
    public Button toggleARButton;
    public GameObject arButtonContainer;
    [SerializeField] private TextMeshProUGUI statusText;
    
    [Header("Settings")]
    public bool startWithAR = false;
    public bool isARActive = false;
    private bool isARSupported = false;

    void Start()
    {
        // Verifica suporte AR
        StartCoroutine(CheckARSupport());
        
        // Configura UI
        SetupUI();
        
        // Inicializa com a câmera padrão
        if (!startWithAR)
        {
            SetActiveCinemachineObjects();
        }
        else
        {
            SetActiveARObjects();
        }
    }
    
    IEnumerator CheckARSupport()
    {
        // Aguarda o ARSession iniciar a verificação
        yield return new WaitForSeconds(0.5f);
        
        #if UNITY_ANDROID && !UNITY_EDITOR
        // Verificação real em dispositivo
        isARSupported = ARSession.state != ARSessionState.Unsupported && 
                        ARSession.state != ARSessionState.NeedsInstall;
        
        if (!isARSupported)
        {
            Debug.Log("AR não suportado neste dispositivo");
            arButtonContainer.SetActive(false);
            statusText.text = "Dispositivo sem suporte AR";
            
            // Garante que está usando Cinemachine
            if (isARActive)
            {
                SetActiveCinemachineObjects();
            }
        }
        else
        {
            arButtonContainer.SetActive(true);
            statusText.text = "Trocar Câmera";
        }
        #else
        // Em editor ou outras plataformas, considera suportado para teste
        isARSupported = true;
        arButtonContainer.SetActive(true);
        #endif
    }
    
    void SetupUI()
    {
        if (toggleARButton != null)
        {
            toggleARButton.onClick.RemoveAllListeners();
            toggleARButton.onClick.AddListener(ToggleCamera);
        }
        
        // Inicialmente esconde o botão AR até verificar suporte
        if (arButtonContainer != null)
        {
            arButtonContainer.SetActive(false);
        }
    }
    
    public void ToggleCamera()
    {
        if (!isARSupported && !isARActive)
        {
            Debug.Log("AR não suportado neste dispositivo");
            return;
        }
        
        if (isARActive)
        {
            SetActiveCinemachineObjects();
        }
        else
        {
            SetActiveARObjects();
        }
    }
    
    void SetActiveARObjects()
    {
        // Ativa AR
        if (arSession != null) arSession.enabled = true;
        if (arOrigin != null) arOrigin.SetActive(true);
        if (arCameraManager != null) arCameraManager.enabled = true;
        
        // Desativa Cinemachine
        if (cinemachineBrain != null) cinemachineBrain.SetActive(false);
        if (mainCamera != null) mainCamera.SetActive(false);
        if (freelookCamera != null) freelookCamera.SetActive(false);
        if (inputManager != null) inputManager.SetActive(false);
        if (cinemachineLight != null) cinemachineLight.SetActive(false);
        
        // Garante que a câmera AR está ativa
        if (arOrigin != null)
        {
            Camera arCamera = arOrigin.GetComponentInChildren<Camera>();
            if (arCamera != null)
            {
                arCamera.enabled = true;
                arCamera.tag = "MainCamera"; // Importante!
            }
        }
        
        if (MoverMotorButton != null) MoverMotorButton.SetActive(true);
        
        isARActive = true;
    }

    void SetActiveCinemachineObjects()
    {
        // Desativa AR
        if (arSession != null) arSession.enabled = false;
        if (arOrigin != null) arOrigin.SetActive(false);
        if (arCameraManager != null) arCameraManager.enabled = false;
        
        // Ativa Cinemachine
        if (cinemachineBrain != null) cinemachineBrain.SetActive(true);
        if (mainCamera != null) mainCamera.SetActive(true);
        if (freelookCamera != null) freelookCamera.SetActive(true);
        if (inputManager != null) inputManager.SetActive(true);
        if (cinemachineLight != null) cinemachineLight.SetActive(true);
        
        // Garante que a câmera principal está ativa
        if (mainCamera != null)
        {
            Camera cam = mainCamera.GetComponent<Camera>();
            if (cam != null) cam.enabled = true;
            mainCamera.tag = "MainCamera";
        }
        
        if (MoverMotorButton != null) MoverMotorButton.SetActive(false);
        
        isARActive = false;
    }
    // Método público para verificar status
    public bool IsARActive()
    {
        return isARActive;
    }
    
    public bool IsARSupported()
    {
        return isARSupported;
    }
}