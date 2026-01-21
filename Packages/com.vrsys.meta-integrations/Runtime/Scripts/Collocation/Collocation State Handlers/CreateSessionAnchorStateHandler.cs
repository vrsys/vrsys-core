using System;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VRSYS.Meta.Collocation
{
    public class CreateSessionAnchorStateHandler : CollocationStateHandler
    {
        #region Constructor

        public CreateSessionAnchorStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.CreatingSessionAnchor;
        }

        #endregion

        #region Collocation State Handler Methodds

        public override void StartState()
        {
            StartCreateAnchor();
        }

        protected override void EndState()
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Success,
                "Successfully created session anchor.");
            _manager.BroadcastState(stateMessage);
            
            // TODO: Enter share anchor state
        }

        #endregion

        #region Private Methods

        private void StartCreateAnchor()
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Started,
                "Starting to create session anchor.");
            _manager.BroadcastState(stateMessage);

            if (_manager.UseDefaultAnchor)
            {
                CreateAnchor(_manager.DefaultAnchorWorldPosition, Quaternion.identity);
            }
            else
            {
                // TODO: implement anchor positioning mechanism
            }
        }

        private async void CreateAnchor(Vector3 targetWorldPosition, Quaternion targetWorldRotation)
        {
            bool isFirstTry = _retryCount == 0;

            CollocationStateStatus status = isFirstTry ? CollocationStateStatus.Running : CollocationStateStatus.Retry;
            string message = isFirstTry
                ? "Starting to create session anchor."
                : $"Retry to create session anchor. Retry: {_retryCount}";

            CollocationStateMessage stateMessage = new CollocationStateMessage(State, status, message);
            _manager.BroadcastState(stateMessage);

            try
            {
                OVRSpatialAnchor anchor =
                    Object.Instantiate(_manager.AnchorPrefab, targetWorldPosition, targetWorldRotation);

                while (!anchor.Created)
                {
                    await Task.Yield();
                }
                
                _manager.SetCurrentAnchor(anchor);
                
                EndState();
            }
            catch(Exception e)
            {
                if (_retryCount == _manager.MaxRetries)
                {
                    stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Error,
                        $"Error creating session anchor. Exception: {e.Message}");
                    _manager.BroadcastState(stateMessage);
                    return;
                }

                _retryCount++;

                stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Retry,
                    $"Failed to create session anchor. Exception: {e.Message}. Retry in {_manager.RetryTime} seconds");
                _manager.BroadcastState(stateMessage);

                await Task.Delay((int)(_manager.RetryTime * 1000));
                
                CreateAnchor(targetWorldPosition, targetWorldRotation);
            }
        }

        #endregion
    }
}
