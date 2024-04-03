using OdinNative.Core.Handles;
using OdinNative.Core.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Core
{
    /// <summary>
    /// A set of values that are used when initializing the native ODIN runtime
    /// </summary>
    public class OdinLibraryParameters
    {
        /// <summary>
        /// Returns the name of the native ODIN runtime binary that fits the current environment
        /// </summary>
        /// <param name="names">possible names of the native sdk binary</param>
        /// <param name="platform">detected platform</param>
        /// <returns>true if a matching binary exists</returns>
        public static bool TryGetNativeBinaryName(out string[] names, out SupportedPlatform platform)
        {
            return PlatformSpecific.TryGetNativeBinaryName(out names, out platform);
        }

        /// <summary>
        /// Possible install location of the native ODIN runtime binary
        /// </summary>
        public string[] PossibleNativeBinaryLocations { get; set; }

        /// <summary>
        /// Determines which platform specific code needs to be executed
        /// </summary>
        public SupportedPlatform Platform { get; set; }

        /// <summary>
        /// Creates a new <see cref="OdinLibraryParameters"/>-Object.
        /// </summary>
        public OdinLibraryParameters()
            : this(null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="OdinLibraryParameters"/>-Object.
        /// </summary>
        /// <param name="odinBinaryFolder">location where the native Aki sdk files can be found.</param>
        public OdinLibraryParameters(string odinBinaryFolder)
        {
            if (TryGetNativeBinaryName(out string[] nativeBinaryLocations, out SupportedPlatform platform) == false)
                throw new NotSupportedException("platform is not supported");

            PossibleNativeBinaryLocations = ExtendPossibleLocations(nativeBinaryLocations, odinBinaryFolder);
            Platform = platform;
        }

        private string[] ExtendPossibleLocations(string[] nativeBinaryLocations, string odinBinaryFolder)
        {
            IEnumerable<string> result = nativeBinaryLocations;
            if (odinBinaryFolder != null)
            {
                result = result.Select(s => s == null ? null : Path.Combine(odinBinaryFolder, s));
            }
            if (odinBinaryFolder == null || Path.IsPathRooted(odinBinaryFolder) == false)
            {
#if UNITY_ANDROID || UNITY_WEBGL
                string root = "lib";
#elif ENABLE_IL2CPP
                string path = typeof(OdinHandle).Assembly.Location;
                string root = string.IsNullOrEmpty(path) ? UnityEngine.Application.dataPath : path;
#else
                string root = Path.GetDirectoryName(Path.GetFullPath(typeof(OdinHandle).Assembly.Location));
#endif
                result = result.Concat(result.Select(s => s == null ? null : Path.Combine(root, s)));
            }
            return result.ToArray();
        }

        /// <summary>
        /// Creates a new <see cref="OdinLibraryParameters"/>-Object.
        /// </summary>
        /// <param name="nativeBinaryLocation">Location to the Aki library binary.</param>
        /// <param name="platform">Determines which platform specific code will be executed.</param>
        public OdinLibraryParameters(string nativeBinaryLocation, SupportedPlatform platform)
        {
            PossibleNativeBinaryLocations = new string[] { nativeBinaryLocation };
            Platform = platform;
        }
    }
}
