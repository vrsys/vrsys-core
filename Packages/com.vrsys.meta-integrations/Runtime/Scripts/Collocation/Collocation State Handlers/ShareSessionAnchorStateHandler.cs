
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VRSYS.Meta.Collocation
{
    public class ShareSessionAnchorStateHandler : CollocationStateHandler
    {
        #region Constructor

        public ShareSessionAnchorStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.SharingSessionAnchor;
        }

        #endregion

        #region Collocation State Handler Methods

        public override void StartState()
        {
            ShareSessionAnchor();
        }

        protected override void EndState(CollocationStateHandler nextStateHandler)
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Success,
                "Successfully shared session anchor.");
            _manager.BroadcastState(stateMessage);
            
            base.EndState(nextStateHandler);
        }

        #endregion

        #region Private Methods

        private async void ShareSessionAnchor()
        {
            bool isFirstTry = _retryCount == 0;

            CollocationStateStatus status = isFirstTry ? CollocationStateStatus.Started : CollocationStateStatus.Retry;
            string message = isFirstTry
                ? $"Starting to share session anchor in collocation session {_manager.HostedSessionId}."
                : $"Retry to share session anchor in collocation session {_manager.HostedSessionId}";

            CollocationStateMessage stateMessage = new CollocationStateMessage(State, status, message);
            _manager.BroadcastState(stateMessage);

            var shareResult = await OVRSpatialAnchor.ShareAsync(new List<OVRSpatialAnchor> { _manager.CurrentAnchor },
                _manager.HostedSessionId);

            if (!shareResult.Success)
            {
                if (_retryCount == _manager.MaxRetries)
                {
                    stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Failed,
                        $"Failed to share session anchor. Status: {shareResult.Status}");
                    _manager.BroadcastState(stateMessage);
                }

                _retryCount++;

                stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Retry,
                    $"Failed to share session anchor. Retry in {_manager.RetryTime} seconds.");
                _manager.BroadcastState(stateMessage);

                await Task.Delay((int)(_manager.RetryTime * 1000));
                
                ShareSessionAnchor();
                return;
            }
            
            EndState(_manager.AligningToAnchorStateHandler);
        }

        #endregion
    }
}
