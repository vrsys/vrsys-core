using UnityEngine;

namespace VRSYS.Meta.Collocation
{
    public class DisplaySessionsStateHandler : CollocationStateHandler
    {
        #region Properties

        private SessionListUi _sessionListUi;

        #endregion
        
        #region Constructor

        public DisplaySessionsStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.DisplaySessions;
        }

        #endregion

        #region Collocation State Handler Methods

        public override void StartState()
        {
            InitializeSessionListUi();
        }

        protected override void EndState()
        {
            
        }

        #endregion

        #region Private Methods

        private void InitializeSessionListUi()
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Started,
                "Initializing session list ui.");
            _manager.BroadcastState(stateMessage);
            
            GameObject go = GameObject.Instantiate(_manager.SessionListUiPrefab);
            _sessionListUi = go.GetComponent<SessionListUi>();
            
            _sessionListUi.Initialize(_manager.SessionDatas, this);
        }

        #endregion

        #region Public Methods

        public void JoinSession(OVRColocationSession.Data data)
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Success,
                $"Joining collocation session. UUID: {data.AdvertisementUuid}");
            _manager.BroadcastState(stateMessage);
            
            _manager.SetCurrentSession(data);
            
            // TODO: Enter load session anchor state
        }

        public void CreateSession()
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Success,
                "Selected creation of new session.");
            _manager.BroadcastState(stateMessage);
            
            // TODO: Enter create session state
        }

        #endregion
    }
}
