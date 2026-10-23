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
//   Authors:        Tony Jan Zoeppig, Sebastian Muehlhaus, Ephraim Schott
//   Date:           2025
//-----------------------------------------------------------------

using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace VRSYS.Core.Interaction
{
    public class InteractableNetworkState : NetworkBehaviour
    {
        #region Member Variables

        [HideInInspector] public NetworkVariable<bool> isGrabbed = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Header("Ownership Return")]
        [Tooltip("After a release the owner keeps simulating the object and hands it back to the server once it has " +
                 "stayed still for this many seconds.")]
        [SerializeField] private float _settleTime = 0.3f;
        [Tooltip("Movement per physics step (metres) below which the object counts as still.")]
        [SerializeField] private float _settlePositionThreshold = 0.001f;
        [Tooltip("Rotation per physics step (degrees) below which the object counts as still.")]
        [SerializeField] private float _settleRotationThreshold = 0.5f;

        [Header("Debugging")]
        [SerializeField] private bool _getOwnership = false;

        private Coroutine _returnOwnershipRoutine;

        #endregion

        #region MonoBehaviour Callbacks

        private void Update()
        {
            if (_getOwnership)
            {
                SwitchOwnerRpc(NetworkManager.LocalClientId);
                _getOwnership = false;
            }
        }

        #endregion

        #region Custom Methods

        public void UpdateIsGrabbed(bool isGrabbed)
        {
            if(!IsOwner)
                SwitchOwnerRpc(NetworkManager.LocalClientId);

            StopReturnOwnership();
            UpdateIsGrabbedRpc(isGrabbed);

            // The releasing owner keeps simulating the object (e.g. a throw) and hands it back to the server once it
            // has come to rest, so the server drives it again (e.g. during a replay). NetworkTransform does not carry
            // velocity, so returning it right away would make the server continue the object from rest.
            if (!isGrabbed && !IsServer)
                _returnOwnershipRoutine = StartCoroutine(ReturnOwnershipWhenSettled());
        }

        private void StopReturnOwnership()
        {
            if (_returnOwnershipRoutine == null)
                return;

            StopCoroutine(_returnOwnershipRoutine);
            _returnOwnershipRoutine = null;
        }

        private IEnumerator ReturnOwnershipWhenSettled()
        {
            Transform t = transform;
            Vector3 lastPosition = t.position;
            Quaternion lastRotation = t.rotation;
            float stillTime = 0f;

            while (stillTime < _settleTime)
            {
                yield return new WaitForFixedUpdate();

                // Someone else grabbed it meanwhile: it is no longer ours to return.
                if (!IsSpawned || !IsOwner)
                {
                    _returnOwnershipRoutine = null;
                    yield break;
                }

                bool still = (t.position - lastPosition).sqrMagnitude <=
                             _settlePositionThreshold * _settlePositionThreshold &&
                             Quaternion.Angle(t.rotation, lastRotation) <= _settleRotationThreshold;
                stillTime = still ? stillTime + Time.fixedDeltaTime : 0f;
                lastPosition = t.position;
                lastRotation = t.rotation;
            }

            _returnOwnershipRoutine = null;
            ReturnOwnershipRpc();
        }

        #endregion

        #region RPCs

        [Rpc(SendTo.Server)]
        private void UpdateIsGrabbedRpc(bool isGrabbed)
        {
            this.isGrabbed.Value = isGrabbed;
        }

        [Rpc(SendTo.Server)]
        private void ReturnOwnershipRpc(RpcParams rpcParams = default)
        {
            // Ignore a late request: the object was grabbed again or changed hands meanwhile.
            if (rpcParams.Receive.SenderClientId != OwnerClientId || isGrabbed.Value)
                return;

            NetworkObject.RemoveOwnership();
        }
        
        [Rpc(SendTo.Server)]
        private void SwitchOwnerRpc(ulong newOwner)
        {
            NetworkObject.ChangeOwnership(newOwner);
        }

        #endregion
    }
}