#if UNITY_EDITOR
using OdinNative.Unity.Audio;
using UnityEngine;
using UnityEditor;

namespace OdinNative.Unity.UIEditor
{
    /// <summary>
    /// Adds a custom layout to the MicrophoneReader component
    /// </summary>
    [CustomEditor(typeof(MicrophoneReader))]
    public class MicrophoneReaderEditor : Editor
    {
        SerializedProperty CaptureAudio;
        SerializedProperty SilenceAudio;
        SerializedProperty RecordingLoop;
        SerializedProperty CustomInputDevice;
        SerializedProperty InputDevice;

        SerializedProperty AudioClipLength;
        SerializedProperty OverrideSampleRate;
        SerializedProperty SampleRate;
        SerializedProperty StartListener;

        SerializedProperty CustomMicVolumeScale;
        SerializedProperty MicVolumeScale;

        SerializedProperty LoopbackTestRunner;

        private bool toggleMicSettings;
        private bool toggleShowTest;

        private GUIStyle FoldoutLabelStyle;

        void OnEnable()
        {
            CaptureAudio = serializedObject.FindProperty("RedirectCapturedAudio");
            SilenceAudio = serializedObject.FindProperty("SilenceCapturedAudio");
            RecordingLoop = serializedObject.FindProperty("ContinueRecording");
            CustomInputDevice = serializedObject.FindProperty("CustomInputDevice");
            InputDevice = serializedObject.FindProperty("InputDevice");

            AudioClipLength = serializedObject.FindProperty("AudioClipLength");
            OverrideSampleRate = serializedObject.FindProperty("OverrideSampleRate");
            SampleRate = serializedObject.FindProperty("SampleRate");
            StartListener = serializedObject.FindProperty("AutostartListen");

            CustomMicVolumeScale = serializedObject.FindProperty("CustomMicVolumeScale");
            MicVolumeScale = serializedObject.FindProperty("MicVolumeScale");

            LoopbackTestRunner = serializedObject.FindProperty("Loopback");
        }

        /// <summary>
        /// Implementation for the Unity custom inspector of MicrophoneReader.
        /// </summary>
        public override void OnInspectorGUI()
        {
            changeStyles();
            MicrophoneReader micReader = (target as MicrophoneReader);
            if (micReader == null)
            {
                DrawDefaultInspector(); // fallback
                return;
            }

            EditorGUILayout.PropertyField(CaptureAudio, new GUIContent("Redirect Captured Audio", "Redirect the captured audio to all rooms."));
            EditorGUILayout.PropertyField(SilenceAudio, new GUIContent("Silence Captured Audio", "Silence the captured audio in all rooms."));
            EditorGUILayout.PropertyField(RecordingLoop, new GUIContent("Continue Recording", "Indicates whether the recording should continue recording if AudioClipLength is reached, and wrap around and record from the beginning of the AudioClip."));
            EditorGUILayout.PropertyField(CustomMicVolumeScale, new GUIContent("Microphone Boost", "Indicates whether the recording should be boosted by a fixed scale."));
            if (CustomMicVolumeScale.boolValue && MicVolumeScale.propertyType == SerializedPropertyType.Float)
                EditorGUILayout.Slider(MicVolumeScale, 0f, 2f, new GUIContent("Mic-Boost Volume Scale", "Indicates the scale of boost for the recording. *Caution, can cause distortions i.e overdrive and 0 is silence."));
            EditorGUILayout.PropertyField(CustomInputDevice, new GUIContent("Input Device", "Enable custom input device. If Disabled the default is the first element in Microphone.devices"));
            if(CustomInputDevice.boolValue)
                EditorGUILayout.PropertyField(InputDevice, new GUIContent("Device Name", "The name of the device. If you pass a empty string for the device name then the default microphone will be used. You can get a list of available microphone devices from Microphone.devices"));

            GUILayout.Space(10);
            CreateMicSettingsLayout();
            if (toggleShowTest)
            {
                GUILayout.Space(10);
                EditorGUILayout.PropertyField(RecordingLoop, new GUIContent("Loopback Test", "Start/Stop Audio- Loopback"));
            }
            serializedObject.ApplyModifiedProperties();

        }

        private void changeStyles()
        {
            FoldoutLabelStyle = new GUIStyle(EditorStyles.foldout);
            FoldoutLabelStyle.fontStyle = FontStyle.Bold;
            FoldoutLabelStyle.fontSize = 14;
        }

        private static void drawRect(int height)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            rect.height = height;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            GUILayout.Space(3);

        }

        private void CreateMicSettingsLayout()
        {
            toggleMicSettings = EditorGUILayout.Foldout(toggleMicSettings, "Mic-AudioClip Settings", FoldoutLabelStyle);
            drawRect(2);
            if (toggleMicSettings)
            {
                EditorGUILayout.PropertyField(AudioClipLength, new GUIContent("AudioClip length", "Set the length of the AudioClip produced by the recording"));
                EditorGUILayout.PropertyField(OverrideSampleRate, new GUIContent("Override SampleRate", "Enable, to override \"Capture device sample rate\" from Odin Editor Config for this Gameobject"));
                if (OverrideSampleRate.boolValue)
                    EditorGUILayout.PropertyField(SampleRate, new GUIContent("SampleRate", "The new SampleRate to use for microphone capture"));
                EditorGUILayout.PropertyField(StartListener, new GUIContent("Autostart Microphone-Listen", "Automatical microphone start on Start()"));
            }
        }
    }
}
#endif
