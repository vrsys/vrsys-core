using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OdinNative.Odin.Media
{
    interface IAudioStream
    {
        ushort GetMediaId();
        ulong GetPeerId();
        void AudioPushData(float[] buffer);
        Task AudioPushDataTask(float[] buffer, CancellationToken cancellationToken);
        void AudioPushDataAsync(float[] buffer);
        bool AudioStats(out Core.Imports.NativeBindings.OdinAudioStreamStats stats);
        bool AudioReset();
        uint AudioReadData(float[] buffer);
        Task<uint> AudioReadDataTask(float[] buffer, CancellationToken cancellationToken);
        Task<uint> AudioReadDataAsync(float[] buffer);
    }
}
