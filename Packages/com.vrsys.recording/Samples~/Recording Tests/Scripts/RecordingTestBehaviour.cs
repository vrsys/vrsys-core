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

        /// <summary>The object this component changes. Decides whether the server drives it (networked).</summary>
        protected abstract GameObject Target { get; }

        /// <summary>Advance the animation by <paramref name="deltaTime"/> seconds.</summary>
        protected abstract void Tick(float deltaTime);

        /// <summary>Restore the state captured in Awake (pose, parent, active state) and restart the animation.</summary>
        protected abstract void ResetToInitialState();

        protected virtual void OnEnable()
        {
            FindController();
        }

        protected virtual void OnDisable()
        {
            if (_controller != null)
            {
                _controller.onReplayIsPreparing.RemoveListener(ResetIfAuthority);
                _controller.onRecordingHasStarted.RemoveListener(ResetIfAuthority);
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

            if (IsReplaying || !HasAuthority)
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
            if (_controller != null)
                return;
            _controller = FindFirstObjectByType<RecorderController>();
            if (_controller != null)
            {
                _controller.onReplayIsPreparing.AddListener(ResetIfAuthority);
                _controller.onRecordingHasStarted.AddListener(ResetIfAuthority);
            }
        }

        private void ResetIfAuthority()
        {
            if (HasAuthority)
                ResetToInitialState();
        }
    }
}
