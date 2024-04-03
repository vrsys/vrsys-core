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

using System;
using Unity.Netcode;
using UnityEngine;
using VRSYS.Core.Avatar;

namespace VRSYS.Core.Navigation
{
    public class Groundfollowing : MonoBehaviour
    {
        #region Member Variables

        public bool active;
        public LayerMask floorLayers;
        public Transform head;

        public float riseSpeed = 9.81f;
        public float fallSpeed = 2f;
        public float adjustmentThreshold = 0.01f;

        private RaycastHit hit;
        private float targetHeight;
        
        private bool isFalling = false;
        private float fallStartTime;

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            if (GetComponent<NetworkObject>() is not null)
            {
                if (!GetComponent<NetworkObject>().IsOwner)
                {
                    Destroy(this);
                    return;
                }
            }
        }

        private void Update()
        {
            if (!active)
                return;

            UpdateTargetHeight();
            UpdateHeight();
        }

        #endregion

        #region Custom Methods

        private void UpdateTargetHeight()
        {
            // raycast downwards
            if (Physics.Raycast(head.position, -transform.up, out hit,
                    Single.PositiveInfinity, floorLayers))
            {
                targetHeight = hit.point.y;
            }
            // raycast upwards
            else if (Physics.Raycast(head.position, transform.up, out hit,
                         Single.PositiveInfinity, floorLayers))
            {
                targetHeight = hit.point.y;
            }
        }

        private void UpdateHeight()
        {
            float heightDiff = targetHeight - transform.position.y;

            if (Mathf.Abs(heightDiff) < adjustmentThreshold)
            {
                isFalling = false;
                transform.position = new Vector3(transform.position.x, targetHeight, transform.position.z);
            }
            else if (heightDiff < 0) // falling
            {
                if (isFalling == false)
                {
                    isFalling = true;
                    fallStartTime = Time.time;
                }
                
                float fallTime = Time.time - fallStartTime;
                Vector3 fallVec = Vector3.down * Mathf.Min(9.81f / 2f * Mathf.Pow(fallTime, 2f), 100f);
                
                transform.position += fallVec * Time.deltaTime;
            }
            else if (heightDiff > 0) // rising
            {
                float y = heightDiff * (riseSpeed * Time.deltaTime);
                transform.position += new Vector3(0, y, 0);
            } 
        }

        #endregion
    }
}
