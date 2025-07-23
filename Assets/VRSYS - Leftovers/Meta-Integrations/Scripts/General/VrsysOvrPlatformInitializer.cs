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
using Oculus.Platform;
using UnityEngine;
using UnityEngine.Events;
using VRSYS.Core.Logging;

namespace VRSYS.Meta.General
{
    public class VrsysOvrPlatformInitializer : MonoBehaviour
    {
        #region Singleton

        public static VrsysOvrPlatformInitializer Instance;

        #endregion

        #region Member Variables

        [HideInInspector] public bool Initialized = false;
        [HideInInspector] public ulong LocalUserId = 0;

        #endregion

        #region Events

        public UnityEvent<ulong> OnLocalUserIdRetrieved = new UnityEvent<ulong>();

        #endregion

        #region MonoBehaviour Callbacks

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            StartCoroutine(Initialize());
        }

        #endregion

        #region Coroutines

        private IEnumerator Initialize()
        {
            if(OvrPlatformInit.status == OvrPlatformInitStatus.NotStarted)
                OvrPlatformInit.InitializeOvrPlatform();

            while (OvrPlatformInit.status != OvrPlatformInitStatus.Succeeded)
            {
                if (OvrPlatformInit.status == OvrPlatformInitStatus.Failed)
                {
                    ExtendedLogger.LogError(GetType().Name, "Error initializing OvrPlatform.", this);
                    yield break;
                }

                yield return null;
            }

            bool getUserIdComplete = false;

            Users.GetLoggedInUser().OnComplete(message =>
            {
                if (!message.IsError)
                {
                    LocalUserId = message.Data.ID;
                    OnLocalUserIdRetrieved.Invoke(LocalUserId);

                    Initialized = true;
                }
                else
                {
                    ExtendedLogger.LogError(GetType().Name, $"Error loading user ID: {message.GetError().Message}", this);
                }

                getUserIdComplete = true;
            });

            while (!getUserIdComplete)
                yield return null;
        }

        #endregion
    }
}
