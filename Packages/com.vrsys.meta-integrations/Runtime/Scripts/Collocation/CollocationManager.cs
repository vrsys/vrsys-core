using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using VRSYS.Core.Networking;

namespace VRSYS.Meta.Collocation
{
    public class CollocationManager : MonoBehaviour, INetworkUserCallbacks
    {
        #region Properties

        [Header("State")]
        public CollocationState State => _currentState.State;
        public UnityEvent<CollocationStateMessage> OnStateChanged = new ();

        [Header("Configuration")] 
        [SerializeField] private float _discoveryTime = 10f;
        public float DiscoverTime => _discoveryTime;
        [SerializeField] private float _retryTime = 1;
        public float RetryTime => _retryTime;
        [SerializeField] private int _maxRetries = 5;
        public int MaxRetries => _maxRetries;

        [SerializeField] [UserRoleSelector] private List<UserRole> _collocationRoles;

        [FormerlySerializedAs("_sessionListUi")]
        [Header("UI")] 
        
        [SerializeField] private GameObject _sessionListUiPrefab;
        public GameObject SessionListUiPrefab => _sessionListUiPrefab;
        
        [Header("Debugging")] 
        [SerializeField] private bool _verbose = true;
        public bool Verbose => _verbose;

        public List<OVRColocationSession.Data> SessionDatas { get; private set; }

        private OVRColocationSession.Data _currentSessionData;
        public OVRColocationSession.Data CurrentSessionData => _currentSessionData;
        
        #endregion

        #region Collocation States

        private CollocationStateHandler _currentState;
        
        public SearchSessionStateHandler SearchSessionStateHandler { get; private set; }
        
        public DisplaySessionsStateHandler DisplaySessionsStateHandler { get; private set; }

        #endregion

        #region INetworkUserCallbacks

        public void OnLocalNetworkUserSetup()
        {
            if (_collocationRoles.Contains(NetworkUser.LocalInstance.userRole.Value))
            {
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
        }

        public void EnterState(CollocationStateHandler state)
        {
            _currentState = state;
            state.StartState();
        }

        public void AddSession(OVRColocationSession.Data sessionData)
        {
            if (SessionDatas == null)
                SessionDatas = new List<OVRColocationSession.Data>();
            
            SessionDatas.Add(sessionData);
        }

        public void SetCurrentSession(OVRColocationSession.Data data) =>_currentSessionData = data;

        #endregion

        #region Private Methods

        private void InitializeStates()
        {
            SearchSessionStateHandler = new SearchSessionStateHandler(this);
            DisplaySessionsStateHandler = new DisplaySessionsStateHandler(this);
        }
        
        private void StartCollocation()
        {
            if (ConnectionManager.Instance.offlineSession)
            {
                // TODO: Load local anchor state
            }
            else
            {
                EnterState(SearchSessionStateHandler);
            }
        }

        #endregion
    }
}
