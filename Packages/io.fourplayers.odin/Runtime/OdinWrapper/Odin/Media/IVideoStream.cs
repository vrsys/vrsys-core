using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Odin.Media
{
    interface IVideoStream
    {
        ushort GetMediaId();
        ulong GetPeerId();
    }
}
