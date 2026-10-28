using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StateModelManager : MonoBehaviour
{
    [SerializeField] public TextMeshProUGUI buttonLigar;
    public GameObject SliderShow;
    public GameObject ExpandirButton;

    public GameObject MotorExpande;
    public GameObject MotorLigaDesliga;
    
    //coisas desativadas durante o motor estar ligado
    public List<GameObject> EsconderPartes;

    private bool ligado = false; //false = desligado | true = ligado

    public void SetExperimentAccess(bool available)
    {
        if (ExpandirButton != null)
            ExpandirButton.SetActive(available);
        if (SliderShow != null)
            SliderShow.SetActive(available && ligado);
        if (buttonLigar != null && buttonLigar.transform.parent != null)
            buttonLigar.transform.parent.gameObject.SetActive(available);
        gameObject.SetActive(available);
    }

    void Start()
    {
        // O estado inicial não depende mais de overrides de prefab/cena:
        // somente a representação de exploração fica visível.
        ligado = false;
        if (MotorExpande != null)
            MotorExpande.SetActive(true);
        if (MotorLigaDesliga != null)
            MotorLigaDesliga.SetActive(false);
        if (ExpandirButton != null)
            ExpandirButton.SetActive(true);
        if (SliderShow != null)
            SliderShow.SetActive(false);
        if (buttonLigar != null)
            buttonLigar.text = "Ligar";
    }

    // Update is called once per frame
    public void LigaDesliga()
    {
        ligado =  !ligado;
        ExperimentEventRecorder.Record(
            "motor_power_changed",
            new ExperimentEventData
            {
                objectId = gameObject.name,
                textValue = ligado ? "on" : "off"
            });

        if (!MotorLigaDesliga.activeSelf)
        {
            MotorExpande.SetActive(false);
            MotorLigaDesliga.SetActive(true);
            ExpandirButton.SetActive(true);
            SliderShow.SetActive(false);
        }

        if(ligado)
        {
            MotorExpande.SetActive(false);
            MotorLigaDesliga.SetActive(true);
            buttonLigar.text = "Desligar";
            ExpandirButton.SetActive(false);
            SliderShow.SetActive(true);
        }
        else
        {
            MotorLigaDesliga.SetActive(false);
            MotorExpande.SetActive(true);
            buttonLigar.text = "Ligar";
            ExpandirButton.SetActive(true);
            SliderShow.SetActive(false);
        }
    }
}
