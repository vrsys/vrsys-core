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

using OdinNative.Odin.Room;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRSYS.Core.Chat.Odin;

namespace VRSYS.Core.PersonalMenu
{
    public class OdinRoomTile : MonoBehaviour
    {
        #region Member Variables

        private string roomName;

        [Header("UI Elements")] 
        public TextMeshProUGUI roomNameText;
        public Toggle muteToggle;
        public Toggle stereoToggle;
        public Button disconnectButton;

        #endregion

        #region Custom Methods

        public void Initialize(OdinRoomConfiguration roomConfig)
        {
            roomName = roomConfig.roomName;

            // setup ui elements
            roomNameText.text = roomName;
            muteToggle.isOn = !roomConfig.transmitAudio;
            stereoToggle.isOn = roomConfig.defaultStereo;
            
            // setup events
            muteToggle.onValueChanged.AddListener(ToggleMute);
            stereoToggle.onValueChanged.AddListener(ToggleStereo);
            disconnectButton.onClick.AddListener(Disconnect);
        }

        private void ToggleMute(bool arg0)
        {
            UserVoiceComponent.LocalInstance.ToggleMuteInRoom(roomName);
        }
        
        private void ToggleStereo(bool arg0)
        {
            UserVoiceComponent.LocalInstance.ToggleStereoVoice(roomName);
        }
        
        private void Disconnect()
        {
            UserVoiceComponent.LocalInstance.LeaveOdinRoom(roomName);
        }

        #endregion
    }
}
