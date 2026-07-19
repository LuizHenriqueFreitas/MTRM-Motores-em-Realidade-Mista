using TMPro;
using UnityEngine;

public class ToggleAnimation : MonoBehaviour
{
    public Animator animator;
    [SerializeField] public TextMeshProUGUI buttonExpandir;
    public GameObject buttonLigar;
    public GameObject Modelo3DNecessario;
    public GameObject Modelo3DParalelo;
    private bool expanded = false;

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    public void Toggle()
    {
        expanded = !expanded;

        if (!Modelo3DNecessario.activeSelf)
        {
            //animator.Play("Default", 0, 0);
            Modelo3DParalelo.SetActive(false);
            Modelo3DNecessario.SetActive(true);
            buttonLigar.SetActive(true);
        }

        if (expanded)
        {
            animator.Play("Explosion", 0, 0); // toca normalmente
            buttonExpandir.text = "Montar";
            buttonLigar.SetActive(false);
        }
        else
        {
            animator.Play("Build", 0, 0); // come�a no final
            buttonExpandir.text = "Expandir";
            buttonLigar.SetActive(true);
        }
    }

    private void monta()
    {
        
    }
}
