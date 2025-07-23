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
//   Authors:        Tony Jan Zoeppig, Sebastian Muehlhaus
//   Date:           2025
//-----------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Events;
using VRSYS.Core.Avatar;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Networking
{
    [RequireComponent(typeof(NetworkObject), typeof(AvatarAnatomy))]
    public class NetworkUser : NetworkBehaviour
    {
        #region Member Variables

        // Local User Instance
        public static NetworkUser LocalInstance;
        
        public AvatarAnatomy avatarAnatomy { get; private set; }
        
        public Transform head => avatarAnatomy.head;

        public bool verbose = false;

        [Tooltip("Local components, which are destroyed on remote user instances (e.g. Camera, Audio Listener, Tracking, ...)")]
        public List<Behaviour> localBehaviours = new List<Behaviour>();

        [HideInInspector] public NetworkVariable<Color> userColor = new(default, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        
        [HideInInspector] public NetworkVariable<FixedString32Bytes> userName = new (default, 
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        private NetworkVariable<int> userRoleIdx = new(-1, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        [HideInInspector] public UserRole userRole;

        [HideInInspector] public ulong userID = 0;

        // events
        public UnityEvent onLocalUserSetup = new UnityEvent();

        [Header("Local Connection Management")]
        public bool disconnectNow = false;
        
        private bool isDisconnecting = false;

        private NetworkUserSpawnInfo spawnInfo;
        
        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            avatarAnatomy = GetComponent<AvatarAnatomy>();
            
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            spawnInfo = ConnectionManager.Instance.userSpawnInfo;

            if (IsOwner)
            {
                // Set local user instance
                LocalInstance = this;
                
                // handle user name workflow
                if (spawnInfo.userName.Equals(""))
                {
                    userName.Value = "Player_" + Random.Range(0, 1000);
                }
                else
                {
                    userName.Value = spawnInfo.userName;
                }
                
                // handle user color workflow
                userColor.Value = spawnInfo.userColor;
                
                // handle user role workflow
                userRole = spawnInfo.userRole;
                userRoleIdx.Value = ConnectionManager.Instance.userRoleList.GetUserRoleIdx(userRole);

                onLocalUserSetup.Invoke();

                var networkUserLifecycleCallbackTargets = FindObjectsOfType<MonoBehaviour>(true).OfType<INetworkUserCallbacks>();
                foreach (var target in networkUserLifecycleCallbackTargets)
                    target.OnLocalNetworkUserSetup();
            }
            else
            {
                // register user name changed event
                userName.OnValueChanged += UpdateUserNameLabel;
                
                // register user color changed event
                userColor.OnValueChanged += UpdateUserColor;
                
                // register user role idx changed event
                userRoleIdx.OnValueChanged += UpdateUserRole;
                
                // Remove unnecessary local components such as tracking
                while(localBehaviours.Count > 0)
                {
                    DestroyImmediate(localBehaviours[0]);
                    localBehaviours.RemoveAt(0);
                }
                
                var networkUserCallbackTargets = FindObjectsOfType<MonoBehaviour>(true).OfType<INetworkUserCallbacks>();
                foreach (var target in networkUserCallbackTargets)
                    target.OnRemoteNetworkUserSetup(this);
            }
            
            var userNameStr = userName.Value.ToString(); 
            avatarAnatomy.SetUserName(userNameStr);
            gameObject.name = userNameStr + (IsOwner ? " [Local]" : " [Remote]");
            avatarAnatomy.SetColor(userColor.Value);
        }

        private void Update()
        {
            if(disconnectNow)
            {
                disconnectNow = false;
                RequestDisconnect();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
            {
                var networkUserCallbackTargets = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).OfType<INetworkUserCallbacks>();
                foreach (var target in networkUserCallbackTargets)
                {
                    target.OnRemoteNetworkUserDisconnect(this);
                }
            }

            if (IsOwner)
            {
                
                LocalInstance = null;
            }
            base.OnNetworkDespawn();
        }

        #endregion

        #region Custom Methods
        
        public void SetUserName(string name)
        {
            if (!IsOwner)
            {
                ExtendedLogger.LogError(GetType().Name, "Only the owner can change the user name. Use SetUserNameRpc instead.");
                return;
            }
                
            userName.Value = name;
            var userNameStr = userName.Value.ToString(); 
            avatarAnatomy.SetUserName(userNameStr);
            gameObject.name = userNameStr + (IsOwner ? " [Local]" : " [Remote]");
        }

        public void SetUserId(ulong id)
        {
            userID = id;
        }

        private void UpdateUserNameLabel(FixedString32Bytes previousValue, FixedString32Bytes newValue)
        {
            var userNameStr = userName.Value.ToString();
            avatarAnatomy.SetUserName(userNameStr);
            gameObject.name = userNameStr + (IsOwner ? " [Local]" : " [Remote]");
        }
        
        private void UpdateUserColor(Color previousValue, Color newValue)
        {
            avatarAnatomy.SetColor(userColor.Value);
        }
        
        private void UpdateUserRole(int previousValue, int newValue)
        {
            userRole = ConnectionManager.Instance.userRoleList.GetUserRole(newValue);
        }

        public static float CalcLocalHeight()
        {
            return LocalInstance.head.localPosition.y;// * LocalInstance.transform.localScale.y;
        }
        
        public void RequestDisconnect()
        {
            if (!IsOwner || isDisconnecting)
                return;
            isDisconnecting = true;
            if (!IsServer)
            {
                if(verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "notifying server about disconnect");
                RequestDisconnectServerRpc(AuthenticationService.Instance.PlayerId, NetworkManager.Singleton.LocalClientId);
            }
            else
            {
                if(verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "disconnecting server " + NetworkManager.Singleton.LocalClientId);
                FinalizeDisconnect();
            }
        }
        
        private void FinalizeDisconnect()
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }
        
        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer && clientId == NetworkManager.ServerClientId)
            {
                if(verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "server shutting down");
                FinalizeDisconnect();
            }
        }

        #endregion

        #region RPCs

        [ServerRpc]
        private void RequestDisconnectServerRpc(FixedString64Bytes authPlayerId, ulong clientId)
        {
            if(verbose)
                ExtendedLogger.LogInfo(GetType().Name, "disconnecting player " + authPlayerId + " with clientId " + clientId);
            ConnectionManager.Instance.KickPlayer(authPlayerId.Value);
            PerformDisconnectClientRpc(clientId);
        }
        
        [ClientRpc]
        private void PerformDisconnectClientRpc(ulong clientId)
        {
            if(clientId != NetworkManager.Singleton.LocalClientId)
                return;
            if(verbose)
                ExtendedLogger.LogInfo(GetType().Name, "disconnecting client " + NetworkManager.Singleton.LocalClientId);
            FinalizeDisconnect();
        }

        [Rpc(SendTo.Owner)]
        public void SetUserNameRpc(string userName)
        {
            SetUserName(userName);
        }

        #endregion
    }    
}