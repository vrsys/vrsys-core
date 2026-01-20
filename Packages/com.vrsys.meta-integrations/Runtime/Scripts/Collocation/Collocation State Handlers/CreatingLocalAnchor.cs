using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VRSYS.Meta.Collocation
{
    public class CreatingLocalAnchorStateHandler : CollocationStateHandler
    {
        #region Fields
        
        private string m_FilePath; // Presistent anchor storage path
        private HashSet<Guid> _anchorUuids = new(); // Saved anchor UUIDs
        
        private List<OVRSpatialAnchor> _anchorInstances = new(); // Spatial anchor instances
        
        private Action<bool, OVRSpatialAnchor.UnboundAnchor> _onLocalized;

        #endregion
        
        #region Constructor

        public CreatingLocalAnchorStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.LoadingLocalAnchor;
            m_FilePath = Path.Combine(Application.persistentDataPath, "SavedAnchorIds.json");
        }

        #endregion

        #region Collocation State Handler Methods

        public override void StartState()
        {
            // Enable Anchor creation user interface
            // Subscribe CreateAnchor() callback
            
            // if user interface does not exist on user, ???
        }

        #endregion

        #region Private Methods

        protected override void EndState()
        {
            // When user confirms created anchor, save anchor
            foreach (OVRSpatialAnchor anchor in _anchorInstances)
            {
                SaveAnchorAsync(anchor);
            }
            
            // Then enter AligningToAnchorState
            manager.EnterState(manager.AligningToAnchorStateHandler);
        }

        #endregion
        
        #region User Input Callbacks

        public void CreateAnchor()
        {
            // Create a green (savable) spatial anchor
            GameObject go = Instantiate(_saveableAnchorPrefab, _saveableTransform.position, _saveableTransform.rotation);
            
            // Keep checking for a valid and localized anchor state
            if (!await anchor.WhenLocalizedAsync())
            {
                Debug.LogError($"Unable to create anchor.");
                anchor.gameObject
                return;
            }
            
            // Add the anchor to the list of all instances
            _anchorInstances.Add(anchor);
        }
        
        private async void SaveAnchorAsync(OVRSpatialAnchor anchor)
        {
            // save the savable (green) anchors only
            if ((await anchor.SaveAnchorAsync()).Success)
            {
                // Remember UUID so you can load the anchor later
                _anchorUuids.Add(anchor.Uuid);
            }
        }

        #endregion
    }
}