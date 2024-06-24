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

using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Navigation
{
    public class JoystickNavigation : ControllerNavigation
    {
        #region Member Variables


        [Header("Controller Specific Input Actions")]
        public InputActionProperty verticalMovementAction;
        public InputActionProperty rotateAction;
        
        [Header("Movement Properties")]
        public bool verticalMovement = false;
        public bool flipVerticalMovementMapping = false;
        [Range(0, 10)] public float verticalTranslationVelocity = 3.0f;

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
            MapInput(CalcTranslationInput(), CalcRotationInput());
        }

        #endregion

        #region Custom Methods

        protected override Vector3 CalcTranslationInput()
        {
            Vector3 xzInput = new Vector3(moveAction.action.ReadValue<Vector2>().x, 0f,
                moveAction.action.ReadValue<Vector2>().y);

            ExtendedLogger.LogInfo(GetType().Name, "xzInput " + xzInput);
            
            Vector3 transInput = xzInput * (translationVelocity * Time.deltaTime);

            if (verticalMovement)
            {
                Vector3 yInput = -new Vector3(0f, verticalMovementAction.action.ReadValue<float>(), 0f);
                yInput = flipVerticalMovementMapping ? -yInput : yInput;
                transInput += yInput * (verticalTranslationVelocity * Time.deltaTime);
            }

            return transInput;
        }

        protected override Vector3 CalcRotationInput()
        {
            Vector3 rotInput = new Vector3(0f, rotateAction.action.ReadValue<float>(), 0f);
            rotInput *= (rotationVelocity * Time.deltaTime);
            return rotInput;
        }

        #endregion
    }
}