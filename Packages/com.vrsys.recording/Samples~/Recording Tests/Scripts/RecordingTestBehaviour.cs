using Unity.Netcode;
using UnityEngine;

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
    /// </summary>
    public abstract class RecordingTestBehaviour : MonoBehaviour
    {
        private RecorderController _controller;
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

        /// <summary>The scene's recorder, once found (looked up once per second until one exists).</summary>
        protected RecorderController Controller => _controller;

        /// <summary>Called once the recorder is found, e.g. to register a generic recorder provider.</summary>
        protected virtual void OnControllerFound(RecorderController controller)
        {
        }

        /// <summary>Called when this component is disabled while it knows a recorder.</summary>
        protected virtual void OnControllerLost(RecorderController controller)
        {
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
            FindController();
        }

        protected virtual void OnDisable()
        {
            if (_controller != null)
            {
                _controller.onReplayIsPreparing.RemoveListener(HandleReplayPreparing);
                _controller.onRecordingHasStarted.RemoveListener(HandleRecordingStarted);
                OnControllerLost(_controller);
            }
            _controller = null;
        }

        protected virtual void Update()
        {
            if (_controller == null && Time.unscaledTime >= _nextControllerLookup)
            {
                _nextControllerLookup = Time.unscaledTime + 1f;
                FindController();
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

        /// <summary>True while a replay is being prepared or running.</summary>
        protected bool IsReplaying
        {
            get
            {
                RecorderState state = _controller != null ? _controller.recorderState : null;
                return state != null &&
                       (state.currentState == State.PreparingReplay || state.currentState == State.Replaying);
            }
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

        private void FindController()
        {
            if (_controller == null)
                _controller = FindFirstObjectByType<RecorderController>();
            if (_controller != null)
            {
                // Hot reload can retain the controller reference while its provider list is rebuilt.
                _controller.onReplayIsPreparing.RemoveListener(HandleReplayPreparing);
                _controller.onRecordingHasStarted.RemoveListener(HandleRecordingStarted);
                _controller.onReplayIsPreparing.AddListener(HandleReplayPreparing);
                _controller.onRecordingHasStarted.AddListener(HandleRecordingStarted);
                OnControllerFound(_controller);
            }
        }

        private void HandleRecordingStarted()
        {
            if (HasAuthority)
                ResetToInitialState();
        }

        private void HandleReplayPreparing()
        {
            OnReplayPreparing();
            if (HasAuthority)
                ResetToInitialState();
        }
    }
}
