using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OdinNative.Odin
{
    /// <summary>
    /// interface for transmitting UserData
    /// </summary>
    public interface IUserData
    {
        /// <summary>
        /// Indicates whether data is null or empty
        /// </summary>
        /// <returns>true if empty</returns>
        bool IsEmpty();
        /// <summary>
        /// Used for converting Data on network level
        /// </summary>
        /// <returns>arbitrary data</returns>
        byte[] ToBytes();
    }

    /// <summary>
    /// Odin UserData helper for marshal byte arrays
    /// </summary>
    public class UserData : IUserData
    {
        /// <summary>
        /// Default Encoding
        /// </summary>
        public Encoding Encoding { get; set; }
        /// <summary>
        /// Raw UserData
        /// </summary>
        public byte[] Buffer { get => _buffer; set => _buffer = value; }
        private byte[] _buffer;
        /// <summary>
        /// string to UserData
        /// </summary>
        /// <param name="text">string representation of userdata</param>
        public static explicit operator UserData(string text) => new UserData(text);
        /// <summary>
        /// UserData to string representation
        /// </summary>
        /// <remarks>can result in an empty string</remarks>
        /// <param name="userdata">userdata object</param>
        public static explicit operator string(UserData userdata) => userdata?.ToString() ?? string.Empty;
        /// <summary>
        /// byte array to UserData
        /// </summary>
        /// <param name="data">raw representation of userdata</param>
        public static explicit operator UserData(byte[] data) => new UserData(data);
        /// <summary>
        /// UserData to raw representation
        /// </summary>
        /// <remarks>can result in an empty byte array</remarks>
        /// <param name="userdata">userdata object</param>
        public static implicit operator byte[](UserData userdata) => userdata?.ToBytes() ?? new byte[0];

        internal UserData() : this(new byte[0]) { }
        /// <summary>
        /// Odin UserData with default encoding UTF8
        /// </summary>
        /// <param name="text">string representation of userdata</param>
        public UserData(string text) : this(text, Encoding.UTF8) { }
        /// <summary>
        /// Odin UserData with custom encoding
        /// </summary>
        /// <param name="text">string representation of userdata</param>
        /// <param name="encoding">custom encoding</param>
        public UserData(string text, Encoding encoding) : this(encoding.GetBytes(text), encoding) { }
        /// <summary>
        /// Odin UserData with default encoding UTF8
        /// </summary>
        /// <param name="data">raw representation</param>
        public UserData(byte[] data) : this(data, null) { }
        /// <summary>
        /// Odin UserData with custom encoding
        /// </summary>
        /// <param name="data">raw representation</param>
        /// <param name="encoding">custom encoding</param>
        public UserData(byte[] data, Encoding encoding)
        {
            Encoding = encoding ?? Encoding.UTF8;
            Buffer = data;
        }

        /// <summary>
        /// Copies data from memory to Buffer
        /// </summary>
        /// <param name="ptr">source</param>
        /// <param name="size">Buffer size</param>
        public virtual void CopyFrom(IntPtr ptr, ulong size)
        {
            Buffer = new byte[size];
            if (ptr == IntPtr.Zero) return;

            Marshal.Copy(ptr, Buffer, 0, Buffer.Length);
        }

        /// <summary>
        /// Indicates whether data is null or empty
        /// </summary>
        /// <returns>true if empty</returns>
        public virtual bool IsEmpty()
        {
            return Buffer.Length == 0 || string.IsNullOrEmpty(this.ToString());
        }

        /// <summary>
        /// Indicates whether substring occurs
        /// </summary>
        /// <remark>uses the specified encoding</remark>
        /// <param name="value"></param>
        /// <returns>true if contain</returns>
        public virtual bool Contains(string value)
        {
            return this.ToString()
                .Contains(value);
        }

        /// <summary>
        /// Indicates whether element occurs
        /// </summary>
        /// <param name="value">byte</param>
        /// <returns>true if contain</returns>
        public virtual bool Contains(byte value)
        {
            return this.Buffer
                .Contains(value);
        }

        /// <summary>
        /// Indicates whether two sequence are equal
        /// </summary>
        /// <param name="pattern">byte sequence</param>
        /// <returns>true if contain</returns>
        public virtual IEnumerable<int> PatternAt(byte[] pattern)
        {
            for (int i = 0; i < Buffer.Length; i++)
                if (Buffer.Skip(i).Take(pattern.Length).SequenceEqual(pattern))
                    yield return i;
        }

        /// <summary>
        /// Creates a shallow copy of the Buffer
        /// </summary>
        /// <returns>new instance</returns>
        public virtual UserData Clone()
        {
            return new UserData((byte[])Buffer.Clone()) { Encoding = this.Encoding };
        }

        /// <summary>
        /// Used for converting Data on network level
        /// </summary>
        /// <returns>arbitrary data</returns>
        public virtual byte[] ToBytes()
        {
            return Buffer.ToArray();
        }

        /// <summary>
        /// String representation of Buffer based on the specified encoding
        /// </summary>
        /// <returns>string representation</returns>
        public override string ToString()
        {
            return Encoding.GetString(Buffer);
        }
    }
}
