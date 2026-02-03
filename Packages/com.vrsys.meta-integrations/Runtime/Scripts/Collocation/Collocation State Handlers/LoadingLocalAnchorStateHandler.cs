using System;
using UnityEngine;
using VRSYS.Core.Logging;
using System.Collections.Generic;

namespace VRSYS.Meta.Collocation
{
    /// <summary>
    /// TODO: Implement selection from existing spatial anchors. Currently uses last in file.
    /// </summary>
    public class LoadingLocalAnchorStateHandler : CollocationStateHandler
    {
        [SerializeField] private ConfirmationUI _confirmationUIPrefab;
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
            var anchorIDs = await SpatialAnchorManager.LoadAnchorIdsFromFile();
            if (anchorIDs.Count > 0)
            {
                Guid[] guids = new Guid[anchorIDs.Count];
                anchorIDs.CopyTo(guids);
                LoadSpatialAnchor(guids[^1]);
            }
            else
            {
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Failed, "No anchor IDs saved."));
                _manager.EnterState(_manager.CreatingLocalAnchorStateHandler);
            }
        }
        
        public async void LoadSpatialAnchor(Guid anchorUuid)
        {
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
                SpatialAnchorManager.DeleteIDfromSaved(anchorUuid); // Clean up ID that cannot be loaded
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Failed, "Failed to load spatial anchor."));
                _manager.EnterState(_manager.CreatingLocalAnchorStateHandler);
            }
        }

        private void WaitForConfirmation()
        {
            _confirmationUI = GameObject.Instantiate(_confirmationUIPrefab);
            _confirmationUI.Initialize(OnConfirm: OnConfirmAnchor, OnReject: OnCreateNewAnchor);
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Running, "Waiting for user confirmation of loaded anchor..."));
        }

        /// <summary>
        ///  Callback to User confirming selected anchors
        /// </summary>
        private void OnConfirmAnchor()
        {
            _manager.SetCurrentAnchor(loadedAnchor);
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Success, "User confirmed loaded anchor."));
            _manager.EnterState(_manager.AligningToAnchorStateHandler);
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
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Success, "Successfully erased the loaded spatial anchor."));
            }
            else
            {
                _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Failed, "Failed to erase the loaded spatial anchor."));
            }
            
            await SpatialAnchorManager.DeleteIDfromSaved(loadedAnchor.Uuid);
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Success, "Deleted anchor ID."));
            
            GameObject.Destroy(loadedAnchor);
            loadedAnchor = null;
            
            _manager.EnterState(_manager.CreatingLocalAnchorStateHandler);
        }

        protected override void EndState()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}