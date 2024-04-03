using System;
using UnityEngine.Events;
using OdinNative.Odin.Room;

namespace OdinNative.Unity.Events
{
    /// <summary>
    /// This class provides the base functionality for ODIN SDK UnityEvents.
    /// A persistent callback that can be saved with the Scene.
    /// </summary>
    [Serializable]
    public class RoomJoinProxy : UnityEvent<RoomJoinEventArgs>
    {
    }
}
