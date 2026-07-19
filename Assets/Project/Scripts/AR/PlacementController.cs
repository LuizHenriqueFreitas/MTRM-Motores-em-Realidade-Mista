using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class PlacementController : MonoBehaviour
{
    [Header("Motor")]
    [SerializeField]
    private GameObject motor;

    [Header("AR Managers")]
    [SerializeField]
    private ARPlaneManager planeManager;

    private ARRaycastManager arRaycastManager;

    private bool placementMode = true;

    private Vector3 offsetPosition = new Vector3(0f, 0.02f, 0f);

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();


    void Awake()
    {
        arRaycastManager = GetComponent<ARRaycastManager>();
    }


    public void EnablePlacement()
    {
        placementMode = true;

        // volta a mostrar os planos
        planeManager.enabled = true;

        foreach (ARPlane plane in planeManager.trackables)
        {
            plane.gameObject.SetActive(true);
        }
    }


    void Update()
    {
        if (!placementMode)
            return;

        if (Input.touchCount == 0)
            return;

        Touch touch = Input.GetTouch(0);

        if (touch.phase != TouchPhase.Began)
            return;

        if (arRaycastManager.Raycast(touch.position, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            if (!motor.activeSelf)
                motor.SetActive(true);

            motor.transform.position = hitPose.position + offsetPosition;
            motor.transform.rotation = Quaternion.Euler(-91, 133, 47);

            placementMode = false;

            // desativa visualização dos planos
            planeManager.enabled = false;

            foreach (ARPlane plane in planeManager.trackables)
            {
                plane.gameObject.SetActive(false);
            }
        }
    }
}