using TMPro;
using UnityEngine;

public class MotorUIController : MonoBehaviour
{
    public TextMeshProUGUI buttonText;

    private ToggleAnimation motor;

    public void SetMotor(ToggleAnimation newMotor)
    {
        motor = newMotor;
    }

    public void ToggleMotor()
    {
        if (motor == null) return;

        motor.Toggle();

        if (buttonText.text == "Expandir")
            buttonText.text = "Montar";
        else
            buttonText.text = "Expandir";
    }
}