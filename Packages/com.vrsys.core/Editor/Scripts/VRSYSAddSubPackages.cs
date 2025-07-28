using System.Linq;
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
