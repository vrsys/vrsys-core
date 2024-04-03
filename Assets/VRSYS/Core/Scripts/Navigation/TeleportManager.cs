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

using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using VRSYS.Core.Avatar;
using VRSYS.Core.Logging;
using VRSYS.Core.Networking;
using VRSYS.Core.Utility;

namespace VRSYS.Core.Navigation
{
    public class TeleportManager : NetworkBehaviour, ITeleportManagerCallbacks
    {
        #region Serialized Data Type

        public struct PreviewData : INetworkSerializable, System.IEquatable<PreviewData>
        {
            public bool enabled;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public float headHeight;
            public float triggerProgress;

            public bool Equals(PreviewData other)
            {
                return enabled == other.enabled &&
                       position.Equals(other.position, epsilon: 0.001f) &&
                       rotation.Equals(other.rotation, epsilon: 0.001f) &&
                       headHeight.Equals(other.headHeight, epsilon: 0.001f) &&
                       triggerProgress.Equals(other.triggerProgress, epsilon: 0.001f);
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                if (serializer.IsReader)
                {
                    var reader = serializer.GetFastBufferReader();
                    reader.ReadValueSafe(out enabled);
                    reader.ReadValueSafe(out position);
                    reader.ReadValueSafe(out rotation);
                    reader.ReadValueSafe(out scale);
                    reader.ReadValueSafe(out headHeight);
                    reader.ReadValueSafe(out triggerProgress);
                }
                else
                {
                    var writer = serializer.GetFastBufferWriter();
                    writer.WriteValueSafe(enabled);
                    writer.WriteValueSafe(position);
                    writer.WriteValueSafe(rotation);
                    writer.WriteValueSafe(scale);
                    writer.WriteValueSafe(headHeight);
                    writer.WriteValueSafe(triggerProgress);
                }
            }
        }

        #endregion
        
        public HandType handType;
        private HandType currentHandType;
        public ActionBasedController leftController;
        public XRRayInteractor leftRayInteractor;
        public ActionBasedController rightController;
        public XRRayInteractor rightRayInteractor;
        private InputActionProperty teleportActionValue;
        private XRRayInteractor rayInteractor;

        [SerializeField]
        private GameObject previewAvatarPrefab;

        // TODO make private
        public AvatarHMDAnatomy previewAvatarInstance;

        private ProgressVisualization previewProgress;

        private float currentTriggerProgress;

        private Transform targetParentTransform;

        private float orientationSpecificationMinDistance = 0.25f;

        private NetworkVariable<PreviewData> networkedPreview = new NetworkVariable<PreviewData>(default,
                NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private Transform headTransform
        {
            get
            {
                return NetworkUser.LocalInstance.head;
            }
        }

        private Vector3 currentJumpNormal
        {
            get
            {
                return previewAvatarInstance.transform.up;
            }
        }

        private NetworkUser networkUser_;
        private NetworkUser networkUser
        {
            get
            {
                if (networkUser_ == null)
                    networkUser_ = GetComponent<NetworkUser>();
                return networkUser_;
            }
        }

        // interaction thresholds
        private float activationThreshold = 0.05f;
        private float lockThreshold = 0.9f;
        private float lastTriggerValue = 0.0f;
        private bool canJump = false;

        // teleportation target settings
        private bool lockUpdates = false;
        private Vector3 targetPosition;
        private Quaternion targetRotation;

        private string previewInitName = ""; 
        
        #region MonoBehaviour
        
        private void Update()
        {
            if (!IsOwner)
                return;

            if (handType != currentHandType)
                ApplyHandTypeUpdate();
            
            EvaluateTeleportExecution();
        }

        public override void OnDestroy()
        {
            if(previewAvatarInstance != null)
                Destroy(previewAvatarInstance.gameObject);
            base.OnDestroy();
        }

        private void InitPreviewAvatar()
        {
            // Called From OnNetworkSpawn()
            // On scene change, this is executed for remote clients prior to the previous scene being unloaded.
            // Therefore, Unity will associate it with the previous scene.
            // To ensure we have a preview avatar in the new scene, we configure it as DontDestroyOnLoad.
            // Otherwise, the instance is unloaded with the previous scene.
            // Instead, we explicitly clean it up OnDestroy.
            
            var avatarGo = Instantiate(previewAvatarPrefab);
            DontDestroyOnLoad(avatarGo);
            if (previewInitName.Length > 0)
                avatarGo.name = previewInitName;
            
            previewAvatarInstance = avatarGo.GetComponent<AvatarHMDAnatomy>();
            previewProgress = avatarGo.GetComponentInChildren<ProgressVisualization>();
            avatarGo.SetActive(false);
            ApplyHandTypeUpdate(); 
            ApplyPreviewData(networkedPreview.Value);
            UpdatePreviewAvatarInstanceName(networkUser.userName.Value.ToString());
        }
        
        private void ApplyHandTypeUpdate()
        {
            currentHandType = handType;

            teleportActionValue = currentHandType == HandType.Left
                ? leftController.selectActionValue
                : rightController.selectActionValue;
            
            rayInteractor = currentHandType == HandType.Left
                ? leftRayInteractor
                : rightRayInteractor;
        }

        private void EvaluateTeleportExecution()
        {
            var triggerValue = GetTriggerValue();
            if (lastTriggerValue > activationThreshold && triggerValue < activationThreshold)
                PerformTeleport();
            lastTriggerValue = triggerValue;
        }

        public float GetTriggerValue()
        {
            return teleportActionValue.action.ReadValue<float>();
        }

        public XRRayInteractor GetRayInteractor()
        {
            return rayInteractor;
        }

        #endregion

        #region Networking Setup

        public override void OnNetworkSpawn()
        {
            InitPreviewAvatar();
            networkUser.userName.OnValueChanged += OnUserNameChanged;
            if (IsOwner)
            {
                var previewData = new PreviewData();
                previewData.enabled = false;
                networkedPreview.Value = previewData;
                return;
            }
            networkedPreview.OnValueChanged += OnNetworkedPreviewChanged;
        }

        private void OnUserNameChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
        {
            UpdatePreviewAvatarInstanceName(newValue.ToString());
        }

        private void OnNetworkedPreviewChanged(PreviewData previousValue, PreviewData newValue)
        {
            ApplyPreviewData(newValue);
        }

        private void UpdatePreviewAvatarInstanceName(string name)
        {
            if (previewAvatarInstance == null)
            {
                previewInitName = name + (IsOwner ? "[Local]": "[Remote]") + " Avatar Preview";
                return;
            }
            previewAvatarInstance.name = name + (IsOwner ? "[Local]": "[Remote]") + " Avatar Preview";
        }

        private void ApplyPreviewData(PreviewData preview)
        {
            previewAvatarInstance.gameObject.SetActive(preview.enabled);
            previewAvatarInstance.transform.position = preview.position;
            previewAvatarInstance.transform.rotation = preview.rotation;
            previewAvatarInstance.transform.localScale = preview.scale;
            previewAvatarInstance.head.transform.localPosition = preview.headHeight * Vector3.up;
            previewProgress?.SetProgress(preview.triggerProgress);
        }

        private void WriteNetworkedPreviewData()
        {
            PreviewData d = networkedPreview.Value;
            d.enabled = previewAvatarInstance.gameObject.activeSelf;
            d.position = previewAvatarInstance.transform.position;
            d.rotation = previewAvatarInstance.transform.rotation;
            d.scale = previewAvatarInstance.transform.localScale;
            d.headHeight = previewAvatarInstance.head.transform.localPosition.y;
            d.triggerProgress = currentTriggerProgress;
            networkedPreview.Value = d;
        }

        #endregion

        #region TeleportManagerInterface
        
        private void UpdatePreviewPosition(RaycastHit hit)
        {
            if (previewAvatarInstance.transform.parent != transform.parent)
                previewAvatarInstance.transform.SetParent(transform.parent, worldPositionStays: true);
            previewAvatarInstance.transform.rotation = Quaternion.FromToRotation(headTransform.up, hit.normal) * headTransform.rotation;
            previewAvatarInstance.transform.position = hit.point;
            previewAvatarInstance.transform.localScale = transform.localScale;
            previewAvatarInstance.head.transform.localPosition = NetworkUser.CalcLocalHeight() * Vector3.up;
            previewAvatarInstance.gameObject.SetActive(true);
            previewProgress?.SetProgress(currentTriggerProgress);
        }

        private void UpdatePreviewOrientation(Vector3 currentHitPoint)
        {
            if (previewAvatarInstance.transform.parent != targetParentTransform)
                previewAvatarInstance.transform.SetParent(targetParentTransform, worldPositionStays: true);
            
            var jumpPosition = previewAvatarInstance.transform.position;

            if ((currentHitPoint - jumpPosition).magnitude < orientationSpecificationMinDistance * transform.localScale.x)
                return;

            var forwardLookAt = UtilityFunctions.ClosestPointOnPlane(jumpPosition, currentJumpNormal, currentHitPoint);
            var forward = (forwardLookAt - jumpPosition).normalized;
            var up = currentJumpNormal;

            previewAvatarInstance.transform.rotation = Quaternion.LookRotation(forward, up);
            previewAvatarInstance.transform.localScale = transform.localScale;
            previewAvatarInstance.head.transform.localPosition = NetworkUser.CalcLocalHeight() * Vector3.up;
            previewProgress?.SetProgress(currentTriggerProgress);
        }

        public void RefineTargetPositionSpecification(RaycastHit currentHit, float triggerProgress)
        {
            currentTriggerProgress = triggerProgress;
            UpdatePreviewPosition(currentHit);
            WriteNetworkedPreviewData();
        }

        public void RefineTargetRotationSpecification(RaycastHit currentHit, float triggerProgress)
        {
            currentTriggerProgress = triggerProgress;
            UpdatePreviewOrientation(currentHit.point);
            WriteNetworkedPreviewData();
        }

        public void EvaluateRayHit(RaycastHit hit)
        {
            if (lockUpdates)
                return;
            
            var triggerValue = GetTriggerValue();
            var progress = Mathf.Clamp((triggerValue - activationThreshold) /
                                       (lockThreshold - activationThreshold), 0, 1);

            if (triggerValue > activationThreshold)
            {
                if(triggerValue > lockThreshold || canJump)
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
            if (lockUpdates)
                return;
            
            if (GetTriggerValue() < lockThreshold)
                this.targetParentTransform = targetParentTransform;
        }

        public void EvaluateTargetParentReset()
        {
            if (lockUpdates)
                return;
            
            if (GetTriggerValue() < lockThreshold)
                targetParentTransform = transform.parent;
        }

        public void Cancel()
        {
            if (lockUpdates)
                return;
            
            previewAvatarInstance?.gameObject.SetActive(false);
            if (previewAvatarInstance is not null)
                WriteNetworkedPreviewData();
        }

        public void PerformTeleport()
        {
            if (!canJump)
            {
                Cancel();
                return;
            }

            ApplyTeleport();

            
            if(targetParentTransform != transform.parent)
            {
                lockUpdates = true;
                if (targetParentTransform == null)
                    Invoke(nameof(RemoveParentTransformServerRpc), .5f); // invoked with delay to ensure, that position update was serialized to server before reparenting
                else
                {
                    Invoke(nameof(TriggerSetTargetParentTransform), .5f); // invoked with delay to ensure, that position update was serialized to server before reparenting
                }
            }

            previewAvatarInstance.gameObject.SetActive(false);

            canJump = false;

            WriteNetworkedPreviewData();
        }

        private void ApplyTeleport()
        {
            ExtendedLogger.LogInfo(GetType().Name, "Performing teleport...");
            
            // align xr origin with target position and rotation
            transform.position = previewAvatarInstance.transform.position;
            transform.rotation = previewAvatarInstance.transform.rotation;

            // project head pos to xr-origin plane and transform to world coordinates
            var headPos = headTransform.localPosition;
            headPos.y = 0;
            headPos = transform.TransformPoint(headPos);
            
            // adjust xr origin to align head pos with target position
            transform.position += transform.position - headPos;
            transform.RotateAround(headTransform.position, transform.up, -headTransform.localEulerAngles.y);
        }

        private void TriggerSetTargetParentTransform()
        {
            SetTargetParentTransformServerRpc(targetParentTransform.GetComponent<NetworkObject>());
        }
        
        [ServerRpc]
        private void SetTargetParentTransformServerRpc(NetworkObjectReference targetParentRef)
        {
            if(targetParentRef.TryGet(out NetworkObject targetParent))
            {
                NetworkObject.TrySetParent(targetParent, true);
            }
            else
            {
                NetworkObject.TryRemoveParent(true);
            }
        }

        [ServerRpc]
        private void RemoveParentTransformServerRpc()
        {
            NetworkObject.TryRemoveParent(true);
        }

        public override void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
        {
            base.OnNetworkObjectParentChanged(parentNetworkObject);

            if (!IsOwner)
            {
                if(previewAvatarInstance != null)
                    previewAvatarInstance.transform.parent = parentNetworkObject == null ? null : parentNetworkObject.transform;
                return;
            }
            else
            {
                lockUpdates = false;
            }
        }

        #endregion
    }
}
