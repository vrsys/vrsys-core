using System.Collections.Generic;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace VRSYS.HandTracking.XRMultiplayerScripts
{
    /// <summary>
    /// This class will synchronize the hand poses over the network.
    /// It will also allow for the user to control the fidelity of the hand poses and how much data is being sent over the network.
    /// This class is going to have major changes in the near future based on the Hand Pose work being done.
    /// </summary>
    public class XRHandPoseReplicator : NetworkBehaviour
    {
        public XRHandSkeletonDriver leftTrackedHand;
        public XRHandSkeletonDriver rightTrackedHand;
        
        /// <summary>
        /// Controls the level of fidelity and how much data is being sent over the network.
        /// 0 is highest level of fidelity and the most bandwidth, 2 is the lowest and the least bandwidth.
        /// </summary>
        [Header("Hands and Fingers"), Tooltip("0 is highest, 2 is lowest")]
        [Range(0, 2), SerializeField] int m_FidelityLevel;
        [SerializeField, Tooltip("Determines minimum value threshold for updating finger rotations")] float m_MinUpdateDelta = .1f;
        [SerializeField] JointBasedHand[] m_HandCurler;

        [SerializeField] float m_FingerLerpSpeed = 20.0f;
        [SerializeField] bool m_UpdateHandsLocally;
        
        public bool UpdateHandsLocally => m_UpdateHandsLocally;

        // [Header("Controller Inputs")]
        // [SerializeField] InputActionProperty[] m_GripInputProperties;
        // [SerializeField] InputActionProperty[] m_TriggerInputProperties;
        // [SerializeField] InputActionProperty[] m_ThumbTouchProperties;

        NetworkList<Vector3> m_FingerRotationsLeft;
        NetworkList<Vector3> m_FingerRotationsRight;

        NetworkList<float> m_FingerCurlLeft;
        NetworkList<float> m_FingerCurlRight;

        NetworkVariable<bool> m_IsInitialized = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        Pose[] m_handTrackedStartPose = new Pose[2];

        [Header("Offsets")]
        // [SerializeField] Vector3[] m_HandControllerOffsets;
        // [SerializeField] Vector3[] m_HandControllerEulerOffsets;
        HandFidelityOption[] m_LocalHandFidelityOptions;

        // public XRInputModalityManager.InputMode trackingType { get => m_TrackingType.Value; }
        // readonly NetworkVariable<XRInputModalityManager.InputMode> m_TrackingType = new(XRInputModalityManager.InputMode.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        //
        // XRInputModalityManager m_XRModalityManager;
        // XROrigin m_XROrigin;

        Transform m_LeftHandTransformReference;
        Transform m_RightHandTransformReference;

        Transform m_LeftControllerTransformReference;
        Transform m_RightControllerTransformReference;

        /// <summary>
        /// Internal references to the Local Player Transforms.
        /// </summary>
        protected Transform m_LeftHandOrigin, m_RightHandOrigin;

        private void Awake()
        {
            m_FingerRotationsLeft = new NetworkList<Vector3>(new List<Vector3>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            m_FingerRotationsRight = new NetworkList<Vector3>(new List<Vector3>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

            m_FingerCurlLeft = new NetworkList<float>(new List<float>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            m_FingerCurlRight = new NetworkList<float>(new List<float>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

            for (int i = 0; i < m_HandCurler.Length; i++)
            {
                m_handTrackedStartPose[i].position = m_HandCurler[i].transform.localPosition;
                m_handTrackedStartPose[i].rotation = m_HandCurler[i].transform.localRotation;
            }
        }

        // private void OnEnable()
        // {
        //     m_TrackingType.OnValueChanged += UpdateTrackingType;
        // }
        //
        // private void OnDisable()
        // {
        //     m_TrackingType.OnValueChanged -= UpdateTrackingType;
        // }

        // public override void OnDestroy()
        // {
        //     base.OnDestroy();
        //     if (IsOwner)
        //     {
        //         m_XRModalityManager.trackedHandModeStarted.RemoveListener(SwapToHands);
        //         m_XRModalityManager.motionControllerModeStarted.RemoveListener(SwapToControllers);
        //     }
        // }

        private void Update()
        {
            if (!m_IsInitialized.Value || !NetworkManager.IsConnectedClient || NetworkManager.ShutdownInProgress) return;

            // if (trackingType == XRInputModalityManager.InputMode.TrackedHand)
            // {
            // TODO: Check InputType==TrackedHand in Networked Input Modality Manager
            switch (m_FidelityLevel)
            {
                case 0:
                    SyncAllFingerData();
                    break;
                case 1:
                    SyncFingerCurl();
                    break;
                case 2:
                    SyncFingerCurlLimited();
                    break;
            }
            // }
            // else
            // {
            //     SyncControllerTracking();
            // }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // if (IsOwner)
            // {
            //     m_XROrigin = FindFirstObjectByType<XROrigin>();
            //     if (m_XROrigin.TryGetComponent(out m_XRModalityManager))
            //     {
            //         SetupLocalHands();
            //     }
            //
            //     SetupLocalFingerReferences();
            // }
            // else
            // {
            //     ChangeControllerType(m_TrackingType.Value);
            // }
            if (IsOwner)
            {
                m_LeftHandTransformReference = leftTrackedHand.rootTransform;
                m_RightHandTransformReference = rightTrackedHand.rootTransform;
                SetupLocalFingerReferences();
            }
            else
            {
                SetFidelity(m_FidelityLevel);
                ResetHandsToStart();
            }
        }

        // void UpdateTrackingType(XRInputModalityManager.InputMode old, XRInputModalityManager.InputMode current)
        // {
        //     ChangeControllerType(current);
        // }

        // void SetupLocalHands()
        // {
        //     m_LeftControllerTransformReference = m_XRModalityManager.leftController.transform;
        //     m_RightControllerTransformReference = m_XRModalityManager.rightController.transform;
        //
        //     if (m_XRModalityManager.leftHand == null)    //Rig doesn't have hands setup
        //     {
        //         m_LeftHandTransformReference = m_XRModalityManager.leftController.transform;
        //         m_RightHandTransformReference = m_XRModalityManager.rightController.transform;
        //         SetTrackingType(XRInputModalityManager.InputMode.MotionController);
        //     }
        //     else    //Setup Hands and modality change listeners
        //     {
        //         m_LeftHandTransformReference = m_XRModalityManager.leftHand.GetComponentInChildren<XRHandSkeletonDriver>().rootTransform;
        //         m_RightHandTransformReference = m_XRModalityManager.rightHand.GetComponentInChildren<XRHandSkeletonDriver>().rootTransform;
        //
        //         SetTrackingType(XRInputModalityManager.currentInputMode.Value);
        //
        //         m_XRModalityManager.trackedHandModeStarted.AddListener(SwapToHands);
        //         m_XRModalityManager.motionControllerModeStarted.AddListener(SwapToControllers);
        //     }
        // }

        void SetupLocalFingerReferences()
        {
            // XRInputModalityManager modalityMangager = FindFirstObjectByType<XRInputModalityManager>();
            //
            // // Early out if hands are not setup
            // if (modalityMangager.leftHand == null)
            // {
            //     // Set Default values for finger rotations
            //     for (int i = 0; i < 3; i++)
            //     {
            //         m_FingerCurlLeft.Add(0.0f);
            //         m_FingerCurlRight.Add(0.0f);
            //     }
            //
            //     m_IsInitialized.Value = true;
            //     ChangeControllerType(XRInputModalityManager.InputMode.MotionController);
            //     return;
            // }
            
            // XRHandSkeletonDriver localLeftHandSkeleton = modalityMangager.leftHand.GetComponentInChildren<XRHandSkeletonDriver>();
            // XRHandSkeletonDriver localRightHandSkeleton = modalityMangager.rightHand.GetComponentInChildren<XRHandSkeletonDriver>();
            
            // Presume we have hands
            XRHandSkeletonDriver localLeftHandSkeleton = leftTrackedHand;
            XRHandSkeletonDriver localRightHandSkeleton = rightTrackedHand;

            m_LocalHandFidelityOptions = new HandFidelityOption[2];
            for (int i = 0; i < m_LocalHandFidelityOptions.Length; i++)
            {
                m_LocalHandFidelityOptions[i].fingerJoints = new FingerJoints[5];

                // Loop through each finger and setup name and joints
                for (int j = 0; j < m_LocalHandFidelityOptions[i].fingerJoints.Length; j++)
                {
                    m_LocalHandFidelityOptions[i].fingerJoints[j].fingerName = m_HandCurler[i].handFidelityOptions[0].fingerJoints[j].fingerName;
                    m_LocalHandFidelityOptions[i].fingerJoints[j].jointTransformReferences = new List<JointToTransformReference>();

                    // Loop through each joint in the finger and setup the joint references
                    for (int k = 0; k < m_HandCurler[i].handFidelityOptions[0].fingerJoints[j].jointTransformReferences.Count; k++)
                    {
                        XRHandSkeletonDriver currentHandSkeletonDriver = i % 2 == 0 ? localLeftHandSkeleton : localRightHandSkeleton;
                        // Loop through each local hand and look up the joint reference
                        foreach (var localJoint in currentHandSkeletonDriver.jointTransformReferences)
                        {
                            if (m_HandCurler[i].handFidelityOptions[0].fingerJoints[j].jointTransformReferences[k].xrHandJointID == localJoint.xrHandJointID)
                            {
                                m_LocalHandFidelityOptions[i].fingerJoints[j].jointTransformReferences.Add(localJoint);
                                break;
                            }
                        }
                    }
                }
            }

            foreach (var fingerSync in m_LocalHandFidelityOptions[0].fingerJoints)
            {
                foreach (var joint in fingerSync.jointTransformReferences)
                {
                    m_FingerRotationsLeft.Add(joint.jointTransform.eulerAngles);
                }
            }

            foreach (var fingerSync in m_LocalHandFidelityOptions[1].fingerJoints)
            {
                foreach (var joint in fingerSync.jointTransformReferences)
                {
                    m_FingerRotationsRight.Add(joint.jointTransform.eulerAngles);
                }
            }


            // Set Default values for finger curl
            foreach (var fingerSync in m_LocalHandFidelityOptions[0].fingerJoints)
            {
                m_FingerCurlLeft.Add(0.0f);
            }

            foreach (var fingerSync in m_LocalHandFidelityOptions[1].fingerJoints)
            {
                m_FingerCurlRight.Add(0.0f);
            }


            m_IsInitialized.Value = true;

            SetFidelity(m_FidelityLevel);
        }

        // public void ChangeControllerType(XRInputModalityManager.InputMode inputMode)
        // {
        //     if (inputMode == XRInputModalityManager.InputMode.MotionController)
        //     {
        //         SetFidelity(2);
        //         SetHandsToControllerOffset();
        //     }
        //     else
        //     {
        //         SetFidelity(m_FidelityLevel);
        //         ResetHandsToStart();
        //     }
        // }

        void SetFidelity(int fidelity)
        {
            fidelity = Mathf.Clamp(fidelity, 0, 2);

            m_HandCurler[0].fidelityLevel = fidelity;
            m_HandCurler[1].fidelityLevel = fidelity;

            m_HandCurler[0].useCurl = fidelity > 0;
            m_HandCurler[1].useCurl = fidelity > 0;
        }

        void SyncAllFingerData()
        {
            if (IsOwner)
            {
                SetNetworkFingerRotations();

                if (m_UpdateHandsLocally)
                {
                    GetNetworkFingerRotations();
                }
            }
            else
            {
                GetNetworkFingerRotations();
            }
        }

        void SetNetworkFingerRotations()
        {
            int currentIdx = 0;
            for (int i = 0; i < m_LocalHandFidelityOptions[0].fingerJoints.Length; i++)
            {
                for (int j = 0; j < m_LocalHandFidelityOptions[0].fingerJoints[i].jointTransformReferences.Count; j++)
                {
                    m_FingerRotationsLeft[currentIdx++] =
                        m_LocalHandFidelityOptions[0].fingerJoints[i].jointTransformReferences[j].jointTransform.eulerAngles;

                }
            }

            currentIdx = 0;
            for (int i = 0; i < m_LocalHandFidelityOptions[1].fingerJoints.Length; i++)
            {
                for (int j = 0; j < m_LocalHandFidelityOptions[1].fingerJoints[i].jointTransformReferences.Count; j++)
                {
                    m_FingerRotationsRight[currentIdx++] =
                        m_LocalHandFidelityOptions[1].fingerJoints[i].jointTransformReferences[j].jointTransform.eulerAngles;
                }
            }
        }

        void GetNetworkFingerRotations()
        {
            int currentIdx = 0;
            int hand = 0;
            for (int i = 0; i < m_HandCurler[hand].handFidelityOptions[0].fingerJoints.Length; i++)
            {
                for (int j = 0; j < m_HandCurler[hand].handFidelityOptions[0].fingerJoints[i].jointTransformReferences.Count; j++)
                {
                    m_HandCurler[hand].handFidelityOptions[0].fingerJoints[i].jointTransformReferences[j].jointTransform.rotation =
                        Quaternion.Slerp(m_HandCurler[hand].handFidelityOptions[0].fingerJoints[i].jointTransformReferences[j].jointTransform.rotation,
                        Quaternion.Euler(m_FingerRotationsLeft[currentIdx++]),
                        Time.deltaTime * m_FingerLerpSpeed);
                }
            }

            currentIdx = 0;
            hand = 1;
            for (int i = 0; i < m_HandCurler[hand].handFidelityOptions[0].fingerJoints.Length; i++)
            {
                for (int j = 0; j < m_HandCurler[hand].handFidelityOptions[0].fingerJoints[i].jointTransformReferences.Count; j++)
                {
                    m_HandCurler[hand].handFidelityOptions[0].fingerJoints[i].jointTransformReferences[j].jointTransform.rotation =
                       Quaternion.Slerp(m_HandCurler[hand].handFidelityOptions[0].fingerJoints[i].jointTransformReferences[j].jointTransform.rotation,
                       Quaternion.Euler(m_FingerRotationsRight[currentIdx++]),
                       Time.deltaTime * m_FingerLerpSpeed);
                }
            }
        }

        void SyncFingerCurl()
        {
            if (IsOwner)
            {
                SetNetworkCurl();

                if (m_UpdateHandsLocally)
                {
                    GetNetworkCurl();
                }
            }
            else
            {
                GetNetworkCurl();
            }
        }

        void SetNetworkCurl()
        {
            for (int i = 0; i < m_LocalHandFidelityOptions[0].fingerJoints.Length; i++)
            {
                SetLocalNetworkFingerCurl(0, i, GetAverageX(0, i));
            }

            for (int i = 0; i < m_LocalHandFidelityOptions[1].fingerJoints.Length; i++)
            {
                SetLocalNetworkFingerCurl(1, i, GetAverageX(1, i));
            }
        }

        void GetNetworkCurl()
        {
            for (int i = 0; i < m_HandCurler[0].handFidelityOptions[0].fingerJoints.Length; i++)
            {
                m_HandCurler[0].SetCurl(i, m_FingerCurlLeft[i]);
            }

            for (int i = 0; i < m_HandCurler[1].handFidelityOptions[0].fingerJoints.Length; i++)
            {
                m_HandCurler[1].SetCurl(i, m_FingerCurlRight[i]);
            }
        }


        float GetAverageX(int hand, int finger)
        {
            float x = 0;
            int digitCount = 4;

            if (finger == 0)     //Thumbs have 1 less joint
            {
                digitCount--;
            }

            for (int i = 1; i < digitCount; i++)
            {
                float currentX = m_LocalHandFidelityOptions[hand].fingerJoints[finger].jointTransformReferences[i].jointTransform.localEulerAngles.x;
                if (currentX < 0 || currentX > 180)
                {
                    currentX = 0;
                }
                x += currentX;
            }

            float avg = Mathf.Clamp(x / (digitCount - 1), 0, 100);

            return avg / 100;
        }

        void SyncFingerCurlLimited()
        {
            if (IsOwner)
            {
                SetNetworkCurlLimited();

                if (m_UpdateHandsLocally)
                {
                    GetNetworkCurlLimited();
                }
            }
            else
            {
                GetNetworkCurlLimited();
            }
        }

        void SetNetworkCurlLimited()
        {

            for (int i = 0; i < 2; i++)
            {
                SetLocalNetworkFingerCurl(0, i, GetAverageX(0, i));
            }

            for (int i = 0; i < 2; i++)
            {
                SetLocalNetworkFingerCurl(1, i, GetAverageX(1, i));
            }

            SetLocalNetworkFingerCurl(0, 2, GetAverageXCombined(0));
            SetLocalNetworkFingerCurl(1, 2, GetAverageXCombined(1));
        }

        void GetNetworkCurlLimited()
        {
            for (int i = 0; i < 3; i++)
            {
                m_HandCurler[0].SetCurl(i, m_FingerCurlLeft[i]);
            }

            for (int i = 0; i < 3; i++)
            {
                m_HandCurler[1].SetCurl(i, m_FingerCurlRight[i]);
            }
        }

        float GetAverageXCombined(int hand)
        {
            float x = 0;

            int digitCount = 4;
            int startFinger = 2;
            int endFinger = 5;

            int count = 0;
            for (int i = startFinger; i < endFinger; i++)
            {
                for (int j = 1; j < digitCount; j++)
                {
                    float currentX = m_LocalHandFidelityOptions[hand].fingerJoints[i].jointTransformReferences[j].jointTransform.localEulerAngles.x;
                    if (currentX < 0 || currentX > 180)
                    {
                        currentX = 0;
                    }
                    x += currentX;
                    count++;
                }
            }

            float avg = Mathf.Clamp(x / count, 0, 100);

            return avg / 100;
        }

        // void SyncControllerTracking()
        // {
        //     //TODO: Sync Controller Input and map to hand poses
        //     if (IsOwner)
        //     {
        //         SetNetworkControllerFingerSync();
        //         if (m_UpdateHandsLocally)
        //         {
        //             GetNetworkedControllerFingerSync();
        //         }
        //     }
        //     else
        //     {
        //         GetNetworkedControllerFingerSync();
        //     }
        // }
        //
        // void SetNetworkControllerFingerSync()
        // {
        //     SetLocalNetworkFingerCurl(0, 0, m_ThumbTouchProperties[0].action?.ReadValue<float>() ?? 0.0f);
        //     SetLocalNetworkFingerCurl(0, 1, m_TriggerInputProperties[0].action?.ReadValue<float>() ?? 0.0f);
        //     SetLocalNetworkFingerCurl(0, 2, m_GripInputProperties[0].action?.ReadValue<float>() ?? 0.0f);
        //
        //     SetLocalNetworkFingerCurl(1, 0, m_ThumbTouchProperties[1].action?.ReadValue<float>() ?? 0.0f);
        //     SetLocalNetworkFingerCurl(1, 1, m_TriggerInputProperties[1].action?.ReadValue<float>() ?? 0.0f);
        //     SetLocalNetworkFingerCurl(1, 2, m_GripInputProperties[1].action?.ReadValue<float>() ?? 0.0f);
        // }

        void SetLocalNetworkFingerCurl(int hand, int finger, float value)
        {
            if (hand == 0)
            {
                if (Mathf.Abs(m_FingerCurlLeft[finger] - value) > m_MinUpdateDelta)
                    m_FingerCurlLeft[finger] = value;
            }
            else
            {
                if (Mathf.Abs(m_FingerCurlRight[finger] - value) > m_MinUpdateDelta)
                    m_FingerCurlRight[finger] = value;
            }
        }
        //
        // void GetNetworkedControllerFingerSync()
        // {
        //     GetNetworkCurlLimited();
        // }
        //
        // void SwapToHands()
        // {
        //     SetTrackingType(XRInputModalityManager.InputMode.TrackedHand);
        // }
        //
        // void SwapToControllers()
        // {
        //     SetTrackingType(XRInputModalityManager.InputMode.MotionController);
        // }
        //
        // public void SetTrackingType(XRInputModalityManager.InputMode trackingType)
        // {
        //     m_TrackingType.Value = trackingType;
        //     if (trackingType == XRInputModalityManager.InputMode.MotionController)
        //     {
        //         m_LeftHandOrigin = m_LeftControllerTransformReference;
        //         m_RightHandOrigin = m_RightControllerTransformReference;
        //     }
        //     else
        //     {
        //         m_LeftHandOrigin = m_LeftHandTransformReference;
        //         m_RightHandOrigin = m_RightHandTransformReference;
        //     }
        //
        //     // XRINetworkPlayer.LocalPlayer.SetHandOrigins(m_LeftHandOrigin, m_RightHandOrigin);
        //     
        // }

        void ResetHandsToStart()
        {
            for (int i = 0; i < m_HandCurler.Length; i++)
            {
                m_HandCurler[i].transform.localPosition = m_handTrackedStartPose[i].position;
                m_HandCurler[i].transform.localRotation = m_handTrackedStartPose[i].rotation;
            }
        }
        
        // void SetHandsToControllerOffset()
        // {
        //     for (int i = 0; i < m_HandCurler.Length; i++)
        //     {
        //         m_HandCurler[i].transform.localPosition = m_HandControllerOffsets[i];
        //         m_HandCurler[i].transform.localRotation = Quaternion.Euler(m_HandControllerEulerOffsets[i]);
        //     }
        // }
    }
}
