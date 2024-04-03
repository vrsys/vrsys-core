using OdinNative.Core.Imports;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Core.Handles
{
    class TokenGeneratorHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public static implicit operator IntPtr(TokenGeneratorHandle handle) => handle?.DangerousGetHandle() ?? IntPtr.Zero;
        internal NativeMethods.OdinTokenGeneratorDestroyDelegate Free;

        /// <summary>
        /// Creates a new ODIN token generator handle
        /// </summary>
        /// <remarks>On <see cref="ReleaseHandle"/> the handle calls <see cref="NativeMethods.OdinTokenGeneratorDestroyDelegate"/></remarks>
        /// <param name="handle">Token generator handle pointer from <see cref="NativeMethods.OdinTokenGeneratorCreateDelegate"/></param>
        /// <param name="tokenGeneratorDestroyDelegate">Will be called on <see cref="ReleaseHandle"/></param>
        internal TokenGeneratorHandle(IntPtr handle, NativeMethods.OdinTokenGeneratorDestroyDelegate tokenGeneratorDestroyDelegate)
            : base(true)
        {
            Free = tokenGeneratorDestroyDelegate;
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            bool result = true;
            try
            {
                if (OdinLibrary.IsInitialized)
                    Free(handle);

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
            return $"{nameof(TokenGeneratorHandle)} {this.handle.ToString()} valid {!this.IsInvalid} closed {this.IsClosed}";
        }
    }
}
