using System;
using Unity.Netcode;

namespace VRSYS.Recording
{
    /// <summary>Recorded data types as the plugin numbers them (DataType.h).</summary>
    public enum SemanticDataType
    {
        Transform = 0,
        Sound = 1,
        Generic = 2
    }

    /// <summary>
    /// One recorded data stream from a point in time on, as the cause or an effect of a semantic event. There is no
    /// end time: an effect lasts until the end of the recording.
    /// </summary>
    [Serializable]
    public struct SemanticEventReference : INetworkSerializable
    {
        public SemanticDataType type;
        // Recorder id of the stream in the recording (Recorder.Id).
        public int id;
        // Editing track; recordings and overwrites use track 0.
        public int track;
        // Recording time in seconds.
        public float startTime;

        /// <summary>The stream of <paramref name="recorder"/> from <paramref name="startTime"/> on.</summary>
        public static SemanticEventReference For(Recorder recorder, float startTime)
        {
            SemanticDataType type = SemanticDataType.Transform;
            if (recorder is AudioRecorder)
                type = SemanticDataType.Sound;
            else if (recorder is GenericRecorder)
                type = SemanticDataType.Generic;

            return new SemanticEventReference { type = type, id = recorder.Id, track = 0, startTime = startTime };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref type);
            serializer.SerializeValue(ref id);
            serializer.SerializeValue(ref track);
            serializer.SerializeValue(ref startTime);
        }
    }

    /// <summary>A registered semantic event, as the manager distributes it to all peers.</summary>
    public struct SemanticEventData : INetworkSerializable
    {
        public int eventId;
        public string name;
        public string description;
        public SemanticEventReference cause;
        public SemanticEventReference[] effects;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Null strings and arrays cannot be written.
            if (serializer.IsWriter)
            {
                name ??= "";
                description ??= "";
                effects ??= Array.Empty<SemanticEventReference>();
            }

            serializer.SerializeValue(ref eventId);
            serializer.SerializeValue(ref name);
            serializer.SerializeValue(ref description);
            serializer.SerializeValue(ref cause);
            serializer.SerializeValue(ref effects);
        }
    }
}
