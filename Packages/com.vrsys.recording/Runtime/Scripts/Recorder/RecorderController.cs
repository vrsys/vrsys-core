using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRSYS.Core.Avatar;
using VRSYS.Core.Networking;
using VRSYS.Recording.Scripts;
using Vrsys.Scripts.Recording;

namespace VRSYS.Scripts.Recording
{
    [RequireComponent(typeof(RecorderState))]
    [RequireComponent(typeof(PluginConfigurator))]
    [RequireComponent(typeof(NetworkController))]
    public class RecorderController : MonoBehaviour
    {
        [DllImport("RecordingPlugin")]
        private static extern bool StopRecording(int recorderId);

        [DllImport("RecordingPlugin")]
        private static extern bool StopReplay(int recorderId);

        [DllImport("RecordingPlugin")]
        private static extern bool OpenExistingRecordingFile(int recorderId, string recordingDir,
            int recordingDirNameLength, string recordingName, int recordingNameLength);

        [DllImport("RecordingPlugin")]
        private static extern bool OpenExistingRecordingFileForEditing(int recorderId, string recordingDir,
            int recordingDirNameLength, string recordingName, int recordingNameLength);

        [DllImport("RecordingPlugin")]
        private static extern float GetRecordingDuration(int recorderId);

        [DllImport("RecordingPlugin")]
        private static extern bool CreateNewRecordingFile(int recorderId, string recordingDir,
            int recordingDirNameLength, string recordingName, int recordingNameLength);

        [DllImport("RecordingPlugin")]
        private static extern bool CreateCSVFile(int recorderId, string recordingName, int recordingNameLength);

        [DllImport("RecordingPlugin")]
        private static extern bool CreateWAVFile(int recorderId, string recordingName, int recordingNameLength);

        public int transformRecordingStepsPerSecond = 20;
        public int audioRecordingStepsPerSecond = 10;

        public bool attachTransformRecorderToAll = true;
        public bool replayHierarchyChanges = true;
        public bool recordOnLocalTransformChangesOnly = true;
        public bool replayAudio = true;
        public bool recordMicro = true;
        public bool recordAudioListener = true;
        public bool recordAllSoundSources = true;

        [Tooltip("Optional anchor for playback. When set, recorded objects are matched/placed relative " +
                 "to this transform: pre-existing duplicate objects beneath it are matched to the " +
                 "recording, and objects that have to be instantiated for playback are created under it. " +
                 "Its position/rotation/scale thus offsets the whole replay. Leave empty to match/place " +
                 "objects at the scene root.")]
        public Transform replayRoot;

        public bool createWAV = false;
        public bool createCSV = false;
        public bool synchronizedPlayback = false;
        public bool lateJoinPlayback = false;
        public bool downloadFilesFromServer = false;

        private ScenePreparator _scenePreparator;
        public RecorderState recorderState;
        private PluginConfigurator _configurator;
        private NetworkController _networkController;

        private float _lastTransformRecordTime;
        private Dictionary<int, Recorder> _transformRecorder = new Dictionary<int, Recorder>();
        private Dictionary<int, Recorder> _audioRecorder = new Dictionary<int, Recorder>();
        private Dictionary<int, Recorder> _genericRecorder = new Dictionary<int, Recorder>();

        private float lastPlaybackTimeWrite;
        private string playbackStartDate;

        private float _lastReplayListRefresh;
        public bool localPlayback = false;
        public bool uploadFilesToServer = false;
        public bool debugLogs = true;

        [HideInInspector]
        public int RecorderID
        {
            get
            {
                if (recorderState != null)
                    return recorderState.recorderID;

                return -1;
            }
        }

        [HideInInspector]
        public String FixedRecordingPlaybackFile
        {
            set
            {
                if (recorderState != null)
                    recorderState.fixedPlaybackRecordingName = value;
            }
            get
            {
                if (recorderState != null)
                    return recorderState.fixedPlaybackRecordingName;

                return "Empty";
            }
        }

        [HideInInspector]
        public Dictionary<string, GameObject> RecordedObjectPresent
        {
            get { return recorderState.recordedObjectPresent; }
        }

        [HideInInspector]
        public State CurrentState
        {
            get { return recorderState.currentState; }
        }

        public void Start()
        {
            recorderState = GetComponent<RecorderState>();
            _configurator = GetComponent<PluginConfigurator>();
            _networkController = GetComponent<NetworkController>();
            _scenePreparator = GetComponent<ScenePreparator>();
            
            if (recorderState.recordingDirectory == "")
                recorderState.recordingDirectory = Application.persistentDataPath + "/";

            // this is done to avoid potential interferences by corrupted states of the recording plugin
            bool resultStopRec = StopRecording(recorderState.recorderID);
            bool resultStopRep = StopReplay(recorderState.recorderID);
        }

        private readonly List<IGenericRecorderProvider> _genericRecorderProviders = new List<IGenericRecorderProvider>();

        /// <summary>
        /// Register an external provider (e.g. the optional Meta Avatar integration) that attaches
        /// additional <see cref="GenericRecorder"/>s when a recording or replay is prepared. This is
        /// the decoupled replacement for the previously hard-coded Meta avatar recorder attachment.
        /// </summary>
        public void RegisterGenericRecorderProvider(IGenericRecorderProvider provider)
        {
            if (provider != null && !_genericRecorderProviders.Contains(provider))
                _genericRecorderProviders.Add(provider);
        }

        public void UnregisterGenericRecorderProvider(IGenericRecorderProvider provider)
        {
            _genericRecorderProviders.Remove(provider);
        }

        private void AttachArbitraryRecorder()
        {
            foreach (IGenericRecorderProvider provider in _genericRecorderProviders)
            {
                if (provider != null)
                    provider.AttachGenericRecorders(this);
            }
        }

        public int GetNextAvailableSoundID()
        {
            for (int i = 0; i < 1000; ++i)
            {
                if(!_audioRecorder.ContainsKey(i))
                    return i;
            }

            return -1;
        }
        
        private unsafe void AttachSoundRecorder()
        {
            if (recorderState.currentState == State.Recording || recorderState.currentState == State.PrepareRecording)
            {
                if (Microphone.devices.Length > 0)
                {
                    
                    if (recordMicro)
                    {
                        // Use the default Unity microphone device. Applications that capture audio through a
                        // specific voice SDK can inject their own clip via MicrophoneRecorder.SetMicrophoneReader.
                        string microphone = Microphone.devices[0];
                        Debug.Log("Default microphone: " + microphone);

                        GameObject newGo = new GameObject();
                        newGo.name = "SoundSource:0";
                        newGo.transform.parent = transform;
                        MicrophoneRecorder microphoneRecorder = newGo.AddComponent<MicrophoneRecorder>();
                        microphoneRecorder.SetId(0);
                        microphoneRecorder.Controller = this;

                        // Only start a fresh Unity capture when nothing is already recording from this
                        // device. When a voice SDK such as ODIN already holds the microphone, calling
                        // Microphone.Start again would disrupt its active capture, so we skip it and leave
                        // the reader unset for the SDK-specific override (MicrophoneRecorder.SetMicrophoneReader).
                        if (!Microphone.IsRecording(microphone))
                        {
                            AudioClip microphoneClip = Microphone.Start(microphone, true, 10, AudioSettings.outputSampleRate);
                            microphoneRecorder.SetMicrophoneReader(new MicrophoneClipReader(microphoneClip, microphone));
                        }
                        else
                        {
                            Debug.Log("Microphone '" + microphone + "' is already recording (e.g. in use by a voice " +
                                      "SDK like ODIN); skipping Microphone.Start and leaving the reader for an override.");
                        }

                        if (NetworkUser.LocalInstance != null)
                            microphoneRecorder.SetUserTransform(NetworkUser.LocalInstance.head);
                    }
                    // debug
                    // AudioSource testSource = newGo.AddComponent<AudioSource>();
                    // testSource.clip = microphoneClip;
                    // testSource.loop = true;
                    // testSource.Play();
                }

                if (recordAudioListener)
                {
                    var listener = FindAnyObjectByType<AudioListener>();
                    if (listener != null)
                    {
                        GameObject listenerGameObject = listener.gameObject;
                        GameObject newGo2 = new GameObject();
                        newGo2.name = "SoundSource:1";
                        newGo2.transform.parent = transform;
                        AudioListenerRecorder audioListenerRecorder = null;
                        if (recorderState.currentState == State.PreparingReplay ||
                            recorderState.currentState == State.Replaying)
                            audioListenerRecorder = newGo2.AddComponent<AudioListenerRecorder>();
                        else
                            audioListenerRecorder = listenerGameObject.AddComponent<AudioListenerRecorder>();
                        audioListenerRecorder.SetId(1);
                        audioListenerRecorder.Controller = this;
                    }
                }
                
                if (recordAllSoundSources)
                {
                    AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    for (int i = 0; i < sources.Length; ++i)
                    {
                        AudioSourceRecorder recorder = sources[i].gameObject.AddComponent<AudioSourceRecorder>();
                        recorder.SetId(2 + i);
                        recorder.Controller = this;
                    }
                }
            }
        }

        
        private void AttachTransformRecorder()
        {
            Debug.Log("Attaching transform recorder scripts to gameobjects in the scene");

            // During replay with a configured replay root, only objects beneath that root are animated.
            // Attaching recorders to the replay-root subtree only prevents objects that share a recorded
            // hierarchy name but live outside the anchor (e.g. an original "/Cube" next to the anchored
            // duplicate "/Anchor/Cube") from being played back as well.
            if (recorderState.currentState == State.PreparingReplay && replayRoot != null)
            {
                AttachTransformRecorderRecursively(replayRoot.gameObject);
                Debug.Log("Attaching transform recorder scripts to gameobjects in the scene finished");
                return;
            }

            if (attachTransformRecorderToAll)
            {
                GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

                foreach (var rootObject in rootObjects)
                {
                    bool isRecordingSetup = rootObject.name.Contains("===RECORDING===");
                    //bool isScene = rootObject.name.Contains("__SCENE__");
                    bool isUi = rootObject.name.Contains("__UI__");
                    if (!isRecordingSetup /*&& !isScene*/ && !isUi)
                        AttachTransformRecorderRecursively(rootObject);
                }

                if (DontDestroySceneAccessor.Instance != null)
                {
                    rootObjects = DontDestroySceneAccessor.Instance.GetAllRootsOfDontDestroyOnLoad();

                    foreach (var rootObject in rootObjects)
                    {
                        bool isRecordingSetup = rootObject.name.Contains("===RECORDING===");
                        //bool isScene = rootObject.name.Contains("__SCENE__");
                        bool isUi = rootObject.name.Contains("__UI__");
                        if (!isRecordingSetup /*&& !isScene*/ && !isUi)
                            AttachTransformRecorderRecursively(rootObject);
                    }
                }
            }

            Debug.Log("Attaching transform recorder scripts to gameobjects in the scene finished");
        }

        public void AttachTransformRecorderRecursively(GameObject root)
        {
            if (recorderState.currentState == State.PreparingReplay)
            {
                if (root.GetComponent<NetworkUser>() != null)
                    return;
                if (root.GetComponent<XROrigin>() != null)
                    return;
            }

            if (root.CompareTag("DoNotPlayback"))
                return;


            foreach (Transform childTransform in root.transform)
            {
                AttachTransformRecorderRecursively(childTransform.gameObject);
            }


            TransformRecorder[] transformRecorders = root.GetComponents<TransformRecorder>();
            bool found = false;
            bool alreadyUsedForPlayback = false;
            foreach (var transformRecorder in transformRecorders)
            {
                if (transformRecorder.controller.RecorderID == RecorderID)
                    found = true;
                if (transformRecorder.controller.recorderState.currentState == State.Replaying ||
                    transformRecorder.controller.recorderState.currentState == State.PreparingReplay)
                    alreadyUsedForPlayback = true;
            }

            bool attach = !found && ((recorderState.currentState == State.PreparingReplay && !alreadyUsedForPlayback) ||
                                     (recorderState.currentState == State.PrepareRecording ||
                                      recorderState.currentState == State.Recording));
            if (attach)
            {
                TransformRecorder recorder = root.AddComponent<TransformRecorder>();
                recorder.controller = this;
                recorder.SetId(root.GetInstanceID());
                recorder.RegisterRecorder();
            }
        }

        private void CleanDestroy()
        {
            if (recorderState.currentState == State.Recording || recorderState.currentState == State.PrepareRecording)
            {
                EndRecording();
            }

            if (recorderState.currentState == State.Replaying ||
                recorderState.currentState == State.PreparingReplay)
            {
                bool result = StopReplay(recorderState.recorderID);
                OnReplayEnd();
                if (!result)
                    Debug.LogError("Could not stop the replay!");
                else 
                    recorderState.currentState = State.Idle;
            }
        }

        private void OnDestroy()
        {
            CleanDestroy();
        }

        private void OnDisable()
        {
            //throw new NotImplementedException();
        }

        public void OnApplicationQuit()
        {
            CleanDestroy();
        }

        public void PrepareAndStartDistributedReplay()
        {
            if (recorderState.currentState == State.Idle)
            {
                _networkController.StartDownloadOnAllClientsEvent();
            }
        }

        public void StartLocalRecording()
        {
            recorderState.localRecording = true;
            PrepareRecording();
            StartRecording();
        }

        public void PrepareAndStartLocalReplay()
        {
            if (recorderState.currentState == State.Idle)
            {
                PrepareLocalReplay();
            }
        }

        public void PrepareLocalReplay()
        {
            Debug.Log("Preparing local replay.");
            recorderState.currentState = State.PreparingReplay;
            localPlayback = true;
            if (downloadFilesFromServer)
                _networkController.StartDownloads();
        }

        public void StartReplay()
        {
            recorderState.currentState = State.PreparingReplay;
            Debug.Log("Starting replay for recorder with id: " + recorderState.recorderID);

            bool openForEditing = GetComponent<IRecordingEditor>() != null;
            bool result = openForEditing
                ? OpenExistingRecordingFileForEditing(recorderState.recorderID, recorderState.recordingDirectory,
                    recorderState.recordingDirectory.Length, recorderState.selectedReplayFile,
                    recorderState.selectedReplayFile.Length)
                : OpenExistingRecordingFile(recorderState.recorderID, recorderState.recordingDirectory,
                    recorderState.recordingDirectory.Length, recorderState.selectedReplayFile,
                    recorderState.selectedReplayFile.Length);

            if (!result)
            {
                Debug.LogError("Playback file existence check: Failed " + Time.time);
                return;
            }
            else
            {
                Debug.Log("Playback file existence check: Successful " + Time.time);
            }

            recorderState.recordingDuration = GetRecordingDuration(recorderState.recorderID);
            recorderState.currentMinSliderValue = 0.0f;
            recorderState.currentMaxSliderValue = recorderState.recordingDuration;

            DisableAllAudioSources();
            
            Debug.Log("Preparing scene for playback.");
            _scenePreparator.PrepareReplayScene();

            AttachTransformRecorder();
            // Audio sources are already handeled by the scene preaparator prepare replay scene
            AttachArbitraryRecorder();

            playbackStartDate = DateTime.Now.ToString("g", CultureInfo.GetCultureInfo("es-ES")).Replace(" ", "_")
                .Replace(":", "_").Replace("/", "_");

            recorderState.recordedObjectPresent = _scenePreparator.GetNamePresent();
            recorderState.currentState = State.Replaying;
            recorderState.currentReplayTime = 0.0f;
            recorderState.currentRecordingTime = -1.0f;
            
            OnReplayStart();
        }

        public void SendEndReplayEvent()
        {
            _networkController.EndReplayOnAllClientsEvent();
        }

        public void EndReplay()
        {
            recorderState.currentState = State.Idle;
            Debug.Log("Stopping replay for recorder with id: " + recorderState.recorderID);
            bool result = StopReplay(recorderState.recorderID);

            if (result)
                Debug.Log("Playback stopped successful! " + Time.time);
            else
                Debug.LogError("Playback not stopped successful! " + Time.time);

            recorderState.ResetAfterReplay();
            OnReplayEnd();
            localPlayback = false;
            DestroyRecorder();
            _scenePreparator.CleanReplayScene();
            EnableAllAudioSources();
        }

        public void SendStartRecordingEvents()
        {
            _networkController.PrepareRecordingOnAllClientsEvent();

            _networkController.StartRecordingOnAllClientsEvent();
        }

        public void StartRecording()
        {
            recorderState.currentState = State.Recording;
            recorderState.currentReplayTime = -1.0f;
            recorderState.currentRecordingTime = 0.0f;
            
            OnRecordingStart();
            Debug.Log("Starting recording for recorder with id: " + recorderState.recorderID);
        }

        public void PrepareRecording()
        {
            Debug.Log("Preparing recording for recorder with id: " + recorderState.recorderID);
            recorderState.currentState = State.PrepareRecording;

            AttachTransformRecorder();
            AttachSoundRecorder();
            AttachArbitraryRecorder();

            bool result = CreateNewRecordingFile(recorderState.recorderID, recorderState.recordingDirectory,
                recorderState.recordingDirectory.Length, recorderState.recordingFile,
                recorderState.recordingFile.Length);
            if (!result)
                Debug.LogError("Recording file creation: Failed for recorder with id: " + recorderState.recorderID);
            else
                Debug.Log("Recording file creation: Successful for recorder with id: " + recorderState.recorderID);
        }

        public void SendEndRecordingEvent()
        {
            _networkController.EndRecordingOnAllClientsEvent();
        }

        public void EndRecording()
        {
            recorderState.currentState = State.Idle;
            recorderState.localRecording = false;
            Debug.Log("Stopping recording for recorder with id: " + recorderState.recorderID);

            bool result = StopRecording(recorderState.recorderID);

            if (!result)
            {
                Debug.LogError("Recording stopped: Failed");
                return;
            }

            Debug.Log("Recording stopped: Successful");

            if (uploadFilesToServer)
                UploadFilesToServer();

            if (createWAV)
            {
                Debug.Log("Started WAV file creation coroutine.");
                Invoke(nameof(WAVCreationCoroutine), 5.0f);
            }

            if (createCSV)
            {
                Debug.Log("Started CSV file creation coroutine.");
                Invoke(nameof(CSVCreationCoroutine), 5.0f);
            }

            OnRecordingEnd();
            
            _networkController.UpdateReplayList();
            
            DestroyRecorder();
        }

        private void UploadFilesToServer()
        {
            string transformFile = recorderState.recordingDirectory + "/" + recorderState.recordingFile + ".transform";
            string soundFile = recorderState.recordingDirectory + "/" + recorderState.recordingFile + ".sound";
            string arbFile = recorderState.recordingDirectory + "/" + recorderState.recordingFile + ".generic";
            string metaFile = recorderState.recordingDirectory + "/" + recorderState.recordingFile + ".recordmeta";
            string date = DateTime.Now.ToString("g", CultureInfo.GetCultureInfo("es-ES")).Replace(" ", "_")
                .Replace(":", "_").Replace("/", "_");

            string fileName = "placeholder";
            if (recorderState.recordingFile != "")
                fileName = recorderState.recordingFile + "_" + date;
            else
                fileName = date;

            Debug.Log("Trying to transmit transform file: " + transformFile);
            if (File.Exists(transformFile))
            {
                _networkController.Upload(recorderState.projectName, transformFile, fileName + ".transform",
                    recorderState.selectedServer);
            }

            Debug.Log("Trying to transmit sound file: " + soundFile);
            if (File.Exists(soundFile))
            {
                _networkController.Upload(recorderState.projectName, soundFile, fileName + ".sound",
                    recorderState.selectedServer);
            }

            Debug.Log("Trying to transmit meta file: " + metaFile);
            if (File.Exists(metaFile))
            {
                _networkController.Upload(recorderState.projectName, metaFile, fileName + ".recordmeta",
                    recorderState.selectedServer);
            }

            Debug.Log("Trying to transmit arbitrary file: " + arbFile);
            if (File.Exists(arbFile))
            {
                _networkController.Upload(recorderState.projectName, arbFile, fileName + ".generic",
                    recorderState.selectedServer);
            }

            Debug.Log("Finished transmitting all files.");
        }

        public void WAVCreationCoroutine()
        {
            StartCoroutine(CreateAndUploadWAVFiles());
        }

        public void CSVCreationCoroutine()
        {
            StartCoroutine(CreateCSVFile());
        }

        public void RegisterRecorder(int id, Recorder recorder)
        {
            if (recorder is TransformRecorder && !_transformRecorder.ContainsKey(id))
                _transformRecorder.Add(id, recorder);
            if (recorder is AudioRecorder && !_audioRecorder.ContainsKey(id))
                _audioRecorder.Add(id, recorder);
            if (recorder is GenericRecorder && !_genericRecorder.ContainsKey(id))
                _genericRecorder.Add(id, recorder);
        }

        public void DeregisterRecorder(int id, Recorder recorder)
        {
            if (recorder is TransformRecorder && _transformRecorder.ContainsKey(id))
                _transformRecorder.Remove(id);
            if (recorder is AudioRecorder && _audioRecorder.ContainsKey(id))
                _audioRecorder.Remove(id);
            if (recorder is GenericRecorder && _genericRecorder.ContainsKey(id))
                _genericRecorder.Remove(id);
        }

        public Dictionary<int, Recorder> GetAudioRecorder()
        {
            return _audioRecorder;
        }

        public Dictionary<int, Recorder> GetTransformRecorders()
        {
            return _transformRecorder;
        }

        public Dictionary<int, Recorder> GetGenericRecorders()
        {
            return _genericRecorder;
        }

        public Recorder GetTransformRecorder(int id)
        {
            if (_transformRecorder.ContainsKey(id))
                return _transformRecorder[id];
            return null;
        }

        public IEnumerator SignalObjectInstantiation(GameObject go)
        {
            if (recorderState.currentState == State.Recording)
            {
                // This yield is done, as spawned network objects might be renamed briefly after spawn
                // Because of this the recorders are first attached after a small wait
                yield return new WaitForSeconds(0.3f);
                AttachTransformRecorderRecursively(go);
            }
        }

        private void DestroyRecorder()
        {
            foreach (var kv in _transformRecorder)
                if (kv.Value != null)
                    Destroy(kv.Value);

            foreach (var kv in _audioRecorder)
                if (kv.Value != null)
                    Destroy(kv.Value);

            foreach (var kv in _genericRecorder)
                if (kv.Value != null)
                   Destroy(kv.Value);

            _transformRecorder.Clear();
            _audioRecorder.Clear();
            _genericRecorder.Clear();
        }

        public void FixedUpdate()
        {
            if (recorderState.currentState == State.PreparingReplay)
            {
                if (!localPlayback)
                {
                    if (downloadFilesFromServer)
                        _networkController.UpdateDownloadStatusEvent();
                }
                else
                {
                    if ((downloadFilesFromServer && !_networkController.IsDownloading()) || !downloadFilesFromServer)
                        StartReplay();
                }
            }
        }

        private void SetFixedPlaybackRecordingFileIfSet()
        {
            if (recorderState.fixedPlaybackRecordingName != null && recorderState.fixedPlaybackRecordingName.Length > 0 &&
                recorderState.selectedReplayFile != recorderState.fixedPlaybackRecordingName)
            {
                recorderState.selectedReplayFile = recorderState.fixedPlaybackRecordingName;
            }
        }

        private void LateJoinLocalPlaybackStart()
        {
            Debug.Log("Trying to late join recording playback.");

            // make sure playback is started for late joining users
            if (recorderState.currentState == State.Idle && _networkController._userReplayTimes.Count > 0 &&
                !_networkController._userReplayTimes.ContainsKey(NetworkUser.LocalInstance.name))
                PrepareLocalReplay();

            if (recorderState.currentState == State.PreparingReplay && !localPlayback &&
                !_networkController.IsDownloading())
            {
                Debug.Log("Late join local playback start. Downloads should be finished.");
                StartReplay();
            }
        }

        private void OnRecordingStart()
        {
            foreach (var kv in _transformRecorder)
            {
                if (kv.Value != null)
                {
                    kv.Value.OnRecordingStart();
                }
            }

            foreach (var kv in _audioRecorder)
            {
                if (kv.Value != null)
                {
                    kv.Value.OnRecordingStart();
                }
            }

            foreach (var kv in _genericRecorder)
            {
                if (kv.Value != null)
                {
                    kv.Value.OnRecordingStart();
                }
            }
        }
        
        private void OnRecordingEnd()
        {
            Debug.Log("OnRecordingEnd called!");
            foreach (var kv in _transformRecorder)
            {
                if (kv.Value != null)
                {
                    kv.Value.OnRecordingEnd();
                }
            }

            foreach (var kv in _audioRecorder)
            {
                if (kv.Value != null)
                {
                    kv.Value.OnRecordingEnd();
                }
            }

            foreach (var kv in _genericRecorder)
            {
                if (kv.Value != null)
                {
                    kv.Value.OnRecordingEnd();
                }
            }
            
            Debug.Log("OnRecordingEnd finished!");
        }
        
        private void OnReplayStart()
        {
            Debug.Log("OnReplayStart called!");
            foreach (var kv in _transformRecorder)
            {
                if (kv.Value != null)
                {
                    kv.Value.OnReplayStart();
                }
            }

            foreach (var kv in _audioRecorder)
            {
                if (kv.Value != null)
                {
                    kv.Value.OnReplayStart();
                }
            }

            foreach (var kv in _genericRecorder)
            {
                if (kv.Value != null)
                {
                    kv.Value.OnReplayStart();
                }
            }
            Debug.Log("OnReplayStart finished!");
        }
        
        private void OnReplayEnd()
        {
            foreach (var kv in _transformRecorder)
            {
                if (kv.Value != null)
                {
                    kv.Value.OnReplayEnd();
                }
            }

            foreach (var kv in _audioRecorder)
            {
                if (kv.Value != null)
                {
                    kv.Value.OnReplayEnd();
                }
            }

            foreach (var kv in _genericRecorder)
            {
                if (kv.Value != null)
                {
                    kv.Value.OnReplayEnd();
                }
            }
        }

        private void SynchronizePlayback()
        {
            // make sure all users are close in time to each other in the playback
            // Note: this means that individual temporal navigation is not possible
            if (recorderState.currentState == State.Replaying && synchronizedPlayback)
            {
                float synchronisationMaxDeviation = 0.4f;
                // Synchronize every client to the host's (Netcode server's) replay time, broadcast via NetworkController.
                float serverPlaybackTime = _networkController.HostReplayTime;
                if (NetworkUser.LocalInstance == null)
                    return;
                
                if (Mathf.Abs(serverPlaybackTime - recorderState.currentReplayTime) > synchronisationMaxDeviation)
                {
                    float timeAdvance = 3.0f;
                    int it = 1;
                    int i = 0;
                    while (Mathf.Abs(serverPlaybackTime - recorderState.currentReplayTime) > timeAdvance & i < it)
                    {
                        i++;
                        Debug.Log("Current playback time: " + recorderState.currentReplayTime + ", target time: " + serverPlaybackTime);

                        if (recorderState.currentReplayTime < 1.0f)
                            recorderState.currentReplayTime = 1.0f;

                        foreach (var kv in _transformRecorder)
                        {
                            if (kv.Value != null)
                            {
                                bool playback = kv.Value.Replay(recorderState.currentReplayTime);

                                Debug.Log("Playback state: " + playback);

                                if (recorderState.currentReplayTime + timeAdvance <
                                    recorderState.recordingDuration &&
                                    recorderState.currentReplayTime + timeAdvance < serverPlaybackTime)
                                {
                                    recorderState.currentReplayTime += timeAdvance;
                                }
                                else if (recorderState.currentReplayTime + timeAdvance >
                                         recorderState.recordingDuration &&
                                         serverPlaybackTime < recorderState.currentReplayTime)
                                {
                                    recorderState.currentReplayTime = 0.0f;
                                }

                                break;
                            }
                        }

                        foreach (var kv in _audioRecorder)
                        {
                            if (kv.Value != null && !kv.Value.Replay(recorderState.currentReplayTime))
                            {
                                break;
                            }
                        }

                        foreach (var kv in _genericRecorder)
                        {
                            if (kv.Value != null && !kv.Value.Replay(recorderState.currentReplayTime))
                            {
                                break;
                            }
                        }
                    }

                    Debug.Log("Adjusting playback time from: " + recorderState.currentReplayTime + " to: " +
                              serverPlaybackTime);
                    recorderState.currentReplayTime = serverPlaybackTime;
                }
            }
        }

        public void Update()
        {
            SetFixedPlaybackRecordingFileIfSet();

            if (recorderState.currentState != State.Recording && lateJoinPlayback)
                LateJoinLocalPlaybackStart();

            if (recorderState.currentState == State.Replaying && synchronizedPlayback)
            {
                // The host publishes its replay time; every client (incl. the host) syncs to it.
                _networkController.PublishHostReplayTime(recorderState.currentReplayTime);
                SynchronizePlayback();
            }
        }

        public void LateUpdate()
        {
            if (recorderState.currentState == State.Idle)
                Idle();

            if (recorderState.currentState == State.Recording)
                Recording();

            if (recorderState.currentState == State.Replaying &&
                recorderState.currentReplayTime < recorderState.recordingDuration)
                Replay();
        }

        private void Idle()
        {
            if (Time.time - _lastReplayListRefresh >= 2.0f)
            {
                _networkController.UpdateReplayList();
                _lastReplayListRefresh = Time.time;
            }
        }

        private void Recording()
        {
            if (Time.time - _lastTransformRecordTime > 1.0f / (float)transformRecordingStepsPerSecond)
            {
                try
                {
                    foreach (var kv in _transformRecorder)
                    {
                        if (kv.Value != null)
                        {
                            if (!kv.Value.Record(recorderState.currentRecordingTime))
                            {
                                EndRecording();
                                break;
                            }
                        }
                    }
                } catch (Exception e)
                {
                    Debug.LogError("Error while recording transforms: " + e);
                }

                try
                {
                    foreach (var kv in _audioRecorder)
                    {
                        if (kv.Value != null)
                        {
                            if (!kv.Value.Record(recorderState.currentRecordingTime))
                            {
                                EndRecording();
                                break;
                            }
                        }
                    }
                } catch (Exception e)
                {
                    Debug.LogError("Error while recording audio: " + e);
                }

                try
                {
                    foreach (var kv in _genericRecorder)
                    {
                        if (kv.Value != null)
                        {
                            if (!kv.Value.Record(recorderState.currentRecordingTime))
                            {
                                EndRecording();
                                break;
                            }
                        }
                    }
                } catch (Exception e)
                {
                    Debug.LogError("Error while recording arbitrary data: " + e);
                }

                _lastTransformRecordTime = Time.time;
            }

            recorderState.currentRecordingTime += Time.deltaTime;
        }

        private void Replay()
        {
            foreach (var kv in _transformRecorder)
            {
                if (kv.Value != null && !kv.Value.Replay(recorderState.currentReplayTime))
                {
                    //Debug.Log("Could not replay transform for object: " + kv.Value.gameObject.name);
                    //EndReplay();
                    //break;
                }
            }


            foreach (var kv in _audioRecorder)
            {
                if (kv.Value != null && !kv.Value.Replay(recorderState.currentReplayTime))
                {
                    //EndReplay();
                    //break;
                }
            }

            foreach (var kv in _genericRecorder)
            {
                if (kv.Value != null && !kv.Value.Replay(recorderState.currentReplayTime))
                {
                    //EndReplay();
                    //break;
                }
            }

            if (recorderState.currentReplayTime > recorderState.recordingDuration)
                EndReplay();
        }

        private void DisableAllAudioSources()
        {
            AudioSource[] audioSources = (AudioSource[])FindObjectsOfType(typeof(AudioSource));

            foreach (AudioSource audioSource in audioSources)
            {
                if (audioSource.gameObject.name != "UserSpeaker" &&
                    !audioSource.gameObject.name.Contains("Speaker for Player"))
                {
                    audioSource.Pause();
                }
            }
        }

        private void EnableAllAudioSources()
        {
            AudioSource[] audioSources = (AudioSource[])FindObjectsOfType(typeof(AudioSource));

            foreach (AudioSource audioSource in audioSources)
            {
                audioSource.UnPause();
            }
        }

        IEnumerator CreateAndUploadWAVFiles()
        {
            bool finished = CreateWAVFile(recorderState.recorderID, recorderState.recordingFile,
                recorderState.recordingFile.Length);

            if (finished)
            {
                string date = DateTime.Now.ToString("g", CultureInfo.GetCultureInfo("es-ES")).Replace(" ", "_")
                    .Replace(":", "_").Replace("/", "_");

                for (int i = 0; i < 2; i++)
                {
                    string audioFile = recorderState.recordingDirectory + "/" + recorderState.recordingFile + "_" + i +
                                       ".wav";
                    Debug.Log("Trying to transmit audio file: " + audioFile);
                    if (System.IO.File.Exists(audioFile))
                    {
                        _networkController.Upload(recorderState.projectName, audioFile,
                            recorderState.recordingFile + "_" + date + "_" + i + ".wav",
                            recorderState.selectedServer);
                    }
                }
            }
            else
            {
                Debug.LogError("Could not create WAV files!");
            }

            yield return null;
        }

        IEnumerator CreateCSVFile()
        {
            bool finished = CreateCSVFile(recorderState.recorderID, recorderState.recordingFile,
                recorderState.recordingFile.Length);

            if (!finished)
                Debug.LogError("Could not create CSV file!");
            yield return null;
        }

        public void AddOriginalIdGameobject(int originalId, int newId, GameObject go)
        {
            if (!recorderState.originalIdGameObjects.ContainsKey(originalId))
            {
                recorderState.originalIdGameObjects.Add(originalId, go);
                if (!recorderState.newIdOriginalId.ContainsKey(newId))
                    recorderState.newIdOriginalId.Add(newId, originalId);
            }
        }

        public float GetRecordingDuration()
        {
            return recorderState.recordingDuration;
        }
    }
}
