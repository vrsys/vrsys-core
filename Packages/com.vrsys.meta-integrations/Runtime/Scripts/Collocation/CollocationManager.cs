// VRSYS plugin of Virtual Reality and Visualization Group (Bauhaus-University Weimar)
//  _    ______  _______  _______
// | |  / / __ \/ ___/\ \/ / ___/
// | | / / /_/ /\__ \  \  /\__ \ 
// | |/ / _, _/___/ /  / /___/ / 
// |___/_/ |_|/____/  /_//____/  
//
//  __                            __                       __   __   __    ___ .  . ___
// |__)  /\  |  | |__|  /\  |  | /__`    |  | |\ | | \  / |__  |__) /__` |  |   /\   |  
// |__) /~~\ \__/ |  | /~~\ \__/ .__/    \__/ | \| |  \/  |___ |  \ .__/ |  |  /~~\  |  
//
//       ___               __                                                           
// |  | |__  |  |\/|  /\  |__)                                                          
// |/\| |___ |  |  | /~~\ |  \                                                                                                                                                                                     
//
// Copyright (c) 2023 Virtual Reality and Visualization Group
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//-----------------------------------------------------------------
//   Authors:        Tony Zoeppig, Karoline Brehm
//   Date:           2025
//-----------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VRSYS.Core.Logging;
using VRSYS.Core.Networking;


// TODO: Check that asset menu creation for prefab still works
namespace VRSYS.Meta.Collocation
{
    public class CollocationManager : MonoBehaviour, INetworkUserCallbacks
    {
        #region Properties

        /// <summary>
        /// State of the Collocation Process
        /// </summary>
        public CollocationState State => _currentState.State;
        [HideInInspector] public UnityEvent<CollocationStateMessage> OnStateChanged = new ();

        [HideInInspector] public UnityEvent OnRestart = new();
        
        [Header("Configuration")] 
        
        [Tooltip("Time in seconds defining how long existing collocation sessions are searched.")]
        [SerializeField] private float _discoveryTime = 10f;
        public float DiscoverTime => _discoveryTime;
        
        [Tooltip("Time in seconds defining how long system waits before retrying failed action.")]
        [SerializeField] private float _retryTime = 1;
        public float RetryTime => _retryTime;
        
        [Tooltip("Defines how often failed actions are retried, before process stops.")]
        [SerializeField] private int _maxRetries = 5;
        public int MaxRetries => _maxRetries;
        
        [Tooltip("User roles that try to collocate themselves.")]
        [SerializeField] [UserRoleSelector] private List<UserRole> _collocationRoles;
        public List<UserRole> CollocationRoles => _collocationRoles;
        
        [Header("Anchor configuration")]
        
        [Tooltip("If true, local anchor is used to create collocation session.")]
        [SerializeField] private bool _useLocalAnchor = false;

        [Tooltip("If true, tries to load previous anchor automatically")] 
        [SerializeField] private bool _tryLoadLocalAnchor = false;
        
        [Tooltip("If true, session anchor is always created at DefaultSessionAnchorWorldPosition.")]
        [SerializeField] private bool _useDefaultSessionAnchor = true;
        public bool UseDefaultSessionAnchor => _useDefaultSessionAnchor;
        
        [Tooltip("World position at which default anchor is created.")]
        [SerializeField] private Vector3 _defaultSessionAnchorWorldPosition = Vector3.zero;
        public Vector3 DefaultSessionAnchorWorldPosition => _defaultSessionAnchorWorldPosition;

        [Tooltip("Anchor prefab spawned to create anchor.")] 
        [SerializeField] private OVRSpatialAnchor _anchorPrefab;
        public OVRSpatialAnchor AnchorPrefab => _anchorPrefab;

        [Header("UI")] 
        
        [Tooltip("Prefab of UI used to display available collocation sessions.")]
        [SerializeField] private SessionListUi _sessionListUiPrefab;
        public SessionListUi SessionListUiPrefab => _sessionListUiPrefab;

        [Tooltip("Prefab of UI used to create a new collocation session.")]
        [SerializeField] private CreateSessionUi _createSessionUiPrefab;
        public CreateSessionUi CreateSessionUi => _createSessionUiPrefab;
        
        [Tooltip("Prefab of UI used to confirm anchor alignment.")]
        [SerializeField] private ConfirmationUI _confirmationUIPrefab;
        public ConfirmationUI ConfirmationUIPrefab => _confirmationUIPrefab;
        
        [Header("Debugging")] 
        
        [Tooltip("If true, Info logs are printed to the console. If false, only Warning and Error logs will be printed.")]
        [SerializeField] private bool _verbose = true;

        #region Local Anchor Properties

        public string AnchorIDsFilePath { get; private set; } // Persistent anchor storage path

        #endregion
        
        public List<OVRColocationSession.Data> SessionDatas { get; private set; }

        private OVRColocationSession.Data _joinedSessionData; // only set if session client
        public OVRColocationSession.Data JoinedSessionData => _joinedSessionData;
        
        public bool IsSessionHost { get; private set; }
        
        public Guid HostedSessionId { get; private set; }
        
        public OVRSpatialAnchor CurrentAnchor { get; private set; }
        
        public SavedAnchorIDManager SavedAnchorIDManager { get; private set; }

        private bool _isSuccessfullyCollocated = false;

        private bool _isFailed = false;

        public bool CanRestart => _isSuccessfullyCollocated || _isFailed;
        
        private CollocationStateHandler _currentState;
        
        #endregion

        #region INetworkUserCallbacks

        public void OnLocalNetworkUserSetup()
        {
            if (_collocationRoles.Contains(NetworkUser.LocalInstance.userRole.Value))
            {
                SavedAnchorIDManager = new SavedAnchorIDManager();
                StartCollocation();
            }
        }

        public void OnRemoteNetworkUserSetup(NetworkUser user)
        {
            // ...
        }

        #endregion

        #region Public Methods

        public void BroadcastState(CollocationStateMessage message)
        {
            OnStateChanged.Invoke(message);
            LogStateMessage(message);
        }

        public void EnterState<T>() where T : CollocationStateHandler
        {
            _currentState = (T)Activator.CreateInstance(typeof(T), args: new object[]{this});
            _currentState.StartState();
        }

        public void AddAvailableSession(OVRColocationSession.Data sessionData)
        {
            if (SessionDatas == null)
                SessionDatas = new List<OVRColocationSession.Data>();
            
            SessionDatas.Add(sessionData);
        }

        public void SetJoinedSession(OVRColocationSession.Data data) =>_joinedSessionData = data;

        public void ResetSessionData()
        {
            SessionDatas = null;
            _joinedSessionData = default;
        }

        public void SetHostInformation(Guid sessionId)
        {
            IsSessionHost = true;
            HostedSessionId = sessionId;
        }

        public void ResetHostInformation()
        {
            IsSessionHost = false;
            HostedSessionId = default;
        }

        public void SetCurrentAnchor(OVRSpatialAnchor anchor) => CurrentAnchor = anchor;

        public void SetIsSuccessfullyCollocated(bool isCollocated) => _isSuccessfullyCollocated = isCollocated;

        public void SetIsFailed(bool isFailed) => _isFailed = isFailed;

        public void RestartCollocation()
        {
            if (!CanRestart)
                return;
            
            OnRestart.Invoke();

            _currentState = new RestartCollocationStateHandler(this, StartCollocation);
            _currentState.StartState();
        }

        #endregion

        #region Private Methods

        private void LogStateMessage(CollocationStateMessage message)
        {
            switch (message.Status)
            {
                case CollocationStateStatus.Failed:
                    ExtendedLogger.LogInfo(GetType().Name, $"[{message.State}] [{message.Status}] {message.Message}",
                        this);
                    break;
                case CollocationStateStatus.Error:
                    ExtendedLogger.LogError(GetType().Name, $"[{message.State}] [{message.Status}] {message.Message}",
                        this);
                    break;
                default:
                    if(_verbose)
                        ExtendedLogger.LogInfo(GetType().Name, $"[{message.State}] [{message.Status}] {message.Message}",
                            this);
                    break;
            }
        }
        
        /// <summary>
        /// Entry point
        /// </summary>
        private void StartCollocation()
        {
            if (_useLocalAnchor)
            {
                if (_tryLoadLocalAnchor)
                    EnterState<LoadingLocalAnchorStateHandler>();
                else
                    EnterState<CreatingLocalAnchorStateHandler>();
            }
            else
            {
                EnterState<SearchSessionStateHandler>();
            }
        }

        #endregion
    }
}
