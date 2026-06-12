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
        private bool recordAllSoundSources;
        private bool attachTransformRecorderToAll;
        private bool replayHierarchyChanges;
        private bool playbackTransform;
        private bool replayAudio;
        private bool recordOnLocalTransformChangesOnly;
        private int transformRecordingStepsPerSecond;
        private bool downloadFilesFromServer;
        private bool uploadFilesToServer;
        private bool createWAV;
        private bool createCSV;
        private bool enableDebugInfo;
        private float maxSynchronizationTimeMS;
        private int recordingMaxBufferSize;
        private int replayBufferNumber;
        private float replayBufferTimeInterval;
        private int recordingSoundMaxBufferSize;
        private LogLevel pluginLogLevel;
        private Transform replayRoot;
        private string _downloadPassword = "";

        // Foldout expand/collapse state. Defaults follow the agreed layout: the frequently used
        // recording/playback sections start expanded; one-time setup and infrequent sections start collapsed.
        private bool _showSceneSetup = false;
        private bool _showTransformRecording = true;
        private bool _showAudioRecording = true;
        private bool _showPlayback = true;
        private bool _showServerFiles = false;
        private bool _showOutputDebug = false;
        
        public override void OnInspectorGUI()
        {
            RecorderController controller = (RecorderController)target;
            NetworkController networkController = controller.GetComponent<NetworkController>();
            if (controller.pluginSettings == null)
                controller.pluginSettings = new RecordingPluginSettings();
            EditorGUI.BeginChangeCheck();
            
            // --- Always-visible top section: Recorder State + the primary recording/replay controls ---

            GUILayout.Label("\nThe following state stores information about the recording file as well as other information.");

            controller.recorderState = (RecorderState)EditorGUILayout.ObjectField("Recorder State",
                controller.GetComponent<RecorderState>(), typeof(RecorderState));

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

            EditorGUILayout.Space();

            // --- Scene Setup: one-time prefab setup actions. Collapsed by default. ---
            _showSceneSetup = EditorGUILayout.BeginFoldoutHeaderGroup(_showSceneSetup, "Scene Setup");
            if (_showSceneSetup)
            {
                EditorGUI.indentLevel++;
                GUILayout.Label("These buttons set up the scripts required for recording prefab information.\nThis is necessary during playback to instantiate the objects.");
                if (GUILayout.Button("Setup Requirements for Networked Prefabs"))
                {
                    AddPrefabInformationToAllNetworkPrefabs();
                }

                if (GUILayout.Button("Setup Requirements for all Prefabs under Assets"))
                {
                    AddPrefabInformationToAllPrefabs();
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // --- Transform Recording: capture options for transforms. Expanded by default. ---
            _showTransformRecording = EditorGUILayout.BeginFoldoutHeaderGroup(_showTransformRecording, "Transform Recording");
            if (_showTransformRecording)
            {
                EditorGUI.indentLevel++;
                attachTransformRecorderToAll = EditorGUILayout.Toggle(
                    new GUIContent("Attach to all transforms", "Attach a transform recorder to every object in the scene when recording."),
                    controller.attachTransformRecorderToAll);
                transformRecordingStepsPerSecond = EditorGUILayout.IntField(
                    new GUIContent("Recording steps per second", "How many transform samples are captured per second while recording."),
                    controller.transformRecordingStepsPerSecond);
                recordOnLocalTransformChangesOnly = EditorGUILayout.Toggle(
                    new GUIContent("Record on local changes only", "When enabled, a transform is recorded only when its local position/rotation/scale changes. When disabled, changes are detected from the global (world) transform instead."),
                    controller.recordOnLocalTransformChangesOnly);
                EditorGUI.indentLevel--;
            }
            else
            {
                // Keep the working copies in sync with the stored values while the section is collapsed,
                // so the change-check below does not write back stale state.
                attachTransformRecorderToAll = controller.attachTransformRecorderToAll;
                transformRecordingStepsPerSecond = controller.transformRecordingStepsPerSecond;
                recordOnLocalTransformChangesOnly = controller.recordOnLocalTransformChangesOnly;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // --- Audio Recording: audio capture options. Expanded by default. ---
            _showAudioRecording = EditorGUILayout.BeginFoldoutHeaderGroup(_showAudioRecording, "Audio Recording");
            if (_showAudioRecording)
            {
                EditorGUI.indentLevel++;
                recordMicro = EditorGUILayout.Toggle(
                    new GUIContent("Record microphone", "Capture the local microphone into the recording."),
                    controller.recordMicro);
                recordAudioListener = EditorGUILayout.Toggle(
                    new GUIContent("Record audio listener", "Capture the scene's AudioListener output into the recording."),
                    controller.recordAudioListener);
                recordAllSoundSources = EditorGUILayout.Toggle(
                    new GUIContent("Record all sound sources", "Capture every AudioSource in the scene into the recording."),
                    controller.recordAllSoundSources);
                EditorGUI.indentLevel--;
            }
            else
            {
                // Keep the working copies in sync with the stored values while the section is collapsed,
                // so the change-check below does not write back stale toggle state.
                recordMicro = controller.recordMicro;
                recordAudioListener = controller.recordAudioListener;
                recordAllSoundSources = controller.recordAllSoundSources;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // --- Playback: settings that control how a recording is replayed. Expanded by default. ---
            _showPlayback = EditorGUILayout.BeginFoldoutHeaderGroup(_showPlayback, "Playback");
            if (_showPlayback)
            {
                EditorGUI.indentLevel++;
                replayRoot = (Transform)EditorGUILayout.ObjectField(
                    new GUIContent("Replay Root", "Optional anchor for playback. When set, recorded objects are matched/placed relative to this transform: pre-existing duplicate objects beneath it are matched to the recording, and objects that have to be instantiated for playback are created under it. Its position/rotation/scale thus offsets the whole replay. Leave empty to match/place objects at the scene root."),
                    controller.replayRoot, typeof(Transform), true);
                replayHierarchyChanges = EditorGUILayout.Toggle(
                    new GUIContent("Replay hierarchy changes", "Reapply recorded reparenting (hierarchy changes) during playback. Disable to keep objects under their initial parent."),
                    controller.replayHierarchyChanges);
                playbackTransform = EditorGUILayout.Toggle(
                    new GUIContent("Playback transform", "Attach TransformRecorder components during replay so recorded transforms are applied."),
                    controller.playbackTransform);
                replayAudio = EditorGUILayout.Toggle(
                    new GUIContent("Playback audio", "Handle recorded audio sources during replay."),
                    controller.replayAudio);
                synchronisedPlayback = EditorGUILayout.Toggle(
                    new GUIContent("Synchronised playback", "Keep playback time synchronised across all networked clients."),
                    controller.synchronizedPlayback);
                lateJoinPlayback = EditorGUILayout.Toggle(
                    new GUIContent("Late join playback", "Start clients that join after playback began at the current playback time."),
                    controller.lateJoinPlayback);
                if (networkController != null)
                {
                    EditorGUILayout.Space();
                    maxSynchronizationTimeMS = EditorGUILayout.FloatField(
                        new GUIContent("Max Synchronisation Time MS", "Delay added to distributed recording and replay commands so clients can schedule them against synchronized network time."),
                        networkController.maxSynchronizationTimeMS);
                }
                EditorGUI.indentLevel--;
            }
            else
            {
                replayRoot = controller.replayRoot;
                replayHierarchyChanges = controller.replayHierarchyChanges;
                playbackTransform = controller.playbackTransform;
                replayAudio = controller.replayAudio;
                synchronisedPlayback = controller.synchronizedPlayback;
                lateJoinPlayback = controller.lateJoinPlayback;
                if (networkController != null)
                    maxSynchronizationTimeMS = networkController.maxSynchronizationTimeMS;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // --- Server & Files: server transfer toggles and file/directory actions. Collapsed by default. ---
            _showServerFiles = EditorGUILayout.BeginFoldoutHeaderGroup(_showServerFiles, "Server & Files");
            if (_showServerFiles)
            {
                EditorGUI.indentLevel++;
                uploadFilesToServer = EditorGUILayout.Toggle(
                    new GUIContent("Upload recording files to server", "Upload the produced recording files to the configured server."),
                    controller.uploadFilesToServer);
                downloadFilesFromServer = EditorGUILayout.Toggle(
                    new GUIContent("Download recording files from server", "Download recording files from the configured server before playback."),
                    controller.downloadFilesFromServer);

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
                EditorGUI.indentLevel--;
            }
            else
            {
                uploadFilesToServer = controller.uploadFilesToServer;
                downloadFilesFromServer = controller.downloadFilesFromServer;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // --- Output & Debug: file export options and debug logging. Collapsed by default. ---
            _showOutputDebug = EditorGUILayout.BeginFoldoutHeaderGroup(_showOutputDebug, "Output & Debug");
            if (_showOutputDebug)
            {
                EditorGUI.indentLevel++;
                createWAV = EditorGUILayout.Toggle(
                    new GUIContent("Create WAV", "Export recorded audio to a .wav file alongside the recording."),
                    controller.createWAV);
                createCSV = EditorGUILayout.Toggle(
                    new GUIContent("Create CSV", "Export recorded data to a .csv file alongside the recording."),
                    controller.createCSV);
                enableDebugInfo = EditorGUILayout.Toggle(
                    new GUIContent("Print debug logs", "Print verbose recording/playback debug logs to the console."),
                    controller.debugLogs);
                EditorGUILayout.Space();
                GUILayout.Label("Native Plugin");
                recordingMaxBufferSize = EditorGUILayout.IntField(
                    new GUIContent("Recording max buffer size", "Maximum native buffer size for transform recording data."),
                    controller.pluginSettings.recordingMaxBufferSize);
                replayBufferNumber = EditorGUILayout.IntField(
                    new GUIContent("Replay buffer number", "Number of native replay buffers to keep available."),
                    controller.pluginSettings.replayBufferNumber);
                replayBufferTimeInterval = EditorGUILayout.FloatField(
                    new GUIContent("Replay buffer time interval", "Time span covered by each native replay buffer."),
                    controller.pluginSettings.replayBufferTimeInterval);
                recordingSoundMaxBufferSize = EditorGUILayout.IntField(
                    new GUIContent("Sound max buffer size", "Maximum native buffer size for recorded sound data."),
                    controller.pluginSettings.recordingSoundMaxBufferSize);
                pluginLogLevel = (LogLevel)EditorGUILayout.EnumPopup(
                    new GUIContent("Plugin log level", "Minimum native recording plugin log level forwarded to Unity."),
                    controller.pluginSettings.logLevel);
                EditorGUILayout.LabelField(
                    new GUIContent("Plugin version", "Version reported by the native recording plugin."),
                    new GUIContent(controller.pluginSettings.versionInfo));
                EditorGUI.indentLevel--;
            }
            else
            {
                createWAV = controller.createWAV;
                createCSV = controller.createCSV;
                enableDebugInfo = controller.debugLogs;
                recordingMaxBufferSize = controller.pluginSettings.recordingMaxBufferSize;
                replayBufferNumber = controller.pluginSettings.replayBufferNumber;
                replayBufferTimeInterval = controller.pluginSettings.replayBufferTimeInterval;
                recordingSoundMaxBufferSize = controller.pluginSettings.recordingSoundMaxBufferSize;
                pluginLogLevel = controller.pluginSettings.logLevel;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                controller.replayRoot = replayRoot;
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                controller.recordMicro = recordMicro;
                controller.recordAudioListener = recordAudioListener;
                controller.recordAllSoundSources = recordAllSoundSources;
                controller.attachTransformRecorderToAll = attachTransformRecorderToAll;
                controller.transformRecordingStepsPerSecond = transformRecordingStepsPerSecond;
                controller.replayHierarchyChanges = replayHierarchyChanges;
                controller.playbackTransform = playbackTransform;
                controller.replayAudio = replayAudio;
                controller.recordOnLocalTransformChangesOnly = recordOnLocalTransformChangesOnly;
                controller.createWAV = createWAV;
                controller.createCSV = createCSV;
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
                controller.pluginSettings.recordingMaxBufferSize = recordingMaxBufferSize;
                controller.pluginSettings.replayBufferNumber = replayBufferNumber;
                controller.pluginSettings.replayBufferTimeInterval = replayBufferTimeInterval;
                controller.pluginSettings.recordingSoundMaxBufferSize = recordingSoundMaxBufferSize;
                controller.pluginSettings.logLevel = pluginLogLevel;
                Undo.RecordObject(target, "Changed Values");
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                if (networkController != null)
                {
                    Undo.RecordObject(networkController, "Changed Values");
                    networkController.maxSynchronizationTimeMS = maxSynchronizationTimeMS;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(networkController);
                }
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
