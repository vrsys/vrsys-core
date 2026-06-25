// VRSYS plugin of Virtual Reality and Visualization Group (Bauhaus-University Weimar)
//-----------------------------------------------------------------
//   Authors:        Anton Lammert
//   Date:           2026
//-----------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using VRSYS.Core.Chat.Odin;

namespace VRSYS.Recording
{
    /// <summary>
    /// <see cref="IMicrophoneClipReader"/> backed by ODIN's capture stream instead of a Unity
    /// <c>Microphone</c> clip. ODIN pushes raw PCM buffers through
    /// <see cref="OdinMicrophoneDataAccessor.OnMicrophoneData"/>; this adapter ring-buffers those pushed
    /// samples and serves them to the <see cref="MicrophoneRecorder"/> through the pull-based
    /// <see cref="Read"/> contract.
    ///
    /// Lives in the optional <c>vrsys.recording.odin</c> assembly, which only compiles when the VRSYS
    /// ODIN chat package is present, so the core recording package stays voice-SDK free.
    /// </summary>
    public class OdinMicrophoneClipReader : IMicrophoneClipReader, IDisposable
    {
        private readonly OdinMicrophoneDataAccessor _accessor;
        private readonly Queue<float> _samples = new Queue<float>();
        private readonly object _lock = new object();

        // Hard cap on buffered audio (~5 s) so a stalled or slow recorder can never grow the queue without
        // bound; oldest samples are dropped first.
        private int _maxBufferedSamples;

        // Debugging: one-time flags so verifying the ODIN capture flow does not spam the log.
        private bool _loggedFirstData;
        private bool _loggedFirstServe;
        private bool _loggedEmptyBufferWarning;
        private bool _loggedStarvation;
        private int _buffersReceived;

        public int Channels => _accessor != null ? Mathf.Max(1, _accessor.Channels) : 1;
        public int SamplingRate => _accessor != null ? _accessor.SamplingRate : 0;

        public OdinMicrophoneClipReader(OdinMicrophoneDataAccessor accessor)
        {
            _accessor = accessor;
            if (_accessor != null)
                _accessor.OnMicrophoneData.AddListener(OnMicrophoneData);
        }

        private void OnMicrophoneData(float[] buffer, int position)
        {
            if (buffer == null || buffer.Length == 0)
                return;

            // Peak of the buffer ODIN delivers, to confirm whether the source actually carries signal or is
            // silence (computed outside the lock, read-only over the incoming array).
            float peak = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float amp = buffer[i] < 0f ? -buffer[i] : buffer[i];
                if (amp > peak)
                    peak = amp;
            }

            int queuedAfter;
            bool logFirst = false;
            bool logPeriodic = false;
            lock (_lock)
            {
                if (_maxBufferedSamples <= 0 && SamplingRate > 0)
                    _maxBufferedSamples = SamplingRate * Channels * 5;

                // Copy out by value: ODIN cycles through a small pool of reusable buffers, so the incoming
                // array must not be retained by reference.
                foreach (float sample in buffer)
                    _samples.Enqueue(sample);

                if (_maxBufferedSamples > 0)
                    while (_samples.Count > _maxBufferedSamples)
                        _samples.Dequeue();

                queuedAfter = _samples.Count;
                _buffersReceived++;
                if (!_loggedFirstData)
                {
                    _loggedFirstData = true;
                    logFirst = true;
                }
                else if (_buffersReceived % 200 == 0)
                {
                    logPeriodic = true;
                }
            }

            // Logged outside the lock so the (possibly audio-thread) ODIN callback releases it quickly.
            if (logFirst)
                Debug.Log("[OdinMicrophoneClipReader] First ODIN audio buffer received: bufferLength=" +
                          buffer.Length + ", samplingRate=" + SamplingRate + ", channels=" + Channels +
                          ", queuedSamples=" + queuedAfter + ", peakAmplitude=" + peak.ToString("F4") +
                          ". (peakAmplitude ~0 means ODIN is delivering silence to the recorder.)");
            else if (logPeriodic)
                Debug.Log("[OdinMicrophoneClipReader] ODIN audio level: buffersReceived=" + _buffersReceived +
                          ", peakAmplitude=" + peak.ToString("F4") + ", queuedSamples=" + queuedAfter + ".");
        }

        public float Read(float[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                if (!_loggedEmptyBufferWarning)
                {
                    _loggedEmptyBufferWarning = true;
                    Debug.LogWarning("[OdinMicrophoneClipReader] Read was called with a zero-length buffer. The " +
                                     "MicrophoneRecorder initialized its sampling rate before ODIN reported one " +
                                     "(SamplingRate=0), so no microphone audio can be captured.");
                }
                return -1.0f;
            }

            lock (_lock)
            {
                if (_samples.Count < buffer.Length)
                {
                    if (!_loggedStarvation)
                    {
                        _loggedStarvation = true;
                        Debug.Log("[OdinMicrophoneClipReader] Recorder requested " + buffer.Length +
                                  " samples but only " + _samples.Count + " are buffered; waiting for more ODIN " +
                                  "audio. (Logged once.)");
                    }
                    return -1.0f;
                }

                // Number of buffer-fulls currently queued, mirroring MicrophoneClipReader.Read so the
                // recorder back-dates the first sample by the existing backlog.
                float available = _samples.Count / (float)buffer.Length;
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = _samples.Dequeue();

                if (!_loggedFirstServe)
                {
                    _loggedFirstServe = true;
                    Debug.Log("[OdinMicrophoneClipReader] First audio served to the recorder: requested=" +
                              buffer.Length + ", bufferFullsAvailable=" + available.ToString("F2") + ".");
                }
                return available;
            }
        }

        public void Dispose()
        {
            if (_accessor != null)
                _accessor.OnMicrophoneData.RemoveListener(OnMicrophoneData);
            lock (_lock)
                _samples.Clear();
        }
    }
}
