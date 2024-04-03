using UnityEngine;
using VRSYS.Core.Networking;

namespace VRSYS.Core.ScriptableObjects
{
    [CreateAssetMenu(menuName = "Scriptable Objects/NetworkUserSpawnInfo")]
    public class NetworkUserSpawnInfo : ScriptableObject
    {
        public UserRole userRole = UserRole.HMD;
        public string userName;
        public Color userColor;
        
        public void SetUserRole(UserRole role) => userRole = role;
        public void SetUserName(string name) => userName = name;
        public void SetUserColor(Color color) => userColor = color;
    }
}