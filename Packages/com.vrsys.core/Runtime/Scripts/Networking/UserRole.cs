using System;
using Unity.Netcode;

namespace VRSYS.Core.Networking
{
    [Serializable]
    public class UserRole : INetworkSerializable, IEquatable<UserRole>
    {
        #region Public Fields

        public string Name;

        #endregion

        #region Constructor

        public UserRole() { }

        public UserRole(string name)
        {
            Name = name;
        }

        #endregion

        #region Operator Overloads

        public static bool operator ==(UserRole a, UserRole b)
        {
            if (ReferenceEquals(a, null))
            {
                if (ReferenceEquals(b, null))
                    return true;

                return false;
            }

            if (ReferenceEquals(b, null))
            {
                return false;
            }
            
            return a.Name == b.Name;
        }

        public static bool operator !=(UserRole a, UserRole b)
        {
            return !(a == b);
        }
        
        

        #endregion

        #region INetworkSerializable

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Name);
        }

        #endregion

        #region IEquatable

        public bool Equals(UserRole other) => String.Equals(Name, other.Name);

        #endregion
    }

    [Serializable]
    public class UserRoleEntry
    {
        #region Public Fields

        public string Name;

        #endregion
    }
}
