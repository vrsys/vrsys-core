﻿// VRSYS plugin of Virtual Reality and Visualization Group (Bauhaus-University Weimar)
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
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Scripts.Recording
{
    public class MetaAvatarRecorder : GenericRecorder
    {
        private MetaAvatarReplayDataReader _avatarDataReader;
        private MetaAvatarReplayDataWriter _avatarDataWriter;
        private int _recordedDataIndex = 0;
        private uint? _firstParsedTicks = null;
        private float _previousReplayTime = -1.0f;
        private uint _tickOffset = 0;
        private uint? _lastEmittedTicks = null;

        private MetaAvatarReplayDataReader _rerecReader;
        private bool _rerecStartedReader;
        private int _rerecSampleIndex;

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
            _avatarDataReader.StopReadingData();
        }

        public override void OnReplayStart()
        {
            ExtendedLogger.LogInfo(GetType().Name, "Meta Avatar Recorder On Replay Start Called", this);
            base.OnReplayStart();
            if(_avatarDataWriter == null)
                _avatarDataWriter = GetComponent<MetaAvatarReplayDataWriter>();
            _recordedDataIndex = 0;
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
            _intDTO[0] = userID1;
            _intDTO[1] = userID2;
            _intDTO[2] = _recordedDataIndex;
            _intDTO[3] = avatarData.Data.Length;
            
            id = (int) avatarData.UserID;
            
            if (avatarData.Data.Length > _charDTO.Length)
            {
                Debug.LogWarning("Warning! Cannot record avatar data, as the data size is exceeding the array size.");    
                return;
            }
            
            Array.Copy( avatarData.Data, avatarData.Data.GetLowerBound(0), _charDTO, _charDTO.GetLowerBound(0), avatarData.Data.Length);

            bool result = RecordGenericAtTimestamp(controller.RecorderID, controller.recorderState.currentRecordingTime, id, _intDTO, _floatDTO, _charDTO);

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
            base.BeginRerecordCapture();
            _rerecSampleIndex = 0;

            _rerecReader = FindAnyObjectByType<MetaAvatarReplayDataReader>();
            if (_rerecReader == null)
            {
                ExtendedLogger.LogError(GetType().Name,
                    "ReRecord begin: no MetaAvatarReplayDataReader found in scene", this);
                return;
            }

            _rerecReader.OnAvatarDataRead.AddListener(RerecordAvatarData);
            _rerecStartedReader = _rerecReader.StartReadingData();
            if (!_rerecStartedReader)
                ExtendedLogger.LogWarning(GetType().Name,
                    "ReRecord begin: avatar reader could not be started; relying on existing run", this);
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

            _rerecReader = null;
            _rerecStartedReader = false;
        }

        private void RerecordAvatarData(MetaAvatarReplayDataReader.AvatarData avatarData)
        {
            if (!inRerecordingMode)
                return;
            if (avatarData.Data == null || avatarData.Data.Length == 0)
                return;
            if (avatarData.Data.Length > _charDTO.Length)
            {
                Debug.LogWarning("ReRecord: avatar data exceeds char DTO size; skipping");
                return;
            }

            int u1, u2;
            Decombine(avatarData.UserID, out u1, out u2);

            int[] ints = new int[_intDTO.Length];
            float[] floats = new float[_floatDTO.Length];
            byte[] chars = new byte[_charDTO.Length];

            ints[0] = u1;
            ints[1] = u2;
            ints[2] = _rerecSampleIndex++;
            ints[3] = avatarData.Data.Length;
            Array.Copy(avatarData.Data, 0, chars, 0, avatarData.Data.Length);

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
            int userID1 = _intDTO[0];
            int userID2 = _intDTO[1];
            ulong userID = Combine(userID1, userID2);
            int recordedDataIndex = _intDTO[2];
            int dataLength = _intDTO[3];
            
            // if new avatar data was received process it
            if (recordedDataIndex != _recordedDataIndex)
            {
                byte[] data = new byte[dataLength];
                Array.Copy( _charDTO, _charDTO.GetLowerBound(0), data, data.GetLowerBound(0), dataLength);
                
                // Parse original timestamp from bytes 16..19
                uint parsedTicks =
                    (uint)data[16]
                    | ((uint)data[17] << 8)
                    | ((uint)data[18] << 16)
                    | ((uint)data[19] << 24);
                
                bool isSeeked = _previousReplayTime >= 0.0f && Mathf.Abs(replayTime - _previousReplayTime) > SeekDetectionThreshold;

                if (isSeeked && _lastEmittedTicks.HasValue)
                {
                    const uint tickFreqHz = 2_000_000u;
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