
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRSYS.Core.Avatar;

namespace VRSYS.Core.Editor
{
    [CustomEditor(typeof(TrackedHandSerializer))]
    public class TrackedHandSerializerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            TrackedHandSerializer serializer = (TrackedHandSerializer)target;
            
            if(GUILayout.Button("Setup Hand Fidelity Options"))
                serializer.SetupHandFidelityOptions();
            
            if(GUILayout.Button("Clear Hand Fidelity Options"))
                serializer.ClearHandFidelityOptions();
        }
    }
}
#endif
