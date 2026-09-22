using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Recording
{
    [RequireComponent(typeof(NetworkObject))]
    public class ReRecorderMetaAvatarLinker : NetworkBehaviour
    {
        public static ReRecorderMetaAvatarLinker Instance;
        
        public bool verbose;

        // Object references can't be networked here: playback writers have no NetworkObject.
        // The mapping is replicated as user-id pairs and resolved to local objects on each peer.
        public struct UserLink : INetworkSerializable, IEquatable<UserLink>
        {
            public ulong PlaybackUserId;
            public ulong RealUserId;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref PlaybackUserId);
                serializer.SerializeValue(ref RealUserId);
            }

            public bool Equals(UserLink other) =>
                PlaybackUserId == other.PlaybackUserId && RealUserId == other.RealUserId;
        }

        public NetworkList<UserLink> _links;

        private Dictionary<MetaAvatarReplayDataWriter, MetaAvatarReplayDataReader> _playbackToRealUser =
            new Dictionary<MetaAvatarReplayDataWriter, MetaAvatarReplayDataReader>();

        public IReadOnlyDictionary<MetaAvatarReplayDataWriter, MetaAvatarReplayDataReader> PlaybackToRealUser =>
            _playbackToRealUser;

        private void Awake()
        {
            if(Instance != null)
                Destroy(Instance);
            Instance = this;
            
            _links = new NetworkList<UserLink>(); // must exist before the NetworkObject spawns
        }

        public override void OnNetworkSpawn()
        {
            _links.OnListChanged += OnLinksChanged;
            ApplyLinks(); // resolve any state already replicated to a late joiner
        }

        public override void OnNetworkDespawn()
        {
            _links.OnListChanged -= OnLinksChanged;
        }

        private void OnLinksChanged(NetworkListEvent<UserLink> _) => ApplyLinks();

        // Server-authoritative: discovers local avatars, decides the mapping, and replicates it as user-id pairs.
        // Every peer rebuilds its local dictionary in ApplyLinks when the replicated list changes.
        public void BuildLinks()
        {
            if (!IsServer)
                return;

            _links.Clear();

            MetaAvatarReplayDataWriter[] entityWriter = FindObjectsByType<MetaAvatarReplayDataWriter>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Dictionary<ulong, MetaAvatarReplayDataReader> realEntityReader =
                new Dictionary<ulong, MetaAvatarReplayDataReader>();
            List<MetaAvatarReplayDataWriter> playbackEntityWriter = new List<MetaAvatarReplayDataWriter>();

            foreach (MetaAvatarReplayDataWriter entity in entityWriter)
            {
                if (entity == null)
                    continue;

                Transform root = entity.transform.root;
                bool isRealUser = root.GetComponent<NetworkObject>() != null;

                if (!isRealUser)
                {
                    playbackEntityWriter.Add(entity);
                    continue;
                }

                MetaAvatarReplayDataReader reader = entity.GetComponent<MetaAvatarReplayDataReader>();
                if (reader == null)
                {
                    ExtendedLogger.LogWarning(GetType().Name,
                        $"Real-user avatar '{root.name}' has no MetaAvatarReplayDataReader; it cannot be linked.",
                        entity);
                    continue;
                }

                ulong readerUserId = reader.GetUserId();
                if (readerUserId == 0)
                {
                    if (verbose)
                        ExtendedLogger.LogInfo(GetType().Name,
                            $"Skipping real-user reader on '{root.name}': user id is 0 (not yet assigned).", entity);
                    //continue;
                }

                realEntityReader[readerUserId] = reader;
            }

            List<MetaAvatarReplayDataWriter> unassignedWriters = new List<MetaAvatarReplayDataWriter>();
            HashSet<ulong> linkedRealUsers = new HashSet<ulong>();
            foreach (MetaAvatarReplayDataWriter playbackWriter in playbackEntityWriter)
            {
                if (playbackWriter == null)
                    continue;

                ulong userId = playbackWriter.GetUserId();
                if (userId == 0)
                {
                    if (verbose)
                        ExtendedLogger.LogInfo(GetType().Name,
                            $"Skipping playback writer '{playbackWriter.name}': user id is 0 (not yet assigned).",
                            playbackWriter);
                    //continue;
                }

                MetaAvatarReplayDataReader realUserEntity;
                if (!realEntityReader.TryGetValue(userId, out realUserEntity))
                {
                    unassignedWriters.Add(playbackWriter);
                    if (verbose)
                        ExtendedLogger.LogInfo(GetType().Name,
                            $"No real user found for playback user {userId}; deferring '{playbackWriter.name}' to fallback assignment.",
                            playbackWriter);
                    continue;
                }

                _links.Add(new UserLink { PlaybackUserId = userId, RealUserId = realUserEntity.GetUserId() });
                linkedRealUsers.Add(realUserEntity.GetUserId());

                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name,
                        $"Linked playback user {userId} to real user {realUserEntity.GetUserId()}.", this);
            }

            // if a real user is not yet matched, assign it to a playback writer that is not yet matched
            foreach (KeyValuePair<ulong, MetaAvatarReplayDataReader> kv in realEntityReader)
            {
                if (linkedRealUsers.Contains(kv.Key) || unassignedWriters.Count == 0)
                    continue;

                ulong playbackUserId = unassignedWriters[0].GetUserId();
                _links.Add(new UserLink { PlaybackUserId = playbackUserId, RealUserId = kv.Key });
                linkedRealUsers.Add(kv.Key);
                unassignedWriters.RemoveAt(0);

                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name,
                        $"Fallback-linked playback user {playbackUserId} to real user {kv.Key}.", this);
            }

            // Surface unmatched writers unconditionally: such a writer will not play back any real user.
            if (unassignedWriters.Count > 0)
            {
                ExtendedLogger.LogWarning(GetType().Name,
                    $"{unassignedWriters.Count} playback avatar(s) could not be matched to a real user: "
                    + string.Join(", ", unassignedWriters.Select(w => $"'{w.name}' (user {w.GetUserId()})")), this);
            }

            ExtendedLogger.LogInfo(GetType().Name,
                $"Built {_links.Count} playback->real user link(s) from {playbackEntityWriter.Count} "
                + $"playback writer(s) and {realEntityReader.Count} real user(s).", this);
        }

        // Runs on every peer when the replicated list changes (and once on spawn). Resolves user-id pairs to local objects.
        // ponytail: full rebuild on each change; if avatar counts grow, apply NetworkListEvent deltas incrementally instead.
        private void ApplyLinks()
        {
            _playbackToRealUser.Clear();

            Dictionary<ulong, MetaAvatarReplayDataWriter> writersByUser =
                new Dictionary<ulong, MetaAvatarReplayDataWriter>();
            foreach (MetaAvatarReplayDataWriter writer in FindObjectsByType<MetaAvatarReplayDataWriter>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (writer == null || writer.transform.root.GetComponent<NetworkObject>() != null)
                    continue; // only playback writers are link keys
                writersByUser[writer.GetUserId()] = writer;
            }

            Dictionary<ulong, MetaAvatarReplayDataReader> readersByUser =
                new Dictionary<ulong, MetaAvatarReplayDataReader>();
            foreach (MetaAvatarReplayDataReader reader in FindObjectsByType<MetaAvatarReplayDataReader>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (reader != null)
                    readersByUser[reader.GetUserId()] = reader;
            }

            foreach (UserLink link in _links)
            {
                if (writersByUser.TryGetValue(link.PlaybackUserId, out MetaAvatarReplayDataWriter writer) &&
                    readersByUser.TryGetValue(link.RealUserId, out MetaAvatarReplayDataReader reader))
                {
                    _playbackToRealUser[writer] = reader;
                }
                else
                {
                    ExtendedLogger.LogWarning(GetType().Name,
                        $"Could not resolve networked link playback user {link.PlaybackUserId} -> real user "
                        + $"{link.RealUserId} to local objects.", this);
                }
            }

            if (verbose)
            {
                string mapping = string.Join("\n", _playbackToRealUser.Select(kv =>
                    $"    '{kv.Key.name}' (user {kv.Key.GetUserId()}) -> real user {kv.Value.GetUserId()}"));
                ExtendedLogger.LogInfo(GetType().Name,
                    $"Applied {_playbackToRealUser.Count} of {_links.Count} networked link(s):\n" + mapping, this);
            }
        }
 
        public void ClearLinks()
        {
            if (IsServer)
                _links.Clear(); // OnListChanged -> ApplyLinks clears the local dictionary on every peer
        }
    }
}
