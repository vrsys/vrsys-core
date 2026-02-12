using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using VRSYS.Core.Logging;


namespace VRSYS.Core.Networking
{
    public class TrackedHandSerializer : MonoBehaviour
    {
        #region Enums

        private enum FidelityLevel
        {
            JointRotations,
            FingerCurls
        }

        #endregion

        #region Structs

        
        [Serializable]
        public struct FingerJoints
        {
            /// <summary>
            /// Finger name
            /// </summary>
            public string fingerName;

            /// <summary>
            /// The current curl amount of the finger
            /// </summary>
            [Range(0.0f, 1.0f)] public float curlAmount;

            /// <summary>
            /// <see cref="JointToTransformReference"/> List
            /// </summary>
            public List<JointToTransformReference> jointTransformReferences;
        }

        [Serializable]
        public struct HandFidelityOption
        {
            public FingerJoints[] fingerJoints;
        }

        #endregion

        #region Properties
        
        [SerializeField] private FidelityLevel _fidelityLevel;

        [SerializeField, Tooltip("Specifies where the root of the hand is.")] 
        private Transform _handRoot;
        
        [SerializeField, Tooltip("Defines how fast the fingers curl.")] 
        private float _curlSpeed = 12.0f;

        [SerializeField, Tooltip("Specifies the names of the fingers.")]
        private string[] _fingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };

        [SerializeField, Tooltip("Specifies the start index of the finger joints.")]
        private XRHandJointID[] _fingerStartJointIds =
        {
            XRHandJointID.ThumbMetacarpal, XRHandJointID.IndexMetacarpal, XRHandJointID.MiddleMetacarpal,
            XRHandJointID.RingMetacarpal, XRHandJointID.LittleMetacarpal
        };

        [SerializeField, Tooltip("Groups of Joint To Transform References.")]
        public HandFidelityOption[] _handFidelityOptions;

        [SerializeField, Tooltip("Sets the Min/Max eule rotation of the fingers.")]
        private Vector2 _minMaxEulerX = new Vector2(0, 100);

        #endregion

        #region MonoBehaviour Methods

        private void Awake()
        {
            if (_handFidelityOptions == null)
                SetupHandFidelityOptions();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// attempts to find and automatically assign the hand references.
        /// </summary>
        [ContextMenu("Setup Hand Fidelity Options")]
        public void SetupHandFidelityOptions()
        {
            try
            {
                _handFidelityOptions = new HandFidelityOption[2];

                for (int i = 0; i < _handFidelityOptions.Length; i++) // looping through the fidelity levels
                {
                    _handFidelityOptions[i].fingerJoints = new FingerJoints[5];

                    for (int j = 0; j < _handFidelityOptions[i].fingerJoints.Length; j++) // looping through each finger of the current fidelity level
                    {
                        // setting name for finger and transform references for all joint of the current finger 
                        _handFidelityOptions[i].fingerJoints[j].fingerName = _fingerNames[j];
                        _handFidelityOptions[i].fingerJoints[j].jointTransformReferences =
                            new List<JointToTransformReference>();

                        int jointDepth = i == 0 ? 4 : 3; // fidelity level rotations considers all 4 joints, fidelity level curls only 3
                        if (j == 0)
                            jointDepth -= 1; // Thumb has 1 joint less

                        int startDepth = i == 0 ? 0 : 1; // on fidelity level curl metacarpal joints are ignored

                        _handFidelityOptions[i].fingerJoints[j].jointTransformReferences =
                            GetFingerJoints(_fingerNames[j], startDepth, jointDepth, _fingerStartJointIds[j]);

                    }
                }
            }
            catch (Exception e)
            {
                ExtendedLogger.LogError(GetType().Name, $"Error in SetupHandFidelityOptions: {e}", this);
            }
        }

        #endregion
    }
}
