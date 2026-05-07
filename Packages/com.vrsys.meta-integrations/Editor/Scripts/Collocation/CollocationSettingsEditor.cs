// VRSYS plugin of Virtual Reality and Visualization Group (Bauhaus-University Weimar)
//  _    ______  _______  _______
// | |  / / __ \/ ___/\ \/ / ___/
// | | / / /_/ /\__ \  \  /\__ \
// | |/ / _, _/___/ /  / /___/ /
// |___/_/ |_|/____/  /_//____/
//
//  __                            __                       __   __   __    ___ .  . ___
// |__)  /\  |  | |__|  /\  |  | /__`    |  | |\ | | \  / |__  |__) /__` |  |   /\   |
// |__) /~~\ \__/ |  | /~~\ \__/ .__/    \__/ | \| |  \/  |___ |  \ .__/ |  |  /~~\  |
//
//       ___               __
// |  | |__  |  |\/|  /\  |__)
// |/\| |___ |  |  | /~~\ |  \
//
// Copyright (c) 2026 Virtual Reality and Visualization Group
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//-----------------------------------------------------------------
//   Authors:        Tony Zoeppig
//   Date:           2026
//-----------------------------------------------------------------

using UnityEditor;
using UnityEngine;

namespace VRSYS.Meta.Collocation
{
    [CustomEditor(typeof(CollocationSettings))]
    public class CollocationSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty _usePlayerPrefs;
        private SerializedProperty _discoveryTime;
        private SerializedProperty _retryTime;
        private SerializedProperty _maxRetries;
        private SerializedProperty _useLocalAnchor;
        private SerializedProperty _tryLoadLocalAnchor;
        private SerializedProperty _useDefaultSessionAnchor;
        private SerializedProperty _defaultSessionAnchorWorldPos;
        private SerializedProperty _autoStartSession;
        private SerializedProperty _defaultSessionName;

        private void OnEnable()
        {
            _usePlayerPrefs = serializedObject.FindProperty("_usePlayerPrefs");
            _discoveryTime = serializedObject.FindProperty("_discoveryTime");
            _retryTime = serializedObject.FindProperty("_retryTime");
            _maxRetries = serializedObject.FindProperty("_maxRetries");
            _useLocalAnchor = serializedObject.FindProperty("_useLocalAnchor");
            _tryLoadLocalAnchor = serializedObject.FindProperty("_tryLoadLocalAnchor");
            _useDefaultSessionAnchor = serializedObject.FindProperty("_useDefaultSessionAnchor");
            _defaultSessionAnchorWorldPos = serializedObject.FindProperty("_defaultSessionAnchorWorldPos");
            _autoStartSession = serializedObject.FindProperty("_autoStartSession");
            _defaultSessionName = serializedObject.FindProperty("_defaultSessionName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool useLocalAnchor = _useLocalAnchor.boolValue;

            // --- Data Saving ---
            DrawSectionHeader("Data Saving");
            EditorGUILayout.PropertyField(_usePlayerPrefs, new GUIContent("Use Player Prefs"));

            EditorGUILayout.Space();

            // --- General Properties ---
            DrawSectionHeader("General Properties");
            EditorGUILayout.PropertyField(_discoveryTime, new GUIContent("Discovery Time"));
            EditorGUILayout.PropertyField(_retryTime, new GUIContent("Retry Time"));
            EditorGUILayout.PropertyField(_maxRetries, new GUIContent("Max Retries"));

            EditorGUILayout.Space();

            // --- Local Anchors ---
            DrawSectionHeader("Local Anchors");
            EditorGUILayout.PropertyField(_useLocalAnchor, new GUIContent("Use Local Anchor"));

            if (useLocalAnchor)
                EditorGUILayout.PropertyField(_tryLoadLocalAnchor, new GUIContent("Try Load Local Anchor"));

            if (!useLocalAnchor)
            {
                EditorGUILayout.Space();

                // --- Session Default Anchor ---
                DrawSectionHeader("Session Default Anchor");
                EditorGUILayout.PropertyField(_useDefaultSessionAnchor, new GUIContent("Use Default Session Anchor"));

                if (_useDefaultSessionAnchor.boolValue)
                    EditorGUILayout.PropertyField(_defaultSessionAnchorWorldPos, new GUIContent("World Position"));

                EditorGUILayout.Space();

                // --- Session Auto Start ---
                DrawSectionHeader("Session Auto Start");
                EditorGUILayout.PropertyField(_autoStartSession, new GUIContent("Auto Start Session"));

                if (_autoStartSession.boolValue)
                {
                    EditorGUILayout.PropertyField(_defaultSessionName, new GUIContent("Default Session Name"));

                    if (string.IsNullOrEmpty(_defaultSessionName.stringValue))
                    {
                        EditorGUILayout.HelpBox("Default Session Name cannot be empty.", MessageType.Error);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSectionHeader(string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1f));
            EditorGUILayout.Space(2f);
        }
    }
}
