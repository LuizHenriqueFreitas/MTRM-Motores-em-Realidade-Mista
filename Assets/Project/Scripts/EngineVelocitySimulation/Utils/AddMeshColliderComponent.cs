using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AddMeshColliderComponent : MonoBehaviour
{
    public List<GameObject> chields;
    // Start is called before the first frame update
    void Start()
    {
        foreach(Transform chield in this.transform)
        {
            chields.Add(chield.gameObject);
        }
        
        foreach(GameObject c in chields)
        {
            c.AddComponent<MeshCollider>();
            c.AddComponent<OutlineSelectedModel>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
