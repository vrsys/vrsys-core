using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using Unity.Netcode; // Required for Directory access

namespace VRSYS.Scripts.Recording
{
#if UNITY_EDITOR
    [ExecuteInEditMode]
    [CustomEditor(typeof(RecorderController))]
    public class RecorderControllerEditor : Editor
    {
        private bool synchronisedPlayback;
        private bool lateJoinPlayback;
        private bool downloadFilesFromServer;
        private bool uploadFilesToServer;
        private bool enableDebugInfo;
        private string _downloadPassword = "";
        
        public override void OnInspectorGUI()
        {
            RecorderController controller = (RecorderController)target;
            EditorGUI.BeginChangeCheck();
            
            GUILayout.Label("The following button will setup scripts required for recording prefab information. \nThis is necessary during playback to instantiate the objects.");
            if (GUILayout.Button("Setup Requirements"))
            {
                SetupRequirements();
            }

            GUILayout.Label("\nThe following state stores information about the recording file as well as other information.");

            controller.recorderState = (RecorderState)EditorGUILayout.ObjectField("Recorder State",
                controller.GetComponent<RecorderState>(), typeof(RecorderState));

            if (controller.recorderState != null)
            {
                GUILayout.Label("\nRecording Directory:");
                if (GUILayout.Button("Open Recording Directory"))
                {
                    if (string.IsNullOrEmpty(controller.recorderState.recordingDirectory))
                    {
                        OpenRecordingDirectory(Application.persistentDataPath);
                    }
                    else{
                        OpenRecordingDirectory(controller.recorderState.recordingDirectory);
                    }
                }

                GUILayout.Label("\nDownload Project Zip:");
                _downloadPassword = EditorGUILayout.PasswordField("Download password", _downloadPassword);

                bool canDownload =
                    !string.IsNullOrEmpty(controller.recorderState.projectName) &&
                    !string.IsNullOrEmpty(controller.recorderState.selectedServer) &&
                    !string.IsNullOrEmpty(_downloadPassword);

                using (new EditorGUI.DisabledScope(!canDownload))
                {
                    if (GUILayout.Button("Download Project Zip"))
                    {
                        string projectName = controller.recorderState.projectName;
                        string serverAddress = controller.recorderState.selectedServer;
                        string saveDir = string.IsNullOrEmpty(controller.recorderState.recordingDirectory)
                            ? Application.persistentDataPath
                            : controller.recorderState.recordingDirectory;
                        string savePath = Path.Combine(saveDir, projectName + ".zip");

                        controller.StartCoroutine(NetworkUtils.DownloadProjectZip(
                            projectName,
                            serverAddress,
                            _downloadPassword,
                            savePath,
                            (ok, msg) =>
                            {
                                if (ok) Debug.Log("Download saved to: " + msg);
                                else Debug.LogError("Download failed: " + msg);
                            }));
                    }
                }
            }
            
            GUILayout.Label("\nThe following buttons can be used in the editor to create/replay recordings.");

            if (controller.CurrentState == State.Idle)
            {
                if (GUILayout.Button("Start Distributed Recording"))
                {
                    controller.SendStartRecordingEvents();
                }
                
                if (GUILayout.Button("Start Local Recording"))
                {
                    controller.StartLocalRecording();
                }

                if (GUILayout.Button("Start Distributed Replay"))
                {
                    controller.PrepareAndStartDistributedReplay();
                }
                
                if (GUILayout.Button("Start Local Replay"))
                {
                    controller.PrepareAndStartLocalReplay();
                }
            }

            if (controller.CurrentState == State.Recording)
            {
                if (GUILayout.Button("Stop Recording"))
                {
                    if(controller.localRecording)
                        controller.EndRecording();
                    else
                        controller.SendEndRecordingEvent();
                }
            }
            
            if (controller.CurrentState == State.Replaying)
            {
                if (controller.localPlayback && GUILayout.Button("Toggle Local Play/Pause"))
                {
                    controller.recorderState.replayPaused = !controller.recorderState.replayPaused;
                }
                if (!controller.localPlayback && GUILayout.Button("Toggle Global Play/Pause"))
                {
                    // TODO trigger global toggle pause event
                }
                

                if (GUILayout.Button("Stop Replay"))
                {
                    controller.SendEndReplayEvent();
                }
            }
            
            if (controller.CurrentState == State.PreparingReplay)
            {
                if (GUILayout.Button("Cancel Download & Playback"))
                {
                    // TODO: enable canceling of playback and downloads
                }
            }
            
            GUILayout.Label("\nThe following settings control playback behaviour.");
            synchronisedPlayback = GUILayout.Toggle(controller.synchronizedPlayback, "Synchronised playback");
            lateJoinPlayback = GUILayout.Toggle(controller.lateJoinPlayback, "Late join playback");
            uploadFilesToServer = GUILayout.Toggle(controller.uploadFilesToServer, "Upload recording files to server");
            downloadFilesFromServer = GUILayout.Toggle(controller.downloadFilesFromServer, "Download recording files from server");
            enableDebugInfo = GUILayout.Toggle(controller.debugLogs, "Print debug logs");

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                controller.synchronizedPlayback = synchronisedPlayback;
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                controller.lateJoinPlayback = lateJoinPlayback;
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                controller.downloadFilesFromServer = downloadFilesFromServer;
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                controller.uploadFilesToServer = uploadFilesToServer;
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                controller.debugLogs = enableDebugInfo;
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
        }

        private void OpenRecordingDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (Directory.Exists(path))
                {
                    EditorUtility.RevealInFinder(path);
                }
                else
                {
                    Debug.LogError("The specified recording directory does not exist: " + path);
                }
            }
            else
            {
                Debug.LogError("No recording directory is specified.");
            }
        }

        public void SetupRequirements()
        {
            AddPrefabInformationToAllNetworkPrefabs();
        }

        private void AddPrefabInformationToAllNetworkPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:NetworkPrefabsList");
    
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                NetworkPrefabsList prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);

                if (prefabsList == null)
                    continue;

                foreach (var networkPrefab in prefabsList.PrefabList)
                {
                    if (networkPrefab.Prefab == null)
                        continue;

                    GameObject go = networkPrefab.Prefab;
                    string assetPath = AssetDatabase.GetAssetPath(go);

                    RecordingPrefabInformation[] oldPrefabInformation = go.GetComponents<RecordingPrefabInformation>();
                    if (oldPrefabInformation.Length > 0)
                    {
                        foreach (var information in oldPrefabInformation)
                        {
                            information.Setup(assetPath);
                        }
                    }
                    else
                    {
                        RecordingPrefabInformation recordingPrefabInformation = go.AddComponent<RecordingPrefabInformation>();
                        if (recordingPrefabInformation != null)
                            recordingPrefabInformation.Setup(assetPath);
                    }

                    PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                }
            }
        }

        
        private void AddPrefabInformationToAllPrefabs()
        {
            string[] assetGUIDs = AssetDatabase.FindAssets("t:GameObject");
            Debug.Log("searching in " + assetGUIDs.Length + " gameobjects...");

            foreach (var guid in assetGUIDs)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                RecordingPrefabInformation[] oldPrefabInformation = go.GetComponents<RecordingPrefabInformation>();
                if (oldPrefabInformation.Length > 0)
                {
                    foreach (var information in oldPrefabInformation)
                    {
                        information.Setup(assetPath);
                    }
                }
                else
                {
                    RecordingPrefabInformation recordingPrefabInformation = go.AddComponent<RecordingPrefabInformation>();
                    if (recordingPrefabInformation != null)
                    {
                        recordingPrefabInformation.Setup(assetPath);
                    }
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            }
        }
    }
#endif
}
