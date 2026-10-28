using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RPMController : MonoBehaviour
{
    public MotorController motorController;
    public Slider rpmSelector;
    [SerializeField] public TextMeshProUGUI rpmView;

    private float RPM;
    // Start is called before the first frame update
    void Start()
    {
        if (motorController != null && rpmSelector != null)
        {
            rpmSelector.minValue = motorController.RPMMinimo;
            rpmSelector.maxValue = motorController.RPMMaximo;
            rpmSelector.value = motorController.RPMAtual;
        }

        RPM = rpmSelector.value;
        UpdateLabel();
    }

    // Update is called once per frame
    public void Check()
    {
        if (motorController == null || rpmSelector == null)
            return;

        RPM = rpmSelector.value;
        motorController.DefinirRPM(RPM);
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (rpmView != null)
            rpmView.text = $"{RPM:F0} RPM";
    }
}
