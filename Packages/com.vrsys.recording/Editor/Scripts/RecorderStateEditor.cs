using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VRSYS.Scripts.Recording
{
#if UNITY_EDITOR
    [CustomEditor(typeof(RecorderState))]
    public class RecorderStateEditor : Editor
    {
        private string projectName;
        private int recorderID;
        private string recordingFile;
        private string fixedPlaybackRecordingName;
        private string selectedServer;
        private int selectedReplayFileIndex = 0; // Add this line to store the selected string index
        private string[] replayFileOptions; // Array of strings for the dropdown
        private bool useLocalReplayFiles = true; // Mirror of RecorderState.useLocalReplayFiles for the inspector

        
        public override void OnInspectorGUI()
        {
            RecorderState state = (RecorderState)target;
            bool change = false;
            
            EditorGUI.BeginChangeCheck();
            
            projectName = EditorGUILayout.TextField(new GUIContent("Project name: ", "Name of the project for which recordings are created. To be used for up- and download."), state.projectName);
            
            recorderID = EditorGUILayout.IntField(new GUIContent("Recorder ID", "The ID of a recorder needs to be unique."), state.recorderID);

            recordingFile = EditorGUILayout.TextField(new GUIContent("Recording Name: ", "Name of the recording file that will be created when recording."), state.recordingFile);
            
            useLocalReplayFiles = EditorGUILayout.Toggle(new GUIContent("Use Local Replay Files", "If enabled, the playback dropdown lists recordings found in the local recording directory and the server is not queried for the replay list. If disabled, it lists recordings retrieved from the server."), state.useLocalReplayFiles);

            if (useLocalReplayFiles)
                IdentifyLocallyStoredRecordings(Application.persistentDataPath);
            else
                replayFileOptions = state.replayList != null ? state.replayList.replayNames : null;

            if (replayFileOptions == null)
                replayFileOptions = new string[0];

            selectedReplayFileIndex = Mathf.Clamp(selectedReplayFileIndex, 0, Mathf.Max(0, replayFileOptions.Length - 1));

            int newSelectedReplayFileIndex = EditorGUILayout.Popup("Playback File:", selectedReplayFileIndex, replayFileOptions);
            change = true;
            if (newSelectedReplayFileIndex != selectedReplayFileIndex || 
                replayFileOptions.Length <= selectedReplayFileIndex || 
                 !replayFileOptions.Contains(fixedPlaybackRecordingName) || 
                replayFileOptions[selectedReplayFileIndex] != replayFileOptions[newSelectedReplayFileIndex])
            {
                selectedReplayFileIndex = newSelectedReplayFileIndex;
                
                if(replayFileOptions.Length > 0)
                    fixedPlaybackRecordingName = replayFileOptions[selectedReplayFileIndex];
                change = true;
            }
            
            EditorGUILayout.LabelField("State", state.currentState.ToString());
            
            selectedServer = EditorGUILayout.TextField(new GUIContent("Selected Server", "Url of the server to which files are uploaded/from where files are downloaded."), state.selectedServer);
            
            if (state.currentState == State.Replaying)
            {
                EditorGUILayout.LabelField("Playback Time", state.currentReplayTime.ToString("F"));
                EditorGUILayout.LabelField("Recording Duration", state.recordingDuration.ToString("F"));
            }

            if(state.currentState == State.Recording)
                EditorGUILayout.LabelField("Recording Time", state.currentRecordingTime.ToString("F"));

            if (EditorGUI.EndChangeCheck() || change)
            {
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                state.recorderID = recorderID;
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                state.recordingFile = recordingFile;
                Undo.RecordObject(target, "Changed Values");
                state.projectName = projectName;
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                state.fixedPlaybackRecordingName = fixedPlaybackRecordingName;
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                state.selectedServer = selectedServer;
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                state.useLocalReplayFiles = useLocalReplayFiles;
            }
        }

        private void IdentifyLocallyStoredRecordings(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Debug.LogError("Directory does not exist: " + directory);
                return;
            }

            string[] filesWithPaths = Directory.GetFiles(directory, "*.recordmeta");
            replayFileOptions = new string[filesWithPaths.Length];

            for (int i = 0; i < filesWithPaths.Length; i++)
                replayFileOptions[i] = Path.GetFileNameWithoutExtension(filesWithPaths[i]);
        }
    }
#endif
}