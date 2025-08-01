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
//   Authors:        Tony Zoeppig
//   Date:           2025
//-----------------------------------------------------------------

using System.Reflection;
using Oculus.Avatar2;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Meta.Avatars
{
    public class MetaAvatarInputManager : OvrAvatarInputManager
    {
        #region Member Variables

        [SerializeField] private OVRCameraRig _ovrCameraRig;

        #endregion

        #region MonoBehaviour Callbacks

        private void Awake()
        {
            if (_ovrCameraRig == null)
                _ovrCameraRig = GetComponentInParent<OVRCameraRig>();
        }

        #endregion

        #region Input Manager Callbacks

        protected override void OnTrackingInitialized()
        {
            if (_ovrCameraRig == null)
            {
                ExtendedLogger.LogError(GetType().Name, "No OVRCameraRig configured.", this);
                return;
            }

            OvrPluginInvoke("StartFaceTracking");
            OvrPluginInvoke("StartEyeTracking");

            IOvrAvatarInputTrackingDelegate inputTrackingDelegate = new VRSYSMetaInputTrackingDelegate(_ovrCameraRig);
            var inputControlDelegate = new VRSYSMetaInputControlDelegate();

            _inputTrackingProvider = new OvrAvatarInputTrackingDelegatedProvider(inputTrackingDelegate);
            _inputControlProvider = new OvrAvatarInputControlDelegatedProvider(inputControlDelegate);
        }

        #endregion

        #region Custom Methods

        private static void OvrPluginInvoke(string method, params object[] args)
        {
            typeof(OVRPlugin).GetMethod(method, BindingFlags.Public | BindingFlags.Static)?.Invoke(null, args);
        }

        #endregion
    }
}
