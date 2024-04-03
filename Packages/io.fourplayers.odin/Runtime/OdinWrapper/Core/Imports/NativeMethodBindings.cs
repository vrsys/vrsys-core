using OdinNative.Core.Handles;
using OdinNative.Odin.Media;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using static OdinNative.Core.Imports.NativeBindings;

namespace OdinNative.Core.Imports
{
    internal partial class NativeMethods
    {
        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate bool OdinStartupDelegate(string version);
        readonly OdinStartupDelegate _OdinStartup;
        /// <summary>
        /// Starts the internal ODIN client runtime and verifies thatthe correct API header file is used.
        /// This is ref-counted so you need matching calls of startup and shutdown in your application.
        /// A lot of the functions in the API require a running ODIN runtime.With the only exception being
        /// the `resampler`,`access_key` and `token_generator` related functions.
        /// <list type="table">
        /// <listheader><term>OdinRoom</term><description>RoomHandle for medias and events</description></listheader>
        /// <item>Create <description><see cref="RoomCreate"/></description></item>
        /// <item>Close <description><see cref="RoomClose"/></description></item>
        /// <item>Destroy <description><see cref="RoomDestroy"/></description></item>
        /// <item></item>
        /// <listheader><term>OdinMediaStream</term><description>StreamHandle for audio and video</description></listheader>
        /// <item>Create <description><see cref="AudioStreamCreate"/></description></item>
        /// <item>Destroy <description><see cref="MediaStreamDestroy(StreamHandle)"/></description></item>
        /// <item></item>
        /// <listheader><term>Stop</term><description> Stops the internal ODIN client runtime <see cref="Shutdown"/></description></listheader>
        /// </list>
        /// </summary>
        /// <remarks>Use <see cref="OdinNative.Core.Imports.NativeBindings.OdinVersion"/> to pass the `version` argument.</remarks>
        /// <returns>false on Version mismatch</returns>
        private bool Startup(string version = OdinNative.Core.Imports.NativeBindings.OdinVersion)
        {
            using (Lock)
                return _OdinStartup(version);
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate bool OdinStartupExDelegate(string version, NativeBindings.OdinAudioStreamConfig config);
        readonly OdinStartupExDelegate _OdinStartupEx;
        /// <summary>
        /// Starts the internal ODIN client runtime and allows passing the sample rate and channel layout
        /// for audio output.This is ref-counted so you need matching calls of startup and shutdown in your
        /// application.
        /// </summary>
        /// <remarks>Make sure to use the same settings on consecutive calls of this function.</remarks>
        private bool StartupEx(NativeBindings.OdinAudioStreamConfig config, string version = OdinNative.Core.Imports.NativeBindings.OdinVersion)
        {
            using (Lock)
                return _OdinStartupEx(version, config);
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate void OdinShutdownDelegate();
        readonly OdinShutdownDelegate _OdinShutdown;
        /// <summary>
        /// Stops native ODIN runtime threads that were previously started with <see cref="Startup"/>
        /// </summary>
        private void Shutdown()
        {
            using (Lock)
                _OdinShutdown();
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinGenerateAccessKeyDelegate([In, Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder stringBuffer, [In] int bufferLength);
        readonly OdinGenerateAccessKeyDelegate _OdinAccessKeyGenerate;
        /// <summary>
        /// Generates an access key required to access the ODIN network
        /// </summary>
        /// <param name="key">access key</param>
        /// <param name="capacity">max string buffer size</param>
        /// <returns>string length or error code that is readable with <see cref="ErrorFormat"/></returns>
        internal uint GenerateAccessKey(out string key, int capacity = 128)
        {
            using (Lock)
            {
                StringBuilder stringBuilder = new StringBuilder(capacity);
                uint error = _OdinAccessKeyGenerate(stringBuilder, capacity);
                key = stringBuilder.ToString();
                if (InternalIsError(error))
                    CheckAndThrow(error);

                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinAccessKeyPublicKeyDelegate(string accessKey, [In, Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder stringBuffer, [In] int bufferLength);
        readonly OdinAccessKeyPublicKeyDelegate _OdinAccessKeyPublicKey;
        private uint LoadPublicKey(string accessKey, out string key, int capacity = 128)
        {
            using (Lock)
            {
                StringBuilder stringBuilder = new StringBuilder(capacity);
                uint error = _OdinAccessKeyPublicKey(accessKey, stringBuilder, capacity);
                key = stringBuilder.ToString();
                if (InternalIsError(error))
                    CheckAndThrow(error);

                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinAccessKeySecretKeyDelegate(string accessKey, [In, Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder stringBuffer, [In] int bufferLength);
        readonly OdinAccessKeySecretKeyDelegate _OdinAccessKeySecretKey;
        private uint LoadSecretKey(string accessKey, out string key, int capacity = 128)
        {
            using (Lock)
            {
                StringBuilder stringBuilder = new StringBuilder(capacity);
                uint error = _OdinAccessKeySecretKey(accessKey, stringBuilder, capacity);
                key = stringBuilder.ToString();
                if (InternalIsError(error))
                    CheckAndThrow(error);

                return error;
            }
        }

        #region Token Generator
        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate IntPtr OdinTokenGeneratorCreateDelegate(string accessKey);
        readonly OdinTokenGeneratorCreateDelegate _OdinTokenGeneratorCreate;
        /// <summary>
        /// Creates a new ODIN token generator
        /// </summary>
        /// <param name="accessKey">*const c_char</param>
        /// <returns><see cref="TokenGeneratorHandle"/> always owns the <see cref="IntPtr"/> handle</returns>
        public TokenGeneratorHandle TokenGeneratorCreate(string accessKey)
        {
            using (Lock)
            {
                IntPtr handle = _OdinTokenGeneratorCreate(accessKey);
                return new TokenGeneratorHandle(handle, _OdinTokenGeneratorDestroy);
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate void OdinTokenGeneratorDestroyDelegate(IntPtr tokenGenerator);
        readonly OdinTokenGeneratorDestroyDelegate _OdinTokenGeneratorDestroy;
        /// <summary>
        /// Destroys an allocated ODIN token generator
        /// </summary>
        /// <param name="tokenGenerator">*mut OdinTokenGenerator</param>
        public void TokenGeneratorDestroy(TokenGeneratorHandle tokenGenerator)
        {
            using (Lock)
                _OdinTokenGeneratorDestroy(tokenGenerator);
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinTokenGeneratorCreateTokenDelegate(IntPtr tokenGenerator, string roomId, string userId, [In, Out] [MarshalAs(UnmanagedType.LPStr)] StringBuilder stringBuffer, [In] int bufferLength);
        readonly OdinTokenGeneratorCreateTokenDelegate _OdinTokenGeneratorCreateToken;
        /// <summary>
        /// Generates a signed JWT, which can be used by an ODIN client to join a room
        /// </summary>
        /// <param name="tokenGenerator">allocated TokenGenerator</param>
        /// <param name="roomId">*const c_char</param>
        /// <param name="userId">*const c_char</param>
        /// <param name="token">char *</param>
        /// <param name="capacity">size *mut</param>
        /// <returns>string length or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint TokenGeneratorCreateToken(TokenGeneratorHandle tokenGenerator, string roomId, string userId, out string token, int capacity = 512)
        {
            using (Lock)
            {
                StringBuilder stringBuilder = new StringBuilder(capacity);
                uint error = _OdinTokenGeneratorCreateToken(tokenGenerator, roomId, userId, stringBuilder, capacity);
                token = stringBuilder.ToString();
                if (InternalIsError(error))
                    CheckAndThrow(error, $"{tokenGenerator} roomId {roomId} userId {userId}");

                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinTokenGeneratorCreateTokenExDelegate(IntPtr tokenGenerator, string roomId, string userId, OdinTokenOptions options, [In, Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder stringBuffer, [In] int bufferLength);
        readonly OdinTokenGeneratorCreateTokenExDelegate _OdinTokenGeneratorCreateTokenEx;
        /// <summary>
        /// Generates a signed JWT just like <see cref="TokenGeneratorCreateToken"/> and allows passing custom options for advanced use-cases
        /// </summary>
        /// <param name="tokenGenerator">allocated TokenGenerator</param>
        /// <param name="roomId">*const c_char</param>
        /// <param name="userId">*const c_char</param>
        /// <param name="options">const struct OdinTokenOptions *</param>
        /// <param name="token">char *</param>
        /// <param name="capacity">size *mut</param>
        /// <returns>string length or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint TokenGeneratorCreateTokenEx(TokenGeneratorHandle tokenGenerator, string roomId, string userId, OdinTokenOptions options, out string token, int capacity = 512)
        {
            using (Lock)
            {
                StringBuilder stringBuilder = new StringBuilder(capacity);
                uint error = _OdinTokenGeneratorCreateTokenEx(tokenGenerator, roomId, userId, options, stringBuilder, capacity);
                token = stringBuilder.ToString();
                if (InternalIsError(error))
                    CheckAndThrow(error, $"{tokenGenerator} roomId {roomId} userId {userId}");

                return error;
            }
        }
        #endregion

        #region Room
        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinAudioSetStreamDelayDelegate(IntPtr room, UInt64 ms);
        readonly OdinAudioSetStreamDelayDelegate _OdinAudioSetStreamDelay;
        /// <summary>
        /// Sets the delay estimate for the reverse stream used in the ODIN echo cancellation. This function
        /// is important in scenarios where the audio output and the audio input are not synchronized. An
        /// accurate delay value ensures that the echo canceller can correctly align the two audio streams,
        /// resulting in effective echo cancellation.
        /// </summary>
        /// <remarks>Improper delay values may lead to poor echo cancellation and thus degrade the quality of the audio communication.</remarks>
        /// <param name="room">*mut OdinRoom</param>
        /// <param name="delay_ms">delay in milliseconds</param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint AudioSetStreamDelay(RoomHandle room, ulong delay_ms)
        {
            using (Lock)
            {
                uint error = _OdinAudioSetStreamDelay(room, delay_ms);
                CheckAndThrow(error, $"{room} ms {delay_ms}");
                return error;
            }
        }


        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinRoomConfigureApmDelegate(IntPtr room, NativeBindings.OdinApmConfig apmConfig);
        readonly OdinRoomConfigureApmDelegate _OdinRoomConfigureApm;
        /// <summary>
        /// Set OdinRoomConfig <see cref="NativeBindings.OdinApmConfig"/> in the <see cref="RoomCreate"/> provided room
        /// </summary>
        /// <remarks>currently only returns 0</remarks>
        /// <param name="room">*mut OdinRoom</param>
        /// <param name="config"><see cref="OdinNative.Core.OdinRoomConfig"/></param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint RoomConfigure(RoomHandle room, OdinRoomConfig config)
        {
            using (Lock)
            {
                uint error = _OdinRoomConfigureApm(room, config.GetOdinApmConfig());
                CheckAndThrow(error, $"{room} config {config}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate IntPtr OdinRoomCreateDelegate();
        readonly OdinRoomCreateDelegate _OdinRoomCreate;
        /// <summary>
        /// Returns a pointer for a new ODIN room in an unconnected state. Please note, that this function
        /// will return `NULL` when the internal ODIN client runtime is not initialized or has already been
        /// terminated using `odin_shutdown`.
        /// </summary>
        /// <returns><see cref="RoomHandle"/> always owns the <see cref="IntPtr"/> handle</returns>
        public RoomHandle RoomCreate()
        {
            using (Lock)
            {
                IntPtr handle = _OdinRoomCreate();
                return new RoomHandle(handle, _OdinRoomDestroy);
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinRoomCloseDelegate(IntPtr room);
        readonly OdinRoomCloseDelegate _OdinRoomClose;
        /// <summary>
        ///  Closes the specified ODIN room handle, thus making our own peer leave the room on the server and closing the connection if needed.
        /// </summary>
        /// <param name="room">*mut OdinRoom</param>
        public uint RoomClose(RoomHandle room)
        {
            using (Lock)
            {
                uint error = _OdinRoomClose(room);
                CheckAndThrow(error, $"{room.ToString()}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinRoomDestroyDelegate(IntPtr room);
        readonly OdinRoomDestroyDelegate _OdinRoomDestroy;
        /// <summary>
        /// Destroys the specified ODIN room handle.
        /// </summary>
        /// <param name="room">*mut OdinRoom</param>
        public uint RoomDestroy(RoomHandle room)
        {
            using (Lock)
            {
                uint error = _OdinRoomDestroy(room);
                CheckAndThrow(error, $"{room.ToString()}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinRoomIdDelegate(IntPtr room, [In, Out] [MarshalAs(UnmanagedType.LPStr)] StringBuilder stringBuffer, [In] int bufferLength);
        readonly OdinRoomIdDelegate _OdinRoomId;
        /// <summary>
        /// Retrieves the room ID (e.g. the name of the room) from the specified `OdinRoomHandle`.
        /// </summary>
        /// <param name="room">*mut OdinRoom</param>
        /// <param name="roomId">room id string</param>
        /// <param name="capacity">max string buffer size</param>
        /// <returns>string length or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint RoomGetId(RoomHandle room, out string roomId, int capacity = 128)
        {
            using (Lock)
            {
                StringBuilder stringBuilder = new StringBuilder(capacity);
                uint error = _OdinRoomId(room, stringBuilder, capacity);
                roomId = stringBuilder.ToString();
                if(InternalIsError(error))
                    CheckAndThrow(error);

                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinRoomCustomerDelegate(IntPtr room, [In, Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder stringBuffer, [In] int bufferLength);
        readonly OdinRoomCustomerDelegate _OdinRoomCustomer;
        /// <summary>
        /// Retrieves the identifier of the customer the room is assigned to from the specified `OdinRoomHandle`.
        /// </summary>
        /// <param name="room">*mut OdinRoom</param>
        /// <param name="customer">customer string</param>
        /// <param name="capacity">max string buffer size</param>
        /// <returns>string length or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint RoomGetCustomer(RoomHandle room, out string customer, int capacity = 128)
        {
            using (Lock)
            {
                StringBuilder stringBuilder = new StringBuilder(capacity);
                uint error = _OdinRoomCustomer(room, stringBuilder, capacity);
                customer = stringBuilder.ToString();
                if (InternalIsError(error))
                    CheckAndThrow(error);

                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinRoomPeerIdDelegate(IntPtr room, [Out] out ulong out_peer_id);
        readonly OdinRoomPeerIdDelegate _OdinRoomPeerId;
        /// <summary>
        /// Retrieves your own peer ID from the specified `OdinRoomHandle`.
        /// </summary>
        /// <param name="room">*mut OdinRoom</param>
        /// <param name="peerId">uint64_t *</param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint RoomGetPeerId(RoomHandle room, out ulong peerId)
        {
            using (Lock)
            {
                uint error = _OdinRoomPeerId(room, out peerId);
                CheckAndThrow(error);
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinRoomConnectionStatsDelegate(IntPtr room, [Out] out OdinConnectionStats stats);
        readonly OdinRoomConnectionStatsDelegate _OdinRoomConnectionStats;
        /// <summary>
        /// Retrieves statistics for the underlying connection of the specified room handle.
        /// </summary>
        /// <param name="room">*mut OdinRoom</param>
        /// <param name="stats"><see cref="OdinNative.Core.Imports.NativeBindings.OdinConnectionStats"/>: Statistics for the underlying connection of a room.</param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint RoomConnectionStats(RoomHandle room, out OdinConnectionStats stats)
        {
            using (Lock)
            {
                uint error = _OdinRoomConnectionStats(room, out stats);
                CheckAndThrow(error, $"{room.ToString()}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinRoomJoinDelegate(IntPtr room, string url, string token);
        readonly OdinRoomJoinDelegate _OdinRoomJoin;
        /// <summary>
        /// Takes an URL to an ODIN gateway (e.g. `https://gateway.odin.4players.io`) and a signed room
        /// token obtained externally that authorizes the client to connect to a specific room.
        /// </summary>
        /// <param name="room">*mut OdinRoom</param>
        /// <param name="gatewayUrl">*const c_char</param>
        /// <param name="roomToken">*const c_char</param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint RoomJoin(RoomHandle room, string gatewayUrl, string roomToken)
        {
            using (Lock)
            {
                uint error = _OdinRoomJoin(room, gatewayUrl, roomToken);
                CheckAndThrow(error, $"{room} url: {gatewayUrl} token: {roomToken}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinRoomAddMediaDelegate(IntPtr room, IntPtr mediaStream);
        readonly OdinRoomAddMediaDelegate _OdinRoomAddMedia;
        /// <summary>
        /// Add a <see cref="OdinNative.Odin.Media.MediaStream"/> in the <see cref="RoomCreate"/> provided room.
        /// </summary>
        /// <param name="room">*mut OdinRoom</param>
        /// <param name="stream">*mut <see cref="OdinNative.Odin.Media.MediaStream"/></param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint RoomAddMedia(RoomHandle room, StreamHandle stream)
        {
            using (Lock)
            {
                uint error = _OdinRoomAddMedia(room, stream);
                CheckAndThrow(error, $"{room} {stream.ToString()}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinRoomSetPositionScaleDelegate(IntPtr room, float distance);
        readonly OdinRoomSetPositionScaleDelegate _OdinRoomSetPositionScale;
        /// <summary>
        /// Configures the allowed 'view' distance of peers in the specified `OdinRoom`. Per default,
        /// the room will use a distance of `1.0` fo proximity calculation.You can change this value
        /// to fit your requirements (e.g.relation to the size of your map, region or world in game).
        /// </summary>
        /// <remarks>Make sure that all of your ODIN client configure the same `distance` value.</remarks>
        /// <param name="room">*mut OdinRoom</param>
        /// <param name="scale">float scale</param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint RoomSetPositionScale(RoomHandle room, float scale)
        {
            using (Lock)
            {
                uint error = _OdinRoomSetPositionScale(room, scale);
                CheckAndThrow(error, $"{room} position scale {scale}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinRoomSetEventCallbackDelegate(IntPtr room, OdinEventCallback callback, MarshalByRefObject extra_data);
        readonly OdinRoomSetEventCallbackDelegate _OdinRoomSetEventCallback;
        /// <summary>
        /// Register a <see cref="OdinEventCallback"/> for all room events in the <see cref="RoomCreate"/> provided room.
        /// </summary>
        /// <param name="room">*mut OdinRoom</param>
        /// <param name="callback">extern "C" fn(event: *const <see cref="OdinNative.Core.Imports.NativeBindings.OdinEvent"/>) -> ()</param>
        /// <param name="extra_data">custom ref object</param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint RoomSetEventCallback(RoomHandle room, OdinEventCallback callback, MarshalByRefObject extra_data = null)
        {
            using (Lock)
            {
                uint error = _OdinRoomSetEventCallback(room, callback, extra_data);
                CheckAndThrow(error, $"{room.ToString()}");
                return error;
            }
        }
        public delegate void OdinEventCallback(IntPtr room, IntPtr odinEvent, MarshalByRefObject userData);

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinRoomUpdateUserDataDelegate(IntPtr room, byte[] userData, ulong userDataLength);
        readonly OdinRoomUpdateUserDataDelegate _OdinRoomUpdateUserData;
        /// <summary>
        /// Updates the user data for our own peer in the specified `OdinRoom`. 
        /// The server will populate this data to all other visible peers in the same room.
        /// </summary>
        /// <remarks>This function can be called before joining a room to set initial user data upon connect.</remarks>
        /// <param name="room">*mut OdinRoom</param>
        /// <param name="userData">*const u8</param>
        /// <param name="userDataLength">usize</param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint RoomUpdateUserData(RoomHandle room, byte[] userData, ulong userDataLength)
        {
            using (Lock)
            {
                uint error = _OdinRoomUpdateUserData(room, userData, userDataLength);
                CheckAndThrow(error, $"{room} data size {userDataLength}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinRoomUpdatePositionDelegate(IntPtr room, float x, float y);
        readonly OdinRoomUpdatePositionDelegate _OdinRoomUpdatePosition;
        /// <summary>
        /// Updates the two-dimensional position of our own peer in the given `OdinRoom`. The server will
        /// use the specified coordinates for each peer in the same room to apply automatic distance based
        /// culling. This is ideal for any scenario, where you want to put a large number of peers into the
        /// same room and make them only 'see' each other while being in proximity.
        /// </summary>
        /// <remarks>This should _only_ be used after configuring the room with <see cref="OdinNative.Core.Imports.NativeMethods.RoomSetPositionScale"/>.</remarks>
        /// <param name="room">*mut OdinRoom</param>
        /// <param name="x">float</param>
        /// <param name="y">float</param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint RoomUpdatePosition(RoomHandle room, float x, float y)
        {
            using (Lock)
            {
                uint error = _OdinRoomUpdatePosition(room, x, y);
                CheckAndThrow(error, $"{room} x: {x}, y: {y}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinRoomSendMessageDelegate(IntPtr room, [In] UInt64[] peerIdList, [In] ulong peerIdListSize, [In] byte[] data, [In] ulong dataLength);
        readonly OdinRoomSendMessageDelegate _OdinRoomSendMessage;
        /// <summary>
        /// Sends arbitrary data to a list of target peers over the ODIN server.
        /// </summary>
        /// <param name="room">*mut OdinRoom</param>
        /// <param name="peerIdList">*const u64</param>
        /// <param name="peerIdListSize">usize</param>
        /// <param name="data">*const u8</param>
        /// <param name="dataLength">usize</param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint RoomSendMessage(RoomHandle room, ulong[] peerIdList, ulong peerIdListSize, byte[] data, ulong dataLength)
        {
            using (Lock)
            {
                uint error = _OdinRoomSendMessage(room, peerIdList, peerIdListSize, data, dataLength);
                CheckAndThrow(error, $"{room} peer list size {peerIdListSize}");
                return error;
            }
        }
        #endregion Room

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinAudioProcessReverseDelegate(IntPtr room, [In] float[] buffer, [In] int bufferLength);
        readonly OdinAudioProcessReverseDelegate _OdinAudioProcessReverse;
        /// <summary>
        /// Processes the reverse audio stream, also known as the loopback data to be used in the ODIN echo
        /// canceller.This should only be done if you are _NOT_ using <see cref="OdinNative.Core.Imports.NativeMethods.AudioMixStreams"/>.
        /// </summary>
        /// <param name="room">struct OdinRoom*</param>
        /// <param name="buffer">float*</param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        internal uint AudioProcessReverse(RoomHandle room, float[] buffer)
        {
            using (Lock)
            {
                uint error = _OdinAudioProcessReverse(room, buffer, buffer.Length);
                if (InternalIsError(error))
                    CheckAndThrow(error, $"{room} process reverse buffer size {buffer.Length}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinAudioMixStreamsDelegate(IntPtr room, [In] IntPtr[] mediaStreams, [In] int streamsLength, [In, Out] float[] buffer, [In, Out] int bufferLength);
        readonly OdinAudioMixStreamsDelegate _OdinAudioMixStreams;
        /// <summary>
        /// Send audio data with multiple MediaStreams to mix
        /// </summary>
        /// <param name="room">struct OdinRoom*</param>
        /// <param name="handles">struct OdinMediaStream *const *</param>
        /// <param name="buffer">float *</param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        internal uint AudioMixStreams(RoomHandle room, StreamHandle[] handles, float[] buffer)
        {
            using (Lock)
            {
                IntPtr[] streams = handles
                    .Select(h => h.DangerousGetHandle())
                    .Where(p => p != IntPtr.Zero)
                    .ToArray();

                uint error = _OdinAudioMixStreams(room, streams, streams.Length, buffer, buffer.Length);

                if (InternalIsError(error))
                    CheckAndThrow(error, $"{room} handle count {handles.Length} buffer size {buffer.Length}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate IntPtr OdinResamplerCreateDelegate(uint fromRate, uint toRate, short channelCount);
        readonly OdinResamplerCreateDelegate _OdinResamplerCreate;
        /// <summary>
        /// Creates a new Resampler instance. 
        /// One resampler should be used per audio stream.
        /// This is intended for situations where the audio output pipeline doesn't support 48kHz.
        /// </summary>
        /// <remarks>If the resampler was created with more than one channel, the data is assumed to be interleaved.</remarks>
        /// <param name="fromRate">uint32_t from_rate</param>
        /// <param name="toRate">uint32_t to_rate</param>
        /// <param name="channelCount">uint16_t channel_count</param>
        /// <returns><see cref="ResamplerHandle"/> always owns the <see cref="IntPtr"/> handle</returns>
        public ResamplerHandle ResamplerCreate(uint fromRate, uint toRate, short channelCount)
        {
            using (Lock)
            {
                IntPtr handle = _OdinResamplerCreate(fromRate, toRate, channelCount);
                return new ResamplerHandle(handle, fromRate, toRate, channelCount, _OdinResamplerDestroy);
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinResamplerProcessDelegate(ResamplerHandle resampler, [In] float[] input, [In] int inputLength, [In, Out] float[] output, [In, Out] ref int outputCapacity);
        readonly OdinResamplerProcessDelegate _OdinResamplerProcess;
        /// <summary>
        /// Resamples a chunk of audio.
        /// </summary>
        /// <remarks>If the resampler was created with more than one channel, the data is assumed to be interleaved. 
        /// The `outputCapacity` also serves as an out parameter in case the provided capactiy wasn't enough to fullfill the resample request, in which case this function will write the minimum required buffer size into the given variable.
        /// On success the written size is returned in both, the return value and `outputCapacity`</remarks>
        /// <param name="resampler">mut *OdinResamplerHandle <see cref="OdinNative.Core.Handles.ResamplerHandle"/></param>
        /// <param name="input">const float *input</param>
        /// <param name="inputLength">size_t input_len</param>
        /// <param name="output">float *output</param>
        /// <param name="outputCapacity">size_t *output_capacity</param>
        /// <returns>outputCapacity or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint ResamplerProcess(ResamplerHandle resampler, [In] float[] input, [In] int inputLength, [In, Out] float[] output, [In, Out] ref int outputCapacity)
        {
            using (Lock)
            {
                uint error = _OdinResamplerProcess(resampler, input, inputLength, output, ref outputCapacity);
                if (InternalIsError(error))
                    CheckAndThrow(error, $"{resampler} input size {input.Length} length {inputLength}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinResamplerDestroyDelegate(IntPtr resampler);
        readonly OdinResamplerDestroyDelegate _OdinResamplerDestroy;
        /// <summary>
        /// Destroys the given Resampler.
        /// </summary>
        /// <remarks>After this call, all attempts to use this handle will fail.</remarks>
        /// <param name="resampler">mut *OdinResamplerHandle <see cref="OdinNative.Core.Handles.ResamplerHandle"/></param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint ResamplerDestroy(ResamplerHandle resampler)
        {
            using (Lock)
            {
                uint error = _OdinResamplerDestroy(resampler);
                CheckAndThrow(error);
                return error;
            }
        }

        #region Media
        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate IntPtr OdinVideoStreamCreateDelegate();
        readonly OdinVideoStreamCreateDelegate _OdinVideoStreamCreate;
        /// <summary>
        /// Creates a new video stream, which can be added to a room and send data over it.
        /// </summary>
        /// <remarks>Video streams are not supported yet.</remarks>
        /// <returns><see cref="OdinNative.Odin.Media.MediaStream"/> *</returns>
        internal IntPtr VideoStreamCreate()
        {
            using (Lock)
                return _OdinVideoStreamCreate();
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinMediaStreamResumeDelegate(IntPtr mediaStream);
        readonly OdinMediaStreamResumeDelegate _OdinMediaStreamResume;
        /// <summary>
        /// Instructs the server to resume the specified output `OdinMediaStreamHandle`, re-initiating the
        /// reception of data.This operation essentially communicates a server-side unmute request from the
        /// client, indicating a desire to restart packet reception for this media stream.
        /// </summary>
        /// <returns><see cref="StreamHandle"/> * as <see cref="IntPtr"/> so <see cref="StreamHandle"/> can own the handle</returns>
        public uint MediaStreamResume(StreamHandle handle)
        {
            using (Lock)
                return _OdinMediaStreamResume(handle);
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinMediaStreamPauseDelegate(IntPtr mediaStream);
        readonly OdinMediaStreamPauseDelegate _OdinMediaStreamPause;
        /// <summary>
        /// Instructs the server to pause the specified `OdinMediaStreamHandle`, ceasing the reception of
        /// data.This operation essentially communicates a server-side mute request from the client, thus
        /// indicating a desire to halt packet reception for this media stream.
        /// </summary>
        /// <returns><see cref="StreamHandle"/> * as <see cref="IntPtr"/> so <see cref="StreamHandle"/> can own the handle</returns>
        public uint MediaStreamPause(StreamHandle handle)
        {
            using (Lock)
                return _OdinMediaStreamPause(handle);
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate IntPtr OdinAudioStreamCreateDelegate(NativeBindings.OdinAudioStreamConfig config);
        readonly OdinAudioStreamCreateDelegate _OdinAudioStreamCreate;
        /// <summary>
        /// Creates a new audio stream, which can be added to a room and send data over it.
        /// </summary>
        /// <remarks>Creates a native <see cref="StreamHandle"/>. Can only be destroyed with <see cref="MediaStreamDestroy(StreamHandle)"/></remarks>
        /// <param name="config"><see cref="OdinMediaConfig"/></param>
        /// <returns><see cref="StreamHandle"/> * as <see cref="IntPtr"/> so <see cref="StreamHandle"/> can own the handle</returns>
        public StreamHandle AudioStreamCreate(OdinMediaConfig config)
        {
            using (Lock)
            {
                IntPtr handle = _OdinAudioStreamCreate(config.GetOdinAudioStreamConfig());
                return new StreamHandle(handle);
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinMediaStreamDestroyDelegate(IntPtr mediaStream);
        readonly OdinMediaStreamDestroyDelegate _OdinMediaStreamDestroy;
        /// <summary>
        /// Destroys the specified `OdinMediaStream`, after which you will no longer be able to receive
        /// or send any data over it.If the media is currently 'attached' to a room it will be removed.
        /// </summary>
        /// Destroy a native <see cref="StreamHandle"/> that is created before with <see cref="AudioStreamCreate"/> and is not a remote stream.
        /// <param name="handle"><see cref="StreamHandle"/> *</param>
        public void MediaStreamDestroy(StreamHandle handle)
        {
            using (Lock)
                _OdinMediaStreamDestroy(handle);
        }

        internal void MediaStreamDestroy(IntPtr handle)
        {
            using (Lock)
                _OdinMediaStreamDestroy(handle);
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate OdinMediaStreamType OdinMediaStreamTypeDelegate(IntPtr mediaStream);
        readonly OdinMediaStreamTypeDelegate _OdinMediaStreamType;
        /// <summary>
        /// Returns the type of the specified media stream.
        /// </summary>
        /// <remarks>This function will always return <see cref="OdinNative.Core.Imports.NativeBindings.OdinMediaStreamType.OdinMediaStreamType_Audio"/> at the moment.</remarks>
        /// <param name="handle"></param>
        /// <returns>OdinMediaStreamType</returns>
        public OdinMediaStreamType MediaStreamType(StreamHandle handle)
        {
            using (Lock)
                return _OdinMediaStreamType(handle);
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinAudioPushDataDelegate(IntPtr mediaStream, [In] float[] buffer, [In] int bufferLength);
        readonly OdinAudioPushDataDelegate _OdinAudioPushData;
        /// <summary>
        /// Sends the buffer data to Odin.
        /// </summary>
        /// <param name="mediaStream">OdinMediaStream *</param>
        /// <param name="buffer">allocated buffer to read from</param>
        /// <param name="bufferLength">size of the buffer</param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint AudioPushData(StreamHandle mediaStream, float[] buffer, int bufferLength)
        {
            using (Lock)
            {
                uint error = _OdinAudioPushData(mediaStream, buffer, bufferLength);
                if (InternalIsError(error))
                    CheckAndThrow(error, $"{mediaStream} buffer size {buffer.Length} length {bufferLength}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinAudioReadDataDelegate(IntPtr mediaStream, [In, Out][MarshalAs(UnmanagedType.LPArray)] float[] buffer, [In] int bufferLength, [In, Out][MarshalAs(UnmanagedType.I4)] OdinChannelLayout channelLayout);
        readonly OdinAudioReadDataDelegate _OdinAudioReadData;
        /// <summary>
        /// Reads audio data from the specified `OdinMediaStream`. 
        /// This will return audio data in 48kHz interleaved.
        /// </summary>
        /// <remarks>writes only audio data into the buffer even if the buffer size exceeded the available data</remarks>
        /// <param name="mediaStream">OdinMediaStream *</param>
        /// <param name="buffer">allocated buffer to write to</param>
        /// <param name="bufferLength">size of the buffer</param>
        /// <param name="channelLayout">unused</param>
        /// <returns>count of written data</returns>
        public uint AudioReadData(StreamHandle mediaStream, [In, Out] float[] buffer, int bufferLength, OdinChannelLayout channelLayout = OdinChannelLayout.OdinChannelLayout_Mono)
        {
            using (Lock)
                return _OdinAudioReadData(mediaStream, buffer, bufferLength, channelLayout);
        }
        
        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinAudioResetDelegate(IntPtr mediaStream);
        readonly OdinAudioResetDelegate _OdinAudioReset;
        
        /// <summary>
        /// Resets the specified `OdinMediaStreamHandle` to its initial state, restoring it to its default
        /// configuration. This operation resets the internal Opus encoder/decoder, ensuring a clean state.
        /// Additionally, it clears internal buffers, providing a fresh start.
        /// </summary>
        /// <param name="mediaStream">OdinMediaStream *</param>
        /// <returns>0 or error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint AudioReset(StreamHandle mediaStream)
        {
            using (Lock)
            {
                uint error = _OdinAudioReset(mediaStream);
                CheckAndThrow(error, $"{mediaStream.ToString()}");
                return error;
            }
        }
        

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinAudioStatsDelegate(IntPtr mediaStream, out OdinAudioStreamStats stats);
        readonly OdinAudioStatsDelegate _OdinAudioStats;
        /// <summary>
        /// Retrieves statistics for the specified `OdinMediaStreamHandle`.
        /// </summary>
        /// <remarks>This will only work for remote/output streams.</remarks>
        /// <param name="mediaStream">OdinMediaStream *</param>
        /// <param name="stats"><see cref="OdinNative.Core.Imports.NativeBindings.OdinAudioStreamStats"/>: Audio stream statistics.</param>
        /// <returns>count of written data</returns>
        public uint AudioStats(StreamHandle mediaStream, out OdinAudioStreamStats stats)
        {
            using (Lock)
            {
                uint error = _OdinAudioStats(mediaStream, out stats);
                CheckAndThrow(error, $"{mediaStream.ToString()}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinMediaStreamMediaIdDelegate(IntPtr mediaStream, [Out] out ushort mediaId);
        readonly OdinMediaStreamMediaIdDelegate _OdinMediaStreamMediaId;
        /// <summary>
        /// Returns the media ID of the specified <see cref="StreamHandle"/>
        /// </summary>
        /// <param name="handle"><see cref="StreamHandle"/> *mut</param>
        /// <param name="mediaId">media id of the handle</param>
        /// <returns>error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint MediaStreamMediaId(StreamHandle handle, out UInt16 mediaId)
        {
            using (Lock)
            {
                uint error = _OdinMediaStreamMediaId(handle, out mediaId);
                CheckAndThrow(error, $"{handle.ToString()}");
                return error;
            }
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinMediaStreamPeerIdDelegate(IntPtr mediaStream, [Out] out ulong peerId);
        readonly OdinMediaStreamPeerIdDelegate _OdinMediaStreamPeerId;
        /// <summary>
        /// Returns the peer ID of the specified <see cref="StreamHandle"/>
        /// </summary>
        /// <param name="handle"><see cref="StreamHandle"/> *mut</param>
        /// <param name="peerId">peer id of the handle</param>
        /// <returns>error code that is readable with <see cref="ErrorFormat"/></returns>
        public uint MediaStreamPeerId(StreamHandle handle, out UInt64 peerId)
        {
            using (Lock)
            {
                uint error = _OdinMediaStreamPeerId(handle, out peerId);
                CheckAndThrow(error, $"{handle.ToString()}");
                return error;
            }
        }
        #endregion Media

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate uint OdinErrorFormatDelegate(uint error, [In, Out][MarshalAs(UnmanagedType.SysUInt)] IntPtr buffer, [In] int bufferLength);
        readonly OdinErrorFormatDelegate _OdinErrorFormat;
        /// <summary>
        /// Writes a readable string representation of the error in a buffer.
        /// </summary>
        /// <param name="error">error code</param>
        /// <param name="buffer">String buffer pointer</param>
        /// <param name="bufferLength">String buffer length</param>
        /// <returns>0 or error code that is readable with <see cref="GetErrorMessage"/></returns>
        internal uint ErrorFormat(uint error, IntPtr buffer, int bufferLength)
        {
            using (Lock)
                return _OdinErrorFormat(error, buffer, bufferLength);
        }

        [UnmanagedFunctionPointer(Native.OdinCallingConvention)]
        internal delegate bool OdinIsErrorDelegate(uint error);
        readonly OdinIsErrorDelegate _OdinIsError;
        /// <summary>
        /// Check if the error code is in range of errors.
        /// </summary>
        /// <remarks>Code <see cref="Utility.OK"/> is never a error and will not be checked</remarks>
        /// <param name="error">error code</param>
        /// <returns>true if error</returns>
        internal bool IsError(uint error)
        {
            if (error == Utility.OK) return false;

            using (Lock)
                return _OdinIsError(error);
        }
    }
}
