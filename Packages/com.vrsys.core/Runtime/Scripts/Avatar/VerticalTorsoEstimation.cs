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
//   Authors:        Sebastian Muehlhaus
//   Date:           2023
//-----------------------------------------------------------------

using UnityEngine;

namespace VRSYS.Core.Avatar
{
    public class VerticalTorsoEstimation : MonoBehaviour
    {
        [Tooltip("The head transform which is used to align the body to. If nothing is specified, transform of parent GameObject will be used.")]
        public Transform headTransform;

        private void Awake()
        {
            if (headTransform == null)
                headTransform = transform.parent;
        }

        void Update()
        {
            ApplyTorsoUpdate(headTransform, transform);
        }

        public static void ApplyTorsoUpdate(Transform headTransform, Transform torsoTransform)
        {
            torsoTransform.position = headTransform.position;

            var eulerX = headTransform.localEulerAngles.x;
            var invRot = Quaternion.Inverse(headTransform.localRotation);
            var lookAtRot = Quaternion.LookRotation(Vector3.Cross(headTransform.localRotation * Vector3.right, Vector3.up));

            torsoTransform.localRotation = invRot * lookAtRot;

            // When user is looking down, we need to move the body back, to avoid clipping into the shirt
            if (eulerX > 0.0f && eulerX < 120.0f)
            {
                float movementFactor;
                if (eulerX < 90.0f)
                {
                    movementFactor = eulerX / 90.0f;
                }
                else
                {
                    eulerX -= 90.0f;
                    movementFactor = 1.0f - eulerX / 30.0f;
                }
                torsoTransform.position += torsoTransform.forward * -(movementFactor * 0.30f * torsoTransform.lossyScale.x);
            }
        }
    }
}