using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
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

        [Header("Debugging")] 
        [SerializeField] private bool _verbose = true;
        public bool Verbose => _verbose;

        public List<OVRColocationSession.Data> SessionDatas { get; private set; }
        
        public AnchorCreationManager AnchorCreationManager { get; private set; }
        
        #endregion

        #region Collocation States

        private CollocationStateHandler _currentState;
        
        public SearchSessionStateHandler SearchSessionStateHandler { get; private set; }
        
        // Local Anchor States
        public LoadingLocalAnchorStateHandler LoadingLocalAnchorStateHandler { get; private set; }
        public CreatingLocalAnchorStateHandler CreatingLocalAnchorStateHandler { get; private set; }
        
        // TODO: For local anchor or support both group and individual anchors?
        public AligningToAnchorStateHandler AligningToAnchorStateHandler { get; private set; }
        
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

        #endregion

        #region Private Methods

        private void InitializeStates()
        {
            SearchSessionStateHandler = new SearchSessionStateHandler(this);
            CreatingLocalAnchorStateHandler = new CreatingLocalAnchorStateHandler(this);
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
