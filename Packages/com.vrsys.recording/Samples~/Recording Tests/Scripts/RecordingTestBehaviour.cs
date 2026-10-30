using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace VRSYS.Recording.Testing
{
    /// <summary>
    /// Base class for the small test animations used by the recording samples.
    ///
    /// The animation runs while the recorder is idle or recording and stops while a replay is prepared or
    /// running, so it never fights the playback. When a recording starts
    /// (<see cref="RecorderController.onRecordingHasStarted"/>) and right before a replay matches recorded
    /// objects to the scene (<see cref="RecorderController.onReplayIsPreparing"/>), every test object is put back
    /// into its initial state (pose, parent, active state). The replay finds objects by the hierarchy path they
    /// had when the recording started, so both have to start from the same layout.
    ///
    /// Networked targets (with a <see cref="NetworkObject"/>) are only driven by the server; their changes reach
    /// the clients through Netcode (e.g. a NetworkTransform). Everything else is driven locally on every peer.
    ///
    /// A scene may contain several recorders that record and replay different parts of the scene
    /// (<see cref="RecorderController.recordingRoots"/>). The component follows every recorder, but only reacts to
    /// the ones that cover this object (<see cref="RecorderController.CoversObject"/>): it pauses while one of them
    /// replays it and keeps running while another recorder replays other objects.
    /// </summary>
    public abstract class RecordingTestBehaviour : MonoBehaviour
    {
        private readonly List<RecorderController> _controllers = new List<RecorderController>();
        private readonly Dictionary<RecorderController, (UnityAction preparing, UnityAction started)> _handlers =
            new Dictionary<RecorderController, (UnityAction, UnityAction)>();
        private float _nextControllerLookup;
        private bool _wasRunning = true;

        protected RecordingTestObjectsController TestObjectsController { get; private set; }

        internal void SetTestObjectsController(RecordingTestObjectsController controller)
        {
            TestObjectsController = controller;
        }

        protected virtual void OnTestBehaviourRunningChanged(bool running)
        {
        }

        /// <summary>The object this component changes. Decides whether the server drives it (networked).</summary>
        protected abstract GameObject Target { get; }

        /// <summary>Advance the animation by <paramref name="deltaTime"/> seconds.</summary>
        protected abstract void Tick(float deltaTime);

        /// <summary>Restore the state captured in Awake (pose, parent, active state) and restart the animation.</summary>
        protected abstract void ResetToInitialState();

        /// <summary>
        /// The recorder responsible for this object: one that is replaying or recording it, otherwise the first one
        /// that covers it, otherwise the first recorder in the scene (null until one is found).
        /// </summary>
        protected RecorderController Controller
        {
            get
            {
                RecorderController covering = null;
                foreach (RecorderController controller in _controllers)
                {
                    if (controller == null || !controller.CoversObject(gameObject))
                        continue;
                    State state = controller.recorderState.currentState;
                    if (state != State.Idle)
                        return controller;
                    if (covering == null)
                        covering = controller;
                }
                if (covering != null)
                    return covering;
                foreach (RecorderController controller in _controllers)
                    if (controller != null)
                        return controller;
                return null;
            }
        }

        /// <summary>All recorders in the scene (looked up again once per second).</summary>
        protected IReadOnlyList<RecorderController> Controllers => _controllers;

        /// <summary>Called once per recorder when it is found, e.g. to register a generic recorder provider.</summary>
        protected virtual void OnControllerFound(RecorderController controller)
        {
        }

        /// <summary>Called for every known recorder when this component is disabled.</summary>
        protected virtual void OnControllerLost(RecorderController controller)
        {
        }

        /// <summary>
        /// The generic recorder of type <typeparamref name="T"/> on <paramref name="target"/> that belongs to
        /// <paramref name="controller"/>, added if missing. Each recorder gets its own component, so one recorder can
        /// replay the data while another one records it.
        /// </summary>
        protected static T GetOrAddGenericRecorder<T>(GameObject target, RecorderController controller)
            where T : GenericRecorder
        {
            foreach (T existing in target.GetComponents<T>())
            {
                if (existing.controller == controller || existing.controller == null)
                {
                    existing.controller = controller;
                    return existing;
                }
            }
            T recorder = target.AddComponent<T>();
            recorder.controller = controller;
            return recorder;
        }

        /// <summary>
        /// Called on every peer right before a replay matches recorded objects to the scene, before the
        /// authority resets the test objects. Use it for clean-up that every peer must do itself.
        /// </summary>
        protected virtual void OnReplayPreparing()
        {
        }

        protected virtual void OnEnable()
        {
            TestObjectsController = GetComponentInParent<RecordingTestObjectsController>(true);
            FindControllers();
        }

        protected virtual void OnDisable()
        {
            foreach (RecorderController controller in _controllers)
            {
                if (controller == null)
                    continue;
                if (_handlers.TryGetValue(controller, out var handlers))
                {
                    controller.onReplayIsPreparing.RemoveListener(handlers.preparing);
                    controller.onRecordingHasStarted.RemoveListener(handlers.started);
                }
                OnControllerLost(controller);
            }
            _controllers.Clear();
            _handlers.Clear();
        }

        protected virtual void Update()
        {
            if (Time.unscaledTime >= _nextControllerLookup)
            {
                _nextControllerLookup = Time.unscaledTime + 1f;
                FindControllers();
            }

            bool running = TestObjectsController == null || TestObjectsController.IsRunning;
            if (running != _wasRunning)
            {
                _wasRunning = running;
                OnTestBehaviourRunningChanged(running);
            }

            if (!running || IsReplaying || !HasAuthority)
                return;

            Tick(Time.deltaTime);
        }

        /// <summary>True while a recorder that covers this object prepares or runs a replay.</summary>
        protected bool IsReplaying => IsReplayedBy(null);

        // True if a recorder other than `except` replays (or prepares to replay) this object.
        private bool IsReplayedBy(RecorderController except)
        {
            foreach (RecorderController controller in _controllers)
            {
                if (controller == null || controller == except || controller.recorderState == null)
                    continue;
                State state = controller.recorderState.currentState;
                if ((state == State.PreparingReplay || state == State.Replaying) && controller.CoversObject(gameObject))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Whether this peer may change the target: always for local objects; networked objects only on the
        /// server of a running session (or in a scene without any NetworkManager).
        /// </summary>
        protected bool HasAuthority
        {
            get
            {
                GameObject target = Target;
                if (target == null || target.GetComponent<NetworkObject>() == null)
                    return true;
                NetworkManager network = NetworkManager.Singleton;
                if (network == null)
                    return true;
                return network.IsListening && network.IsServer;
            }
        }

        /// <summary>The target's NetworkObject if it is networked and spawned, otherwise null.</summary>
        protected NetworkObject SpawnedNetworkObject
        {
            get
            {
                GameObject target = Target;
                NetworkObject networkObject = target != null ? target.GetComponent<NetworkObject>() : null;
                return networkObject != null && networkObject.IsSpawned ? networkObject : null;
            }
        }

        private void FindControllers()
        {
            _controllers.RemoveAll(c => c == null);
            foreach (RecorderController controller in
                     FindObjectsByType<RecorderController>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID))
            {
                if (_controllers.Contains(controller))
                    continue;
                _controllers.Add(controller);
                UnityAction preparing = () => HandleReplayPreparing(controller);
                UnityAction started = () => HandleRecordingStarted(controller);
                _handlers[controller] = (preparing, started);
                controller.onReplayIsPreparing.AddListener(preparing);
                controller.onRecordingHasStarted.AddListener(started);
                OnControllerFound(controller);
            }
        }

        // A recording starts from the initial layout, unless another recorder is replaying this object right now
        // (a recording of a running replay captures the replayed state instead).
        private void HandleRecordingStarted(RecorderController controller)
        {
            if (!controller.CoversObject(gameObject) || IsReplayedBy(controller))
                return;
            if (HasAuthority)
                ResetToInitialState();
        }

        // Only the recorder that will replay this object resets it; one that is already replayed by another recorder
        // stays with that replay.
        private void HandleReplayPreparing(RecorderController controller)
        {
            if (!controller.CoversObject(gameObject) || IsReplayedBy(controller))
                return;
            OnReplayPreparing();
            if (HasAuthority)
                ResetToInitialState();
        }
    }
}
