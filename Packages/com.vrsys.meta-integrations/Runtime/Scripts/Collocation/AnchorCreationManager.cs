using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VRSYS.Core.Networking;
using VRSYS.Core.Utility;

namespace VRSYS.Meta.Collocation
{
    public class AnchorCreationManager : MonoBehaviour
    {
        [SerializeField] private GameObject _anchorCreationUI;
        
        [SerializeField] private GameObject _floorPlane;
        [SerializeField] private GameObject _anchorPrefab;
        [SerializeField] private GameObject _anchorPreview;
        [SerializeField] private LineRenderer _rayVisual;
        
        [SerializeField] private InputActionReference _anchorCreationAction;
        [SerializeField] private Transform _userHand;
        
        [SerializeField] private LayerMask anchorLayerMask;
        
        private bool _isAnchorCreationActive;
        private RaycastHit _hit;
        
        private GameObject _anchor;
        private OVRSpatialAnchor _spatialAnchor;
        
        private Action<OVRSpatialAnchor> returnAnchor;
        
        private enum AnchorCreationState
        {
            Idle,
            Aiming,
            Locked
        }

        private AnchorCreationState interactionState;

        private void Start()
        {
            // Setup UI and Interation
            _isAnchorCreationActive = true;
            _anchorCreationAction.action.Enable();
            
            // Place floor plane on ground height of user
            _floorPlane.transform.position = NetworkUser.LocalInstance.transform.position;
        }

        public void SetupAnchorCreationMode(Action<OVRSpatialAnchor> returnAction)
        {
            returnAnchor = returnAction;
            
            // Setup UI and Interation
            _isAnchorCreationActive = true;
            _anchorCreationAction.action.Enable();
            
            // Place floor plane on ground height of user
            _floorPlane.transform.position = NetworkUser.LocalInstance.transform.position;
        }

        private void Update()
        {
            // While UI active, do not process raycast
            if (!_isAnchorCreationActive)
                return;
            
            float input = _anchorCreationAction.action.ReadValue<float>();

            if (input < 0.2f)
            {
                if (interactionState == AnchorCreationState.Locked)
                {
                    CreateAnchor();
                    _rayVisual.enabled = false;
                    _anchorPreview.SetActive(false);
                    return;
                }
                
                if (interactionState != AnchorCreationState.Idle)
                {
                    _rayVisual.enabled = false;
                    _anchorPreview.SetActive(false);
                    
                    interactionState = AnchorCreationState.Idle;
                }
            }
            else if (input >= 0.2f && input < 0.9f)
            {
                if (interactionState == AnchorCreationState.Locked)
                {
                    CreateAnchor();
                    _rayVisual.enabled = false;
                    _anchorPreview.SetActive(false);
                    return;
                }
                
                if (interactionState != AnchorCreationState.Aiming)
                {
                    _rayVisual.enabled = true;
                    _anchorPreview.SetActive(true);
                    
                    interactionState = AnchorCreationState.Aiming;
                }

                UpdateVisuals(input);
            }
            else if (input >= 0.9f)
            {
                if (interactionState != AnchorCreationState.Locked)
                {
                    _rayVisual.enabled = true;
                    _anchorPreview.SetActive(true);
                    
                    interactionState = AnchorCreationState.Locked;
                }
                
                UpdateVisuals(input);
            }
        }

        private void UpdateVisuals(float input)
        {
            _rayVisual.SetPosition(0, _userHand.position);
            
            if (Physics.Raycast(_userHand.position, _userHand.forward,  out RaycastHit hit, anchorLayerMask))
            { 
                _rayVisual.SetPosition(1, hit.point);
                
                if(interactionState == AnchorCreationState.Aiming)
                {
                    // update position only
                    _anchorPreview.transform.position = _hit.point;
                }
                else if(interactionState == AnchorCreationState.Locked)
                {
                    // update rotation only
                    _anchorPreview.transform.LookAt(hit.point);
                }
            }
            else
            {
                _rayVisual.SetPosition(1, _userHand.position + _userHand.forward);
            }
        }
        
        private void CreateAnchor()
        {
            _isAnchorCreationActive = false; // for now disable anchor creation mode
            
            // create a spatial anchor at the hitpoint
            var go = Instantiate(_anchorPrefab, _anchorPreview.transform.position, _anchorPreview.transform.rotation);
            _spatialAnchor = go.AddComponent<OVRSpatialAnchor>();
            SetupSpatialAnchorAsync();
            interactionState = AnchorCreationState.Idle;
        }

        private async void SetupSpatialAnchorAsync()
        {
            // Keep checking for a valid and localized anchor state
            if (!await _spatialAnchor.WhenLocalizedAsync())
            {
                Debug.LogError($"Unable to create anchor.");
                Destroy(_spatialAnchor.gameObject);
                _spatialAnchor = null;
                
                // retry anchor creation
                _isAnchorCreationActive = true;
            }
            else
            {
                // Spatial anchor was created
                _anchorCreationUI.SetActive(true);
            }
        }
        
        /// <summary>
        ///  Callback to User confirming selected anchors
        /// </summary>
        private void ConfirmAnchors()
        {
            // Teardown
            _anchorCreationUI.SetActive(false);
            _anchorCreationAction.action.Disable();
            
            returnAnchor(_spatialAnchor);
        }

        /// <summary>
        ///  Callback to User confirming selected anchors
        /// </summary>
        private void RedoAnchors()
        {
            Destroy(_spatialAnchor.gameObject);
            _spatialAnchor = null;
            _anchorCreationUI.SetActive(false);
            
            // retry anchor creation
            _isAnchorCreationActive = true;
        }
        
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