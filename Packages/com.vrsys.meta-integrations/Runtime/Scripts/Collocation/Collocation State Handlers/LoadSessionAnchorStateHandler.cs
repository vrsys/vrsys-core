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
                    
                    _manager.SetIsFailed(true);
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
                
                _manager.SetIsFailed(true);
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
