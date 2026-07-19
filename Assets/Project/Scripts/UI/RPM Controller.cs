using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RPMControlle : MonoBehaviour
{
    public MotorController motorController;
    public Slider rpmSelector;
    [SerializeField] public TextMeshProUGUI rpmView;

    private float RPM;
    // Start is called before the first frame update
    void Start()
    {
        RPM = rpmSelector.value;
    }

    // Update is called once per frame
    public void Check()
    {
        RPM = rpmSelector.value;
        motorController.setRpm(RPM);
        rpmView.text = RPM + " RPM";
    }
}
