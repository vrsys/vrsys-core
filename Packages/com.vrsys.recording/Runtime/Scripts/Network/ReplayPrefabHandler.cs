using Unity.Netcode;
using UnityEngine;

namespace VRSYS.Recording
{
    /// <summary>
    /// Data the server attaches to a networked prefab it spawns as a replay copy (see ScenePreparator).
    /// </summary>
    public struct ReplaySpawnData : INetworkSerializable
    {
        public bool isReplayCopy;
        public int recordedObjectId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref isReplayCopy);
            serializer.SerializeValue(ref recordedObjectId);
        }
    }

    /// <summary>
    /// Creates a client's instance of a networked prefab. A replay copy spawned by the server is built exactly like
    /// the server built it (so both sides have the same NetworkBehaviours) and handed to the ScenePreparator, which
    /// names and parents it like the recorded object. Any other spawn of the prefab is a plain instance.
    /// </summary>
    public class ReplayPrefabHandler : NetworkPrefabInstanceHandlerWithData<ReplaySpawnData>
    {
        private readonly GameObject _prefab;
        private readonly ScenePreparator _scenePreparator;

        public ReplayPrefabHandler(GameObject prefab, ScenePreparator scenePreparator)
        {
            _prefab = prefab;
            _scenePreparator = scenePreparator;
        }

        public override NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation,
            ReplaySpawnData instantiationData)
        {
            if (!instantiationData.isReplayCopy || _scenePreparator == null)
                return Object.Instantiate(_prefab, position, rotation).GetComponent<NetworkObject>();

            GameObject copy = ScenePreparator.CreateNetworkedCopy(_prefab, null);
            copy.transform.SetPositionAndRotation(position, rotation);
            copy.SetActive(true);
            _scenePreparator.OnNetworkedCopyArrived(instantiationData.recordedObjectId, copy);
            return copy.GetComponent<NetworkObject>();
        }

        public override void Destroy(NetworkObject networkObject)
        {
            if (_scenePreparator != null)
                _scenePreparator.OnNetworkedCopyDestroyed(networkObject.gameObject);
            Object.Destroy(networkObject.gameObject);
        }
    }

    /// <summary>
    /// A NetworkObject nested in a replay copy. Netcode spawns nested NetworkObjects of a prefab one by one after the
    /// root, so on a client this takes the not yet spawned NetworkObject from the copy the ReplayPrefabHandler built
    /// instead of instantiating anything.
    /// </summary>
    public class ReplayNestedObjectHandler : NetworkPrefabInstanceHandlerWithData<ReplaySpawnData>
    {
        private readonly uint _prefabIdHash;
        private readonly ScenePreparator _scenePreparator;

        public ReplayNestedObjectHandler(uint prefabIdHash, ScenePreparator scenePreparator)
        {
            _prefabIdHash = prefabIdHash;
            _scenePreparator = scenePreparator;
        }

        public override NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation,
            ReplaySpawnData instantiationData)
        {
            NetworkObject nested = instantiationData.isReplayCopy && _scenePreparator != null
                ? _scenePreparator.FindNestedNetworkObject(instantiationData.recordedObjectId, _prefabIdHash)
                : null;
            if (nested == null)
                RecordingLogger.LogError(nameof(ReplayPrefabHandler), "Replay: no nested NetworkObject " + _prefabIdHash + " found in the replay copy of recorded object " +
                               instantiationData.recordedObjectId + ".");
            return nested;
        }

        public override void Destroy(NetworkObject networkObject)
        {
            if (networkObject != null)
                Object.Destroy(networkObject.gameObject);
        }
    }
}
