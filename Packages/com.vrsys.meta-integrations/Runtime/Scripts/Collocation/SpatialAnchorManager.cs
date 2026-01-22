using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace VRSYS.Meta.Collocation
{
    public class SpatialAnchorManager
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

                // using var streamReader = File.OpenText(AnchorIDsFilePath);
                // using var jsonTextReader = new JsonTextReader(streamReader);

                // var kvp = (JObject)await JToken.ReadFromAsync(jsonTextReader);
                // foreach (var (idAsString, dateTime) in kvp)
                // {
                //     var tokens = idAsString.Split("-");
                //     var low = Convert.ToUInt64(tokens[0], 16);
                //     var high = Convert.ToUInt64(tokens[1], 16);
                //     var serializableGuid = new SerializableGuid(low, high);
                //     _savedAnchors.Add(serializableGuid, (DateTime)dateTime);
                // }
                
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
    }
}