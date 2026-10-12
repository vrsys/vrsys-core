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
//   Authors:        Tony Zoeppig, Karoline Brehm
//   Date:           2025
//-----------------------------------------------------------------

using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR;
using VRSYS.Core.Networking;

namespace VRSYS.Meta.Collocation
{
    public class AligningToAnchorStateHandler : CollocationStateHandler
    {
        private XRInputSubsystem _xrInputSubsystem;
        
        public AligningToAnchorStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.AligningToAnchor;
        }

        public override void StartState()
        {
            _manager.OnRestart.AddListener(EndState);
            
            InitializeXRInputSubsystem();
            AlignScene();
        }

        /// <summary>
        /// e.g. call when user wants to go back to anchor creation / loading ...
        /// </summary>
        protected override void EndState()
        {
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running,
                "Removing callback to TrackingOriginUpdates and resetting scene alignment."));
            _xrInputSubsystem.trackingOriginUpdated -= OnTrackingOriginUpdated;
            ResetTrackingOrigin();
            // TODO: Anchor unloading?
        }
        
        private void InitializeXRInputSubsystem()
        {
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Started,
                "Setting up callback to TrackingOriginUpdates."));
            
            List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems); // Returns active Subsystems of XRInputSubsystem type
            if (subsystems.Count > 0)
            {
                _xrInputSubsystem = subsystems[0];
                _xrInputSubsystem.trackingOriginUpdated += OnTrackingOriginUpdated;
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running,
                    "Subscribed to TrackingOriginUpdates."));
            }
            else
            {
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Failed,
                    "No XR Subsystem found. Cannot subscribe to TrackingOriginUpdates."));
            }
        }

        /// <summary>
        /// Aligns scene to the current anchor held by Colocation Manager
        /// </summary>
        public void AlignScene()
        {
            var trackingOrigin = NetworkUser.LocalInstance.transform; // XR Origin
            var alignmentAnchor = _manager.CurrentAnchor;
            
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running,
                $"Aligning tracking space with current position {trackingOrigin.position} to current anchor " 
                + $"with position {alignmentAnchor.transform.position}"));

            // The navigation node (parent of the tracking origin) determines the target world pose for the anchor
            var targetPos = trackingOrigin.parent != null ? trackingOrigin.parent.position : Vector3.zero;
            Debug.Log($"[Collocation] Aliging anchor to target {targetPos}");
            Matrix4x4 targetWorldMatrix = trackingOrigin.parent != null ? trackingOrigin.parent.localToWorldMatrix : Matrix4x4.identity;
            Matrix4x4 alignmentMatrix = ComputeAlignmentMatrix(alignmentAnchor.transform, trackingOrigin, targetWorldMatrix);
            
            trackingOrigin.localPosition = alignmentMatrix.GetColumn(3);
            trackingOrigin.localRotation = alignmentMatrix.rotation;
            
            Debug.Log($"[Recording] Tracking space origin now in: {NetworkUser.LocalInstance.transform.position}, {alignmentAnchor.transform.position}");
            
            _manager.SetIsSuccessfullyCollocated(true);
            
            _manager.BroadcastState(new CollocationStateMessage(CollocationState.AligningToAnchor, CollocationStateStatus.Success,
                "Colocated."));
        }

        private Matrix4x4 ComputeAlignmentMatrix(Transform anchor, Transform origin, Matrix4x4 targetWorldMatrix)
        {
            // 1. Calculate the anchor's pose relative to the tracking origin (Tracking Space Pose)
            // This represents where the anchor is according to the SLAM system.
            Matrix4x4 anchorInTrackingSpace = origin.worldToLocalMatrix * anchor.localToWorldMatrix;

            // 2. Extract the yaw-only component from the tracking space pose
            Vector3 relativePosition = anchorInTrackingSpace.GetColumn(3);
            Vector3 forward = anchorInTrackingSpace.MultiplyVector(Vector3.forward);
            forward.y = 0;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = anchorInTrackingSpace.MultiplyVector(Vector3.up);
                forward.y = 0;
            }
            forward.Normalize();
            Quaternion yawOnlyRotation = Quaternion.LookRotation(forward, Vector3.up);

            // 3. Construct the cleaned tracking space matrix
            Matrix4x4 aTracking = Matrix4x4.TRS(relativePosition, yawOnlyRotation, Vector3.one);

            // 4. Calculate the new tracking origin pose.
            // Goal: find T_world such that T_world * A_tracking = Target_world
            // T_world = Target_world * A_tracking^-1
            Matrix4x4 targetTrackingOriginWorldMatrix = targetWorldMatrix * aTracking.inverse;

            // 5. Convert the world pose back to the tracking origin's parent's local space
            if (origin.parent != null)
            {
                return origin.parent.worldToLocalMatrix * targetTrackingOriginWorldMatrix;
            }

            return targetTrackingOriginWorldMatrix;
        }
        
        private void OnTrackingOriginUpdated(XRInputSubsystem obj)
        {
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Started,
                $"TrackingOriginUpdated Event received. origin {NetworkUser.LocalInstance.transform.position}, anchor {_manager.CurrentAnchor.transform.position}"));
            _manager.StartCoroutine(ResetAlignmentNextFrame());
        }
        
        IEnumerator ResetAlignmentNextFrame()
        {
            Debug.Log("Resetting scene alignment...");
            ResetTrackingOrigin();
            
            // Wait for one frame, because after resetting the new tracked anchor position is not yet available
            // TODO: Is it possible to fetch the new anchor position without waiting?
            yield return null;  
            
            Debug.Log("Aliging scene to spatial anchor...");
            AlignScene();
        }
        
        public void ResetTrackingOrigin()
        {
            Debug.Log($"Before reset: origin {NetworkUser.LocalInstance.transform.position}, anchor {_manager.CurrentAnchor.transform.position}");
            var origin = NetworkUser.LocalInstance.transform;
            origin.localPosition = Vector3.zero;
            origin.localRotation = Quaternion.identity;
            origin.localScale = Vector3.one;
            Debug.Log($"After reset: origin {NetworkUser.LocalInstance.transform.position}, anchor {_manager.CurrentAnchor.transform.position}");
        }
    }
}