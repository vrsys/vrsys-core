using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Odin
{
    /// <summary>
    /// Exception type for C# Wrapper
    /// </summary>
    class OdinWrapperException : Exception
    {
        public OdinWrapperException(string message) 
            : base(message)
        { }

        public OdinWrapperException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
