using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VRSYS.Meta.Collocation
{
    public class CreatingLocalAnchorStateHandler : CollocationStateHandler
    {
        #region Fields
        
        private string m_FilePath; // Persistent anchor storage path
        private HashSet<Guid> _anchorUuids = new(); // Saved anchor UUIDs
        
        private List<OVRSpatialAnchor> _anchorInstances = new(); // Spatial anchor instances
        
        private Action<bool, OVRSpatialAnchor.UnboundAnchor> _onLocalized;

        #endregion
        
        #region Constructor

        public CreatingLocalAnchorStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.CreatingLocalAnchor;
            m_FilePath = Path.Combine(Application.persistentDataPath, "SavedAnchorIds.json");
        }

        #endregion

        #region Collocation State Handler Methods

        public override void StartState()
        {
            // TODO: Enable Anchor creation user interface
            // TODO: Subscribe AlignmentAnchorsCreated() callback
        }
        
        protected override void EndState()
        {
            // Then enter AligningToAnchorState
            manager.EnterState(manager.AligningToAnchorStateHandler);
        }

        #endregion
        
        #region Private Methods
        
        // Callback for AlignmentAnchorCreationManager
        public void AlignmentAnchorsCreated(List<OVRSpatialAnchor> anchors)
        {
            // Add the anchor to the list of all instances
            _anchorInstances.AddRange(anchors);
            
            // When user confirms created anchors, persist anchors
           foreach (OVRSpatialAnchor anchor in _anchorInstances)
           {
               SaveAnchorAsync(anchor);
           }
           SaveAnchorIdsToFile();
        }
        
        private async void SaveAnchorAsync(OVRSpatialAnchor anchor)
        {
            // Wait that the anchor is ready to use before saving (valid and localized anchor state)
            if (!await anchor.WhenLocalizedAsync())
            {
                Debug.LogError($"Unable to create anchor.");
                _anchorInstances.Remove(anchor);
                GameObject.Destroy(anchor.gameObject);
                return;
            }
            
            // Save anchor and save anchor UUID to file storage
            if ((await anchor.SaveAnchorAsync()).Success)
            {
                // Remember UUID so you can load the anchor later
                _anchorUuids.Add(anchor.Uuid);
            }
            else
            {
                Debug.LogError("Implement failure handling for anchor save.");
            }
        }

        private async void SaveAnchorIdsToFile()
        {
            await SaveAnchorGUIDsAsync();
            Debug.Log($"Saved current saved anchor IDs to file. Now tracking {_anchorUuids.Count} anchor guids");
            
            // When anchors are saved, exit the anchor creation state
            EndState();
        }
        
        #endregion
        
        #region UUID Storage Operations
        
        /// <summary>
        /// Save list of anchor UUID to persistent storage as JSON.
        /// </summary>
        private async Awaitable SaveAnchorGUIDsAsync()
        {
            var jsonString = JsonConvert.SerializeObject(_anchorUuids, Formatting.Indented);
            await File.WriteAllTextAsync(m_FilePath, jsonString);
        }

        #endregion
    }
}