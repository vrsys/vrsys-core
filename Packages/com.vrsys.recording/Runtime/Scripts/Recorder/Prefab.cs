using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VRSYS.Recording
{
    // as the path of a prefab in the resources folder is not available during runtime
    // a scriptable object is used to access the path. See: https://stackoverflow.com/a/69250824
    [CreateAssetMenu(menuName = "Scriptable Objects/Prefab")]
    public class Prefab : ScriptableObject
    {
        public string assetPath;

        // Optional Addressables address/key of the prefab, captured alongside assetPath. Lets playback
        // load the prefab via Addressables (which works in player builds / Android) for prefabs that do
        // not live under a Resources folder. Empty when the prefab is not marked Addressable or the
        // Addressables package is not installed.
        public string addressableKey;
    }
}
