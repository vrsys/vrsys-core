using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OdinNative.Core;

namespace OdinNative.Odin
{
    /// <summary>
    /// ODIN default configuration
    /// </summary>
    public static class OdinDefaults
    {
        /// <summary>
        /// Enable additional logs
        /// </summary>
        public static bool Verbose = false;
        /// <summary>
        /// Enable additional debug logs
        /// </summary>
        public static bool Debug = false;
        /// <summary>
        /// Default access key
        /// </summary>
        public static string AccessKey { get; set; } = "";
        /// <summary>
        /// Default server url
        /// </summary>
        public static string Server { get; set; } = "https://gateway.odin.4players.io";
        /// <summary>
        /// Default text representation of UserData
        /// </summary>
        public static string UserDataText { get; set; } = "";

        /// <summary>
        /// Microphone default Sample-Rate
        /// </summary>
        public static MediaSampleRate DeviceSampleRate { get; set; } = MediaSampleRate.Device_Max;
        /// <summary>
        /// Microphone default Channel
        /// </summary>
        public static MediaChannels DeviceChannels { get; set; } = MediaChannels.Mono;

        /// <summary>
        /// Playback default Sample-Rate
        /// </summary>
        public static MediaSampleRate RemoteSampleRate { get; set; } = MediaSampleRate.Hz48000;
        /// <summary>
        /// Playback default Channel
        /// </summary>
        public static MediaChannels RemoteChannels { get; set; } = MediaChannels.Mono;

        #region Events
        /// <summary>
        /// Idicates whether the event is enabled by default
        /// </summary>
        public static bool PeerJoinedEvent = true;
        /// <summary>
        /// Idicates whether the event is enabled by default
        /// </summary>
        public static bool PeerLeftEvent = true;
        /// <summary>
        /// Idicates whether the event is enabled by default
        /// </summary>
        public static bool PeerUpdatedEvent = true;
        /// <summary>
        /// Idicates whether the event is enabled by default
        /// </summary>
        public static bool MediaAddedEvent = true;
        /// <summary>
        /// Idicates whether the event is enabled by default
        /// </summary>
        public static bool MediaRemovedEvent = true;
        /// <summary>
        /// Idicates whether the event is enabled by default
        /// </summary>
        public static bool RoomUpdatedEvent = true;
        /// <summary>
        /// Idicates whether the event is enabled by default
        /// </summary>
        public static bool MediaActiveStateChangedEvent = true;
        /// <summary>
        /// Idicates whether the event is enabled by default
        /// </summary>
        public static bool MessageReceivedEvent = true;
        #endregion Events
        /// <summary>
        /// JWT room token lifetime
        /// </summary>
        public static ulong TokenLifetime { get; set; } = 300;

        #region Apm
        /// <summary>
        /// Idicates whether the ApmConfig setting is enabled by default
        /// </summary>
        public static bool VoiceActivityDetection = true;
        /// <summary>
        /// Idicates the vad attack probability ApmConfig setting by default
        /// </summary>
        public static float VoiceActivityDetectionAttackProbability = 0.9f;
        /// <summary>
        /// Idicates the vad release probability ApmConfig setting by default
        /// </summary>
        public static float VoiceActivityDetectionReleaseProbability = 0.8f;
        /// <summary>
        /// Idicates whether the ApmConfig setting is enabled by default
        /// </summary>
        public static bool VolumeGate = false;
        /// <summary>
        /// Idicates the gate attack loudness ApmConfig setting by default
        /// </summary>
        public static float VolumeGateAttackLoudness = -30;
        /// <summary>
        /// Idicates the gate release loudness ApmConfig setting by default
        /// </summary>
        public static float VolumeGateReleaseLoudness = -40;
        /// <summary>
        /// Idicates whether the ApmConfig setting is enabled by default
        /// </summary>
        public static bool EchoCanceller = false;
        /// <summary>
        /// Idicates whether the ApmConfig setting is enabled by default
        /// </summary>
        public static bool HighPassFilter = false;
        /// <summary>
        /// Idicates whether the ApmConfig setting is enabled by default
        /// </summary>
        public static bool PreAmplifier = false;
        /// <summary>
        /// Idicates the level of noise suppression ApmConfig setting by default
        /// </summary>
        public static Core.Imports.NativeBindings.OdinNoiseSuppressionLevel NoiseSuppressionLevel = Core.Imports.NativeBindings.OdinNoiseSuppressionLevel.None;
        /// <summary>
        /// Idicates whether the ApmConfig setting is enabled by default
        /// </summary>
        public static bool TransientSuppressor = false;
        /// <summary>
        /// Idicates whether the ApmConfig setting is enabled by default
        /// </summary>
        public static bool GainController = false;
        #endregion Apm
    }
}
