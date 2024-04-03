using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Odin.Media
{
    /// <summary>
    /// Internal collection of available media streams
    /// </summary>
    /// <remarks>Used by <see cref="OdinNative.Odin.Peer.Peer"/></remarks>
    public class MediaCollection : IReadOnlyCollection<MediaStream>, IEqualityComparer<MediaStream>
    {
        private volatile ConcurrentDictionary<long, MediaStream> _Medias;
        /// <summary>
        /// Internal collection of available media streams
        /// </summary>
        public MediaCollection()
        {
            _Medias = new ConcurrentDictionary<long, MediaStream>();
        }

        /// <summary>
        /// Try to get a media stream by ID
        /// </summary>
        /// <param name="key">MediaId</param>
        /// <returns>MediaStream or null</returns>
        public MediaStream this[long key]
        {
            get
            {
                if (_Medias != null && _Medias.TryGetValue(key, out MediaStream media))
                    return media;

                return null;
            }
        }

        /// <summary>
        /// Count of streams in the collection
        /// </summary>
        public int Count => _Medias.Count;

        /// <summary>
        /// Indicates whether elements can be added or removed from the collection
        /// </summary>
        public bool IsReadOnly { get; internal set; } = false;

        /// <summary>
        /// Add a stream to the collection
        /// </summary>
        /// <remarks>Always false if the collection IsReadOnly</remarks>
        /// <param name="item">stream to add</param>
        /// <returns>true on success or false</returns>
        public bool Add(MediaStream item)
        {
            if(IsReadOnly) return false;
            return InternalAdd(item);
        }

        internal bool InternalAdd(MediaStream item)
        {
            return _Medias.TryAdd(item.Id, item);
        }

        /// <summary>
        /// Free and empty the collection
        /// </summary>
        public void Clear()
        {
            FreeAll();
            _Medias.Clear();
        }

        /// <summary>
        /// Determines whether the stream by id is in the collection
        /// </summary>
        /// <param name="id">handle id of the stream</param>
        /// <returns>true on success or false</returns>
        public bool Contains(long id)
        {
            return _Medias.ContainsKey(id);
        }

        /// <summary>
        /// Determines whether the media is in the collection
        /// </summary>
        /// <param name="item">stream</param>
        /// <returns>true on success or false</returns>
        public bool Contains(MediaStream item)
        {
            return _Medias.Values.Contains(item);
        }

        /// <summary>
        /// Copies stream of the collection to an array
        /// </summary>
        /// <param name="array">target array</param>
        /// <param name="arrayIndex">array offset</param>
        public void CopyTo(MediaStream[] array, int arrayIndex)
        {
            _Medias.Values.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Determines whether the streams are equal
        /// </summary>
        /// <param name="x">stream</param>
        /// <param name="y">stream</param>
        /// <returns>true if equal or false</returns>
        public bool Equals(MediaStream x, MediaStream y)
        {
            return x.Id == y.Id;
        }

        /// <summary>
        /// Get enumerator for iteration
        /// </summary>
        /// <returns>enumerator</returns>
        public IEnumerator<MediaStream> GetEnumerator()
        {
            return _Medias.Values.GetEnumerator();
        }

        /// <summary>
        /// Default GetHashCode
        /// </summary>
        /// <param name="obj">stream</param>
        /// <returns>hash code</returns>
        public int GetHashCode(MediaStream obj)
        {
            return obj.GetHashCode();
        }

        /// <summary>
        /// Remove a stream by handle id from the collection
        /// </summary>
        /// <remarks>Always false if the collection IsReadOnly</remarks>
        /// <param name="id">handle id of the stream to remove</param>
        /// <returns>true on success or false</returns>
        public bool Remove(long id)
        {
            if(IsReadOnly) return false;
            return InternalRemove(id);
        }

        internal bool InternalRemove(long id)
        {
            return _Medias.TryRemove(id, out _);
        }

        /// <summary>
        /// Remove a stream from the collection
        /// </summary>
        /// <remarks>Always false if the collection IsReadOnly</remarks>
        /// <param name="item">stream to remove</param>
        /// <returns>true on success or false</returns>
        public bool Remove(MediaStream item)
        {
            return Remove(item.Id);
        }

        internal bool Free(long id)
        {
            bool result = _Medias.TryRemove(id, out MediaStream media);
            if (result) media.Dispose();
            return result;
        }

        internal void FreeAll()
        {
            foreach (var kvp in _Medias)
                Free(kvp.Key);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _Medias.GetEnumerator();
        }
    }
}
