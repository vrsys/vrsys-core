using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace VRSYS.Meta.Bodytracking
{
    public class NetworkedBodyTrackingHandler : MonoBehaviour
    {
        #region Member Variables

        [Tooltip("List of body tracking related behaviours that will be destroyed for remote avatars.")]
        [SerializeField] private List<Behaviour> localBehaviours;

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            if(GetComponentInParent<NetworkObject>() != null)
                if (!GetComponentInParent<NetworkObject>().IsOwner)
                {
                    Destroy(this);
                    return;
                }
        }

        #endregion
    }
}
