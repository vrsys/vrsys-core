using OdinNative.Core;
using OdinNative.Core.Handles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OdinNative.Odin.Media
{
    /// <summary>
    /// Representation of a read only audio stream
    /// </summary>
    public class PlaybackStream : MediaStream
    {
        internal PlaybackStream(OdinMediaConfig config, StreamHandle stream) 
            : base(config, stream)
        { }

        internal PlaybackStream(StreamHandle stream)
            : base(new OdinMediaConfig(OdinDefaults.RemoteSampleRate, OdinDefaults.RemoteChannels), stream) // Defaults match server config
        { }

        /// <summary>
        /// AudioPushDataTask and AudioPushDataAsync are not supported!
        /// </summary>
        /// <remarks>Remote streams are always readonly</remarks>
        /// <param name="buffer"></param>
        /// <exception cref="OdinWrapperException"></exception>
        public override void AudioPushData(float[] buffer)
        {
            throw new OdinWrapperException("Remote streams are always readonly!",
                new NotSupportedException("AudioPushData, AudioPushDataTask and AudioPushDataAsync are not supported!"));
        }
        /// <summary>
        /// AudioPushDataTask and AudioPushDataAsync are not supported!
        /// </summary>
        /// <remarks>Remote streams are always readonly</remarks>
        /// <param name="buffer"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="OdinWrapperException"></exception>
        public override Task AudioPushDataTask(float[] buffer, CancellationToken cancellationToken)
        {
            throw new OdinWrapperException("Remote streams are always readonly!",
                new NotSupportedException("AudioPushData, AudioPushDataTask and AudioPushDataAsync are not supported!"));
        }
        /// <summary>
        /// AudioPushDataTask and AudioPushDataAsync are not supported!
        /// </summary>
        /// <remarks>Remote streams are always readonly</remarks>
        /// <param name="buffer"></param>
        /// <exception cref="OdinWrapperException"></exception>
        public override void AudioPushDataAsync(float[] buffer)
        {
            throw new OdinWrapperException("Remote streams are always readonly!",
                new NotSupportedException("AudioPushData, AudioPushDataTask and AudioPushDataAsync are not supported!"));
        }
        
        /// <summary>
        /// This operation resets the internal Opus encoder/decoder, ensuring a clean state. Additionally, it clears internal buffers, providing a fresh start.
        /// </summary>
        /// <returns>True, if reset was successful</returns>
        public override bool AudioReset()
        {
            bool result = false;
            if(!Handle.IsInvalid && !Handle.IsClosed)
            {
                uint errorCode = OdinLibrary.Api.AudioReset(Handle);
                HasErrors = Utility.IsError(errorCode);
                result = errorCode == Utility.OK;
            }

            return result;
        }
    }
}
