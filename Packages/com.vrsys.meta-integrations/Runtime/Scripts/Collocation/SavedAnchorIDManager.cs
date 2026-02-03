using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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