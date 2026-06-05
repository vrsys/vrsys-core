using UnityEngine;
using UnityEngine.InputSystem;

namespace VRSYS.Scripts.Recording
{
    
    [RequireComponent(typeof(RecorderController))]
    public class InputController : MonoBehaviour
    {
        public bool inputEnabled = true;
        public RecorderState state;

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
                Debug.Log("Preparing local playback.");
                _controller.PrepareLocalReplay();
            }
            else if (state.currentState == State.Replaying)
            {
                Debug.Log("Local playback end.");
                _controller.EndReplay();
            }
        }
        
        public void TogglePlayback()
        {
            if (state.currentState == State.Idle)
            {
                Debug.Log("Sending start replay/download event to all clients.");
                _controller.PrepareAndStartDistributedReplay();
            }
            else if (state.currentState == State.Replaying)
            {
                Debug.Log("Sending end replay event to all clients.");
                _controller.SendEndReplayEvent();
            }
        }

        public void ToggleRecording()
        {
            if (state.currentState == State.Idle)
            {
                Debug.Log("Sending start recording event to all clients.");
                _controller.SendStartRecordingEvents();
            }
            else if (state.currentState == State.Recording)
            {
                Debug.Log("Sending end recording event to all clients.");
                _controller.SendEndRecordingEvent();
            }
        }
        
        public void ToggleLocalRecording()
        {
            if (state.currentState == State.Idle)
            {
                Debug.Log("Local recording start.");
                _controller.PrepareRecording();
                _controller.StartRecording();
            }
            else if (state.currentState == State.Recording)
            {
                Debug.Log("Local recording end.");
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
                Debug.LogWarning("Trying to start/stop recording");
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
                Debug.LogWarning("Trying to start/stop replay");
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