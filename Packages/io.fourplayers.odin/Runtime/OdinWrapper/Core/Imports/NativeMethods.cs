using OdinNative.Core.Handles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static OdinNative.Core.Imports.NativeBindings;

namespace OdinNative.Core.Imports
{
    internal partial class NativeMethods
    {
        readonly OdinHandle Handle;

        public NativeMethods(OdinHandle handle)
        {
            Handle = handle;

            handle.GetLibraryMethod("odin_startup", out _OdinStartup);
            handle.GetLibraryMethod("odin_startup_ex", out _OdinStartupEx);
            handle.GetLibraryMethod("odin_shutdown", out _OdinShutdown);
            handle.GetLibraryMethod("odin_room_create", out _OdinRoomCreate);
            handle.GetLibraryMethod("odin_audio_set_stream_delay", out _OdinAudioSetStreamDelay);
            handle.GetLibraryMethod("odin_room_configure_apm", out _OdinRoomConfigureApm);
            handle.GetLibraryMethod("odin_room_close", out _OdinRoomClose);
            handle.GetLibraryMethod("odin_room_destroy", out _OdinRoomDestroy);
            handle.GetLibraryMethod("odin_room_id", out _OdinRoomId);
            handle.GetLibraryMethod("odin_room_customer", out _OdinRoomCustomer);
            handle.GetLibraryMethod("odin_room_peer_id", out _OdinRoomPeerId);
            handle.GetLibraryMethod("odin_room_connection_stats", out _OdinRoomConnectionStats);
            handle.GetLibraryMethod("odin_room_join", out _OdinRoomJoin);
            handle.GetLibraryMethod("odin_room_add_media", out _OdinRoomAddMedia);
            handle.GetLibraryMethod("odin_room_update_peer_user_data", out _OdinRoomUpdateUserData);
            handle.GetLibraryMethod("odin_room_update_position", out _OdinRoomUpdatePosition);
            handle.GetLibraryMethod("odin_room_set_event_callback", out _OdinRoomSetEventCallback);
            handle.GetLibraryMethod("odin_room_set_position_scale", out _OdinRoomSetPositionScale);
            handle.GetLibraryMethod("odin_room_send_message", out _OdinRoomSendMessage);
            handle.GetLibraryMethod("odin_video_stream_create", out _OdinVideoStreamCreate);
            handle.GetLibraryMethod("odin_audio_stream_create", out _OdinAudioStreamCreate);
            handle.GetLibraryMethod("odin_media_stream_resume", out _OdinMediaStreamResume);
            handle.GetLibraryMethod("odin_media_stream_pause", out _OdinMediaStreamPause);
            handle.GetLibraryMethod("odin_media_stream_destroy", out _OdinMediaStreamDestroy);
            handle.GetLibraryMethod("odin_media_stream_type", out _OdinMediaStreamType);
            handle.GetLibraryMethod("odin_media_stream_media_id", out _OdinMediaStreamMediaId);
            handle.GetLibraryMethod("odin_media_stream_peer_id", out _OdinMediaStreamPeerId);
            handle.GetLibraryMethod("odin_audio_push_data", out _OdinAudioPushData);
            handle.GetLibraryMethod("odin_audio_read_data", out _OdinAudioReadData);
            handle.GetLibraryMethod("odin_audio_reset", out _OdinAudioReset);
            handle.GetLibraryMethod("odin_audio_stats", out _OdinAudioStats);
            handle.GetLibraryMethod("odin_audio_mix_streams", out _OdinAudioMixStreams);
            handle.GetLibraryMethod("odin_audio_process_reverse", out _OdinAudioProcessReverse);
            handle.GetLibraryMethod("odin_resampler_create", out _OdinResamplerCreate);
            handle.GetLibraryMethod("odin_resampler_process", out _OdinResamplerProcess);
            handle.GetLibraryMethod("odin_resampler_destroy", out _OdinResamplerDestroy);
            handle.GetLibraryMethod("odin_error_format", out _OdinErrorFormat);
            handle.GetLibraryMethod("odin_access_key_generate", out _OdinAccessKeyGenerate);
            handle.GetLibraryMethod("odin_access_key_public_key", out _OdinAccessKeyPublicKey);
            handle.GetLibraryMethod("odin_access_key_secret_key", out _OdinAccessKeySecretKey);
            handle.GetLibraryMethod("odin_token_generator_create", out _OdinTokenGeneratorCreate);
            handle.GetLibraryMethod("odin_token_generator_destroy", out _OdinTokenGeneratorDestroy);
            handle.GetLibraryMethod("odin_token_generator_create_token", out _OdinTokenGeneratorCreateToken);
            handle.GetLibraryMethod("odin_token_generator_create_token_ex", out _OdinTokenGeneratorCreateTokenEx);
            handle.GetLibraryMethod("odin_is_error", out _OdinIsError);
        }

        private struct LockObject : IDisposable
        {
            private OdinHandle Handle;

            public LockObject(OdinHandle handle)
            {
                Handle = handle;
                bool success = false;
                Handle.DangerousAddRef(ref success);
                if (success == false)
                    throw new ObjectDisposedException(typeof(OdinLibrary).FullName);
            }
            void IDisposable.Dispose()
            {
                Handle.DangerousRelease();
            }
        }

        private LockObject Lock
        {
            get { return new LockObject(Handle); }
        }

        private void CheckAndThrow(uint error, string message = null)
        {
            if (Check(error))
#pragma warning disable CS0618 // Type or member is obsolete
                Utility.Throw(OdinLibrary.CreateException(error, message));
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private bool Check(uint error)
        {
            return IsError(error);
        }

        /// <summary>
        /// Internal function to convert a specified error code into a human-readable error message
        /// </summary>
        /// <param name="error">string buffer</param>
        /// <param name="bufferSize">max string buffer size</param>
        /// <returns>Error message</returns>
        internal string GetErrorMessage(uint error, int bufferSize = 1024)
        {
            using (Lock)
            {
                IntPtr _stringPointer = Marshal.AllocHGlobal(bufferSize);
                uint size = ErrorFormat(error, _stringPointer, bufferSize);
                byte[] buffer = new byte[size];
                Marshal.Copy(_stringPointer, buffer, 0, buffer.Length);
                Marshal.FreeHGlobal(_stringPointer);
                return Native.Encoding.GetString(buffer);
            }
        }

        /// <summary>
        /// Internal function to determines if the specified error code identifies an actual error
        /// </summary>
        /// <param name="error">error code</param>
        /// <returns>true if error</returns>
        internal bool InternalIsError(uint error)
        {
            return Utility.IsError(error);
        }
    }
}
