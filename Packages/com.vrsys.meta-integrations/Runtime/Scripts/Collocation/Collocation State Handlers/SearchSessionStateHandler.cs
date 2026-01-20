using System.Threading.Tasks;
using VRSYS.Core.Logging;
using VRSYS.Meta.Collocation;

public class SearchSessionStateHandler : CollocationStateHandler
{
    #region Constructor

    public SearchSessionStateHandler(CollocationManager manager) : base(manager)
    {
        State = CollocationState.SearchingCollocationSession;
    }

    #endregion

    #region Collocation State Handler Methods

    public override void StartState()
    {
        StartDiscoveringSessions();
    }
    
    protected override void EndState()
    {
        if (_manager.SessionDatas == null || _manager.SessionDatas.Count == 0)
        {
            // TODO: switch to create session state
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
            if (_retryCount == _manager.MaxRetries)
            {
                stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Failed,
                    $"Failed to start collocation session discovery. Result: {discoveryStartResult.Status}");
                ExtendedLogger.LogWarning(GetType().Name, stateMessage.Message, _manager);
                _manager.BroadcastState(stateMessage);
                return;
            }

            _retryCount++;
            
            if(_manager.Verbose)
                ExtendedLogger.LogInfo(GetType().Name, $"Failed to start collocation session discovery. Retry in {_manager.RetryTime} seconds.");

            await Task.Delay((int)(_manager.RetryTime * 1000));

            StartDiscoveringSessions();
            
            return;
        }

        stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Running,
            $"Started collocation discovery for {_manager.DiscoverTime} seconds.");
        
        if(_manager.Verbose)
            ExtendedLogger.LogInfo(GetType().Name, stateMessage.Message, _manager);

        await Task.Delay((int)(_manager.DiscoverTime * 1000));

        EndState();
    }

    #endregion

    #region Event Callbacks

    private void OnSessionDiscovered(OVRColocationSession.Data sessionData) => _manager.AddSession(sessionData);

    #endregion
}
