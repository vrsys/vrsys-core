using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VRSYS.HandTracking.XRMultiplayerScripts
{
    /// <summary>
    /// This class controls the curl of the fingers based on hand tracking or controller tracking.
    /// </summary>
    public class JointBasedHand : MonoBehaviour
    {
        /// <summary>
        /// Controls how the fingers are updated.
        /// Curl allows for a much more lightweight approximation of finger movements.
        /// </summary>
        public bool useCurl
        {
            get => m_UseCurl;
            set => m_UseCurl = value;
        }
        bool m_UseCurl;

        /// <summary>
        /// Controls the level of fidelity for fingers.
        /// </summary>
        /// <remarks>Set from <see cref="XRHandPoseReplicator.SetFidelity(int)"/>.</remarks>
        public int fidelityLevel
        {
            get => m_FidelityLevel;
            set => m_FidelityLevel = value;
        }
        int m_FidelityLevel;

        [Header("Setup Settings")]
        /// <summary>
        /// Specify where the root of the hand is.
        /// </summary>
        [SerializeField, Tooltip("Specify where the root of the hand is.")]
        protected Transform m_HandRoot;

        public Transform RootTransform => m_HandRoot;
        
        /// <summary>
        /// Specify the names of the fingers.
        /// </summary>
        [SerializeField, Tooltip("Specify the names of the fingers.")]
        protected string[] m_FingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };

        /// <summary>
        /// Specify the start index of the finger joints.
        /// </summary>
        [SerializeField, Tooltip("Specify the start index of the finger joints.")]
        protected XRHandJointID[] m_FingerStartJointIds = { XRHandJointID.ThumbMetacarpal, XRHandJointID.IndexMetacarpal, XRHandJointID.MiddleMetacarpal, XRHandJointID.RingMetacarpal, XRHandJointID.LittleMetacarpal };

        [Header("Hand Fidelity Options")]
        /// <summary>
        /// Groups of <see cref="JointToTransformReference"/>.
        /// </summary>
        [Tooltip("Groups of Joint To Transform References. Use Context Menu for auto generation.")]
        public HandFidelityOption[] handFidelityOptions;

        /// <summary>
        /// Sets the Min/Max euler rotation of the fingers.
        /// </summary>
        [SerializeField, Tooltip("Sets the Min/Max euler rotation of the fingers.")]
        protected Vector2 m_MinMaxEulerX = new Vector2(0, 100);


        //3, 4, 5           -- Thumb
        //7, 8, 9, 10       -- Index
        //12, 13, 14, 15    -- Middle
        //17, 18, 19, 20    -- Ring
        //22, 23, 24, 25    -- Little

        /// <inheritdoc/>
        private void Update()
        {
            if (!m_UseCurl) return;

            m_FidelityLevel = Mathf.Clamp(m_FidelityLevel, 0, handFidelityOptions.Length);
            foreach (var joint in handFidelityOptions[m_FidelityLevel].fingerJoints)
            {
                foreach (var finger in joint.jointTransformReferences)
                {
                    Vector3 rot = Vector3.zero;
                    rot.x = Mathf.Lerp(m_MinMaxEulerX.x, m_MinMaxEulerX.y, joint.curlAmount);
                    finger.jointTransform.localRotation = Quaternion.Euler(rot);
                }
            }
        }

        /// <summary>
        /// Controls the Curl level of fingers.
        /// </summary>
        /// <remarks>Called from <see cref="XRHandPoseReplicator.GetNetworkCurl()"/>.</remarks>
        /// <param name="fingerID">ID of the specific finger.</param>
        /// <param name="curlAmount">Amount to curl the finger.</param>
        public void SetCurl(int fingerID, float curlAmount)
        {
            handFidelityOptions[m_FidelityLevel].fingerJoints[fingerID].curlAmount = curlAmount;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Clears the hand references.
        /// </summary>
        public void ClearHandReferences()
        {
            handFidelityOptions = new HandFidelityOption[3];
        }

        /// <summary>
        /// Attempts to find and automatically assign the hand references.
        /// </summary>
        [ContextMenu("Setup Hand References")]
        public void SetupHandReferences()
        {
            try
            {
                handFidelityOptions = new HandFidelityOption[3];

                for (int i = 0; i < handFidelityOptions.Length; i++)
                {
                    handFidelityOptions[i].fingerJoints = new FingerJoints[i <= 1 ? 5 : 3];

                    for (int j = 0; j < handFidelityOptions[i].fingerJoints.Length; j++)
                    {
                        handFidelityOptions[i].fingerJoints[j].fingerName = m_FingerNames[j];
                        handFidelityOptions[i].fingerJoints[j].jointTransformReferences = new List<JointToTransformReference>();

                        int jointDepth = i == 0 ? 4 : 3;
                        if(j == 0) jointDepth -= 1;  // Thumb has 1 less joint than the other fingers

                        int startDepth = i == 0 ? 0 : 1;

                        handFidelityOptions[i].fingerJoints[j].jointTransformReferences = GetFingerJoints(m_FingerNames[j], startDepth, jointDepth, m_FingerStartJointIds[j]);
                    }

                    //Get extra fingers as mittens
                    if(i == 2)
                    {
                        handFidelityOptions[i].fingerJoints[2].jointTransformReferences.AddRange(GetFingerJoints(m_FingerNames[3], 1, 3, m_FingerStartJointIds[3]));
                        handFidelityOptions[i].fingerJoints[2].jointTransformReferences.AddRange(GetFingerJoints(m_FingerNames[4], 1, 3, m_FingerStartJointIds[4]));
                    }
                }
            }
            catch (Exception e)
            {
                //Utils.LogError($"Error in FindNetworkHandReferences: {e}");
                Debug.LogError($"Error in FindNetworkHandReferences: {e}");
            }
        }

        List<JointToTransformReference> GetFingerJoints(string fingerName, int startDepth, int jointDepth, XRHandJointID fingerStartJointId)
        {
            try
            {
                List<JointToTransformReference> fingerJoints = new();
                JointToTransformReference currentJoint = new();

                foreach (Transform child in m_HandRoot)
                {
                    if (child.name.Contains(fingerName))
                    {
                        Transform currentChild = child;

                        //Navigate to the starting joint based on the startDepth
                        for(int i = 0; i < startDepth; i++)
                        {
                            currentChild = currentChild.GetChild(0);
                        }

                        // Get all joints in the finger and add them to the list based on the jointDepth
                        for (int i = 0; i < jointDepth; i++)
                        {
                            currentJoint.jointTransform = currentChild;
                            int currentHandJointId = (int)fingerStartJointId + i + startDepth;
                            currentJoint.xrHandJointID = (XRHandJointID)currentHandJointId;
                            fingerJoints.Add(currentJoint);
                            currentChild = currentChild.GetChild(0);
                        }
                    }
                }

                return fingerJoints;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in GetFingerJoints: {e}");
                return null;
            }
        }

#endif
    }

    [Serializable]
    public struct HandFidelityOption
    {
        public FingerJoints[] fingerJoints;
    }

    [Serializable]
    public struct FingerJoints
    {
        /// <summary>
        /// Finger Name.
        /// </summary>
        public string fingerName;

        /// <summary>
        /// The current curl amount of the finger.
        /// </summary>
        [Range(0.0f, 1.0f)]
        public float curlAmount;

        /// <summary>
        /// <see cref="JointToTransformReference"/> List.
        /// </summary>
        public List<JointToTransformReference> jointTransformReferences;
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(JointBasedHand))]
    public class HandCurlerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            JointBasedHand myScript = (JointBasedHand)target;
            if (GUILayout.Button("Setup References"))
            {
                myScript.SetupHandReferences();
            }

            if (GUILayout.Button("Clear Hand References"))
            {
                myScript.ClearHandReferences();
            }
        }
    }
#endif
}
