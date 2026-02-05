using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VRSYS.Core.Logging;
using VRSYS.Core.Networking;
using System.IO;


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
        
        #endregion

        #region Collocation States

        private CollocationStateHandler _currentState;

        public SearchSessionStateHandler SearchSessionStateHandler { get; private set; }
        
        // Local Anchor States
        public LoadingLocalAnchorStateHandler LoadingLocalAnchorStateHandler { get; private set; }
        public CreatingLocalAnchorStateHandler CreatingLocalAnchorStateHandler { get; private set; }
        public AligningToAnchorStateHandler AligningToAnchorStateHandler { get; private set; }
        
        // Meta Session States
        public DisplaySessionsStateHandler DisplaySessionsStateHandler { get; private set; }
        public CreateSessionStateHandler CreateSessionStateHandler { get; private set; }
        public LoadSessionAnchorStateHandler LoadSessionAnchorStateHandler { get; private set; }
        public CreateSessionAnchorStateHandler CreateSessionAnchorStateHandler { get; private set; }
        public ShareSessionAnchorStateHandler ShareSessionAnchorStateHandler { get; private set; }

        #endregion

        #region INetworkUserCallbacks

        public void OnLocalNetworkUserSetup()
        {
            if (_collocationRoles.Contains(NetworkUser.LocalInstance.userRole.Value))
            {
                SavedAnchorIDManager = new SavedAnchorIDManager();
                InitializeStates();
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

        public void EnterState(CollocationStateHandler state)
        {
            _currentState = state;
            state.StartState();
        }

        public void AddAvailableSession(OVRColocationSession.Data sessionData)
        {
            if (SessionDatas == null)
                SessionDatas = new List<OVRColocationSession.Data>();
            
            SessionDatas.Add(sessionData);
        }

        public void SetJoinedSession(OVRColocationSession.Data data) =>_joinedSessionData = data;

        public void SetHostInformation(Guid sessionId)
        {
            IsSessionHost = true;
            HostedSessionId = sessionId;
        }

        public void SetCurrentAnchor(OVRSpatialAnchor anchor) => CurrentAnchor = anchor;

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
        
        private void InitializeStates()
        {
            // Local Anchor States
            LoadingLocalAnchorStateHandler = new LoadingLocalAnchorStateHandler(this);
            CreatingLocalAnchorStateHandler = new CreatingLocalAnchorStateHandler(this);
            // Meta Shared Session States
            SearchSessionStateHandler = new SearchSessionStateHandler(this);
            DisplaySessionsStateHandler = new DisplaySessionsStateHandler(this);
            CreateSessionStateHandler = new CreateSessionStateHandler(this);
            LoadSessionAnchorStateHandler = new LoadSessionAnchorStateHandler(this);
            CreateSessionAnchorStateHandler = new CreateSessionAnchorStateHandler(this);
            ShareSessionAnchorStateHandler = new ShareSessionAnchorStateHandler(this);
            // Aligning State
            AligningToAnchorStateHandler = new AligningToAnchorStateHandler(this);
        }
        
        /// <summary>
        /// Entry point
        /// </summary>
        private void StartCollocation()
        {
            if (_useLocalAnchor)
            {
                if (_tryLoadLocalAnchor)
                    EnterState(LoadingLocalAnchorStateHandler);
                else
                    EnterState(CreatingLocalAnchorStateHandler);
            }
            else
            {
                EnterState(SearchSessionStateHandler);
            }
        }

        #endregion
    }
}
