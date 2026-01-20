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
        CollocationStateMessage message = new CollocationStateMessage(State, CollocationStateStatus.Started,
            "Starting discovery of collocation sessions...");
        manager.UpdateState(message);

        StartDiscoveringSessions();
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
        manager.UpdateState(stateMessage);
        
        OVRColocationSession.ColocationSessionDiscovered += OnSessionDiscovered;
        var discoveryStartResult = await OVRColocationSession.StartDiscoveryAsync(); // start discovery

        if (discoveryStartResult.Status == OVRColocationSession.Result.Failure)
        {
            if (_retryCount == manager.MaxRetries)
            {
                stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Failed,
                    $"Failed to start collocation session discovery. Result: {discoveryStartResult.Status}");
                ExtendedLogger.LogError(GetType().Name, stateMessage.Message, manager);
                return;
            }

            _retryCount++;
            
            if(manager.Verbose)
                ExtendedLogger.LogInfo(GetType().Name, $"Failed to start collocation session discovery. Retry in {manager.RetryTime} seconds.");

            await Task.Delay((int)(manager.RetryTime * 1000));

            StartDiscoveringSessions();
            
            return;
        }

        stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Running,
            $"Started collocation discovery for {manager.DiscoverTime} seconds.");
        
        if(manager.Verbose)
            ExtendedLogger.LogInfo(GetType().Name, stateMessage.Message, manager);

        await Task.Delay((int)(manager.DiscoverTime * 1000));

        EvaluateDiscoveryResults();
    }

    private void EvaluateDiscoveryResults()
    {
        
    }

    #endregion

    #region Event Callbacks

    private void OnSessionDiscovered(OVRColocationSession.Data data)
    {
        
    }

    #endregion
}
