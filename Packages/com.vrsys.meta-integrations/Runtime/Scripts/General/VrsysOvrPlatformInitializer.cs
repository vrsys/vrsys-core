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

using System;
using System.Collections;
using Oculus.Avatar2;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;
using UnityEngine.Events;
using VRSYS.Core.Logging;

namespace VRSYS.Meta.General
{
    public class VrsysOvrPlatformInitializer : MonoBehaviour
    {
        #region Enum

        public enum OvrPlatformInitStatus
        {
            NotStarted = 0,
            Initializing,
            Succeeded,
            Failed
        }

        #endregion
        
        #region Singleton

        public static VrsysOvrPlatformInitializer Instance;

        #endregion

        #region Member Variables

        public static OvrPlatformInitStatus status { get; private set; } = OvrPlatformInitStatus.NotStarted;
        
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
            StartCoroutine(InitializeOvrUser());
        }

        #endregion

        #region Static Methods

        public static void InitializeOvrPlatform()
        {
            if (status == OvrPlatformInitStatus.Succeeded)
            {
                ExtendedLogger.LogWarning($"{typeof(VrsysOvrPlatformInitializer)}", "OvrPlatform is already initalized.");
                return;
            }

            try
            {
                status = OvrPlatformInitStatus.Initializing;
                Oculus.Platform.Core.AsyncInitialize().OnComplete(InitializeComplete);

                void InitializeComplete(Message<PlatformInitialize> msg)
                {
                    if (msg.Data.Result != PlatformInitializeResult.Success)
                    {
                        status = OvrPlatformInitStatus.Failed;
                        ExtendedLogger.LogError($"{typeof(VrsysOvrPlatformInitializer)}",
                            "Failed to initialize OvrPlatform");
                    }
                    else
                    {
                        Entitlements.IsUserEntitledToApplication().OnComplete(CheckEntitlement);
                    }
                }

                void CheckEntitlement(Message msg)
                {
                    if (msg.IsError == false)
                    {
                        Users.GetAccessToken().OnComplete(GetAccessTokenComplete);
                    }
                    else
                    {
                        status = OvrPlatformInitStatus.Failed;
                        var e = msg.GetError();
                        ExtendedLogger.LogError($"{typeof(VrsysOvrPlatformInitializer)}",
                            $"Failed entitlement check: {e.Code} - {e.Message}");
                    }
                }

                void GetAccessTokenComplete(Message<string> msg)
                {
                    if (String.IsNullOrEmpty(msg.Data))
                    {
                        string output = "Token is null or empty.";
                        if (msg.IsError)
                        {
                            var e = msg.GetError();
                            output = $"{e.Code} - {e.Message}";
                        }

                        status = OvrPlatformInitStatus.Failed;
                        ExtendedLogger.LogError($"{typeof(VrsysOvrPlatformInitializer)}",
                            $"Failed to retrieve access toke: {output}");
                    }
                    else
                    {
                        ExtendedLogger.LogInfo($"{typeof(VrsysOvrPlatformInitializer)}",
                            "Successfully retrieved access token.");
                        OvrAvatarEntitlement.SetAccessToken(msg.Data);
                        status = OvrPlatformInitStatus.Succeeded;
                    }
                }
            }
            catch (Exception e)
            {
                status = OvrPlatformInitStatus.Failed;
                ExtendedLogger.LogError($"{typeof(VrsysOvrPlatformInitializer)}", $"{e.Message}\n{e.StackTrace}");
            }
        }

        public static void ResetOvrPlatformInitState() => status = OvrPlatformInitStatus.NotStarted;

        #endregion

        #region Coroutines

        private IEnumerator InitializeOvrUser()
        {
            if(status == OvrPlatformInitStatus.NotStarted)
                InitializeOvrPlatform();

            while (status != OvrPlatformInitStatus.Succeeded)
            {
                if (status == OvrPlatformInitStatus.Failed)
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
