using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace VRSYS.Meta.Bodytracking
{
    public class BodyTrackingSerializer : NetworkBehaviour
    {
        #region Member Variables

        [Tooltip("List of body tracking related behaviours that will be destroyed for remote avatars.")]
        [SerializeField] private List<Behaviour> localBehaviours;

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            if (!IsOwner)
            {
                for(int i = 0; i < localBehaviours.Count; i++)
                    Destroy(localBehaviours[i]);
            }
        }

        private void Update()
        {
            if (IsOwner)
            {
                SerializeBodyTracking();
            }
        }

        #endregion

        #region Custom Methods

        private void SerializeBodyTracking()
        {
            
        }

        private void DeserializeBodyTracking()
        {
            
        }

        #endregion
    }
}
