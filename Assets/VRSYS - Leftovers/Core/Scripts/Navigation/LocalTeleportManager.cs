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
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using VRSYS.Core.Avatar;
using VRSYS.Core.Utility;

namespace VRSYS.Core.Navigation
{
    public class LocalTeleportManager : MonoBehaviour, ITeleportManagerCallbacks
    {
        public Transform headTransform;

        public HandType handType;
        private HandType currentHandType;
        public ActionBasedController leftController;
        public ActionBasedController rightController;
        private InputActionProperty teleportActionValue;
        private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor;

        [SerializeField] private GameObject previewAvatarPrefab;

        private AvatarHMDAnatomy previewAvatarInstance;

        private ProgressVisualization previewProgress;

        private float currentTriggerProgress;

        private Transform targetParentTransform;

        private float orientationSpecificationMinDistance = 0.25f;

        private Vector3 currentJumpNormal
        {
            get
            {
                return previewAvatarInstance.transform.up;
            }
        }

        // interaction thresholds
        private float activationThreshold = 0.05f;
        private float lockThreshold = 0.9f;
        private float lastTriggerValue = 0.0f;
        private bool canJump = false;

        // Start is called before the first frame update
        void Start()
        {

            var avatarGo = Instantiate(previewAvatarPrefab);
            previewAvatarInstance = avatarGo.GetComponent<AvatarHMDAnatomy>();
            previewAvatarInstance.name = gameObject.name + " Avatar Preview";
            previewProgress = avatarGo.GetComponentInChildren<ProgressVisualization>();
            avatarGo.SetActive(false);
            ApplyHandTypeUpdate();
        }

        private void Update()
        {
            if (handType != currentHandType)
                ApplyHandTypeUpdate();
            EvaluateTeleportExecution();
        }

        private void EvaluateTeleportExecution()
        {
            var triggerValue = GetTriggerValue();
            if (lastTriggerValue > activationThreshold && triggerValue < activationThreshold)
                PerformTeleport();
            lastTriggerValue = triggerValue;
        }

        public void OnDestroy()
        {
            Destroy(previewAvatarInstance);
        }

        private void ApplyHandTypeUpdate()
        {
            currentHandType = handType;

            teleportActionValue = currentHandType == HandType.Left
                ? leftController.selectActionValue
                : rightController.selectActionValue;

            rayInteractor = currentHandType == HandType.Left
                ? leftController.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>()
                : rightController.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
        }

        private void UpdatePreviewPosition(RaycastHit hit)
        {
            previewAvatarInstance.transform.rotation = Quaternion.FromToRotation(headTransform.up, hit.normal) * headTransform.rotation;
            previewAvatarInstance.transform.position = hit.point;
            previewAvatarInstance.head.transform.localPosition = CalcUserHeight() * Vector3.up;
            previewAvatarInstance.gameObject.SetActive(true);
            previewProgress?.SetProgress(currentTriggerProgress);
        }

        private void UpdatePreviewOrientation(Vector3 currentHitPoint)
        {
            var jumpPosition = previewAvatarInstance.transform.position;

            if ((currentHitPoint - jumpPosition).magnitude < orientationSpecificationMinDistance)
                return;

            var forwardLookAt = UtilityFunctions.ClosestPointOnPlane(jumpPosition, currentJumpNormal, currentHitPoint);
            var forward = (forwardLookAt - jumpPosition).normalized;
            var up = currentJumpNormal;

            previewAvatarInstance.transform.rotation = Quaternion.LookRotation(forward, up);
            previewAvatarInstance.head.transform.localPosition = CalcUserHeight() * Vector3.up;
            previewProgress?.SetProgress(currentTriggerProgress);
        }

        public void SetTargetPosition(RaycastHit hit)
        {
            // Set parent so we can update initial hit position based on moving player (due to parent update)
            previewAvatarInstance.transform.SetParent(transform.parent, worldPositionStays: true);
            UpdatePreviewPosition(hit);
        }

        public void RefineTargetPositionSpecification(RaycastHit hit, float triggerProgress)
        {
            currentTriggerProgress = triggerProgress;
            UpdatePreviewPosition(hit);
        }

        public void RefineTargetRotationSpecification(RaycastHit currentHit, float triggerProgress)
        {
            currentTriggerProgress = triggerProgress;
            UpdatePreviewOrientation(currentHit.point);
        }

        public void Cancel()
        {
            previewAvatarInstance?.gameObject.SetActive(false);
        }

        public void PerformTeleport()
        {
            if (!canJump)
            {
                Cancel();
                return;
            }

            var initialHitPoint = previewAvatarInstance.transform.position;

            // account for tracking y-rotation offset and apply preview rotation to user
            var inverseLocalCamRotation = Quaternion.Inverse(Quaternion.Euler(0, headTransform.localEulerAngles.y, 0));
            transform.rotation = previewAvatarInstance.transform.rotation * inverseLocalCamRotation;

            // account for tracking offset from tracking origin and apply preview position to user
            Vector3 inverseLocalCamPosition = UtilityFunctions.ClosestPointOnPlane(transform.position, transform.up, headTransform.position) - transform.position;
            transform.position = initialHitPoint - inverseLocalCamPosition;

            transform.SetParent(targetParentTransform, worldPositionStays: true);

            previewAvatarInstance.gameObject.SetActive(false);

            canJump = false;
        }

        private float CalcUserHeight()
        {
            return headTransform.localPosition.y * headTransform.parent.lossyScale.y;
        }

        public float GetTriggerValue()
        {
            return teleportActionValue.action.ReadValue<float>();
        }

        public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor GetRayInteractor()
        {
            return rayInteractor;
        }

        public void EvaluateRayHit(RaycastHit hit)
        {
            var triggerValue = GetTriggerValue();
            var progress = Mathf.Clamp((triggerValue - activationThreshold) /
                                       (lockThreshold - activationThreshold), 0, 1);

            if (triggerValue > activationThreshold)
            {
                if (triggerValue > lockThreshold || canJump)
                {
                    RefineTargetRotationSpecification(hit, 1);
                    canJump = true;
                }
                else
                {
                    RefineTargetPositionSpecification(hit, progress);
                }
            }
        }

        public void EvaluateTargetParentUpdate(Transform targetParentTransform)
        {
            if (GetTriggerValue() < lockThreshold)
                this.targetParentTransform = targetParentTransform;
        }

        public void EvaluateTargetParentReset()
        {
            if (GetTriggerValue() < lockThreshold)
                targetParentTransform = transform.parent;
        }
    }
}
