// VRSYS plugin of Virtual Reality and Visualization Research Group (Bauhaus University Weimar)
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
// Copyright (c) 2022 Virtual Reality and Visualization Research Group
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
//   Authors:        Sebastian Muehlhaus, Andre Kunert, Tony Jan Zoeppig
//   Date:           2023
//-----------------------------------------------------------------

using Unity.Netcode;
using UnityEngine;
using VRSYS.Core.Avatar;
using VRSYS.Core.Logging;
using VRSYS.Core.Networking;
using VRSYS.Core.ScriptableObjects;
using VRSYS.Core.Utility;

namespace VRSYS.Core.Projection
{
    public class ProjectionWallRuntimeSetup : MonoBehaviour, INetworkUserCallbacks
    {
        public ProjectionWallSettings settings;
        public OffAxisProjection leftCamera;
        public OffAxisProjection rightCamera;
        public ProjectionScreen screen;
        public bool hideRemoteUsersWithSameConfig = true;

        [Header("Debug")]
        public bool verbose = false;

        private ProjectionWallSettings.Config config => settings.config;
        private ProjectionWallSettings.StereoUserSettings user;

        public bool isProjectionMaster => user.masterFlag;

        private Vector4 windowSettings => config.multiUserSettings.windowSettingsVector4;
        private Vector4 windowSettingsCropped => config.multiUserSettings.windowSettingsCroppedVector4;
        private float viewportHorizontalOffset => windowSettingsCropped.z / (int)windowSettings.x;
        private float viewportWidth => windowSettingsCropped.x / (int)windowSettings.x;
        private bool isInitialized = false;

        private int _isOwner = -1;
        private bool isOwner
        {
            get
            {
                if(_isOwner == -1)
                {
                    var no = GetComponent<NetworkObject>();
                    _isOwner = no == null || no.IsOwner ? 1 : 0;
                }
                return _isOwner == 1;
            }
        }

        private void Awake()
        {
            ParseDeviceConfiguration();

            SetDTrackPort();

        }
        /*private void Start()
        {
            // Custom fixed update for projection calculation, set a 120Hz
            //InvokeRepeating("MyFixedUpdate", 0, (1f / 120f));
        }*/

        private void LateUpdate()
        {
            if(isInitialized && isOwner)
                CalculateProjection();
        }

        public void Initialize()
        {
            if (!isOwner)
                return;

            ApplyUserConfiguration();
            ApplyScreenConfiguration();
            ApplyCameraConfiguration();

            isInitialized = true;
        }

        private void SetDTrackPort()
        {
            var dTrack = (DTrack.DTrack)FindFirstObjectByType(typeof(DTrack.DTrack));

            dTrack.listenPort = config.multiUserSettings.dTrackPort;
        }

        private void ApplyUserConfiguration()
        {
            NetworkUser.LocalInstance.SetUserName(user.username);
            
            var head = GetComponent<AvatarAnatomy>().head;
            var dtrackReceiver = head.GetComponent<DTrack.DTrackReceiver6Dof>();

            if (user.headtrackingFlag)
            {
                dtrackReceiver.enabled = true;
                dtrackReceiver.bodyId = user.trackingID;
                SetEyeDistance(user.eyeDistance);
            }
            else
            {
                head.transform.localPosition = user.fixedHeadPosVector3;
                head.transform.localRotation = Quaternion.identity;

                dtrackReceiver.enabled = false;
                SetEyeDistance(0f);
            }

            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, "[Camera] Headtracking: " + user.headtrackingFlag);
        }

        private void ApplyCameraConfiguration()
        {
            SetViewportOnCameras();
            SetEyeDistance(user.eyeDistance);
        }

        private void ApplyScreenConfiguration()
        {
            screen.transform.localPosition = config.multiUserSettings.screenPosVector3;
            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, "Screen dimensions [" + config.multiUserSettings.screenWidth + "," + config.multiUserSettings.screenHeight + "]");
            screen.width = config.multiUserSettings.screenWidth;
            screen.height = config.multiUserSettings.screenHeight;
        }

        private void ParseDeviceConfiguration()
        {
            if (!settings.hasRead)
                settings.Read();
            user = settings.localUserSettings;
        }

        private void CalculateProjection()
        {
            leftCamera.CalculateProjection();
            rightCamera.CalculateProjection();
        }

        private void SetViewportOnCameras()
        {            
            leftCamera.camera.rect = new Rect(viewportHorizontalOffset * 0.5f, 0f, viewportWidth * 0.5f, 1f);
            rightCamera.camera.rect = new Rect(0.5f + viewportHorizontalOffset * 0.5f, 0f, viewportWidth * 0.5f, 1f);            
        }

        private void SetEyeDistance(float eyeDist)
        {                        
            leftCamera.transform.localPosition = new Vector3(-eyeDist * 0.5f, 0f, 0f);
            rightCamera.transform.localPosition = new Vector3(eyeDist * 0.5f, 0f, 0f);
        }

        public void OnLocalNetworkUserSetup()
        {
            Initialize();
        }

        public void OnRemoteNetworkUserSetup(NetworkUser user)
        {
            if (!hideRemoteUsersWithSameConfig)
                return;

            var setup = user.GetComponent<ProjectionWallRuntimeSetup>();
            if (setup == null)
                return;
            if(setup.settings == settings)
            {
                var layerInit = user.GetComponent<LocalLayerInitializer>();
                layerInit.Apply();
            }
        }
    }
}
