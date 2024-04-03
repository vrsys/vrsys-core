using OdinNative.Odin.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Odin.Room
{
    /// <summary>
    /// Arguments for RoomJoin events right before the room is joined
    /// </summary>
    public class RoomJoinEventArgs : EventArgs
    {
        /// <summary>
        /// room object
        /// </summary>
        public Room Room;
    }

    /// <summary>
    /// Arguments for RoomJoined events when the room was joined successfully
    /// </summary>
    public class RoomJoinedEventArgs : EventArgs
    {
        /// <summary>
        /// room object
        /// </summary>
        public Room Room;
    }

    /// <summary>
    /// Arguments for RoomLeave events right before the room handle is destroyed
    /// </summary>
    public class RoomLeaveEventArgs : EventArgs
    {
        /// <summary>
        /// room object
        /// </summary>
        public Room Room;
    }

    /// <summary>
    /// Arguments for RoomLeft events when the room handle was destroyed
    /// </summary>
    public class RoomLeftEventArgs : EventArgs
    {
        /// <summary>
        /// room name
        /// </summary>
        public string RoomName;
    }

    /// <summary>
    /// Arguments for PeerJoined events in the current room
    /// </summary>
    public class PeerJoinedEventArgs : EventArgs
    {
        /// <summary>
        /// peer Id
        /// </summary>
        public ulong PeerId { get; internal set; }
        /// <summary>
        /// user Id
        /// </summary>
        public string UserId { get; internal set; }
        /// <summary>
        /// peer object
        /// </summary>
        public Peer.Peer Peer;
    }
    /// <summary>
    /// EventHandler in the current room
    /// </summary>
    /// <param name="sender">sender of type <see cref="Room"/></param>
    /// <param name="e">Arguments events in the current room</param>
    public delegate void RoomPeerJoinedEventHandler(object sender, PeerJoinedEventArgs e);

    /// <summary>
    /// Arguments for PeerLeft events in the current room
    /// </summary>
    public class PeerLeftEventArgs : EventArgs
    {
        /// <summary>
        /// peer id
        /// </summary>
        public ulong PeerId { get; internal set; }
    }
    /// <summary>
    /// EventHandler in the current room
    /// </summary>
    /// <param name="sender">sender of type <see cref="Room"/></param>
    /// <param name="e">Arguments events in the current room</param>
    public delegate void RoomPeerLeftEventHandler(object sender, PeerLeftEventArgs e);

    /// <summary>
    /// Arguments for PeerUserDataChanged events in the current room
    /// </summary>
    public class PeerUserDataChangedEventArgs : EventArgs
    {
        /// <summary>
        /// peer id
        /// </summary>
        public ulong PeerId { get; internal set; }
        /// <summary>
        /// peer object
        /// </summary>
        public Peer.Peer Peer;
        /// <summary>
        /// peer userdata
        /// </summary>
        public UserData UserData;
    }
    /// <summary>
    /// EventHandler in the current room
    /// </summary>
    /// <param name="sender">sender of type <see cref="Room"/></param>
    /// <param name="e">Arguments events in the current room</param>
    public delegate void RoomPeerUserDataChangedEventHandler(object sender, PeerUserDataChangedEventArgs e);

    /// <summary>
    /// Arguments for MediaAdded events in the current room
    /// </summary>
    public class MediaAddedEventArgs : EventArgs
    {
        /// <summary>
        /// peer id
        /// </summary>
        public ulong PeerId { get; internal set; }
        /// <summary>
        /// peer object
        /// </summary>
        public Peer.Peer Peer;
        /// <summary>
        /// <see cref="OdinNative.Odin.Media.MediaStream"/> with <see cref="OdinNative.Odin.Media.IAudioStream"/>
        /// </summary>
        public PlaybackStream Media;
    }
    /// <summary>
    /// EventHandler in the current room
    /// </summary>
    /// <param name="sender">sender of type <see cref="Room"/></param>
    /// <param name="e">Arguments events in the current room</param>
    public delegate void RoomMediaAddedEventHandler(object sender, MediaAddedEventArgs e);

    /// <summary>
    /// Arguments for MediaRemoved events in the current room
    /// </summary>
    public class MediaRemovedEventArgs : EventArgs
    {
        /// <summary>
        /// stream handle id
        /// </summary>
        public long MediaStreamId { get; internal set; }
        /// <summary>
        /// peer object
        /// </summary>
        public Peer.Peer Peer;
    }
    /// <summary>
    /// EventHandler in the current room
    /// </summary>
    /// <param name="sender">sender of type <see cref="Room"/></param>
    /// <param name="e">Arguments events in the current room</param>
    public delegate void RoomMediaRemovedEventHandler(object sender, MediaRemovedEventArgs e);

    /// <summary>
    /// Arguments for MediaActiveStateChanged events in the current room
    /// </summary>
    public class MediaActiveStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// stream handle id
        /// </summary>
        public long MediaStreamId { get; internal set; }
        /// <summary>
        /// peer id
        /// </summary>
        public ulong PeerId { get; internal set; }
        /// <summary>
        /// state of the media
        /// </summary>
        public bool Active { get; internal set; }
    }
    /// <summary>
    /// EventHandler in the current room
    /// </summary>
    /// <param name="sender">sender of type <see cref="Room"/></param>
    /// <param name="e">Arguments events in the current room</param>
    public delegate void MediaActiveStateChangedEventHandler(object sender, MediaActiveStateChangedEventArgs e);

    /// <summary>
    /// Arguments for RoomUserDataChanged events in the current room
    /// </summary>
    public class RoomUserDataChangedEventArgs : EventArgs
    {
        /// <summary>
        /// room name
        /// </summary>
        public string RoomName { get; internal set; }
        /// <summary>
        /// room userdata
        /// </summary>
        public UserData Data;
    }
    /// <summary>
    /// EventHandler in the current room
    /// </summary>
    /// <param name="sender">sender of type <see cref="Room"/></param>
    /// <param name="e">Arguments events in the current room</param>
    public delegate void RoomUserDataChangedEventHandler(object sender, RoomUserDataChangedEventArgs e);

    /// <summary>
    /// Arguments for MessageReceived events in the current room
    /// </summary>
    public class MessageReceivedEventArgs : EventArgs
    {

        /// <summary>
        /// peer id
        /// </summary>
        public ulong PeerId { get; internal set; }
        /// <summary>
        /// arbitrary data
        /// </summary>
        public byte[] Data;
    }
    /// <summary>
    /// EventHandler in the current room
    /// </summary>
    /// <param name="sender">sender of type <see cref="Room"/></param>
    /// <param name="e">Arguments events in the current room</param>
    public delegate void RoomMessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);

    /// <summary>
    /// Arguments for ConnectionStateChanged events in the current room
    /// </summary>
    public class ConnectionStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Connection state of the ODIN client
        /// </summary>
        public Core.Imports.NativeBindings.OdinRoomConnectionState ConnectionState { get; internal set; }
        /// <summary>
        /// Reason of connection state
        /// </summary>
        public Core.Imports.NativeBindings.OdinRoomConnectionStateChangeReason ChangeReason { get; internal set; }
        /// <summary>
        /// Connection retry count
        /// </summary>
        public int Retry { get; internal set; }
    }
    /// <summary>
    /// EventHandler in the current room
    /// </summary>
    /// <param name="sender">sender of type <see cref="Room"/></param>
    /// <param name="e">Arguments events in the current room</param>
    public delegate void RoomConnectionStateChangedEventHandler(object sender, ConnectionStateChangedEventArgs e);
}
