using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace VRSYS.Core.Utility
{
    public class ContextRayToggle : MonoBehaviour
    {
        #region Member Variables

        [Header("Ray Components")] 
        
        [Tooltip("Ray interactor that gets toggled on and off.")]
        public XRRayInteractor rayInteractor;
        [Tooltip("Ray visual that gets toggled on and off.")]
        public XRInteractorLineVisual rayVisual;
        [Tooltip("Ray renderer that gets toggled on and off.")]
        public LineRenderer rayRenderer;

        [Header("Direct Interactor Components")]

        [Tooltip("Direct interactor that gets toggled on and off.")]
        public XRDirectInteractor directInteractor;
        [Tooltip("Collider of direct interactor that gets toggled on and off.")]
        public Collider directInteractorCollider;

        [Header("Raycast Configuration")] 
        
        [Tooltip("Transform that defines origin and forward direction of raycast.")]
        public Transform raycastOrigin;
        [Tooltip("Distance of raycast check.")]
        public float raycastDistance;
        [Tooltip("If set to true, configured raycast mask will be overwritten by raycast mask configured on ray interactor.")]
        public bool useInteractorLayers = false;
        [Tooltip("Layer mask used for raycast (selected layers = included in raycast).")]
        public LayerMask raycastMask;

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

            if (useInteractorLayers)
                raycastMask = rayInteractor.raycastMask;
        }

        private void Update()
        {
            PerformRaycast();
        }

        #endregion

        #region Custom Methods

        private void PerformRaycast()
        {
            if (Physics.Raycast(raycastOrigin.position, raycastOrigin.forward, raycastDistance, raycastMask))
            {
                ToggleRayInteractor(true);
            }
            else
            {
                ToggleRayInteractor(false);
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

        #endregion
    }
}
