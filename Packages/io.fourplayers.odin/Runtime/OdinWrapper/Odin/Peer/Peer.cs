using OdinNative.Odin.Media;
using OdinNative.Odin.Room;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OdinNative.Odin.Peer
{
    /// <summary>
    /// Client/Remote peer
    /// </summary>
    public class Peer : IDisposable
    {
        /// <summary>
        /// Peer id
        /// </summary>
        public ulong Id { get; private set; }
        /// <summary>
        /// Associated room name of this peer
        /// </summary>
        public string RoomName { get; private set; }
        /// <summary>
        /// Peers user id
        /// </summary>
        public string UserId { get; internal set; }
        /// <summary>
        /// Peer userdata
        /// </summary>
        public UserData UserData { get; private set; }
        /// <summary>
        /// Associated medias of this peer
        /// </summary>
        public MediaCollection Medias { get; private set; }
        /// <summary>
        /// Client/Remote peer
        /// </summary>
        /// <param name="id">peer id</param>
        /// <param name="roomName">name of the room</param>
        /// <param name="userData">initial userdata</param>
        public Peer(ulong id, string roomName, UserData userData)
        {
            Id = id;
            RoomName = roomName;
            UserData = userData ?? new UserData();
            Medias = new MediaCollection();
        }

        /// <summary>
        /// Associate a media with the peer
        /// </summary>
        /// <param name="stream">media stream</param>
        public void AddMedia(PlaybackStream stream)
        {
            Medias.InternalAdd(stream);
        }

        /// <summary>
        /// Remove a associated media from the peer
        /// </summary>
        /// <param name="mediaStreamId">stream handle id</param>
        public bool RemoveMedia(long mediaStreamId)
        {
            return Medias.InternalRemove(mediaStreamId);
        }

        internal void SetUserData(byte[] newData)
        {
            UserData = new UserData(newData);
        }

        internal void SetUserData(UserData userData)
        {
            UserData = userData;
        }

        /// <summary>
        /// Get a copy of all ids of <see cref="OdinNative.Odin.Media.MediaStream"/> from this peer
        /// </summary>
        /// <returns>MediaStreamIds</returns>
        public List<long> GetMediaStreamIds()
        {
            return Medias.Select(p => p.Id).ToList();
        }

        /// <summary>
        /// Debug
        /// </summary>
        /// <returns>info</returns>
        public override string ToString()
        {
            return $"{nameof(Peer)}: {nameof(Id)} {Id}" +
                $", {nameof(RoomName)} \"{RoomName}\"" +
                $", {nameof(UserId)} \"{UserId}\"" +
                $", {nameof(UserData)} {!UserData?.IsEmpty()}" +
                $", {nameof(Medias)} {Medias?.Count}";
        }

        private bool disposedValue;
        /// <summary>
        /// Free peer with all associated medias
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Medias.FreeAll();
                    UserData = null;
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Default deconstructor
        /// </summary>
        ~Peer()
        {
            Dispose(disposing: false);
        }

        /// <summary>
        /// Free peer with all associated medias
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}