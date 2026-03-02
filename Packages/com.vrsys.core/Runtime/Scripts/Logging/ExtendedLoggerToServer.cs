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
//   Authors:        Tony Jan Zoeppig
//   Date:           2025
//-----------------------------------------------------------------

using Unity.Netcode;
using UnityEngine;
using VRSYS.Core.Networking;

namespace VRSYS.Core.Logging
{
    public class ExtendedLoggerToServer : NetworkBehaviour
    {
        #region Member Variables

        [SerializeField] private LogLevel _logLevel = LogLevel.Info;
        [SerializeField] private bool _printAllLogs = false;

        private string _logTag
        {
            get
            {
                if (NetworkUser.LocalInstance != null)
                    return $"<color=white>[<color=purple>{NetworkUser.LocalInstance.userName.Value}</color>]</color>";

                return $"<color=white>[<color=purple>Client {NetworkManager.LocalClientId}</color>]</color>";
            }
        }
        
        #endregion

        #region Mono- & NetworkBehaviour Callbacks

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                return;
            
            ExtendedLogger.OnInfoLog.AddListener(LogInfo);
            ExtendedLogger.OnWarningLog.AddListener(LogWarning);
            ExtendedLogger.OnErrorLog.AddListener(LogError);

            if(_printAllLogs)
                Application.logMessageReceived += OnUnityLogReceived;
        }

        public override void OnNetworkDespawn()
        {
            if(IsServer)
                return;
            
            ExtendedLogger.OnInfoLog.RemoveListener(LogInfo);
            ExtendedLogger.OnWarningLog.RemoveListener(LogWarning);
            ExtendedLogger.OnErrorLog.RemoveListener(LogError);
        }

        #endregion

        #region Private Methods

        private void LogInfo(ExtendedLoggerLogInformation logInfo) => LogInfo(logInfo.FormattedMessage);

        private void LogInfo(string message)
        {
            if (_logLevel < LogLevel.Warning)
            {
                string log = _logTag + message;
                LogInfoRpc(log);
            }
        }

        private void LogWarning(ExtendedLoggerLogInformation logInfo) => LogWarning(logInfo.FormattedMessage);

        private void LogWarning(string message)
        {
            if (_logLevel < LogLevel.Error)
            {
                string log = _logTag + message;
                LogWarningRpc(log);
            }
        }

        private void LogError(ExtendedLoggerLogInformation logInfo) => LogError(logInfo.FormattedMessage);

        private void LogError(string message)
        {
            if (_logLevel < LogLevel.None)
            {
                string log = _logTag + message;
                
                LogErrorRpc(log);
            }
        }
        
        private void OnUnityLogReceived(string condition, string stacktrace, LogType type)
        {
            switch (type)
            {
                case LogType.Log:
                    LogInfo(condition);
                    break;
                case LogType.Warning:
                    LogWarning(condition);
                    break;
                case LogType.Error:
                    LogError(condition + "\n" + stacktrace);
                    break;
                case LogType.Exception:
                    LogError(condition + "\n" + stacktrace);
                    break;
                default:
                    LogInfo(condition + "\n" + stacktrace);
                    break;
            }
        }
        
        #endregion

        #region RPCs

        [Rpc(SendTo.Server)]
        private void LogInfoRpc(string message) => Debug.Log(message);

        [Rpc(SendTo.Server)]
        private void LogWarningRpc(string message) => Debug.LogWarning(message);

        [Rpc(SendTo.Server)]
        private void LogErrorRpc(string message) => Debug.LogError(message);

        #endregion
    }
}
