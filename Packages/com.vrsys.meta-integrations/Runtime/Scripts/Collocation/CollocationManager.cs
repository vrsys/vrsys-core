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
        public CollocationState State { get; private set; } = CollocationState.Idle;
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
        
        #endregion

        #region Collocation States

        private CollocationStateHandler _currentState;
        
        public SearchSessionStateHandler SearchSessionStateHandler { get; private set; }

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

        public void UpdateState(CollocationStateMessage message)
        {
            State = message.State;
            
            OnStateChanged.Invoke(message);
        }

        #endregion

        #region Private Methods

        private void InitializeStates()
        {
            SearchSessionStateHandler = new SearchSessionStateHandler(this);
        }
        
        private void StartCollocation()
        {
            if (ConnectionManager.Instance.offlineSession)
            {
                // TODO: Load local anchor state
            }
            else
            {
                _currentState = SearchSessionStateHandler;
                SearchSessionStateHandler.StartState();
            }
        }

        #endregion
    }
}
