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
//   Authors:        Tony Zoeppig, Sebastian Muehlhaus
//   Date:           2023
//-----------------------------------------------------------------

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using VRSYS.Core.Logging;
using VRSYS.Core.ScriptableObjects;

namespace VRSYS.Core.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkUserSpawner : NetworkBehaviour
    {
        #region Member Variables

        [FormerlySerializedAs("userTypes")] public List<UserRolePrefab> userPrefabs;
        
        public List<Transform> spawnPoints;

        public bool verbose = false;

        private NetworkUserSpawnInfo spawnInfo;

        #endregion

        #region Netcode Callbacks

        public override void OnNetworkSpawn()
        {
            spawnInfo = ConnectionManager.Instance.userSpawnInfo;

            if(!ConnectionManager.Instance.startDedicatedServer)
            {
                int prefabIndex = userPrefabs.FindIndex(userType => userType.UserRole == spawnInfo.userRole);

                if (prefabIndex == -1)
                {
                    ExtendedLogger.LogError(GetType().Name, "no user prefab found for user role " + spawnInfo.userRole);
                    return;
                }

                if(verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "spawning " + spawnInfo.userName + " " + spawnInfo.userRole.ToString());

                SpawnUserPrefabServerRPC(prefabIndex);
            }
        }

        #endregion

        #region RPCs

        [ServerRpc(RequireOwnership = false)]
        public void SpawnUserPrefabServerRPC(int prefabIndex, ServerRpcParams serverRpcParams = default)
        {
            // Instantiate user prefab
            GameObject user = Instantiate(userPrefabs[prefabIndex].UserPrefab);
            
            // Spawn point handling
            if (spawnPoints.Count == 0)
            {
                user.transform.position = transform.position;
                user.transform.rotation = transform.rotation;
            }
            else
            {
                int spawnPointIndex = UnityEngine.Random.Range(0, spawnPoints.Count);
                user.transform.position = spawnPoints[spawnPointIndex].position;
                user.transform.rotation = spawnPoints[spawnPointIndex].rotation;
            }

            // Spawn user prefab
            user.GetComponent<NetworkObject>().SpawnAsPlayerObject(serverRpcParams.Receive.SenderClientId, destroyWithScene: false);
        }

        #endregion

        #region Data Structs

        [Serializable]
        public struct UserRolePrefab
        {
            public UserRole UserRole;
            public GameObject UserPrefab;
        }

        #endregion
    }
}