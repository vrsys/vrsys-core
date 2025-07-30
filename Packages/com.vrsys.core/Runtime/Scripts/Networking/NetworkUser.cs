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
using UnityEngine.Serialization;
using VRSYS.Core.Avatar;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Networking
{
    [RequireComponent(typeof(NetworkObject), typeof(AvatarAnatomy))]
    public class NetworkUser : NetworkBehaviour
    {
        #region Local Instance

        public static NetworkUser LocalInstance;

        #endregion

        #region Static Events

        // Connected Events
        public static UnityEvent LocalNetworkUserConnected = new UnityEvent();
        public static UnityEvent<NetworkUser> RemoteNetworkUserConnected = new UnityEvent<NetworkUser>();
        
        // Disconnected Events
        public static UnityEvent LocalNetworkUserDisconnected = new UnityEvent();
        public static UnityEvent<NetworkUser> RemoteNetworkUserDisconnected = new UnityEvent<NetworkUser>();

        #endregion
        
        #region Member Variables
        
        public AvatarAnatomy avatarAnatomy { get; private set; }
        
        public Transform head => avatarAnatomy.head;
        
        [Tooltip("Local components, which are destroyed on remote user instances (e.g. Camera, Audio Listener, Tracking, ...)")]
        public List<Behaviour> localBehaviours = new List<Behaviour>();

        [Header("Local Connection Management")]
        public bool disconnectNow = false;
        
        [Header("Debug")]
        public bool verbose = false;
        
        private bool _isDisconnecting = false;

        private NetworkUserSpawnInfo _spawnInfo;
        
        #endregion

        #region Network Variables

        [HideInInspector] public NetworkVariable<Color> userColor = new(default, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        
        [HideInInspector] public NetworkVariable<FixedString32Bytes> userName = new (default, 
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [HideInInspector] public NetworkVariable<ulong> userId = new NetworkVariable<ulong>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        [HideInInspector] public NetworkVariable<UserRole> userRole = new(default, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private NetworkVariable<bool> _initialized = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        

        #endregion

        #region Mono- & NetworkBehaviour Callbacks

        public override void OnNetworkSpawn()
        {
            avatarAnatomy = GetComponent<AvatarAnatomy>();
            
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            _spawnInfo = ConnectionManager.Instance.userSpawnInfo;
            
            if (IsOwner)
            {
                // Set local user instance
                LocalInstance = this;
                
                // handle user name workflow
                if (_spawnInfo.userName.Equals(""))
                {
                    userName.Value = "Player_" + Random.Range(0, 1000);
                }
                else
                {
                    userName.Value = _spawnInfo.userName;
                }
                
                // handle user color workflow
                userColor.Value = _spawnInfo.userColor;
                
                // handle user role workflow
                userRole.Value = _spawnInfo.userRole;
                
                // mark user as initialized
                _initialized.Value = true;

                BroadcastLocalUserConnected();
            }
        }

        private void Start()
        {
            if(!IsOwner)
            {
                // register user name changed event
                userName.OnValueChanged += OnUserNameChanged;
                
                // register user color changed event
                userColor.OnValueChanged += OnUserColorChanged;
                
                // Remove unnecessary local components such as tracking
                while(localBehaviours.Count > 0)
                {
                    DestroyImmediate(localBehaviours[0]);
                    localBehaviours.RemoveAt(0);
                }

                if (_initialized.Value)
                {
                    BroadcastRemoteUserConnected();
                }
                else
                {
                    _initialized.OnValueChanged += OnUserInitialized;
                }
            }
            
            UpdateObjectName();
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
        }

        #endregion

        #region Public Methods

        public void SetUserName(string name)
        {
            if (!IsOwner)
            {
                SetUserNameRpc(name);
                return;
            }
                
            userName.Value = name;
            UpdateObjectName();
        }
        
        public void SetUserId(ulong id)
        {
            if (!IsOwner)
            {
                SetUserIdRpc(id);
                return;
            }
            
            userId.Value = id;
        }

        public void SetUserColor(Color color)
        {
            if (!IsOwner)
            {
                SetUserColorRpc(color);
                return;
            }

            userColor.Value = color;
        }
        
        public void RequestDisconnect()
        {
            if (!IsOwner || _isDisconnecting)
                return;
            _isDisconnecting = true;
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

        #endregion

        #region Private Methods
        
        private void FinalizeDisconnect()
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }

        private void BroadcastLocalUserConnected()
        {
            LocalNetworkUserConnected.Invoke();

            var networkUserLifecycleCallbackTargets = FindObjectsOfType<MonoBehaviour>(true).OfType<INetworkUserCallbacks>();
            foreach (var target in networkUserLifecycleCallbackTargets)
                target.OnLocalNetworkUserSetup();
        }

        private void BroadcastRemoteUserConnected()
        {
            RemoteNetworkUserConnected.Invoke(this);
                    
            var networkUserCallbackTargets = FindObjectsOfType<MonoBehaviour>(true).OfType<INetworkUserCallbacks>();
            foreach (var target in networkUserCallbackTargets)
                target.OnRemoteNetworkUserSetup(this);
        }

        private void UpdateObjectName()
        {
            gameObject.name = userName.Value + (IsOwner ? " [Local]" : " [Remote]");
        }

        #endregion

        #region Event Callbacks

        private void OnUserNameChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue) =>
            UpdateObjectName();
        
        private void OnUserColorChanged(Color previousValue, Color newValue)
        {
            avatarAnatomy.SetColor(userColor.Value);
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
        
        private void OnUserInitialized(bool previousValue, bool newValue)
        {
            BroadcastRemoteUserConnected();

            _initialized.OnValueChanged -= OnUserInitialized;
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
        private void SetUserNameRpc(string userName)=> SetUserName(userName);

        [Rpc(SendTo.Owner)]
        private void SetUserIdRpc(ulong id) => SetUserId(id);

        [Rpc(SendTo.Owner)]
        private void SetUserColorRpc(Color color) => SetUserColor(color);

        #endregion
    }    
}