using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using VRSYS.Core.Logging;
using VRSYS.Recording;

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

        [DllImport("RecordingPlugin")]
        private static extern bool RegisterGenericDescription(int recorderId, int id, byte[] description,
            int descriptionLength);

        [DllImport("RecordingPlugin")]
        private static extern int GetGenericIntArraySize();

        [DllImport("RecordingPlugin")]
        private static extern int GetGenericFloatArraySize();

        [DllImport("RecordingPlugin")]
        private static extern int GetGenericCharArraySize();

        protected const int intDTOSize = 10;
        protected const int floatDTOSize = 10;
        protected const int byteDTOSize = 2048;

        private static bool _dtoSizesChecked = false;
        private static bool _descriptionEndpointMissingLogged = false;
        
        protected int[] _recIntDTO = new int[intDTOSize];
        protected float[] _recFloatDTO = new float[floatDTOSize];
        protected byte[] _recCharDTO = new byte[byteDTOSize];
        protected int[] _replayIntDTO = new int[intDTOSize];
        protected float[] _replayFloatDTO = new float[floatDTOSize];
        protected byte[] _replayCharDTO = new byte[byteDTOSize];
        
        protected bool replay = false;

        private List<RerecordSample> _rerecBuffer = new List<RerecordSample>();
        private readonly object _rerecSync = new object();

        public override void Start()
        {
            base.Start();
            VerifyDTOSizes();
        }

        // The C# DTO array sizes are hard-coded constants; warn once if they drift from the plugin's layout.
        private static void VerifyDTOSizes()
        {
            if (_dtoSizesChecked)
                return;
            _dtoSizesChecked = true;

            int pluginInt = GetGenericIntArraySize();
            int pluginFloat = GetGenericFloatArraySize();
            int pluginChar = GetGenericCharArraySize();

            if (pluginInt != intDTOSize || pluginFloat != floatDTOSize || pluginChar != byteDTOSize)
            {
                ExtendedLogger.LogWarning(nameof(GenericRecorder),
                    $"Generic DTO size mismatch with plugin! C# (int/float/char): {intDTOSize}/{floatDTOSize}/{byteDTOSize}, " +
                    $"plugin: {pluginInt}/{pluginFloat}/{pluginChar}. Recorded/replayed generic data may be corrupted.");
            }
        }

        /// <summary>
        /// Stores a free-text description of what this recorder's generic id records (e.g. how the int, float and
        /// char slots are used) in the recording's meta information (.recordmeta). Must be called while recording,
        /// after <see cref="Recorder.id"/> has its final value. Registering again replaces the description.
        /// </summary>
        protected bool RegisterDescription(string description)
        {
            if (controller == null)
                return false;

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(description ?? string.Empty);
            try
            {
                return RegisterGenericDescription(controller.RecorderID, id, bytes, bytes.Length);
            }
            catch (EntryPointNotFoundException)
            {
                // Plugin binary built before RegisterGenericDescription existed: recording continues without it.
                if (!_descriptionEndpointMissingLogged)
                {
                    _descriptionEndpointMissingLogged = true;
                    ExtendedLogger.LogWarning(nameof(GenericRecorder),
                        "RecordingPlugin has no RegisterGenericDescription endpoint; rebuild the plugin to store generic data descriptions.");
                }
                return false;
            }
        }

        protected virtual bool FillGenericData()
        {
            return false;
        }

        protected virtual void ClearGenericData()
        {
            Array.Clear(_recIntDTO, 0, _recIntDTO.Length);
            Array.Clear(_recFloatDTO, 0, _recFloatDTO.Length);
            Array.Clear(_recCharDTO, 0, _recCharDTO.Length);
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

                if(result)
                    ClearGenericData();
                
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

            EmitRerecordSampleFromDTO(currentReplayTime);
        }

        protected void EmitRerecordSampleFromDTO(float time)
        {
            int[] ints = new int[_recIntDTO.Length];
            float[] floats = new float[_recFloatDTO.Length];
            byte[] chars = new byte[_recCharDTO.Length];
            Array.Copy(_recIntDTO, ints, _recIntDTO.Length);
            Array.Copy(_recFloatDTO, floats, _recFloatDTO.Length);
            Array.Copy(_recCharDTO, chars, _recCharDTO.Length);
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
