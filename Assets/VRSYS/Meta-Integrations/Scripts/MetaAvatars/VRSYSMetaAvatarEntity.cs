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

using System.Collections;
using Oculus.Avatar2;
using UnityEngine;
using VRSYS.Core.Logging;
using VRSYS.Meta.General;

namespace VRSYS.Meta.Avatars
{
    public class VRSYSMetaAvatarEntity : OvrAvatarEntity
    {
        #region Member Variables

        [Header("VRSYS Avatar Entity")] 
        public bool autoLoadOnStart = false;

        [Header("VRSYS Debug")] 
        public bool verbose = false; 

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            if (IsLocal)
            {
                if(!VrsysOvrPlatformInitializer.Instance.Initialized)
                    VrsysOvrPlatformInitializer.Instance.OnLocalUserIdRetrieved.AddListener(OnLocalUserIdRetrieved);
                else if (autoLoadOnStart)
                {
                    _userId = VrsysOvrPlatformInitializer.Instance.LocalUserId;
                    StartCoroutine(LoadAvatar());
                }
            }
        }

        #endregion

        #region Public Methods

        public void LoadLocalAvatar()
        {
            if(verbose)
                ExtendedLogger.LogInfo(GetType().Name, "Starting loading lcoal avatar...", this);

            _userId = VrsysOvrPlatformInitializer.Instance.LocalUserId;
            StartCoroutine(LoadAvatar());
        }

        public void LoadAvatarByCdn(ulong userId)
        {
            if(verbose)
                ExtendedLogger.LogInfo(GetType().Name, $"Triggered loading avatar for cdn: {_userId}", this);

            _userId = userId;
            StartCoroutine(LoadAvatar());
        }

        #endregion

        #region Private Methods

        private IEnumerator LoadAvatar()
        {
            while (OvrPlatformInit.status != OvrPlatformInitStatus.Succeeded)
            {
                if (OvrPlatformInit.status == OvrPlatformInitStatus.Failed)
                {
                    ExtendedLogger.LogError(GetType().Name, "Error initializing OvrPlatform.", this);
                    yield break;
                }

                yield return null;
            }
            
            if(verbose)
                ExtendedLogger.LogInfo(GetType().Name, $"Loading avatar for cdn: {_userId}", this);
            
            LoadUser();
        }

        #endregion

        #region Event Callbacks

        private void OnLocalUserIdRetrieved(ulong userID)
        {
            _userId = userID;
            
            if (autoLoadOnStart)
                StartCoroutine(LoadAvatar());
        }

        #endregion
    }
}
