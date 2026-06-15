using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace VRSYS.Scripts.Recording
{
    public class AudioRecorder : Recorder
    {
        
        [DllImport("RecordingPlugin")]
        protected static extern bool RecordSoundDataAtTimestamp(int recorderId, float[] soundData, int soundDataLength,
            int samplingRate, int startIndex, int channelNum, float timeStamp, int soundOrigin);

        [DllImport("RecordingPlugin")]
        protected static extern bool RecordSoundDataWithGOInfoAtTimestamp(int recorderId, float[] soundData,
            int soundDataLength,
            int samplingRate, int startIndex, int channelNum, int correspondingGOID, float timeStamp, int soundOrigin);

        [DllImport("RecordingPlugin")]
        private static extern int GetSoundChunkForTime(int recorderId, int soundOrigin, float timeStamp,
            IntPtr soundData);

        [DllImport("RecordingPlugin")]
        private static extern int GetSoundChunkAndGOInformationForTime(int recorderId, int soundOrigin, float timeStamp,
            IntPtr soundData, IntPtr correspondingGOID);

        [DllImport("RecordingPlugin")]
        private static extern int GetSamplingRate(int recorderId, int soundOrigin);

        [DllImport("RecordingPlugin")]
        private static extern int GetChannelNum(int recorderId, int soundOrigin);
        
        public struct RerecordChunk
        {
            public float time;
            public float[] samples;
            public int samplingRate;
            public int channelNum;
            public int correspondingGameobjectId;
        }

        public GameObject correspondingGameObject;

        private List<RerecordChunk> _rerecBuffer = new List<RerecordChunk>();
        private readonly object _rerecSync = new object();

        private readonly object _pendingSync = new object();
        private List<float[]> _pendingChunks = new List<float[]>();
        private List<int> _pendingChannels = new List<int>();
        private float _rerecNextChunkTime = -1.0f;
        protected int _rerecCorrespondingGoId;

        public override void BeginRerecordCapture()
        {
            base.BeginRerecordCapture();
            lock (_rerecSync)
                _rerecBuffer.Clear();
            lock (_pendingSync)
            {
                _pendingChunks.Clear();
                _pendingChannels.Clear();
            }
            _rerecNextChunkTime = -1.0f;

            GameObject go = correspondingGameObject != null ? correspondingGameObject : gameObject;
            _rerecCorrespondingGoId = controller.recorderState.ResolveOriginalId(go);
        }

        protected void EmitRerecordChunk(RerecordChunk chunk)
        {
            lock (_rerecSync)
                _rerecBuffer.Add(chunk);
        }

        protected void QueueRerecordRawChunk(float[] data, int channels)
        {
            if (!inRerecordingMode)
                return;
            float[] copy = new float[data.Length];
            Array.Copy(data, copy, data.Length);
            lock (_pendingSync)
            {
                _pendingChunks.Add(copy);
                _pendingChannels.Add(channels);
            }
        }

        protected void DrainPendingChunksToRerecBuffer(float currentReplayTime, int samplingRate)
        {
            if (_rerecNextChunkTime < 0.0f)
                _rerecNextChunkTime = currentReplayTime;

            List<float[]> chunks;
            List<int> channels;
            lock (_pendingSync)
            {
                if (_pendingChunks.Count == 0)
                    return;
                chunks = _pendingChunks;
                channels = _pendingChannels;
                _pendingChunks = new List<float[]>();
                _pendingChannels = new List<int>();
            }

            for (int i = 0; i < chunks.Count; ++i)
            {
                float[] samples = chunks[i];
                int ch = Mathf.Max(1, channels[i]);
                EmitRerecordChunk(new RerecordChunk
                {
                    time = _rerecNextChunkTime,
                    samples = samples,
                    samplingRate = samplingRate,
                    channelNum = ch,
                    correspondingGameobjectId = _rerecCorrespondingGoId
                });
                _rerecNextChunkTime += samples.Length / (float)(samplingRate * ch);
            }
        }

        public List<RerecordChunk> DrainRerecordChunks()
        {
            lock (_rerecSync)
            {
                List<RerecordChunk> drained = _rerecBuffer;
                _rerecBuffer = new List<RerecordChunk>();
                return drained;
            }
        }

        private AudioClip _clip;
        private AudioSource _source;
        private float globalRecordingStartOffset = 0.0f;

        protected int RecordingSamplingRate = -1;
        protected int RecordingChannelNum = -1;
        protected int SoundRecordingStepsPerSecond = 10;
        protected float RecordingTime = -1.0f;
        protected bool FirstRecord = false;

        private int _playbackSamplingRate = -1;
        private int _playbackChannelNum = -1;
        
        private float[][] _replayAudioData =
        {
            new float[1000],
            new float[512],
            new float[1024],
            new float[2048],
            new float[4096],
            new float[4800]
        };
        private bool _initializedReplay = false;
        private float _nextSoundReplayTime;
        private int _audioWritePos;
        private float[] _soundDTO = new float[4800];
        protected int[] _goIDDTO = new int[1];
        private float[] _emptySound = new float[1024];

        private GameObject _audioSourceGo;
        private GameObject _targetGo;

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (_source != null)
                Destroy(_source);
        }

        protected void InitializeReplayData()
        {
            int recorderId = controller.RecorderID;
            
            _playbackSamplingRate = GetSamplingRate(recorderId, id);
            
            _playbackChannelNum = GetChannelNum(recorderId, id);

            if(controller.debugLogs)
                Debug.Log("Playback sampling rate: " + _playbackSamplingRate + ", Playback channel num: " + _playbackChannelNum);

            if (_playbackSamplingRate < 0)
                _playbackSamplingRate = 16000;

            if (_playbackChannelNum < 1)
                _playbackChannelNum = 1;

            _clip = AudioClip.Create("AudioReplay" + id, _playbackSamplingRate * 10, _playbackChannelNum,
                _playbackSamplingRate, false);
            if (_source == null)
            {
                _audioSourceGo = gameObject;
                _audioSourceGo.transform.parent = controller.gameObject.transform;
                _source = _audioSourceGo.GetComponent<AudioSource>();
                if (_source == null)
                    _source = _audioSourceGo.AddComponent<AudioSource>();
            }

            _source.clip = _clip;
            _source.loop = true;
            _initializedReplay = true;

            _source.spatialBlend = 1f;
            _source.spatialize = true;
            _source.volume = 1f;

            SoundRecordingStepsPerSecond = _playbackSamplingRate / 1024;
        }

        unsafe public override bool Replay(float replayTime)
        {
            if (!_initializedReplay)
                InitializeReplayData();

            if (_targetGo != null)
            {
                _audioSourceGo.transform.position = _targetGo.transform.position;
                _audioSourceGo.transform.rotation = _targetGo.transform.rotation;
            }
            
            if (replayTime - _lastReplayTime == 0.0f)
                _source.Pause();
            else if (!_source.isPlaying)
                _source.Play();

            if (Mathf.Abs(replayTime - _lastReplayTime) >= 0.5f)
            {
                _audioWritePos = (int)((_source.time / _clip.length) * _clip.samples);
                _nextSoundReplayTime = replayTime;
            }
            
            bool newData = false;
            if (replayTime >= _nextSoundReplayTime - 1.0f)
            {
                float loadTime = _nextSoundReplayTime;

                fixed (float* p = _soundDTO)
                {
                    fixed (int* u = _goIDDTO)
                    {
                        // load the audio data for the next 3 seconds and insert it into the audio clip
                        for (int i = 0; i < 3 * SoundRecordingStepsPerSecond; i++)
                        {
                            int result = -1;

                            if (loadTime < controller.GetRecordingDuration())
                            {
                                result = GetSoundChunkAndGOInformationForTime(controller.RecorderID, id, loadTime,
                                    (IntPtr)p, (IntPtr)u);

                                if (result < 1)
                                {
                                    if(controller.debugLogs)
                                        Debug.LogWarning(
                                        "Could not get new sound data! Reason: GetSoundChunkForTime returned a negative value for sound with id: " +
                                        id + " for time: " + loadTime);
                                    break;
                                }

                                if (controller.recorderState.originalIdGameObjects.ContainsKey(_goIDDTO[0]))
                                {
                                    _targetGo = controller.recorderState.originalIdGameObjects[_goIDDTO[0]];
                                }

                                bool setData = false;

                                if (result == 1000)
                                {
                                    Debug.LogWarning("Received empty data for sound with id: " + id);
                                    setData = _clip.SetData(_emptySound, _audioWritePos % _clip.samples);
                                }
                                else
                                {
                                    int index = -1;
                                    for (int j = 0; j < _replayAudioData.Length; ++j)
                                        if (_replayAudioData[j].Length == result)
                                            index = j;

                                    if (index == -1)
                                    {
                                        float[] tmpArray = new float[result];
                                        if(controller.debugLogs)
                                            Debug.Log("New sound array is allocated! Length: " + result);
                                        Array.Copy(_soundDTO, _soundDTO.GetLowerBound(0), tmpArray,
                                            tmpArray.GetLowerBound(0), result);
                                        setData = _clip.SetData(tmpArray, _audioWritePos % _clip.samples);
                                    }
                                    else
                                    {
                                        Array.Copy(_soundDTO, _soundDTO.GetLowerBound(0), _replayAudioData[index],
                                            _replayAudioData[index].GetLowerBound(0), result);
                                        setData = _clip.SetData(_replayAudioData[index],
                                            _audioWritePos % _clip.samples);
                                    }
                                }


                                if (!setData && controller.debugLogs)
                                    Debug.LogError("Could not set audio data!");
                                else
                                    newData = true;


                                _audioWritePos += result / _playbackChannelNum;
                                loadTime += (result / _playbackChannelNum) / (float)_playbackSamplingRate;
                            }
                        }

                        _nextSoundReplayTime = loadTime;
                    }
                }
            }
            else
            {
                newData = true;
            }

            _lastReplayTime = replayTime;

            return newData;
        }

        public AudioSource GetAudioSource()
        {
            return _source;
        }
    }
}