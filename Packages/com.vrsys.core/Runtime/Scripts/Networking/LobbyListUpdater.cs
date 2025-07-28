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
//   Authors:        Tony Zoeppig
//   Date:           2025
//-----------------------------------------------------------------

using System.Collections.Generic;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.Events;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Networking
{
    public class LobbyListUpdater : MonoBehaviour
    {
        #region Member Variables

        [Header("Update Parameter")]
        
        [Tooltip("If set to true, the list of available lobbies will be queried from the Lobby services.")]
        public bool updateLobbyList = true;
        [Tooltip("Defines the interval in seconds in which the list of available lobbies will be queried.")]
        public float updateInterval = 10f;
        
        public List<LobbyData> lobbyList = new List<LobbyData>();
        [Tooltip("Invoked when list of available lobbies has been updated.")]
        public UnityEvent onLobbyListUpdated;

        [Header("Debugging")] 
        
        [Tooltip("If set to true, component will also log info logs.")]
        public bool verbose = false;
        
        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            ConnectionManager.Instance.onConnectionStateChange.AddListener(Initialize);
        }

        private void OnDestroy()
        {
            if(ConnectionManager.Instance != null)
                ConnectionManager.Instance.onConnectionStateChange.RemoveListener(Initialize);
        }

        #endregion

        #region Custom Methods

        private void Initialize(ConnectionState state)
        {
            // start list update
            if(state == ConnectionState.Online)
                InvokeRepeating(nameof(UpdateLobbyList), 5f, updateInterval);
        }

        private async void UpdateLobbyList()
        {
            if (updateLobbyList)
            {
                if(verbose)
                    ExtendedLogger.LogInfo(GetType().Name ,"Updating lobby list...");
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
                    
                    QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);

                    lobbyList = new List<LobbyData>();

                    foreach (var lobby in response.Results)
                    {
                        LobbyData lobbyData = new LobbyData
                        {
                            LobbyId = lobby.Id,
                            LobbyName = lobby.Name,
                            CurrentUser = lobby.Players.Count,
                            MaxUser = lobby.MaxPlayers
                        };
                        
                        lobbyList.Add(lobbyData);
                    }
                
                    if(verbose)
                        ExtendedLogger.LogInfo(GetType().Name, "Found " + lobbyList.Count + " lobbies.");
                
                    onLobbyListUpdated.Invoke();
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogError(e);
                }
            }
        }

        #endregion

        #region Structs

        public struct LobbyData
        {
            public string LobbyId;
            public string LobbyName;
            public int CurrentUser;
            public int MaxUser;
        }

        #endregion
    }
}
