using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Networking;

namespace VRSYS.Scripts.Recording
{

    [RequireComponent(typeof(RecorderState))]
    [RequireComponent(typeof(NetworkController))]
    public class TimeInteractor : MonoBehaviour
    {
        public bool inputActivated;
        
        public InputActionProperty pauseDesktop;
        public InputActionProperty rewindDesktop;
        public InputActionProperty forwardDesktop;

        public InputActionProperty timeNavigation;
        public InputActionProperty timeNavigationActive;
        public InputActionProperty pauseHMD;
        public InputActionProperty leftTriggerValue;
        
        private Vector2 _leftThumb;

        private RecorderState _state;
        private NetworkController _networkController;

        private float _lastPlaybackTimeShareUpdate;
        private float _stopToggleTime;
        
        private GameObject _localUser;
        private bool _hmd;
        
        public void Start()
        {
            if (inputActivated)
            {
                pauseDesktop.action.Enable();
                forwardDesktop.action.Enable();
                rewindDesktop.action.Enable();
                timeNavigation.action.Enable();
                timeNavigationActive.action.Enable();
                pauseHMD.action.Enable();
                leftTriggerValue.action.Enable();
            }

            _state = GetComponent<RecorderState>();
            _networkController = GetComponent<NetworkController>();
        }
        
        public void Update()
        {
            if (_localUser == null)
            {
                if (NetworkUser.LocalInstance != null)
                {
                    _localUser = NetworkUser.LocalInstance.gameObject;
                }
            }
            
            if(_state.currentState == State.Replaying)
                TimeControl();
        }

        public void ToggleGlobalPause()
        {
            _networkController.TogglePlayPauseReplayOnAllClients(!_state.replayPaused);
        }
        
        public void ToggleLocalPause()
        {
            _state.replayPaused = !_state.replayPaused;
        }
        
        private float TimeInteraction()
        {
            float timeDif = 0.0f;

            _leftThumb = timeNavigation.action.ReadValue<Vector2>() * (Time.deltaTime * 5.0f);
            float x = _leftThumb.magnitude;
            if (timeNavigationActive.action.IsPressed())
                timeDif = _leftThumb.x > 0 ? x : -x;
            
            if (forwardDesktop.action.IsPressed())
                timeDif = Time.deltaTime * 8.0f;

            if (rewindDesktop.action.IsPressed())
                timeDif = -Time.deltaTime * 8.0f;

            if ((pauseHMD.action.triggered || pauseDesktop.action.triggered) && Time.time - _stopToggleTime > 0.5)
            {
                if(NetworkManager.Singleton == null)
                    ToggleLocalPause();
                else
                    ToggleGlobalPause();
                _stopToggleTime = Time.time;
            }
            
            return timeDif;
        }

        public void NavigateToStart()
        {
            if(_state.currentState == State.Replaying)
                _state.currentReplayTime = 0.1f;
        }
        
        public void NavigateToEnd()
        {
            if(_state.currentState == State.Replaying && _state.recordingDuration >= 0.0f)
                _state.currentReplayTime = _state.recordingDuration - 0.1f;
        }

        private void SendCurrentTimesToCollaborators()
        {
            // update replay time of current user for all other user
            if (Mathf.Abs(Time.time - _lastPlaybackTimeShareUpdate) >= 0.1f)
            {
                _networkController.UpdateUserReplayTimeOnAllClientsEvent(_state.currentReplayTime);
                _lastPlaybackTimeShareUpdate = Time.time;
            }
        }
        
        private void TimeControl()
        {
            float timeDif = TimeInteraction();

            if (timeDif != 0.0f)
            {
                if (_state.currentReplayTime + timeDif < _state.recordingDuration - 0.1f &&
                    _state.currentReplayTime + timeDif >= 0.0f)
                    _state.currentReplayTime += timeDif;
            }

            // Linear time progression is driven by RecorderController; the interactor only adds the
            // user-controlled offset (timeDif) on top of it.
            if(_state.currentState == State.Replaying)
                SendCurrentTimesToCollaborators();
        }
    }
}