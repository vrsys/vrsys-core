using UnityEngine;

namespace VRSYS.Recording
{
    /// <summary>
    /// Excludes this object and everything beneath it from transform recording and replay: no transform recorder is
    /// attached to them, so they are neither recorded nor moved by a replay. Use it e.g. for the recording setup itself,
    /// UI, tools or user rigs that must keep working while a replay runs.
    ///
    /// Replaces the "DoNotPlayback" tag, which every project had to define. Objects that still carry that tag are
    /// excluded as well (see <see cref="LegacyTag"/>).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("VRSYS/Recording/Exclude From Recording")]
    public sealed class ExcludeFromRecording : MonoBehaviour
    {
        /// <summary>The former opt-out tag, still honored for existing scenes and prefabs.</summary>
        public const string LegacyTag = "DoNotPlayback";

        /// <summary>True if <paramref name="gameObject"/> itself is marked (component or legacy tag).</summary>
        public static bool IsMarked(GameObject gameObject)
        {
            return gameObject != null &&
                   (gameObject.TryGetComponent(out ExcludeFromRecording _) || gameObject.CompareTag(LegacyTag));
        }

        /// <summary>True if <paramref name="gameObject"/> or one of its parents is marked.</summary>
        public static bool IsExcluded(GameObject gameObject)
        {
            for (Transform t = gameObject != null ? gameObject.transform : null; t != null; t = t.parent)
                if (IsMarked(t.gameObject))
                    return true;
            return false;
        }
    }
}
