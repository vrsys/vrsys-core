using System;
using System.Linq;
using OdinNative.Core;
using OdinNative.Core.Imports;
using OdinNative.Odin;
using UnityEngine;
using Random = System.Random;

namespace OdinNative.Unity
{
    /// <summary>
    /// UnityEditor UI component for instance config of <see cref="OdinNative.Odin.OdinDefaults"/>
    /// </summary>
    [DisallowMultipleComponent, ExecuteInEditMode]
    public class OdinEditorConfig : MonoBehaviour, IOdinApmConfig
    {
        /// <summary>
        /// Enable additional Logs
        /// </summary>
        public bool Verbose = OdinDefaults.Verbose;
        /// <summary>
        /// Enable additional Logs
        /// </summary>
        public bool VerboseDebug = OdinDefaults.Debug;
        /// <summary>
        /// Odin Client ApiKey
        /// </summary>
        public string AccessKey;
        /// <summary>
        /// Odin Client ID
        /// </summary>
        /// <remarks>Room token userId</remarks>
        public string ClientId;
        /// <summary>
        /// Gateway
        /// </summary>
        public string Server = OdinDefaults.Server;
        /// <summary>
        /// Default UserData content
        /// </summary>
        public string UserDataText = OdinDefaults.UserDataText;

        /// <summary>
        /// Microphone Sample-Rate
        /// </summary>
        public MediaSampleRate DeviceSampleRate = OdinDefaults.DeviceSampleRate;
        /// <summary>
        /// Microphone Channels
        /// </summary>
        public MediaChannels DeviceChannels = OdinDefaults.DeviceChannels;

        /// <summary>
        /// Playback Sample-Rate
        /// </summary>
        public MediaSampleRate RemoteSampleRate = OdinDefaults.RemoteSampleRate;
        /// <summary>
        /// Playback Channels
        /// </summary>
        public MediaChannels RemoteChannels = OdinDefaults.RemoteChannels;

        #region Events
        public bool PeerJoinedEvent = OdinDefaults.PeerJoinedEvent;
        public bool PeerLeftEvent = OdinDefaults.PeerLeftEvent;
        public bool PeerUpdatedEvent = OdinDefaults.PeerUpdatedEvent;
        public bool MediaAddedEvent = OdinDefaults.MediaAddedEvent;
        public bool MediaRemovedEvent = OdinDefaults.MediaRemovedEvent;
        public bool RoomUpdatedEvent = OdinDefaults.RoomUpdatedEvent;
        public bool MediaActiveStateChangedEvent = OdinDefaults.MediaActiveStateChangedEvent;
        public bool MessageReceivedEvent = OdinDefaults.MessageReceivedEvent;
        #endregion Events

        /// <summary>
        /// Time untill the token expires
        /// </summary>
        public ulong TokenLifetime = OdinDefaults.TokenLifetime;

        #region Apm
        /// <summary>
        /// Turns VAD on and off
        /// </summary>
        public bool VoiceActivityDetection = OdinDefaults.VoiceActivityDetection;
        bool IOdinApmConfig.VoiceActivityDetection { get => VoiceActivityDetection; set => VoiceActivityDetection = value; }
        /// <summary>
        /// Setup engage of VAD
        /// </summary>
        public float VoiceActivityDetectionAttackProbability = OdinDefaults.VoiceActivityDetectionAttackProbability;
        float IOdinApmConfig.VoiceActivityDetectionAttackProbability { get => VoiceActivityDetectionAttackProbability; set => VoiceActivityDetectionAttackProbability = value; }
        /// <summary>
        /// Setup disengage of VAD
        /// </summary>
        public float VoiceActivityDetectionReleaseProbability = OdinDefaults.VoiceActivityDetectionReleaseProbability;
        float IOdinApmConfig.VoiceActivityDetectionReleaseProbability { get => VoiceActivityDetectionReleaseProbability; set => VoiceActivityDetectionReleaseProbability = value; }
        /// <summary>
        /// Turns volume gate on and off
        /// </summary>
        public bool VolumeGate = OdinDefaults.VolumeGate;
        bool IOdinApmConfig.VolumeGate { get => VolumeGate; set => VolumeGate = value; }
        /// <summary>
        /// Setup engage of volume gate
        /// </summary>
        public float VolumeGateAttackLoudness = OdinDefaults.VolumeGateAttackLoudness;
        float IOdinApmConfig.VolumeGateAttackLoudness { get => VolumeGateAttackLoudness; set => VolumeGateAttackLoudness = value; }
        /// <summary>
        /// Setup disengage of volume gate
        /// </summary>
        public float VolumeGateReleaseLoudness = OdinDefaults.VolumeGateReleaseLoudness;
        float IOdinApmConfig.VolumeGateReleaseLoudness { get => VolumeGateReleaseLoudness; set => VolumeGateReleaseLoudness = value; }
        /// <summary>
        /// Turns Echo cancellation on and off
        /// </summary>
        public bool EchoCanceller = OdinDefaults.EchoCanceller;
        bool IOdinApmConfig.EchoCanceller { get => EchoCanceller; set => EchoCanceller = value; }
        /// <summary>
        /// Reduces lower frequencies of the input (Automatic game control)
        /// </summary>
        public bool HighPassFilter = OdinDefaults.HighPassFilter;
        bool IOdinApmConfig.HighPassFilter { get => HighPassFilter; set => HighPassFilter = value; }
        /// <summary>
        /// Amplifies the audio input
        /// </summary>
        public bool PreAmplifier = OdinDefaults.PreAmplifier;
        bool IOdinApmConfig.PreAmplifier { get => PreAmplifier; set => PreAmplifier = value; }
        /// <summary>
        /// Turns noise suppression on and off
        /// </summary>
        public Core.Imports.NativeBindings.OdinNoiseSuppressionLevel NoiseSuppressionLevel = OdinDefaults.NoiseSuppressionLevel;
        NativeBindings.OdinNoiseSuppressionLevel IOdinApmConfig.NoiseSuppressionLevel { get => NoiseSuppressionLevel; set => NoiseSuppressionLevel = value; }
        /// <summary>
        /// Filters high amplitude noices
        /// </summary>
        public bool TransientSuppressor = OdinDefaults.TransientSuppressor;
        bool IOdinApmConfig.TransientSuppressor { get => TransientSuppressor; set => TransientSuppressor = value; }
        /// <summary>
        /// Turns gain controller on and off
        /// </summary>
        public bool GainController = OdinDefaults.GainController;
        bool IOdinApmConfig.GainController { get => GainController; set => GainController = value; }
        #endregion Apm

        internal string Identifier => SystemInfo.deviceUniqueIdentifier;


        void Awake()
        {
            if (string.IsNullOrEmpty(ClientId))
                ClientId = string.Join(".", Application.companyName, Application.productName);
        }

        public void GenerateUIAccessKey()
        {
            AccessKey = GenerateAccessKey();
        }

        /// <summary>
        /// Generates a new ODIN access key.
        /// </summary>
        /// <returns>ODIN access key</returns>
        private static string GenerateAccessKey()
        {
            var rand = new Random();
            byte[] key = new byte[33];
            rand.NextBytes(key);
            key[0] = 0x01;
            byte[] subArray = new ArraySegment<byte>(key, 1, 31).ToArray();
            key[32] = Crc8(subArray);
            return Convert.ToBase64String(key);
        }

        private static byte Crc8(byte[] data)
        {
            byte crc = 0xff;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x80) != 0) crc = (byte)((crc << 1) ^ 0x31);
                    else crc <<= 1;
                }
                crc = (byte)(0xff & crc);
            }
            return crc;
        }

    }


}
