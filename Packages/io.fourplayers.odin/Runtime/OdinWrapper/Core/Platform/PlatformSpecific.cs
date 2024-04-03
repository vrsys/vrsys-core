using OdinNative.Core.Imports;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Core.Platform
{
    /// <summary>
    /// This class file helps covering the platform specific requirements of the ODIN package as install locations 
    /// will vary based on how it is installed.
    /// 
    /// 1) Installing from git:
    ///    $PROJECT_PATH/Library/PackageCache/io.fourplayers.odin@$COMMIT_HASH
    /// 
    /// 2) Installing from Unity asset store:
    ///    $PROJECT_PATH/Assets/4Players/ODIN
    /// 
    /// 3) Installing from tarball:
    ///    $PROJECT_PATH/Assets/io.fourplayers.odin
    /// 
    /// 4) Installing from Unity package bundle:
    ///    $PROJECT_PATH/Packages/io.fourplayers.odin
    /// </summary>
    internal static class PlatformSpecific
    {
        private const string PackageName = "io.fourplayers.odin";
        private const string PackageVendor = "4Players";
        private const string PackageShortName = "ODIN";

        private const string AssetPath = "Assets/" + PackageName + "/Plugins";
        private const string AssetStorePath = "Assets/" + PackageVendor + "/" + PackageShortName + "/Plugins";
        private const string PackagePath = "Packages/" + PackageName + "/Plugins";
        
        private const string WindowsLibName = "odin.dll";
        private const string LinuxLibName = "libodin.so";
        private const string AppleLibName = "libodin.dylib";

        private static class NativeWindowsMethods
        {
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Winapi)]
            public static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Winapi)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string procedureName);
            [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
            public static extern bool FreeLibrary(IntPtr hModule);
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Winapi)]
            public static extern int GetModuleFileName(IntPtr hModule, [Out] byte[] lpFilename, [In][MarshalAs(UnmanagedType.U4)] int nSize);
        }

        private static class NativeUnixMehods
        {
#if (UNITY_STANDALONE_WIN && ENABLE_IL2CPP) || UNITY_WSA || UNITY_UWP
            public static IntPtr dlopen(string filename, int flags) => IntPtr.Zero;
            public static IntPtr dlerror() => IntPtr.Zero;
            public static IntPtr dlsym(IntPtr id, string symbol) => IntPtr.Zero;
            public static int dlclose(IntPtr id) => 0;
            public static int uname(IntPtr buf) => 0;
#else
            [DllImport("__Internal")]
            public static extern IntPtr dlopen(string filename, int flags);
            [DllImport("__Internal")]
            public static extern IntPtr dlerror();
            [DllImport("__Internal")]
            public static extern IntPtr dlsym(IntPtr id, string symbol);
            [DllImport("__Internal")]
            public static extern int dlclose(IntPtr id);
            [DllImport("__Internal")]
            public static extern int uname(IntPtr buf);
#endif
        }

        public static void LoadDynamicLibrary(SupportedPlatform platform, string[] possibleNames, out IntPtr handle, out string location)
        {
            foreach (string possibleName in possibleNames)
            {
                if (LoadDynamicLibrary(platform, possibleName, out handle, out location))
                    return;
            }
            string message = string.Join(", ", possibleNames);
            switch (platform)
            {
                case SupportedPlatform.Windows:
                    throw new DllNotFoundException(message, GetLastWindowsError());
                case SupportedPlatform.Android:
                case SupportedPlatform.iOS:
                case SupportedPlatform.Linux:
                case SupportedPlatform.MacOSX:
                    throw new DllNotFoundException(message, GetLastErrorUnix());
                default: throw new NotSupportedException();
            }
        }
        private static bool LoadDynamicLibrary(SupportedPlatform platform, string name, out IntPtr handle, out string location)
        {

            switch (platform)
            {
                case SupportedPlatform.Windows:
                    handle = NativeWindowsMethods.LoadLibrary(name);
                    if (handle == IntPtr.Zero)
                        goto default;
                    location = GetLocationWindows(handle) ?? name;
                    return true;
                case SupportedPlatform.Android:
                case SupportedPlatform.iOS:
                case SupportedPlatform.Linux:
                case SupportedPlatform.MacOSX:
                    handle = NativeUnixMehods.dlopen(name, 2 /* RTLD_NOW */);
                    if (handle == IntPtr.Zero)
                        goto default;
                    location = name;
                    return true;
                default:
                    handle = IntPtr.Zero;
                    location = null;
                    return false;
            }
        }

        private static string GetLocationWindows(IntPtr handle)
        {
            byte[] bytes = new byte[260];
            int length = NativeWindowsMethods.GetModuleFileName(handle, bytes, bytes.Length);
            if (length <= 0 || length == bytes.Length)
                return null;
            return Encoding.Default.GetString(bytes, 0, length);
        }

        public static void GetLibraryMethod<T>(SupportedPlatform platform, IntPtr handle, string name, out T t)
        {
            IntPtr result;
            switch (platform)
            {
                case SupportedPlatform.Windows:
                    result = NativeWindowsMethods.GetProcAddress(handle, name);
                    if (result == IntPtr.Zero)
                        throw new EntryPointNotFoundException(name, GetLastWindowsError());
                    break;
                case SupportedPlatform.Android:
                case SupportedPlatform.iOS:
                case SupportedPlatform.Linux:
                case SupportedPlatform.MacOSX:
                    result = NativeUnixMehods.dlsym(handle, name);
                    if (result == IntPtr.Zero)
                        throw new EntryPointNotFoundException(name, GetLastErrorUnix());
                    break;
                default: throw new NotSupportedException();
            }
            t = Marshal.GetDelegateForFunctionPointer<T>(result);
        }

        public static void UnloadDynamicLibrary(SupportedPlatform platform, IntPtr handle)
        {
            switch (platform)
            {
                case SupportedPlatform.Windows:
                    if (NativeWindowsMethods.FreeLibrary(handle) == false)
                        throw GetLastWindowsError();
                    break;
                case SupportedPlatform.Android:
                case SupportedPlatform.iOS:
                case SupportedPlatform.Linux:
                case SupportedPlatform.MacOSX:
                    if (NativeUnixMehods.dlclose(handle) != 0)
                        throw GetLastErrorUnix() ?? new InvalidOperationException();
                    break;
                default: throw new NotSupportedException();
            }
        }

        static SupportedPlatform GetUnixPlatform()
        {
#if PLATFORM_IOS || UNITY_IOS
            return SupportedPlatform.iOS;
#else
            IntPtr buf = IntPtr.Zero;
            try
            {
                buf = Marshal.AllocHGlobal(8192);
                if (NativeUnixMehods.uname(buf) == 0)
                {
                    string os = Marshal.PtrToStringAnsi(buf);
                    if (os == "Darwin")
                        return SupportedPlatform.MacOSX;
                }
            }
            catch
            {
            }
            finally
            {
                if (buf != IntPtr.Zero)
                    Marshal.FreeHGlobal(buf);
            }
            return SupportedPlatform.Linux;
#endif
        }

        private static Exception GetLastWindowsError()
        {
            return new Win32Exception(Marshal.GetLastWin32Error());
        }

        private static Exception GetLastErrorUnix()
        {
            IntPtr error = NativeUnixMehods.dlerror();
            string message = Marshal.PtrToStringAnsi(error);
            return message != null ? new InvalidOperationException(message) : null;
        }

        /// <summary>
        /// Returns the name of the native SDK binary that fits the current environment
        /// </summary>
        /// <param name="names">possible names of the native sdk binary</param>
        /// <param name="platform">detected platform</param>
        /// <returns>true if a matching binary exists</returns>
        public static bool TryGetNativeBinaryName(out string[] names, out SupportedPlatform platform)
        {
            // check if OS is 64-, 32-, or something else bit
            bool is64Bit;
            switch (Native.SizeOfPointer)
            {
                case 8: is64Bit = true; break;
                case 4: is64Bit = false; break;
                default: names = null; platform = 0; return false;
            }

            // check if operating system is supported
            OperatingSystem operatingSystem = Environment.OSVersion;
            switch (operatingSystem.Platform)
            {
                case PlatformID.MacOSX: platform = SupportedPlatform.MacOSX; break;
                case PlatformID.Unix: platform = GetUnixPlatform(); break;
                case PlatformID.Win32NT:
                    if (operatingSystem.Version >= new Version(5, 1)) // if at least windows xp or newer
                    {
                        platform = SupportedPlatform.Windows;
                        break;
                    }
                    else goto default;
                default: platform = 0; names = null; return false;
            }

            string LibraryCache = "Library/PackageCache";
            try
            {
                LibraryCache = System.IO.Directory
                    .GetDirectories(LibraryCache)
                    .Where(dir => dir.Contains(PackageName))
                    .FirstOrDefault();
            } catch (System.IO.DirectoryNotFoundException) { /* nop */ }

            switch (platform)
            {
                case SupportedPlatform.iOS:
                    names = new string[] { AppleLibName,
#if UNITY_64
                        string.Format("{0}/{1}", UnityEngine.Application.dataPath, AppleLibName), // Data
                        string.Format("{0}/../{1}/{2}", UnityEngine.Application.dataPath, "Frameworks", AppleLibName), // Frameworks
                        string.Format("{0}/../{1}/{2}", UnityEngine.Application.dataPath, "PlugIns", AppleLibName), // PlugIns
                        string.Format("{0}/../{1}/{2}", UnityEngine.Application.dataPath, "SharedSupport", AppleLibName) // SharedSupport
#endif
                    };
                    break;
                case SupportedPlatform.MacOSX:
                    names = new string[] { AppleLibName,
                        string.Format("{0}/{1}/{2}", PackagePath, "macos/universal", AppleLibName), // PkgManager
                        string.Format("{0}/{1}/{2}", AssetPath, "macos/universal", AppleLibName), // Editor
                        string.Format("{0}/{1}/{2}", AssetStorePath, "macos/universal", AppleLibName), // Asset Store
                        string.Format("{0}/{1}/{2}", LibraryCache, "Plugins/macos/universal", AppleLibName) // PackageCache
#if UNITY_64
                        ,string.Format("{0}/{1}/{2}", UnityEngine.Application.dataPath, "Plugins", AppleLibName), string.Format("{0}/{1}", "Plugins", AppleLibName) // Standalone appbundle
#endif
                    };
                    break;
                case SupportedPlatform.Android:
#if UNITY_ANDROID
                    if (UnityEngine.SystemInfo.unsupportedIdentifier == UnityEngine.SystemInfo.deviceUniqueIdentifier)
                        goto case SupportedPlatform.Linux;

                    if (CultureInfo.InvariantCulture.CompareInfo.IndexOf(UnityEngine.SystemInfo.processorType, "ARM", CompareOptions.IgnoreCase) >= 0)
                        {
                            names = is64Bit
                                ? new string[] { LinuxLibName,
                                    string.Format("{0}/{1}/{2}", PackagePath, "android/aarch64", LinuxLibName), // PkgManager (ADB)
                                    string.Format("{0}/{1}/{2}", AssetPath, "android/aarch64", LinuxLibName), // Editor  (ADB)
                                    string.Format("{0}/{1}/{2}", AssetStorePath, "android/aarch64", LinuxLibName), // Asset Store  (ADB)
                                    string.Format("{0}/{1}/{2}", LibraryCache, "Plugins/android/aarch64", LinuxLibName) // PackageCache  (ADB)
                                    ,string.Format("{0}/{1}/{2}", UnityEngine.Application.dataPath, "Plugins", LinuxLibName)
                                    ,string.Format("{0}/{1}/{2}/{3}", UnityEngine.Application.dataPath, "Plugins", "aarch64", LinuxLibName), string.Format("{0}/{1}/{2}", "Plugins", "aarch64", LinuxLibName) // Standalone
                                    ,string.Format("{0}/{1}/{2}", UnityEngine.Application.dataPath, "lib", LinuxLibName)
                                    ,string.Format("{0}/{1}", UnityEngine.Application.dataPath, LinuxLibName)
                                }
                                : new string[] { LinuxLibName,
                                    string.Format("{0}/{1}/{2}", PackagePath, "android/armv7", LinuxLibName), // PkgManager  (ADB)
                                    string.Format("{0}/{1}/{2}", AssetPath, "android/armv7", LinuxLibName), // Editor  (ADB)
                                    string.Format("{0}/{1}/{2}", AssetStorePath, "android/armv7", LinuxLibName), // Asset Store  (ADB)
                                    string.Format("{0}/{1}/{2}", LibraryCache, "Plugins/android/armv7", LinuxLibName) // PackageCache  (ADB)
                                    ,string.Format("{0}/{1}/{2}", UnityEngine.Application.dataPath, "Plugins", LinuxLibName)
                                    ,string.Format("{0}/{1}/{2}/{3}", UnityEngine.Application.dataPath, "Plugins", "armv7", LinuxLibName), string.Format("{0}/{1}/{2}", "Plugins", "armv7", LinuxLibName)  // Standalone
                                    ,string.Format("{0}/{1}/{2}", UnityEngine.Application.dataPath, "lib", LinuxLibName)
                                    ,string.Format("{0}/{1}", UnityEngine.Application.dataPath, LinuxLibName)
                                };
                    }
                        else
                            goto case SupportedPlatform.Linux;
                    break;
#endif
                case SupportedPlatform.Linux:
                    names = is64Bit
                        ? new string[] { LinuxLibName,
                            string.Format("{0}/{1}/{2}", PackagePath, "linux/x86_64", LinuxLibName), // PkgManager
                            string.Format("{0}/{1}/{2}", AssetPath, "linux/x86_64", LinuxLibName), // Editor
                            string.Format("{0}/{1}/{2}", AssetStorePath, "linux/x86_64", LinuxLibName), // Asset Store
                            string.Format("{0}/{1}/{2}", LibraryCache, "Plugins/linux/x86_64", LinuxLibName) // PackageCache
#if UNITY_64
                            ,string.Format("{0}/{1}/{2}", UnityEngine.Application.dataPath, "Plugins", LinuxLibName)
                            ,string.Format("{0}/{1}/{2}/{3}", UnityEngine.Application.dataPath, "Plugins", "x86_64", LinuxLibName), string.Format("{0}/{1}/{2}", "Plugins", "x86_64", LinuxLibName) // Standalone
#endif
                        }
                        : new string[] { LinuxLibName,
                            string.Format("{0}/{1}/{2}", PackagePath, "linux/x86", LinuxLibName), // PkgManager
                            string.Format("{0}/{1}/{2}", AssetPath, "linux/x86", LinuxLibName), // Editor
                            string.Format("{0}/{1}/{2}", AssetStorePath, "linux/x86", LinuxLibName), // Asset Store
                            string.Format("{0}/{1}/{2}", LibraryCache, "Plugins/linux/x86", LinuxLibName) // PackageCache
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                            ,string.Format("{0}/{1}/{2}", UnityEngine.Application.dataPath, "Plugins", LinuxLibName)
                            ,string.Format("{0}/{1}/{2}/{3}", UnityEngine.Application.dataPath, "Plugins", "x86", LinuxLibName), string.Format("{0}/{1}/{2}", "Plugins", "x86", LinuxLibName)  // Standalone
#endif
                        };
                    break;
                case SupportedPlatform.Windows:
                    names = is64Bit
                        ? new string[] { WindowsLibName,
                            string.Format("{0}/{1}/{2}", PackagePath, "windows/x86_64", WindowsLibName), // PkgManager
                            string.Format("{0}/{1}/{2}", AssetPath, "windows/x86_64", WindowsLibName), // Editor
                            string.Format("{0}/{1}/{2}", AssetStorePath, "windows/x86_64", WindowsLibName), // Asset Store
                            string.Format("{0}/{1}/{2}", LibraryCache, "Plugins/windows/x86_64", WindowsLibName) // PackageCache
#if UNITY_64
                            ,string.Format("{0}/{1}/{2}", UnityEngine.Application.dataPath, "Plugins", WindowsLibName)
                            ,string.Format("{0}/{1}/{2}/{3}", UnityEngine.Application.dataPath, "Plugins", "x86_64", WindowsLibName), string.Format("{0}/{1}/{2}", "Plugins", "x86_64", WindowsLibName)  // Standalone
#endif
                        }
                        : new string[] { WindowsLibName,
                            string.Format("{0}/{1}/{2}", PackagePath, "windows/x86", WindowsLibName), // PkgManager
                            string.Format("{0}/{1}/{2}", AssetPath, "windows/x86", WindowsLibName), // Editor
                            string.Format("{0}/{1}/{2}", AssetStorePath, "windows/x86", WindowsLibName), // Asset Store
                            string.Format("{0}/{1}/{2}", LibraryCache, "Plugins/windows/x86", WindowsLibName) // PackageCache
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                            ,string.Format("{0}/{1}/{2}", UnityEngine.Application.dataPath, "Plugins", WindowsLibName)
                            ,string.Format("{0}/{1}/{2}/{3}", UnityEngine.Application.dataPath, "Plugins", "x86", WindowsLibName), string.Format("{0}/{1}/{2}", "Plugins", "x86", WindowsLibName)  // Standalone
#endif
                        };
                    break;
                default: throw new NotImplementedException();
            }
            return true;
        }
    }
}
