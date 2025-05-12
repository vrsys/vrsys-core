using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.HandTracking
{
    public class TrackedHandSerializer : NetworkBehaviour
    {
        #region Member Variables

        private bool initialized = false;
        
        public List<Transform> handBones = new List<Transform>();

        private NetworkList<Vector3> handBonePositions;
        private NetworkList<Quaternion> handBoneRotations;
            

        #endregion

        #region MonoBehaviour Callbacks

        private void Awake()
        {
            handBonePositions = new NetworkList<Vector3>(writePerm: NetworkVariableWritePermission.Owner);
            handBoneRotations = new NetworkList<Quaternion>(writePerm: NetworkVariableWritePermission.Owner);
        }

        private void Start()
        {
            if (handBones.Count > 0)
            {
                initialized = true;

                if (IsOwner)
                {
                    for(int i = 0; i < handBones.Count; i++)
                    {
                        handBonePositions.Add(handBones[i].localPosition);
                        handBoneRotations.Add(handBones[i].localRotation);
                    }
                }
            }
            else
            {
                ExtendedLogger.LogWarning(GetType().Name, "No bones have been configured.");
            }
        }

        private void Update()
        {
            if (initialized)
            {
                if (IsOwner)
                {
                    for(int i = 0; i < handBones.Count; i++)
                    {
                        handBonePositions[i] = handBones[i].localPosition;
                        handBoneRotations[i] = handBones[i].localRotation;
                    }
                }
                else
                {
                    for(int i = 0; i < handBonePositions.Count; i++)
                    {
                        handBones[i].localPosition = handBonePositions[i];
                        handBones[i].localRotation = handBoneRotations[i];
                    }
                }
            }
        }

        #endregion
    }
}
