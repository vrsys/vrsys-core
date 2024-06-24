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
//   Authors:        Tony Jan Zoeppig, Sebastian Muehlhaus, Ephraim Schott, Lucky Chandrautama
//   Date:           2024
//-----------------------------------------------------------------

using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRSYS.Core.Navigation
{
    public class DesktopNavigation : ControllerNavigation
    {
        #region Member Variables


        [Header("Controller-Specific Input Actions")] 
        public InputActionProperty yawAction;
        public InputActionProperty pitchAction;
                
        private Vector3 rotInput = Vector3.zero;


        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            Init();        
        }

        private void Update()
        {
            if (!isOfflineOrOwner)
                return;
            MapSteeringInput(CalculateTranslationInput(), CalculateRotationInput());
        }

        #endregion
        
        #region Custom Methods

        protected override Vector3 CalculateTranslationInput()
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

        protected override Vector3 CalculateRotationInput()
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

        protected override void MapSteeringInput(Vector3 transInput, Vector3 rotInput)
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