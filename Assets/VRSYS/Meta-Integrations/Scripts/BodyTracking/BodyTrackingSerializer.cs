using System.Collections.Generic;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using static Meta.XR.Movement.MSDKUtility;         // NativeTransform, Serialize/Deserialize helpers
using Meta.XR.Movement;                            // high-level Movement types
using Meta.XR.Movement.Networking; // Movement SDK networking helpers
using Meta.XR.Movement.Retargeting;  // JointType definition
using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;
using UnityEngine.Assertions;
using System.Linq;

namespace VRSYS.Meta.Bodytracking
{
    public class BodyTrackingSerializer : NetworkBehaviour
    {
        #region Inspector ------------------------------------------------------

        [Tooltip("Behaviour(s) that should run ONLY on the local player – " +
                 "e.g. any *hardware* tracking providers.  They will be " +
                 "destroyed on remote avatars so those avatars cannot read " +
                 "the local XR devices.")]
        [SerializeField] private List<Behaviour> localOnlyBehaviours = new();

        [Tooltip("NetworkCharacterRetargeter used to *read* the local pose " +
                 "and *apply* remote poses.  Auto-assigned if left blank.")]
        [SerializeField] private NetworkCharacterRetargeter retargeter;

        [Tooltip("Packets per second the owner sends.  Lower = less bandwidth, " +
                 "higher = smoother animation.")]
        [Range(10, 120)]
        [SerializeField] private int sendRate = 60;

        #endregion

        #region Internal buffers ----------------------------------------------

        private float _nextSend;
        private NativeArray<byte> _outgoing;              // reused serialised data
        private NativeArray<NativeTransform> _bodyBuf;    // remote playback buffer
        private NativeArray<float> _faceBuf;    // remote playback buffer

        #endregion

        #region Unity lifecycle -----------------------------------------------

        private void Awake()
        {
            // Grab the retargeter if the user forgot to assign it.
            if (retargeter == null)
                retargeter = GetComponentInChildren<NetworkCharacterRetargeter>(true);
        }

        public override void OnNetworkSpawn()
        {
            // ─── Remote-only setup ──────────────────────────────────────────
            if (!IsOwner)
            {
                // Remove *hardware* input so the remote avatar does NOT try
                // to read controllers / hand tracking on this client.
                foreach (var b in localOnlyBehaviours.Where(b => b != null))
                    Destroy(b);
            }

            // ─── Shared setup ───────────────────────────────────────────────
            // Allocate playback buffers (one copy per avatar instance)
            _bodyBuf = new NativeArray<NativeTransform>(
                retargeter.NumberOfJoints, Allocator.Persistent);
            _faceBuf = new NativeArray<float>(
                retargeter.NumberOfShapes, Allocator.Persistent);
        }

        private void Update()
        {
            if (!IsOwner) return;                        // remotes only play back

            SerializePose(out var bytes);

        }

        // NOTE: We do **NOT** dispose _bodyBuf / _faceBuf here!
        //       The retargeter takes ownership (it disposes the arrays in its
        //       own OnDestroy, after all jobs have completed).  Disposing them
        //       twice caused your ObjectDisposedException.
        private void OnDestroy()
        {
            if (_outgoing.IsCreated) _outgoing.Dispose();
        }

        #endregion

        #region Serialise / deserialise helpers -------------------------------

        private void SerializePose(out byte[] managedBytes)
        {
            using var body = retargeter.GetCurrentBodyPose(JointType.NoWorldSpace);
            var face = retargeter.GetCurrentFacePose();

            SerializeSkeletonAndFace(
                retargeter.RetargetingHandle,
                Time.time,
                body,
                face,
                /*lastAck*/ -1,
                retargeter.BodyIndicesToSync,
                retargeter.FaceIndicesToSync,
                ref _outgoing);

            managedBytes = _outgoing.ToArray();
            Debug.Log($"###Sharks >> Serialized Data: {string.Join(", ", managedBytes)}");
            SendPoseServerRpc(managedBytes);                    // Owner → Server
        }

        private void ApplyPose(byte[] data)
        {
            using var packet = new NativeArray<byte>(data, Allocator.Temp);

            if (!DeserializeSkeletonAndFace(
                    retargeter.RetargetingHandle,
                    packet,
                    out _,
                    out _,
                    out _,
                    ref _bodyBuf,
                    ref _faceBuf))
                return; // bad / truncated packet

            retargeter.ApplyBodyPose(
                _bodyBuf, JointType.NoWorldSpace);
            retargeter.ApplyFacePose(_faceBuf);
        }

        #endregion

        #region Netcode RPCs ---------------------------------------------------

        /// <summary>Runs on the **owner** and forwards the pose to the server.</summary>
        [ServerRpc(RequireOwnership = true)]
        private void SendPoseServerRpc(byte[] poseData, ServerRpcParams _ = default)
        {
            // Tell the server to broadcast to *everyone EXCEPT the sender*.
            var others = NetworkManager.Singleton.ConnectedClientsIds
                         .Where(id => id != OwnerClientId).ToArray();

            if (others.Length == 0) return; // solo host mode

            var p = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = others }
            };
            BroadcastPoseClientRpc(poseData, p);
        }

        /// <summary>
        /// Runs on every client that received the packet (never the sender),
        /// feeds the data straight into the retargeter.
        /// </summary>
        [ClientRpc]
        private void BroadcastPoseClientRpc(
            byte[] poseData, ClientRpcParams _ = default)
        {
            if (IsOwner) return;           // safety; should never be called

            ApplyPose(poseData);
        }

        #endregion
    }
}
