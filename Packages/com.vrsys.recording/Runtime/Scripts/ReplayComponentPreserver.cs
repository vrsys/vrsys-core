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
        // Type -> optional condition per component (null: always preserved).
        private static readonly Dictionary<Type, Func<Component, bool>> PreservedOnNetworkedCopyTypes =
            new Dictionary<Type, Func<Component, bool>>();

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
        /// still pick up a replayed prop. Other playback instances (e.g. replayed avatars) still lose it. With a
        /// condition, only components it accepts are preserved (e.g. colliders except those of a replayed tablet).
        /// </summary>
        public static void PreserveOnNetworkedCopies(Type componentType, Func<Component, bool> condition = null)
        {
            if (componentType != null)
                PreservedOnNetworkedCopyTypes[componentType] = condition;
        }

        public static void PreserveOnNetworkedCopies<T>(Func<T, bool> condition = null) where T : Component =>
            PreserveOnNetworkedCopies(typeof(T), condition == null ? null : c => condition((T)c));

        public static bool IsPreservedOnNetworkedCopies(Component component)
        {
            if (component == null)
                return false;

            Type type = component.GetType();
            foreach (var preserved in PreservedOnNetworkedCopyTypes)
            {
                if (preserved.Key.IsAssignableFrom(type) && (preserved.Value == null || preserved.Value(component)))
                    return true;
            }
            return false;
        }

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
