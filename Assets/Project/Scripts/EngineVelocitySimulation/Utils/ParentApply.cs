using System.Collections.Generic;
using UnityEngine;

public class ParentApply : MonoBehaviour
{
    public List<GameObject> chield;

    void Start()
    {
        foreach (GameObject c in chield)
        {
            c.transform.SetParent(this.transform);
            c.transform.localEulerAngles = transform.localPosition;
        }
    }

    void Update()
    {
        
    }
}
