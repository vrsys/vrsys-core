using System.Collections.Generic;
using UnityEngine;

namespace VRSYS.Recording
{
    /// <summary>
    /// Registers a push event when a recorded body runs into another recorded body at rest and moves it. The cause is
    /// the transform stream of the mover at the contact, the effects are the transform streams of the pushed object.
    /// Uses the physics of the server, so only contacts the server simulates are found.
    /// </summary>
    public class ContactEventSource : SemanticEventSource
    {
        // A contact waiting to show whether the pushed object really moved.
        private struct Candidate
        {
            public Rigidbody mover;
            public Rigidbody pushed;
            public float contactTime;
            public Vector3 position;
            public Quaternion rotation;
        }

        [SerializeField] private string _eventName = "Push";

        [Tooltip("Recording seconds after the contact at which the pushed object must have moved.")]
        [SerializeField] private float _confirmDelay = 0.3f;

        [Tooltip("Metres the pushed object must have moved by then.")]
        [SerializeField] private float _minDistance = 0.02f;

        [Tooltip("Degrees the pushed object must have turned by then (instead of moving).")]
        [SerializeField] private float _minAngle = 5.0f;

        [Tooltip("Recording seconds in which a contact of the same two objects is not considered again.")]
        [SerializeField] private float _pairCooldown = 1.0f;

        [Tooltip("Speed in m/s below which a body counts as at rest before a contact.")]
        [SerializeField] private float _restSpeed = 0.05f;

        private readonly List<SemanticContactRelay> _relays = new List<SemanticContactRelay>();
        private readonly List<Candidate> _candidates = new List<Candidate>();
        private readonly Dictionary<(int, int), float> _lastContactTime = new Dictionary<(int, int), float>();

        internal float RestSpeed => _restSpeed;

        public override void OnRecordingStarted()
        {
            _candidates.Clear();
            _lastContactTime.Clear();
        }

        public override void OnRecorderRegistered(Recorder recorder)
        {
            // Every recorded body gets a relay, kinematic ones too: they become dynamic when released.
            Rigidbody body = recorder is TransformRecorder ? recorder.GetComponent<Rigidbody>() : null;
            if (body == null || body.GetComponent<SemanticContactRelay>() != null)
                return;

            SemanticContactRelay relay = body.gameObject.AddComponent<SemanticContactRelay>();
            relay.Init(this, body);
            _relays.Add(relay);
        }

        public override void OnRecordingStopping()
        {
            // Contacts that can't wait for their delay any more are judged by what moved so far.
            ConfirmCandidates(true);

            foreach (SemanticContactRelay relay in _relays)
                if (relay != null)
                    Destroy(relay);
            _relays.Clear();
            _candidates.Clear();
        }

        private void Update()
        {
            if (_candidates.Count > 0)
                ConfirmCandidates(false);
        }

        // Called by the relay of `pushed`: `mover` ran into it while it was at rest.
        internal void OnContact(Rigidbody mover, Rigidbody pushed)
        {
            if (Manager == null || !Manager.IsRecordingOnServer)
                return;

            // The same two objects are considered once per cooldown, which also skips their further contacts.
            (int, int) pair = (mover.GetInstanceID(), pushed.GetInstanceID());
            bool coolingDown = _lastContactTime.TryGetValue(pair, out float lastTime) &&
                               Manager.RecordingTime - lastTime < _pairCooldown;
            if (coolingDown)
                return;

            _lastContactTime[pair] = Manager.RecordingTime;
            _candidates.Add(new Candidate
            {
                mover = mover,
                pushed = pushed,
                contactTime = Manager.RecordingTime,
                position = pushed.position,
                rotation = pushed.rotation
            });
        }

        private void ConfirmCandidates(bool final)
        {
            for (int i = 0; i < _candidates.Count;)
            {
                Candidate candidate = _candidates[i];
                bool isDue = final || Manager.RecordingTime - candidate.contactTime >= _confirmDelay;
                if (!isDue)
                {
                    ++i;
                    continue;
                }

                _candidates.RemoveAt(i);
                if (HasMoved(candidate))
                    Register(candidate);
            }
        }

        private bool HasMoved(Candidate candidate)
        {
            if (candidate.pushed == null)
                return false;

            // Moved or turned far enough since the contact.
            bool moved = Vector3.Distance(candidate.pushed.position, candidate.position) >= _minDistance;
            bool turned = Quaternion.Angle(candidate.pushed.rotation, candidate.rotation) >= _minAngle;
            return moved || turned;
        }

        private void Register(Candidate candidate)
        {
            // The mover's own stream is the cause, so the pickup of the mover (effect on that stream) chains to it.
            if (candidate.mover == null ||
                !Manager.TryGetPrimaryReference(candidate.mover.gameObject, candidate.contactTime,
                    out SemanticEventReference cause))
                return;

            List<SemanticEventReference> effects = new List<SemanticEventReference>();
            Manager.CollectReferences(candidate.pushed.gameObject, candidate.contactTime, false, effects);
            if (effects.Count == 0)
                return;

            Manager.RegisterEvent(_eventName, candidate.mover.name + " pushed " + candidate.pushed.name, cause,
                effects);
        }
    }
}
