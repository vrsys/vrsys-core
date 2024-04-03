using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Core
{
    /// <summary>
    /// Exception type for the native ODIN runtime
    /// </summary>
    public class OdinException : Exception
    {
        /// <summary>
        /// OdinErrorCode
        /// </summary>
        public uint ErrorCode;
        /// <summary>
        /// OdinErrorCode container
        /// </summary>
        /// <param name="error">OdinErrorCode</param>
        /// <param name="message">odin error message</param>
        public OdinException(uint error, string message)
            : base(message)
        {
            ErrorCode = error;
        }

        /// <summary>
        /// OdinErrorCode container
        /// </summary>
        /// <param name="error">OdinErrorCode</param>
        /// <param name="message">odin error message</param>
        /// <param name="innerException">wrapper inner</param>
        public OdinException(uint error, string message, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = error;
        }
    }
}
