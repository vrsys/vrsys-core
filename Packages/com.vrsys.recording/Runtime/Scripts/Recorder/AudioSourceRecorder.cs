using System;
using UnityEngine;
using VRSYS.Core.Networking;

namespace VRSYS.Scripts.Recording
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioSourceRecorder : AudioRecorder
    {
        private long recordStartTime = -1;
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
            if (RecordingTime < 0.0f)
            {
                RecordingTime = recordTime;
                recordStartTime = DateTime.Now.Ticks;
                FirstRecord = true;
            }

            if (Mathf.Abs(recordTime - RecordingTime) > 0.1f)
            {
                if (controller.debugLogs)
                {
                    //Debug.LogError("Error! Audio Listener time not aligned. Difference: " + (recordTime - RecordingTime));
                    //Debug.LogError("This problem might be caused because the sampling rate of the audio clip is not well suited for the sampling rate of the audio listener!");
                }

                RecordingTime = recordTime;
            }

            return true;
        }

        public override void Update()
        {
            base.Update();
            _goIDDTO[0] = gameObject.GetInstanceID();
            _isPlaying = source.isPlaying;
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
            
            if (FirstRecord)
            {
                RecordingTime += (float)(new TimeSpan(DateTime.Now.Ticks - recordStartTime)).TotalSeconds;
                FirstRecord = false;
            }
            
            if (RecordingTime >= 0.0f)
            {
                //float duration = (data.Length / RecordingChannelNum) / (float)RecordingSamplingRate;
                float recordingTimeOfChunk = RecordingTime - effectiveSampleDuration;
                if (recordingTimeOfChunk < 0)
                {
                    if(controller.debugLogs)
                        Debug.LogError("Error! Sound recording time should not be negative!");
                    recordingTimeOfChunk = 0.0f;
                }

                //Debug.Log("Recording sound for id: " + id +", start time: " + recordingTimeOfChunk + ", end time: " + RecordingTime);

                RecordSoundDataWithGOInfoAtTimestamp(controller.RecorderID, data, effectiveSampleCount * channels,
                    RecordingSamplingRate, 0, RecordingChannelNum, _goIDDTO[0], recordingTimeOfChunk, id);
                RecordingTime += effectiveSampleDuration;
            }
        }
    }
}
