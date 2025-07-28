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

using System.IO;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Utility
{
    [CreateAssetMenu(menuName = "VRSYS/Core/Scriptable Objects/JsonConfig")]
    public class JsonConfig : ScriptableObject
    {
        public string configPath = "configs/my-config.json";

        public string rawContent { get; protected set; }

        public bool verbose = false;

        public bool hasRead { get; private set; }

        // override this in derived classes for custom read behavior, e.g. Json Parsing
        public virtual void Read()
        {
            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, "read config: " + configPath);

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException(configPath);
            }

            StreamReader reader = new StreamReader(configPath);
            rawContent = reader.ReadToEnd();
            reader.Close();

            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, "content: " + rawContent);

            hasRead = true;
        }

        public void Save(string configString)
        {
            string file = string.Format(configPath);
            string configJson = JsonUtility.ToJson(configString, true); // prettyPrint = true
            using (StreamWriter sw = File.CreateText(file))
            {
                sw.Write(configJson);
                sw.Close();
            }
            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, "saved config: " + configPath);
        }
    }
}
