using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace VRSYS.Recording.Testing
{
    /// <summary>
    /// Creates an instance of <see cref="prefab"/> every <see cref="spawnInterval"/> seconds and removes it again
    /// after <see cref="lifetime"/> seconds, to check that objects created and destroyed during a recording are
    /// replayed. Instances are named "<see cref="instanceName"/> &lt;n&gt;" so every one has a unique hierarchy
    /// path within a recording, and they take turns on <see cref="spawnSlots"/> positions along this object's local
    /// x axis.
    ///
    /// The replay can only recreate the instances if the prefab has a <see cref="RecordingPrefabInformation"/> that
    /// points to its location in a Resources folder. A prefab with a <see cref="NetworkObject"/> is spawned and
    /// despawned by the server only (it must be in the session's network prefab list); a local prefab is
    /// instantiated on every peer. Live instances are renamed and removed before a replay so they are not mistaken
    /// for recorded ones, and their lifetime stops counting while the tests are paused.
    /// </summary>
    [AddComponentMenu("VRSYS/Recording/Testing/Recording Test Spawner")]
    public class RecordingTestSpawner : RecordingTestBehaviour
    {
        [Tooltip("Prefab to create. Needs a RecordingPrefabInformation pointing to its location in a Resources " +
                 "folder so the replay can recreate it. A prefab with a NetworkObject is spawned by the server.")]
        public GameObject prefab;

        [Tooltip("Instances are named \"<Instance Name> <n>\"; the number keeps names unique within a recording.")]
        public string instanceName = "Spawned Object";

        [Tooltip("Parent of the instances. Empty: this object for a local prefab, the scene root for a networked one.")]
        public Transform spawnParent;

        [Tooltip("Seconds between two instances.")]
        public float spawnInterval = 1.5f;

        [Tooltip("Seconds an instance exists before it is removed.")]
        public float lifetime = 3.5f;

        [Tooltip("Number of positions the instances take turns on, side by side along this object's local x axis.")]
        public int spawnSlots = 4;

        [Tooltip("Distance between two neighbouring positions in metres.")]
        public float slotSpacing = 0.3f;

        private class Instance
        {
            public GameObject gameObject;
            public float age;
        }

        private readonly List<Instance> _instances = new List<Instance>();
        private float _sinceSpawn;
        private int _counter;
        private bool _warnedMissingPrefab;

        private bool IsNetworkedPrefab => prefab != null && prefab.GetComponent<NetworkObject>() != null;

        // The prefab decides who may spawn: a networked prefab is only spawned by the server.
        protected override GameObject Target => prefab != null ? prefab : gameObject;

        protected override void Tick(float deltaTime)
        {
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                Instance instance = _instances[i];
                if (instance.gameObject == null)
                {
                    _instances.RemoveAt(i);
                    continue;
                }

                instance.age += deltaTime;
                if (instance.age >= lifetime)
                {
                    Remove(instance.gameObject, false);
                    _instances.RemoveAt(i);
                }
            }

            _sinceSpawn += deltaTime;
            if (spawnInterval > 0f && _sinceSpawn >= spawnInterval)
            {
                _sinceSpawn -= spawnInterval;
                Spawn();
            }
        }

        private void Spawn()
        {
            if (prefab == null)
            {
                if (!_warnedMissingPrefab)
                {
                    _warnedMissingPrefab = true;
                    Debug.LogWarning($"[{nameof(RecordingTestSpawner)}] No prefab assigned on '{name}'.", this);
                }
                return;
            }

            int number = ++_counter;
            int slots = Mathf.Max(1, spawnSlots);
            int slot = (number - 1) % slots;
            Vector3 localOffset = new Vector3((slot - (slots - 1) * 0.5f) * slotSpacing, 0f, 0f);
            Vector3 position = transform.TransformPoint(localOffset);

            bool networked = IsNetworkedPrefab;
            Transform parent = spawnParent != null ? spawnParent : networked ? null : transform;

            GameObject instance = Instantiate(prefab, position, transform.rotation, parent);
            instance.name = instanceName + " " + number;

            // Instances spawned at the scene root cannot find the test objects controller themselves, so hand it
            // over: their own test behaviours then pause together with the rest of the tests.
            foreach (RecordingTestBehaviour behaviour in instance.GetComponentsInChildren<RecordingTestBehaviour>(true))
                behaviour.SetTestObjectsController(TestObjectsController);

            NetworkManager network = NetworkManager.Singleton;
            if (networked && network != null && network.IsListening)
                instance.GetComponent<NetworkObject>().Spawn(true);

            _instances.Add(new Instance { gameObject = instance });

            // Objects created during a recording are not picked up automatically; tell the recorder about them.
            RecorderController controller = Controller;
            if (controller != null && controller.recorderState != null &&
                controller.recorderState.currentState == State.Recording)
                controller.StartCoroutine(controller.SignalObjectInstantiation(instance));
        }

        private static void Remove(GameObject instance, bool beforeReplay)
        {
            if (instance == null)
                return;

            // Destroy only takes effect at the end of the frame. Renaming first keeps a live instance from being
            // matched to a recorded object of the same name while the replay is prepared.
            if (beforeReplay)
                instance.name += " (Removed)";

            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                NetworkManager network = networkObject.NetworkManager;
                if (network != null && network.IsServer && network.IsListening && !network.ShutdownInProgress)
                    networkObject.Despawn(true);
                return;
            }

            Destroy(instance);
        }

        private void RemoveAll(bool beforeReplay)
        {
            foreach (Instance instance in _instances)
                Remove(instance.gameObject, beforeReplay);
            _instances.Clear();
        }

        protected override void OnReplayPreparing()
        {
            RemoveAll(true);
        }

        protected override void ResetToInitialState()
        {
            RemoveAll(true);
            _sinceSpawn = 0f;
            _counter = 0;
        }

        private void OnDestroy()
        {
            // Local instances under this object go with it; take the others (e.g. at the scene root) along.
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                GameObject instance = _instances[i].gameObject;
                if (instance != null && !instance.transform.IsChildOf(transform))
                    Remove(instance, false);
            }
            _instances.Clear();
        }
    }
}
