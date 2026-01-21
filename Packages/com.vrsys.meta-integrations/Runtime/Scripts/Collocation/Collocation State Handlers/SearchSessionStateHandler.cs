using System.Threading.Tasks;
using VRSYS.Core.Logging;
using VRSYS.Meta.Collocation;

namespace VRSYS.Meta.Collocation
{
    public class SearchSessionStateHandler : CollocationStateHandler
    {
        #region Constructor

    #endregion

    #region Collocation State Handler Methods

    public override void StartState()
    {
        StartDiscoveringSessions();
    }
    
    protected override void EndState()
    {
        OVRColocationSession.ColocationSessionDiscovered -= OnSessionDiscovered;
        OVRColocationSession.StopDiscoveryAsync();
        
        if (_manager.SessionDatas == null || _manager.SessionDatas.Count == 0)
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Success,
                "Could not find existing collocation sessions.");
            _manager.BroadcastState(stateMessage);
            
            _manager.EnterState(_manager.CreateSessionStateHandler);
        }
        else
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Success,
                $"Found {_manager.SessionDatas.Count} collocation sessions");
            _manager.BroadcastState(stateMessage);
            
            _manager.EnterState(_manager.DisplaySessionsStateHandler);
        }
    }

    #endregion

    #region Private Methods

    private async void StartDiscoveringSessions()
    {
        bool isFirstTry = _retryCount == 0;
        
        CollocationStateStatus status = isFirstTry ? CollocationStateStatus.Started : CollocationStateStatus.Retry;

        string message = isFirstTry
            ? "Try starting discovery of collocation sessions."
            : $"Retry to start discovery of collocation sessions. Retry: {_retryCount}";

        CollocationStateMessage stateMessage = new CollocationStateMessage(State, status, message);
        _manager.BroadcastState(stateMessage);
        
        OVRColocationSession.ColocationSessionDiscovered += OnSessionDiscovered;
        var discoveryStartResult = await OVRColocationSession.StartDiscoveryAsync(); // start discovery

        if (discoveryStartResult.Status == OVRColocationSession.Result.Failure)
        {
            OVRColocationSession.ColocationSessionDiscovered -= OnSessionDiscovered;
            
            if (_retryCount == _manager.MaxRetries)
            {
                stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Failed,
                    $"Failed to start collocation session discovery. Result: {discoveryStartResult.Status}. Stopping collocation process.");
                _manager.BroadcastState(stateMessage);
                return;
            }

            _retryCount++;

            stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Retry,
                $"Failed to start collocation session discovery. Retry in {_manager.RetryTime} seconds.");
            _manager.BroadcastState(stateMessage);

            await Task.Delay((int)(_manager.RetryTime * 1000));

            await Task.Delay((int)(manager.DiscoverTime * 1000));

            EndState();
        }

        stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Running,
            $"Started collocation discovery for {_manager.DiscoverTime} seconds.");
        _manager.BroadcastState(stateMessage);

        await Task.Delay((int)(_manager.DiscoverTime * 1000));

        EndState();
    }

    #endregion

    #region Event Callbacks

    private void OnSessionDiscovered(OVRColocationSession.Data sessionData) => _manager.AddAvailableSession(sessionData);

    #endregion
}
