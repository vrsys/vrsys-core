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
        public bool allowNavigation = true;
        public NavigationTechnique currentNavigationTechnique = NavigationTechnique.Steering;
        public bool activateVerticalMovement = false;
        [Range(0, 10)] public float verticalTranslationVelocity = 3.0f;


        [Header("Controller Specific Input Actions")]
        public InputActionProperty translationVelocityAction;
        public InputActionProperty headTransformAction;
        public InputActionProperty headRotationVelocityAction;
        public InputActionProperty verticalMovementSwitch;
        public InputActionProperty techniqueSelectionAction;
        public InputActionProperty untiltingAction;



        public void ToggleVerticalMovement()
        {
            
            if (verticalMovementSwitch.action.WasPressedThisFrame())
            {
                activateVerticalMovement =!activateVerticalMovement;
            }
        }
            

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

            if(allowNavigation)
                ActivateNavigation();


            ResetOrientation();

        }


        private void Steering()
        {
            ToggleVerticalMovement();
            MapSteeringInput(CalculateTranslationInput(), CalculateRotationInput());

        }

        private void ActivateNavigation()
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
            var resetPressed = untiltingAction.action.WasPressedThisFrame();

            if (resetPressed)
                target.rotation = Quaternion.Euler(0, target.rotation.eulerAngles.y, target.rotation.eulerAngles.z);

            if (verbose)
            {
                ExtendedLogger.LogInfo(untiltingAction.action.ToString(), "Reset orientation");
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


            if (activateVerticalMovement)
            {
                float verticalMovement = headTransformAction.action.ReadValue<Vector2>().y;


                Vector3 yInput = new Vector3(0f, verticalMovement, 0f);
                transInput += yInput * (verticalTranslationVelocity * Time.deltaTime);
            }



            return transInput;
        }

        protected override Vector3 CalculateRotationInput()
        {
            float headAcceleration = rotationVelocity * (headRotationVelocityAction.action.ReadValue<float>() + 1);

            Vector2 headRotation = headTransformAction.action.ReadValue<Vector2>();
            
            float pitch = headRotation.x;
            float yaw = headRotation.y;

            Vector3 rotInput;

            if (activateVerticalMovement)
            {
                rotInput = new Vector3(0.0f, pitch, 0.0f);
            }
            else
            {
                rotInput = new Vector3(yaw, pitch, 0.0f);
            }

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
