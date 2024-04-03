using OdinNative.Core;
using OdinNative.Odin.Room;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Odin
{
    /// <summary>
    /// Client Wrapper for ODIN ffi <see cref="OdinNative.Core.OdinLibrary.NativeMethods"/>
    /// </summary>
    public class OdinClient : IDisposable
    {
        /// <summary>
        /// Rooms
        /// </summary>
        internal volatile static RoomCollection _Rooms = new RoomCollection();
        /// <summary>
        /// A collection of all <see cref="Room.Room"/>
        /// </summary>
        public RoomCollection Rooms { get { return _Rooms; } }
        /// <summary>
        /// Connection EndPoint. Default from OdinEditorConfig.
        /// </summary>
        public Uri EndPoint { get; private set; }
        /// <summary>
        /// Client AccessKey for all new rooms. Default from OdinHandler config.
        /// </summary>
        public string AccessKey { get; private set; }
        /// <summary>
        /// Client custom UserData
        /// </summary>
        public IUserData UserData { get; set; }

        /// <summary>
        /// Creates a new instance for ODIN ffi C# Wrapper
        /// </summary>
        /// <remarks><see cref="OdinNative.Odin.UserData"/> will be empty</remarks>
        /// <param name="url"><see cref="EndPoint"/> Odin Server</param>
        /// <param name="accessKey">Odin access key</param>
        public OdinClient(string url, string accessKey)
            : this(new Uri(url), accessKey, new UserData())
        { }


        /// <summary>
        /// Creates a new instance for ODIN ffi C# Wrapper
        /// </summary>
        /// <remarks><see cref="OdinNative.Odin.UserData"/> will be empty</remarks>
        /// <param name="server"><see cref="EndPoint"/> Odin Server</param>
        /// <param name="accessKey">Odin access key</param>
        public OdinClient(Uri server, string accessKey)
            : this(server, accessKey, new UserData())
        { }

        /// <summary>
        /// Creates a new instance for ODIN ffi C# Wrapper
        /// </summary>
        /// <remarks><see cref="OdinNative.Odin.UserData"/> is optional</remarks>
        /// <param name="server"><see cref="EndPoint"/> Odin Server</param>
        /// <param name="accessKey">Odin access key</param>
        /// <param name="userData"><see cref="UserData"/> to set</param>
        public OdinClient(Uri server, string accessKey, IUserData userData = null)
        {
            EndPoint = server;
            UserData = userData ?? new UserData();
            AccessKey = accessKey;
        }

        /// <summary>
        /// Internal library reload
        /// </summary>
        /// <remarks>Consider the state of the AppDomain</remarks>
        /// <param name="init">Idicates to directly initialize the library again after release</param>
        protected internal void ReloadLibrary(bool init = true)
        {
            if (OdinLibrary.IsInitialized)
            {
                Close();
                OdinLibrary.Release();
            }
            if (init) OdinLibrary.Initialize();
        }

        internal static string CreateAccessKey()
        {
            return OdinLibrary.Api.GenerateAccessKey(out string key) == key.Length + 1 ? key : string.Empty;
        }

        #region Gateway
        /// <summary>
        /// Join or create a <see cref="Room.Room"/> by name via a gateway
        /// </summary>
        /// <param name="name">Room name</param>
        /// <param name="userId">User ID</param>
        /// <returns><see cref="Room.Room"/> or null</returns>
        public async Task<Room.Room> JoinRoom(string name, string userId)
        {
            return await JoinRoom(name, userId, UserData, null);
        }

        /// <summary>
        /// Join or create a <see cref="Room.Room"/> by name via a gateway
        /// </summary>
        /// <param name="name">Room name</param>
        /// <param name="userId">Odin client ID</param>
        /// <param name="setup">will invoke to setup a room before adding or joining</param>
        /// <returns><see cref="Room.Room"/> or null</returns>
        public async Task<Room.Room> JoinRoom(string name, string userId, Action<Room.Room> setup = null)
        {
            return await JoinRoom(name, userId, UserData, setup);
        }

        /// <summary>
        /// Join or create a <see cref="Room.Room"/> by name via a gateway
        /// </summary>
        /// <param name="name">Room name</param>
        /// <param name="userId">Odin client ID</param>
        /// <param name="userData">Set new <see cref="UserData"/> on room join</param>
        /// <param name="setup">will invoke to setup a room before adding or joining</param>
        /// <returns><see cref="Room.Room"/> or null</returns>
        public async Task<Room.Room> JoinRoom(string name, string userId, IUserData userData, Action<Room.Room> setup)
        {
            if (string.IsNullOrEmpty(name)) throw new OdinWrapperException("Room name can not be null or empty!", new ArgumentNullException());

            UserData = userData.IsEmpty() ? UserData : userData;
            return await Task.Factory.StartNew<Room.Room>(() =>
            {
                var room = new Room.Room(EndPoint.ToString(), AccessKey, name);
                setup?.Invoke(room);
                Rooms.InternalAdd(room);
                if (room.Join(name, userId, UserData) == false)
                {
                    Rooms.Remove(room);
                    room.Dispose();
                    room = null;
                }
                return room;
            });
        }

        /// <summary>
        /// Join or create a named <see cref="Room.Room"/> by token via a gateway
        /// </summary>
        /// <param name="roomalias">Room alias</param>
        /// <param name="token">Room token</param>
        /// <param name="userData">Set new <see cref="UserData"/> on room join</param>
        /// <param name="setup">will invoke to setup a room before adding or joining</param>
        /// <returns><see cref="Room.Room"/> or null</returns>
        public async Task<Room.Room> JoinNamedRoom(string roomalias, string token, IUserData userData = null, Action<Room.Room> setup = null)
        {
            if (string.IsNullOrEmpty(token)) throw new OdinWrapperException("Room token can not be null or empty!", new ArgumentNullException());

            UserData = userData == null || userData.IsEmpty() ? UserData : userData;
            return await Task.Factory.StartNew<Room.Room>(() =>
            {
                var room = new Room.Room(EndPoint.ToString(), string.Empty, token, roomalias);
                setup?.Invoke(room);
                Rooms.InternalAdd(room);
                if (room.Join(token) == false)
                {
                    Rooms.Remove(room);
                    room.Dispose();
                    room = null;
                }
                return room;
            });
        }

        /// <summary>
        /// Join or create a <see cref="Room.Room"/> by token via a gateway
        /// </summary>
        /// <param name="token">Room token</param>
        /// <param name="userData">Set new <see cref="UserData"/> on room join</param>
        /// <param name="setup">will invoke to setup a room before adding or joining</param>
        /// <returns><see cref="Room.Room"/> or null</returns>
        public async Task<Room.Room> JoinRoom(string token, IUserData userData, Action<Room.Room> setup)
        {
            if (string.IsNullOrEmpty(token)) throw new OdinWrapperException("Room token can not be null or empty!", new ArgumentNullException());

            UserData = userData.IsEmpty() ? UserData : userData;
            return await Task.Factory.StartNew<Room.Room>(() =>
            {
                var room = new Room.Room(EndPoint.ToString(), token);
                setup?.Invoke(room);
                Rooms.InternalAdd(room);
                if (room.Join(token) == false)
                {
                    Rooms.Remove(room);
                    room.Dispose();
                    room = null;
                }
                return room;
            });
        }
        #endregion Gateway

        /// <summary>
        /// Updates the <see cref="UserData"/> for all <see cref="Rooms"/> for the current peer
        /// </summary>
        /// <param name="userData"><see cref="OdinNative.Odin.UserData"/></param>
        public async void UpdateUserData(IUserData userData)
        {
            if (userData == null) throw new OdinWrapperException("UserData can not be null!", new ArgumentNullException());

            UserData = userData;
            foreach (var room in Rooms)
                await room.UpdatePeerUserDataAsync(userData);
        }

        /// <summary>
        /// Updates the <see cref="Room.Room.SetPositionScale(float)"/> for all <see cref="Rooms"/>
        /// </summary>
        /// <remarks>Make sure that all of your ODIN clients configure the same `distance` value.</remarks>
        /// <param name="scale">Per default, the room will use a distance of `1.0` fo proximity calculation</param>
        public async void SetPositionScale(float scale)
        {
            await Task.Factory.StartNew(() =>
            {
                foreach (var room in Rooms)
                    room.SetPositionScale(scale);
            });
        }

        /// <summary>
        /// Updates the <see cref="Room.Room.UpdatePosition(float, float)"/> for all <see cref="Rooms"/>
        /// </summary>
        /// <remarks>This should _only_ be used after configuring the room with <see cref="Room.Room.SetPositionScale(float)"/>.</remarks>
        /// <param name="x">x postition</param>
        /// <param name="y">y postition</param>
        public async void UpdatePosition(float x, float y)
        {
            await Task.Factory.StartNew(() =>
            {
                foreach (var room in Rooms)
                    room.UpdatePosition(x, y);
            });
        }

        /// <summary>
        /// Leave a joined Room
        /// </summary>
        /// <remarks>Will dispose the <see cref="Room.Room"/> object</remarks>
        /// <param name="name">Room name</param>
        /// <returns>true if removed from <see cref="Rooms"/> or false</returns>
        public async Task<bool> LeaveRoom(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return await Task.Factory.StartNew<bool>(() =>
            {
                Rooms.Leave(name);
                // remove and dispose
                return Rooms.Free(name);
            });
        }

        /// <summary>
        /// Main entry for native OdinEvents sanitize and passthrough to instance room.
        /// </summary>
        /// <remarks>Events: PeerJoined, PeerLeft, PeerUpdated, MediaAdded, MediaRemoved</remarks>
        /// <param name="roomPtr">sender room pointer</param>
        /// <param name="odinEvent">OdinEvent struct</param>
        /// <param name="extraData">userdata pointer</param>
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID || UNITY_WEBGL || UNITY_64
        [AOT.MonoPInvokeCallback(typeof(Core.Imports.NativeMethods.OdinEventCallback))]
#endif
        internal static void OnEventReceivedProxy(IntPtr roomPtr, IntPtr odinEvent, MarshalByRefObject extraData)
        {
            try
            {
                var @event = Marshal.PtrToStructure<Core.Imports.NativeBindings.OdinEvent>(odinEvent);

                var sender = OdinClient._Rooms[roomPtr];
                if (sender != null)
                {
                    //TODO get event userDataPtr and sanitize e.g 3D-Audio
                    sender.OnEventReceived(sender, @event, extraData);
                }
            }
            catch (Exception e)
#pragma warning disable CS0618 // Type or member is obsolete
            { Utility.Throw(e); }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Completly closes this Client and all <see cref="Room.Room"/> associated.
        /// </summary>
        /// <remarks>Should only be called in Loading-Screens or Scene transissions</remarks>
        public void Close()
        {
            if (Rooms == null) return;

            try { Rooms.FreeAll(); }
            catch { /* nop */ }
        }

        private bool disposedValue;
        /// <summary>
        /// On dispose will free all <see cref="OdinNative.Odin.Room.Room"/> and <see cref="OdinNative.Core.Imports.NativeMethods.Shutdown"/>
        /// </summary>
        /// <param name="disposing">Indicates to dispose the library</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ReloadLibrary(false);
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Default deconstructor
        /// </summary>
        ~OdinClient()
        {
            Dispose(disposing: false);
        }

        /// <summary>
        /// On dispose will free all <see cref="OdinNative.Odin.Room.Room"/> and <see cref="OdinNative.Core.Imports.NativeMethods.Shutdown"/>
        /// </summary>
        /// <remarks>Override dispose if muliple <see cref="OdinClient"/> are needed</remarks>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
