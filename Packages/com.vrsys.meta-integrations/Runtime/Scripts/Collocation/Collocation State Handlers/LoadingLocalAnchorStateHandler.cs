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
using UnityEngine;
using System.Collections.Generic;

namespace VRSYS.Meta.Collocation
{
    /// <summary>
    /// TODO: Implement selection from existing spatial anchors. Currently uses last in file.
    /// </summary>
    public class LoadingLocalAnchorStateHandler : CollocationStateHandler
    {
        private ConfirmationUI _confirmationUI;
        private OVRSpatialAnchor loadedAnchor;
        
        #region Constructor

        public LoadingLocalAnchorStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.LoadingLocalAnchor;
        }

        #endregion

        #region Collocation State Handler Methods

        public override void StartState()
        {
            // Check for existing anchors and load them
            LoadAnchor();
        }

        #endregion

        #region Private Methods

        private async void LoadAnchor()
        {
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Started, "Loading saved anchor IDs."));
            var anchorIDs = await SavedAnchorIDManager.LoadAnchorIdsFromFile();
            if (anchorIDs.Count > 0)
            {
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running,
                    $"Found {anchorIDs.Count} anchor IDs. Loading last anchor ID in file."));
                Guid[] guids = new Guid[anchorIDs.Count];
                anchorIDs.CopyTo(guids);
                LoadSpatialAnchor(guids[^1]);
            }
            else
            {
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Failed, "No anchor IDs saved."));
                _manager.EnterState<CreatingLocalAnchorStateHandler>();
            }
        }
        
        public async void LoadSpatialAnchor(Guid anchorUuid)
        {
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running,
                $"Loading OVRSpatialAnchor with ID ({anchorUuid})."));
            // Load and localize
            var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
            var result = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(new []{anchorUuid}, unboundAnchors);

            if (result.Success)
            {
                if (await unboundAnchors[0].LocalizeAsync())
                {
                    var pose = unboundAnchors[0].Pose;
                    loadedAnchor = GameObject.Instantiate(_manager.AnchorPrefab, pose.position, pose.rotation);
                    unboundAnchors[0].BindTo(loadedAnchor);
                    _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running, "Spatial anchor loaded sucessfully."));
                    WaitForConfirmation();
                }
            }
            else
            {
                SavedAnchorIDManager.DeleteIDfromSaved(anchorUuid); // Clean up ID that cannot be loaded
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Failed, "Failed to load spatial anchor."));
                _manager.EnterState<CreatingLocalAnchorStateHandler>();
            }
        }

        private void WaitForConfirmation()
        {
            _confirmationUI = GameObject.Instantiate(_manager.ConfirmationUIPrefab);
            _confirmationUI.Initialize(OnConfirm: OnConfirmAnchor, OnReject: OnCreateNewAnchor);
            _confirmationUI.Show();
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running, "Waiting for user confirmation of loaded anchor..."));
        }

        /// <summary>
        ///  Callback to User confirming selected anchors
        /// </summary>
        private void OnConfirmAnchor()
        {
            _manager.SetCurrentAnchor(loadedAnchor);
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Success, "User confirmed loaded anchor."));
            EndState<AligningToAnchorStateHandler>();
        }

        /// <summary>
        ///  Callback to User confirming selected anchors
        /// </summary>
        private async void OnCreateNewAnchor()
        {
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running, "User rejected loaded anchor."));
            
            var result = await loadedAnchor.EraseAnchorAsync();
            if (result.Success)
            {
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Success, "Successfully erased the loaded spatial anchor from persistent storage."));
            }
            else
            {
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Failed, $"Failed to erase the loaded spatial anchor from persistent storage. {result.Status}"));
            }
            
            await SavedAnchorIDManager.DeleteIDfromSaved(loadedAnchor.Uuid);
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Success, "Deleted anchor ID."));
            
            GameObject.Destroy(loadedAnchor.gameObject);
            loadedAnchor = null;
            
            EndState<CreatingLocalAnchorStateHandler>();
        }

        protected override void EndState<T>()
        {
            // Teardown actions
            _confirmationUI.Hide();
            GameObject.Destroy(_confirmationUI.gameObject);
            
            base.EndState<T>();
        }

        #endregion
    }
}