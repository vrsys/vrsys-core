using OdinNative.Core;
using OdinNative.Core.Handles;
using OdinNative.Odin.Media;
using OdinNative.Odin.Peer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OdinNative.Core.Imports.NativeBindings;
using static OdinNative.Core.Imports.NativeMethods;

namespace OdinNative.Odin.Room
{
    /// <summary>
    /// Main Room
    /// </summary>
    public class Room : IDisposable
    {
        /// <summary>
        /// ConnectionState of the Room that is set by ODIN
        /// </summary>
        public static KeyValuePair<OdinRoomConnectionState, OdinRoomConnectionStateChangeReason> ConnectionState { get; private set; } = new KeyValuePair<OdinRoomConnectionState, OdinRoomConnectionStateChangeReason>(OdinRoomConnectionState.Disconnected, OdinRoomConnectionStateChangeReason.ClientRequested);
        /// <summary>
        /// The count of ConnectionState <see cref="OdinNative.Core.Imports.NativeBindings.OdinRoomConnectionState.Connecting"/> retry for reconnects. Reset on Connected
        /// </summary>
        public int ConnectionRetry { get; private set; }

        internal string RoomId => GetRoomId();

        /// <summary>
        /// Room configuration
        /// </summary>
        public readonly RoomConfig Config;
        /// <summary>
        /// true on successful Join or false
        /// </summary>
        public bool IsJoined { get; private set; }

        /// <summary>
        /// Client Peer
        /// </summary>
        public Peer.Peer Self { get; private set; }
        private ulong _JoinedId;
        internal ref readonly ulong OwnId => ref _JoinedId;
        /// <summary>
        /// Odin UserData helper for marshal byte arrays on Room level
        /// </summary>
        public UserData RoomUserData { get; private set; }
        private UserData PeerUserData;

        /// <summary>
        /// Conatiner of room peers
        /// </summary>
        public PeerCollection RemotePeers { get; private set; }
        /// <summary>
        /// Get all medias of room peers
        /// </summary>
        public IEnumerable<MediaCollection> PlaybackMedias => RemotePeers.Select(p => p.Medias);
        /// <summary>
        /// Current room microphone data route
        /// </summary>
        public MicrophoneStream MicrophoneMedia { get; internal set; }

        private RoomHandle _Handle;
        internal IntPtr Handle { get { return _Handle.IsInvalid || _Handle.IsClosed ? IntPtr.Zero : _Handle.DangerousGetHandle(); } }
        private TokenGeneratorHandle _AuthHandle;
        internal IntPtr AuthHandle { get { return _AuthHandle == null || _AuthHandle.IsInvalid || _AuthHandle.IsClosed ? IntPtr.Zero : _AuthHandle.DangerousGetHandle(); } }

        internal Room(string server, string accessKey, string name, OdinRoomConfig apmConfig = null)
            : this(server, accessKey, string.Empty, name, new OdinMediaConfig(MediaSampleRate.Hz48000, MediaChannels.Mono), apmConfig ?? new OdinRoomConfig(), true)
        { }

        internal Room(string server, string token, OdinRoomConfig apmConfig = null)
            : this(server, string.Empty, token, string.Empty, new OdinMediaConfig(MediaSampleRate.Hz48000, MediaChannels.Mono), apmConfig ?? new OdinRoomConfig(), true)
        { }

        internal Room(string server, string accessKey, string token, string name, OdinRoomConfig apmConfig = null)
            : this(server, accessKey, token, name, new OdinMediaConfig(MediaSampleRate.Hz48000, MediaChannels.Mono), apmConfig ?? new OdinRoomConfig(), true)
        { }

        /// <summary>
        /// Create a ODIN ffi room 
        /// </summary>
        /// <param name="server">Endpoint</param>
        /// <param name="accessKey">Access Key</param>
        /// <param name="token">Room token</param>
        /// <param name="name">Room name</param>
        /// <param name="playbackMediaConfig">Config to use for <see cref="OdinNative.Odin.Media.MediaStream"/> on new medias</param>
        /// <param name="apmConfig">Config to use for <see cref="OdinNative.Core.OdinRoomConfig"/></param>
        /// <param name="registerEventCallback">true for <see cref="RegisterEventCallback"/> or false for no room events</param>
        public Room(string server, string accessKey, string token, string name, OdinMediaConfig playbackMediaConfig, OdinRoomConfig apmConfig, bool registerEventCallback)
            : this(new RoomConfig()
            {
                AccessKey = accessKey,
                Server = server,
                Name = name,
                Token = token,
                PlaybackMediaConfig = playbackMediaConfig,
                ApmConfig = apmConfig,
                HasEventCallbacks = registerEventCallback,
            }) { }

        /// <summary>
        /// Create a ODIN ffi room 
        /// </summary>
        /// <param name="config"><see cref="RoomConfig"/> to use for this room</param>
        public Room(RoomConfig config)
        {
            Config = config;
            IsJoined = false;
            RoomUserData = new UserData();
            Init();
        }

        private void Init()
        {
            RemotePeers = new PeerCollection();
            _Handle = OdinLibrary.Api.RoomCreate();
            if(string.IsNullOrEmpty(this.Config.AccessKey) == false)
                _AuthHandle = OdinLibrary.Api.TokenGeneratorCreate(Config.AccessKey);

            if (Config.HasEventCallbacks)
            {
                // Save the room event delegate for the static OdinClient event Proxy
                EventDelegate = new OdinEventCallback(OdinClient.OnEventReceivedProxy);
                RegisterEventCallback(EventDelegate);
            }

            SetApmConfig(Config.ApmConfig);
        }

        /// <summary>
        /// Retrieves the room ID (e.g. the name of the room)
        /// </summary>
        /// <returns>room name</returns>
        public string GetRoomId()
        {
            if (IsJoined == false) return Config.Name;
            OdinLibrary.Api.RoomGetId(_Handle, out string roomId);
            return roomId;
        }

        /// <summary>
        /// Retrieves the identifier of the customer the room
        /// </summary>
        /// <returns>customer</returns>
        public string GetRoomCustomer()
        {
            OdinLibrary.Api.RoomGetCustomer(_Handle, out string customer);
            return customer;
        }

        /// <summary>
        /// Retrieves your own peer ID
        /// </summary>
        /// <returns>peer id</returns>
        public ulong GetRoomPeerId()
        {
            OdinLibrary.Api.RoomGetPeerId(_Handle, out ulong peerId);
            return peerId;
        }

        /// <summary>
        /// Retrieves statistics for the underlying connection of this room
        /// </summary>
        /// <remarks>The room has to be in a joined state</remarks>
        /// <returns>Statistics for the underlying connection of this room</returns>
        public OdinConnectionStats GetRoomConnectionStats()
        {
            if (IsJoined == false) return new OdinConnectionStats();
            OdinLibrary.Api.RoomConnectionStats(_Handle, out OdinConnectionStats stats);
            return stats;
        }

        /// <summary>
        /// Set rooms new Apm config
        /// </summary>
        /// <param name="config">new Apm configuration</param>
        /// <returns>true on successful set or false</returns>
        public bool SetApmConfig(OdinRoomConfig config)
        {
            Config.ApmConfig = config;
            return OdinLibrary.Api.RoomConfigure(_Handle, config) == Utility.OK;
        }

        /// <summary>
        /// Join the room via Odin gateway
        /// </summary>
        /// <remarks>Generates a room token and update UserData before join</remarks>
        /// <param name="name">room name</param>
        /// <param name="userId">user id</param>
        /// <param name="userData">custom userdata</param>
        /// <returns>true on successful join or false</returns>
        public bool Join(string name, string userId, IUserData userData = null)
        {
            if(Test(IsJoined == false, $"Odin: {ConnectionState.Key} {ConnectionState.Value}")) return false;

            Utility.Assert(!string.IsNullOrEmpty(this.Config.AccessKey), "Can not join a room by name without an accesskey. Use Join with token instead!");
            if (AuthHandle == IntPtr.Zero) return false;

            OdinLibrary.Api.TokenGeneratorCreateToken(_AuthHandle, name, userId, out string token);
            this.Config.Token = token;
            if (!string.IsNullOrEmpty(token) && UpdatePeerUserData(userData ?? PeerUserData))
                return Join(token);

            return false;
        }

        /// <summary>
        /// Join the room via Odin gateway
        /// </summary>
        /// <remarks>The room token should be generated *SERVER SIDE* by <see cref="OdinNative.Core.Imports.NativeMethods.TokenGeneratorCreateToken"/></remarks>
        /// <param name="token">room token</param>
        /// <returns>true on successful join or false</returns>
        public bool Join(string token)
        {
            if(Test(IsJoined == false, $"Odin: {ConnectionState.Key} {ConnectionState.Value}")) return false;

            if (string.IsNullOrEmpty(token))
            { 
                Utility.Assert(!string.IsNullOrEmpty(this.Config.Token), "Can not join a room without a token!");
                token = this.Config.Token;
            }

            return IsJoined = Utility.OK == OdinLibrary.Api.RoomJoin(
                _Handle,
                Config.Server,
                token);
        }

        /// <summary>
        /// Try to add a <see cref="OdinNative.Odin.Media.MicrophoneStream"/> to the room and set it to <see cref="MicrophoneMedia"/>
        /// </summary>
        /// <param name="config">Microphone device configuration</param>
        /// <returns>true if media was added to the room or false</returns>
        public bool CreateMicrophoneMedia(OdinMediaConfig config)
        {
            if(Test(IsJoined, $"Odin: {ConnectionState.Key} {ConnectionState.Value}")) return false;

            MicrophoneStream stream = new MicrophoneStream(config);
            bool result = stream.AddMediaToRoom(_Handle);
            if (result)
            {
                stream.GetMediaId();
                MicrophoneMedia = stream;
            }
            else
                stream.Dispose();

            return result;
        }

        /// <summary>
        /// Updates the user data for our own peer.
        /// The server will populate this data to all other visible peers in the same room.
        /// </summary>
        /// <param name="userData">Userdata to send</param>
        /// <returns>true if userdata was set for the peer or false</returns>
        public bool UpdatePeerUserData(IUserData userData)
        {
            byte[] data = userData?.ToBytes() ?? new byte[0];
            PeerUserData = (UserData)data;
            Self?.SetUserData(PeerUserData);
            return OdinLibrary.Api.RoomUpdateUserData(_Handle, data, (ulong)data.Length) == Utility.OK;
        }

        /// <summary>
        /// Updates the user data for our own peer.
        /// The server will populate this data to all other visible peers in the same room.
        /// </summary>
        /// <param name="userData">Userdata to send</param>
        /// <returns>true if userdata was set for the peer or false</returns>
        public async Task<bool> UpdatePeerUserDataAsync(IUserData userData)
        {
            return await Task.Run(() => {
                return UpdatePeerUserData(userData);
            });
        }

        /// <summary>
        /// Sends arbitrary data to a array of target peerIds.
        /// </summary>
        /// <remarks>associated ids of peers must be in the same room and should not be empty</remarks>
        /// <param name="peerIdList">array of ids(<see cref="Peer.Peer.Id"/>)</param>
        /// <param name="data">arbitrary byte array</param>
        /// <returns>true if data was send or false</returns>
        public bool SendMessage(ulong[] peerIdList, byte[] data)
        {
            if(Test(IsJoined, $"Odin: {ConnectionState.Key} {ConnectionState.Value}")) return false;
            if(Test(data != null && data.Length > 0, $"Odin: data is empty")) return false;

            return OdinLibrary.Api.RoomSendMessage(_Handle, peerIdList, peerIdList != null ? (ulong)peerIdList.Length : 0, data, (ulong)data.Length) == Utility.OK;
        }

        /// <summary>
        /// Sends arbitrary data to a array of target peerIds.
        /// </summary>
        /// <remarks>associated ids of peers must be in the same room and should not be empty</remarks>
        /// <param name="peerIdList">array of ids(<see cref="Peer.Peer.Id"/>)</param>
        /// <param name="data">arbitrary byte array</param>
        /// <returns>true if data was send or false</returns>
        public async Task<bool> SendMessageAsync(ulong[] peerIdList, byte[] data)
        {
            if (Test(IsJoined, $"Odin: {ConnectionState.Key} {ConnectionState.Value}")) return false;
            if (Test(data != null && data.Length > 0, $"Odin: data is empty")) return false;

            return await Task.Run(() => {
                return OdinLibrary.Api.RoomSendMessage(_Handle, peerIdList, peerIdList != null ? (ulong)peerIdList.Length : 0, data, (ulong)data.Length) == Utility.OK;
            });
        }

        /// <summary>
        /// Sends arbitrary data to a all remote peers in this room.
        /// </summary>
        /// <param name="data">arbitrary byte array</param>
        /// <param name="includeSelf">idicates whether this current peer id</param>
        /// <returns>true if data was send or false</returns>
        public bool BroadcastMessage(byte[] data, bool includeSelf = false)
        {
            ulong[] peerIds = includeSelf ? GetRemotePeersIds(includeSelf).ToArray() : null;
            return SendMessage(peerIds, data);
        }

        /// <summary>
        /// Sends arbitrary data to a all remote peers in this room.
        /// </summary>
        /// <param name="data">arbitrary byte array</param>
        /// <param name="includeSelf">idicates whether this current peer get the message too</param>
        /// <returns>true if data was send or false</returns>
        public async Task<bool> BroadcastMessageAsync(byte[] data, bool includeSelf = false)
        {
            ulong[] peerIds = includeSelf ? GetRemotePeersIds(includeSelf).ToArray() : null;
            return await SendMessageAsync(peerIds, data);
        }

        /// <summary>
        /// Get a copy of all PeerIds in this room
        /// </summary>
        /// <param name="includeSelf">to add the own peer id from the Joined event</param>
        /// <returns>PeerIds</returns>
        public List<ulong> GetRemotePeersIds(bool includeSelf)
        {
            var result = RemotePeers.Select(p => p.Id).ToList();
            if(includeSelf)
                result.Add(_JoinedId);

            return result;
        }

        /// <summary>
        /// All ids of <see cref="OdinNative.Odin.Media.MediaStream"/> from remote peers in this room
        /// </summary>
        /// <returns>Lookup of PeerId, MediaStreamIds</returns>
        public ILookup<ulong, IEnumerable<long>> GetRemotePeersMediaStreamIds()
        {
            return RemotePeers.ToLookup(p => p.Id, p => p.Medias.Select(m => m.Id));
        }

        /// <summary>
        /// Configures the allowed 'view' distance for proximity calculation of peers in the room
        /// </summary>
        /// <remarks>Make sure that all of your ODIN clients configure the same `distance` value.</remarks>
        /// <param name="scale">Per default, the room will use a distance of `1.0` fo proximity calculation</param>
        /// <returns>true if set or false</returns>
        public bool SetPositionScale(float scale)
        {
            if(Test(IsJoined, $"Odin: {ConnectionState.Key} {ConnectionState.Value}")) return false;
            return OdinLibrary.Api.RoomSetPositionScale(_Handle, scale) == Utility.OK;
        }

        /// <summary>
        /// Updates the two-dimensional position of our own peer in the room
        /// </summary>
        /// <remarks>This should _only_ be used after configuring the room with <see cref="OdinNative.Core.Imports.NativeMethods.RoomSetPositionScale"/>.</remarks>
        /// <param name="x">x postition</param>
        /// <param name="y">y postition</param>
        /// <returns>true if set or false</returns>
        public bool UpdatePosition(float x, float y)
        {
            if(Test(IsJoined, $"Odin: {ConnectionState.Key} {ConnectionState.Value}")) return false;
            return OdinLibrary.Api.RoomUpdatePosition(_Handle, x, y) == Utility.OK;
        }

        /// <summary>
        /// Processes the reverse audio stream, also known as the loopback data to be used in the ODIN echo
        /// canceller.This should only be done if you are _NOT_ using <see cref="OdinNative.Core.Imports.NativeMethods.AudioMixStreams"/>.
        /// </summary>
        /// <param name="buffer">the audio buffer to process</param>
        /// <returns>false on error</returns>
        public bool AudioProcessReverse(float[] buffer)
        {
            return OdinLibrary.Api.AudioProcessReverse(_Handle, buffer) == Utility.OK;
        }

        #region Events
        private OdinEventCallback EventDelegate { get; set; }

        internal delegate void AkiEventHandler(object sender, OdinEvent e);
        internal static event AkiEventHandler OnEvent;
        /// <summary>
        /// Passthrough event that identified a new PeerJoined event by Event-Tag.
        /// </summary>
        /// <remarks>Default <see cref="Room"/> sender and <see cref="PeerJoinedEventArgs"/></remarks>
        public event RoomPeerJoinedEventHandler OnPeerJoined;
        /// <summary>
        /// Passthrough event that identified a new PeerLeft event by Event-Tag.
        /// </summary>
        /// <remarks>Default <see cref="Room"/> sender and <see cref="PeerLeftEventArgs"/></remarks>
        public event RoomPeerLeftEventHandler OnPeerLeft;
        /// <summary>
        /// Passthrough event that identified a new PeerUpdated event by Event-Tag.
        /// </summary>
        /// <remarks>Default <see cref="Room"/> sender and <see cref="PeerUserDataChangedEventArgs"/></remarks>
        public event RoomPeerUserDataChangedEventHandler OnPeerUserDataChanged;
        /// <summary>
        /// Passthrough event that identified a new MediaAdded event by Event-Tag.
        /// </summary>
        /// <remarks>Default <see cref="Room"/> sender and <see cref="MediaAddedEventArgs"/></remarks>
        public event RoomMediaAddedEventHandler OnMediaAdded;
        /// <summary>
        /// Passthrough event that identified a new MediaRemoved event by Event-Tag.
        /// </summary>
        /// <remarks>Default <see cref="Room"/> sender and <see cref="MediaRemovedEventArgs"/></remarks>
        public event RoomMediaRemovedEventHandler OnMediaRemoved;
        /// <summary>
        /// Passthrough event that identified a new MediaActiveStateChanged event by Event-Tag.
        /// </summary>
        /// <remarks>Default <see cref="Room"/> sender and <see cref="MediaActiveStateChangedEventArgs"/></remarks>
        public event MediaActiveStateChangedEventHandler OnMediaActiveStateChanged;
        /// <summary>
        /// Passthrough event that identified a new RoomUserDataChanged event by Event-Tag.
        /// </summary>
        /// <remarks>Default <see cref="Room"/> sender and <see cref="RoomUserDataChangedEventArgs"/></remarks>
        public event RoomUserDataChangedEventHandler OnRoomUserDataChanged;
        /// <summary>
        /// Passthrough event that identified a new MessageReceived event by Event-Tag.
        /// </summary>
        /// <remarks>Default <see cref="Room"/> sender and <see cref="MessageReceivedEventArgs"/></remarks>
        public event RoomMessageReceivedEventHandler OnMessageReceived;
        /// <summary>
        /// Passthrough event that identified a new ConnectionStateChanged event by Event-Tag.
        /// </summary>
        /// <remarks>Default <see cref="Room"/> sender and <see cref="ConnectionStateChangedEventArgs"/></remarks>
        public event RoomConnectionStateChangedEventHandler OnConnectionStateChanged;

        internal void RegisterEventCallback(OdinEventCallback eventCallback)
        {
            OdinLibrary.Api.RoomSetEventCallback(_Handle, eventCallback);
        }

        /// <summary>
        /// Main entry for Room OdinEvents to identify the appropriate event to further passthrough and wrap the arguments.
        /// </summary>
        /// <remarks>Events: PeerJoined, PeerLeft, PeerUpdated, MediaAdded, MediaRemoved</remarks>
        /// <param name="_">this instance</param>
        /// <param name="event">OdinEvent struct</param>
        /// <param name="extraData">userdata pointer</param>
        internal void OnEventReceived(Room _, OdinEvent @event, MarshalByRefObject extraData)
        {
            switch (@event.tag)
            {
                case OdinEventTag.OdinEvent_Joined:
                    Utility.Assert(@event.joined.own_peer_id > 0, $"{nameof(@event.joined.own_peer_id)} is invalid: " + @event.joined.own_peer_id);
                    Utility.Assert(@event.joined.room_user_data != IntPtr.Zero, $"{nameof(@event.joined.room_user_data)} IntPtr is 0");
                    Utility.Assert(@event.joined.room_user_data_len < Int32.MaxValue, $"{nameof(@event.joined.room_user_data_len)} exceeded: " + @event.joined.room_user_data_len);
                    Utility.Assert(@event.joined.room_id != IntPtr.Zero, $"{nameof(@event.joined.room_id)} IntPtr is 0");
                    Utility.Assert(@event.joined.customer != IntPtr.Zero, $"{nameof(@event.joined.customer)} IntPtr is 0");

                    InvokeJoined(@event.joined);
                    break;
                case OdinEventTag.OdinEvent_PeerJoined:
                    Utility.Assert(@event.peer_joined.peer_id > 0, $"{nameof(@event.peer_joined.peer_id)} is invalid: " + @event.peer_joined.peer_id);
                    Utility.Assert(@event.peer_joined.peer_user_data != IntPtr.Zero, $"{nameof(@event.peer_joined.peer_user_data)} IntPtr is 0");
                    Utility.Assert(@event.peer_joined.peer_user_data_len < Int32.MaxValue, $"{nameof(@event.peer_joined.peer_user_data_len)} exceeded: " + @event.peer_joined.peer_user_data_len);
                    Utility.Assert(@event.peer_joined.user_id != IntPtr.Zero, $"{nameof(@event.peer_joined.user_id)} IntPtr is 0");

                    InvokePeerJoined(@event.peer_joined);
                    break;
                case OdinEventTag.OdinEvent_PeerLeft:
                    Utility.Assert(@event.peer_left.peer_id > 0, $"{nameof(@event.peer_left.peer_id)} is invalid: " + @event.peer_left.peer_id);

                    InvokePeerLeft(@event.peer_left); // invokes OnMediaRemoved too, if the peer had any dangling medias left
                    break;
                case OdinEventTag.OdinEvent_PeerUserDataChanged:
                    Utility.Assert(@event.peer_user_data_changed.peer_id > 0, $"{nameof(@event.peer_user_data_changed.peer_id)} is invalid: " + @event.peer_user_data_changed.peer_id);
                    Utility.Assert(@event.peer_user_data_changed.peer_user_data != IntPtr.Zero, $"{nameof(@event.peer_user_data_changed.peer_user_data)} IntPtr is 0");
                    Utility.Assert(@event.peer_user_data_changed.peer_user_data_len < Int32.MaxValue, $"{nameof(@event.peer_user_data_changed.peer_user_data_len)} exceeded: " + @event.peer_user_data_changed.peer_user_data_len);

                    InvokePeerUserDataChanged(@event.peer_user_data_changed);
                    break;
                case OdinEventTag.OdinEvent_MediaAdded:
                    Utility.Assert(@event.media_added.media_handle != IntPtr.Zero, $"{nameof(@event.media_added.media_handle)} IntPtr is 0");
                    Utility.Assert(@event.media_added.peer_id > 0, $"{nameof(@event.media_added.peer_id)} is invalid: " + @event.media_added.peer_id);

                    InvokeMediaAdded(@event.media_added);
                    break;
                case OdinEventTag.OdinEvent_MediaRemoved:
                    Utility.Assert(@event.media_removed.media_handle != IntPtr.Zero, $"{nameof(@event.media_removed.media_handle)} IntPtr is 0");

                    InvokeMediaRemoved(@event.media_removed);
                    break;
                case OdinEventTag.OdinEvent_MediaActiveStateChanged:
                    Utility.Assert(@event.media_active_state_changed.peer_id > 0, $"{nameof(@event.media_active_state_changed.peer_id)} is invalid: " + @event.media_active_state_changed.peer_id);
                    Utility.Assert(@event.media_active_state_changed.media_handle != IntPtr.Zero, $"{nameof(@event.media_active_state_changed.media_handle)} IntPtr is 0");

                    InvokeMediaActiveStateChanged(@event.media_active_state_changed);
                    break;
                case OdinEventTag.OdinEvent_RoomUserDataChanged:
                    Utility.Assert(@event.room_user_data_changed.room_user_data != IntPtr.Zero, $"{nameof(@event.room_user_data_changed.room_user_data)} IntPtr is 0");
                    Utility.Assert(@event.room_user_data_changed.room_user_data_len < Int32.MaxValue, $"{nameof(@event.room_user_data_changed.room_user_data_len)} exceeded: " + @event.room_user_data_changed.room_user_data_len);

                    InvokeRoomUserDataChanged(@event.room_user_data_changed);
                    break;
                case OdinEventTag.OdinEvent_MessageReceived:
                    Utility.Assert(@event.message_received.data != IntPtr.Zero, $"{nameof(@event.message_received.data)} IntPtr is 0");
                    Utility.Assert(@event.message_received.data_len < Int32.MaxValue, $"{nameof(@event.message_received.data_len)} exceeded: " + @event.message_received.data_len);

                    InvokeMessageReceived(@event.message_received);
                    break;

                case OdinEventTag.OdinEvent_RoomConnectionStateChanged:
                    InvokeConnectionStateChanged(@event.room_connection_state_changed);
                    break;
                default:
                    OnEvent?.Invoke(this, @event);
                    break;
            }
        }

        private void InvokeConnectionStateChanged(OdinEvent_RoomConnectionStateChangedData @event)
        {
            ConnectionState = new KeyValuePair<OdinRoomConnectionState, OdinRoomConnectionStateChangeReason>(@event.state, @event.reason);
            if (ConnectionState.Key.HasFlag(OdinRoomConnectionState.Disconnected))
                IsJoined = false;
            if (ConnectionState.Key.HasFlag(OdinRoomConnectionState.Connected))
                ConnectionRetry = 0;
            else
                ConnectionRetry++;

            OnConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs() 
            { 
                ConnectionState = ConnectionState.Key,
                ChangeReason = ConnectionState.Value,
                Retry = ConnectionRetry,
            });
        }

        private void InvokeMessageReceived(OdinEvent_MessageReceivedData @event)
        {
            /* Compatibility with Unity .NET prior to 4.5 (i.e 2.0) so we don't use Int32.MaxValue 0x7fffffff
             * MSDN: The maximum size in any single dimension is 2,147,483,591 (0x7FFFFFC7) for byte arrays 
             * and arrays of single-byte structures, and 2,146,435,071 (0X7FEFFFFF) for arrays containing other types. */
            ulong maxSize = Math.Min(@event.data_len, 0x7FFFFFC7);
            byte[] msgBuffer = new byte[maxSize];
            System.Runtime.InteropServices.Marshal.Copy(@event.data, msgBuffer, 0, msgBuffer.Length);

            OnMessageReceived?.Invoke(this, new MessageReceivedEventArgs()
            {
                PeerId = @event.peer_id,
                Data = msgBuffer,
            });
        }

        private void InvokeMediaActiveStateChanged(OdinEvent_MediaActiveStateChangedData @event)
        {
            var peer = RemotePeers[@event.peer_id];
            long streamId = @event.media_handle.ToInt64();
            if (MicrophoneMedia?.Id == streamId)
                MicrophoneMedia.IsActive = @event.active;
            else
            {
                MediaStream media = peer?.Medias[streamId];
                if (media != null) media.IsActive = @event.active;
            }

            OnMediaActiveStateChanged?.Invoke(this, new MediaActiveStateChangedEventArgs()
            {
                PeerId = @event.peer_id,
                MediaStreamId = streamId,
                Active = @event.active,
            });
        }

        private void InvokeRoomUserDataChanged(OdinEvent_RoomUserDataChangedData @event)
        {
            UserData newRoomData = new UserData();
            newRoomData.CopyFrom(@event.room_user_data, @event.room_user_data_len);
            this.RoomUserData = newRoomData;

            OnRoomUserDataChanged?.Invoke(this, new RoomUserDataChangedEventArgs()
            {
                RoomName = this.Config.Name,
                Data = newRoomData
            });
        }

        private void InvokeMediaRemoved(OdinEvent_MediaRemovedData @event)
        {
            long streamId = @event.media_handle.ToInt64();
            if (Self != null && Self.Medias.Any(m => m.Id == streamId))
            {
                Self.RemoveMedia(streamId);
                return;
            }

            // Remove media from peer
            var peerWithMedia = RemotePeers.FirstOrDefault(p => p.Medias.Any(m => m.Id == streamId));
            peerWithMedia?.RemoveMedia(streamId);

            OnMediaRemoved?.Invoke(this, new MediaRemovedEventArgs()
            {
                MediaStreamId = streamId,
                Peer = peerWithMedia,
            });
        }

        private void InvokeMediaAdded(OdinEvent_MediaAddedData @event)
        {
            Utility.Assert(@event.peer_id != Self?.Id, $"{nameof(@event.peer_id)} is Self");

            var playbackStream = new PlaybackStream(
                Config.PlaybackMediaConfig,
                new StreamHandle(@event.media_handle));

            var mediaPeer = RemotePeers[@event.peer_id];
            if (mediaPeer == null) // should only happen if this client (Self) added a media 
            {
                //add an unknown peer that never joined the room but created a media
                mediaPeer = new Peer.Peer(@event.peer_id, this.Config.Name, new UserData());
                RemotePeers.InternalAdd(mediaPeer);
            }

            mediaPeer.AddMedia(playbackStream);

            OnMediaAdded?.Invoke(this, new MediaAddedEventArgs()
            {
                PeerId = @event.peer_id,
                Peer = mediaPeer,
                Media = playbackStream,
            });
        }

        private void InvokePeerUserDataChanged(OdinEvent_PeerUserDataChangedData @event)
        {
            Utility.Assert(@event.peer_id != Self?.Id, $"{nameof(@event.peer_id)} is Self");

            // Set new userdata to peer
            UserData newData = new UserData();
            newData.CopyFrom(@event.peer_user_data, @event.peer_user_data_len);
            var peer = RemotePeers[@event.peer_id];
            peer?.SetUserData(newData);

            OnPeerUserDataChanged?.Invoke(this, new PeerUserDataChangedEventArgs()
            {
                PeerId = @event.peer_id,
                Peer = peer,
                UserData = newData,
            });
        }

        private void InvokePeerLeft(OdinEvent_PeerLeftData @event)
        {
            //remove dangling medias
            var leavingPeer = RemotePeers[@event.peer_id];
            if (leavingPeer != null)
            {
                foreach (var closingMedia in leavingPeer.Medias)
                    OnMediaRemoved?.Invoke(this, new MediaRemovedEventArgs()
                    {
                        MediaStreamId = (ushort)closingMedia.Id,
                        Peer = leavingPeer,
                    });
            }
            //remove peer
            RemotePeers.Free(@event.peer_id);

            OnPeerLeft?.Invoke(this, new PeerLeftEventArgs()
            {
                PeerId = @event.peer_id
            });
        }

        private void InvokePeerJoined(OdinEvent_PeerJoinedData @event)
        {
            UserData userData = new UserData();
            userData.CopyFrom(@event.peer_user_data, @event.peer_user_data_len);

            var peer = new Peer.Peer(@event.peer_id, this.Config.Name, userData);
            string userId = string.Empty;
            if (@event.user_id_len > 0)
                peer.UserId = userId = Core.Imports.Native.ReadByteString(@event.user_id, (int)@event.user_id_len);

            RemotePeers.InternalAdd(peer);

            OnPeerJoined?.Invoke(this, new PeerJoinedEventArgs()
            {
                PeerId = @event.peer_id,
                UserId = userId,
                Peer = peer
            });
        }

        private void InvokeJoined(OdinEvent_JoinedData @event)
        {
            //id
            _JoinedId = @event.own_peer_id;

            //room user data
            UserData roomData = new UserData();
            roomData.CopyFrom(@event.room_user_data, @event.room_user_data_len);
            RoomUserData = roomData;

            //room
            string roomId = Core.Imports.Native.ReadByteString(@event.room_id, (int)@event.room_id_len);
            Utility.Assert(!string.IsNullOrEmpty(roomId), $"{nameof(@event.room_id)} is \"{roomId}\"");
            this.Config.Name = roomId;

            string userId = string.Empty;
            if (@event.own_user_id_len > 0)
                userId = Core.Imports.Native.ReadByteString(@event.own_user_id, (int)@event.own_user_id_len);

            Self = new Peer.Peer(@event.own_peer_id, roomId, PeerUserData) { UserId = userId };
        }
        #endregion Events

        private static bool Test(bool condition, string message)
        {
            if(condition) return false;
#pragma warning disable CS0618 // Type or member is obsolete
            OdinNative.Core.Utility.Throw(new OdinWrapperException(message));
#pragma warning restore CS0618 // Type or member is obsolete
            return true;
        }

        /// <summary>
        /// Debug
        /// </summary>
        /// <returns>info</returns>
        public override string ToString()
        {
            return $"{nameof(Room)}: {nameof(RoomId)} \"{RoomId}\"" +
                $", {nameof(IsJoined)} {IsJoined}" +
                $", {nameof(OwnId)} {OwnId}" +
                $", {nameof(RemotePeers)} {RemotePeers?.Count}" +
                $", {nameof(PlaybackMedias)} {PlaybackMedias?.Count()}\n\t" +
                $"- {nameof(MicrophoneMedia)} {MicrophoneMedia?.ToString()}\n\t" +
                $"- {nameof(Config)} {Config?.ToString()}";
        }

        /// <summary>
        /// Leave a room and free all remote peers and associated medias
        /// </summary>
        /// <remarks>This resets the room object for a final close use Dispose</remarks>
        public void Leave()
        {
            if (Free())
                Init(); //Reset
        }

        private bool Free()
        {
            bool result = false;

            IsJoined = false;
            RegisterEventCallback(null);
            EventDelegate = null;
            RemotePeers.FreeAll();
            MicrophoneMedia?.Dispose();
            MicrophoneMedia = null;
            Self?.Dispose();
            Self = null;
            
            try
            {
                if (OdinLibrary.IsInitialized)
                    result = OdinLibrary.Api.RoomClose(_Handle) == Utility.OK;
            } catch { }

            _AuthHandle?.Close();
            _Handle?.Close();

            return result;
        }

        private bool disposedValue;
        /// <summary>
        /// On dispose will free the room and token generator
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Free();

                    _AuthHandle?.Dispose();
                    _AuthHandle = null;
                    _Handle?.Dispose();
                    _Handle = null;
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Default deconstructor
        /// </summary>
        ~Room()
        {
            Dispose(disposing: false);
        }

        /// <summary>
        /// On dispose will free the room and token generator
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
