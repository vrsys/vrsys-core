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
using VRSYS.Core.Logging;
using VRSYS.Core.Networking;

namespace VRSYS.Meta.Collocation
{
    public class CreatingLocalAnchorStateHandler : CollocationStateHandler
    {
        private AnchorCreationManager _anchorCreationManager;
        
        #region Constructor

        public CreatingLocalAnchorStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.CreatingLocalAnchor;
        }

        #endregion

        #region Collocation State Handler Methods

        public override void StartState()
        {
            _anchorCreationManager = NetworkUser.LocalInstance.GetComponent<AnchorCreationManager>();
            if (_anchorCreationManager == null)
            {
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Error,
                    "Required AnchorCreationManager component not found on local user."));
                
                _manager.SetIsFailed(true);
                return;
            }
            _anchorCreationManager.OnUserDefinedAnchor.AddListener(OnUserDefinedAnchor);
            _anchorCreationManager.SetupAnchorCreationMode();
            
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Started,
                "Started anchor creation process.");
            _manager.BroadcastState(stateMessage);
        }
        
        protected override void EndState()
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Success,
                "Local Anchor created successfully.");
            _manager.BroadcastState(stateMessage);
            
            // Then enter AligningToAnchorState
            _manager.EnterState<AligningToAnchorStateHandler>();
        }

        #endregion
        
        #region Private Methods
        
        // Callback for AlignmentAnchorCreationManager.OnAnchorCreated Event
        private async void OnUserDefinedAnchor(Vector3 targetWorldPosition, Quaternion targetWorldRotation)
        {
            try
            {
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running,
                    "Creating user defined spatial anchor."));
                
                OVRSpatialAnchor anchor = await CreateAnchor(targetWorldPosition, targetWorldRotation);
                if (anchor == null)
                {
                    _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Failed,
                        "Could not create spatial anchor."));
                    return;
                }
                SaveAnchor(anchor);
            }
            catch (Exception e)
            {
                ExtendedLogger.LogError(GetType().Name, $"Error during anchor creation: {e.Message}");
            }
        }
        
        // copied from CollocationManagerOld.cs TODO: refactor to have only one method :)
        private async Task<OVRSpatialAnchor> CreateAnchor(Vector3 position, Quaternion rotation)
        {
            try
            {
                // create anchor at given position and rotation
                var go = GameObject.Instantiate(_manager.AnchorPrefab, position, rotation);
                OVRSpatialAnchor anchor = go.GetComponent<OVRSpatialAnchor>();
                
                // wait for anchor UUID to be valid
                while (!anchor.Created)
                {
                    await Task.Yield();
                }

                ExtendedLogger.LogInfo(GetType().Name, $"Anchor created successfully. UUID: {anchor.Uuid}");
                return anchor;
            }
            catch (Exception e)
            {
                ExtendedLogger.LogError(GetType().Name, $"Error during anchor creation: {e.Message}");
                return null;
            }
        }
        
        private async void SaveAnchor(OVRSpatialAnchor anchor)
        {
            // save anchor to meta cloud
            var saveResult = await anchor.SaveAnchorAsync();

            if (!saveResult.Success)
            {
                if (_retryCount == _manager.MaxRetries)
                {
                    _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Failed,
                        $"Failed to save spatial anchor. Result: {saveResult.Status}"));
                    return;
                }
                _retryCount++;
            
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Retry,
                    $"Failed to save spatial anchor. Retry in {_manager.RetryTime} seconds."));
            
                await Task.Delay((int)(_manager.RetryTime * 1000));
                SaveAnchor(anchor);
                return;
            }

            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running,
                $"Saved spatial anchor with UUID: {anchor.Uuid}"));
            
            _manager.SetCurrentAnchor(anchor);
            try
            {
                SavedAnchorIDManager.SaveAnchorID(anchor.Uuid);
            }
            catch (Exception e)
            {
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Failed,
                    $"Could not save anchor ID to device storage. Exception: {e.Message}"));
            }
            
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running,
                $"Saved spatial anchor with UUID: {anchor.Uuid}"));
            EndState();
        }
        
        #endregion
    }
}