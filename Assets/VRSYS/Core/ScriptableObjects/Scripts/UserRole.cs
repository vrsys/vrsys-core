using UnityEngine;

namespace VRSYS.Core.Networking
{
    [CreateAssetMenu(menuName = "VRSYS/Core/Scriptable Objects/UserRole")]

    public class UserRole : ScriptableObject
    {
        public string Name;
        public GameObject Prefab;
    }
}
