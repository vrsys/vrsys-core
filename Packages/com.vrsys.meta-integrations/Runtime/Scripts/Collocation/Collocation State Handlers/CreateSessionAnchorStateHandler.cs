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

using System;
using System.Threading.Tasks;
using UnityEngine;
using VRSYS.Core.Networking;
using Object = UnityEngine.Object;

namespace VRSYS.Meta.Collocation
{
    public class CreateSessionAnchorStateHandler : CollocationStateHandler
    {
        #region Properties

        private AnchorCreationManager _anchorCreationManager;

        #endregion
        
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
            
            _manager.EnterState<ShareSessionAnchorStateHandler>();
        }

        #endregion

        #region Private Methods

        private void StartCreateAnchor()
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Started,
                "Starting to create session anchor.");
            _manager.BroadcastState(stateMessage);

            if (_manager.UseDefaultSessionAnchor)
            {
                CreateAnchor(_manager.DefaultSessionAnchorWorldPosition, Quaternion.identity);
            }
            else
            {
                StartCustomAnchorCreation();
            }
        }

        private void StartCustomAnchorCreation()
        {
            _anchorCreationManager = NetworkUser.LocalInstance.GetComponent<AnchorCreationManager>();

            if (_anchorCreationManager == null)
            {
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Error,
                    "Required AnchorCreationManager component not found on local user."));

                return;
            }
            
            _anchorCreationManager.OnUserDefinedAnchor.AddListener(CreateAnchor);
            _anchorCreationManager.SetupAnchorCreationMode();

            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running,
                "Started custom anchor creation process."));
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
                    
                    _manager.SetIsFailed(true);
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
