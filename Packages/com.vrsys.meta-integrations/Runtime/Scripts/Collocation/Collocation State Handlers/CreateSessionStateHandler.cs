using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace VRSYS.Meta.Collocation
{
    public class CreateSessionStateHandler : CollocationStateHandler
    {
        #region Properties

        private CreateSessionUi _createSessionUi;

        #endregion
        
        #region Constructor

        public CreateSessionStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.CreatingSession;
        }

        #endregion

        #region Collocation State Handler Methods

        public override void StartState()
        {
            InitializeCreateSessionUi();
        }

        protected override void EndState()
        {
            Object.Destroy(_createSessionUi.gameObject);
            
            _manager.EnterState(_manager.CreateSessionAnchorStateHandler);
        }

        #endregion

        #region Private Methods

        private void InitializeCreateSessionUi()
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Started,
                "Initializing session list ui.");
            _manager.BroadcastState(stateMessage);
            
            GameObject go = GameObject.Instantiate(_manager.CreateSessionUi.gameObject);
            _createSessionUi = go.GetComponent<CreateSessionUi>();
            
            _createSessionUi.Initialize(this);
        }

        #endregion

        #region Public Methods

        public async void CreateSession(string sessionName)
        {
            bool isFirstTry = _retryCount == 0;

            CollocationStateStatus status = isFirstTry ? CollocationStateStatus.Running : CollocationStateStatus.Retry;

            string message = isFirstTry
                ? $"Try creating collocation session {sessionName}."
                : $"Retry to start collocation session {sessionName}. Retry: {_retryCount}";

            CollocationStateMessage stateMessage = new CollocationStateMessage(State, status, message);
            _manager.BroadcastState(stateMessage);

            byte[] advertisementData = Encoding.UTF8.GetBytes(sessionName);
            var startAdvertisementResult = await OVRColocationSession.StartAdvertisementAsync(advertisementData);

            if (startAdvertisementResult.Success)
            {
                stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Success,
                    $"Successfully created collocation session {sessionName} (UUID: {startAdvertisementResult.Value}");
                _manager.BroadcastState(stateMessage);
                
                _manager.SetHostInformation(startAdvertisementResult.Value);
                
                EndState();
            }
            else
            {
                if (_retryCount == _manager.MaxRetries)
                {
                    stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Failed,
                        $"Failed to start collocation session {sessionName}");
                    _manager.BroadcastState(stateMessage);
                    return;
                }

                _retryCount++;

                stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Retry,
                    $"Failed to start collocation session {sessionName}. Retry in {_manager.RetryTime} seconds");
                _manager.BroadcastState(stateMessage);

                await Task.Delay((int)(_manager.RetryTime * 1000));
                
                CreateSession(sessionName);
            }
        }

        #endregion
    }
}
