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

using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using VRSYS.Core.Networking;

namespace VRSYS.Core.Logging
{
    public class ServerLogInformation
    {
        public ulong user_id;
        public string app_name;
        public string user_name;
        public string device_id;
        public string status;
        public LogLevel logLevel;

        public ServerLogInformation(string appName, string status, ulong userID, string userName, string deviceId, LogLevel logLevel)
        {
            app_name = appName;
            this.status = status;
            user_id = userID;
            user_name = userName;
            device_id = deviceId;
            this.logLevel = logLevel;
        }
    }
    
    public class ExtendedLoggerToWeb : MonoBehaviour
    {
        #region Member Variables

        private static ulong _userId = 0;

        [Tooltip("Server URL logs get send to. Use https.")]
        public string serverUrl = "https://domain/api/app_usage";

        public LogLevel logLevel = LogLevel.Info;

        #endregion

        #region MonoBehaviour Callbacks

        private void Awake()
        {
            ExtendedLogger.OnInfoLog.AddListener(LogInfo);
            ExtendedLogger.OnWarningLog.AddListener(LogWarning);
            ExtendedLogger.OnErrorLog.AddListener(LogError);
        }

        private void OnDestroy()
        {
            ExtendedLogger.OnInfoLog.RemoveListener(LogInfo);
            ExtendedLogger.OnWarningLog.RemoveListener(LogWarning);
            ExtendedLogger.OnErrorLog.RemoveListener(LogError);
        }

        #endregion

        #region Static Methods

        public void SetUserId(ulong userId) => _userId = userId;

        #endregion

        #region LOg Methods

        private void LogInfo(ExtendedLoggerLogInformation logInfo)
        {
            if (logLevel < LogLevel.Warning)
            {
                ServerLogInformation serverLog = new ServerLogInformation(Application.productName, logInfo.ClearMessage, _userId,
                    NetworkUser.LocalInstance.userName.Value.ToString(), SystemInfo.deviceUniqueIdentifier, LogLevel.Info);

                StartCoroutine(LogToServer(serverLog));
            }
        }
        
        private void LogWarning(ExtendedLoggerLogInformation logInfo)
        {
            if (logLevel < LogLevel.Error)
            {
                ServerLogInformation serverLog = new ServerLogInformation(Application.productName, logInfo.ClearMessage, _userId,
                    NetworkUser.LocalInstance.userName.Value.ToString(), SystemInfo.deviceUniqueIdentifier, LogLevel.Warning);

                StartCoroutine(LogToServer(serverLog));
            }
        }
        
        private void LogError(ExtendedLoggerLogInformation logInfo)
        {
            if (logLevel < LogLevel.None)
            {
                ServerLogInformation serverLog = new ServerLogInformation(Application.productName, logInfo.ClearMessage, _userId,
                    NetworkUser.LocalInstance.userName.Value.ToString(), SystemInfo.deviceUniqueIdentifier, LogLevel.Error);

                StartCoroutine(LogToServer(serverLog));
            }
        }

        #endregion

        #region Coroutines

        private IEnumerator LogToServer(ServerLogInformation serverLogInfo)
        {
            string json = JsonUtility.ToJson(serverLogInfo);

            Debug.Log("Logging information to server: " + json);
            using (UnityWebRequest www = new UnityWebRequest(serverUrl, UnityWebRequest.kHttpVerbPOST))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("App start logged successfully.");
                }
                else
                {
                    Debug.LogError($"Failed to log app start: {www.error}");
                }
            }
        }

        #endregion
    }
}
