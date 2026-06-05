using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRSYS.Scripts.Recording
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

        /// <summary>Register a component type that should be preserved on playback prefab instances.</summary>
        public static void Preserve(Type componentType)
        {
            if (componentType != null)
                PreservedTypes.Add(componentType);
        }

        public static void Preserve<T>() where T : Component => Preserve(typeof(T));

        public static bool IsPreserved(Component component)
        {
            if (component == null)
                return false;

            Type type = component.GetType();
            foreach (Type preserved in PreservedTypes)
            {
                if (preserved.IsAssignableFrom(type))
                    return true;
            }

            return false;
        }
    }
}
