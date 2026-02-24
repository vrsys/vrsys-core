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
            
            Matrix4x4 O = Matrix4x4.TRS(
                trackingOrigin.position,
                trackingOrigin.rotation,
                Vector3.one
            );

            Matrix4x4 A = Matrix4x4.TRS(
                alignmentAnchor.transform.position,
                Quaternion.LookRotation(alignmentAnchor.transform.forward),
                Vector3.one);

            var alignmentMatrix = O * A.inverse;

            trackingOrigin.position = alignmentMatrix.GetColumn(3);
            trackingOrigin.rotation = alignmentMatrix.rotation;
            trackingOrigin.localScale = alignmentMatrix.lossyScale;
            
            Debug.Log($"Tracking origin now in: {NetworkUser.LocalInstance.transform.position}, anchor in {alignmentAnchor.transform.position}");
            
            _manager.SetIsSuccessfullyCollocated(true);
            
            _manager.BroadcastState(new CollocationStateMessage(CollocationState.AligningToAnchor, CollocationStateStatus.Success,
                "Colocated."));
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
            origin.position = Vector3.zero;
            origin.rotation = Quaternion.identity;
            origin.localScale = Vector3.one;
            Debug.Log($"After reset: origin {NetworkUser.LocalInstance.transform.position}, anchor {_manager.CurrentAnchor.transform.position}");
        }
    }
}