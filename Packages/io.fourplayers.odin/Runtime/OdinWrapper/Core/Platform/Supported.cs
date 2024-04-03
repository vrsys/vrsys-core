using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Core.Platform
{
    /// <summary>
    /// Platforms supported by the native ODIN runtime
    /// </summary>
    public enum SupportedPlatform
    {
#pragma warning disable 1591
        Android,
        iOS,
        MacOSX,
        Linux,
        Windows,
    }
#pragma warning restore
}
