using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Recording
{
    /// <summary>
    /// Registers semantic events in the recording of the RecorderController on this GameObject and distributes them to
    /// all peers. Events are detected by <see cref="SemanticEventSource"/> components on this GameObject or reported by
    /// game code through <see cref="Report"/>. The consequences of an interaction (e.g. an object breaking) are left to
    /// that code; the manager only registers and distributes what was detected. Only the server registers, while it
    /// records.
    /// </summary>
    [RequireComponent(typeof(RecorderController))]
    public class SemanticEventManager : NetworkBehaviour
    {
        // A report waiting for the recorders of its objects (e.g. objects spawned right before).
        private class PendingReport
        {
            public string name;
            public string description;
            public GameObject cause;
            public List<GameObject> effects;
            public float startTime;
            public float deadline;
        }

        [Tooltip("Seconds a report waits for the recorders of its objects. Then it is registered with the objects " +
                 "that have one.")]
        [SerializeField] private float _reportTimeout = 1.0f;

        private RecorderController _controller;
        private SemanticEventSource[] _sources;
        private readonly List<PendingReport> _pendingReports = new List<PendingReport>();
        private readonly List<SemanticEventData> _events = new List<SemanticEventData>();

        /// <summary>Raised on every peer for each registered event.</summary>
        public event Action<SemanticEventData> EventRegistered;

        public RecorderController Controller => _controller;

        /// <summary>The events registered in the current or last recording, on every peer.</summary>
        public IReadOnlyList<SemanticEventData> Events => _events;

        /// <summary>True on the server (or without a network session) while it records.</summary>
        public bool IsRecordingOnServer => IsServerPeer && _controller.CurrentState == State.Recording;

        /// <summary>Recording time of the current frame, the clock the recorders stamp their samples with.</summary>
        public float RecordingTime => _controller.recorderState.currentRecordingTime;

        // Without a running network session this peer acts as the server.
        private static bool IsServerPeer
        {
            get
            {
                var networkManager = Unity.Netcode.NetworkManager.Singleton;
                return networkManager == null || !networkManager.IsListening || networkManager.IsServer;
            }
        }

        #region MonoBehaviour Callbacks

        private void Awake()
        {
            _controller = GetComponent<RecorderController>();
            _sources = GetComponents<SemanticEventSource>();
            foreach (SemanticEventSource source in _sources)
                source.Bind(this);
        }

        private void OnEnable()
        {
            _controller.onRecordingHasStarted.AddListener(OnRecordingStarted);
            _controller.RecorderRegisteredForRecording += OnRecorderRegistered;
            _controller.RecordingStopping += OnRecordingStopping;
        }

        private void OnDisable()
        {
            _controller.onRecordingHasStarted.RemoveListener(OnRecordingStarted);
            _controller.RecorderRegisteredForRecording -= OnRecorderRegistered;
            _controller.RecordingStopping -= OnRecordingStopping;
        }

        private void Update()
        {
            if (_pendingReports.Count > 0 && IsRecordingOnServer)
                ProcessPendingReports(false);
        }

        #endregion

        #region Recording Lifecycle

        private void OnRecordingStarted()
        {
            _events.Clear();
            _pendingReports.Clear();

            if (!IsServerPeer)
                return;
            foreach (SemanticEventSource source in _sources)
                source.OnRecordingStarted();
        }

        private void OnRecorderRegistered(Recorder recorder)
        {
            if (!IsServerPeer)
                return;
            foreach (SemanticEventSource source in _sources)
                source.OnRecorderRegistered(recorder);
        }

        private void OnRecordingStopping()
        {
            if (!IsServerPeer)
                return;
            foreach (SemanticEventSource source in _sources)
                source.OnRecordingStopping();

            // The recording is still open: register what is still waiting.
            ProcessPendingReports(true);
        }

        #endregion

        #region Registration

        /// <summary>
        /// Registers an event with its cause and effects in the recording (server only, while recording) and sends it to
        /// all clients. Returns the event id, or -1.
        /// </summary>
        public int RegisterEvent(string eventName, string description, SemanticEventReference cause,
            IReadOnlyList<SemanticEventReference> effects)
        {
            if (!IsRecordingOnServer)
            {
                if (RecordingLogger.IsEnabled(LogLevel.Debug))
                    RecordingLogger.LogDebug(GetType().Name,
                        "Not recording on the server, event '" + eventName + "' is ignored.", this);
                return -1;
            }

            if (effects == null || effects.Count == 0)
            {
                RecordingLogger.LogWarning(GetType().Name, "Event '" + eventName + "' has no effects and is ignored.",
                    this);
                return -1;
            }

            eventName = CleanText(eventName, "Event");
            description = CleanText(description, eventName);
            byte[] nameBytes = Encoding.UTF8.GetBytes(eventName);
            byte[] descriptionBytes = Encoding.UTF8.GetBytes(description);

            int recorderId = _controller.RecorderID;
            int eventId = SemanticEventNative.AddSemanticEvent(recorderId, nameBytes, nameBytes.Length,
                descriptionBytes, descriptionBytes.Length, (int)cause.type, cause.id, cause.track, cause.startTime);
            if (eventId < 0)
            {
                RecordingLogger.LogError(GetType().Name, "Could not register event '" + description + "'.", this);
                return -1;
            }

            foreach (SemanticEventReference effect in effects)
            {
                if (SemanticEventNative.AddSemanticEventEffect(recorderId, eventId, (int)effect.type, effect.id,
                        effect.track, effect.startTime))
                    continue;

                // An event with only some of its effects would cascade only partly, so it is dropped.
                SemanticEventNative.RemoveSemanticEvent(recorderId, eventId);
                RecordingLogger.LogError(GetType().Name, "Could not add effect " + effect.type + " " + effect.id +
                                                         " to event '" + description + "'; the event is dropped.", this);
                return -1;
            }

            SemanticEventData data = new SemanticEventData
            {
                eventId = eventId,
                name = eventName,
                description = description,
                cause = cause,
                effects = effects.ToArray()
            };
            AddEvent(data);
            if (IsSpawned)
                EventRegisteredRpc(data);

            if (RecordingLogger.IsEnabled(LogLevel.Info))
                RecordingLogger.LogInfo(GetType().Name,
                    "Registered event " + eventId + " '" + description + "': cause " + cause.type + " " + cause.id +
                    " at " + cause.startTime.ToString("F2") + " s, " + effects.Count + " effect(s).", this);
            return eventId;
        }

        /// <summary>
        /// Reports an interaction detected by game code, on any peer. A client forwards it to the server. The server
        /// registers it with the recording time at which the report arrived, once the objects have recorders: the cause
        /// is the transform stream of the cause object itself, the effects are the transform and sound streams of each
        /// effect object and its children. Objects reported by a client must be spawned NetworkObjects.
        /// </summary>
        public void Report(string eventName, string description, GameObject cause, IReadOnlyList<GameObject> effects)
        {
            effects ??= Array.Empty<GameObject>();
            if (IsServerPeer)
            {
                QueueReport(eventName, description, cause, effects.ToList());
                return;
            }

            if (!TryGetNetworkObject(cause, out NetworkObjectReference causeReference))
            {
                RecordingLogger.LogWarning(GetType().Name,
                    "The cause of '" + eventName + "' is no spawned NetworkObject; the report is ignored.", this);
                return;
            }

            List<NetworkObjectReference> effectReferences = new List<NetworkObjectReference>();
            foreach (GameObject effect in effects)
            {
                if (TryGetNetworkObject(effect, out NetworkObjectReference effectReference))
                    effectReferences.Add(effectReference);
                else
                    RecordingLogger.LogWarning(GetType().Name, "An effect of '" + eventName +
                                                               "' is no spawned NetworkObject and is left out.", this);
            }

            ReportRpc(eventName, description, causeReference, effectReferences.ToArray());
        }

        /// <summary>The transform stream of <paramref name="go"/> itself (for a prop: the GameObject with its Rigidbody).</summary>
        public bool TryGetPrimaryReference(GameObject go, float startTime, out SemanticEventReference reference)
        {
            reference = default;
            if (go == null)
                return false;

            foreach (TransformRecorder recorder in go.GetComponents<TransformRecorder>())
            {
                if (recorder.controller != _controller)
                    continue;
                reference = SemanticEventReference.For(recorder, startTime);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds the transform streams of <paramref name="go"/> and its children, the object itself first, and with
        /// <paramref name="includeSound"/> also their sound streams.
        /// </summary>
        public void CollectReferences(GameObject go, float startTime, bool includeSound,
            List<SemanticEventReference> references)
        {
            if (go == null)
                return;

            foreach (Recorder recorder in go.GetComponentsInChildren<Recorder>(true))
            {
                // A stream of this controller of a wanted type.
                bool ownRecorder = recorder.controller == _controller;
                bool wantedType = recorder is TransformRecorder || (includeSound && recorder is AudioRecorder);
                if (ownRecorder && wantedType)
                    references.Add(SemanticEventReference.For(recorder, startTime));
            }
        }

        #endregion

        #region Reports

        private void QueueReport(string eventName, string description, GameObject cause, List<GameObject> effects)
        {
            if (!IsRecordingOnServer)
                return;

            _pendingReports.Add(new PendingReport
            {
                name = eventName,
                description = description,
                cause = cause,
                effects = effects,
                startTime = RecordingTime,
                deadline = Time.realtimeSinceStartup + _reportTimeout
            });
            ProcessPendingReports(false);
        }

        // Registers the reports whose objects have recorders; with `final` (or after the timeout) also the others.
        private void ProcessPendingReports(bool final)
        {
            for (int i = 0; i < _pendingReports.Count;)
            {
                PendingReport report = _pendingReports[i];
                bool timedOut = final || Time.realtimeSinceStartup >= report.deadline;
                if (TryRegisterReport(report, timedOut))
                    _pendingReports.RemoveAt(i);
                else
                    ++i;
            }
        }

        // Returns false while the report should wait for recorders.
        private bool TryRegisterReport(PendingReport report, bool final)
        {
            bool hasCause = TryGetPrimaryReference(report.cause, report.startTime, out SemanticEventReference cause);
            List<SemanticEventReference> effects = new List<SemanticEventReference>();
            bool allEffectsRecorded = true;
            foreach (GameObject effect in report.effects)
            {
                int countBefore = effects.Count;
                CollectReferences(effect, report.startTime, true, effects);
                // An object without a recorder may still get one (spawned right before the report).
                if (effects.Count == countBefore)
                    allEffectsRecorded = false;
            }

            bool complete = hasCause && allEffectsRecorded;
            if (!complete && !final)
                return false;

            bool usable = hasCause && effects.Count > 0;
            if (!usable)
            {
                RecordingLogger.LogWarning(GetType().Name, "Report '" + report.name +
                                                           "' has no recorded cause or effects and is dropped.", this);
                return true;
            }

            if (!complete)
                RecordingLogger.LogWarning(GetType().Name, "Report '" + report.name +
                                                           "' is registered without its objects that have no recorder.",
                    this);
            RegisterEvent(report.name, report.description, cause, effects);
            return true;
        }

        private static bool TryGetNetworkObject(GameObject go, out NetworkObjectReference reference)
        {
            reference = default;
            NetworkObject networkObject = go != null ? go.GetComponent<NetworkObject>() : null;
            if (networkObject == null || !networkObject.IsSpawned)
                return false;
            reference = networkObject;
            return true;
        }

        #endregion

        #region RPCs

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReportRpc(string eventName, string description, NetworkObjectReference cause,
            NetworkObjectReference[] effects)
        {
            if (!cause.TryGet(out NetworkObject causeObject))
            {
                RecordingLogger.LogWarning(GetType().Name,
                    "The cause of the reported '" + eventName + "' is not spawned here; the report is ignored.", this);
                return;
            }

            List<GameObject> effectObjects = new List<GameObject>();
            foreach (NetworkObjectReference effect in effects)
                if (effect.TryGet(out NetworkObject effectObject))
                    effectObjects.Add(effectObject.gameObject);

            QueueReport(eventName, description, causeObject.gameObject, effectObjects);
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        private void EventRegisteredRpc(SemanticEventData data)
        {
            AddEvent(data);
        }

        #endregion

        private void AddEvent(SemanticEventData data)
        {
            _events.Add(data);
            EventRegistered?.Invoke(data);
        }

        // The .events file is ';'-separated and line based, so texts must not contain ';' or line breaks.
        private static string CleanText(string text, string fallback)
        {
            if (string.IsNullOrWhiteSpace(text))
                return fallback;
            return text.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
