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
        private bool recordMicro;
        private bool recordAudioListener;
        private bool downloadFilesFromServer;
        private bool uploadFilesToServer;
        private bool enableDebugInfo;
        private Transform replayRoot;
        private string _downloadPassword = "";
        
        public override void OnInspectorGUI()
        {
            RecorderController controller = (RecorderController)target;
            EditorGUI.BeginChangeCheck();
            
            GUILayout.Label("The following buttons will setup scripts required for recording prefab information. \nThis is necessary during playback to instantiate the objects.");
            if (GUILayout.Button("Setup Requirements for Networked Prefabs"))
            {
                AddPrefabInformationToAllNetworkPrefabs();
            }

            if (GUILayout.Button("Setup Requirements for all Prefabs under Assets"))
            {
                AddPrefabInformationToAllPrefabs();
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
                    if(controller.recorderState != null && controller.recorderState.localRecording)
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
                    if (controller.localPlayback)
                        controller.EndReplay();
                    else
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
            replayRoot = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Replay Root", "Optional anchor for playback. When set, recorded objects are matched/placed relative to this transform: pre-existing duplicate objects beneath it are matched to the recording, and objects that have to be instantiated for playback are created under it. Its position/rotation/scale thus offsets the whole replay. Leave empty to match/place objects at the scene root."),
                controller.replayRoot, typeof(Transform), true);
            recordMicro = GUILayout.Toggle(controller.recordMicro, "Record microphone");
            recordAudioListener = GUILayout.Toggle(controller.recordAudioListener, "Record audio listener");
            synchronisedPlayback = GUILayout.Toggle(controller.synchronizedPlayback, "Synchronised playback");
            lateJoinPlayback = GUILayout.Toggle(controller.lateJoinPlayback, "Late join playback");
            uploadFilesToServer = GUILayout.Toggle(controller.uploadFilesToServer, "Upload recording files to server");
            downloadFilesFromServer = GUILayout.Toggle(controller.downloadFilesFromServer, "Download recording files from server");
            enableDebugInfo = GUILayout.Toggle(controller.debugLogs, "Print debug logs");

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                controller.replayRoot = replayRoot;
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                controller.recordMicro = recordMicro;
                controller.recordAudioListener = recordAudioListener;
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
                    string addressableKey = ResolveAddressableKey(assetPath);

                    RecordingPrefabInformation[] oldPrefabInformation = go.GetComponents<RecordingPrefabInformation>();
                    if (oldPrefabInformation.Length > 0)
                    {
                        foreach (var information in oldPrefabInformation)
                        {
                            information.Setup(assetPath, addressableKey);
                        }
                    }
                    else
                    {
                        RecordingPrefabInformation recordingPrefabInformation = go.AddComponent<RecordingPrefabInformation>();
                        if (recordingPrefabInformation != null)
                            recordingPrefabInformation.Setup(assetPath, addressableKey);
                    }

                    PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                }
            }
        }

        
        private void AddPrefabInformationToAllPrefabs()
        {
            // Restrict the search to the Assets directory (not Packages) and to actual prefab files.
            // "t:GameObject" also matches imported models (e.g. .fbx), which are read-only and cannot
            // take components, so filter by the .prefab extension.
            string[] assetGUIDs = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" });
            Debug.Log("searching in " + assetGUIDs.Length + " gameobjects...");

            foreach (var guid in assetGUIDs)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (go == null)
                    continue;

                string addressableKey = ResolveAddressableKey(assetPath);

                RecordingPrefabInformation[] oldPrefabInformation = go.GetComponents<RecordingPrefabInformation>();
                if (oldPrefabInformation.Length > 0)
                {
                    foreach (var information in oldPrefabInformation)
                    {
                        information.Setup(assetPath, addressableKey);
                    }
                }
                else
                {
                    RecordingPrefabInformation recordingPrefabInformation = go.AddComponent<RecordingPrefabInformation>();
                    if (recordingPrefabInformation != null)
                    {
                        recordingPrefabInformation.Setup(assetPath, addressableKey);
                    }
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            }
        }

        // Returns the Addressables address of the asset at the given path, or "" if the asset is not
        // marked Addressable or the Addressables package is not installed. Resolved via reflection so the
        // recording package keeps zero hard dependency on com.unity.addressables; it activates
        // automatically once Addressables is present in the project.
        private static string ResolveAddressableKey(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return "";

            System.Type defaultObjectType = System.Type.GetType(
                "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
            if (defaultObjectType == null)
                return ""; // Addressables editor assembly not present

            object settings = defaultObjectType
                .GetProperty("Settings", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                ?.GetValue(null);
            if (settings == null)
                return ""; // Addressables not initialised in this project

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                return "";

            object entry = settings.GetType()
                .GetMethod("FindAssetEntry", new[] { typeof(string) })
                ?.Invoke(settings, new object[] { guid });
            if (entry == null)
                return ""; // asset is not marked Addressable

            return entry.GetType().GetProperty("address")?.GetValue(entry) as string ?? "";
        }
    }
#endif
}
