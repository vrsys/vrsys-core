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
//   Authors:        Sebastian Muehlhaus
//   Date:           2023
//-----------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using VRSYS.Core.Networking;

namespace VRSYS.Core.Projection
{
    public class PortalExitHeadTracking : MonoBehaviour
    {
        public Transform portalEntranceHead;
        public Transform portalEntranceScreen;
        public Transform portalExitScreen;

        public Transform leftEye;
        public Transform rightEye;
        public float eyeDistance = 0.064f;
        
        public bool linkToLocalNetworkUser = false;
        public bool readEyeDistanceFromMainCamera = true;
        
        private List<OffAxisProjection> projections;

        private Vector3 portalEntranceViewDirection => portalEntranceScreen.transform.InverseTransformDirection(portalEntranceScreen.transform.position - portalEntranceHead.transform.position);

        private float _eyeDistance
        {
            get
            {
                if (!readEyeDistanceFromMainCamera)
                    return eyeDistance;
                if (readEyeDistanceFromMainCamera && Camera.main != null)
                    eyeDistance = Camera.main.stereoSeparation;
                return eyeDistance;
            }
        }

        private void Start()
        {
            projections = new(GetComponentsInChildren<OffAxisProjection>());
            
            if (leftEye == null)
            {
                leftEye = projections.Find(
                    p => p.gameObject.name.Contains("left", StringComparison.OrdinalIgnoreCase)
                ).transform;
            }
            
            if (rightEye == null)
            {
                rightEye = projections.Find(
                    p => p.gameObject.name.Contains("right", StringComparison.OrdinalIgnoreCase)
                ).transform;
            }
        }

        // Update is called once per frame
        void LateUpdate()
        {
            if (portalEntranceHead != null)
            {
                var newPosition = portalExitScreen.localPosition - portalEntranceViewDirection;
                newPosition *= (1/transform.lossyScale.x);
                transform.localPosition = newPosition;
                
                if (leftEye != null && rightEye != null)
                {
                    leftEye.transform.localPosition = new Vector3(-0.5f * _eyeDistance, 0, 0);
                    rightEye.transform.localPosition = new Vector3(0.5f * _eyeDistance, 0, 0);
                }

                foreach (var projection in projections)
                {
                    if(!projection.autoUpdateProjection)
                        projection.CalculateProjection();
                }
            }
            else if (linkToLocalNetworkUser && NetworkUser.LocalInstance != null)
            {
                portalEntranceHead = NetworkUser.LocalInstance.head;
            }
        }
    }
}
