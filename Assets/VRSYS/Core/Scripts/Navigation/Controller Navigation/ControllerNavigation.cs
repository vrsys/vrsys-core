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
    public abstract class ControllerNavigation : MonoBehaviour
    {
        public Transform target;

        [Header("Base Steering Input Action")]
        public InputActionProperty moveAction;

        [Header("Base Steering Velocities")]
        [Tooltip("Translation Velocity [m/sec]")]
        [Range(0, 10)]
        public float translationVelocity = 3.0f;

        [Tooltip("Rotation Velocity [degree/sec]")]
        [Range(0, 30)]
        public float rotationVelocity = 5.0f;

        [Header("Misc.")]
        [SerializeField]
        protected bool verbose;

        protected bool? isOfflineOrOwner_;
        protected bool isOfflineOrOwner
        {
            get
            {
                if (!isOfflineOrOwner_.HasValue)
                {
                    if (GetComponent<NetworkObject>() is not null)
                        isOfflineOrOwner_ = GetComponent<NetworkObject>().IsOwner;
                    else
                        isOfflineOrOwner_ = true;
                }
                return isOfflineOrOwner_.Value;
            }
        }

        // Start is called before the first frame update
        protected void Init()
        {
            if (!isOfflineOrOwner)
                Destroy(this);
            else if (target == null)
                target = transform;
        }

        // Update is called once per frame
        protected virtual void MapSteeringInput(Vector3 transInput, Vector3 rotInput)
        {
            // map translation input
            if (transInput.magnitude > 0.0f)
                target.Translate(transInput);

            // map rotation input
            if (rotInput.magnitude > 0.0f)
                target.localRotation *= Quaternion.Euler(rotInput);
        }

        protected abstract Vector3 CalculateTranslationInput();
        protected abstract Vector3 CalculateRotationInput();
    }

}

