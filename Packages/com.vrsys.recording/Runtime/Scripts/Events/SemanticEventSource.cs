using UnityEngine;

namespace VRSYS.Recording
{
    /// <summary>
    /// Detects one kind of semantic event (pickups, pushes, …) and registers it with the
    /// <see cref="SemanticEventManager"/> on the same GameObject. The manager calls the hooks on the server only, for
    /// recordings (never for replays).
    /// </summary>
    [RequireComponent(typeof(SemanticEventManager))]
    public abstract class SemanticEventSource : MonoBehaviour
    {
        protected SemanticEventManager Manager { get; private set; }

        internal void Bind(SemanticEventManager manager)
        {
            Manager = manager;
        }

        /// <summary>A recording has started.</summary>
        public virtual void OnRecordingStarted()
        {
        }

        /// <summary>A recorder was attached for the recording, also for objects spawned while recording.</summary>
        public virtual void OnRecorderRegistered(Recorder recorder)
        {
        }

        /// <summary>The recording is about to stop; events can still be registered. Remove what you attached.</summary>
        public virtual void OnRecordingStopping()
        {
        }
    }
}
