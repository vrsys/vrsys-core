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

using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using VRSYS.Core.Logging;
using VRSYS.Core.Networking;

namespace VRSYS.Core.Avatar
{
    public class UserNameLabel : MonoBehaviour
    {
        #region Member Variables

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI _labelText;
        [SerializeField] private Image _labelBackground;

        [Header("Behaviour Configuration")] 
        [SerializeField] private bool _applyUserColor = false;

        private NetworkUser _networkUser;

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            Initialize();
        }

        #endregion

        #region Private Methods

        private void Initialize()
        {
            _networkUser = GetComponentInParent<NetworkUser>();

            if (_networkUser == null)
            {
                ExtendedLogger.LogError(GetType().Name, $"No {typeof(NetworkUser)} could be found.", this);
                return;
            }

            // register value changed events
            _networkUser.userName.OnValueChanged += OnUserNameChanged;
            _networkUser.userColor.OnValueChanged += OnUserColorChanged;
            
            // initialize label components
            UpdateUserName();
            UpdateBackground();
        }

        private void UpdateUserName() => _labelText.text = _networkUser.userName.Value.ToString();

        private void UpdateBackground()
        {
            if (_applyUserColor)
                _labelBackground.color = _networkUser.userColor.Value;
        }

        #endregion

        #region Event Callbacks

        private void OnUserNameChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue) =>
            UpdateUserName();

        private void OnUserColorChanged(Color previousValue, Color newValue) => UpdateBackground();

        #endregion
    }
}
