using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR;
using VRSYS.Core.Networking;
using UnityEngine.InputSystem;

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
            InitializeXRInputSubsystem();
            AlignScene();
        }

        protected override void EndState()
        {
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running,
                "Removing callback to TrackingOriginUpdates and resetting scene alignment."));
            _xrInputSubsystem.trackingOriginUpdated -= OnTrackingOriginUpdated;
            // TODO: Is it desired to reset scene alignment here?
            // ResetTrackingOrigin();
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
            
            // origin.position = a.transform.position;
            // origin.LookAt(b.transform.position, Vector3.up);
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