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
//   Authors:        Sebastian Muehlhaus, Tony Jan Zoeppig
//   Date:           2023
//-----------------------------------------------------------------

using System;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Networking
{
    [CreateAssetMenu(menuName = "VRSYS/Core/Scriptable Objects/DedicatedServerSettings")]
    public class DedicatedServerSettings : ScriptableObject
    {
        public struct JsonConfig
        {
            public string LobbyName;
            public int MaxConnections;
            public string StartTime;
        }
        
        [FormerlySerializedAs("dedicatedServerConfigPath")] public string jsonConfigPath = "configs/dedicated-server-config.json";
        [FormerlySerializedAs("readOnAwake")] public bool readJsonConfigOnAwake = true;
        
        public void SetJsonConfigPath(string path) => jsonConfigPath = path;
        
        public void SetReadJsonConfigOnAwake(bool readOnAwake) => readJsonConfigOnAwake = readOnAwake;
        
        public JsonConfig jsonConfig { get; private set; }
        
        public void ParseJsonConfigFile(bool verbose = false)
        {
            if (!File.Exists(jsonConfigPath))
            {
                throw new FileNotFoundException(jsonConfigPath);
            }

            StreamReader reader = new StreamReader(jsonConfigPath);
            string json = reader.ReadToEnd();
            reader.Close();

            jsonConfig = JsonUtility.FromJson<JsonConfig>(json);
            
            if(verbose)
                ExtendedLogger.LogInfo(GetType().Name, "server config parsed!");
        }

        private void Awake()
        {
            if(readJsonConfigOnAwake)
                ParseJsonConfigFile();
        }
    }
}
