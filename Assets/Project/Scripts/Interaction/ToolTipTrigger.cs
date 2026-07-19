using TMPro;
using UnityEngine;

public class ToolTipTrigger : MonoBehaviour
{
    [SerializeField] private string titleText;
    [TextArea(2,4)]
    [SerializeField] private string descriptionText;

    public void ShowObject()
    {
        ToolTip.Instance.Show(titleText, descriptionText);
    }

    private void OnMouseDown()
    {
        ShowObject();
    }
}
