using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Recording
{
    public class GenericRecorder : Recorder
    {
        public struct RerecordSample
        {
            public float time;
            public int[] ints;
            public float[] floats;
            public byte[] chars;
        }

        [DllImport("RecordingPlugin")]
        protected static extern bool RecordGenericAtTimestamp(int recorderId, float time, int id, int[] intArray,
            float[] floatArray, byte[] charArray);

        [DllImport("RecordingPlugin")]
        private static extern bool GetGenericAtTime(int recorderId, float time, int id, IntPtr intArray,
            IntPtr floatArray, IntPtr charArray);

        protected int[] _intDTO = new int[10];
        protected float[] _floatDTO = new float[10];
        protected byte[] _charDTO = new byte[2048];
        protected bool replay = false;

        private List<RerecordSample> _rerecBuffer = new List<RerecordSample>();
        private readonly object _rerecSync = new object();

        protected virtual bool FillGenericData()
        {
            return false;
        }

        protected virtual void ProcessReplayData(float replayTime)
        {
        }

        public override bool Record(float recordTime)
        {
            bool result = FillGenericData();

            if (result)
            {
                result = RecordGenericAtTimestamp(controller.RecorderID, recordTime, id, _intDTO, _floatDTO,
                    _charDTO);

                if (!result && controller.debugLogs)
                    ExtendedLogger.LogInfo(GetType().Name, "Could not record arbitrary data with id: " + id, this);

                return result;
            }
            else
            {
                return true;
            }
        }

        public override unsafe bool Replay(float replayTime)
        {
            if (inRerecordingMode)
                return true;

            replay = true;


            fixed (float* f = _floatDTO)
            {
                fixed (int* i = _intDTO)
                {
                    fixed (byte* c = _charDTO)
                    {
                        bool result = GetGenericAtTime(controller.RecorderID, replayTime, id, (IntPtr)i,
                            (IntPtr)f, (IntPtr)c);

                        if (!result)
                        {
                            if (controller.debugLogs)
                                ExtendedLogger.LogInfo(GetType().Name, "Could not replay arbitrary data with id: " + id + " for object with name: " + gameObject.name, this);
                            return false;
                        }

                        ProcessReplayData(replayTime);

                        return true;
                    }
                }
            }
        }

        public override bool Preview(float previewTime)
        {
            return false;
        }

        public virtual int GetRerecordObjectId()
        {
            return controller.recorderState.ResolveOriginalId(gameObject);
        }

        public override void BeginRerecordCapture()
        {
            base.BeginRerecordCapture();
            lock (_rerecSync)
                _rerecBuffer.Clear();
        }

        public override void TickRerecordCapture(float currentReplayTime)
        {
            if (!FillGenericData())
                return;

            EmitRerecordSampleFromDTO(currentReplayTime);
        }

        protected void EmitRerecordSampleFromDTO(float time)
        {
            int[] ints = new int[_intDTO.Length];
            float[] floats = new float[_floatDTO.Length];
            byte[] chars = new byte[_charDTO.Length];
            Array.Copy(_intDTO, ints, _intDTO.Length);
            Array.Copy(_floatDTO, floats, _floatDTO.Length);
            Array.Copy(_charDTO, chars, _charDTO.Length);
            EmitRerecordSample(new RerecordSample { time = time, ints = ints, floats = floats, chars = chars });
        }

        protected void EmitRerecordSample(RerecordSample sample)
        {
            lock (_rerecSync)
                _rerecBuffer.Add(sample);
        }

        public List<RerecordSample> DrainRerecordSamples()
        {
            lock (_rerecSync)
            {
                List<RerecordSample> drained = _rerecBuffer;
                _rerecBuffer = new List<RerecordSample>();
                return drained;
            }
        }
    }
}
