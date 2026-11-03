using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRSYS.Recording
{
    /// <summary>
    /// Registry of component types that must survive <see cref="Utils.RemoveCustomComponents"/> when
    /// instantiating prefabs for playback. The core recording package keeps its own and Unity's
    /// built-in rendering/UI components by default; external assemblies (e.g. the Meta Avatar
    /// integration) and the host application register additional types here so that
    /// <see cref="Utils.RemoveCustomComponents"/> never has to reference them directly.
    /// </summary>
    public static class ReplayComponentPreserver
    {
        private static readonly HashSet<Type> PreservedTypes = new HashSet<Type>();
        private static readonly HashSet<Type> PreservedOnNetworkedCopyTypes = new HashSet<Type>();

        /// <summary>Register a component type that should be preserved on playback prefab instances.</summary>
        public static void Preserve(Type componentType)
        {
            if (componentType != null)
                PreservedTypes.Add(componentType);
        }

        public static void Preserve<T>() where T : Component => Preserve(typeof(T));

        /// <summary>
        /// Register a component type that should only be preserved on replay copies of networked prefabs (see
        /// RecorderController.spawnNetworkedPrefabsForReplay), e.g. colliders and grab interactables so that users can
        /// still pick up a replayed prop. Other playback instances (e.g. replayed avatars) still lose it.
        /// </summary>
        public static void PreserveOnNetworkedCopies(Type componentType)
        {
            if (componentType != null)
                PreservedOnNetworkedCopyTypes.Add(componentType);
        }

        public static void PreserveOnNetworkedCopies<T>() where T : Component => PreserveOnNetworkedCopies(typeof(T));

        public static bool IsPreservedOnNetworkedCopies(Component component) => IsAssignableToAny(component, PreservedOnNetworkedCopyTypes);

        public static bool IsPreserved(Component component) => IsAssignableToAny(component, PreservedTypes);

        private static bool IsAssignableToAny(Component component, HashSet<Type> types)
        {
            if (component == null)
                return false;

            Type type = component.GetType();
            foreach (Type preserved in types)
            {
                if (preserved.IsAssignableFrom(type))
                    return true;
            }

            return false;
        }
    }
}
