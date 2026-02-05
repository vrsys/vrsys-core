using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VRSYS.Core.Networking;
using VRSYS.Core.Utility;

namespace VRSYS.Meta.Collocation
{
    public class AnchorCreationManager : MonoBehaviour
    {
        [HideInInspector] public UnityEvent<Vector3,Quaternion> OnUserDefinedAnchor = new UnityEvent<Vector3,Quaternion>();
        
        [SerializeField] private ConfirmationUI _confirmationUIPrefab;
        [SerializeField] private GameObject _floorPlanePrefab;
        [SerializeField] private GameObject _anchorPrefab;

        [SerializeField] private InputActionProperty _anchorCreationAction;
        [SerializeField] private LayerMask anchorLayerMask;

        [SerializeField] private LineRenderer _rayVisual;
        [SerializeField] private Transform _userHand;
        
        private GameObject _floorPlane;
        private ConfirmationUI _confirmationUI;
        private GameObject _anchorPreview;
        private bool _isAnchorCreationActive;
        
        private enum AnchorCreationState
        {
            Idle,
            Aiming,
            Locked
        }
        private AnchorCreationState interactionState;
        

        public void SetupAnchorCreationMode()
        {
            // Setup UI and Interation
            _isAnchorCreationActive = true;
            _anchorCreationAction.action.Enable();
            _anchorPreview = Instantiate(_anchorPrefab);
            _anchorPrefab.SetActive(false);
            
            _confirmationUI = Instantiate(_confirmationUIPrefab);
            _confirmationUI.Initialize(AnchorConfirmed, RedoAnchor);
            
            // Place floor plane on ground height of user
            _floorPlane = Instantiate(_floorPlanePrefab, NetworkUser.LocalInstance.transform.position, Quaternion.identity);
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
                    ShowAnchorConfirmationUI();
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
                    ShowAnchorConfirmationUI();
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
            
            if (Physics.Raycast(_userHand.position, _userHand.forward,  out RaycastHit hit, 100f, anchorLayerMask))
            {
                _rayVisual.SetPosition(1, hit.point);
                
                if(interactionState == AnchorCreationState.Aiming)
                {
                    // update position
                    _anchorPreview.transform.position = hit.point;
                }
                else if(interactionState == AnchorCreationState.Locked)
                {
                    // rotate
                    _anchorPreview.transform.LookAt(hit.point, _floorPlane.transform.up);
                }
            }
            else
            {
                _rayVisual.SetPosition(1, _userHand.position + _userHand.forward);
            }
        }
        
        private void ShowAnchorConfirmationUI()
        {
            _isAnchorCreationActive = false; // for now disable anchor creation mode
            _rayVisual.enabled = false; // do not show ray
            
            interactionState = AnchorCreationState.Idle;
            
            _confirmationUI.Show();
        }
        
        /// <summary>
        ///  Callback to User confirming selected anchors
        /// </summary>
        private void AnchorConfirmed()
        {
            // Teardown
            _anchorPreview.SetActive(false);
            _rayVisual.enabled = false;
            
            Destroy(_confirmationUI.gameObject);
            Destroy(_floorPlane);
            
            _anchorCreationAction.action.Disable();
            
            OnUserDefinedAnchor.Invoke(_anchorPreview.transform.position, _anchorPreview.transform.rotation);
        }

        /// <summary>
        ///  Callback to User confirming selected anchors
        /// </summary>
        private void RedoAnchor()
        {
            _confirmationUI.Hide();
            _anchorPreview.SetActive(false);
            
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