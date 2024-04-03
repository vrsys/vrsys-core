using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OdinNative.Core.Imports.NativeBindings;

namespace OdinNative.Core
{
    /// <summary>
    /// ODIN audio stream configuration
    /// </summary>
    public class OdinMediaConfig
    {
        /// <summary>
        /// The number audio samples carried per second in Hz
        /// </summary>
        public MediaSampleRate SampleRate
        {
            get { return (MediaSampleRate)sampleRate; }
            set { sampleRate = (uint)value; }
        }

        /// <summary>
        /// The number of audio channels
        /// </summary>
        public MediaChannels Channels
        {
            get { return (MediaChannels)channelCount; }
            set { channelCount = (byte)value; }
        }

        private uint sampleRate
        {
            get { return AudioStreamConfig.sample_rate; }
            set { AudioStreamConfig.sample_rate = value; }
        }

        private byte channelCount
        {
            get { return AudioStreamConfig.channel_count; }
            set { AudioStreamConfig.channel_count = value; }
        }

        internal bool RemoteConfig { get; private set; }

        internal OdinAudioStreamConfig GetOdinAudioStreamConfig() => AudioStreamConfig;

        private OdinAudioStreamConfig AudioStreamConfig = new OdinAudioStreamConfig();

        private OdinMediaConfig(OdinAudioStreamConfig config) : this(config.sample_rate, config.channel_count, true) { }
        /// <summary>
        /// MediaConfig for <see cref="OdinNative.Odin.Media.MediaStream"></see>
        /// </summary>
        /// <param name="rate">stream samplerate</param>
        /// <param name="channels">stream channels</param>
        public OdinMediaConfig(MediaSampleRate rate, MediaChannels channels) : this((uint)rate, (byte)channels, false) { }
        internal OdinMediaConfig(uint rate, byte channels, bool remote = false)
        {
            sampleRate = rate;
            channelCount = channels;
            RemoteConfig = remote;
        }

        /// <summary>
        /// Debug
        /// </summary>
        /// <returns>info</returns>
        public override string ToString()
        {
            return $"{nameof(OdinMediaConfig)} (Remote {RemoteConfig}):" +
                $", {nameof(SampleRate)} {Enum.GetName(typeof(MediaSampleRate), SampleRate)}" +
                $", {nameof(Channels)} {Enum.GetName(typeof(MediaChannels), Channels)}";
        }
    }

    /// <summary>
    /// Supported audio sample rate values
    /// </summary>
    public enum MediaSampleRate : uint
    {
        /// <summary>
        /// Hardware device min samplerate
        /// </summary>
        /// <remarks>Should currently not be used!</remarks>
        Device_Min,
        /// <summary>
        /// Hardware device max samplerate
        /// </summary>
        /// <remarks>Should currently not be used!</remarks>
        Device_Max,
        /// <summary>
        /// 8khz
        /// </summary>
        Hz8000 = 8000,
        /// <summary>
        /// 11.025khz
        /// </summary>
        Hz11025 = 11025,
        /// <summary>
        /// 12khz
        /// </summary>
        Hz12000 = 12000,
        /// <summary>
        /// 16khz
        /// </summary>
        Hz16000 = 16000,
        /// <summary>
        /// 22.05khz
        /// </summary>
        Hz22050 = 22050,
        /// <summary>
        /// 24khz
        /// </summary>
        Hz24000 = 24000,
        /// <summary>
        /// 32khz
        /// </summary>
        Hz32000 = 32000,
        /// <summary>
        /// 44.1khz
        /// </summary>
        Hz44100 = 44100,
        /// <summary>
        /// 48khz
        /// </summary>
        Hz48000 = 48000,
        /// <summary>
        /// 96khz
        /// </summary>
        Hz96000 = 96000,
        /// <summary>
        /// 128khz
        /// </summary>
        Hz128000 = 128000,
        /// <summary>
        /// 144khz
        /// </summary>
        Hz144000 = 144000,
        /// <summary>
        /// 192khz
        /// </summary>
        Hz192000 = 192000,
    }

    /// <summary>
    /// Supported values for audio channel count
    /// </summary>
    public enum MediaChannels : byte
    {
        /// <summary>
        /// Defines a single (monaural) channel
        /// </summary>
        Mono = 1,
        /// <summary>
        /// Defines two (stereo) channels
        /// </summary>
        Stereo = 2,
        //DolbySurround = 3,  // Defines three channels
        //DolbyProLogic = 4,  // Defines four channels
        //DolbyDigital = 6,   // Defines six channels
    }
}
