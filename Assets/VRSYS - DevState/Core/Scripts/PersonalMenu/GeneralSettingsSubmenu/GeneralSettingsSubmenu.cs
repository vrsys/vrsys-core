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
//   Authors:        Tony Jan Zoeppig
//   Date:           2023
//-----------------------------------------------------------------

using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using VRSYS.Core.Logging;
using VRSYS.Core.Networking;
using VRSYS.Core.PersonalMenu;

public class GeneralSettingsSubmenu : PersonalSubmenu
{
    #region Member Variables

    [Header("UI Elements")] 
    public TextMeshProUGUI lobbyNameText;
    public TextMeshProUGUI isHostText;
    public TextMeshProUGUI userCountText;
    public Button disconnectButton;

    private string currentLobbyName = "";
    private LobbyListUpdater lobbyListUpdater;

    #endregion

    #region MonoBehaviour Callbacks

    private void Start()
    {
        currentLobbyName = ConnectionManager.Instance.lobbySettings.lobbyName;
        lobbyListUpdater = FindObjectOfType<LobbyListUpdater>();
        
        lobbyNameText.text = ConnectionManager.Instance.lobbySettings.lobbyName;
        isHostText.text = NetworkManager.Singleton.IsHost.ToString();
        userCountText.text = ConnectionManager.Instance.lobby.Players.Count + "/" +
                             ConnectionManager.Instance.lobby.MaxPlayers;

        if (lobbyListUpdater == null)
        {
            ExtendedLogger.LogError(GetType().Name, "No LobbyListUpdater present in current scene!");
            userCountText.text = "No LobbyListUpdater present in current scene!";
            userCountText.color = Color.red;
        }
        else
        {
            lobbyListUpdater.onLobbyListUpdated.AddListener(UpdateUserCount);
        }

        disconnectButton.onClick.AddListener(NetworkUser.LocalInstance.RequestDisconnect);
    }

    #endregion

    #region Custom Methods

    private void UpdateUserCount()
    {
        int idx = lobbyListUpdater.lobbyList.FindIndex(lobby => lobby.LobbyName == currentLobbyName);

        if (idx == -1)
        {
            ExtendedLogger.LogInfo(GetType().Name, "Current lobby could not be found in lobby list!");
        }
        else
        {
            LobbyListUpdater.LobbyData lobby = lobbyListUpdater.lobbyList[idx];
            
            userCountText.text = lobby.CurrentUser + "/" + lobby.MaxUser;
        }
    }

    #endregion
}
