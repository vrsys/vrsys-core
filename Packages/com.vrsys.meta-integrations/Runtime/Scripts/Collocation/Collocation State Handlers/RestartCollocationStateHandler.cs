using UnityEngine;
using UnityEngine.Events;

namespace VRSYS.Meta.Collocation
{
    public class RestartCollocationStateHandler : CollocationStateHandler
    {
        #region Properties

        private UnityAction _endAction;

        #endregion
        
        #region Constructor

        public RestartCollocationStateHandler(CollocationManager manager, UnityAction endFunction) : base(manager)
        {
            State = CollocationState.RestartCollocation;
            _endAction = endFunction;
        }

        #endregion

        #region Collocation State Handler Methods

        public override void StartState()
        {
            ResetCollocationManager();
        }

        protected override void EndState()
        {
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Success,
                "Successful reset. Restarting collocation process..."));
            
            _endAction.Invoke();
        }

        #endregion

        #region Private Methods

        private void ResetCollocationManager()
        {
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Started,
                "Resetting current collocation to restart collocation process.."));
            
            if (_manager.IsSessionHost)
            {
                OVRColocationSession.StopAdvertisementAsync();
                _manager.ResetHostInformation();
            }

            _manager.ResetSessionData();

            GameObject.Destroy(_manager.CurrentAnchor.gameObject);
            
            _manager.SetIsSuccessfullyCollocated(false);
            _manager.SetIsFailed(false);
            
            EndState();
        }

        #endregion
    }
}
