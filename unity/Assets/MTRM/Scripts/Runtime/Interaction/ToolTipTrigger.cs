using TMPro;
using UnityEngine;

public class ToolTipTrigger : MonoBehaviour
{
    [SerializeField] private string titleText;
    [TextArea(2,4)]
    [SerializeField] private string descriptionText;

    public string TitleText => titleText;
    public string EffectiveTitle
    {
        get
        {
            ComponentTag tag = GetComponent<ComponentTag>();
            return tag != null &&
                   !string.IsNullOrWhiteSpace(tag.DisplayName)
                ? tag.DisplayName
                : titleText;
        }
    }
    public string EffectiveDescription
    {
        get
        {
            ComponentTag tag = GetComponent<ComponentTag>();
            return tag != null &&
                   !string.IsNullOrWhiteSpace(tag.EducationalDescription)
                ? tag.EducationalDescription
                : descriptionText;
        }
    }

    public void ShowObject()
    {
        ToolTip.Instance.Show(EffectiveTitle, EffectiveDescription);
    }

    private void OnMouseDown()
    {
        if (Input.GetMouseButtonDown(0))
            ShowObject();
    }

}
