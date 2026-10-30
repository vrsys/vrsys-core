using UnityEngine;

namespace VRSYS.Recording
{
    /// <summary>
    /// Marks an object that a replay created (a prefab copy, a networked copy or an empty placeholder for a recorded
    /// object missing from the scene), including every object in its subtree. The ScenePreparator adds it and destroys
    /// the object when the replay ends, so its original pose is not restored like that of an object that was in the
    /// scene before.
    ///
    /// Replaces the former "InstantiatedForPlayback" tag: no tag has to be set up in the project, and copies keep the
    /// tags of their prefab. Use <see cref="IsReplayInstance"/> to tell replay copies apart from the scene's objects.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class ReplayInstance : MonoBehaviour
    {
        [Tooltip("ID of the recorder whose replay created this object.")]
        public int recorderId = -1;

        /// <summary>True if <paramref name="gameObject"/> was created by a replay.</summary>
        public static bool IsReplayInstance(GameObject gameObject)
        {
            return gameObject != null && gameObject.TryGetComponent(out ReplayInstance _);
        }

        /// <summary>True if <paramref name="gameObject"/> was created by a replay of the recorder <paramref name="recorderId"/>.</summary>
        public static bool IsReplayInstanceOf(GameObject gameObject, int recorderId)
        {
            return gameObject != null && gameObject.TryGetComponent(out ReplayInstance instance) &&
                   instance.recorderId == recorderId;
        }

        /// <summary>Marks <paramref name="root"/> and all objects beneath it as created by the replay of <paramref name="recorderId"/>.</summary>
        public static void Mark(Transform root, int recorderId)
        {
            if (root == null)
                return;
            foreach (Transform child in root)
                Mark(child, recorderId);

            if (!root.TryGetComponent(out ReplayInstance instance))
                instance = root.gameObject.AddComponent<ReplayInstance>();
            instance.recorderId = recorderId;
        }
    }
}
