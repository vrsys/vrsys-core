using System;
using System.Runtime.InteropServices;
using UnityEngine;
using VRSYS.Core.Networking;

namespace VRSYS.Scripts.Recording
{
    public class AudioListenerRecorder : AudioRecorder
    {
        private long recordStartTime = -1;

        private void Awake()
        {
            EnsureRecordingSamplingRate();
        }
        
        public override void Start()
        {
            base.Start();
            EnsureRecordingSamplingRate();
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
                if(controller.debugLogs)
                    Debug.LogError("Error! Audio Listener time not aligned. Difference: " + (recordTime - RecordingTime));
                RecordingTime = recordTime;
            }

            return true;
        }

        public override void Update()
        {
            base.Update();
            _goIDDTO[0] = gameObject.GetInstanceID();
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            bool isRecording = controller != null && controller.CurrentState == State.Recording;

            if (inRerecordingMode)
                QueueRerecordRawChunk(data, channels);

            if (!isRecording)
                return;

            if (RecordingChannelNum == -1)
                RecordingChannelNum = channels;

            if (RecordingSamplingRate <= 0 || RecordingChannelNum <= 0 || data == null || data.Length <= 0)
                return;

            if (FirstRecord)
            {
                RecordingTime += (float)(new TimeSpan(DateTime.Now.Ticks - recordStartTime)).TotalSeconds;
                FirstRecord = false;
            }

            if (RecordingTime >= 0.0f)
            {
                float duration = (data.Length / RecordingChannelNum) / (float)RecordingSamplingRate;
                if (duration <= 0.0f || float.IsNaN(duration) || float.IsInfinity(duration))
                    return;

                float recordingTimeOfChunk = RecordingTime - duration;
                if (recordingTimeOfChunk < 0)
                {
                    if(controller.debugLogs)
                        Debug.LogError("Error! Sound recording time should not be negative!");
                    recordingTimeOfChunk = 0.0f;
                    RecordingTime = duration;
                }

                RecordSoundDataWithGOInfoAtTimestamp(controller.RecorderID, data, data.Length, RecordingSamplingRate, 0, RecordingChannelNum, _goIDDTO[0], recordingTimeOfChunk, id);
                    RecordingTime += duration;
            }
        }

        public override void TickRerecordCapture(float currentReplayTime)
        {
            DrainPendingChunksToRerecBuffer(currentReplayTime, AudioSettings.outputSampleRate);
        }
    }
}
