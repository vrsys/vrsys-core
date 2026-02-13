using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Hands;
using VRSYS.Core.Logging;


namespace VRSYS.Core.Networking
{
    public class TrackedHandSerializer : NetworkBehaviour
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
        
        [Header("Fidelity Level Settings")]
        [SerializeField] private FidelityLevel _fidelityLevel;

        [Header("Hand Components")]
        [SerializeField, Tooltip("Specifies where the root of the hand is.")] 
        private Transform _handRoot;

        [SerializeField, Tooltip("Renderer used to visualize tracked hand.")]
        private SkinnedMeshRenderer _handRenderer;

        [Header("Update Configurations")]
        [SerializeField, Tooltip("Defines how fast the finger rotate.")]
        private float _fingerLerpSpeed = 20.0f;
        
        [SerializeField, Tooltip("Defines how fast the fingers curl.")] 
        private float _curlSpeed = 12.0f;
        
        [SerializeField] float _minCurlUpdateDelta = 0.1f;

        [Header("Hand Configurations")]
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

        [Header("Others")]
        [SerializeField, Tooltip("Components that get destroyed on remote users.")]
        private List<Behaviour> _localBehaviours;

        #endregion

        #region Networked Properties

        private NetworkVariable<bool> _initialized = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<Vector3> _rootPosition = new(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<Quaternion> _rootRotation = new(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkList<Vector3> _fingerRotations = new (new List<Vector3>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkList<float> _fingerCurls = new (new List<float>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        #endregion

        #region MonoBehaviour Methods

        private void Awake()
        {
            if (_handFidelityOptions == null)
                SetupHandFidelityOptions();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                InitializeNetworkProperties();
            }
            else
            {
                SetupRemoteUser();
            }
        }

        private void Update()
        {
            if(!_initialized.Value)
                return;
            
            SyncRootNode();
            
            switch (_fidelityLevel)
            {
                case FidelityLevel.JointRotations:
                    SyncFingerRotations();
                    break;
                case FidelityLevel.FingerCurls:
                    SyncFingerCurls();
                    break;
            }
            
            if(!IsOwner)
                if (_fidelityLevel == FidelityLevel.FingerCurls)
                    ApplyFingerCurl();
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

        [ContextMenu("Clear Hand Fidelity Options")]
        public void ClearHandFidelityOptions() => _handFidelityOptions = new HandFidelityOption[2];

        #endregion

        #region Private Methods

        private List<JointToTransformReference> GetFingerJoints(string fingerName, int startDepth, int jointDepth,
            XRHandJointID fingerStartJointId)
        {
            try
            {
                List<JointToTransformReference> fingerJoints = new();
                JointToTransformReference currentJoint = new();

                foreach (Transform child in _handRoot)
                {
                    if (child.name.Contains(fingerName))
                    {
                        Transform currentChild = child;

                        for (int i = 0; i < startDepth; i++)
                            currentChild = currentChild.GetChild(0);

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
                ExtendedLogger.LogError(GetType().Name, $"Error in GetFingerJoints: {e}", this);
                return null;
            }
        }
        
        private void SetFingerCurl(int fingerIdx, float curlAmount)
        {
            _handFidelityOptions[(int)_fidelityLevel].fingerJoints[fingerIdx].curlAmount = curlAmount;
        }

        private void ApplyFingerCurl()
        {
            foreach (var joint in _handFidelityOptions[1].fingerJoints)
            {
                foreach (var finger in joint.jointTransformReferences)
                {
                    Vector3 rot = Vector3.zero;
                    rot.x = Mathf.Lerp(_minMaxEulerX.x, _minMaxEulerX.y, joint.curlAmount);
                    finger.jointTransform.localRotation = Quaternion.Euler(rot);
                    finger.jointTransform.localRotation = Quaternion.Slerp(finger.jointTransform.localRotation, Quaternion.Euler(rot), _curlSpeed * Time.deltaTime);
                }
            }
        }

        private void SyncRootNode()
        {
            if (IsOwner)
                WriteRootNodeValues();
            else
            {
                ReadRootNodeValues();
            }
        }

        private void WriteRootNodeValues()
        {
            _rootPosition.Value = _handRoot.position;
            _rootRotation.Value = _handRoot.rotation;
        }

        private void ReadRootNodeValues()
        {
            _handRoot.SetPositionAndRotation(_rootPosition.Value, _rootRotation.Value);
        }

        private void SyncFingerRotations()
        {
            if (IsOwner)
                WriteNetworkFingerRotations();
            else
            {
                ReadNetworkFingerRotations();
            }
        }

        private void WriteNetworkFingerRotations()
        {
            int currentIdx = 0;

            for (int i = 0; i < _handFidelityOptions[0].fingerJoints.Length; i++)
            {
                for (int j = 0; j < _handFidelityOptions[0].fingerJoints[i].jointTransformReferences.Count; j++)
                {
                    _fingerRotations[currentIdx] = _handFidelityOptions[0].fingerJoints[i]
                        .jointTransformReferences[j].jointTransform.eulerAngles;

                    currentIdx++;
                }
            }
        }

        private void ReadNetworkFingerRotations()
        {
            int currentIdx = 0;

            for (int i = 0; i < _handFidelityOptions[0].fingerJoints.Length; i++)
            {
                for (int j = 0; j < _handFidelityOptions[0].fingerJoints[i].jointTransformReferences.Count; j++)
                {
                    _handFidelityOptions[0].fingerJoints[i].jointTransformReferences[j].jointTransform.rotation =
                        Quaternion.Slerp(
                            _handFidelityOptions[0].fingerJoints[i].jointTransformReferences[j].jointTransform.rotation,
                            Quaternion.Euler(_fingerRotations[currentIdx]), Time.deltaTime * _fingerLerpSpeed);
                    currentIdx++;
                }
            }
        }

        private void SyncFingerCurls()
        {
            if (IsOwner)
                WriteNetworkFingerCurls();
            else
            {
                ReadNetworkFingerCurls();
            }
        }

        private void WriteNetworkFingerCurls()
        {
            for(int i = 0; i < _handFidelityOptions[1].fingerJoints.Length; i++)
                WriteNetworkFingerCurl(i, GetAverageX(i));
        }

        private void ReadNetworkFingerCurls()
        {
            for(int i = 0; i < _handFidelityOptions[1].fingerJoints.Length; i++)
                SetFingerCurl(i, _fingerCurls[i]);
        }

        private float GetAverageX(int finger)
        {
            float x = 0;
            float digitCount = _handFidelityOptions[1].fingerJoints[finger].jointTransformReferences.Count;

            for (int i = 0; i < digitCount; i++)
            {
                float currentX = _handFidelityOptions[1].fingerJoints[finger].jointTransformReferences[i].jointTransform
                    .localEulerAngles.x;

                if (currentX < 0 || currentX > 180)
                    currentX = 0;

                x += currentX;
            }

            float average = Mathf.Clamp(x / digitCount, 0, 100);

            return average / 100;
        }
        
        private void WriteNetworkFingerCurl(int finger, float value)
        {
            if (Mathf.Abs(_fingerCurls[finger] - value) > _minCurlUpdateDelta)
                _fingerCurls[finger] = value;
        }

        private void InitializeNetworkProperties()
        {
            _rootPosition.Value = _handRoot.position;
            _rootRotation.Value = _handRoot.rotation;
            
            foreach (var finger in _handFidelityOptions[0].fingerJoints)
            {
                foreach (var joint in finger.jointTransformReferences)
                {
                    _fingerRotations.Add(joint.jointTransform.eulerAngles);
                }
            }

            foreach (var finger in _handFidelityOptions[1].fingerJoints)
            {
                _fingerCurls.Add(0.0f);
            }

            _initialized.Value = true;
        }

        private void SetupRemoteUser()
        {
            // Delete components, only required on local user
            while (_localBehaviours.Count > 0)
            {
                DestroyImmediate(_localBehaviours[0]);
                _localBehaviours.RemoveAt(0);
            }

            // Set renderer active by default
            _handRenderer.enabled = true;
        }

        #endregion
    }
}
