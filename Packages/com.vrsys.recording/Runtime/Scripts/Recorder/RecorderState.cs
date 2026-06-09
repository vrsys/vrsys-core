using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VRSYS.Recording.Scripts;

namespace VRSYS.Scripts.Recording
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
        
        public string projectName;
        public string recordingDirectory;
        [Tooltip("Name of the recording file created.")]
        public string recordingFile = "New_Recording";
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