using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Logging;

namespace VRSYS.Recording
{
    
    [RequireComponent(typeof(RecorderController))]
    public class InputController : MonoBehaviour
    {
        public bool inputEnabled = true;
        public RecorderState state;

        [SerializeField] private bool verbose = false;

        public InputActionProperty recordDesktop;
        public InputActionProperty replayDesktop;
        public InputActionProperty switchReplayFileDesktop;
        public InputActionProperty recordHMD;
        public InputActionProperty replayHMD;

        private float _interactionToggleTime;
        private float _recordingToggleTime;
        private float _replayToggleTime;

        private RecorderController _controller;
        
        public void Start()
        {
            if (inputEnabled)
            {
                recordDesktop.action.Enable();
                replayDesktop.action.Enable();
                recordHMD.action.Enable();
                replayHMD.action.Enable();
                switchReplayFileDesktop.action.Enable();
            }

            _controller = GetComponent<RecorderController>();
        }

        public void Update()
        {
            HandleInput();
        }

        
        public void ToggleLocalPlayback()
        {
            if (state.currentState == State.Idle)
            {
                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Preparing local playback.", this);
                _controller.PrepareLocalReplay();
            }
            else if (state.currentState == State.Replaying)
            {
                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Local playback end.", this);
                _controller.EndReplay();
            }
        }
        
        public void TogglePlayback()
        {
            if (state.currentState == State.Idle)
            {
                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Sending start replay/download event to all clients.", this);
                _controller.PrepareAndStartDistributedReplay();
            }
            else if (state.currentState == State.Replaying)
            {
                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Sending end replay event to all clients.", this);
                _controller.SendEndReplayEvent();
            }
        }

        public void ToggleRecording()
        {
            if (state.currentState == State.Idle)
            {
                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Sending start recording event to all clients.", this);
                _controller.SendStartRecordingEvents();
            }
            else if (state.currentState == State.Recording)
            {
                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Sending end recording event to all clients.", this);
                _controller.SendEndRecordingEvent();
            }
        }
        
        public void ToggleLocalRecording()
        {
            if (state.currentState == State.Idle)
            {
                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Local recording start.", this);
                _controller.PrepareRecording();
                _controller.StartRecording();
            }
            else if (state.currentState == State.Recording)
            {
                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Local recording end.", this);
                _controller.EndRecording();
            }
        }

        public void ToggleFileSelectionSwitch()
        {
            bool found = false;
            for (int i = 0; i < state.replayList.replayNames.Length; ++i)
            {
                if (state.replayList.replayNames[i] == state.selectedReplayFile)
                {
                    state.selectedReplayFile = state.replayList.replayNames[(i + 1) % state.replayList.replayNames.Length];
                    found = true;
                    break;
                }
            }

            if (!found && state.replayList.replayNames.Length > 0)
            {
                state.selectedReplayFile = state.replayList.replayNames[0];
            }
        }
        
        private void HandleInput()
        {
            if (switchReplayFileDesktop.action.triggered)
                ToggleFileSelectionSwitch();
            
            // start/end recording
            if (recordDesktop.action.triggered || recordHMD.action.triggered)
            {
                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Trying to start/stop recording", this);
                bool stateSwitch = false;

                if (Time.time - _recordingToggleTime > 0.5)
                {
                    _recordingToggleTime = Time.time;
                    stateSwitch = true;
                }

                if(stateSwitch)
                    ToggleRecording();
            }

            // start/end replay
            if ((replayDesktop.action.triggered || replayHMD.action.triggered))
            {
                if (verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Trying to start/stop replay", this);
                bool stateSwitch = false;
                if (Time.time - _replayToggleTime > 0.5)
                {
                    _replayToggleTime = Time.time;
                    stateSwitch = true;
                }
                
                if(stateSwitch)
                    TogglePlayback();
            }
        }
    }
}