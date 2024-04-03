using OdinNative.Core.Imports;
using OdinNative.Core.Platform;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OdinNative.Core.Handles
{
    internal class OdinHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public string Location { get; }
        public SupportedPlatform Platform { get; }
        private static AutoResetEvent DllUnloaded = new AutoResetEvent(true);

        public static OdinHandle Load(SupportedPlatform platform, string[] possibleNames)
        {
            DllUnloaded.WaitOne();
            PlatformSpecific.LoadDynamicLibrary(platform, possibleNames, out IntPtr handle, out string location);
            return new OdinHandle(handle, platform, location);
        }

        private OdinHandle(IntPtr handle, SupportedPlatform platform, string location)
            : base(true)
        {
            SetHandle(handle);
            Platform = platform;
            Location = location;
            
            NativeMethods.OdinStartupExDelegate startupClientLib;
            GetLibraryMethod("odin_startup_ex", out startupClientLib);
            NativeBindings.OdinAudioStreamConfig defaultConfig = new NativeBindings.OdinAudioStreamConfig() 
            {
#if UNITY_STANDALONE || UNITY_EDITOR || ENABLE_IL2CPP || ENABLE_MONO
                sample_rate = (uint)UnityEngine.AudioSettings.outputSampleRate,
                channel_count = (byte)OdinNative.Core.Imports.NativeBindings.OdinChannelLayout.OdinChannelLayout_Mono
#else
                sample_rate = OdinNative.Core.Imports.NativeBindings.BlockSamplerate, 
                channel_count = (byte)OdinNative.Core.Imports.NativeBindings.OdinChannelLayout.OdinChannelLayout_Mono
#endif
            };
            startupClientLib(OdinNative.Core.Imports.NativeBindings.OdinVersion, defaultConfig);
        }

        public void GetLibraryMethod<T>(string name, out T t)
        {
            PlatformSpecific.GetLibraryMethod(Platform, handle, name, out t);
        }

        protected override bool ReleaseHandle()
        {
            bool result = true;
            try
            {
                NativeMethods.OdinShutdownDelegate shutdownClientLib;
                GetLibraryMethod("odin_shutdown", out shutdownClientLib);
                shutdownClientLib();
            }
            catch
            {
                result = false;
            }
            try
            {
                PlatformSpecific.UnloadDynamicLibrary(Platform, handle);
            }
            catch
            {
                result = false;
            }
            try
            {
                DllUnloaded.Set();
            }
            catch (ObjectDisposedException) { /* nop */ }
            return result;
        }

        public override string ToString()
        {
            return $"{nameof(OdinHandle)} {this.handle.ToString()} valid {!this.IsInvalid} closed {this.IsClosed}";
        }
    }
}
