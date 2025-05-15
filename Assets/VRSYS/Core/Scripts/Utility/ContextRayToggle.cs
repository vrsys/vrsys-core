// VRSYS plugin of Virtual Reality and Visualization Group (Bauhaus-University Weimar)
//  _    ______  _______  _______
// | |  / / __ \/ ___/\ \/ / ___/
// | | / / /_/ /\__ \  \  /\__ \ 
// | |/ / _, _/___/ /  / /___/ / 
// |___/_/ |_|/____/  /_//____/  
//
//  __                            __                       __   __   __    ___ .  . ___
// |__)  /\  |  | |__|  /\  |  | /__`    |  | |\ | | \  / |__  |__) /__` |  |   /\   |  
// |__) /~~\ \__/ |  | /~~\ \__/ .__/    \__/ | \| |  \/  |___ |  \ .__/ |  |  /~~\  |  
//
//       ___               __                                                           
// |  | |__  |  |\/|  /\  |__)                                                          
// |/\| |___ |  |  | /~~\ |  \                                                                                                                                                                                     
//
// Copyright (c) 2023 Virtual Reality and Visualization Group
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//-----------------------------------------------------------------
//   Authors:        Tony Zoeppig
//   Date:           2025
//-----------------------------------------------------------------

using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace VRSYS.Core.Utility
{
    public class ContextRayToggle : MonoBehaviour
    {
        #region Member Variables

        [Header("Ray Interactor")] 
        
        [Tooltip("GameObject that has ray interactor components attached to it.")]
        [SerializeField] private GameObject ray;
        
        // Ray interactor that gets toggled on and off.
        private XRRayInteractor rayInteractor;
        // Ray visual that gets toggled on and off.
        private XRInteractorLineVisual rayVisual;
        // Ray renderer that gets toggled on and off.
        private LineRenderer rayRenderer;


        [Header("Direct Interactor")] 
        
        [Tooltip("GameObject that has direct interactor components attached to it.")]
        [SerializeField] private GameObject virtualHand;

        // Direct interactor that gets toggled on and off.
        private XRDirectInteractor directInteractor;
        // Collider of direct interactor that gets toggled on and off.
        private Collider directInteractorCollider;


        [Header("Raycast Configuration")]
        [Tooltip("If set to true, configured ray origin will be overwritten by ray origin configured on ray interactor. If this is null, raycast origin will be used as fallback.")]
        public bool useInteractorRayOrigin = false;
        [Tooltip("Transform that defines origin and forward direction of raycast.")]
        public Transform raycastOrigin;
        [Tooltip("If set to true, configured raycast distance will be overwritten by raycast distance configured on ray interactor.")]
        public bool useInteractorDistance = false;
        [Tooltip("Distance of raycast check.")]
        [SerializeField] private float raycastDistance;
        [Tooltip("If set to true, configured raycast mask will be overwritten by raycast mask configured on ray interactor.")]
        public bool useInteractorLayers = false;
        [Tooltip("Layer mask used for raycast (selected layers = included in raycast).")]
        [SerializeField] private LayerMask raycastMask;

        
        // Helper bools
        private bool isDirectInteracting = false;
        private bool isRayInteracting = false;

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            if(GetComponent<NetworkObject>() != null)
                if (!GetComponent<NetworkObject>().IsOwner)
                {
                    Destroy(this);
                    return;
                }
            
            Initialize();
        }

        private void Update()
        {
            UpdateRayActive();
        }

        #endregion

        #region Custom Methods

        private void Initialize()
        {
            // get ray interactor components
            rayInteractor = ray.GetComponent<XRRayInteractor>();
            rayVisual = ray.GetComponent<XRInteractorLineVisual>();
            rayRenderer = ray.GetComponent<LineRenderer>();

            // get direct interactor components
            directInteractor = virtualHand.GetComponent<XRDirectInteractor>();
            directInteractorCollider = virtualHand.GetComponent<Collider>();

            // setup raycast configuration
            if (useInteractorRayOrigin && rayInteractor.rayOriginTransform != null)
                raycastOrigin = rayInteractor.rayOriginTransform;
            
            if (useInteractorDistance)
                raycastDistance = rayInteractor.maxRaycastDistance;

            if (useInteractorLayers)
                raycastMask = rayInteractor.raycastMask;

            // register ray interactor events
            rayInteractor.selectEntered.AddListener(OnRaySelectEntered);
            rayInteractor.selectExited.AddListener(OnRaySelectExited);
            
            // register direct interactor events
            directInteractor.hoverEntered.AddListener(OnDirectHoverEntered);
            directInteractor.hoverExited.AddListener(OnDirectHoverExited);
        }

        private void UpdateRayActive()
        {
            if(isDirectInteracting) // if direct interactor is hovering --> assume direct interaction is intended
                ToggleRayInteractor(false);
            else if(!isRayInteracting) // if is ray interacting --> keep ray active until interacting terminated, else perform raycast and toggle ray based on result
            {
               ToggleRayInteractor(PerformRaycast());
            }
        }

        private bool PerformRaycast()
        {
            if (Physics.Raycast(raycastOrigin.position, raycastOrigin.forward, raycastDistance, raycastMask))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void ToggleRayInteractor(bool active)
        {
            if (rayInteractor.enabled != active)
            {
                rayInteractor.enabled = active;
                rayVisual.enabled = active;
                rayRenderer.enabled = active;

                directInteractor.enabled = !active;
                directInteractorCollider.enabled = !active;
            }
        }

        private void OnRaySelectEntered(SelectEnterEventArgs arg0) => UpdateIsInteracting(true);

        private void OnRaySelectExited(SelectExitEventArgs arg0) => UpdateIsInteracting(false);

        private void UpdateIsInteracting(bool interacting) => isRayInteracting = interacting;

        private void OnDirectHoverEntered(HoverEnterEventArgs arg0) => UpdateIsDirectInteracting(true);

        private void OnDirectHoverExited(HoverExitEventArgs arg0) => UpdateIsDirectInteracting(false);

        private void UpdateIsDirectInteracting(bool interacting) => isDirectInteracting = interacting;

        #endregion
    }
}
