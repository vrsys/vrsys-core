using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using VRSYS.Core.Logging;
using VRSYS.Meta.Avatars;

namespace VRSYS.Scripts.Recording
{
    [RequireComponent(typeof(ReRecorder))]
    public class RerecorderMetaAvatarLinker : MonoBehaviour
    {
        public bool verbose;

        private readonly Dictionary<MetaAvatarReplayDataWriter, MetaAvatarReplayDataWriter> _playbackToRealUser =
            new Dictionary<MetaAvatarReplayDataWriter, MetaAvatarReplayDataWriter>();

        public IReadOnlyDictionary<MetaAvatarReplayDataWriter, MetaAvatarReplayDataWriter> PlaybackToRealUser =>
            _playbackToRealUser;

        private ReRecorder _reRecorder;

        private void Awake()
        {
            _reRecorder = GetComponent<ReRecorder>();
        }

        private void OnEnable()
        {
            _reRecorder.RerecordingStarted += BuildLinks;
            _reRecorder.RerecordingEnded += ClearLinks;
        }

        private void OnDisable()
        {
            _reRecorder.RerecordingStarted -= BuildLinks;
            _reRecorder.RerecordingEnded -= ClearLinks;
        }

        public void BuildLinks()
        {
            _playbackToRealUser.Clear();

            MetaAvatarReplayDataWriter[] replayEntities = FindObjectsByType<MetaAvatarReplayDataWriter>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Dictionary<ulong, MetaAvatarReplayDataWriter> realUsersByUserId =
                new Dictionary<ulong, MetaAvatarReplayDataWriter>();
            List<MetaAvatarReplayDataWriter> playbackEntities = new List<MetaAvatarReplayDataWriter>();

            foreach (MetaAvatarReplayDataWriter entity in replayEntities)
            {
                if (entity == null)
                    continue;

                Transform root = entity.transform.root;
                bool isRealUser = root.GetComponent<NetworkObject>() != null;

                if (!isRealUser)
                {
                    playbackEntities.Add(entity);
                    continue;
                }

                MetaAvatarHandler handler = root.GetComponent<MetaAvatarHandler>();
                if (handler != null)
                {
                    VRSYSMetaAvatarEntity local = handler.LocalAvatar();
                    VRSYSMetaAvatarEntity remote = handler.RemoteAvatar();
                    if (entity != local && entity != remote)
                        continue;
                }

                MetaAvatarReplayDataReader reader = root.GetComponent<MetaAvatarReplayDataReader>();
                if (reader == null)
                    continue;

                ulong userId = reader.GetUserId();
                if (userId == 0)
                    continue;

                realUsersByUserId[userId] = entity;
            }

            foreach (MetaAvatarReplayDataWriter playback in playbackEntities)
            {
                if (playback == null)
                    continue;

                ulong userId = playback.GetUserId();
                if (userId == 0)
                    continue;

                MetaAvatarReplayDataWriter realUserEntity;
                if (!realUsersByUserId.TryGetValue(userId, out realUserEntity))
                    continue;

                _playbackToRealUser[playback] = realUserEntity;

                if (verbose)
                {
                    ExtendedLogger.LogInfo(GetType().Name,
                        "Linked playback avatar to real user avatar for user " + userId, this);
                }
            }

            if (verbose)
            {
                ExtendedLogger.LogInfo(GetType().Name,
                    "Built " + _playbackToRealUser.Count + " playback->real user links from "
                    + playbackEntities.Count + " avatar entities.", this);
            }
        }

        private void ClearLinks()
        {
            _playbackToRealUser.Clear();
        }
    }
}
