using OdinNative.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OdinNative.Odin.Media
{
    /// <summary>
    /// Representation of a write only audio stream
    /// </summary>
    public class MicrophoneStream : MediaStream
    {
        public bool IsMuted { get; private set; }
        private bool MutedState = false;

        public MicrophoneStream()
            : base(new OdinMediaConfig(OdinDefaults.RemoteSampleRate, OdinDefaults.RemoteChannels))  // Defaults match server config
        { }

        public MicrophoneStream(OdinMediaConfig config)
            : base(config)
        { }

        public void MuteStream(bool mute)
        {
            if (IsMuted && mute)
                return;

            IsMuted = mute;
            MutedState = false;
        }

        public override void AudioPushData(float[] buffer, int length)
        {
            if (IsMuted)
            {
                if (!MutedState)
                {
                    MutedState = true;
                    Array.Clear(buffer, 0, length);
                    base.AudioPushData(buffer, length);
                }
                return;
            }

            base.AudioPushData(buffer, length);
        }

        /// <summary>
        /// AudioReadData and AudioReadDataAsync are not supported!
        /// </summary>
        /// <remarks>This will only work for remote/output streams.</remarks>
        /// <param name="stats">Audio stream statistics</param>
        /// <exception cref="OdinWrapperException"></exception>
        /// <returns>throws OdinWrapperException</returns>
        public override bool AudioStats(out Core.Imports.NativeBindings.OdinAudioStreamStats stats)
        {
            throw new OdinWrapperException("Microphone streams are always local! This will only work for remote/output streams.",
                new NotSupportedException("AudioStats, AudioReadData, AudioReadDataTask and AudioReadDataAsync are not supported!"));
        }

        /// <summary>
        /// AudioReset is not supported!
        /// </summary>
        /// <returns>throws OdinWrapperException</returns>
        /// <exception cref="OdinWrapperException"></exception>
        public override bool AudioReset()
        {
            throw new OdinWrapperException("Microphone streams can not be reset!",
                new NotSupportedException("AudioStats, AudioReadData, AudioReadDataTask, AudioReset and AudioReadDataAsync are not supported!"));
        }

        /// <summary>
        /// AudioReadData is not supported!
        /// </summary>
        /// <remarks>Microphone streams are always writeonly</remarks>
        /// <param name="buffer"></param>
        /// <exception cref="OdinWrapperException"></exception>
        /// <returns>throws OdinWrapperException</returns>
        public override uint AudioReadData(float[] buffer)
        {
            throw new OdinWrapperException("Microphone streams are always writeonly!",
                new NotSupportedException("AudioStats, AudioReadData, AudioReadDataTask and AudioReadDataAsync are not supported!"));
        }
        /// <summary>
        /// AudioReadDataTask is not supported!
        /// </summary>
        /// <remarks>Microphone streams are always writeonly</remarks>
        /// <param name="buffer"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="OdinWrapperException"></exception>
        /// <returns>throws OdinWrapperException</returns>
        public override Task<uint> AudioReadDataTask(float[] buffer, CancellationToken cancellationToken)
        {
            throw new OdinWrapperException("Microphone streams are always writeonly!",
                new NotSupportedException("AudioStats, AudioReadData, AudioReadDataTask and AudioReadDataAsync are not supported!"));
        }
        /// <summary>
        /// AudioReadDataAsync is not supported!
        /// </summary>
        /// <remarks>Microphone streams are always writeonly</remarks>
        /// <param name="buffer"></param>
        /// <exception cref="OdinWrapperException"></exception>
        /// <returns>throws OdinWrapperException</returns>
        public override Task<uint> AudioReadDataAsync(float[] buffer)
        {
            throw new OdinWrapperException("Microphone streams are always writeonly!",
                new NotSupportedException("AudioStats, AudioReadData, AudioReadDataTask and AudioReadDataAsync are not supported!"));
        }
    }
}
