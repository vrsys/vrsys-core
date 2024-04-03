#if UNITY_EDITOR
using OdinNative.Unity;
using UnityEngine;
using UnityEditor;

namespace OdinNative.Unity.UIEditor
{
    /// <summary>
    /// Adds a custom layout to the OdinHandler component
    /// </summary>
    [CustomEditor(typeof(OdinHandler))]
    public class OdinHandlerEditor : Editor
    {
        SerializedProperty MicrophoneObject;

        SerializedProperty OnRoomJoin;
        SerializedProperty OnRoomJoined;
        SerializedProperty OnRoomUpdated;
        SerializedProperty OnRoomLeave;
        SerializedProperty OnRoomLeft;
        SerializedProperty OnMessageReceived;

        SerializedProperty OnPeerJoined;
        SerializedProperty OnPeerUpdated;
        SerializedProperty OnPeerLeft;
        SerializedProperty OnMediaAdded;
        SerializedProperty OnMediaActiveStateChanged;
        SerializedProperty OnMediaRemoved;

        SerializedProperty Persistent;
        SerializedProperty UseManual3DAudio;
        SerializedProperty PlaybackCreation;
        SerializedProperty AudioMixerObject;
        SerializedProperty AudioMixerGroupObject;

        private bool toggleEventListeners;
        private bool toggleHandlerSettings;

        private GUIStyle FoldoutLabelStyle;
        private GUIStyle ToolbarTabStyle;

        int roomEventToolbarInt = 0;
        string[] roomEventToolbarLabels = { "Room join", "Room joined", "Room updated", "Room leave", "Room left", "Message Received" };

        int peerEventToolbarInt = 0;
        string[] peerEventToolbarLabels = { "Peer joined", "Peer left", "Peer updated" };

        int mediaEventToolbarInt = 0;
        string[] mediaEventToolbarLabels = { "Media added", "Media state changed", "Media removed" };

        int audioModeSelected = 1;
        GUIContent[] audioModeOptions = new GUIContent[] { 
            new GUIContent("Manual positional audio", "Setup positional audio PlaybackStreams for manual use (mutually exclusive to \"Playback auto creation\")"),
            new GUIContent("Playback auto creation", "Automatically creates Playback components within the handler object (mutually exclusive to \"Manual positional audio\")")
        };

        void OnEnable()
        {
            MicrophoneObject = serializedObject.FindProperty("Microphone");

            OnRoomJoin = serializedObject.FindProperty("OnRoomJoin");
            OnRoomJoined = serializedObject.FindProperty("OnRoomJoined");
            OnRoomUpdated = serializedObject.FindProperty("OnRoomUserDataChanged");
            OnRoomLeave = serializedObject.FindProperty("OnRoomLeave");
            OnRoomLeft = serializedObject.FindProperty("OnRoomLeft");
            OnMessageReceived = serializedObject.FindProperty("OnMessageReceived");

            OnPeerJoined = serializedObject.FindProperty("OnPeerJoined");
            OnPeerUpdated = serializedObject.FindProperty("OnPeerUserDataChanged");
            OnPeerLeft = serializedObject.FindProperty("OnPeerLeft");
            OnMediaAdded = serializedObject.FindProperty("OnMediaAdded");
            OnMediaActiveStateChanged = serializedObject.FindProperty("OnMediaActiveStateChanged");
            OnMediaRemoved = serializedObject.FindProperty("OnMediaRemoved");

            Persistent = serializedObject.FindProperty("_persistent");
            UseManual3DAudio = serializedObject.FindProperty("Use3DAudio");
            PlaybackCreation = serializedObject.FindProperty("CreatePlayback");
            AudioMixerObject = serializedObject.FindProperty("PlaybackAudioMixer");
            AudioMixerGroupObject = serializedObject.FindProperty("PlaybackAudioMixerGroup");
        }

        /// <summary>
        /// Implementation for the Unity custom inspector of OdinHandler.
        /// </summary>
        public override void OnInspectorGUI()
        {
            changeStyles();
            OdinHandler odinHandler = (target as OdinHandler);
            if (odinHandler == null)
            {
                DrawDefaultInspector(); // fallback
                return;
            }

            EditorGUILayout.PropertyField(MicrophoneObject, new GUIContent("Microphone Reader", "MonoBehaviour class to read data from the microphone and send it to ODIN."));
            EditorGUILayout.PropertyField(AudioMixerObject, new GUIContent("Playback Mixer", "Unity Audio Mixer to use for selecting different Audio Mixer Groups."));
            EditorGUILayout.PropertyField(AudioMixerGroupObject, new GUIContent("Default Playback Mixer Group", "Unity Audio Mixer Group to use for inserting default effects that process the signal of incoming ODIN audio streams and changing the parameters of effects."));

            GUILayout.Space(10);
            CreateAudioBoxes();

            GUILayout.Space(10);
            CreateEventListenersLayout();

            serializedObject.ApplyModifiedProperties();
        }

        private void CreateAudioBoxes(bool radioStyle = true)
        {
            if (radioStyle)
            {
                if (UseManual3DAudio.boolValue)
                    audioModeSelected = 0;
                else
                    audioModeSelected = 1;
                audioModeSelected = GUILayout.SelectionGrid(audioModeSelected, audioModeOptions, audioModeOptions.Length, EditorStyles.radioButton);
                switch (audioModeSelected)
                {
                    case 0:
                        UseManual3DAudio.boolValue = true;
                        PlaybackCreation.boolValue = false;
                        break;
                    case 1:
                        UseManual3DAudio.boolValue = false;
                        PlaybackCreation.boolValue = true;
                        break;
                }
            }
            else
            {
                EditorGUILayout.PropertyField(UseManual3DAudio, new GUIContent("Manual positional audio", "Setup positional audio PlaybackStreams for manual use (mutually exclusive to \"Playback auto creation\")"));
                EditorGUILayout.PropertyField(PlaybackCreation, new GUIContent("Playback auto creation", "Automatically creates Playback components within the handler object (mutually exclusive to \"Manual positional audio\")"));
            }
        }

        private void changeStyles()
        {
            FoldoutLabelStyle = new GUIStyle(EditorStyles.foldout);
            FoldoutLabelStyle.fontStyle = FontStyle.Bold;
            FoldoutLabelStyle.fontSize = 14;

            ToolbarTabStyle = new GUIStyle(EditorStyles.toolbarButton);
        }

        private static void drawRect(int height)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            rect.height = height;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            GUILayout.Space(3);
        }

        private void CreateEventListenersLayout()
        {
            toggleEventListeners = EditorGUILayout.Foldout(toggleEventListeners, "Event Handling", FoldoutLabelStyle);
            drawRect(2);
            if (toggleEventListeners)
            {
                roomEventToolbarInt = GUILayout.Toolbar(roomEventToolbarInt, roomEventToolbarLabels, ToolbarTabStyle, GUI.ToolbarButtonSize.FitToContents);
                drawRect(1);
                switch (roomEventToolbarInt)
                {
                    // Room join
                    case 0:
                        {
                            EditorGUILayout.PropertyField(OnRoomJoin, new GUIContent("OnRoomJoin", "Setup the room join event"));
                            break;
                        }
                    // Room joined
                    case 1:
                        {
                            EditorGUILayout.PropertyField(OnRoomJoined, new GUIContent("OnRoomJoined", "Setup the room joined event"));
                            break;
                        }
                    // Peer updated
                    case 2:
                        {
                            EditorGUILayout.PropertyField(OnRoomUpdated, new GUIContent("OnRoomUserDataChanged", "Setup the room user data changed event"));
                            break;
                        }
                    // Room leave
                    case 3:
                        {
                            EditorGUILayout.PropertyField(OnRoomLeave, new GUIContent("OnRoomLeave", "Setup the room leave event"));
                            break;
                        }
                    // Room left
                    case 4:
                        {
                            EditorGUILayout.PropertyField(OnRoomLeft, new GUIContent("OnRoomLeft", "Setup the room left event"));
                            break;
                        }
                    // Message received
                    case 5:
                        {
                            EditorGUILayout.PropertyField(OnMessageReceived, new GUIContent("OnMessageReceived", "Setup the message received event"));
                            break;
                        }
                }

                GUILayout.Space(10);

                peerEventToolbarInt = GUILayout.Toolbar(peerEventToolbarInt, peerEventToolbarLabels, ToolbarTabStyle, GUI.ToolbarButtonSize.FitToContents);
                drawRect(1);
                switch (peerEventToolbarInt)
                {
                    // Peer joined
                    case 0:
                        {
                            EditorGUILayout.PropertyField(OnPeerJoined, new GUIContent("OnPeerJoined", "Setup the peer joined event"));
                            break;
                        }
                    // Peer left
                    case 1:
                        {
                            EditorGUILayout.PropertyField(OnPeerLeft, new GUIContent("OnPeerLeft", "Setup the peer left event"));
                            break;
                        }
                    // Peer updated
                    case 2:
                        {
                            EditorGUILayout.PropertyField(OnPeerUpdated, new GUIContent("OnPeerUserDataChanged", "Setup the peer user data changed event"));
                            break;
                        }
                }

                GUILayout.Space(10);

                mediaEventToolbarInt = GUILayout.Toolbar(mediaEventToolbarInt, mediaEventToolbarLabels, ToolbarTabStyle, GUI.ToolbarButtonSize.FitToContents);
                drawRect(1);
                switch (mediaEventToolbarInt)
                {
                    // Media added
                    case 0:
                        {
                            EditorGUILayout.PropertyField(OnMediaAdded, new GUIContent("OnMediaAdded", "Setup the media added event"));
                            break;
                        }
                    // Media state changed
                    case 1:
                        {
                            EditorGUILayout.PropertyField(OnMediaActiveStateChanged, new GUIContent("OnMediaActiveStateChanged", "Setup the media active state changed event"));
                            break;
                        }
                    // Media removed
                    case 2:
                        {
                            EditorGUILayout.PropertyField(OnMediaRemoved, new GUIContent("OnMediaRemoved", "Setup the media removed event"));
                            break;
                        }
                }
            }
        }
    }
}
#endif