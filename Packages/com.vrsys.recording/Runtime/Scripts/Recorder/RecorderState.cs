using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace VRSYS.Recording
{
    public enum State
    {
        Idle, Recording, Replaying, PreparingReplay, PrepareRecording
    }
    
    
    [Serializable]
    public class ReplayList
    {
        public string[] replayNames = null;
    }

    public enum FileNameDateSuffix
    {
        None,
        Date,
        DateTime
    }
    
    [RequireComponent(typeof(RecorderController))]
    [RequireComponent(typeof(NetworkController))]
    [RequireComponent(typeof(TimeInteractor))]
    public class RecorderState : MonoBehaviour
    {
        [Tooltip("ID of the recorder.")]
        public int recorderID;

        private State _previousState = State.Idle;
        public State currentState = State.Idle;

        [Tooltip("True while a local (non-distributed) recording is in progress. Lives here next to " +
                 "currentState so the two cannot desync.")]
        public bool localRecording = false;

        public UnityEvent OnStateChanged = new UnityEvent();
        
        [Tooltip("Project the recordings belong to. Recordings are created in, replayed from and downloaded to a " +
                 "subfolder with this name in the recording directory, and uploaded to this project on the server. " +
                 "Leave empty to use the recording directory itself.")]
        public string projectName;
        [Tooltip("Folder for recordings (the persistent data path if empty). Projects are subfolders of it.")]
        public string recordingDirectory;
        [Tooltip("Name of the recording file created.")]
        public string recordingFile = "New_Recording";
        [FormerlySerializedAs("fileNameDateExtension")] [Tooltip("None: FileName, Date: FileName + Date, DateTime: FileName + Date + Timestamp")]
        public FileNameDateSuffix fileNameDateSuffix = FileNameDateSuffix.Date;
        [Tooltip("List of all servers that can be used to upload and download recording files.")]
        public List<String> serverList;
        [Tooltip("Name of the recording that should be used for playback")]
        public string fixedPlaybackRecordingName = "";
        
        private NetworkController networkController;
        public RecorderController recorderController;
        private TimeInteractor timeInteractor;
        
        public float currentRecordingTime = -1.0f;
        public float currentReplayTime = -1.0f;
        public float recordingDuration = -1.0f;
        public float currentMinSliderValue = -1.0f;
        public float currentMaxSliderValue = -1.0f;
        public Dictionary<string, GameObject> recordedObjectPresent;
        public Dictionary<int, GameObject> originalIdGameObjects;
        public Dictionary<int, int> newIdOriginalId;
        public string selectedServer;
        public string selectedReplayFile;
        public ReplayList replayList;
        public bool replayPaused = false;
        [Tooltip("If enabled, the playback list is populated from recordings in the local recording " +
                 "directory and the server is not queried for the replay list.")]
        public bool useLocalReplayFiles = true;

        /// <summary>
        /// Folder of the current project's recordings: the recording directory (the persistent data path if none is
        /// set), with the project name as a subfolder if one is set. Ends with '/', as the native plugin expects.
        /// </summary>
        public string ProjectDirectory => GetProjectDirectory(recordingDirectory, projectName);

        public static string GetProjectDirectory(string baseDirectory, string project)
        {
            string directory = (string.IsNullOrEmpty(baseDirectory) ? Application.persistentDataPath : baseDirectory)
                .TrimEnd('/', '\\');
            if (!string.IsNullOrEmpty(project))
                directory += "/" + project;
            return directory + "/";
        }

        /// <summary>
        /// A project name that works as a folder name and in server URLs: ASCII letters, digits, '-', '_' and '.' are
        /// kept, every other character becomes '_' (the recording server accepts the same characters).
        /// </summary>
        public static string SanitizeProjectName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";
            char[] chars = name.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool allowed = c < 128 && (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.');
                if (!allowed)
                    chars[i] = '_';
            }
            return new string(chars).Trim('.', '_');
        }

        public void Start()
        {
            networkController = GetComponent<NetworkController>();
            recorderController = GetComponent<RecorderController>();
            timeInteractor = GetComponent<TimeInteractor>();

            originalIdGameObjects = new Dictionary<int, GameObject>();
            newIdOriginalId = new Dictionary<int, int>();
            
            if(RecorderStateManager.Instance != null)
                RecorderStateManager.Instance.RegisterRecorderState(this);
        }

        private void Update()
        {
            if (_previousState != currentState)
            {
                _previousState = currentState;
                OnStateChanged.Invoke();
            }
        }

        public void ResetAfterReplay()
        {
            newIdOriginalId.Clear();
            recordedObjectPresent.Clear();
            originalIdGameObjects.Clear();
        }

        public int ResolveOriginalId(GameObject go)
        {
            if (go == null)
                return 0;
            int instanceId = go.GetInstanceID();
            if (newIdOriginalId != null && newIdOriginalId.ContainsKey(instanceId))
                return newIdOriginalId[instanceId];
            return instanceId;
        }

        public void OnDestroy()
        {
            if(RecorderStateManager.Instance != null)
                RecorderStateManager.Instance.DeRegisterRecorderState(this);
        }
    }
}