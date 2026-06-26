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

        protected const int intDTOSize = 10;
        protected const int floatDTOSize = 10;
        protected const int byteDTOSize = 2048;
        
        protected int[] _recIntDTO = new int[intDTOSize];
        protected float[] _recFloatDTO = new float[floatDTOSize];
        protected byte[] _recCharDTO = new byte[byteDTOSize];
        protected int[] _replayIntDTO = new int[intDTOSize];
        protected float[] _replayFloatDTO = new float[floatDTOSize];
        protected byte[] _replayCharDTO = new byte[byteDTOSize];
        
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
                result = RecordGenericAtTimestamp(controller.RecorderID, recordTime, id, _recIntDTO, _recFloatDTO,
                    _recCharDTO);

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


            fixed (float* f = _replayFloatDTO)
            {
                fixed (int* i = _replayIntDTO)
                {
                    fixed (byte* c = _replayCharDTO)
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

            int[] ints = new int[_recIntDTO.Length];
            float[] floats = new float[_recFloatDTO.Length];
            byte[] chars = new byte[_recCharDTO.Length];
            Array.Copy(_recIntDTO, ints, _recIntDTO.Length);
            Array.Copy(_recFloatDTO, floats, _recFloatDTO.Length);
            Array.Copy(_recCharDTO, chars, _recCharDTO.Length);
            var sample = new RerecordSample { time = currentReplayTime, ints = ints, floats = floats, chars = chars };
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
