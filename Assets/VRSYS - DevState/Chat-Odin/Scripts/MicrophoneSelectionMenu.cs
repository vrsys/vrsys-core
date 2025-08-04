// VRSYS plugin of Virtual Reality and Visualization Research Group (Bauhaus University Weimar)
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
// Copyright (c) 2022 Virtual Reality and Visualization Research Group
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
//   Authors:        Tony Zoeppig, Karoline Brehm
//   Date:           2023
//-----------------------------------------------------------------

using System.Linq;
using OdinNative.Unity.Audio;
using TMPro;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Chat.Odin
{
    public class MicrophoneSelectionMenu : MonoBehaviour
    {
        #region Member Variables

        [Header("Interactive UI Elements")]
        public TMP_Dropdown microphoneDropdown;

        private MicrophoneReader microphoneReader;

        [Header("Debug")]
        public bool verbose = false;

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            SetupUIElements();
            SetupUIEvents();

            microphoneReader = OdinHandler.Instance.Microphone;
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

        private void SetupUIElements()
        {
            microphoneDropdown.options.Clear();
            microphoneDropdown.AddOptions(Microphone.devices.ToList());
        }
        
        private void SetupUIEvents()
        {
            microphoneDropdown.onValueChanged.AddListener(OnMicrophoneDropdownValueChanged);
        }

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

        #endregion
    }
}
