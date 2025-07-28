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
using VRSYS.Core.Utility;

namespace VRSYS.Core.Navigation
{
    public class HMDSteeringNavigation : MonoBehaviour
    {
        #region Member Variables

        [Header("Controls")] 
        public Transform head;
        public HandType steeringHand;
        public InputActionProperty leftSteeringAction;
        public Transform leftController;
        public InputActionProperty rightSteeringAction;
        public Transform rightController;
        public InputActionProperty leftTurnAction;
        public InputActionProperty rightTurnAction;
        public Transform forwardIndicator;
        public NavigationBounds navigationBounds;
        
        [Header("Steering Properties")]
        public bool verticalSteering;
        [Range(0, 10)] public float steeringSpeed = 3f;
        [Range(0, 100)] public float rotationSpeed = 3f;
        
        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            if(GetComponent<NetworkObject>() != null)
                if (!GetComponent<NetworkObject>().IsOwner)
                {
                    Destroy(this);
                    return;
                }
        }

        private void Update()
        {
            ApplyDisplacement();
            ApplyRotation();
            if (navigationBounds != null)
                EnsureIsInBounds();
        }

        private void EnsureIsInBounds()
        {
            if (navigationBounds.bounds.Contains(head.position))
                return;
            var closestPos = navigationBounds.collider.ClosestPointOnBounds(head.position);
            var displacement = closestPos - head.position;
            transform.position += displacement;
        }

        #endregion

        #region Custom Methods

        private void ApplyDisplacement()
        {
            Vector3 direction = Vector3.zero;
            float speedFactor = 0;
            
            if (steeringHand == HandType.Left)
            {
                speedFactor = leftSteeringAction.action.ReadValue<float>();
                direction = leftController.forward;
            }
            else if (steeringHand == HandType.Right)
            {
                speedFactor = rightSteeringAction.action.ReadValue<float>();
                direction = rightController.forward;
            }

            if (forwardIndicator != null)
                direction = forwardIndicator.forward;

            if (!verticalSteering)
            {
                direction.y = 0;
            }

            Vector3 moveVec = direction.normalized * (speedFactor * steeringSpeed * Time.deltaTime);

            transform.position += moveVec * transform.localScale.x; // including scale to keep perceived velocity constant with scale
        }
        
        private void ApplyRotation()
        {
            float turnFactor = 0;
            if (steeringHand == HandType.Left)
            {
                turnFactor = leftTurnAction.action.ReadValue<Vector2>().x;
            }
            else if (steeringHand == HandType.Right)
            {
                turnFactor = rightTurnAction.action.ReadValue<Vector2>().x;
            }

            transform.RotateAround(head.position, Vector3.up, turnFactor * rotationSpeed * Time.deltaTime);
        }

        #endregion
    }
}