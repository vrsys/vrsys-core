// VRSYS plugin of Virtual Reality and Visualization Group (Bauhaus-University Weimar)
//  _    ______  _______  _______
// | |  / / __ \/ ___/\ \/ / ___/
// | | / / /_/ /\__ \  \  /\__ \ 
// | |/ / _, _/___/ /  / /___/ / 
// |___/_/ |_|/____/  /_//____/  
//
//  __                            __                       __   __   __    ___ .  . ___
// |__)  /\  |  | |__|  /\  |  | /__`    |  | |\ | | \  / |__  |__) /__` |  |   /\   |  
// |__) /~~\ \__/ |  | /~~\ \__/ .__/    \__/ | \| |  \/  |___ |  \ .__/ |  |  /~~\  |  
//
//       ___               __                                                           
// |  | |__  |  |\/|  /\  |__)                                                          
// |/\| |___ |  |  | /~~\ |  \                                                                                                                                                                                     
//
// Copyright (c) 2023 Virtual Reality and Visualization Group
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//-----------------------------------------------------------------
//   Authors:        Tony Zoeppig, Karoline Brehm
//   Date:           2025
//-----------------------------------------------------------------

using System.Threading.Tasks;
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
        OVRColocationSession.ColocationSessionDiscovered -= OnSessionDiscovered;
        OVRColocationSession.StopDiscoveryAsync();
        
        if (_manager.SessionDatas == null || _manager.SessionDatas.Count == 0)
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Success,
                "Could not find existing collocation sessions.");
            _manager.BroadcastState(stateMessage);
            
            _manager.EnterState<CreateSessionStateHandler>();
        }
        else
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Success,
                $"Found {_manager.SessionDatas.Count} collocation sessions");
            _manager.BroadcastState(stateMessage);
            
            _manager.EnterState<DisplaySessionsStateHandler>();
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
                
                _manager.SetIsFailed(true);
                
                return;
            }

            _retryCount++;

            stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Retry,
                $"Failed to start collocation session discovery. Retry in {_manager.RetryTime} seconds.");
            _manager.BroadcastState(stateMessage);

            await Task.Delay((int)(_manager.RetryTime * 1000));

            StartDiscoveringSessions();
            
            return;
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