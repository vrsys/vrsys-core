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
//   Authors:        Tony Jan Zoeppig, Sebastian Muehlhaus
//   Date:           2025
//-----------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using VRSYS.Core.Logging;
using VRSYS.Core.Networking;

namespace VRSYS.Meta.Collocation
{
    public class CollocationManager : MonoBehaviour, INetworkUserCallbacks
    {
        #region Member Variables

        [FormerlySerializedAs("_userCollocationDiscovery")]
        [Header("Collocation Configuration")]
        
        [Tooltip("If set to true, Metas collocation discovery sessions will be used to share spatial anchors. Should be used if multiple collocated setups join same network session")]
        [SerializeField] private bool _useCollocationDiscovery = true;

        [Tooltip("User role responsible for hosting collocation session and creating spatial anchor.")]
        [SerializeField] [UserRoleSelector] private UserRole _hostRole;

        [Tooltip("User roles that use collocation")] 
        [SerializeField] [UserRoleSelector] private List<UserRole> _collcoationUserRoles;

        [Tooltip("Time that users with host role wait initially to discover potentially existing collocation sessions before starting their own one.")]
        [SerializeField] private float _discoveryTime = 10f;

        private bool _sessionDiscovered;
        private bool _isAdvertising;
        private Guid _currentSessionUUID;

        [Header("Shared Spatial Anchors Configuration")]
        
        [Tooltip("Prefab instantiated for spatial anchor.")]
        [SerializeField] private OVRSpatialAnchor _anchorPrefab;

        [Tooltip("Time to wait before retrying to load and align anchor in case of failure")] 
        [SerializeField] private float _retryTime = 5f;
        
        // static events
        public static UnityEvent<Guid> CollocationSessionStarted = new UnityEvent<Guid>();
        public static UnityEvent<Guid> CollocationSessionDiscovered = new UnityEvent<Guid>();

        [Header("Debug")] 
        
        [Tooltip("If true, warning and info logs will be printed to console. If false, only error logs will be logged.")]
        [SerializeField] private bool _verbose = false;

        #endregion

        #region MonoBehaviour Callbacks

        private void OnDestroy()
        {
            if (_isAdvertising)
                OVRColocationSession.StopAdvertisementAsync();
        }

        #endregion

        #region INetworkUserCallbacks

        public void OnLocalNetworkUserSetup()
        {
            if (_collcoationUserRoles.Contains(NetworkUser.LocalInstance.userRole.Value))
                InitializeCollocation();
        }

        public void OnRemoteNetworkUserSetup(NetworkUser user)
        {
            // ...
        }

        #endregion

        #region Private Methods

        private async void InitializeCollocation()
        {
            OVRColocationSession.ColocationSessionDiscovered += OnCollocationSessionDiscovered; // assign callback to session discovered event
            var discoveryResult = await OVRColocationSession.StartDiscoveryAsync(); // start actual discovery

            if (discoveryResult.Status == OVRColocationSession.Result.Failure)
            {
                ExtendedLogger.LogError(GetType().Name, $"Failed to start collocation session discovery with status: {discoveryResult.Status}", this);
                return;
            }
            
            if(_verbose)
                ExtendedLogger.LogInfo(GetType().Name, $"Session discovery started. Status: {discoveryResult.Status}", this);
            
            if(NetworkUser.LocalInstance.userRole.Value == _hostRole) // if host role --> search for existing session, start own session if none found in given discoveryTime
                StartCoroutine(WaitForDiscoveryResult());
        }

        private void OnCollocationSessionDiscovered(OVRColocationSession.Data session)
        {
            OVRColocationSession.StopDiscoveryAsync(); // stop discovering collocation session
            OVRColocationSession.ColocationSessionDiscovered -= OnCollocationSessionDiscovered; // deregister event

            _currentSessionUUID = session.AdvertisementUuid; // get uuid of discovered session
            CollocationSessionDiscovered.Invoke(_currentSessionUUID); // invoke event with uuid of discovered session

            _sessionDiscovered = true;
            
            if(_verbose)
                ExtendedLogger.LogInfo(GetType().Name, $"Discovered collocation session with UUID {_currentSessionUUID}", this);

            LoadAndAlignToAnchor();
        }

        private async void LoadAndAlignToAnchor()
        {
            try
            {
                if(_verbose)
                    ExtendedLogger.LogInfo(GetType().Name, $"Loading anchors for group UUID {_currentSessionUUID}", this);

                // Load anchors shared in discovered collocation session
                var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
                var result = await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(_currentSessionUUID, unboundAnchors);

                // retry loading anchors if load process failed or no anchors have been shared so far
                if (!result.Success || unboundAnchors.Count == 0)
                {
                    ExtendedLogger.LogError(GetType().Name, $"Failed to load anchors. Loading success: {result.Success}, Anchor Count: {unboundAnchors.Count}", this);
                    
                    if(_verbose)
                        ExtendedLogger.LogInfo(GetType().Name, $"Restarting loading anchors in {_retryTime} seconds.", this);
                    
                    Invoke(nameof(LoadAndAlignToAnchor), _retryTime);
                }
                
                // if anchors could be loaded, first anchor in array is considered as alignment anchor --> trigger localization of anchor
                if (await unboundAnchors[0].LocalizeAsync())
                {
                    if(_verbose)
                        ExtendedLogger.LogInfo(GetType().Name, $"Anchor localized successfully. Anchor UUID: {unboundAnchors[0].Uuid}", this);
                    
                    // Instantiate spatial anchor in scne
                    var spatialAnchor = Instantiate(_anchorPrefab);
                    // Bind localized anchor to instantiated anchor in scene
                    unboundAnchors[0].BindTo(spatialAnchor);
                    
                    // Trigger alignment of user to anchor
                    AlignmentManager.AlignUserToAnchor(spatialAnchor);
                    return;
                }
                
                ExtendedLogger.LogError(GetType().Name, $"Failed to localize anchor. UUID: {unboundAnchors[0].Uuid}", this);
            }
            catch (Exception e)
            {
                ExtendedLogger.LogError(GetType().Name, $"Error during anchor loading and alignment: {e.Message}", this);
            }
        }

        private async void StartCollocationSession()
        {
            // start session advertisement
            byte[] advertisementData = Encoding.UTF8.GetBytes("SharedSpatialAnchorsSession");
            var startAdvertisementResult = await OVRColocationSession.StartAdvertisementAsync(advertisementData);

            if (startAdvertisementResult.Success)
            {
                if(_verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Successfully started collocation session advertisement.", this);

                _isAdvertising = true; // mark local user as advertising
                _currentSessionUUID = startAdvertisementResult.Value; // get uuid of started session
                
                CollocationSessionStarted.Invoke(_currentSessionUUID);
                
                if(_verbose)
                    ExtendedLogger.LogInfo(GetType().Name, $"Collocation session UUID: {_currentSessionUUID}", this);
                
                // Create alignment anchor
                CreateAndShareAlignmentAnchor();
            }
        }

        private async void CreateAndShareAlignmentAnchor()
        {
            try
            {
                if (_verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Creating alignment anchor...", this);
                
                // create anchor at world root
                var anchor = await CreateAnchor(Vector3.zero, Quaternion.identity);
                
                if (anchor == null)
                {
                    ExtendedLogger.LogError(GetType().Name, "Failed to create alignment anchor.", this);
                    return;
                }

                if (!anchor.Localized)
                {
                    ExtendedLogger.LogError(GetType().Name,
                        "Alignment anchor is not localized. Cannot proceed with sharing.", this);
                    return;
                }

                // save anchor to meta cloud
                var saveResult = await anchor.SaveAnchorAsync();

                if (!saveResult.Success)
                {
                    ExtendedLogger.LogError(GetType().Name,
                        $"Failed to save alignment anchor. Error: {saveResult.Status}", this);
                    return;
                }

                if (_verbose)
                    ExtendedLogger.LogInfo(GetType().Name, $"Aligment anchor saved successfully. UUID: {anchor.Uuid}", this);

                // share anchor in collcoation session
                var shareResult = await OVRSpatialAnchor.ShareAsync(new List<OVRSpatialAnchor> { anchor }, _currentSessionUUID);

                if (!shareResult.Success)
                {
                    ExtendedLogger.LogError(GetType().Name, $"Failed to share alignment anchor. Error: {shareResult.Status}", this);
                    return;
                }

                if (_verbose)
                    ExtendedLogger.LogInfo(GetType().Name, $"Alignment anchor shared successfully. Group UUID: {_currentSessionUUID}", this);
            }
            catch (Exception e)
            {
                ExtendedLogger.LogError(GetType().Name, $"Error during anchor creation and sharing: {e.Message}", this);
            }
        }

        private async Task<OVRSpatialAnchor> CreateAnchor(Vector3 position, Quaternion rotation)
        {
            try
            {
                // create anchor at given position and rotation
                var anchor = Instantiate(_anchorPrefab, position, rotation);

                // wait for anchor to initialize
                while (!anchor.Created)
                {
                    await Task.Yield();
                }

                if (_verbose)
                    ExtendedLogger.LogInfo(GetType().Name, $"Anchor created successfully. UUID: {anchor.Uuid}", this);

                return anchor;
            }
            catch (Exception e)
            {
                ExtendedLogger.LogError(GetType().Name, $"Error during anchor creation: {e.Message}", this);
                return null;
            }
        }

        #endregion

        #region Coroutines

        private IEnumerator WaitForDiscoveryResult()
        {
            // wait if session is found
            yield return new WaitForSeconds(_discoveryTime);
            
            // if no session was discovered, start own
            if (!_sessionDiscovered)
            {
                if(_verbose)
                    ExtendedLogger.LogInfo(GetType().Name, $"No collocation session found in given discovery time of {_discoveryTime} seconds.", this);
                
                // stop session discovery and deregister success callback
                OVRColocationSession.StartDiscoveryAsync();
                OVRColocationSession.ColocationSessionDiscovered -= OnCollocationSessionDiscovered;
                
                // start own session
                StartCollocationSession();
            }
        }

        #endregion
    }
}
