using OdinNative.Core.Imports;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Core.Handles
{
    class StreamHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public static implicit operator IntPtr(StreamHandle handle) => handle?.DangerousGetHandle() ?? IntPtr.Zero;

        internal StreamHandle(IntPtr handle)
            : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            bool result = true;
            try
            {
                if (OdinLibrary.IsInitialized)
                    OdinLibrary.Api.MediaStreamDestroy(handle);

                SetHandleAsInvalid();
            }
            catch
            {
                result = false;
            }

            return result;
        }

        public override string ToString()
        {
            return $"{nameof(StreamHandle)} {this.handle.ToString()} valid {!this.IsInvalid} closed {this.IsClosed}";
        }
    }
}
