using UnityEngine;

namespace VRSYS.Core.ScriptableObjects
{
    [CreateAssetMenu(menuName = "Scriptable Objects/NavigationPlatformSpawnInfo")]
    public class NavigationPlatformSpawnInfo : ScriptableObject
    {
        public string platformName = "NavigationPlatform";
        
        public void SetPlatformName(string name) => platformName = name;
    }
}
