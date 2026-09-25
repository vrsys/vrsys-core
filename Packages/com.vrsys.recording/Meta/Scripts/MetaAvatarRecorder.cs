// VRSYS plugin of Virtual Reality and Visualization Group (Bauhaus-University Weimar)
//  _    ______  _______  _______
// | |  / / __ \/ ___/\ \/ / ___/
// | | / / /_/ /\__ \  \  /\__ \
// | |/ / _, _/___/ /  / /___/ /
// |___/_/ |_|/____/  /_//____/
//
//  __                            __                       __   __   __    ___ .  . ___
// |__)  /\  |  | |__|  /\  |  | /__`    |  | |\ | | \  / |__  |__) /__` |  |   /\   |
// |__) /~~\ \__/ |  | /~~\ \__/ .__/    \__/ | \| |  \/  |___ |  \ .__/ |  |  /~~\  |
//
//       ___               __
// |  | |__  |  |\/|  /\  |__)
// |/\| |___ |  |  | /~~\ |  \
//
// Copyright (c) 2024 Virtual Reality and Visualization Group
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//-----------------------------------------------------------------
//   Authors:        Anton Lammert
//   Date:           2026
//-----------------------------------------------------------------
 
using System;
using Unity.Netcode;
using UnityEngine;
using VRSYS.Core.Avatar;
using VRSYS.Core.Logging;

namespace VRSYS.Recording
{
    public class MetaAvatarRecorder : GenericRecorder
    {
        private MetaAvatarReplayDataReader _avatarDataReader;
        private MetaAvatarReplayDataWriter _avatarDataWriter;
        private int _recordedDataIndex = 0;

        private AudioRecorder _correspondingAudioRecorder = null;
        private int _recordedSoundId = -1;
        private bool _hasAudioAssociation;

        public bool TryGetRecordedAudioId(out int soundId)
        {
            soundId = _recordedSoundId;
            return _hasAudioAssociation && soundId >= 0;
        }
        
        private uint? _firstParsedTicks = null;
        private float _previousReplayTime = -1.0f;
        private uint _tickOffset = 0;
        private uint? _lastEmittedTicks = null;
        private uint? _lastEmittedTrueTicks = null;
        private uint? _firstEmittedRerecordTick = null;
        
        private MetaAvatarReplayDataReader _rerecReader;
        private bool _rerecStartedReader;
        private int _rerecSampleIndex;

        private const uint tickFreqHz = 2_000_000u;
        private const float SeekDetectionThreshold = 0.5f;
        
        public override void OnRecordingStart()
        {
            ExtendedLogger.LogInfo(GetType().Name, "Meta Avatar Recorder On Recording Start Called", this);
            base.OnRecordingStart();
            if(_avatarDataReader == null)
                _avatarDataReader = GetComponent<MetaAvatarReplayDataReader>();
            _avatarDataReader.OnAvatarDataRead.AddListener(RecordAvatarData);
            _recordedDataIndex = 0;
            id = (int) _avatarDataReader.GetUserId();
            Debug.Log("Avatar users id: " + _avatarDataReader.GetUserId());
            // Resolve before StartReadingData, whose coroutine can emit immediately.
            NetworkObject userNetworkObject = GetComponentInParent<NetworkObject>();
            _correspondingAudioRecorder = userNetworkObject != null
                ? userNetworkObject.GetComponentInChildren<AudioRecorder>() : null;
            var t = userNetworkObject.GetComponentsInChildren<AudioRecorder>();
            foreach (var v in t)
            {
                Debug.Log("Found audio recorder: " + v.gameObject.name);
            }
            _recordedSoundId = _correspondingAudioRecorder != null ? _correspondingAudioRecorder.Id : -1;
            _hasAudioAssociation = _recordedSoundId >= 0;
            RegisterDescription(BuildGenericDescription(_avatarDataReader.GetUserId()));
            bool startedReadingData = _avatarDataReader.StartReadingData();
            if(!startedReadingData)
                ExtendedLogger.LogError(GetType().Name, "Meta Avatar Data Reader did not start reading data!", this);
            else 
                ExtendedLogger.LogInfo(GetType().Name, "Meta Avatar Data Reader did start reading data!", this);
            
        }
        
        public override void OnRecordingEnd()
        {
            ExtendedLogger.LogInfo(GetType().Name, "Meta Avatar Recorder On Recording End Called", this);
            base.OnRecordingEnd();
            _avatarDataReader.OnAvatarDataRead.RemoveListener(RecordAvatarData);
            _avatarDataReader.StopReadingData();
        }

        public override void OnReplayStart()
        {
            ExtendedLogger.LogInfo(GetType().Name, "Meta Avatar Recorder On Replay Start Called", this);
            base.OnReplayStart();
            if(_avatarDataWriter == null)
                _avatarDataWriter = GetComponent<MetaAvatarReplayDataWriter>();
            _recordedDataIndex = -1;
            _hasAudioAssociation = false;
            _recordedSoundId = -1;
            bool intializeReplayDataWriter = _avatarDataWriter.Initialize();
            id = (int) _avatarDataWriter.GetUserId();
            if (!intializeReplayDataWriter)
                ExtendedLogger.LogError(GetType().Name, "Meta Avatar Data Writer not initialized!", this);
            _avatarDataWriter.StartReplay();
        }
        
        public override void OnReplayEnd()
        {
            ExtendedLogger.LogInfo(GetType().Name, "Meta Avatar Recorder On Replay End Called", this);
            base.OnReplayEnd();
            if(_avatarDataWriter == null)
                _avatarDataWriter = GetComponent<MetaAvatarReplayDataWriter>();
            _avatarDataWriter.StopReplay();
            _avatarDataWriter.DestroyReplayEntity();
        }

        // Layout written by RecordAvatarData / RerecordAvatarData and read by ProcessReplayData.
        private string BuildGenericDescription(ulong userId)
        {
            return "MetaAvatarRecorder: Meta Avatar SDK streaming data of user " + userId +
                   "i[0]/i[1]: Meta user id, low/high 32 bits; " +
                   "i[2]: sample index; " +
                   "i[3]: length in bytes of the avatar data in c; " +
                   "i[4]: id of the corresponding audio recorder (" + _recordedSoundId + ", -1 = none); " +
                   "i[5]: 1 if i[4] is a valid audio association; " +
                   "f: unused; " +
                   "c[0..i[3]): Meta avatar stream packet:";
        }

        private static ulong Combine(int a, int b) {
            uint ua = (uint)a;
            ulong ub = (uint)b;
            return ub <<32 | ua;
        }
        private static void Decombine(ulong c, out int a, out int b) {
            a = (int)(c & 0xFFFFFFFFUL);
            b = (int)(c >> 32);
        }
        
        public void RecordAvatarData(MetaAvatarReplayDataReader.AvatarData avatarData)
        {
            if(controller.recorderState.currentState != State.Recording)
                return;
            
            int userID1, userID2 = 0;
            Decombine(avatarData.UserID, out userID1, out userID2);
            _recIntDTO[0] = userID1;
            _recIntDTO[1] = userID2;
            _recIntDTO[2] = _recordedDataIndex;
            _recIntDTO[3] = avatarData.Data.Length;

            _recIntDTO[4] = _recordedSoundId;
            _recIntDTO[5] = 1; // audio-association field present; legacy zero meant "unknown"
            
            id = (int) avatarData.UserID;
            
            if (avatarData.Data.Length > _recCharDTO.Length)
            {
                Debug.LogWarning("Warning! Cannot record avatar data, as the data size is exceeding the array size.");    
                return;
            }
            
            Array.Copy( avatarData.Data, avatarData.Data.GetLowerBound(0), _recCharDTO, _recCharDTO.GetLowerBound(0), avatarData.Data.Length);

            bool result = RecordGenericAtTimestamp(controller.RecorderID, controller.recorderState.currentRecordingTime, id, _recIntDTO, _recFloatDTO, _recCharDTO);

            if (!result && controller.debugLogs)
                Debug.Log("Could not record arbitrary data with id: " + id);
            _recordedDataIndex += 1;
        }
        
        protected override bool FillGenericData()
        {
            return false;
        }

        public override int GetRerecordObjectId()
        {
            return id;
        }

        public override void BeginRerecordCapture()
        {
            if (_avatarDataWriter == null || ReRecorderMetaAvatarLinker.Instance == null ||
                !ReRecorderMetaAvatarLinker.Instance.PlaybackToRealUser.TryGetValue(_avatarDataWriter, out var reader))
                throw new InvalidOperationException("No live reader is linked to the playback avatar.");
            BeginRerecordCapture(reader);
        }

        public void BeginRerecordCapture(MetaAvatarReplayDataReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            base.BeginRerecordCapture();
            _rerecSampleIndex = _recordedDataIndex + 1;
            _rerecReader = reader;
            _avatarDataWriter.HideAvatar();
            _rerecReader.OnAvatarDataRead.AddListener(RerecordAvatarData);
            _rerecStartedReader = _rerecReader.StartReadingData();
            if (!_rerecStartedReader)
                throw new InvalidOperationException("The linked avatar reader is not ready.");
        }

        public override void EndRerecordCapture()
        {
            base.EndRerecordCapture();

            if (_rerecReader != null)
            {
                _rerecReader.OnAvatarDataRead.RemoveListener(RerecordAvatarData);
                if (_rerecStartedReader)
                    _rerecReader.StopReadingData();
            }

            if (_avatarDataWriter != null) 
                _avatarDataWriter.ShowAvatar();
            _firstEmittedRerecordTick = null;
            _rerecReader = null;
            _rerecStartedReader = false;
        }

        private void RerecordAvatarData(MetaAvatarReplayDataReader.AvatarData avatarData)
        {
            if (!inRerecordingMode)
                return;
            if (avatarData.Data == null || avatarData.Data.Length < 20)
                return;
            if (avatarData.Data.Length > _recCharDTO.Length)
            {
                Debug.LogWarning("ReRecord: avatar data exceeds char DTO size; skipping");
                return;
            }

            int u1, u2;
            Decombine(avatarData.UserID, out u1, out u2);

            int[] ints = new int[_recIntDTO.Length];
            float[] floats = new float[_recFloatDTO.Length];
            byte[] chars = new byte[_recCharDTO.Length];

            ints[0] = u1;
            ints[1] = u2;
            ints[2] = _rerecSampleIndex++;
            ints[3] = avatarData.Data.Length;
            ints[4] = _recordedSoundId;
            ints[5] = _hasAudioAssociation ? 1 : 0;
            
            Array.Copy(avatarData.Data, 0, chars, 0, avatarData.Data.Length);

            // TODO: modify the new avatar data such that the playback between the original and the new data is seamless
            //       data parts that might need to be modified: original timestamp
                        
            uint parsedTicks =
                (uint)chars[16]
                | ((uint)chars[17] << 8)
                | ((uint)chars[18] << 16)
                | ((uint)chars[19] << 24);

            if (!_firstEmittedRerecordTick.HasValue)
            {
                _firstEmittedRerecordTick = parsedTicks;
                _tickOffset = _lastEmittedTrueTicks.HasValue
                    ? _lastEmittedTrueTicks.Value + tickFreqHz / 10 - parsedTicks : 0;
            }
            
            uint fakeTicks = parsedTicks + _tickOffset;

            // Write fakeTicks back into bytes 16..19 (little-endian)
            chars[16] = (byte)(fakeTicks & 0xFF);
            chars[17] = (byte)((fakeTicks >> 8) & 0xFF);
            chars[18] = (byte)((fakeTicks >> 16) & 0xFF);
            chars[19] = (byte)((fakeTicks >> 24) & 0xFF);
            
            EmitRerecordSample(new RerecordSample
            {
                time = controller.recorderState.currentReplayTime,
                ints = ints,
                floats = floats,
                chars = chars
            });
        }
        
        protected override void ProcessReplayData(float replayTime)
        {
            if(inRerecordingMode)
                return;
            
            int userID1 = _replayIntDTO[0];
            int userID2 = _replayIntDTO[1];
            ulong userID = Combine(userID1, userID2);
            int recordedDataIndex = _replayIntDTO[2];
            int dataLength = _replayIntDTO[3];
            int correspondingAudioRecorderID = _replayIntDTO[4];
            _recordedSoundId = correspondingAudioRecorderID;
            _hasAudioAssociation = _replayIntDTO[5] == 1 && correspondingAudioRecorderID >= 0;
            
            // if new avatar data was received process it
            if (recordedDataIndex != _recordedDataIndex)
            {
                byte[] data = new byte[dataLength];
                Array.Copy( _replayCharDTO, _replayCharDTO.GetLowerBound(0), data, data.GetLowerBound(0), dataLength);
                
                // Parse original timestamp from bytes 16..19
                uint parsedTicks =
                    (uint)data[16]
                    | ((uint)data[17] << 8)
                    | ((uint)data[18] << 16)
                    | ((uint)data[19] << 24);
                
                bool isSeeked = _previousReplayTime >= 0.0f && Mathf.Abs(replayTime - _previousReplayTime) > SeekDetectionThreshold;

                if (isSeeked && _lastEmittedTicks.HasValue)
                {
                    _tickOffset = _lastEmittedTicks.Value + tickFreqHz / 10 - parsedTicks;
                    _recordedDataIndex = recordedDataIndex - 1;
                }

                uint fakeTicks = parsedTicks + _tickOffset;

                // Write fakeTicks back into bytes 16..19 (little-endian)
                data[16] = (byte)(fakeTicks & 0xFF);
                data[17] = (byte)((fakeTicks >> 8) & 0xFF);
                data[18] = (byte)((fakeTicks >> 16) & 0xFF);
                data[19] = (byte)((fakeTicks >> 24) & 0xFF);

                _lastEmittedTicks = fakeTicks;
                _lastEmittedTrueTicks = parsedTicks;
                _previousReplayTime = replayTime;
                
                bool success = _avatarDataWriter.ApplyData(data);
                if (!success)
                {
                    ExtendedLogger.LogError(GetType().Name, "Applying avatar replay data failed!", this);
                    data = new byte[dataLength];
                    _avatarDataWriter.ApplyData(data);
                }
                else
                {
                    if(verbose)
                        Debug.Log("Applying recorded avatar data for user: " + userID + ", at time: " + replayTime);
                }

                _recordedDataIndex = recordedDataIndex;
            }
            
        }
    }
}
