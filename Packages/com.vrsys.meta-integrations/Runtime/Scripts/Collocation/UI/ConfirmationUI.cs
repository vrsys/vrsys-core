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
//   Authors:        Tony Zoeppig, Karoline Brehm
//   Date:           2025
//-----------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using VRSYS.Core.Networking;

using UnityEngine.Events;
using UnityEngine.Serialization;

namespace VRSYS.Meta.Collocation
{
    public class ConfirmationUI : MonoBehaviour
    {
        #region Properties

        [Header("UI Elements")] 
        [SerializeField] private Button _confirm;
        [SerializeField] private Button _reject;

        #endregion

        #region Public Methods

        public void Initialize(UnityAction OnConfirm, UnityAction OnReject)
        {
            _confirm.onClick.AddListener(OnConfirm);
            _reject.onClick.AddListener(OnReject);
            Hide();
        }
        
        public void Show()
        {
            gameObject.SetActive(true);
            // Set position & rotation
            Transform userHead = NetworkUser.LocalInstance.head;
            
            transform.position = userHead.position + userHead.forward * 0.3f;

            Vector3 rotationAngles = userHead.rotation.eulerAngles;
            rotationAngles = new Vector3(0, rotationAngles.y, 0);
            transform.rotation = Quaternion.Euler(rotationAngles);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        #endregion
    }
}