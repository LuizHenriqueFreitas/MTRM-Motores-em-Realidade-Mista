using UnityEngine;

public class SpringAnimation : MonoBehaviour
{
    public float compressionAmount = 0.03f;
    private Vector3 originalScale;
    private Vector3 originalPosition;
    
    void Start()
    {
        originalScale = transform.localScale;
        originalPosition = transform.localPosition;
    }
    
    public void SetCompression(float factor) // 0-1
    {
        // Compress spring
        Vector3 pos = originalPosition;
        pos.z -= factor * compressionAmount;
        transform.localPosition = pos;
        
        // Scale spring
        Vector3 scale = originalScale;
        scale.z = originalScale.z * (1f - factor * 0.3f);
        transform.localScale = scale;
    }
}