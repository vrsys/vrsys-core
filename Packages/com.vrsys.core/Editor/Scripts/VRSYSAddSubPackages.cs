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

#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Editor
{
    public class VRSYSAddSubPackages : MonoBehaviour
    {
        private static string _logTag = $"{typeof(VRSYSAddSubPackages)}";
        
        #region Chat Odin Package

        private static ListRequest OdinListRequest;

        private static AddRequest OdinRequest;
        private static string odinPackageUrl = "https://github.com/4Players/odin-sdk-unity.git";

        private static AddRequest VRSYSOdinRequest;
        private static string chatOdinPackageUrl = "https://github.com/vrsys/vrsys-core.git?path=/Packages/com.vrsys.chat-odin";

        [MenuItem("VRSYS/Add sub packages/Chat Odin")]
        public static void CheckOdinPackages()
        {
            OdinListRequest = Client.List();
            EditorApplication.update += ListOdinProgress;
        }

        private static void ListOdinProgress()
        {
            if (OdinListRequest.IsCompleted)
            {
                if (OdinListRequest.Status == StatusCode.Success)
                {
                    bool odinIncluded = false;
                    bool chatOdinIncluded = false;
                    
                    foreach (var package in OdinListRequest.Result)
                    {
                        if (package.packageId.Contains("io.fourplayers.odin"))
                            odinIncluded = true;
                        if (package.packageId.Contains("com.vrsys.chat-odin"))
                            chatOdinIncluded = true;
                    }

                    if (!odinIncluded)
                    {
                        AddOdin();
                    }
                    else if (!chatOdinIncluded)
                        AddChatOdin();
                    else
                    {
                        ExtendedLogger.LogInfo(_logTag, "All packages already included.");
                    }

                    EditorApplication.update -= ListOdinProgress;
                }
            }
        }

        private static void AddOdin()
        {
            OdinRequest = Client.Add(odinPackageUrl);
            EditorApplication.update += AddOdinProgress;
        }

        private static void AddOdinProgress()
        {
            if (OdinRequest.IsCompleted)
            {
                if (OdinRequest.Status == StatusCode.Success)
                {
                    ExtendedLogger.LogInfo(_logTag, $"Installed {OdinRequest.Result.packageId}");
                    AddChatOdin();
                }
                else
                {
                    ExtendedLogger.LogError(_logTag, OdinRequest.Error.message);
                }

                EditorApplication.update -= AddOdinProgress;
            }
        }

        private static void AddChatOdin()
        {
            VRSYSOdinRequest = Client.Add(chatOdinPackageUrl);
            EditorApplication.update += AddChatOdinProgress;
        }
        
        private static void AddChatOdinProgress()
        {
            if (VRSYSOdinRequest.IsCompleted)
            {
                if (VRSYSOdinRequest.Status == StatusCode.Success)
                {
                    ExtendedLogger.LogInfo(_logTag, $"Installed {VRSYSOdinRequest.Result.packageId}");
                    AddChatOdin();
                }
                else
                {
                    ExtendedLogger.LogError(_logTag, VRSYSOdinRequest.Error.message);
                }

                EditorApplication.update -= AddChatOdinProgress;
            }
        }

        #endregion

        #region Meta Integrations Package

        private static AddRequest MetaRequest;
        private static string metaIntegrationsPackageUrl = "https://github.com/vrsys/vrsys-core.git?path=/Packages/com.vrsys.meta-integrations";
        
        [MenuItem("VRSYS/Add sub packages/Meta Integrations")]
        public static void AddMetaIntegrations()
        {
            MetaRequest = Client.Add(metaIntegrationsPackageUrl);
            EditorApplication.update += AddMetaProgress;
        }
        
        private static void AddMetaProgress()
        {
            if (MetaRequest.IsCompleted)
            {
                if(MetaRequest.Status == StatusCode.Success)
                    ExtendedLogger.LogInfo(_logTag, $"Installed {MetaRequest.Result.packageId}");
                else
                {
                    ExtendedLogger.LogError(_logTag, MetaRequest.Error.message);
                }

                EditorApplication.update -= AddMetaProgress;
            }
        }

        #endregion
    }
}

#endif
