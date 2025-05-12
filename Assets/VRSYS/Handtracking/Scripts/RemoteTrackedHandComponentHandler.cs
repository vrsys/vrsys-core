using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


namespace VRSYS.HandTracking
{
    public class RemoteTrackedHandComponentHandler : MonoBehaviour
    {
        #region Member Variables

        public List<Component> localHandTrackingComponents = new List<Component>();
        public List<SkinnedMeshRenderer> trackedHandMeshRenderer = new List<SkinnedMeshRenderer>();
        public Material defaultHandMaterial;

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            if(GetComponentInParent<NetworkBehaviour>() != null)
                if (!GetComponentInParent<NetworkBehaviour>().IsOwner)
                {
                    foreach (var component in localHandTrackingComponents)
                    {
                        Destroy(component);
                    }

                    foreach (var renderer in trackedHandMeshRenderer)
                    {
                        renderer.enabled = true;
                        renderer.material = defaultHandMaterial;
                    }
                }
        }

        #endregion
    }
}
