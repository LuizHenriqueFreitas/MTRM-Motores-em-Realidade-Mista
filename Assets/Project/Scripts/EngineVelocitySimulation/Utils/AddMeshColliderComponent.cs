using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AddMeshColliderComponent : MonoBehaviour
{
    public List<GameObject> children;
    
    void Start()
    {
        // Limpa a lista para evitar duplicação
        children.Clear();
        
        // Pega todos os filhos
        foreach(Transform child in this.transform)
        {
            children.Add(child.gameObject);
        }
        
        // Adiciona MeshCollider em cada filho
        foreach(GameObject obj in children)
        {
            // Verifica se já existe um collider
            if (obj.GetComponent<Collider>() == null)
            {
                // Adiciona MeshCollider
                MeshCollider collider = obj.AddComponent<MeshCollider>();
                
                // IMPORTANTE: Atribui a mesh
                MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    // Se a mesh não for Read/Write enabled, cria uma cópia
                    Mesh mesh = meshFilter.sharedMesh;
                    
                    // Para garantir que funciona no build
                    if (!mesh.isReadable)
                    {
                        // Cria uma cópia da mesh
                        Mesh copy = Instantiate(mesh);
                        collider.sharedMesh = copy;
                    }
                    else
                    {
                        collider.sharedMesh = mesh;
                    }
                    
                    // Configura o collider
                    collider.convex = false; // Ou true se precisar
                    collider.isTrigger = false;
                }
                else
                {
                    Debug.LogWarning($"Objeto {obj.name} não tem MeshFilter ou mesh é null");
                }
                
                // Adiciona o OutlineSelectedModel
                obj.AddComponent<OutlineSelectedModel>();
            }
            else
            {
                Debug.Log($"Objeto {obj.name} já tem collider");
            }
        }
    }
}