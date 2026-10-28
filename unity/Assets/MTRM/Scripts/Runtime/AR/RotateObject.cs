using UnityEngine;

public class RotateObject : MonoBehaviour
{
    [SerializeField]
    private float rotationSpeed = 0.2f;

    void Update()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Moved)
            {
                float rot = touch.deltaPosition.x * rotationSpeed;

                transform.Rotate(0, -rot, 0, Space.World);
            }
        }
    }
}