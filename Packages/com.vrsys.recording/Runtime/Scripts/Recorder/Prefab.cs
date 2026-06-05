using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VRSYS.Scripts.Recording
{
    // as the path of a prefab in the resources folder is not available during runtime
    // a scriptable object is used to access the path. See: https://stackoverflow.com/a/69250824
    [CreateAssetMenu(menuName = "Scriptable Objects/Prefab")]
    public class Prefab : ScriptableObject
    {
        public string assetPath;
    }
}
