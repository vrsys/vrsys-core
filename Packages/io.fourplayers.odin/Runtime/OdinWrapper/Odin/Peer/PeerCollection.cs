using OdinNative.Odin.Media;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OdinNative.Odin.Peer
{
    /// <summary>
    /// Intern peer dictionary
    /// </summary>
    /// <remarks>Used by <see cref="OdinNative.Odin.Room.Room.RemotePeers"/></remarks>
    public class PeerCollection : IReadOnlyCollection<Peer>, IEqualityComparer<Peer>
    {
        private ConcurrentDictionary<ulong, Peer> _Peers;
        /// <summary>
        /// Intern peer dictionary
        /// </summary>
        public PeerCollection()
        {
            _Peers = new ConcurrentDictionary<ulong, Peer>();
        }

        /// <summary>
        /// Try to get Peer by id
        /// </summary>
        /// <param name="key">Peer Id</param>
        /// <returns>Peer or null</returns>
        public Peer this[ulong key]
        {
            get
            {
                if (_Peers != null && _Peers.TryGetValue(key, out Peer peer))
                    return peer;

                return null;
            }
        }

        /// <summary>
        /// Count of peers in the collection
        /// </summary>
        public int Count => _Peers.Count;

        /// <summary>
        /// Indicates whether elements can be added or removed from the collection
        /// </summary>
        public bool IsReadOnly { get; internal set; } = false;

        /// <summary>
        /// Add a peer to the collection
        /// </summary>
        /// <remarks>Always false if the collection IsReadOnly</remarks>
        /// <param name="item">peer to add</param>
        /// <returns>true on success or false</returns>
        public bool Add(Peer item)
        {
            if(IsReadOnly) return false;
            return InternalAdd(item);
        }

        internal bool InternalAdd(Peer item)
        {
            return _Peers.TryAdd(item.Id, item);
        }

        /// <summary>
        /// Free and empty the collection
        /// </summary>
        public void Clear()
        {
            FreeAll();
            _Peers.Clear();
        }

        /// <summary>
        /// Determines whether the peer by id is in the collection
        /// </summary>
        /// <param name="id">peer id of the peer</param>
        /// <returns>true on success or false</returns>
        public bool Contains(ulong id)
        {
            return _Peers.ContainsKey(id);
        }

        /// <summary>
        /// Determines whether the peer is in the collection
        /// </summary>
        /// <param name="item">peer</param>
        /// <returns>true on success or false</returns>
        public bool Contains(Peer item)
        {
            return _Peers.Values.Contains(item);
        }

        /// <summary>
        /// Copies peers of the collection to an array
        /// </summary>
        /// <param name="array">target array</param>
        /// <param name="arrayIndex">array offset</param>
        public void CopyTo(Peer[] array, int arrayIndex)
        {
            _Peers.Values.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Determines whether the peers are equal
        /// </summary>
        /// <param name="x">peer</param>
        /// <param name="y">peer</param>
        /// <returns>true if equal or false</returns>
        public bool Equals(Peer x, Peer y)
        {
            return x.Id == y.Id;
        }

        /// <summary>
        /// Get enumerator for iteration
        /// </summary>
        /// <returns>enumerator</returns>
        public IEnumerator<Peer> GetEnumerator()
        {
            return _Peers.Values.GetEnumerator();
        }

        /// <summary>
        /// Default GetHashCode
        /// </summary>
        /// <param name="obj">peer</param>
        /// <returns>hash code</returns>
        public int GetHashCode(Peer obj)
        {
            return obj.GetHashCode();
        }

        /// <summary>
        /// Remove a peer by id from the collection
        /// </summary>
        /// <remarks>Always false if the collection IsReadOnly</remarks>
        /// <param name="id">peer id of the peer to remove</param>
        /// <returns>true on success or false</returns>
        public bool Remove(ulong id)
        {
            if (IsReadOnly) return false;
            return InternalRemove(id);
        }

        internal bool InternalRemove(ulong id)
        {
            return _Peers.TryRemove(id, out _);
        }

        /// <summary>
        /// Remove a peer from the collection
        /// </summary>
        /// <remarks>Always false if the collection IsReadOnly</remarks>
        /// <param name="item">peer to remove</param>
        /// <returns>true on success or false</returns>
        public bool Remove(Peer item)
        {
            if (IsReadOnly) return false;
            return Remove(item.Id);
        }

        internal bool Free(ulong id)
        {
            bool result = _Peers.TryRemove(id, out Peer peer);
            if (result) peer.Dispose();
            return result;
        }

        internal void FreeAll()
        {
            foreach (var kvp in _Peers)
                Free(kvp.Key);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _Peers.GetEnumerator();
        }
    }
}