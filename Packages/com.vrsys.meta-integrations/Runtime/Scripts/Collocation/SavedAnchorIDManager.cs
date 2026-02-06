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
//   Authors:        Tony Zoeppig, Karoline Brehm
//   Date:           2025
//-----------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace VRSYS.Meta.Collocation
{
    public class SavedAnchorIDManager
    {
        public static string AnchorIDsFilePath { get; private set; } = Path.Combine(Application.persistentDataPath, "SavedAnchorIds.json");
        
        public static async void SaveAnchorID(Guid anchorUuid)
        {
            Debug.Log($"PATH: {AnchorIDsFilePath}");
            await SaveAnchorGUIDsAsync(anchorUuid);
        }

        public static async Awaitable<HashSet<Guid>> LoadAnchorIdsFromFile()
        {
            Debug.Log("Loading saved GUIDs");
            try
            {
                if (!File.Exists(AnchorIDsFilePath))
                    return new HashSet<Guid>();
                
                var text = await File.ReadAllTextAsync(AnchorIDsFilePath);
                return JsonConvert.DeserializeObject<HashSet<Guid>>(text);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new HashSet<Guid>();
            }
        }
        
        /// <summary>
        /// Save list of anchor UUID to persistent storage as JSON.
        /// </summary>
        private static async Awaitable SaveAnchorGUIDsAsync(Guid anchorUuid)
        {
            var currentlySavedIDs = await LoadAnchorIdsFromFile();
            currentlySavedIDs.Add(anchorUuid);
            var jsonString = JsonConvert.SerializeObject(currentlySavedIDs, Formatting.Indented);
            await File.WriteAllTextAsync(AnchorIDsFilePath, jsonString);
        }

        public static async Awaitable DeleteIDfromSaved(Guid anchorUuid)
        {
            var currentlySavedIDs = await LoadAnchorIdsFromFile();
            currentlySavedIDs.Remove(anchorUuid);
            var jsonString = JsonConvert.SerializeObject(currentlySavedIDs, Formatting.Indented);
            await File.WriteAllTextAsync(AnchorIDsFilePath, jsonString);
        }
    }
}