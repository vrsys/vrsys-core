using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Editor
{
    public class VRSYSAddSubPackages : MonoBehaviour
    {
        
        #region Chat Odin Package

        private static AddRequest OdinRequest;
        private static string chatOdinPackageUrl = "https://github.com/vrsys/vrsys-core.git?path=/Packages/com.vrsys.chat-odin#v1.0.0";

        [MenuItem("VRSYS/Add sub packages/Chat Odin")]
        public static void AddChatOdin()
        {
            OdinRequest = Client.Add(chatOdinPackageUrl);
            EditorApplication.update += AddOdinProgress;
        }

        private static void AddOdinProgress()
        {
            if (OdinRequest.IsCompleted)
            {
                if(OdinRequest.Status == StatusCode.Success)
                    ExtendedLogger.LogInfo($"{typeof(VRSYSAddSubPackages)}", $"Installed {OdinRequest.Result.packageId}");
                else
                {
                    ExtendedLogger.LogError($"{typeof(VRSYSAddSubPackages)}", OdinRequest.Error.message);
                }

                EditorApplication.update -= AddOdinProgress;
            }
        }

        #endregion

        #region Meta Integrations Package

        private static AddRequest MetaRequest;
        private static string metaIntegrationsPackageUrl = "https://github.com/vrsys/vrsys-core.git?path=/Packages/com.vrsys.meta-integrations#v1.0.0";
        
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
                    ExtendedLogger.LogInfo($"{typeof(VRSYSAddSubPackages)}", $"Installed {MetaRequest.Result.packageId}");
                else
                {
                    ExtendedLogger.LogError($"{typeof(VRSYSAddSubPackages)}", MetaRequest.Error.message);
                }

                EditorApplication.update -= AddMetaProgress;
            }
        }

        #endregion
    }
}
