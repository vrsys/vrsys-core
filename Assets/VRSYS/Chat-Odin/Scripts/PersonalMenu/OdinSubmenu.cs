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

using System.Collections.Generic;
using System.Linq;
using OdinNative.Unity.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRSYS.Core.Chat.Odin;
using VRSYS.Core.Logging;

namespace VRSYS.Core.PersonalMenu
{
    public class OdinSubmenu : PersonalSubmenu
    {
        #region Member Variables

        [Header("UI Elements")] 
        public Toggle globallyMutedToggle;
        public TMP_Dropdown microphoneDropdown;
        public Transform roomTilesParent;
        public GameObject roomTilePrefab;
        
        [Header("Debug")]
        public bool verbose = false;

        private MicrophoneReader microphoneReader;
        private Dictionary<string, GameObject> roomTiles = new Dictionary<string, GameObject>();

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            // Global mute settings
            globallyMutedToggle.isOn = UserVoiceComponent.LocalInstance.isGloballyMuted;
            globallyMutedToggle.onValueChanged.AddListener(ToggleGlobalMute);
            
            // Microphone Settings
            microphoneDropdown.options.Clear();
            microphoneDropdown.AddOptions(Microphone.devices.ToList());
            microphoneDropdown.onValueChanged.AddListener(OnMicrophoneDropdownValueChanged);
            microphoneReader = OdinHandler.Instance.Microphone;
            
            // Room Settings
            InitializeRoomTiles();
            SetupRoomEvents();

            
        }
        
        private void Update()
        {
            if (Microphone.devices.Length != microphoneDropdown.options.Count)
            {
                UpdateMicrophoneDropdown();
            }
        }

        #endregion

        #region Custom Methods
        
        private void OnMicrophoneDropdownValueChanged(int arg0)
        {
            microphoneReader.StopListen();
            microphoneReader.CustomInputDevice = true;
            microphoneReader.InputDevice = microphoneDropdown.options[microphoneDropdown.value].text;
            microphoneReader.StartListen();
            
            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, $"now listening to microphone: {microphoneReader.InputDevice}");
        }
        
        private void UpdateMicrophoneDropdown()
        {
            string currentMic = microphoneDropdown.options[microphoneDropdown.value].text;
            
            microphoneDropdown.options.Clear();
            microphoneDropdown.AddOptions(Microphone.devices.ToList());
            
            int index = Microphone.devices.ToList().FindIndex(x => x == currentMic);
            
            if(index != -1)
                microphoneDropdown.value = index;
            else if (index == -1 && Microphone.devices.Length > 0)
                microphoneDropdown.value = 0;
            else
                ExtendedLogger.LogError(GetType().Name, "No microphone connected!");
        }

        private void InitializeRoomTiles()
        {
            foreach (var room in UserVoiceComponent.LocalInstance.currentRooms)
            {
                AddRoomTile(room);
            }
        }

        private void SetupRoomEvents()
        {
            UserVoiceComponent.LocalInstance.onJoinedRoom.AddListener(AddRoomTile);
            UserVoiceComponent.LocalInstance.onLeftRoom.AddListener(DeleteRoomTile);
        }

        private void AddRoomTile(OdinRoomConfiguration roomConfig)
        {
            GameObject roomTile = Instantiate(roomTilePrefab, roomTilesParent);
            roomTiles.Add(roomConfig.roomName, roomTile);
            
            roomTile.GetComponent<OdinRoomTile>().Initialize(roomConfig);
        }

        private void DeleteRoomTile(string roomName)
        {
            Destroy(roomTiles[roomName]);
        }
        
        private void ToggleGlobalMute(bool arg0)
        {
            UserVoiceComponent.LocalInstance.ToggleMuteGlobally();
        }

        #endregion
    }
}
