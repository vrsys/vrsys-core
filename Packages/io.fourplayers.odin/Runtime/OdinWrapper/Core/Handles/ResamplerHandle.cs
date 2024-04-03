using OdinNative.Core.Imports;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Core.Handles
{
    class ResamplerHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public static implicit operator IntPtr(ResamplerHandle handle) => handle?.DangerousGetHandle() ?? IntPtr.Zero;
        public uint FromRate;
        public uint ToRate;
        public short Channels;
        internal NativeMethods.OdinResamplerDestroyDelegate Free;

        /// <summary>
        /// Creates a new ODIN resampler handle
        /// </summary>
        /// <remarks>On <see cref="ReleaseHandle"/> the handle calls <see cref="NativeMethods.OdinResamplerDestroyDelegate"/></remarks>
        /// <param name="handle">Resampler handle pointer from <see cref="NativeMethods.OdinResamplerCreateDelegate"/></param>
        /// <param name="fromRate">source samplerate</param>
        /// <param name="toRate">target samplerate</param>
        /// <param name="channelCount">source channels</param>
        /// <param name="resamplerDestroyDelegate">Will be called on <see cref="ReleaseHandle"/></param>
        internal ResamplerHandle(IntPtr handle, uint fromRate, uint toRate, short channelCount, NativeMethods.OdinResamplerDestroyDelegate resamplerDestroyDelegate)
            : base(true)
        {
            FromRate = fromRate;
            ToRate = toRate;
            Channels = channelCount;
            Free = resamplerDestroyDelegate;
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
            return $"{nameof(ResamplerHandle)} {this.handle.ToString()} valid {!this.IsInvalid} closed {this.IsClosed}";
        }
    }
}
