using System.Collections.Generic;
using UnityEngine;
using Task = System.Threading.Tasks.Task;

namespace VRSYS.Meta.Collocation
{
    public class LoadSessionAnchorStateHandler : CollocationStateHandler
    {
        #region Constructor

        public LoadSessionAnchorStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.LoadingSessionAnchor;
        }

        #endregion

        #region Collocation State Handler Methods

        public override void StartState()
        {
            LoadSessionAnchor();
        }

        protected override void EndState()
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Success,
                "Successfully loaded and localized session anchor.");
            
            // TODO: enter alignment state
        }

        #endregion

        #region Private Methods

        private async void LoadSessionAnchor()
        {
            bool isFirstTry = _retryCount == 0;

            CollocationStateStatus status = isFirstTry ? CollocationStateStatus.Started : CollocationStateStatus.Retry;
            string message = isFirstTry ? "Starting to load session anchor." : "Retry to load session anchor.";

            CollocationStateMessage stateMessage = new CollocationStateMessage(State, status, message);
            _manager.BroadcastState(stateMessage);

            var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
            var loadResult =
                await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(_manager.JoinedSessionData.AdvertisementUuid,
                    unboundAnchors);

            if (!loadResult.Success || unboundAnchors.Count == 0)
            {
                if (_retryCount == _manager.MaxRetries)
                {
                    stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Failed,
                        $"Failed to load session anchor. Anchors found: {unboundAnchors.Count}, Result: {loadResult.Status}");
                    _manager.BroadcastState(stateMessage);
                    return;
                }

                _retryCount++;

                stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Retry,
                    $"Failed to load session anchor. Retrying in {_manager.RetryTime} seconds.");
                _manager.BroadcastState(stateMessage);

                await Task.Delay((int)(_manager.RetryTime * 1000));
                
                LoadSessionAnchor();
                
                return;
            }
            
            LocalizeAnchor(unboundAnchors[0]);
        }

        private async void LocalizeAnchor(OVRSpatialAnchor.UnboundAnchor unboundAnchor)
        {
            bool isFirstTry = _retryCount == 0;

            CollocationStateStatus status = isFirstTry ? CollocationStateStatus.Running : CollocationStateStatus.Retry;
            string message = isFirstTry ? "Starting to localize session anchor." : "Retry to localize session anchor.";

            CollocationStateMessage stateMessage = new CollocationStateMessage(State, status, message);
            _manager.BroadcastState(stateMessage);

            if (await unboundAnchor.LocalizeAsync())
            {
                stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Running,
                    "Session anchor successfully localized. Starting binding of anchor.");
                _manager.BroadcastState(stateMessage);

                var spatialAnchor = Object.Instantiate(_manager.AnchorPrefab);
                unboundAnchor.BindTo(spatialAnchor);
                
                _manager.SetCurrentAnchor(spatialAnchor);
                
                EndState();
                
                return;
            }

            if (_retryCount == _manager.MaxRetries)
            {
                stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Failed,
                    "Failed to localize session anchor.");
                _manager.BroadcastState(stateMessage);
                return;
            }

            _retryCount++;

            stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Retry,
                $"Failed to localize session anchor. Retry in {_manager.RetryTime} seconds.");
            _manager.BroadcastState(stateMessage);
            
            LocalizeAnchor(unboundAnchor);
        }

        #endregion
    }
}
