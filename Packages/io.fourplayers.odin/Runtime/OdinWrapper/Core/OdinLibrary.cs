using OdinNative.Core.Handles;
using OdinNative.Core.Imports;
using OdinNative.Core.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OdinNative.Core
{
    /// <summary>
    /// Main lib entry class
    /// </summary>
    public static class OdinLibrary
    {
        private static OdinHandle Handle;
        private static NativeMethods NativeMethods;
        private static ReaderWriterLock InitializedLock = new ReaderWriterLock();
        private static bool ProcessExitRegistered = false;

        internal static NativeMethods Api
        {
            get
            {
                InitializedLock.AcquireReaderLock(1000);
                try
                {
                    if (IsInitialized == false)
                    {
                        LockCookie cookie = InitializedLock.UpgradeToWriterLock(Timeout.Infinite);
                        try
                        {
                            if (IsInitialized == false)
                                Initialize();
                        }
                        finally
                        {
                            InitializedLock.DowngradeFromWriterLock(ref cookie);
                        }
                    }
                    return NativeMethods;
                }
                finally
                {
                    InitializedLock.ReleaseReaderLock();
                }
            }
        }

        /// <summary>
        /// Indicates whether or not the native ODIN runtime has been loaded and initialized
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                OdinHandle handle = Handle;
                return handle != null && handle.IsClosed == false && handle.IsInvalid == false;
            }
        }

        /// <summary>
        /// Initializes the native ODIN runtime
        /// </summary>
        /// <remarks>
        /// This function explicitly loads the ODIN library. It will be invoked automatically by the SDK when required.
        /// </remarks>
        public static void Initialize()
        {
            Initialize(new OdinLibraryParameters());
        }

        /// <summary>
        /// Creates a new <see cref="OdinLibrary"/>-Instance
        /// </summary>
        /// <param name="parameters">Information used to create the instance</param>
        /// <exception cref="System.InvalidOperationException">a <see cref="OdinLibrary"/> is already created</exception>
        /// <exception cref="System.NullReferenceException"><paramref name="parameters"/> is null</exception>
        public static void Initialize(OdinLibraryParameters parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            InitializedLock.AcquireWriterLock(Timeout.Infinite);
            try
            {
                if (Handle != null) throw new InvalidOperationException("Library is already initialized");
                if (ProcessExitRegistered == false)
                {
                    AppDomain.CurrentDomain.ProcessExit += ProcessExit;
                    ProcessExitRegistered = true;
                }
                Platform = parameters.Platform;
                Handle = OdinHandle.Load(Platform, parameters.PossibleNativeBinaryLocations);
                NativeBinary = Handle.Location;
                NativeMethods = new NativeMethods(Handle);
            }
            catch
            {
                Handle?.Dispose();
                Handle = null;
                throw;
            }
            finally
            {
                InitializedLock.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="OdinLibrary"/>-Instance
        /// </summary>
        public static void Release()
        {
            InitializedLock.AcquireWriterLock(Timeout.Infinite);
            try
            {
                Handle?.Dispose();
                Handle = null;
            }
            finally
            {
                InitializedLock.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// Location of the native ODIN runtime binary
        /// </summary>
        public static string NativeBinary { get; private set; }

        /// <summary>
        /// Platform the library is running on
        /// </summary>
        /// <remarks>
        /// This value is used to determine how the native ODIN runtime library will loaded and unloaded.
        /// </remarks>
        public static SupportedPlatform Platform { get; private set; }

        private static void ProcessExit(object sender, EventArgs e)
        {
            if(IsInitialized)
                Release();
        }

        internal static Exception CreateException(uint error, string extraMessage = null)
        {
            string message = Api.GetErrorMessage(error);
            OdinException result = new OdinException(error, message);
            if (!string.IsNullOrEmpty(extraMessage))
                result.Data.Add("extraMessage", extraMessage);

            return result;
        }
    }
}
