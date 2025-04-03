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
//   Date:           2025
//-----------------------------------------------------------------

using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Avatar;
using VRSYS.Core.Logging;
using VRSYS.Core.Utility;

namespace VRSYS.Core.Navigation
{
    public class ThumbstickSteering : MonoBehaviour
    {
        #region Enums

        public enum SteeringDirection
        {
            Head,
            LeftHand,
            RightHand
        }

        public enum RotationMode
        {
            Continuous,
            Snap
        }

        #endregion
        
        #region Member Variables

        [Header("Input Actions")] 
        
        [Tooltip("Expects input as Vector2 from left thumbstick.")]
        public InputActionProperty leftThumbstick;
        [Tooltip("Expects input as Vector2 from right thumbstick")]
        public InputActionProperty rightThumbstick;

        [Header("Steering Configuration")] 
        
        [Tooltip("This bool can be set to false to disable steering.")]
        public bool steeringEnabled = true;
        [Tooltip("Transform that gets modified during steering. If not configured the transform is used, that this script is attached to.")]
        public Transform steeringTarget;
        [Tooltip("This determines which thumbstick is responsible for steering. The other thumbstick will be used for rotation implicitly.")]
        public HandType steeringHand = HandType.Left;
        [Tooltip("This determines the reference which sets the direction of the steering. LeftHand/RightHand: steering in the direction the respective hand is pointing, Head: steering in the direction the user is looking")]
        public SteeringDirection steeringDirection = SteeringDirection.LeftHand;
        [Tooltip("Steering speed in m/s.")]
        [Range(0, 10)] public float steeringSpeed = 3f;
        [Tooltip("If set to true, user can also steer up- and down-wards. If set to false, steering is limited to xz-plane.")]
        public bool verticalSteering = false;

        [Header("Rotation Configuration")] 
        
        [Tooltip("This bool can be set to false to disable rotation.")]
        public bool rotationEnabled = true;
        [Tooltip("Transform that gets modified during rotation. If not configured the transform is used, that this script is attached to.")]
        public Transform rotationTarget;
        [Tooltip("Transform around which the rotation is executed. If not configured the transform is used, that this script is attached to.")]
        public Transform rotationReference;
        [Tooltip("This determines if a continuous rotation is applied or a step-wise/snap rotation.")]
        public RotationMode rotationMode = RotationMode.Continuous;
        [Tooltip("Rotation speed in degree/s (only applies if continuous rotation is selected).")]
        [Range(0, 360)] public float continuousRotationSpeed = 180f;
        [Tooltip("Rotation amount in degree (only applies if snap rotation is selected).")]
        [Range(0, 180)] public float snapRotationAmount = 30f;
        [Tooltip("If true, snapping the thumbstick down results in a 180 degree turn (only applies if snap rotation is selected.")]
        public bool enableDirectionFlip = false;

        // Variables related to initialization
        private bool initialized = false;
        
        // Variables related to determining steering direction
        private Transform head;
        private Transform leftHand;
        private Transform rightHand;

        private Transform forwardIndicator
        {
            get
            {
                Transform indicator = null;

                switch (steeringDirection)
                {
                    case SteeringDirection.Head:
                        indicator = head;
                        break;
                    case SteeringDirection.LeftHand:
                        indicator = leftHand;
                        break;
                    case SteeringDirection.RightHand:
                        indicator = rightHand;
                        break;
                }

                return indicator;
            }
        }

        private Vector3 forwardDirection
        {
            get
            {
                Vector3 direction = Vector3.zero;

                switch (steeringDirection)
                {
                    case SteeringDirection.Head:
                        direction = head.forward;
                        break;
                    case SteeringDirection.LeftHand:
                        direction = leftHand.forward;
                        break;
                    case SteeringDirection.RightHand:
                        direction = rightHand.forward;
                        break;
                }

                if (!verticalSteering)
                    direction.y = 0f;
                
                return direction;
            }
        }

        // Variables related to rotation
        private float snapThreshold = 0.9f;
        private float lastFlipInput = 0.0f;
        private float lastRotInput = 0.0f;

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

            Initialize();
        }

        private void Update()
        {
            if (!initialized)
            {
                Initialize();
                return;
            }

            ApplySteering();
            ApplyRotation();
        }

        #endregion

        #region Custom Methods

        private void Initialize()
        {
            AvatarHMDAnatomy anatomy = GetComponent<AvatarHMDAnatomy>();
            
            if (anatomy == null)
            {
                ExtendedLogger.LogError(GetType().Name, "This component has to be attached to the root of a HMD user with a NetworkUser and AvatarHMDAnatomy component.", this);
                return;
            }

            if (steeringTarget == null)
                steeringTarget = transform;

            if (rotationReference == null)
                rotationReference = transform;
            
            head = anatomy.head;
            leftHand = anatomy.leftHand;
            rightHand = anatomy.rightHand;

            initialized = true;
        }

        private void ApplySteering()
        {
            Vector2 input = steeringHand == HandType.Left
                ? leftThumbstick.action.ReadValue<Vector2>()
                : rightThumbstick.action.ReadValue<Vector2>();

            Vector3 direction = GetSteeringDirection(input);

            steeringTarget.position += direction * (steeringSpeed * input.magnitude * Time.deltaTime);
        }

        private Vector3 GetSteeringDirection(Vector2 input)
        {
            // get angle between thumbstick forward and input
            float angle = Vector2.SignedAngle(input, Vector2.up);

            // translate thumbstick direction into indicator coordinates
            Vector3 direction = Quaternion.AngleAxis(angle, verticalSteering ? forwardIndicator.up : Vector3.up) * forwardDirection;

            // limit to xz-plane if vertical steering deactivated
            direction.y = verticalSteering ? direction.y : 0;

            // return direction normalized
            return direction.normalized;
        }

        private void ApplyRotation()
        {
            Vector2 input = steeringHand == HandType.Left
                ? rightThumbstick.action.ReadValue<Vector2>()
                : leftThumbstick.action.ReadValue<Vector2>();
            
            if (rotationMode == RotationMode.Continuous)
                ApplyContinuousRotation(input);
            else
            {
                ApplySnapRotation(input);
            }

            lastFlipInput = input.y;
            lastRotInput = input.x;
        }

        private void ApplyContinuousRotation(Vector2 input)
        {
            float angle = input.x * continuousRotationSpeed * Time.deltaTime;
            rotationTarget.RotateAround(rotationReference.position, Vector3.up, angle);
        }

        private void ApplySnapRotation(Vector2 input)
        {
            if(enableDirectionFlip && lastFlipInput > -snapThreshold && input.y <= -snapThreshold)
                rotationTarget.RotateAround(rotationReference.position, Vector3.up, 180);
            
            if(Mathf.Abs(lastRotInput) < snapThreshold && Mathf.Abs(input.x) >= snapThreshold)
            {
                float angle = input.x < 0 ? -snapRotationAmount : snapRotationAmount;
                rotationTarget.RotateAround(rotationReference.position, Vector3.up, angle);
            }
        }

        #endregion
    }
}
