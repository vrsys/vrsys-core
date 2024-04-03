using OdinNative.Core.Handles;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Odin.Room
{
    /// <summary>
    /// Intern room dictionary
    /// </summary>
    /// <remarks>Used by <see cref="OdinNative.Odin.OdinClient.Rooms"/></remarks>
    public class RoomCollection : IReadOnlyCollection<Room>, IEqualityComparer<Room>
    {
        private volatile ConcurrentDictionary<string, Room> _Rooms;
        /// <summary>
        /// Intern room dictionary
        /// </summary>
        public RoomCollection()
        {
            _Rooms = new ConcurrentDictionary<string, Room>();
        }

        /// <summary>
        /// Try to get room by name
        /// </summary>
        /// <param name="key">Room name</param>
        /// <returns>Room or null</returns>
        public Room this[string key]
        {
            get
            {
                if (string.IsNullOrEmpty(key)) return null;

                if (_Rooms != null && _Rooms.TryGetValue(key, out Room room))
                    return room;

                return null;
            }
        }

        internal Room this[IntPtr handle]
        {
            get
            {
                if (IntPtr.Zero == handle || _Rooms == null) return null;

                return _Rooms.Values.FirstOrDefault(r => r.Handle == handle);
            }
        }

        /// <summary>
        /// Count of rooms in the collection
        /// </summary>
        public int Count => _Rooms.Count;

        /// <summary>
        /// Indicates whether elements can be removed from the collection
        /// </summary>
        public bool IsRemoveOnly { get; internal set; } = false;

        /// <summary>
        /// Add a room to the collection
        /// </summary>
        /// <remarks>Always false if the collection IsRemoveOnly</remarks>
        /// <param name="item">room to add</param>
        /// <returns>true on success or false</returns>
        public bool Add(Room item)
        {
            if(IsRemoveOnly) return false;
            return InternalAdd(item);
        }

        internal bool InternalAdd(Room item)
        {
            if (string.IsNullOrEmpty(item.Config.Name))
                return _Rooms.TryAdd(item.Config.Token, item);
            else
                return _Rooms.TryAdd(item.Config.Name, item);
        }

        /// <summary>
        /// Free and empty the collection
        /// </summary>
        public void Clear()
        {
            FreeAll();
            _Rooms.Clear();
        }

        /// <summary>
        /// Determines whether the room by name/token is in the collection
        /// </summary>
        /// <param name="key">room key of the room</param>
        /// <returns>true on success or false</returns>
        public bool Contains(string key)
        {
            return _Rooms.ContainsKey(key);
        }

        /// <summary>
        /// Determines whether the room is in the collection
        /// </summary>
        /// <param name="item">room</param>
        /// <returns>true on success or false</returns>
        public bool Contains(Room item)
        {
            return _Rooms.Values.Contains(item);
        }

        /// <summary>
        /// Copies rooms of the collection to an array
        /// </summary>
        /// <param name="array">target array</param>
        /// <param name="arrayIndex">array offset</param>
        public void CopyTo(Room[] array, int arrayIndex)
        {
            _Rooms.Values.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Compares two rooms by name
        /// </summary>
        /// <param name="x">room</param>
        /// <param name="y">room</param>
        /// <returns>is equal</returns>
        public bool Equals(Room x, Room y)
        {
            return x.Config.Name == y.Config.Name;
        }

        /// <summary>
        /// Get enumerator for iteration
        /// </summary>
        /// <returns>enumerator</returns>
        public IEnumerator<Room> GetEnumerator()
        {
            return _Rooms.Values.GetEnumerator();
        }

        /// <summary>
        /// Default GetHashCode
        /// </summary>
        /// <param name="obj">room</param>
        /// <returns>hash code</returns>
        public int GetHashCode(Room obj)
        {
            return obj.GetHashCode();
        }

        /// <summary>
        /// Removes the room from this collection
        /// </summary>
        /// <remarks>does NOT leave or free the room</remarks>
        /// <param name="key">Room name</param>
        /// <returns>is removed</returns>
        public bool Remove(string key)
        {
            return _Rooms.TryRemove(key, out _);
        }

        /// <summary>
        /// Removes the room from this collection
        /// </summary>
        /// <remarks>does NOT leave or free the room</remarks>
        /// <param name="item">room</param>
        /// <returns>is removed</returns>
        public bool Remove(Room item)
        {
            if (string.IsNullOrEmpty(item.Config.Name))
                return Remove(item.Config.Token);
            else
                return Remove(item.Config.Name);
        }

        /// <summary>
        /// Get the room and leave
        /// </summary>
        /// <remarks>does NOT remove the room from the collection</remarks>
        /// <param name="key">room name</param>
        public void Leave(string key)
        {
            if(_Rooms.TryGetValue(key, out Room room))
                room.Leave();
        }

        /// <summary>
        /// Free and remove the room
        /// </summary>
        /// <param name="key">room name</param>
        /// <returns>is removed</returns>
        internal bool Free(string key)
        {
            bool result = _Rooms.TryRemove(key, out Room room);
            if (result) room.Dispose();
            return result;
        }

        internal void FreeAll()
        {
            foreach (var kvp in _Rooms)
                Free(kvp.Key);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _Rooms.GetEnumerator();
        }
    }
}
