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
            InitializeXRInputSubsystem();
            AlignScene();
        }

        protected override void EndState()
        {
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running,
                "Removing callback to TrackingOriginUpdates and resetting scene alignment."));
            _xrInputSubsystem.trackingOriginUpdated -= OnTrackingOriginUpdated;
            // TODO: Is it desired to reset scene alignment here?
            ResetSceneAlignment();
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
            var origin = NetworkUser.LocalInstance.transform; // XR Origin
            var alignmentAnchor = _manager.CurrentAnchor;
            
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running,
                $"Aligning scene with origin {origin.position} to current anchor " 
                + $"with position {alignmentAnchor.transform.position}"));
            
            Matrix4x4 O = Matrix4x4.TRS(
                origin.position,
                origin.rotation,
                Vector3.one
            );

            Matrix4x4 A = Matrix4x4.TRS(
                alignmentAnchor.transform.position,
                Quaternion.LookRotation(alignmentAnchor.transform.forward),
                Vector3.one);

            var alignmentMatrix = O * A.inverse;

            origin.position = alignmentMatrix.GetColumn(3);
            origin.rotation = alignmentMatrix.rotation;
            origin.localScale = alignmentMatrix.lossyScale;

            // origin.position = a.transform.position;
            // origin.LookAt(b.transform.position, Vector3.up);
        }
        
        private void OnTrackingOriginUpdated(XRInputSubsystem obj)
        {
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Started,
                $"TrackingOriginUpdated. Realigning scene..."));
            _manager.StartCoroutine(ResetAlignmentNextFrame());
        }
        
        IEnumerator ResetAlignmentNextFrame()
        {
            yield return null;
            ResetSceneAlignment();
            AlignScene();
        }
        
        public void ResetSceneAlignment()
        {
            var origin = NetworkUser.LocalInstance.transform;
            origin.position = Vector3.zero;
            origin.rotation = Quaternion.identity;
        }
    }
}