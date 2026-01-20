using System;
using UnityEngine;
using Meta.XR;

namespace VRSYS.Meta.Collocation
{
    public class AnchorCreationManager : MonoBehaviour
    {
        [SerializeField] private GameObject _anchorPrefab;
        [SerializeField] private GameObject _anchorPreview;
        
        // EXAMPLE from https://developers.meta.com/horizon/documentation/unity/unity-mr-utility-kit-environment-raycast
        
        // public Transform rightControllerAnchor;
        // public GameObject prefabToPlace;
        //
        // private void Update()
        // {
        //     if (OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger))
        //     {
        //         var ray = new Ray(
        //             rightControllerAnchor.position,
        //             rightControllerAnchor.forward
        //         );
        //
        //         TryPlace(ray);
        //     }
        // }
        //
        // private void TryPlace(Ray ray)
        // {
        //     if (Raycast(ray, out var hit))
        //     {
        //         var objectToPlace = Instantiate(prefabToPlace);
        //         objectToPlace.transform.SetPositionAndRotation(
        //             hit.point,
        //             Quaternion.LookRotation(hit.normal, Vector3.up)
        //         );
        //
        //         objectToPlace.AddComponent<OVRSpatialAnchor>();
        //     }
        // }
    }
}