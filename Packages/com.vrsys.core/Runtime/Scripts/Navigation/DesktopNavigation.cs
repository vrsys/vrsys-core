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
//   Authors:        Tony Jan Zoeppig, Sebastian Muehlhaus, Ephraim Schott
//   Date:           2023
//-----------------------------------------------------------------

using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRSYS.Core.Navigation
{
    public class DesktopNavigation : MonoBehaviour
    {
        #region Member Variables

        public Transform target;

        [Header("Input Actions")] 
        public InputActionProperty moveAction;
        public InputActionProperty yawAction;
        public InputActionProperty pitchAction;
        
        [Header("Movement Properties")]
        [Tooltip("Translation Velocity [m/sec]")]
        [Range(0.1f, 10.0f)]
        public float translationVelocity = 3.0f;

        [Tooltip("Rotation Velocity [degree/sec]")]
        [Range(1.0f, 180.0f)]
        public float rotationVelocity = 30.0f;
        
        private Vector3 rotInput = Vector3.zero;

        private bool? isOfflineOrOwner_;
        private bool isOfflineOrOwner
        {
            get
            {
                if(!isOfflineOrOwner_.HasValue)
                {
                    if (GetComponent<NetworkObject>() is not null)
                        isOfflineOrOwner_ = GetComponent<NetworkObject>().IsOwner;
                    else
                        isOfflineOrOwner_ = true;
                }
                return isOfflineOrOwner_.Value;
            }
        }

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            if (!isOfflineOrOwner)
                Destroy(this);
            else if (target == null)
                target = transform;                
        }

        private void Update()
        {
            if (!isOfflineOrOwner)
                return;
            MapInput(CalcTranslationInput(), CalcRotationInput());
        }

        #endregion
        
        #region Custom Methods

        private Vector3 CalcTranslationInput()
        {
            Vector2 input = moveAction.action.ReadValue<Vector2>();
            Vector3 transInput = Vector3.zero;

            // foward input
            transInput.z += input.y > 0f ? 1.0f : 0.0f;
            transInput.z -= input.y < 0f ? 1.0f : 0.0f;
            transInput.x += input.x > 0f ? 1.0f : 0.0f;
            transInput.x -= input.x < 0f ? 1.0f : 0.0f;

            return transInput * (translationVelocity * Time.deltaTime);
        }

        private Vector3 CalcRotationInput()
        {
            float yaw = yawAction.action.ReadValue<float>();
            float pitch = pitchAction.action.ReadValue<float>();
            float inputY = yaw;
            float inputX = pitch;

            // head rot input
            rotInput.y += inputY * rotationVelocity * Time.deltaTime;

            // pitch rot input
            rotInput.x -= inputX * rotationVelocity * Time.deltaTime ;
            rotInput.x = Mathf.Clamp(rotInput.x, -80, 80);

            return rotInput;
        }

        private void MapInput(Vector3 transInput, Vector3 rotInput)
        {
            // map translation input
            if (transInput.magnitude > 0.0f)
            {
                target.Translate(transInput);
            }

            // map rotation input
            if (rotInput.magnitude > 0.0f)
            {
                target.localRotation = Quaternion.Euler(rotInput.x, rotInput.y, 0.0f);
            }
        }

        #endregion
    }
}