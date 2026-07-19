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

    void Start()
    {
    }

    // Update is called once per frame
    public void LigaDesliga()
    {
        ligado =  !ligado;

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
