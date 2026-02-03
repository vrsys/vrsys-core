using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.XR;
using 

namespace VRSYS.Meta.Collocation
{
    public class AligningToAnchorStateHandler : CollocationStateHandler
    {
        public AligningToAnchorStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.AligningToAnchor;
        }

        public override void StartState()
        {
            throw new System.NotImplementedException();
        }

        protected override void EndState()
        {
            throw new System.NotImplementedException();
        }
        
        private XRInputSubsystem _xrInputSubsystem;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems); //Returns active Subsystems of XRInputSubsystem type
            if (subsystems.Count > 0)
            {
                _xrInputSubsystem = subsystems[0];
                _xrInputSubsystem.trackingOriginUpdated += OnTrackingOriginUpdated;
            }
        }

        private void OnTrackingOriginUpdated(XRInputSubsystem obj)
        {
            Debug.Log("Tracking Origin Updated");
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
            var origin = FindAnyObjectByType<XROrigin>().transform;
            origin.position = Vector3.zero;
            origin.rotation = Quaternion.identity;
        }

        /// <summary>
        /// Aligns scene to current tracked anchor position
        /// </summary>
        public void AlignScene()
        {
            Debug.Log("Align Scene");
            if (!AlignmentAnchor.IsAlignmentValid())
            {
                Debug.Log("No alignment nodes specified");
                return;
            }
            var result = AlignmentAnchor.GetAlignmentAnchors();
            if (result is (ARAnchor a, ARAnchor b))
            {
                var origin = FindAnyObjectByType<XROrigin>().transform;
                Debug.Log($"Anchor Pos {a.transform.position} Origin Position {origin.position}");

                Matrix4x4 O = Matrix4x4.TRS(
                    origin.position,
                    origin.rotation,
                    Vector3.one
                );

                Matrix4x4 A = Matrix4x4.TRS(
                    a.transform.position,
                    Quaternion.LookRotation(b.transform.position - a.transform.position),
                    Vector3.one);

                var alignmentMatrix = O * A.inverse;

                origin.position = alignmentMatrix.GetColumn(3);
                origin.rotation = alignmentMatrix.rotation;
                origin.localScale = alignmentMatrix.lossyScale;

                // origin.position = a.transform.position;
                // origin.LookAt(b.transform.position, Vector3.up);
            }
        }
    }
}