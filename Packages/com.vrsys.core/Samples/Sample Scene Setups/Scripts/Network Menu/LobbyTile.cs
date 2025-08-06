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
//   Date:           2023
//-----------------------------------------------------------------

using System.Collections;
using TMPro;
using Unity.Services.Lobbies;
using UnityEngine;
using UnityEngine.UI;

namespace VRSYS.Core.Networking
{
    public class LobbyTile : MonoBehaviour
    {
        #region Member Variables

        // lobby Data
        private LobbyListUpdater.LobbyData _lobbyData;
        
        // network menu
        private NetworkMenu _networkMenu;

        [Header("UI Elements")] 
        public TextMeshProUGUI lobbyNameText;
        public TextMeshProUGUI userCountText;
        public Button joinButton;

        [Header("Configuration")] 
        public float lobbyUpdateInterval = 2f;

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            joinButton.onClick.AddListener(JoinLobby);
        }

        #endregion

        #region Custom Methods

        public void SetupTile(LobbyListUpdater.LobbyData lobbyData, NetworkMenu menu)
        {
            _lobbyData = lobbyData;
            _networkMenu = menu;

            lobbyNameText.text = lobbyData.LobbyName;
            userCountText.text = lobbyData.CurrentUser + "/" + lobbyData.MaxUser;

            if (lobbyData.CurrentUser == lobbyData.MaxUser)
                joinButton.interactable = false;

            if(gameObject.activeSelf)
                StartCoroutine(UpdateLobbyDataCoroutine());
        }

        private async void UpdateLobby()
        {
            var lobby = await LobbyService.Instance.GetLobbyAsync(_lobbyData.LobbyId);

            userCountText.text = lobby.Players.Count + "/" + lobby.MaxPlayers;
        }

        private void JoinLobby()
        {
            transform.root.gameObject.SetActive(false);
            ConnectionManager.Instance.JoinLobby(_lobbyData.LobbyId);
        }

        #endregion

        #region Coroutines

        private IEnumerator UpdateLobbyDataCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(lobbyUpdateInterval);
                UpdateLobby();
            }
        }

        #endregion
    }   
}