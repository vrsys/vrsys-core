using System;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Recording
{
    public class MicrophoneRecorder : AudioRecorder
    {
        private IMicrophoneClipReader _microphoneClipReader;
        private float[] _audioData;
        private int _audioSamplesPerRecordStep = -1;
        private Transform _userTransform;

        // Debugging: throttled heartbeat timer and running chunk total to verify microphone capture.
        private float _lastMicLogTime;
        private int _chunksRecordedTotal;
        private float _peakSinceLog;

        public string rerecMicDeviceOverride;

        private MicrophoneClipReader _rerecReader;
        private string _rerecDevice;
        private float[] _rerecBuf;
        private int _rerecSamplingRate;
        private int _rerecChannels;
        private float _rerecNextChunkTime = -1.0f;

        public void SetMicrophoneReader(IMicrophoneClipReader reader)
        {
            // Releasing any previous reader's subscriptions (e.g. an Odin push-stream listener) before
            // swapping it out, so overriding the default Unity-microphone reader does not leak the old one.
            (_microphoneClipReader as IDisposable)?.Dispose();
            _microphoneClipReader = reader;
        }

        public void SetUserTransform(Transform transform)
        {
            _userTransform = transform;
        }


        public override void Start()
        {
            base.Start();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            (_microphoneClipReader as IDisposable)?.Dispose();
        }
        
        public override bool Record(float recordTime)
        {
            if (RecordingSamplingRate <= 0)
            {
                // Defer initialization until the reader reports a valid sampling rate. With a voice SDK such
                // as ODIN, the microphone data accessor needs a few frames before it knows its capture rate;
                // sampling it too early (SamplingRate == 0) latches a zero-length capture buffer for the whole
                // recording. Bail out (still returning true so recording continues) and retry on the next tick
                // until the reader is ready.
                int readerSamplingRate = _microphoneClipReader.SamplingRate;
                if (readerSamplingRate <= 0)
                    return true;

                RecordingChannelNum = Mathf.Max(1, _microphoneClipReader.Channels);
                RecordingSamplingRate = readerSamplingRate;
                _audioSamplesPerRecordStep = Mathf.Max(1, RecordingSamplingRate / SoundRecordingStepsPerSecond);

                _audioData = new float[_audioSamplesPerRecordStep];
                FirstRecord = true;

                if (controller.debugLogs)
                    ExtendedLogger.LogInfo(GetType().Name, "[MicrophoneRecorder id=" + id + "] Initialized capture: samplingRate=" +
                              RecordingSamplingRate + ", channels=" + RecordingChannelNum + ", samplesPerStep=" +
                              _audioSamplesPerRecordStep + ", bufferLength=" + _audioData.Length + ".", this);
            }

            float readAudio = 1;
            bool recordedData = false;
            int chunksThisCall = 0;

            float recordedTime = 0.0f;
            
            while (readAudio > 0)
            {
                readAudio = _microphoneClipReader.Read(_audioData);
                if (readAudio >= 0)
                {
                    // Peak of the samples actually handed to the native recorder, so the heartbeat can show
                    // whether the recorded data carries signal or is silence.
                    for (int s = 0; s < _audioData.Length; ++s)
                    {
                        float amp = _audioData[s] < 0f ? -_audioData[s] : _audioData[s];
                        if (amp > _peakSinceLog)
                            _peakSinceLog = amp;
                    }

                    if (RecordingTime < 0 && FirstRecord)
                    {
                        RecordingTime = recordTime - readAudio / SoundRecordingStepsPerSecond;
                        FirstRecord = false;
                        if(controller.debugLogs)
                            ExtendedLogger.LogInfo(GetType().Name, "Initial microphone recording time: " + RecordingTime, this);
                    }

                    if (RecordingTime >= 0.0f)
                    {
                        _goIDDTO[0] = gameObject.GetInstanceID();

                        bool result = RecordSoundDataWithGOInfoAtTimestamp(controller.RecorderID, _audioData, _audioSamplesPerRecordStep, RecordingSamplingRate, 0, RecordingChannelNum, _goIDDTO[0], RecordingTime, id);

                        if (!result)
                        {
                            if (controller.debugLogs)
                                ExtendedLogger.LogWarning(GetType().Name, "[MicrophoneRecorder id=" + id + "] RecordSoundDataWithGOInfoAtTimestamp " +
                                                 "returned false at time " + RecordingTime + "; stopping capture.", this);
                            return false;
                        }

                        chunksThisCall++;
                    }

                    RecordingTime += _audioSamplesPerRecordStep / (float)RecordingSamplingRate;
                    recordedTime = _audioSamplesPerRecordStep / (float)RecordingSamplingRate;

                    recordedData = true;
                }
            }

            _chunksRecordedTotal += chunksThisCall;
            if (controller.debugLogs && Time.realtimeSinceStartup - _lastMicLogTime >= 1.0f)
            {
                _lastMicLogTime = Time.realtimeSinceStartup;
                ExtendedLogger.LogInfo(GetType().Name, "[MicrophoneRecorder id=" + id + "] Heartbeat: chunksThisCall=" + chunksThisCall +
                          ", totalChunks=" + _chunksRecordedTotal + ", recordingTime=" + RecordingTime.ToString("F2") +
                          ", bufferLength=" + (_audioData != null ? _audioData.Length : 0) +
                          ", peakAmplitude=" + _peakSinceLog.ToString("F4") +
                          ". (peakAmplitude ~0 means the data being recorded is silent.)", this);
                _peakSinceLog = 0f;
            }

            if (recordedData && Mathf.Abs(recordTime - (RecordingTime + recordedTime)) > 0.5f)
            {
                if(controller.debugLogs)
                    ExtendedLogger.LogError(GetType().Name, "Error! Microphone time not aligned. Difference: " + (recordTime - (RecordingTime + recordedTime)), this);
            }

            return true;
        }

        public override void BeginRerecordCapture()
        {
            base.BeginRerecordCapture();
            _rerecNextChunkTime = -1.0f;

            _rerecDevice = ResolveRerecMicDevice();
            if (string.IsNullOrEmpty(_rerecDevice))
            {
                ExtendedLogger.LogError(GetType().Name, "MicrophoneRecorder.BeginRerecordCapture: no microphone device available", this);
                return;
            }

            AudioClip clip = Microphone.Start(_rerecDevice, true, 10, AudioSettings.outputSampleRate);
            if (clip == null)
            {
                ExtendedLogger.LogError(GetType().Name, "MicrophoneRecorder.BeginRerecordCapture: could not start microphone " + _rerecDevice, this);
                _rerecDevice = null;
                return;
            }

            _rerecReader = new MicrophoneClipReader(clip, _rerecDevice);
            _rerecSamplingRate = _rerecReader.SamplingRate;
            _rerecChannels = Mathf.Max(1, _rerecReader.Channels);
            int samplesPerStep = Mathf.Max(1,
                _rerecSamplingRate / Mathf.Max(1, controller.audioRecordingStepsPerSecond)) * _rerecChannels;
            _rerecBuf = new float[samplesPerStep];
        }

        public override void TickRerecordCapture(float currentReplayTime)
        {
            if (_rerecReader == null)
                return;

            if (_rerecNextChunkTime < 0.0f)
                _rerecNextChunkTime = currentReplayTime;

            float read = 1.0f;
            while (read > 0.0f)
            {
                read = _rerecReader.Read(_rerecBuf);
                if (read < 0.0f)
                    break;

                float[] samples = new float[_rerecBuf.Length];
                Array.Copy(_rerecBuf, samples, _rerecBuf.Length);
                EmitRerecordChunk(new RerecordChunk
                {
                    time = _rerecNextChunkTime,
                    samples = samples,
                    samplingRate = _rerecSamplingRate,
                    channelNum = _rerecChannels,
                    correspondingGameobjectId = _rerecCorrespondingGoId
                });
                _rerecNextChunkTime += samples.Length / (float)(_rerecSamplingRate * _rerecChannels);
            }
        }

        public override void EndRerecordCapture()
        {
            base.EndRerecordCapture();
            if (!string.IsNullOrEmpty(_rerecDevice) && Microphone.IsRecording(_rerecDevice))
                Microphone.End(_rerecDevice);
            _rerecReader = null;
            _rerecDevice = null;
            _rerecBuf = null;
        }

        private string ResolveRerecMicDevice()
        {
            if (!string.IsNullOrEmpty(rerecMicDeviceOverride))
                return rerecMicDeviceOverride;
            if (Microphone.devices.Length > 0)
                return Microphone.devices[0];
            return null;
        }
    }
}