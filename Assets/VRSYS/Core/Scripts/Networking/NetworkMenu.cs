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
//   Authors:        Tony Zoeppig, Ephraim Schott, Sebastian Muehlhaus
//   Date:           2023
//-----------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRSYS.Core.Logging;
using VRSYS.Core.ScriptableObjects;

namespace VRSYS.Core.Networking
{
    [Serializable]
    public struct NamedColor
    {
        public string name;
        public Color color;
    }
    
    public class NetworkMenu : MonoBehaviour
    {
        #region Member Variables

        [Header("UI Sections")] 
        public GameObject lobbyOverview;
        public GameObject createLobbySection;
        
        [Header("Interactive UI Elements")] 
        public TMP_InputField userNameInputField;
        public TMP_Dropdown userRoleDropdown;
        public TMP_Dropdown userColorDropdown;
        public TMP_InputField lobbyNameInputField;
        public TMP_InputField maxUsersInputField;
        public Button addLobbyButton;
        public Button createLobbyButton;
        public Button backButton;
        public TextMeshProUGUI stateText;

        [Header("Lobby Tiles")] 
        public GameObject lobbyTilePrefab;
        public Transform tileParent;
        
        [Header("Selectable Setup Options")]
        public List<NamedColor> avatarColors;

        public List<UserRole> unavailableUserRoles;
        private List<UserRole> userRoles;
        
        private NetworkUserSpawnInfo spawnInfo => ConnectionManager.Instance.userSpawnInfo;

        private LobbyListUpdater lobbyListUpdater;

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            userRoles = Enum.GetValues(typeof(UserRole)).Cast<UserRole>().ToList();
            foreach (var unavailableUserRole in unavailableUserRoles)
                userRoles.Remove(unavailableUserRole);

            if (ConnectionManager.Instance.lobbySettings.autoStart)
            {
                gameObject.SetActive(false);
                return;
            }

            lobbyListUpdater = FindObjectOfType<LobbyListUpdater>();
            
            SetupUIElements();
            SetupUIEvents();
        }

        #endregion

        #region Custom Methods

        private void SetupUIElements()
        {
            // setup user name input field
            userNameInputField.text = spawnInfo.userName;
            
            // Setup user role dropdown
            userRoleDropdown.options.Clear();

            List<string> userRoleStr = new List<string>();
            
            foreach (var userRole in userRoles)
                userRoleStr.Add(userRole.ToString());

            userRoleDropdown.AddOptions(userRoleStr);

            int index = userRoleDropdown.options.FindIndex(
                s => s.text.Equals(spawnInfo.userRole.ToString()));
            index = index == -1 ? 0 : index;
            
            userRoleDropdown.value = index;
            UpdateUserRole(); // secure that the user role is set consistent between ui and manager

            // Setup user color dropdown
            userColorDropdown.options.Clear();
            
            List<string> availableUserColors = new List<string>();
            foreach (var color in avatarColors)
            {
                availableUserColors.Add(color.name);
            }
            
            userColorDropdown.AddOptions(availableUserColors);
            
            spawnInfo.userColor = avatarColors[0].color;
        }

        private void SetupUIEvents()
        {
            if(userNameInputField is not null)
                userNameInputField.onValueChanged.AddListener(UpdateUserName);
            if(userRoleDropdown is not null)
                userRoleDropdown.onValueChanged.AddListener(UpdateUserRole);
            if(userColorDropdown is not null)
                userColorDropdown.onValueChanged.AddListener(UpdateUserColor);
            if(lobbyNameInputField is not null)
                lobbyNameInputField.onValueChanged.AddListener(UpdateLobbyName);
            if(maxUsersInputField is not null)
                maxUsersInputField.onValueChanged.AddListener(UpdateMaxUser);
            if(addLobbyButton is not null)
                addLobbyButton.onClick.AddListener(AddLobby);
            if(createLobbyButton is not null)
                createLobbyButton.onClick.AddListener(CreateLobby);
            if(backButton is not null)
                backButton.onClick.AddListener(Back);
            
            ConnectionManager.Instance.onConnectionStateChange.AddListener(UpdateConnectionState);

            if (lobbyListUpdater == null)
            {
                ExtendedLogger.LogError(GetType().Name, "No LobbyListUpdater present in current scene!");
            }
            else
            {
                lobbyListUpdater.onLobbyListUpdated.AddListener(UpdateLobbyList);
            }
        }

        private void UpdateUserName(string arg0)
        {
            spawnInfo.userName = userNameInputField.text;
        }
        
        private void UpdateUserRole(int arg0)
        {
            Enum.TryParse(userRoleDropdown.options[userRoleDropdown.value].text, out spawnInfo.userRole);
        }
        
        private void UpdateUserRole()
        {
            Enum.TryParse(userRoleDropdown.options[userRoleDropdown.value].text, out spawnInfo.userRole);
        }

        public void UpdateUserColor(int arg0)
        {
            NamedColor selectedColor = avatarColors.Find(color => color.name.Equals(userColorDropdown.options[userColorDropdown.value].text));
            spawnInfo.userColor = selectedColor.color;
        }
        
        private void UpdateLobbyName(string arg0)
        {
            ConnectionManager.Instance.lobbySettings.lobbyName = lobbyNameInputField.text;
        }

        private void UpdateMaxUser(string arg0)
        {
            ConnectionManager.Instance.lobbySettings.maxUsers = int.Parse(maxUsersInputField.text);
        }
        
        private void AddLobby()
        {
            ConnectionManager.Instance.lobbySettings.lobbyName = "";
            lobbyOverview.SetActive(false);
            createLobbySection.SetActive(true);
        }

        private void CreateLobby()
        {
            ConnectionManager.Instance.CreateLobby();
            gameObject.SetActive(false);
        }

        private void Back()
        {
            lobbyOverview.SetActive(true);
            createLobbySection.SetActive(false);
        }
        
        private void UpdateConnectionState(ConnectionState state)
        {
            switch (state)
            {
                case ConnectionState.Offline:
                    stateText.text = "Offline";
                    stateText.color = Color.red;
                    break;
                case ConnectionState.Connecting:
                    stateText.text = "Connecting...";
                    stateText.color = Color.yellow;
                    break;
                case ConnectionState.Online:
                    stateText.text = "Online";
                    stateText.color = Color.green;
                    break;
                case ConnectionState.JoinedLobby:
                    gameObject.SetActive(false);
                    break;
            }
        }

        private void UpdateLobbyList()
        {
            foreach (Transform t in tileParent)
            {
                Destroy(t.gameObject);
            }

            foreach (var lobbyData in lobbyListUpdater.lobbyList)
            {
                GameObject lobbyTile = Instantiate(lobbyTilePrefab, tileParent);
                lobbyTile.GetComponent<LobbyTile>().SetupTile(lobbyData);
            }

            RectTransform contentTransform = tileParent.GetComponent<RectTransform>();
            contentTransform.sizeDelta = new Vector2(lobbyListUpdater.lobbyList.Count * (lobbyTilePrefab.GetComponent<RectTransform>().rect.height + tileParent.GetComponent<VerticalLayoutGroup>().spacing),contentTransform.rect.width);
        }

        #endregion
    }
}