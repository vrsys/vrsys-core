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
//   Authors:        Tony Zoeppig, Sebastian Muehlhaus, Karoline Brehm
//   Date:           2025
//-----------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.Events;
using VRSYS.Core.Logging;
using Random = UnityEngine.Random;


namespace VRSYS.Core.Networking
{
    public class ConnectionManager : MonoBehaviour
    {
        #region Member Variables
        
        // Singleton
        public static ConnectionManager Instance;

        [Header("Network User Spawn Info")]
        public NetworkUserSpawnInfo userSpawnInfo;

        [Header("Dedicated Server Properties")]
        public bool startDedicatedServer = false;
        public DedicatedServerSettings dedicatedServerSettings;

        [Header("Lobby Properties")] 
        public LobbySettings lobbySettings;

        [Header("Local Network Settings")] 
        [Tooltip("Set to true, if your app does not require Unity Services. Bypasses Unity Authentication, Relay and Lobby Services.")] public bool offlineSession = false;
        public LocalNetworkSettings localNetworkSettings;

        [Header("Debugging")] 
        [SerializeField] private bool verbose = false;

        [Header("Events")] 
        public UnityEvent onAuthenticated = new UnityEvent();
        public UnityEvent onLobbyCreated = new UnityEvent();
        public UnityEvent onLobbyJoined = new UnityEvent();
        public UnityEvent onJoinedLocalNetwork = new UnityEvent();
        
        // Connection parameters
        [HideInInspector] public Lobby lobby;
        private string lobbyId;
        private bool isLobbyCreator = false;
        private RelayHostData hostData;
        private RelayJoinData joinData;

        // Connection state
        private ConnectionState connectionState = ConnectionState.Offline;
        [HideInInspector] public UnityEvent<ConnectionState> onConnectionStateChange = new UnityEvent<ConnectionState>();

        private static string authenticatorGameObjectName = "UnityServicesAuthenticator";
        
        #endregion

        #region MonoBehaviour Callbacks

        private void Awake()
        {
            if(Instance != null)
            {
                if(verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "destroying previous connection manager");
                DestroyImmediate(Instance.gameObject);
                Instance = null;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            if (connectionState == ConnectionState.Offline && !offlineSession)
            {
                connectionState = ConnectionState.Connecting;
                onConnectionStateChange.Invoke(connectionState);
                
                // Create unique profile to distinquish between different local users
                var options = new InitializationOptions();
                options.SetProfile("Player" + Random.Range(0, 1000));
                
                // Initialize Unity Services
                await UnityServices.InitializeAsync(options);
            
                // Setup event listeners
                SetupAuthEvents();
            
                // Unity Login
                await AuthSignInAnonymouslyAsync();

                connectionState = ConnectionState.Online;
                onConnectionStateChange.Invoke(connectionState);
                onAuthenticated.Invoke();
                
                // Dedicated server handling
                if (startDedicatedServer)
                {
                    StartDedicatedServer();
                    return;
                }

                if(lobbySettings.autoStart)
                    AutoStart();
            }
        }

        private void OnDestroy()
        {
            // delete lobby when not used
            if (isLobbyCreator)
            {
                if(verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "deleting lobby " + lobbyId);
                LobbyService.Instance.DeleteLobbyAsync(lobbyId);
            }
            AuthSignOut();
            Instance = null;
        }

        #endregion

        #region Unity Login

        private void SetupAuthEvents()
        {
            AuthenticationService.Instance.SignedIn += OnAuthSignedIn;
            AuthenticationService.Instance.SignInFailed += OnAuthSignInFailed;
            AuthenticationService.Instance.SignedOut += OnAuthSignedOut;
        }

        private void RemoveAuthEvents()
        {
            AuthenticationService.Instance.SignedIn -= OnAuthSignedIn;
            AuthenticationService.Instance.SignInFailed -= OnAuthSignInFailed;
            AuthenticationService.Instance.SignedOut -= OnAuthSignedOut;
        }

        private void OnAuthSignedIn()
        {
            if (verbose)
            {
                // Player ID
                ExtendedLogger.LogInfo(GetType().Name, $"PlayerID: {AuthenticationService.Instance.PlayerId}");

                // Access Token
                ExtendedLogger.LogInfo(GetType().Name, $"Access Token: {AuthenticationService.Instance.AccessToken}");
            }
        }

        private void OnAuthSignInFailed(RequestFailedException err)
        {
            ExtendedLogger.LogError(GetType().Name, $"Error: {err.Message} err.Message", this);
        }
        
        private void OnAuthSignedOut()
        {
            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, "player signed out.");
        }

        async Task AuthSignInAnonymouslyAsync()
        {
            try
            {
                var authGameObject = GameObject.Find(authenticatorGameObjectName);
                if (authGameObject == null)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                    if (verbose)
                        ExtendedLogger.LogInfo(GetType().Name, "sign in anonymously succeeded!");
                    
                    authGameObject = new GameObject(authenticatorGameObjectName);
                    DontDestroyOnLoad(authGameObject);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
        
        private void AuthSignOut()
        {
            try
            {
                var authGameObject = GameObject.Find(authenticatorGameObjectName);
                if (authGameObject != null)
                {
                    AuthenticationService.Instance.SignOut();
                    DestroyImmediate(authGameObject);
                    RemoveAuthEvents();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        #endregion

        #region Lobby

        public async void CreateLobby()
        {
            if(verbose)
                ExtendedLogger.LogInfo(GetType().Name, "creating a new lobby...");
            
            // External connections
            int maxConnections = lobbySettings.maxUsers - 1;
            
            try
            {
                // Create RELAY object
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                hostData = new RelayHostData()
                {
                    Key = allocation.Key,
                    Port = (ushort)allocation.RelayServer.Port,
                    AllocationID = allocation.AllocationId,
                    AllocationIDBytes = allocation.AllocationIdBytes,
                    ConnectionData = allocation.ConnectionData,
                    IPv4Address = allocation.RelayServer.IpV4
                };
                
                // Retrieve JoinCode
                hostData.JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                
                // handle lobby name
                if (lobbySettings.lobbyName.Equals(""))
                {
                    lobbySettings.lobbyName = "Lobby_" + Random.Range(1, 1000);
                }
                
                CreateLobbyOptions options = new CreateLobbyOptions();
                options.IsPrivate = lobbySettings.isPrivate;
                
                // Put the JoinCode in the lobby data, visible by every member
                options.Data = new Dictionary<string, DataObject>()
                {
                    {
                        "joinCode", new DataObject(
                            visibility: DataObject.VisibilityOptions.Member,
                            value: hostData.JoinCode)
                    },
                };

                // Create the lobby
                lobby = await LobbyService.Instance.CreateLobbyAsync(lobbySettings.lobbyName, lobbySettings.maxUsers, options);
                
                // Save Lobby ID for later users
                lobbyId = lobby.Id;

                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "created lobby: " + lobby.Name + ", ID: " + lobby.Id);

                // Heartbeat the lobby every 15 seconds
                StartCoroutine(HeartbeatLobbyCoroutine(lobby.Id, 15));
                
                // Relay & Lobby are set
                
                // Set Transport data
                // Calling SetRelayServerData should automatically set protocol to "Relay Unity Transport"
                Unity.Netcode.NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                    hostData.IPv4Address,
                    hostData.Port,
                    hostData.AllocationIDBytes,
                    hostData.Key,
                    hostData.ConnectionData);
                
                // Start Host
                Unity.Netcode.NetworkManager.Singleton.StartHost();

                isLobbyCreator = true;
                connectionState = ConnectionState.JoinedLobby;
                onConnectionStateChange.Invoke(connectionState);
                onLobbyCreated.Invoke();
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError(e);
                throw;
            }
        }
        
        private IEnumerator HeartbeatLobbyCoroutine(string lobbyName, float waitTimeSeconds)
        {
            var delay = new WaitForSecondsRealtime(waitTimeSeconds);

            while (true)
            {
                LobbyService.Instance.SendHeartbeatPingAsync(lobbyName);
                
                if(verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Lobby Heartbeat");
                
                yield return delay;
            }
        }

        public async void JoinLobby(string lobbyId)
        {
            try
            {
                // Join selected lobby
                lobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
                lobbySettings.lobbyName = lobby.Name;
                this.lobbyId = lobbyId;

                if (verbose)
                {
                    ExtendedLogger.LogInfo(GetType().Name, "joined lobby: " + lobby.Id);
                    ExtendedLogger.LogInfo(GetType().Name, "lobby players: " + lobby.Players.Count);
                }
                
                // Retrieve Relay code
                string joinCode = lobby.Data["joinCode"].Value;
                
                if(verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "received JoinCode: " + joinCode);

                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                
                // Create join object
                joinData = new RelayJoinData
                {
                    Key = allocation.Key,
                    Port = (ushort)allocation.RelayServer.Port,
                    AllocationID = allocation.AllocationId,
                    AllocationIDBytes = allocation.AllocationIdBytes,
                    ConnectionData = allocation.ConnectionData,
                    HostConnectionData = allocation.HostConnectionData,
                    IPv4Address = allocation.RelayServer.IpV4
                };
                
                // Set Transport data
                Unity.Netcode.NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                    joinData.IPv4Address,
                    joinData.Port,
                    joinData.AllocationIDBytes,
                    joinData.Key,
                    joinData.ConnectionData,
                    joinData.HostConnectionData);
                
                // Start Client
                Unity.Netcode.NetworkManager.Singleton.StartClient();
                
                connectionState = ConnectionState.JoinedLobby;
                onConnectionStateChange.Invoke(connectionState);
                onLobbyJoined.Invoke();
            }
            catch (LobbyServiceException e)
            {
                // If no lobby could be found, create a new one
                if(verbose)
                    ExtendedLogger.LogError(GetType().Name, "could not join the lobby: " + e);
            }
        }

        public async void AutoStart()
        {
            if(verbose)
                ExtendedLogger.LogInfo(GetType().Name, "auto starting...");

            if (!string.IsNullOrEmpty(lobbySettings.lobbyName))
            {
                try
                {
                    QueryLobbiesOptions options = new QueryLobbiesOptions();
                
                    // Filter for open lobbies only
                    options.Filters = new List<QueryFilter>()
                    {
                        new QueryFilter(
                            field: QueryFilter.FieldOptions.AvailableSlots,
                            op: QueryFilter.OpOptions.GT,
                            value: "0")
                    };

                    QueryResponse result = await LobbyService.Instance.QueryLobbiesAsync(options);
                    Lobby lobby = result.Results.Find(l => l.Name == lobbySettings.lobbyName);

                    if (lobby != null)
                    {
                        JoinLobby(lobby.Id);
                    }
                    else
                    {
                        CreateLobby();
                    }
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogError(e);
                }
            }
            else
            {
                CreateLobby();
            }
        }

        public async void StartDedicatedServer()
        {
            if(verbose)
                ExtendedLogger.LogInfo(GetType().Name, "creating a new dedicated server...");
            
            // Read dedicated server config file
            dedicatedServerSettings.ParseJsonConfigFile();
            
            // External connections
            int maxConnections = dedicatedServerSettings.jsonConfig.MaxConnections;

            try
            {
                // Create RELAY object
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                hostData = new RelayHostData()
                {
                    Key = allocation.Key,
                    Port = (ushort)allocation.RelayServer.Port,
                    AllocationID = allocation.AllocationId,
                    AllocationIDBytes = allocation.AllocationIdBytes,
                    ConnectionData = allocation.ConnectionData,
                    IPv4Address = allocation.RelayServer.IpV4
                };
                
                // Retrieve JoinCode
                hostData.JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                
                // handle lobby name
                if (!(dedicatedServerSettings.jsonConfig.LobbyName.Length > 0))
                {
                    if (string.IsNullOrEmpty(lobbySettings.lobbyName))
                    {
                        lobbySettings.lobbyName = "Lobby_" + Random.Range(1, 1000);
                    }
                }
                else
                {
                    lobbySettings.lobbyName = dedicatedServerSettings.jsonConfig.LobbyName;
                }

                CreateLobbyOptions options = new CreateLobbyOptions();
                options.IsPrivate = lobbySettings.isPrivate;
                
                // Put the JoinCode in the lobby data, visible by every member
                options.Data = new Dictionary<string, DataObject>()
                {
                    {
                        "joinCode", new DataObject(
                            visibility: DataObject.VisibilityOptions.Member,
                            value: hostData.JoinCode)
                    },
                };

                // Create the lobby
                var lobby = await LobbyService.Instance.CreateLobbyAsync(lobbySettings.lobbyName, maxConnections, options);
                
                // Save Lobby ID for later users
                lobbyId = lobby.Id;

                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "created lobby: " + lobby.Name + ", ID: " + lobby.Id);

                // Heartbeat the lobby every 15 seconds
                StartCoroutine(HeartbeatLobbyCoroutine(lobby.Id, 15));
                
                // Relay & Lobby are set
                
                // Set Transport data
                Unity.Netcode.NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                    hostData.IPv4Address,
                    hostData.Port,
                    hostData.AllocationIDBytes,
                    hostData.Key,
                    hostData.ConnectionData);
                
                // Start Host
                Unity.Netcode.NetworkManager.Singleton.StartServer();

                connectionState = ConnectionState.JoinedLobby;
                onConnectionStateChange.Invoke(connectionState);
                onLobbyCreated.Invoke();
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError(e);
                throw;
            }
        }

        #endregion

        #region Local Session

        public void StartLocalNetworkServer()
        {
            SetLocalNetworkConnectionData();
            NetworkManager.Singleton.StartServer();
            onJoinedLocalNetwork.Invoke();
        }

        public void StartLocalNetworkHost()
        {
            SetLocalNetworkConnectionData();
            NetworkManager.Singleton.StartHost();
            onJoinedLocalNetwork.Invoke();
        }

        public void StartLocalNetworkClient()
        {
            SetLocalNetworkConnectionData();
            NetworkManager.Singleton.StartClient();
            onJoinedLocalNetwork.Invoke();
        }

        private void SetLocalNetworkConnectionData()
        {
            // Calling SetConnectionData automatically sets the protocoll to "Unity Transport"
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(localNetworkSettings.ipAddress, localNetworkSettings.port);
        }

        #endregion

        #region Kick Player

        public async void KickPlayer(string playerId)
        {
            if (isLobbyCreator)
            {
                if(verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "kicking player " + playerId);
                await LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);
            }
        }

        #endregion

        #region Relay Data Structs

        /// <summary>
        /// RelayHostData represents the necessary information
        /// for a host to host a game on a Relay server
        /// </summary>
        public struct RelayHostData
        {
            public string JoinCode;
            public string IPv4Address;
            public ushort Port;
            public Guid AllocationID;
            public byte[] AllocationIDBytes;
            public byte[] ConnectionData;
            public byte[] Key;
        }

        /// <summary>
        /// RelayJoinData represents the necessary information
        /// to join a game on a Relay server
        /// </summary>
        public struct RelayJoinData
        {
            public string JoinCode;
            public string IPv4Address;
            public ushort Port;
            public Guid AllocationID;
            public byte[] AllocationIDBytes;
            public byte[] ConnectionData;
            public byte[] HostConnectionData;
            public byte[] Key;
        }

        #endregion
    }   
}