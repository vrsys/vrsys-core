using System;
using UnityEngine;
using VRSYS.Core.Networking;
using VRSYS.Core.Logging;

namespace VRSYS.Recording
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioSourceRecorder : AudioRecorder
    {
        private AudioSource source;
        private bool _isPlaying;
        private int frequency;

        private void Awake()
        {
            EnsureRecordingSamplingRate();
        }
        
        public override void Start()
        {
            base.Start();

            source = GetComponent<AudioSource>();
            EnsureRecordingSamplingRate();
            if(source != null && source.clip != null)
                frequency = source.clip.frequency;
        }

        private void EnsureRecordingSamplingRate()
        {
            if(RecordingSamplingRate <= 0)
                RecordingSamplingRate = AudioSettings.outputSampleRate;
        }
        
        public override bool Record(float recordTime)
        {
            // Audio callbacks timestamp their own blocks using the controller's DSP clock.
            return true;
        }

        public override void Update()
        {
            base.Update();
            _goIDDTO[0] = gameObject.GetInstanceID();
            _isPlaying = source != null && source.isPlaying;
        }

        public override void TickRerecordCapture(float currentReplayTime)
        {
            DrainPendingChunksToRerecBuffer(currentReplayTime, AudioSettings.outputSampleRate);
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            bool isRecording = _isPlaying && controller != null && controller.CurrentState == State.Recording;

            if (inRerecordingMode)
                QueueRerecordRawChunk(data, channels);

            if (!isRecording)
                return;

            if (RecordingChannelNum == -1)
                RecordingChannelNum = channels;

            if (RecordingSamplingRate <= 0 || RecordingChannelNum <= 0 || data == null || data.Length <= 0)
                return;

            // Ratio of the actual sampling rate of the audio source to the sampling rate of the audio listener
            float samplingRateRatio = 1.0f;//frequency / (float)RecordingSamplingRate;

            // Calculate 'effective' new sample count based on the sampling rate ratio
            int effectiveSampleCount = (int)(data.Length * samplingRateRatio) / channels;

            // Calculate the duration of these effective samples
            float effectiveSampleDuration = effectiveSampleCount / (float)RecordingSamplingRate;
            if (effectiveSampleDuration <= 0.0f || float.IsNaN(effectiveSampleDuration) || float.IsInfinity(effectiveSampleDuration))
                return;
            
            // dspTime identifies the current block; no shared sample counter or wall clock is needed.
            if (!ShouldRecordChunk(data, effectiveSampleCount * channels))
                return;

            float recordingTimeOfChunk = Mathf.Max(0.0f, controller.GetRecordingTime());
            RecordSoundDataWithGOInfoAtTimestamp(controller.RecorderID, data, effectiveSampleCount * channels,
                RecordingSamplingRate, 0, RecordingChannelNum, _goIDDTO[0], recordingTimeOfChunk, id);
        }
    }
}
