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
//   Authors:        Sebastian Muehlhaus
//   Date:           2023
//-----------------------------------------------------------------

using Unity.Netcode;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Navigation
{
    public class NavigationPlatformLink : NetworkBehaviour
    {
        [Header("Pre-Runtime Configuration")]
        public NavigationPlatformSpawnInfo spawnInfo;
        
        [Header("Runtime Configuration")]
        public Transform platformTransform;

        private Transform lastPlatformTransform;

        private bool processingServerRequest = false;

        private void Start()
        {
            if (!IsOwner)
                return;
            if (spawnInfo == null)
                throw new System.NullReferenceException(GetType().Name + "." + nameof(spawnInfo) + " cannot be null");
            platformTransform = GameObject.Find(spawnInfo.platformName)?.transform;
            RequestApplyParentTransform();
        }

        private void Update()
        {
            if (!IsOwner || processingServerRequest)
                return;
            if (platformTransform != lastPlatformTransform)
                RequestApplyParentTransform();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            NotifyLeavePlatform(platformTransform);
        }

        private void RequestApplyParentTransform()
        {
            if(platformTransform != null)
            {
                ExtendedLogger.LogInfo(GetType().Name, "request apply parent transform " + platformTransform.name);
                processingServerRequest = true;
                SetTargetParentTransformServerRpc(platformTransform.GetComponent<NetworkObject>(), NetworkManager.Singleton.LocalClientId);
            }
        }

        [ServerRpc]
        private void SetTargetParentTransformServerRpc(NetworkObjectReference targetParentRef, ulong clientId)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            
            if (targetParentRef.TryGet(out NetworkObject targetParent))
            {
                NetworkObject.TrySetParent(targetParent, false);
                platformTransform = targetParent.transform;
            }
            else
            {
                NetworkObject.TryRemoveParent(false);
                platformTransform = null;
            }
            
            SetTargetParentSetClientRpc(targetParentRef, clientId);
        }

        [ClientRpc]
        private void SetTargetParentSetClientRpc(NetworkObjectReference targetParentRef, ulong originClientId)
        {
            if (targetParentRef.TryGet(out NetworkObject targetParent))
                platformTransform = targetParent.transform;
            else
                platformTransform = null;
            
            FinalizeApplyParentTransform();
            
            if(originClientId == NetworkManager.LocalClientId)
                processingServerRequest = false;
        }

        private void FinalizeApplyParentTransform()
        {
            NotifyLeavePlatform(lastPlatformTransform);
            NotifyEnterPlatform(platformTransform);
            lastPlatformTransform = platformTransform;
        }

        private void NotifyLeavePlatform(Transform t)
        {
            if (t == null)
                return;
            foreach(var p in t.GetComponentsInChildren<INavigationPlatformCallbacks>())
                p.OnLeavePlatform(this);
        }

        private void NotifyEnterPlatform(Transform t)
        {
            if (t == null)
                return;
            foreach (var p in t.GetComponentsInChildren<INavigationPlatformCallbacks>())
                p.OnEnterPlatform(this);
        }
    }
}
