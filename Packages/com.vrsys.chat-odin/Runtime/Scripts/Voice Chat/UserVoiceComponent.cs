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

using System.Collections.Generic;
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
    public class UserVoiceComponent : NetworkBehaviour
    {
        #region Member Variables

        public static UserVoiceComponent LocalInstance
        {
            get
            {
                return NetworkUser.LocalInstance?.GetComponent<UserVoiceComponent>();
            }
        }

        [Header("General Voice Properties")]
        public Transform voiceAttachement;
        private List<GameObject> voiceObjects = new List<GameObject>();

        [Header("Voice Amplitude Measurement")]
        public UnityEvent<float> onAmplitudeChanged;
        public enum AmplitudeCalculation
        {
            Average,
            Peak
        }
        public AmplitudeCalculation amplitudeCalculation = AmplitudeCalculation.Peak;
        private bool amplitudeMeasurementInitialized = false;
        
        [Header("Local Voice Properties")]
        public GameObject localVoiceComponentPrefab;
        private GameObject localVoiceComponent;

        [Header("Voice Control")] 
        public InputActionProperty muteAction;
        public bool isGloballyMuted = false;
        public string muteGroup = "";

        [Header("Remote Voice Properties")] 
        public PlaybackComponent playbackPrefab;

        [Header("Channel Handling")] 
        public bool switchVoiceRoom = false;
        public bool joinVoiceRoom = false;
        public bool leaveVoiceRoom = false;
        public bool leaveAllVoiceRooms = false;
        public bool toggleStereoInRoom = false;
        public bool toggleMuteInRoom = false;
        public string voiceRoomName;

        [Header("Odin Rooms")]
        public OdinRoomsConfigurationInfo odinRoomsConfigurationInfo;
        public List<OdinRoomConfiguration> currentRooms = new List<OdinRoomConfiguration>();

        [Header("Room Events")] 
        public UnityEvent<OdinRoomConfiguration> onJoinedRoom = new UnityEvent<OdinRoomConfiguration>();
        public UnityEvent<string> onLeftRoom = new UnityEvent<string>();

        [Header("Debug")]
        public bool verbose = false;
        
        #endregion

        #region Mono & NetworkBehaviour Callbacks
        
        public void Start()
        {
            if (IsOwner)
            {
                SetupLocalVoiceComponent();

                foreach (var roomConfig in odinRoomsConfigurationInfo.roomConfigurations)
                {
                    JoinOdinRoom(roomConfig);
                }

                // setup microphone mute handling
                OdinHandler.Instance.Microphone.RedirectCapturedAudio = false;
                OdinHandler.Instance.Microphone.OnMicrophoneData += OnMicrophoneData;
                
                OdinHandler.Instance.OnRoomLeft.AddListener(OnRoomLeft);
            }
            
            OdinHandler.Instance.OnMediaAdded.AddListener(MediaAdded);
            OdinHandler.Instance.OnMediaRemoved.AddListener(MediaRemoved);
            OdinHandler.Instance.OnPeerUserDataChanged.AddListener(UserDataChanged);
        }

        private void Update()
        {
            if(amplitudeMeasurementInitialized)
                SetSpeechAmplitude();
            
            if (!IsOwner)
                return;

            if (muteAction.action.WasPressedThisFrame())
            {
                if(verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Mute button press");
                ToggleMuteGlobally();
            }

            if (switchVoiceRoom)
            {
                SwitchOdinRoom(voiceRoomName);
                voiceRoomName = "";
                switchVoiceRoom = false;
            }
            
            if (joinVoiceRoom)
            {
                JoinOdinRoom(voiceRoomName);
                voiceRoomName = "";
                joinVoiceRoom = false;
            }

            if (leaveVoiceRoom)
            {
                LeaveOdinRoom(voiceRoomName);
                voiceRoomName = "";
                leaveVoiceRoom = false;
            }

            if (leaveAllVoiceRooms)
            {
                LeaveOdinRooms();
                leaveAllVoiceRooms = false;
            }

            if (toggleStereoInRoom)
            {
                ToggleStereoVoice(voiceRoomName);
                voiceRoomName = "";
                toggleStereoInRoom = false;
            }
            
            if(toggleMuteInRoom)
            {
                ToggleMuteInRoom(voiceRoomName);
                voiceRoomName = "";
                toggleMuteInRoom = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                OdinHandler.Instance.Microphone.OnMicrophoneData -= OnMicrophoneData;
                LeaveOdinRooms();
            }
            
            OdinHandler.Instance.OnMediaAdded.RemoveListener(MediaAdded);
            OdinHandler.Instance.OnMediaRemoved.RemoveListener(MediaRemoved);
            OdinHandler.Instance.OnPeerUserDataChanged.RemoveListener(UserDataChanged);
        }

        #endregion

        #region Custom Methods

        private void SetupLocalVoiceComponent()
        {
            if (localVoiceComponentPrefab != null)
            {
                localVoiceComponent = Instantiate(localVoiceComponentPrefab, voiceAttachement);
                voiceObjects.Add(localVoiceComponent);
                SetupVoiceComponents(localVoiceComponent);
            }
        }

        // Join a odin voice room without leaving other rooms by room name
        public void JoinOdinRoom(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
            {
                ExtendedLogger.LogError(GetType().Name,"Room name cannot be empty");
                return;
            }
            
            OdinUserData userData = new OdinUserData
            {
                NetworkId = NetworkObjectId,
                IsStereo = false
            };

            roomName = ConnectionManager.Instance.lobbySettings.lobbyName + "_" + roomName;

            OdinRoomConfiguration roomConfig = new OdinRoomConfiguration(roomName, true, true, false);
            currentRooms.Add(roomConfig);
            OdinHandler.Instance.JoinRoom(roomName, userData);
            
            onJoinedRoom.Invoke(roomConfig);
        }
        
        // Join odin voice rooms without leaving other rooms by room configuration
        public void JoinOdinRoom(OdinRoomConfiguration roomConfig)
        {
            if (string.IsNullOrEmpty(roomConfig.roomName))
            {
                ExtendedLogger.LogError(GetType().Name,"Room name cannot be empty");
                return;
            }
            
            OdinUserData userData = new OdinUserData
            {
                NetworkId = NetworkObjectId,
                IsStereo = roomConfig.defaultStereo
            };
            
            roomConfig.roomName = ConnectionManager.Instance.lobbySettings.lobbyName + "_" + roomConfig.roomName;
            currentRooms.Add(roomConfig);
            OdinHandler.Instance.JoinRoom(roomConfig.roomName, userData);
            
            onJoinedRoom.Invoke(roomConfig);
        }

        // Join a odin voice room and leave all other rooms
        public void SwitchOdinRoom(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
            {
                ExtendedLogger.LogError(GetType().Name,"Room name cannot be empty");
                return;
            }
            
            LeaveOdinRooms();
            JoinOdinRoom(roomName);
        }
        
        // Leave a odin voice room
        public void LeaveOdinRoom(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
            {
                ExtendedLogger.LogError(GetType().Name,"Room name cannot be empty");
                return;
            }
            
            OdinHandler.Instance.LeaveRoom(roomName);
            currentRooms.RemoveAll(room => room.roomName == roomName);
            
            onLeftRoom.Invoke(roomName);
        }
        
        // Leave all odin voice rooms
        public void LeaveOdinRooms()
        {
            foreach (var room in currentRooms)
            {
                OdinHandler.Instance.LeaveRoom(room.roomName);
                
                onLeftRoom.Invoke(room.roomName);
            }
            
            currentRooms.Clear();
        }
        
        private void OnRoomLeft(RoomLeftEventArgs arg0)
        {
            OdinHandler.Instance.DestroyPlaybackComponents(arg0.RoomName);
        }
        
        // This method mutes the microphone, by checking if audio transmission is disabled for a room
        // If this is the case it overrides the audio buffer with an empty buffer array before sending the data
        private void OnMicrophoneData(float[] buffer, int position)
        {
            foreach (Room room in OdinHandler.Instance.Rooms)
            {
                float[] pushedBuffer = buffer;
                if (IsMicrophoneMuted(room.Config.Name) || isGloballyMuted)
                    pushedBuffer = new float[buffer.Length];
                
                if(room.MicrophoneMedia != null)
                    room.MicrophoneMedia.AudioPushData(pushedBuffer);
                else if (room.IsJoined && verbose)
                    ExtendedLogger.LogWarning(GetType().Name,
                        $"Room {room.Config.Name} is missing a microphone stream.");
                
            }
        }
        
        private bool IsMicrophoneMuted(string roomName)
        {
            return !currentRooms.Find(room => room.roomName == roomName).transmitAudio;
        }
        
        private void MediaAdded(object roomObject, MediaAddedEventArgs eventArgs)
        {
            ulong peerId = eventArgs.PeerId;
            long mediaId = eventArgs.Media.Id;

            if (roomObject is Room room)
            {
                Peer peer = room.RemotePeers[peerId];
                OdinUserData userData = JsonUtility.FromJson<OdinUserData>(peer.UserData.ToString());
                
                if (!IsOwner && userData.NetworkId == NetworkObjectId)
                {
                    PlaybackComponent playback = Instantiate(playbackPrefab, voiceAttachement);
                    playback.transform.localPosition = Vector3.zero;
                    playback.RoomName = room.Config.Name;
                    playback.PeerId = peerId;
                    playback.MediaStreamId = mediaId;
                    playback.gameObject.name = room.Config.Name;
                    
                    voiceObjects.Add(playback.gameObject);
                    
                    if(verbose)
                        ExtendedLogger.LogInfo(GetType().Name, "Remote voice added");
                    
                    SetupVoiceComponents(playback.gameObject, userData.IsStereo);
                }
            }
        }
        
        private void MediaRemoved(object roomObject, MediaRemovedEventArgs eventArgs)
        {
            ulong peerId = eventArgs.Peer.Id;
            long mediaId = eventArgs.MediaStreamId;

            if(roomObject is Room room)
            {
                if (!IsOwner)
                {
                    GameObject playbackObject = voiceObjects.Find(obj => obj.GetComponent<PlaybackComponent>().PeerId == peerId && obj.GetComponent<PlaybackComponent>().MediaStreamId == mediaId);
                
                    if (playbackObject != null)
                    {
                        voiceObjects.Remove(playbackObject);

                        if (voiceObjects.Count == 0)
                            amplitudeMeasurementInitialized = false;
                    
                        Destroy(playbackObject);
                    
                        if(verbose)
                            ExtendedLogger.LogInfo(GetType().Name, "Remote voice removed");
                    }
                }
            }
        }

        private void SetupVoiceComponents(GameObject voiceObject)
        {
            AmplitudeMeasurement amplitudeMeasurement = voiceObject.GetComponent<AmplitudeMeasurement>();

            if (amplitudeMeasurement != null)
            {
                amplitudeMeasurementInitialized = true;
            }
        }

        private void SetupVoiceComponents(GameObject voiceObject, bool isStereo)
        {
            SetupVoiceComponents(voiceObject);
            
            if (voiceObject.GetComponent<AudioSource>())
            {
                AudioSource voiceAudioSource = voiceObject.GetComponent<AudioSource>();
                voiceAudioSource.spatialBlend = isStereo ? 0f : 1f;

                if (!IsOwner)
                {
                    ConfigureMuteGroup();
                }
            }
        }

        private void SetSpeechAmplitude()
        {
            AmplitudeMeasurement amplitudeMeasurement = voiceObjects[0].GetComponent<AmplitudeMeasurement>();
            
            float speechAmplitude = amplitudeCalculation == AmplitudeCalculation.Peak
                ? amplitudeMeasurement.peakAmplitude
                : amplitudeMeasurement.averageAmplitude;
            
            onAmplitudeChanged.Invoke(speechAmplitude);
        }

        public void ToggleStereoVoice(string roomName)
        {
            if(verbose)
                ExtendedLogger.LogInfo(GetType().Name, "Stereo toggled for room " + roomName + ".");

            Room room = OdinHandler.Instance.Rooms[roomName];
            OdinUserData userData = JsonUtility.FromJson<OdinUserData>(room.Self.UserData.ToString());
            userData.IsStereo = !userData.IsStereo;
            room.UpdatePeerUserData(userData);
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
                    AudioSource audioSource = voiceObjects.Find(o => o.name == roomName)?.GetComponent<AudioSource>();
                    audioSource.spatialBlend = userData.IsStereo ? 0f : 1f;
                }
            }
        }

        public void ToggleMuteGlobally()
        {
            isGloballyMuted = !isGloballyMuted;
        }

        public void ToggleMuteInRoom(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
            {
                ExtendedLogger.LogError(GetType().Name,"Room name cannot be empty");
                return;
            }
            
            OdinRoomConfiguration roomConfig = currentRooms.Find(room => room.roomName == roomName);
            currentRooms.Remove(roomConfig);
            
            roomConfig.transmitAudio = !roomConfig.transmitAudio;
            currentRooms.Add(roomConfig);
        }

        private void ConfigureMuteGroup()
        {
            if (muteGroup.Length > 0)
            {
                bool mute = muteGroup.Equals(LocalInstance.muteGroup);

                foreach (var voiceObject in voiceObjects)
                {
                    voiceObject.GetComponent<AudioSource>().mute = mute;
                }
            }
        }

        #endregion
    }
}