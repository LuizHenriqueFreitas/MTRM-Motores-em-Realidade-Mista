using System.Collections.Generic;
using UnityEngine;

public class AddMeshColliderComponent : MonoBehaviour
{
    [SerializeField] private List<GameObject> children = new List<GameObject>();

    private void Start()
    {
        children.Clear();

        foreach (Renderer childRenderer in GetComponentsInChildren<Renderer>(true))
        {
            GameObject child = childRenderer.gameObject;
            MeshFilter meshFilter = child.GetComponent<MeshFilter>();

            // Objetos organizadores e âncoras não possuem malha e não devem
            // receber componentes que exigem Renderer/Collider.
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;

            children.Add(child);

            MeshCollider collider = child.GetComponent<MeshCollider>();
            if (collider == null)
                collider = child.AddComponent<MeshCollider>();

            collider.sharedMesh = meshFilter.sharedMesh;

            if (child.GetComponent<OutlineSelectedModel>() == null)
                child.AddComponent<OutlineSelectedModel>();
        }
    }
}
