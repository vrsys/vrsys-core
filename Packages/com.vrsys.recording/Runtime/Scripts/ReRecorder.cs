using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Networking;
using Vrsys.Scripts.Recording;

namespace VRSYS.Scripts.Recording
{
    [Flags]
    public enum ReRecordConfiguration
    {
        None = 0,
        Transform = 1,
        Sound = 2,
        Generic = 4,
        All = Transform | Sound | Generic
    }

    [Serializable]
    public struct ReRecordTargets
    {
        public ReRecordConfiguration configuration;
        public List<int> transformRecorderIds;
        public List<int> genericRecorderIds;
        public List<int> audioRecorderIds;
    }

    [RequireComponent(typeof(RecorderController))]
    [RequireComponent(typeof(RecorderState))]
    public class ReRecorder : MonoBehaviour
    {
        [DllImport("RecordingPlugin")]
        private static extern bool ApplyOverwriteTransform(int recorderId, int track, int objectId,
            float start, float end, float[] sampleTimes, float[] sampleMatrixData, int[] sampleObjectInfo,
            int sampleCount);

        [DllImport("RecordingPlugin")]
        private static extern bool ApplyOverwriteSound(int recorderId, int track, int soundId,
            float start, float end, int samplingRate, int channelNum, int correspondingGameobjectId,
            float[] concatenatedSamples, int[] chunkLengths, float[] chunkTimes, int chunkCount);

        [DllImport("RecordingPlugin")]
        private static extern bool ApplyOverwriteGeneric(int recorderId, int track, int objectId,
            float start, float end, float[] sampleTimes, int[] intPayload, float[] floatPayload,
            byte[] charPayload, int sampleCount);

        [DllImport("RecordingPlugin")]
        private static extern bool UndoLastEdit(int recorderId);

        [DllImport("RecordingPlugin")]
        private static extern bool SaveRecordingEdits(int recorderId);

        private const int Track = 0;
        private const int GenericNumberCount = 10;
        private const int GenericCharCount = 2048;

        private const int MicrophoneSoundId = 0;
        private const int AudioListenerSoundId = 1;
        private const float DrainIntervalSeconds = 0.5f;

        // Serializes plugin entry points across worker threads. The plugin (RecorderManager)
        // is a singleton mutating global state; any future off-thread caller should also lock.
        private static readonly object PluginLock = new object();

        private readonly List<TransformRecorder> _activeTransformRecorders = new List<TransformRecorder>();
        private readonly List<GenericRecorder> _activeGenericRecorders = new List<GenericRecorder>();
        private readonly List<AudioRecorder> _activeAudioRecorders = new List<AudioRecorder>();
        private readonly List<AudioRecorder> _transientAudioRecorders = new List<AudioRecorder>();
        private readonly List<GameObject> _transientHostGameObjects = new List<GameObject>();

        private RecorderController _controller;
        private RecorderState _state;
        private float _startTime;
        private float _drainStartTime;
        private float _nextDrainTime;
        private int _lastAppliedOperationCount;
        private int _applyBatchCount;
        private Task<int> _applyChain = Task.FromResult(0);

        private readonly List<TransformOverwriteJob> _pendingTransformJobs = new List<TransformOverwriteJob>();
        private readonly List<GenericOverwriteJob> _pendingGenericJobs = new List<GenericOverwriteJob>();
        private readonly List<AudioOverwriteJob> _pendingAudioJobs = new List<AudioOverwriteJob>();
        private readonly Dictionary<TransformRecorder, float> _transformDrainStartTimes =
            new Dictionary<TransformRecorder, float>();
        private readonly Dictionary<GenericRecorder, float> _genericDrainStartTimes =
            new Dictionary<GenericRecorder, float>();
        private readonly Dictionary<AudioRecorder, float> _audioDrainStartTimes =
            new Dictionary<AudioRecorder, float>();

        public bool IsBuffering { get; private set; }
        
        public bool IsProcessing { get; private set; }
        
        public bool IsRerecording { get; private set; }

        public bool verbose;
        public event Action<bool> ProcessingStateChanged;
        public event Action RerecordingStarted;
        public event Action RerecordingEnded;

        public InputActionProperty toggleRerecordingAction;
        
        private struct TransformOverwriteJob
        {
            public float startTime;
            public float endTime;
            public int objectId;
            public float[] times;
            public float[] matrix;
            public int[] info;
            public int count;
        }

        private struct GenericOverwriteJob
        {
            public float startTime;
            public float endTime;
            public int objectId;
            public float[] times;
            public int[] ints;
            public float[] floats;
            public byte[] chars;
            public int count;
        }

        private struct AudioOverwriteJob
        {
            public float startTime;
            public float endTime;
            public int soundId;
            public int samplingRate;
            public int channelNum;
            public int correspondingGameobjectId;
            public float[] samples;
            public int[] lengths;
            public float[] times;
            public int chunkCount;
        }

        private void Awake()
        {
            _controller = GetComponent<RecorderController>();
            _state = GetComponent<RecorderState>();
            toggleRerecordingAction.action.Enable();
            IsProcessing = false;
            IsRerecording = false;
            IsBuffering = false;
        }

        public bool BeginRerecording(ReRecordTargets targets)
        {
            if (IsBuffering)
            {
                Debug.LogError("ReRecorder.Begin: already buffering");
                return false;
            }

            if (IsProcessing)
            {
                Debug.LogError("ReRecorder.Begin: a previous operation is still processing");
                return false;
            }

            if (_state.currentState != State.Replaying)
            {
                Debug.LogError("ReRecorder.Begin: recorder must be replaying");
                return false;
            }

            if (targets.configuration == ReRecordConfiguration.None)
                targets.configuration = ReRecordConfiguration.All;

            _startTime = Time.time;
            _drainStartTime = _startTime;
            _nextDrainTime = _startTime + DrainIntervalSeconds;
            _lastAppliedOperationCount = 0;
            _applyBatchCount = 0;
            _applyChain = Task.FromResult(0);
            ClearPreparedJobs();
            IsBuffering = true;

            bool ok = true;
            if ((targets.configuration & ReRecordConfiguration.Transform) != 0)
                ok &= CollectTargetTransformRecorders(targets);
            if ((targets.configuration & ReRecordConfiguration.Generic) != 0)
                ok &= CollectTargetGenericRecorders(targets);
            if ((targets.configuration & ReRecordConfiguration.Sound) != 0)
                ok &= CollectTargetAudioRecorders(targets);

            if (!ok)
            {
                ResetSession();
                return false;
            }

            foreach (TransformRecorder r in _activeTransformRecorders)
                r.BeginRerecordCapture();
            foreach (GenericRecorder r in _activeGenericRecorders)
                r.BeginRerecordCapture();
            foreach (AudioRecorder r in _activeAudioRecorders)
                r.BeginRerecordCapture();

            RerecordingStarted?.Invoke();

            IsRerecording = true;
            return true;
        }

        private void Update()
        {
            if (toggleRerecordingAction.action.WasPressedThisFrame())
            {
                if (!IsRerecording)
                {
                    ReRecordTargets targets = new ReRecordTargets
                    {
                        configuration = ReRecordConfiguration.Transform
                    };
                    
                    BeginRerecording(targets);
                }
                else
                {
                    EndRerecording();
                }
            }
        }

        private void FixedUpdate()
        {
            if (!IsBuffering)
                return;

            if (_state.currentState != State.Replaying)
            {
                Debug.LogWarning("ReRecorder: replay ended while buffering; cancelling");
                Cancel();
                return;
            }

            if(!IsRerecording)
                return;
            
            float time = _state.currentReplayTime;

            foreach (TransformRecorder r in _activeTransformRecorders)
                if (r != null) r.TickRerecordCapture(time);
            foreach (GenericRecorder r in _activeGenericRecorders)
                if (r != null) r.TickRerecordCapture(time);
            foreach (AudioRecorder r in _activeAudioRecorders)
                if (r != null) r.TickRerecordCapture(time);

            if (time >= _nextDrainTime)
            {
                DrainCaptureSegment(time);
                QueuePreparedApplyJobs(_controller.RecorderID, "ReRecorder");
                _nextDrainTime = time + DrainIntervalSeconds;
            }
        }

        public async void EndRerecording()
        {
            if (!IsBuffering)
            {
                Debug.LogError("ReRecorder.End: not buffering");
                return;
            }

            float endTime = _state.currentReplayTime;
            if (endTime < _startTime)
            {
                Debug.LogError("ReRecorder.End: end time is before start time");
                ResetSession();
                return;
            }

            int recorderId = _controller.RecorderID;
            DrainCaptureSegment(endTime, true);
            QueuePreparedApplyJobs(recorderId, "ReRecorder.End");
            ResetSession();

            SetProcessing(true);
            try
            {
                if (verbose)
                    Debug.Log("ReRecorder.End: waiting for queued apply jobs to complete.");

                _lastAppliedOperationCount = await _applyChain;
                if (verbose)
                {
                    Debug.Log("ReRecorder.End: all queued apply jobs completed; applied "
                              + _lastAppliedOperationCount + " overwrite jobs.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("ReRecorder.End: apply task threw: " + ex);
            }
            finally
            {
                SetProcessing(false);
            }

            IsRerecording = false;
        }

        private void QueuePreparedApplyJobs(int recorderId, string logContext)
        {
            if (_pendingTransformJobs.Count + _pendingGenericJobs.Count + _pendingAudioJobs.Count == 0)
                return;

            List<TransformOverwriteJob> tJobs = new List<TransformOverwriteJob>(_pendingTransformJobs);
            List<GenericOverwriteJob> gJobs = new List<GenericOverwriteJob>(_pendingGenericJobs);
            List<AudioOverwriteJob> aJobs = new List<AudioOverwriteJob>(_pendingAudioJobs);
            ClearPendingJobs();

            int batchNumber = ++_applyBatchCount;
            if (verbose)
            {
                Debug.Log(logContext + ": queued apply batch " + batchNumber
                          + " with " + FormatJobCounts(tJobs.Count, gJobs.Count, aJobs.Count)
                          + ".");
            }

            Task<int> previousApplyChain = _applyChain;
            _applyChain = ApplyJobsAfterPrevious(previousApplyChain, recorderId, tJobs, gJobs, aJobs,
                logContext, verbose, batchNumber);
            SetProcessing(true);
        }

        private static async Task<int> ApplyJobsAfterPrevious(Task<int> previousApplyChain, int recorderId,
            List<TransformOverwriteJob> tJobs, List<GenericOverwriteJob> gJobs, List<AudioOverwriteJob> aJobs,
            string logContext, bool verbose, int batchNumber)
        {
            int applied = 0;
            if (verbose)
                Debug.Log(logContext + ": apply batch " + batchNumber + " waiting for previous batches.");

            try
            {
                applied = await previousApplyChain;
            }
            catch (Exception ex)
            {
                Debug.LogError(logContext + ": previous apply task threw: " + ex);
            }

            if (verbose)
            {
                Debug.Log(logContext + ": apply batch " + batchNumber + " started with "
                          + FormatJobCounts(tJobs.Count, gJobs.Count, aJobs.Count)
                          + "; " + applied + " previous overwrite jobs applied.");
            }

            try
            {
                int batchApplied = await Task.Run(() =>
                    RunApplyJobs(recorderId, tJobs, gJobs, aJobs, logContext, verbose, batchNumber));
                applied += batchApplied;
                if (verbose)
                {
                    Debug.Log(logContext + ": apply batch " + batchNumber + " finished; applied "
                              + batchApplied + " jobs in this batch, " + applied + " total.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(logContext + ": apply task threw: " + ex);
            }

            return applied;
        }

        private static int RunApplyJobs(int recorderId,
            List<TransformOverwriteJob> tJobs, List<GenericOverwriteJob> gJobs, List<AudioOverwriteJob> aJobs,
            string logContext, bool verbose, int batchNumber)
        {
            int applied = 0;
            int totalJobs = tJobs.Count + gJobs.Count + aJobs.Count;
            int currentJob = 0;
            lock (PluginLock)
            {
                foreach (TransformOverwriteJob j in tJobs)
                {
                    currentJob++;
                    if (verbose)
                    {
                        Debug.Log(logContext + ": batch " + batchNumber + " applying transform job "
                                  + currentJob + "/" + totalJobs + " for object " + j.objectId
                                  + " (" + j.count + " samples, " + FormatTimeRange(j.startTime, j.endTime)
                                  + ").");
                    }

                    if (ApplyOverwriteTransform(recorderId, Track, j.objectId, j.startTime, j.endTime,
                            j.times, j.matrix, j.info, j.count))
                        applied++;
                    else
                        Debug.LogError(logContext + ": ApplyOverwriteTransform failed for object " + j.objectId);
                }
                foreach (GenericOverwriteJob j in gJobs)
                {
                    currentJob++;
                    if (verbose)
                    {
                        Debug.Log(logContext + ": batch " + batchNumber + " applying generic job "
                                  + currentJob + "/" + totalJobs + " for object " + j.objectId
                                  + " (" + j.count + " samples, " + FormatTimeRange(j.startTime, j.endTime)
                                  + ").");
                    }

                    if (ApplyOverwriteGeneric(recorderId, Track, j.objectId, j.startTime, j.endTime,
                            j.times, j.ints, j.floats, j.chars, j.count))
                        applied++;
                    else
                        Debug.LogError(logContext + ": ApplyOverwriteGeneric failed for object " + j.objectId);
                }
                foreach (AudioOverwriteJob j in aJobs)
                {
                    currentJob++;
                    if (verbose)
                    {
                        Debug.Log(logContext + ": batch " + batchNumber + " applying audio job "
                                  + currentJob + "/" + totalJobs + " for sound " + j.soundId
                                  + " (" + j.chunkCount + " chunks, " + FormatTimeRange(j.startTime, j.endTime)
                                  + ").");
                    }

                    if (ApplyOverwriteSound(recorderId, Track, j.soundId, j.startTime, j.endTime,
                            j.samplingRate, j.channelNum, j.correspondingGameobjectId,
                            j.samples, j.lengths, j.times, j.chunkCount))
                        applied++;
                    else
                        Debug.LogError(logContext + ": ApplyOverwriteSound failed for sound " + j.soundId);
                }
            }
            return applied;
        }

        private static string FormatJobCounts(int transformJobs, int genericJobs, int audioJobs)
        {
            int totalJobs = transformJobs + genericJobs + audioJobs;
            return totalJobs + " jobs (transform=" + transformJobs
                   + ", generic=" + genericJobs
                   + ", audio=" + audioJobs + ")";
        }

        private static string FormatTimeRange(float startTime, float endTime)
        {
            return startTime.ToString("F3") + "s-" + endTime.ToString("F3") + "s";
        }

        public async void Cancel()
        {
            if (!IsBuffering)
            {
                Debug.LogWarning("ReRecorder.Cancel: not buffering");
                return;
            }

            int recorderId = _controller.RecorderID;
            Task<int> applyChain = _applyChain;
            ResetSession();

            SetProcessing(true);
            bool ok = true;
            try
            {
                if (verbose)
                    Debug.Log("ReRecorder.Cancel: waiting for queued apply jobs before undo.");

                int applied = await applyChain;
                if (verbose)
                    Debug.Log("ReRecorder.Cancel: undoing " + applied + " already applied overwrite jobs.");

                if (applied > 0)
                    ok = await Task.Run(() => RunUndoJobs(recorderId, applied));
            }
            catch (Exception ex)
            {
                Debug.LogError("ReRecorder.Cancel: " + ex);
            }
            finally
            {
                if (!ok)
                    Debug.LogError("ReRecorder.Cancel: undoing applied overwrite segments failed.");
                _lastAppliedOperationCount = 0;
                _applyChain = Task.FromResult(0);
                SetProcessing(false);
            }
        }

        public async void Undo()
        {
            if (IsBuffering)
            {
                Debug.LogError("ReRecorder.Undo: cannot undo while buffering");
                return;
            }

            if (IsProcessing)
            {
                Debug.LogError("ReRecorder.Undo: already processing");
                return;
            }

            if (_lastAppliedOperationCount == 0)
                return;

            int recorderId = _controller.RecorderID;
            int count = _lastAppliedOperationCount;

            SetProcessing(true);
            bool ok = true;
            try
            {
                ok = await Task.Run(() =>
                {
                    return RunUndoJobs(recorderId, count);
                });
                _lastAppliedOperationCount = 0;
                _applyChain = Task.FromResult(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("ReRecorder.Undo: " + ex);
            }
            finally
            {
                if (!ok)
                    Debug.LogError("ReRecorder.Undo failed.");
                SetProcessing(false);
            }
        }

        public async void Save()
        {
            if (IsBuffering)
            {
                Debug.LogError("ReRecorder.Save: cannot save while buffering");
                return;
            }

            if (IsProcessing)
            {
                Debug.LogError("ReRecorder.Save: already processing");
                return;
            }

            int recorderId = _controller.RecorderID;
            SetProcessing(true);
            bool ok = false;
            try
            {
                ok = await Task.Run(() =>
                {
                    lock (PluginLock)
                    {
                        return SaveRecordingEdits(recorderId);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogError("ReRecorder.Save: " + ex);
            }
            finally
            {
                if (!ok)
                    Debug.LogError("ReRecorder.Save failed.");
                SetProcessing(false);
            }
        }

        private void DrainCaptureSegment(float endTime, bool force = false)
        {
            if (!force && endTime <= _drainStartTime)
                return;

            if (endTime < _drainStartTime)
                return;

            _pendingTransformJobs.AddRange(PrepareTransformJobs(endTime));
            _pendingGenericJobs.AddRange(PrepareGenericJobs(endTime));
            _pendingAudioJobs.AddRange(PrepareAudioJobs(endTime));
            _drainStartTime = endTime;
        }

        private List<TransformOverwriteJob> PrepareTransformJobs(float endTime)
        {
            List<TransformOverwriteJob> jobs = new List<TransformOverwriteJob>();
            foreach (TransformRecorder source in _activeTransformRecorders)
            {
                if (source == null)
                    continue;
                List<TransformRecorder.RerecordSample> samples = source.DrainRerecordSamples();
                if (samples.Count == 0)
                    continue;

                float startTime = GetDrainStartTime(_transformDrainStartTimes, source);
                int objectId = _state.ResolveOriginalId(source.gameObject);
                float[] times = new float[samples.Count];
                float[] matrix = new float[samples.Count * 20];
                int[] info = new int[samples.Count * 2];

                for (int i = 0; i < samples.Count; ++i)
                {
                    times[i] = samples[i].time;
                    Array.Copy(samples[i].matrix, 0, matrix, i * 20, 20);
                    Array.Copy(samples[i].info, 0, info, i * 2, 2);
                }

                jobs.Add(new TransformOverwriteJob
                {
                    startTime = startTime,
                    endTime = endTime,
                    objectId = objectId,
                    times = times,
                    matrix = matrix,
                    info = info,
                    count = samples.Count
                });
                _transformDrainStartTimes[source] = endTime;
            }
            return jobs;
        }

        private List<GenericOverwriteJob> PrepareGenericJobs(float endTime)
        {
            List<GenericOverwriteJob> jobs = new List<GenericOverwriteJob>();
            foreach (GenericRecorder source in _activeGenericRecorders)
            {
                if (source == null)
                    continue;
                List<GenericRecorder.RerecordSample> samples = source.DrainRerecordSamples();
                if (samples.Count == 0)
                    continue;

                float startTime = GetDrainStartTime(_genericDrainStartTimes, source);
                int objectId = source.GetRerecordObjectId();
                float[] times = new float[samples.Count];
                int[] ints = new int[samples.Count * GenericNumberCount];
                float[] floats = new float[samples.Count * GenericNumberCount];
                byte[] chars = new byte[samples.Count * GenericCharCount];

                for (int i = 0; i < samples.Count; ++i)
                {
                    times[i] = samples[i].time;
                    Array.Copy(samples[i].ints, 0, ints, i * GenericNumberCount,
                        Mathf.Min(GenericNumberCount, samples[i].ints.Length));
                    Array.Copy(samples[i].floats, 0, floats, i * GenericNumberCount,
                        Mathf.Min(GenericNumberCount, samples[i].floats.Length));
                    Array.Copy(samples[i].chars, 0, chars, i * GenericCharCount,
                        Mathf.Min(GenericCharCount, samples[i].chars.Length));
                }

                jobs.Add(new GenericOverwriteJob
                {
                    startTime = startTime,
                    endTime = endTime,
                    objectId = objectId,
                    times = times,
                    ints = ints,
                    floats = floats,
                    chars = chars,
                    count = samples.Count
                });
                _genericDrainStartTimes[source] = endTime;
            }
            return jobs;
        }

        private List<AudioOverwriteJob> PrepareAudioJobs(float endTime)
        {
            List<AudioOverwriteJob> jobs = new List<AudioOverwriteJob>();
            foreach (AudioRecorder source in _activeAudioRecorders)
            {
                if (source == null)
                    continue;
                List<AudioRecorder.RerecordChunk> chunks = source.DrainRerecordChunks();
                if (chunks.Count == 0)
                    continue;

                float startTime = GetDrainStartTime(_audioDrainStartTimes, source);
                int totalSamples = 0;
                for (int i = 0; i < chunks.Count; ++i)
                    totalSamples += chunks[i].samples.Length;

                float[] concatenatedSamples = new float[totalSamples];
                int[] chunkLengths = new int[chunks.Count];
                float[] chunkTimes = new float[chunks.Count];
                int offset = 0;

                for (int i = 0; i < chunks.Count; ++i)
                {
                    Array.Copy(chunks[i].samples, 0, concatenatedSamples, offset, chunks[i].samples.Length);
                    chunkLengths[i] = chunks[i].samples.Length;
                    chunkTimes[i] = chunks[i].time;
                    offset += chunks[i].samples.Length;
                }

                AudioRecorder.RerecordChunk first = chunks[0];
                jobs.Add(new AudioOverwriteJob
                {
                    startTime = startTime,
                    endTime = endTime,
                    soundId = source.Id,
                    samplingRate = first.samplingRate,
                    channelNum = first.channelNum,
                    correspondingGameobjectId = first.correspondingGameobjectId,
                    samples = concatenatedSamples,
                    lengths = chunkLengths,
                    times = chunkTimes,
                    chunkCount = chunks.Count
                });
                _audioDrainStartTimes[source] = endTime;
            }
            return jobs;
        }

        private float GetDrainStartTime<TRecorder>(Dictionary<TRecorder, float> drainStartTimes, TRecorder source)
            where TRecorder : Recorder
        {
            float startTime;
            return drainStartTimes.TryGetValue(source, out startTime) ? startTime : _startTime;
        }

        private bool CollectTargetTransformRecorders(ReRecordTargets targets)
        {
            // TODO: currently transform data can only be overwritten for transforms which have been recorded in the first place
            Dictionary<int, Recorder> recorders = _controller.GetTransformRecorders();
            return CollectRecorders(recorders, targets.transformRecorderIds, _activeTransformRecorders);
        }

        private bool CollectTargetGenericRecorders(ReRecordTargets targets)
        {
            Dictionary<int, Recorder> recorders = _controller.GetGenericRecorders();
            return CollectRecorders(recorders, targets.genericRecorderIds, _activeGenericRecorders);
        }

        private static bool CollectRecorders<TRecorder>(Dictionary<int, Recorder> source,
            List<int> ids, List<TRecorder> destination) where TRecorder : Recorder
        {
            if (ids != null && ids.Count > 0)
            {
                foreach (int id in ids)
                {
                    Recorder recorder;
                    if (!source.TryGetValue(id, out recorder))
                        continue;

                    TRecorder typedRecorder = recorder as TRecorder;
                    if (typedRecorder != null)
                        destination.Add(typedRecorder);
                }
            }
            else
            {
                foreach (KeyValuePair<int, Recorder> kv in source)
                {
                    TRecorder typedRecorder = kv.Value as TRecorder;
                    if (typedRecorder != null)
                        destination.Add(typedRecorder);
                }
            }

            return true;
        }

        private bool CollectTargetAudioRecorders(ReRecordTargets targets)
        {
            Dictionary<int, Recorder> recorders = _controller.GetAudioRecorder();

            List<int> ids;
            if (targets.audioRecorderIds != null && targets.audioRecorderIds.Count > 0)
                ids = targets.audioRecorderIds;
            else
                ids = new List<int> { MicrophoneSoundId };

            bool ok = true;
            foreach (int soundId in ids)
            {
                AudioRecorder existing = null;
                Recorder rec;
                if (recorders.TryGetValue(soundId, out rec))
                    existing = rec as AudioRecorder;

                AudioRecorder target = existing != null
                    ? existing
                    : CreateTransientAudioRecorder(soundId);

                if (target == null)
                {
                    ok = false;
                    continue;
                }

                _activeAudioRecorders.Add(target);
            }

            return ok && _activeAudioRecorders.Count > 0;
        }

        private AudioRecorder CreateTransientAudioRecorder(int soundId)
        {
            if (soundId == MicrophoneSoundId)
                return CreateTransientMicrophoneRecorder(soundId);

            if (soundId == AudioListenerSoundId)
                return CreateTransientAudioListenerRecorder(soundId);

            Debug.LogError("ReRecorder: no AudioRecorder registered for soundId " + soundId);
            return null;
        }

        private AudioRecorder CreateTransientMicrophoneRecorder(int soundId)
        {
            GameObject host = new GameObject("ReRecord:Microphone:" + soundId);
            host.transform.parent = transform;
            _transientHostGameObjects.Add(host);

            MicrophoneRecorder recorder = host.AddComponent<MicrophoneRecorder>();
            recorder.SetId(soundId);
            recorder.controller = _controller;
            recorder.MarkAsPreviewRecorder();

            if (NetworkUser.LocalInstance != null && NetworkUser.LocalInstance.head != null)
                recorder.correspondingGameObject = NetworkUser.LocalInstance.head.gameObject;

            _transientAudioRecorders.Add(recorder);
            return recorder;
        }

        private AudioRecorder CreateTransientAudioListenerRecorder(int soundId)
        {
            AudioListener listener = FindAnyObjectByType<AudioListener>();
            if (listener == null)
            {
                Debug.LogError("ReRecorder: no AudioListener in scene to tap for soundId " + soundId);
                return null;
            }

            AudioListenerRecorder recorder = listener.gameObject.AddComponent<AudioListenerRecorder>();
            recorder.SetId(soundId);
            recorder.controller = _controller;
            recorder.MarkAsPreviewRecorder();

            _transientAudioRecorders.Add(recorder);
            return recorder;
        }

        private void ResetSession()
        {
            foreach (TransformRecorder r in _activeTransformRecorders)
                if (r != null) r.EndRerecordCapture();
            foreach (GenericRecorder r in _activeGenericRecorders)
                if (r != null) r.EndRerecordCapture();
            foreach (AudioRecorder r in _activeAudioRecorders)
                if (r != null) r.EndRerecordCapture();

            foreach (AudioRecorder r in _transientAudioRecorders)
                if (r != null) UnityEngine.Object.Destroy(r);

            foreach (GameObject host in _transientHostGameObjects)
                if (host != null) UnityEngine.Object.Destroy(host);

            _activeTransformRecorders.Clear();
            _activeGenericRecorders.Clear();
            _activeAudioRecorders.Clear();
            _transientAudioRecorders.Clear();
            _transientHostGameObjects.Clear();
            ClearPreparedJobs();

            bool wasBuffering = IsBuffering;
            IsBuffering = false;

            if (wasBuffering)
                RerecordingEnded?.Invoke();
        }

        private void ClearPendingJobs()
        {
            _pendingTransformJobs.Clear();
            _pendingGenericJobs.Clear();
            _pendingAudioJobs.Clear();
        }

        private void ClearPreparedJobs()
        {
            ClearPendingJobs();
            _transformDrainStartTimes.Clear();
            _genericDrainStartTimes.Clear();
            _audioDrainStartTimes.Clear();
        }

        private static bool RunUndoJobs(int recorderId, int count)
        {
            bool all = true;
            lock (PluginLock)
            {
                for (int i = 0; i < count; ++i)
                    all &= UndoLastEdit(recorderId);
            }
            return all;
        }

        private void SetProcessing(bool value)
        {
            if (IsProcessing == value)
                return;

            IsProcessing = value;
            ProcessingStateChanged?.Invoke(value);
        }
    }
}
