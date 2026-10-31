using System;
using System.Runtime.InteropServices;
using UnityEngine;
using VRSYS.Core.Networking;
using VRSYS.Core.Logging;

namespace VRSYS.Recording
{
    public class AudioListenerRecorder : AudioRecorder
    {

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
            // Audio callbacks timestamp their own blocks using the controller's DSP clock.
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

            // dspTime identifies the current block; no shared sample counter or wall clock is needed.
            if (!ShouldRecordChunk(data, data.Length))
                return;

            float recordingTimeOfChunk = Mathf.Max(0.0f, controller.GetRecordingTime());
            RecordSoundDataWithGOInfoAtTimestamp(controller.RecorderID, data, data.Length, RecordingSamplingRate, 0, RecordingChannelNum, _goIDDTO[0], recordingTimeOfChunk, id);
        }

        public override void TickRerecordCapture(float currentReplayTime)
        {
            DrainPendingChunksToRerecBuffer(currentReplayTime, AudioSettings.outputSampleRate);
        }
    }
}
