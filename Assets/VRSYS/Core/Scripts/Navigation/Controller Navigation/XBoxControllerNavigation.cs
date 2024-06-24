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
//   Authors:        Lucky Chandrautama
//   Date:           2024
//-----------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Navigation
{
    public class XBoxControllerNavigation : ControllerNavigation
    {
        [Header("Navigation Options")]
        public NavigationTechnique currentNavigationTechnique = NavigationTechnique.Steering;

        [Header("Controller Specific Input Actions")]
        public InputActionProperty translationVelocityAction;
        public InputActionProperty headRotationAction;
        public InputActionProperty headRotationVelocityAction;
        public InputActionProperty techniqueSelectionAction;
        public InputActionProperty resetOrientationAction;

        public enum NavigationTechnique
        {
            Steering,
            Jumping

        }

        private void Start()
        {
            Init();

            currentNavigationTechnique = NavigationTechnique.Steering;
        }


        // Update is called once per frame
        void Update()
        {
            if (!isOfflineOrOwner)
                return;

            TechniqueSelection();
        }


        private void Steering()
        {

            MapSteeringInput(CalculateTranslationInput(), CalculateRotationInput());
            ResetOrientation();

        }

        private void TechniqueSelection()
        {
            float input = techniqueSelectionAction.action.ReadValue<float>();
            int selection = input < 0 ? -1 : input > 0 ? 1 : 0;

            int current = (((int)currentNavigationTechnique) + selection) % (Enum.GetNames(typeof(NavigationTechnique)).Length);
            currentNavigationTechnique = (NavigationTechnique)current;

            switch (currentNavigationTechnique)
            {

                case NavigationTechnique.Jumping:
                    Jumping();
                    break;

                default:
                    Steering();
                    break;
            }
        }

        public void ResetOrientation()
        {
            var resetPressed = resetOrientationAction.action.WasPressedThisFrame();

            if (resetPressed)
                target.rotation = Quaternion.Euler(0, target.rotation.eulerAngles.y, target.rotation.eulerAngles.z);

            if (verbose)
            {
                ExtendedLogger.LogInfo(resetOrientationAction.action.ToString(), "Reset orientation");
            }
        }

        protected override Vector3 CalculateTranslationInput()
        {
            Vector3 xzInput = new Vector3(moveAction.action.ReadValue<Vector2>().x, 0f,
                moveAction.action.ReadValue<Vector2>().y);


            float acceleration = translationVelocity * (translationVelocityAction.action.ReadValue<float>() + 1);

            if (verbose)
            {
                ExtendedLogger.LogInfo(GetType().Name, "acceleration " + acceleration);
                ExtendedLogger.LogInfo(GetType().Name, "xzInput " + xzInput);

            }

            Vector3 transInput = xzInput * (acceleration * Time.deltaTime);


            return transInput;
        }

        protected override Vector3 CalculateRotationInput()
        {
            float headAcceleration = rotationVelocity * (headRotationVelocityAction.action.ReadValue<float>() + 1);

            Vector2 headRotation = headRotationAction.action.ReadValue<Vector2>();
            Vector3 rotInput = new Vector3(headRotation.y, headRotation.x, 0.0f);
            rotInput *= (headAcceleration * Time.deltaTime);
            return rotInput;
        }

        public void Jumping()
        {
            if (verbose)
            {
                ExtendedLogger.LogInfo(GetType().Name, "Jump!");
            }
        }
    }


}
