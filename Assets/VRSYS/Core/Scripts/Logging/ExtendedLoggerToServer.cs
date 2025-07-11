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

        [SerializeField] private LogLevel _logLevel;

        private string logTag
        {
            get
            {
                if (NetworkUser.LocalInstance != null)
                    return "[" + NetworkUser.LocalInstance.userName.Value + "]";

                return "[Client" + NetworkManager.LocalClientId + "]";
            }
        }

        #endregion

        #region Mono- & NetworkBehaviour Callbacks

        public override void OnNetworkSpawn()
        {
            ExtendedLogger.OnInfoLog.AddListener(LogInfo);
            ExtendedLogger.OnWarningLog.AddListener(LogWarning);
            ExtendedLogger.OnErrorLog.AddListener(LogError);
        }

        public override void OnNetworkDespawn()
        {
            ExtendedLogger.OnInfoLog.RemoveListener(LogInfo);
            ExtendedLogger.OnWarningLog.RemoveListener(LogWarning);
            ExtendedLogger.OnErrorLog.RemoveListener(LogError);
        }

        #endregion

        #region Private Methods

        private void LogInfo(ExtendedLoggerLogInformation logInfo)
        {
            if (_logLevel < LogLevel.Warning)
            {
                string log = logTag + logInfo.FormattedMessage;
                LogInfoRpc(log);
            }
        }

        private void LogWarning(ExtendedLoggerLogInformation logInfo)
        {
            if (_logLevel < LogLevel.Error)
            {
                string log = logTag + logInfo.FormattedMessage;
                LogWarningRpc(log);
            }
        }

        private void LogError(ExtendedLoggerLogInformation logInfo)
        {
            if (_logLevel < LogLevel.None)
            {
                string log = logTag + logInfo.FormattedMessage;
                LogErrorRpc(log);
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
