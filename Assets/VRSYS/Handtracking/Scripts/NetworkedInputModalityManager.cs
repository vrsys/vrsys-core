using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using VRSYS.Core.Logging;
using VRSYS.Core.Utility;
using VRSYS.HandTracking.XRMultiplayerScripts;
using UnityEditor;

namespace VRSYS.HandTracking
{
    /// <summary>
    /// Adapter for using XRHandPoseReplicator.cs
    /// </summary>
    public class NetworkedInputModalityManager : NetworkBehaviour
    {
        [Header("Tracked Hand Distribution")] 
        public XRHandPoseReplicator HandPoseReplicator;
        
        [Header("Root Component References")]
        public GameObject LeftController;
        public GameObject RightController;
        public GameObject LocalLeftTrackedHand;
        public GameObject LocalRightTrackedHand;
        public GameObject RemoteLeftTrackedHand;
        public GameObject RemoteRightTrackedHand;
        
        [Header("GO Disabled on Remote User")]
        // TODO: Adjust to account for handtracking based interactors, which possibly contain NetworkBehaviour in ActivationGroup
        public GameObject LocalHandTrackingGO;
        
        [Header("Roots of Visuals Disabled on Local User")]
        public GameObject leftJointBasedHand;
        public GameObject rightJointBasedHand;

        [Header("Controller Geometry for Mode Switch on Remote User")]
        public GameObject LeftControllerGeometry;
        public GameObject RightControllerGeometry;
        
        private Transform _localLeftHandTransform;
        private Transform _localRightHandTransform;
        private Transform _remoteLeftHandTransform;
        private Transform _remoteRightHandTransform;

        private List<NetworkComponentActivationGroup> _motionControllerNetworkedInteractorGroups = new ();
        // TODO: Implement for hands
        //private List<NetworkComponentActivationGroup> _trackedHandNetworkedInteractorGroups = new ();

        private XRInputModalityManager _modalityManager;

        private NetworkVariable<XRInputModalityManager.InputMode> inputMode = new (XRInputModalityManager.InputMode.None,
                NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // For remote visibility handling
        private SkinnedMeshRenderer leftHandRenderer;
        private SkinnedMeshRenderer rightHandRenderer;
        
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _localLeftHandTransform = LocalLeftTrackedHand.GetComponentInChildren<XRHandSkeletonDriver>().rootTransform;
            _localRightHandTransform = LocalRightTrackedHand.transform.GetComponentInChildren<XRHandSkeletonDriver>().rootTransform;
            _remoteLeftHandTransform = RemoteLeftTrackedHand.transform.GetComponentInChildren<JointBasedHand>().RootTransform;
            _remoteRightHandTransform = RemoteRightTrackedHand.transform.GetComponentInChildren<JointBasedHand>().RootTransform;

            _motionControllerNetworkedInteractorGroups.AddRange(LeftController.GetComponentsInChildren<NetworkComponentActivationGroup>());
            _motionControllerNetworkedInteractorGroups.AddRange(RightController.GetComponentsInChildren<NetworkComponentActivationGroup>());
            Debug.Log($"# interactor activation groups {_motionControllerNetworkedInteractorGroups.Count}");
            
            // Same for hands
            
            if (IsOwner) // local user
            {
                if (!RemoteLeftTrackedHand.transform.parent.GetComponent<XRHandPoseReplicator>().UpdateHandsLocally)
                {
                    // TODO: Refactor to distribute position via unified hand position gameobjects
                    // that are updated with the position from MotionController/TrackedHand according to inputType
                    
                    // rootTransform distributes Position via NetworkTransform, so we can't disable the parent
                    // <left/right>JointBasedHand gameobject
                    leftJointBasedHand.GetComponent<JointBasedHand>().enabled = false;
                    rightJointBasedHand.GetComponent<JointBasedHand>().enabled = false;
                    leftJointBasedHand.GetComponentInChildren<SkinnedMeshRenderer>().enabled = false;
                    rightJointBasedHand.GetComponentInChildren<SkinnedMeshRenderer>().enabled = false;
                }
                
                _modalityManager = GetComponent<XRInputModalityManager>();
                
                // Keep disabled on compile time and enable from here, because otherwise controllers are disabled
                // in OnEnabled, before component can be destroyed as localBehaviour by NetworkUser on remote
                _modalityManager.enabled = true;
                
                UpdateInputModalityMode(XRInputModalityManager.currentInputMode.Value);
                XRInputModalityManager.currentInputMode.Subscribe(UpdateInputModalityMode);
            }
            else // remote user
            {
                leftHandRenderer = leftJointBasedHand.GetComponentInChildren<SkinnedMeshRenderer>();
                rightHandRenderer = rightJointBasedHand.GetComponentInChildren<SkinnedMeshRenderer>();
                
                LocalHandTrackingGO.SetActive(false);
            }
            // everyone
            UpdateComponents(inputMode.Value, inputMode.Value);
            inputMode.OnValueChanged += UpdateComponents;
        }
        
        /// <summary>
        /// Applies tracked position and rotation from local tracked hand to remote hand representation.
        /// </summary>
        protected virtual void LateUpdate()
        {
            if (IsOwner && inputMode.Value == XRInputModalityManager.InputMode.TrackedHand)
            {
                // Set transforms to be replicated with ClientNetworkTransforms
                _remoteLeftHandTransform.SetPositionAndRotation(_localLeftHandTransform.position, _localLeftHandTransform.rotation);
                _remoteRightHandTransform.SetPositionAndRotation(_localRightHandTransform.position, _localRightHandTransform.rotation);
            }
        }
        
        private void UpdateInputModalityMode(XRInputModalityManager.InputMode mode)
        {
            // TODO: Add logic to enable/disable interactors safely??
            inputMode.Value = mode;
            switch (mode)
            {
                case XRInputModalityManager.InputMode.None:
                    HandPoseReplicator.enabled = false;
                    break;
                case XRInputModalityManager.InputMode.MotionController:
                    HandPoseReplicator.enabled = false;
                    break;
                case XRInputModalityManager.InputMode.TrackedHand:
                    HandPoseReplicator.enabled = true;
                    break;
            }
        }
        
        private void UpdateComponents(XRInputModalityManager.InputMode _, XRInputModalityManager.InputMode newMode)
        {
            Debug.Log("Updating Components on Input Mode changed");
            switch (newMode)
            {
                case XRInputModalityManager.InputMode.None:
                    SetControllerContentsEnabled(false);
                    SetTrackedHandContentsEnabled(false);
                    break;
                case XRInputModalityManager.InputMode.MotionController:
                    SetControllerContentsEnabled(true);
                    SetTrackedHandContentsEnabled(false);
                    break;
                case XRInputModalityManager.InputMode.TrackedHand:
                    SetControllerContentsEnabled(false);
                    SetTrackedHandContentsEnabled(true);
                    break;
            }
        }

        private void SetControllerContentsEnabled(bool state)
        {
            if (state)
            {
                Debug.Log("Enabling controller contents");
            }
            else
            {
                Debug.Log("Disabling controller contents");
            }
            LeftControllerGeometry.SetActive(state);
            RightControllerGeometry.SetActive(state);
            foreach (NetworkComponentActivationGroup group in _motionControllerNetworkedInteractorGroups)
            {
                group.InputModeActive(state);
            }
        }

        private void SetTrackedHandContentsEnabled(bool state)
        {
            if (!IsOwner)
            {
                leftHandRenderer.enabled = state;
                rightHandRenderer.enabled = state;    
            }
            else
            {
                // On Owner, XRInteractionManager already disables Tracked Hand Left/Right GameObject
            }
            
            // TODO: Extend to handtracking based interactors
            // foreach (NetworkComponentActivationGroup group in _trackedHandNetworkedInteractorGroups)
            // {
            //     group.InputModeActive(state);
            // }
        }
        //
        // public void SetUpFromScene()
        // { 
        //     XRHandPoseReplicator HandPoseReplicator = transform.parent.GameObject.Find("Tracked Hands - Remote").GetComponent<XRHandPoseReplicator>();
        //     GameObject LeftController = GameObject.Find("LeftHand Controller - Networked");
        //     GameObject RightController = GameObject.Find("RightHand Controller - Networked");
        //     
        //     GameObject LocalHandTrackingGO = GameObject.Find("Tracked Hands - Local");
        //     
        //     GameObject LocalLeftTrackedHand = GameObject.Find("");
        //     GameObject LocalRightTrackedHand = GameObject.Find("");
        //     
        //     GameObject RemoteLeftTrackedHand = GameObject.Find("");
        //     GameObject RemoteRightTrackedHand = GameObject.Find("");
        //     
        //     GameObject leftJointBasedHand = GameObject.Find("");
        //     GameObject rightJointBasedHand = GameObject.Find("");
        //     
        //     GameObject LeftControllerGeometry = GameObject.Find("");
        //     GameObject RightControllerGeometry = GameObject.Find("");
        // }
    }
//     
// #if (UNITY_EDITOR)
//     [CustomEditor(typeof(NetworkedInputModalityManager))]
//     public class NetworkedInputModalityManagerEditor : Editor 
//     {
//         public override void OnInspectorGUI()
//         {
//             // Draw the default Inspector
//             DrawDefaultInspector();
//
//             // Reference to the target object being inspected
//             NetworkedInputModalityManager group = (NetworkedInputModalityManager)target;
//
//             // Add a button to the inspector
//             if (GUILayout.Button("Attempt automatic setup by name"))
//             {
//                 // Call a method on the target script to perform the setup
//                 group.SetUpFromScene();
//             
//                 // Mark the object as dirty so changes are saved
//                 EditorUtility.SetDirty(group);
//             }
//         }
//     }
// #endif
}