// VRSYS plugin of Virtual Reality and Visualization Research Group (Bauhaus University Weimar)
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
// Copyright (c) 2022 Virtual Reality and Visualization Research Group
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
//   Authors:        Sebastian Muehlhaus, Tony Zoeppig
//   Date:           2023
//-----------------------------------------------------------------

using System;
using System.Collections.Generic;
using OdinNative.Odin.Media;
using OdinNative.Odin.Peer;
using OdinNative.Odin.Room;
using OdinNative.Unity.Audio;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using VRSYS.Core.Logging;
using VRSYS.Core.Networking;

namespace VRSYS.Core.Chat.Odin
{
    public class ForeignAudioComponent : NetworkBehaviour
    {
        #region MemberVariables

        [Header("Remote Audio Properties")]
        public Transform foreignAudioAttachement;
        public List<GameObject> audioObjects;
        public PlaybackComponent remotePlaybackComponent;

        [Header("Odin Rooms")]
        public OdinRoomsConfigurationInfo odinRoomsConfigurationInfo;
        public Dictionary<string, OdinRoomConfiguration> currentRooms = new Dictionary<string, OdinRoomConfiguration>();

        [Header("Room Events")]
        public UnityEvent<OdinRoomConfiguration> onJoinedRoom = new UnityEvent<OdinRoomConfiguration>();

        public UnityEvent<string> onLeftRoom = new UnityEvent<string>();

        [Header("Debug")]
        public bool verbose = false;
        
        public NetworkVariable<bool> isCurrentlyPlaying = new(default, writePerm: NetworkVariableWritePermission.Owner);
        public UnityEvent<bool, bool> onPlayingStateChange = new UnityEvent<bool, bool>();

        private AudioSource audioSource;
        private volatile bool captureForeignAudio;
        private float[] monoBuffer;
        private MicrophoneStream stream;

        #endregion

        #region Mono & NetworkBehaviour Callbacks

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                Debug.LogError($"{nameof(ForeignAudioComponent)} requires an AudioSource on the same GameObject.");
            audioSource = GetComponent<AudioSource>();
        }

        public override void OnNetworkSpawn()
        {
            InitializeBuffers();

            foreach (var roomConfig in odinRoomsConfigurationInfo.roomConfigurations)
            {
                JoinOdinRoom(roomConfig);
            }

            OdinHandler.Instance.Microphone.RedirectCapturedAudio = false;
            OdinHandler.Instance.OnRoomLeft.AddListener(OnRoomLeft);

            OdinHandler.Instance.OnMediaAdded.AddListener(MediaAdded);
            OdinHandler.Instance.OnMediaRemoved.AddListener(MediaRemoved);
            OdinHandler.Instance.OnPeerUserDataChanged.AddListener(UserDataChanged);

            isCurrentlyPlaying.OnValueChanged += ApplyPlayingStateChange;
        }

        private void Update()
        {
            if (!IsOwner)
                return;

            CheckSendForeignAudio();
        }

        /// We push as much audio as is processed by Unity, not on Update
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!captureForeignAudio || data == null || data.Length == 0)
                return;

            int frames = data.Length / channels;

            if (monoBuffer == null || monoBuffer.Length != frames)
                monoBuffer = new float[frames];

            if (channels == 1)
            {
                Array.Copy(data, monoBuffer, frames);
            }
            else
            {
                for (int frame = 0; frame < frames; frame++)
                {
                    float sample = 0f;

                    for (int ch = 0; ch < channels; ch++)
                        sample += data[frame * channels + ch];

                    monoBuffer[frame] = sample / channels;
                }
            }

            stream?.AudioPushData(monoBuffer, frames);
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
                LeaveOdinRooms();

            OdinHandler.Instance.OnMediaAdded.RemoveListener(MediaAdded);
            OdinHandler.Instance.OnMediaRemoved.RemoveListener(MediaRemoved);
            OdinHandler.Instance.OnPeerUserDataChanged.RemoveListener(UserDataChanged);
        }

        #endregion

        #region Custom Methods

        // Join odin voice rooms without leaving other rooms by room configuration
        public void JoinOdinRoom(OdinRoomConfiguration roomConfig)
        {
            if (string.IsNullOrEmpty(roomConfig.roomName))
            {
                ExtendedLogger.LogError(GetType().Name, "Room name cannot be empty");
                return;
            }

            OdinUserData userData = new OdinUserData
            {
                NetworkId = NetworkObjectId,
                IsStereo = roomConfig.defaultStereo
            };

            roomConfig.roomName = ConnectionManager.Instance.lobbySettings.lobbyName + "_" + roomConfig.roomName;
            currentRooms.Add(roomConfig.roomName, roomConfig);
            OdinHandler.Instance.JoinRoom(roomConfig.roomName, userData);

            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, "Joined Odin Room: " + roomConfig.roomName);

            onJoinedRoom.Invoke(roomConfig);
        }


        // Leave all odin voice rooms
        public void LeaveOdinRooms()
        {
            foreach (var (roomName, _) in currentRooms)
            {
                OdinHandler.Instance.LeaveRoom(roomName);

                onLeftRoom.Invoke(roomName);
            }

            currentRooms.Clear();
        }

        private void OnRoomLeft(RoomLeftEventArgs arg0)
        {
            OdinHandler.Instance.DestroyPlaybackComponents(arg0.RoomName);
        }

        private void MediaAdded(object roomObject, MediaAddedEventArgs eventArgs)
        {
            ulong peerId = eventArgs.PeerId;
            long mediaId = eventArgs.Media.Id;

            if (roomObject is Room room)
            {
                if (!currentRooms.ContainsKey(room.Config.Name)) return;

                AddPlaybackComponent(room, peerId, mediaId);
                if (room.MicrophoneMedia != null)
                {
                    if (verbose)
                        ExtendedLogger.LogInfo(GetType().Name, $"ODIN mic config for {room.Config.Name}: " +
                                                               $"{room.MicrophoneMedia.MediaConfig.SampleRate} Hz, " +
                                                               $"{room.MicrophoneMedia.MediaConfig.Channels} channels");

                    stream = room.MicrophoneMedia;
                }
            }
        }

        private void AddPlaybackComponent(Room room, ulong peerId, long mediaId)
        {
            Peer peer = room.RemotePeers[peerId];
            OdinUserData userData = JsonUtility.FromJson<OdinUserData>(peer.UserData.ToString());

            if (!IsOwner && userData.NetworkId == NetworkObjectId)
            {
                PlaybackComponent playback = Instantiate(remotePlaybackComponent, foreignAudioAttachement);
                playback.transform.localPosition = Vector3.zero;
                playback.RoomName = room.Config.Name;
                playback.PeerId = peerId;
                playback.MediaStreamId = mediaId;
                playback.gameObject.name = room.Config.Name;

                audioObjects.Add(playback.gameObject);

                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Remote voice added");

                SetupVoiceComponents(playback.gameObject, userData.IsStereo);
            }
        }

        private void MediaRemoved(object roomObject, MediaRemovedEventArgs eventArgs)
        {
            ulong peerId = eventArgs.Peer.Id;
            long mediaId = eventArgs.MediaStreamId;
            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, "MediaRemoved " + peerId + " - " + mediaId);

            if (roomObject is Room room)
            {
                if (!IsOwner)
                {
                    GameObject playbackObject = audioObjects.Find(obj =>
                        obj.GetComponent<PlaybackComponent>().PeerId == peerId &&
                        obj.GetComponent<PlaybackComponent>().MediaStreamId == mediaId);

                    if (playbackObject != null)
                    {
                        audioObjects.Remove(playbackObject);

                        Destroy(playbackObject);

                        if (verbose)
                            ExtendedLogger.LogInfo(GetType().Name, "Remote voice removed");
                    }
                }
            }
        }

        private void SetupVoiceComponents(GameObject voiceObject, bool isStereo)
        {
            if (voiceObject.GetComponent<AudioSource>())
            {
                AudioSource voiceAudioSource = voiceObject.GetComponent<AudioSource>();
                voiceAudioSource.spatialBlend = isStereo ? 0f : 1f;
            }
        }

        private void UserDataChanged(object roomObject, PeerUserDataChangedEventArgs eventArgs)
        {
            ulong peerId = eventArgs.PeerId;
            string roomName = eventArgs.Peer.RoomName;

            if (roomObject is Room room)
            {
                Peer peer = room.RemotePeers[peerId];
                OdinUserData userData = JsonUtility.FromJson<OdinUserData>(peer.UserData.ToString());

                if (!IsOwner && userData.NetworkId == NetworkObjectId)
                {
                    AudioSource foreignAudioSource =
                        audioObjects.Find(o => o.name == roomName)?.GetComponent<AudioSource>();
                    foreignAudioSource.spatialBlend = userData.IsStereo ? 0f : 1f;
                }
            }
        }

        // Ring buffer for reusable float arrays
        private class RBuffer
        {
            public static int MicPosition = 0;

            public const int sizesMin = 10;
            public const int sizesMax = 11;

            const int redundancy = 8; // times 8 ea buffer size to cycle
            int index = 0;

            float[][] internalBuffers = new float[redundancy][];

            public float[] buffer
            {
                get { return internalBuffers[index]; }
            }

            public void Cycle()
            {
                index = (index + 1) % redundancy;
            }

            public RBuffer(int size)
            {
                for (int i = 0; i < redundancy; i++)
                {
                    internalBuffers[i] = new float[1 << size]; // 2 ^ 12
                }
            }
        }

        RBuffer[] ClipBuffer = new RBuffer[RBuffer.sizesMax + 1];


        private void InitializeBuffers()
        {
            for (int i = RBuffer.sizesMin; i <= RBuffer.sizesMax; i++)
                ClipBuffer[i] = new RBuffer(i);
        }

        private void CheckSendForeignAudio()
        {
            bool shouldCapture =
                audioSource &&
                audioSource.isPlaying &&
                audioSource.clip;

            captureForeignAudio = shouldCapture;
        }
        
        public void ApplyPlayingStateChange(bool previousValue, bool newValue)
        {
            onPlayingStateChange.Invoke(previousValue, newValue);
        }

        #endregion
    }
}