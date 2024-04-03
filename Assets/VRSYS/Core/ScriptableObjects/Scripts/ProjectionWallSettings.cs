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
//   Authors:        Sebastian Muehlhaus
//   Date:           2023
//-----------------------------------------------------------------

using System;
using System.IO;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Core.ScriptableObjects
{
    [CreateAssetMenu(menuName = "Scriptable Objects/ProjectionWallSettings")]
    public class ProjectionWallSettings : JsonConfig
    {
        [Header("Parsed from SystemConfig")]
        [Tooltip("All fields will be set after instantiation. Their values are JSON parsed from the SystemConfig.content")]
        public Config config;

        public MultiUserSettings multiUserSettings
        {
            get
            {
                return config.multiUserSettings;
            }
        }

        public StereoUserSettings localUserSettings
        {
            get
            {
                foreach (var u in config.multiUserSettings.users)
                {
                    if (u.hostname == Environment.MachineName.ToLower())
                        return u;
                }
                return config.multiUserSettings.userDefault;
            }
        }

        private bool shouldWriteStartupFeedbackFile
        {
            get
            {
                return hasRead && config.startupFeedback && localUserSettings.masterFlag;
            }
        }

        public override void Read()
        {
            base.Read();

            config = JsonUtility.FromJson<Config>(rawContent);

            if (shouldWriteStartupFeedbackFile)
                WriteStartupFeedbackFile();
        }

        // Feedback file is used for slave machine startup (TODO: rework cluster launch to deprecate this)

        private void WriteStartupFeedbackFile()
        {
            string file = config.startupFeedbackPath + "/master.txt";

            using (StreamWriter sw = File.CreateText(file))
                sw.Close();

            ExtendedLogger.LogInfo(GetType().Name, "StartupFeedbackFile written");
        }

        // Serializable Classes for Json Parsing

        [Serializable]
        public class Config
        {
            public string configName = "";
            public bool startupFeedback = false;
            public string startupFeedbackPath = "";
            public MultiUserSettings multiUserSettings;
        }

        [Serializable]
        public class MultiUserSettings
        {
            public int dTrackPort;
            public float screenWidth;
            public float screenHeight;
            public float[] screenPos = new float[4];
            public float[] windowSettings = new float[4];
            public float[] windowSettingsCropped = new float[4];
            public bool monoFallback;
            public float monoFallbackDetectionDuration;
            public float monoFallbackDetectionMovementThreshold;
            public StereoUserSettings userDefault;
            public StereoUserSettings[] users;

            public Vector3 screenPosVector3
            {
                get
                {
                    return new Vector4(screenPos[0], screenPos[1], screenPos[2]);
                }
            }

            public Vector4 windowSettingsVector4
            {
                get
                {
                    return new Vector4(windowSettings[0], windowSettings[1], windowSettings[2], windowSettings[3]);
                }
            }

            public Vector4 windowSettingsCroppedVector4
            {
                get
                {
                    return new Vector4(windowSettingsCropped[0], windowSettingsCropped[1], windowSettingsCropped[2], windowSettingsCropped[3]);
                }
            }
        }

        [Serializable]
        public class StereoUserSettings
        {
            public string username = "";
            public string hostname = "";
            public bool masterFlag = false;
            public bool headtrackingFlag = true;
            public int trackingID = 0;
            public float[] fixedHeadPos = new float[3];
            public float eyeDistance = 0.064f;

            public Vector3 fixedHeadPosVector3
            {
                get
                {
                    return new Vector3(fixedHeadPos[0], fixedHeadPos[1], fixedHeadPos[2]);
                }
            }
        }
    }
}
