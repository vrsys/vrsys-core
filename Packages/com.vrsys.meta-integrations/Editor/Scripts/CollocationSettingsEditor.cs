using UnityEditor;
using UnityEngine;
using VRSYS.Meta.Collocation;

namespace VRSYS.Meta.Editor
{
    [CustomEditor(typeof(CollocationSettings))]
    public class CollocationSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty _usePlayerPrefs;
        private SerializedProperty _discoveryTime;
        private SerializedProperty _skipSessionSearch;
        private SerializedProperty _retryTime;
        private SerializedProperty _maxRetries;
        private SerializedProperty _useLocalAnchor;
        private SerializedProperty _tryLoadLocalAnchor;
        private SerializedProperty _useDefaultSessionAnchor;
        private SerializedProperty _defaultSessionAnchorWorldPos;

        private void OnEnable()
        {
            _usePlayerPrefs = serializedObject.FindProperty("_usePlayerPrefs");
            _discoveryTime = serializedObject.FindProperty("_discoveryTime");
            _skipSessionSearch = serializedObject.FindProperty("_skipSessionSearch");
            _retryTime = serializedObject.FindProperty("_retryTime");
            _maxRetries = serializedObject.FindProperty("_maxRetries");
            _useLocalAnchor = serializedObject.FindProperty("_useLocalAnchor");
            _tryLoadLocalAnchor = serializedObject.FindProperty("_tryLoadLocalAnchor");
            _useDefaultSessionAnchor = serializedObject.FindProperty("_useDefaultSessionAnchor");
            _defaultSessionAnchorWorldPos = serializedObject.FindProperty("_defaultSessionAnchorWorldPos");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Workflow", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_useLocalAnchor, new GUIContent("Use Local Anchor"));
            EditorGUILayout.HelpBox(
                _useLocalAnchor.boolValue
                    ? "Loads or creates an anchor on this device. Session discovery, creation, joining, and sharing are bypassed."
                    : "Uses Meta colocation sessions. The user searches for, joins, or creates a shared session.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Local Anchor", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!_useLocalAnchor.boolValue))
                EditorGUILayout.PropertyField(_tryLoadLocalAnchor, new GUIContent("Try Load Local Anchor"));
            EditorGUILayout.HelpBox(
                _useLocalAnchor.boolValue
                    ? "When enabled, a saved anchor ID is loaded first. Missing or unusable anchors fall back to local anchor creation."
                    : "Only applies to the local-anchor workflow.",
                MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Session Discovery", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_useLocalAnchor.boolValue))
            {
                EditorGUILayout.PropertyField(_skipSessionSearch, new GUIContent("Skip Session Search"));

                using (new EditorGUI.DisabledScope(_skipSessionSearch.boolValue))
                    EditorGUILayout.PropertyField(_discoveryTime, new GUIContent("Discovery Time"));
            }
            EditorGUILayout.HelpBox(
                _useLocalAnchor.boolValue
                    ? "Disabled because the local-anchor workflow bypasses sessions."
                    : _skipSessionSearch.boolValue
                    ? "Skips discovery and immediately creates a development session for the local client."
                    : "Searches for existing sessions for the configured duration before offering session creation.",
                MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Session Anchor", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_useLocalAnchor.boolValue))
            {
                EditorGUILayout.PropertyField(_useDefaultSessionAnchor,
                    new GUIContent("Use Default Session Anchor"));

                using (new EditorGUI.DisabledScope(!_useDefaultSessionAnchor.boolValue))
                    EditorGUILayout.PropertyField(_defaultSessionAnchorWorldPos,
                        new GUIContent("Default World Position"));
            }
            EditorGUILayout.HelpBox(
                _useLocalAnchor.boolValue
                    ? "Disabled because the local-anchor workflow does not create a session anchor."
                    : _useDefaultSessionAnchor.boolValue
                    ? "The session host creates the anchor automatically at this world position with identity rotation."
                    : "The session host places and confirms the anchor manually.",
                MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Failure Handling", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_retryTime, new GUIContent("Retry Time"));
            EditorGUILayout.PropertyField(_maxRetries, new GUIContent("Max Retries"));
            EditorGUILayout.HelpBox(
                "Retry Time is the delay between failed API attempts. Max Retries counts retries after the initial attempt.",
                MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Persistence", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_usePlayerPrefs, new GUIContent("Use Player Prefs"));
            EditorGUILayout.HelpBox(
                "When enabled, runtime setting changes are stored in PlayerPrefs on this device and override the asset values.",
                MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
