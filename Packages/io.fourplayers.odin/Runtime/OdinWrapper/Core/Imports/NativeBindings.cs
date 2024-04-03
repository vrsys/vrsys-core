using OdinNative.Odin.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Core.Imports
{
    /// <summary>
    /// C# bindings for the native ODIN runtime
    /// </summary>
    public static class NativeBindings
    {
        /// <summary>
        /// ODIN_VERSION
        /// </summary>
        public const string OdinVersion = "1.6.4";

        /// <summary>
        /// Block_SAMPLE_RATE
        /// </summary>
        public const UInt32 BlockSamplerate = 48000;

        /// <summary>
        /// Valid levels for aggressiveness of the noise suppression
        /// </summary>
        /// <remarks>
        /// A higher level will reduce the noise level at the expense of a higher speech distortion.
        /// </remarks>
        public enum OdinNoiseSuppressionLevel
        {
            /// <summary>
            /// None
            /// </summary>
            None,
            /// <summary>
            /// 6dB
            /// </summary>
            Low,
            /// <summary>
            /// 12 dB
            /// </summary>
            Moderate,
            /// <summary>
            /// 18 dB
            /// </summary>
            High,
            /// <summary>
            /// 21 dB
            /// </summary>
            VeryHigh,
        }

        internal struct OdinApmConfig
        {
            public bool voice_activity_detection;
            public float voice_activity_detection_attack_probability;
            public float voice_activity_detection_release_probability;
            public bool volume_gate;
            public float volume_gate_attack_loudness;
            public float volume_gate_release_loudness;
            public bool echo_canceller;
            public bool high_pass_filter;
            public bool pre_amplifier;
            public OdinNoiseSuppressionLevel noise_suppression_level;
            public bool transient_suppressor;
            public bool gain_controller;
        }

        internal enum OdinTokenAudience
        {
            None,
            Gateway,
            Sfu
        }

        internal struct OdinTokenOptions
        {
#pragma warning disable CS0649 // never assigned to, and will always have
            public string customer; // Customer identifier should not be set - unless connecting directly to an ODIN server
            public OdinTokenAudience audience;
            public ulong lifetime;
#pragma warning restore CS0649 // never assigned to, and will always have
        }

        #region EventStructs
        [StructLayout(LayoutKind.Explicit)]
        internal struct OdinEvent
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.I4)]
            public OdinEventTag tag;

            #region OdinEvent union
            [FieldOffset(8)]
            [MarshalAs(UnmanagedType.Struct)]
            public OdinEvent_JoinedData joined;
            [FieldOffset(8)]
            [MarshalAs(UnmanagedType.Struct)]
            public OdinEvent_PeerJoinedData peer_joined;
            [FieldOffset(8)]
            [MarshalAs(UnmanagedType.Struct)]
            public OdinEvent_PeerLeftData peer_left;
            [FieldOffset(8)]
            [MarshalAs(UnmanagedType.Struct)]
            public OdinEvent_PeerUserDataChangedData peer_user_data_changed;
            [FieldOffset(8)]
            [MarshalAs(UnmanagedType.Struct)]
            public OdinEvent_MediaAddedData media_added;
            [FieldOffset(8)]
            [MarshalAs(UnmanagedType.Struct)]
            public OdinEvent_MediaRemovedData media_removed;
            [FieldOffset(8)]
            [MarshalAs(UnmanagedType.Struct)]
            public OdinEvent_MediaActiveStateChangedData media_active_state_changed;
            [FieldOffset(8)]
            [MarshalAs(UnmanagedType.Struct)]
            public OdinEvent_RoomUserDataChangedData room_user_data_changed;
            [FieldOffset(8)]
            [MarshalAs(UnmanagedType.Struct)]
            public OdinEvent_RoomConnectionStateChangedData room_connection_state_changed;
            [FieldOffset(8)]
            [MarshalAs(UnmanagedType.Struct)]
            public OdinEvent_MessageReceivedData message_received;
            #endregion OdinEvent union
        };

        internal enum OdinEventTag
        {
            OdinEvent_Joined,
            OdinEvent_PeerJoined,
            OdinEvent_PeerLeft,
            OdinEvent_PeerUserDataChanged,
            OdinEvent_MediaAdded,
            OdinEvent_MediaRemoved,
            OdinEvent_MediaActiveStateChanged,
            OdinEvent_RoomUserDataChanged,
            OdinEvent_RoomConnectionStateChanged,
            OdinEvent_MessageReceived,
        }

        /// <summary>
        /// Statistics for the underlying connection of a room.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct OdinConnectionStats
        {
            /// <summary>
            /// The amount of outgoing UDP datagrams observed
            /// </summary>
            [FieldOffset(0)]
            public ulong udp_tx_datagrams;
            /// <summary>
            /// The amount of outgoing acknowledgement frames observed
            /// </summary>
            [FieldOffset(8)]
            public ulong udp_tx_acks;
            /// <summary>
            /// The total amount of bytes which have been transferred inside outgoing UDP datagrams
            /// </summary>
            [FieldOffset(16)]
            public ulong udp_tx_bytes;
            /// <summary>
            /// The amount of incoming UDP datagrams observed
            /// </summary>
            [FieldOffset(24)]
            public ulong udp_rx_datagrams;
            /// <summary>
            /// The amount of incoming acknowledgement frames observed
            /// </summary>
            [FieldOffset(32)]
            public ulong udp_rx_acks;
            /// <summary>
            /// The total amount of bytes which have been transferred inside incoming UDP datagrams
            /// </summary>
            [FieldOffset(40)]
            public ulong udp_rx_bytes;
            /// <summary>
            /// Current congestion window of the connection
            /// </summary>
            [FieldOffset(48)]
            public ulong cwnd;
            /// <summary>
            /// Congestion events on the connection
            /// </summary>
            [FieldOffset(56)]
            public ulong congestion_events;
            /// <summary>
            /// Current best estimate of the connection latency (round-trip-time) in milliseconds
            /// </summary>
            [FieldOffset(64)]
            public float rtt;
        }

        /// <summary>
        /// Audio stream statistics.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct OdinAudioStreamStats
        {
            /// <summary>
            /// The number of packets processed by the medias jitter buffer.
            /// </summary>
            [FieldOffset(0)]
            public uint jitter_packets_processed;
            /// <summary>
            /// The number of packets dropped because they seemed to arrive too early.
            /// </summary>
            [FieldOffset(4)]
            public uint jitter_packets_dropped_too_early;
            /// <summary>
            /// The number of packets processed because they seemed to arrive too late.
            /// </summary>
            [FieldOffset(8)]
            public uint jitter_packets_dropped_too_late;
            /// <summary>
            /// The number of packets marked as lost during transmission.
            /// </summary>
            [FieldOffset(12)]
            public uint jitter_packets_lost;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct OdinEvent_JoinedData
        {
            [FieldOffset(0)]
            public IntPtr room_id;
            [FieldOffset(8)]
            public ulong room_id_len;
            [FieldOffset(16)]
            public IntPtr room_user_data;
            [FieldOffset(24)]
            [MarshalAs(UnmanagedType.U8)]
            public ulong room_user_data_len;
            [FieldOffset(32)]
            public IntPtr customer;
            [FieldOffset(40)]
            public ulong customer_len;
            [FieldOffset(48)]
            [MarshalAs(UnmanagedType.U8)]
            public ulong own_peer_id;
            [FieldOffset(56)]
            public IntPtr own_user_id;
            [FieldOffset(64)]
            public ulong own_user_id_len;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct OdinEvent_PeerJoinedData
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.U8)]
            public ulong peer_id;
            [FieldOffset(8)]
            public IntPtr peer_user_data;
            [FieldOffset(16)]
            [MarshalAs(UnmanagedType.U8)]
            public ulong peer_user_data_len;
            [FieldOffset(24)]
            public IntPtr user_id;
            [FieldOffset(32)]
            [MarshalAs(UnmanagedType.U8)]
            public ulong user_id_len;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct OdinEvent_PeerLeftData
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.U8)]
            public ulong peer_id;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct OdinEvent_PeerUserDataChangedData
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.U8)]
            public ulong peer_id;
            [FieldOffset(8)]
            public IntPtr peer_user_data;
            [FieldOffset(16)]
            [MarshalAs(UnmanagedType.U8)]
            public ulong peer_user_data_len;
        }

        /// <summary>
        /// Provides access to output media stream
        /// </summary>
        /// <remarks>
        /// Note, that the stream is read only. Use OdinAudioReadData if needed.
        /// </remarks>
        [StructLayout(LayoutKind.Explicit)]
        internal struct OdinEvent_MediaAddedData
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.U8)]
            public ulong peer_id;
            [FieldOffset(8)]
            public IntPtr media_handle;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct OdinEvent_MediaRemovedData
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.U8)]
            public ulong peer_id;
            [FieldOffset(8)]
            public IntPtr media_handle;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct OdinEvent_MediaActiveStateChangedData
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.U8)]
            public ulong peer_id;
            [FieldOffset(8)]
            public IntPtr media_handle;
            [FieldOffset(16)]
            public bool active;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct OdinEvent_RoomUserDataChangedData
        {
            [FieldOffset(0)]
            public IntPtr room_user_data;
            [FieldOffset(8)]
            [MarshalAs(UnmanagedType.U8)]
            public ulong room_user_data_len;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct OdinEvent_RoomConnectionStateChangedData
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.I4)]
            public OdinRoomConnectionState state;
            [FieldOffset(8)]
            [MarshalAs(UnmanagedType.I4)]
            public OdinRoomConnectionStateChangeReason reason;

        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct OdinEvent_MessageReceivedData
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.U8)]
            public ulong peer_id;
            [FieldOffset(8)]
            public IntPtr data;
            [FieldOffset(16)]
            [MarshalAs(UnmanagedType.U8)]
            public ulong data_len;
        }

        /// <summary>
        /// Connection state of the ODIN client
        /// </summary>
        public enum OdinRoomConnectionState
        {
            /// <summary>
            /// Connection is being established
            /// </summary>
            Connecting,
            /// <summary>
            /// Connection is established
            /// </summary>
            Connected,
            /// <summary>
            /// Connection is closed
            /// </summary>
            Disconnected,
        }
        #endregion EventStructs

        /// <summary>
        /// Reason of connection state
        /// </summary>
        public enum OdinRoomConnectionStateChangeReason
        {
            /// <summary>
            /// Connection state change was initiated by the user
            /// </summary>
            ClientRequested,
            /// <summary>
            /// Connection state change was initiated by the server (e.g. peer was kicked)
            /// </summary>
            ServerRequested,
            /// <summary>
            /// Connection state change was caused by a timeout
            /// </summary>
            ConnectionLost,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct OdinAudioStreamConfig
        {
            public uint sample_rate;
            public byte channel_count;
        }

        internal enum OdinChannelLayout : byte
        {
            OdinChannelLayout_Mono = 1,
            OdinChannelLayout_Stereo
        }

        internal enum OdinMediaStreamType
        {
            OdinMediaStreamType_Audio,
            OdinMediaStreamType_Video,
            OdinMediaStreamType_Invalid,
        }
    }
}
