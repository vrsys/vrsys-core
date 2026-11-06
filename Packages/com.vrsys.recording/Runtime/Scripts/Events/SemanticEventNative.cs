using System.Runtime.InteropServices;

namespace VRSYS.Recording
{
    /// <summary>
    /// Bindings of the plugin's semantic event endpoints (Src/Events/EventsEndpoints.cpp). While recording they work on
    /// the recording's event graph, which the plugin writes to &lt;name&gt;.events when the recording stops; otherwise
    /// on the graph of the recording opened for editing. Texts are UTF-8 bytes and must not contain ';' or line breaks.
    /// </summary>
    internal static class SemanticEventNative
    {
        // Returns the new event id, or -1.
        [DllImport("RecordingPlugin")]
        public static extern int AddSemanticEvent(int recorderId, byte[] name, int nameLength, byte[] description,
            int descriptionLength, int causeType, int causeId, int causeTrack, float causeStartTime);

        [DllImport("RecordingPlugin")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool AddSemanticEventEffect(int recorderId, int eventId, int effectType, int effectId,
            int effectTrack, float effectStartTime);

        [DllImport("RecordingPlugin")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RemoveSemanticEvent(int recorderId, int eventId);

        [DllImport("RecordingPlugin")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool ClearSemanticEvents(int recorderId);

        // Returns -1 on error.
        [DllImport("RecordingPlugin")]
        public static extern int GetSemanticEventCount(int recorderId);

        // Returns -1 on error.
        [DllImport("RecordingPlugin")]
        public static extern int GetSemanticEventIdAtIndex(int recorderId, int eventIndex);

        // name and description receive null-terminated UTF-8 text; false if a buffer is too small.
        [DllImport("RecordingPlugin")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool GetSemanticEventInfo(int recorderId, int eventId, byte[] name, int nameCapacity,
            byte[] description, int descriptionCapacity, out int causeType, out int causeId, out int causeTrack,
            out float causeStartTime, out int effectCount);

        [DllImport("RecordingPlugin")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool GetSemanticEventEffect(int recorderId, int eventId, int effectIndex,
            out int effectType, out int effectId, out int effectTrack, out float effectStartTime);
    }
}
