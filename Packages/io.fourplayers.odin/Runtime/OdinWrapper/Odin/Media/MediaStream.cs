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
    /// Base stream
    /// </summary>
    /// <remarks>Video is currently not supported</remarks>
    public abstract class MediaStream : IVideoStream, IAudioStream, IDisposable
    {
        /// <summary>
        /// Handle ID
        /// </summary>
        public long Id { get; internal set; }
        /// <summary>
        /// Internal ID of the media stream
        /// </summary>
        internal ushort MediaId => GetMediaId();
        internal ulong PeerId => GetPeerId();
        /// <summary>
        /// Audio config of the media stream
        /// </summary>
        public OdinMediaConfig MediaConfig { get; private set; }
        /// <summary>
        /// Control of async read and write tasks
        /// </summary>
        public CancellationTokenSource CancellationSource { get; internal set; }
        /// <summary>
        /// Indicates wether or not the media stream is active and sending/receiving data
        /// </summary>
        public bool IsActive { get; internal set; }
        /// <summary>
        /// Indicates wether or not the last media stream api call result is an error code
        /// </summary>
        public bool HasErrors { get; internal set; }
        /// <summary>
        /// Indicates wether or not the media stream handle is invalid or closed
        /// </summary>
        public bool IsInvalid { get { return Handle == null || Handle.IsInvalid || Handle.IsClosed; } }
        internal StreamHandle Handle { get; private set; }
        private ResamplerHandle ResamplerHandle;

        /// <summary>
        /// Base stream
        /// </summary>
        /// <param name="config">audio stream configuration</param>
        public MediaStream(OdinMediaConfig config)
            : this(config, OdinLibrary.Api.AudioStreamCreate(config))
        { }

        internal MediaStream(OdinMediaConfig config, StreamHandle handle)
        {
            Id = ((IntPtr)handle).ToInt64();
            MediaConfig = config;
            Handle = handle;
            CancellationSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Returns the media stream ID and updates <see cref="Id"/>.
        /// </summary>
        /// <returns>id value</returns>
        public ushort GetMediaId()
        {
            uint result = OdinLibrary.Api.MediaStreamMediaId(Handle, out ushort mediaId);
            if(Utility.IsError(result)) HasErrors = true;
            return mediaId;
        }

        /// <summary>
        /// Returns the ID of the peer that own this media stream.
        /// </summary>
        /// <returns>id</returns>
        public ulong GetPeerId()
        {
            uint result = OdinLibrary.Api.MediaStreamPeerId(Handle, out ulong peerId);
            HasErrors = Utility.IsError(result);
            return peerId;
        }

        /// <summary>
        /// Adds this media stream to a room.
        /// </summary>
        /// <param name="roomHandle"><see cref="Room.Room"/> handle</param>
        /// <returns>true on success or false</returns>
        internal bool AddMediaToRoom(RoomHandle roomHandle)
        {
            uint result = OdinLibrary.Api.RoomAddMedia(roomHandle, Handle);
            HasErrors = Utility.IsError(result);
            return result == Utility.OK;
        }

        /// <summary>
        /// Sends data to the audio stream. The data has to be interleaved [-1, 1] float data.
        /// </summary>
        /// <param name="buffer">audio data</param>
        /// <param name="length">bytes to write</param>
        public virtual void AudioPushData(float[] buffer, int length)
        {
            OdinLibrary.Api.AudioPushData(Handle, buffer, length);
        }

        /// <summary>
        /// Sends data to the audio stream. The data has to be interleaved [-1, 1] float data.
        /// </summary>
        /// <param name="buffer">audio data</param>
        public virtual void AudioPushData(float[] buffer)
        {
            AudioPushData(buffer, buffer.Length);
        }

        /// <summary>
        /// Sends data to the audio stream. The data has to be interleaved [-1, 1] float data.
        /// </summary>
        /// <param name="buffer">interleaved audio data</param>
        /// <param name="cancellationToken"></param>
        public virtual Task AudioPushDataTask(float[] buffer, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() => {
                    AudioPushData(buffer, buffer.Length);
                }
            , cancellationToken);
        }

        /// <summary>
        /// Sends audio data using a custom <see cref="CancellationSource"/>.
        /// The data has to be interleaved [-1, 1] float data.
        /// </summary>
        /// <param name="buffer">interleaved audio data</param>
        public virtual async void AudioPushDataAsync(float[] buffer)
        {
            await AudioPushDataTask(buffer, CancellationSource.Token);
        }

        public abstract bool AudioReset();

        /// <summary>
        /// Reads data from the audio stream.
        /// </summary>
        /// <param name="buffer">buffer to write into</param>
        /// <returns>count of written bytes into buffer</returns>
        public virtual uint AudioReadData(float[] buffer)
        {
            return AudioReadData(buffer, buffer.Length);
        }

        /// <summary>
        /// Retrieves statistics for the specified <see cref="MediaStream"/>.
        /// </summary>
        /// <remarks>This will only work for remote/output streams.</remarks>
        /// <param name="stats">Audio stream statistics</param>
        /// <returns>true on success or false</returns>
        public virtual bool AudioStats(out Core.Imports.NativeBindings.OdinAudioStreamStats stats)
        {
            uint result = OdinLibrary.Api.AudioStats(Handle, out stats);
            HasErrors = Utility.IsError(result);
            return result == Utility.OK;
        }

        /// <summary>
        /// Reads data from the audio stream.
        /// </summary>
        /// <param name="buffer">buffer to write into</param>
        /// <param name="length">bytes to read</param>
        /// <returns>count of written bytes into buffer</returns>
        public virtual uint AudioReadData(float[] buffer, int length)
        {
            uint result = OdinLibrary.Api.AudioReadData(Handle, buffer, length);
            HasErrors = Utility.IsError(result);
            return result;
        }

        /// <summary>
        /// Reads data from the audio stream.
        /// </summary>
        /// <param name="buffer">buffer to write into</param>
        /// <param name="cancellationToken"></param>
        /// <returns>count of written bytes into buffer</returns>
        public virtual Task<uint> AudioReadDataTask(float[] buffer, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() => {
                 uint result = OdinLibrary.Api.AudioReadData(Handle, buffer, buffer.Length);
                 HasErrors = Utility.IsError(result);
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Read audio data using a custom custom <see cref="CancellationSource"/>.
        /// </summary>
        /// <param name="buffer">buffer to write into</param>
        /// <returns>count of written bytes into buffer</returns>
        public virtual async Task<uint> AudioReadDataAsync(float[] buffer)
        {
            return await AudioReadDataTask(buffer, CancellationSource.Token);
        }
        
        

        /// <summary>
        /// Set a resampler and resamples a chunk of audio.
        /// </summary>
        /// <remarks>This is intended for situations where the audio output pipeline doesn't support 48kHz.</remarks>
        /// <param name="input">current buffer</param>
        /// <param name="outputSampleRate">resample samplerate</param>
        /// <param name="output">resampled buffer</param>
        /// <param name="capacity">size of resampled buffer</param>
        /// <remarks>The current buffer samplesrate is expected to be <see cref="OdinNative.Odin.OdinDefaults.RemoteSampleRate"/> with <see cref="OdinNative.Odin.OdinDefaults.RemoteChannels"/></remarks>
        /// <returns>true on success or false if missmatch of capacity i.e. errorcode</returns>
        public virtual bool AudioResample(float[] input, uint outputSampleRate, out float[] output, out int capacity)
        {
            if (ResamplerHandle == null)
                ResamplerHandle = OdinLibrary.Api.ResamplerCreate((uint)OdinDefaults.RemoteSampleRate, outputSampleRate, (short)OdinDefaults.RemoteChannels);
            else if (ResamplerHandle.ToRate != outputSampleRate)
            {
                ResamplerHandle.Dispose();
                ResamplerHandle = null;
                return AudioResample(input, outputSampleRate, out output, out capacity);
            }

            output = new float[((ResamplerHandle.ToRate / ResamplerHandle.FromRate) * input.Length) * ResamplerHandle.Channels];
            capacity = output.Length;
            uint result = OdinLibrary.Api.ResamplerProcess(ResamplerHandle, input, input.Length, output, ref capacity);
            HasErrors = Utility.IsError(result);
            return result == capacity;
        }

        /// <summary>
        /// Set a resampler and resamples a chunk of audio.
        /// </summary>
        /// <remarks>This is intended for situations where the audio output pipeline doesn't support 48kHz.</remarks>
        /// <param name="input">source buffer</param>
        /// <param name="outputSampleRate">to samplerate</param>
        /// <param name="output">target buffer</param>
        /// <param name="capacity">target capacity</param>
        /// <returns>sample count on success or errorcode on failure</returns>
        public virtual uint AudioResample(float[] input, uint outputSampleRate, float[] output, int capacity)
        {
            if (ResamplerHandle == null)
                ResamplerHandle = OdinLibrary.Api.ResamplerCreate((uint)OdinDefaults.RemoteSampleRate, outputSampleRate, (short)OdinDefaults.RemoteChannels);
            else if (ResamplerHandle.ToRate != outputSampleRate)
            {
                ResamplerHandle.Dispose();
                ResamplerHandle = null;
                return AudioResample(input, outputSampleRate, output, capacity);
            }

            uint result = OdinLibrary.Api.ResamplerProcess(ResamplerHandle, input, input.Length, output, ref capacity);
            HasErrors = Utility.IsError(result);
            return result;
        }

        /// <summary>
        /// Cancel the custom <see cref="CancellationSource"/>.
        /// </summary>
        /// <returns>true if the token was canceled or false</returns>
        public bool Cancel()
        {
            if (CancellationSource.Token.CanBeCanceled)
            {
                CancellationSource.Cancel();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Debug
        /// </summary>
        /// <returns>info</returns>
        public override string ToString()
        {
            return $"{nameof(MediaStream)}: {nameof(Id)} {Id}, {nameof(MediaId)} {MediaId}, {nameof(PeerId)} {PeerId}, {nameof(HasErrors)} {HasErrors} {nameof(IsInvalid)} {IsInvalid}\n\t- {nameof(MediaConfig)} {MediaConfig?.ToString()}";
        }

        private bool disposedValue;
        /// <summary>
        /// On dispose will free the stream and resampler
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    CancellationSource?.Dispose();
                    MediaConfig = null;
                    Handle?.Close();
                    Handle = null;
                    ResamplerHandle?.Close();
                    ResamplerHandle = null;
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Default deconstructor
        /// </summary>
        ~MediaStream()
        {
            Dispose(disposing: false);
        }

        /// <summary>
        /// On dispose will free the stream and resampler
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
