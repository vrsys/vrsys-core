using System;
using UnityEngine.Events;
using OdinNative.Odin.Room;

namespace OdinNative.Unity.Events
{
    /// <summary>
    /// This class provides the base functionality for UnityEvents based <see cref="OdinNative.Odin.Room.RoomMessageReceivedEventHandler"/>.
    /// A persistent callback that can be saved with the Scene.
    /// Unity Inspector event wrapper <see href="https://docs.unity3d.com/ScriptReference/Events.UnityEvent.html">(UnityEvent)</see>
    /// </summary>
    [Serializable]
    public class MessageReceivedProxy : UnityEvent<object, MessageReceivedEventArgs>
    {
    }
}
