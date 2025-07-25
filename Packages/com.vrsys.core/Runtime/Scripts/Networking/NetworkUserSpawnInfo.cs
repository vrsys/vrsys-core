using UnityEngine;

namespace VRSYS.Core.Networking
{
    [CreateAssetMenu(menuName = "VRSYS/Core/Scriptable Objects/NetworkUserSpawnInfo")]
    public class NetworkUserSpawnInfo : ScriptableObject
    {
        [UserRoleSelector]
        public UserRole userRole = null;
        public string userName;
        public Color userColor;
        
        public void SetUserRole(UserRole role) => userRole = role;
        public void SetUserName(string name) => userName = name;
        public void SetUserColor(Color color) => userColor = color;
    }
}