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
//   Date:           2025
//-----------------------------------------------------------------

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkUserSpawner : NetworkBehaviour
    {
        #region Member Variables

        [FormerlySerializedAs("userTypes")] public List<UserRolePrefab> userPrefabs;

        [Tooltip("If set to true, user role specific spawn point will be used. If set to false or no user role specific spawn point was configured, spawn point configured below will be used.")]
        public bool useRoleSpawnPoints = true;

        [Tooltip("This spawn point is used if userRoleSpawnPoint is false, or no SpawnPoint was configured for the selected user role. If null, it will be set to this Transform.")]
        public Transform spawnPoint;

        public bool verbose = false;

        private NetworkUserSpawnInfo spawnInfo;

        #endregion

        #region Netcode Callbacks

        public override void OnNetworkSpawn()
        {
            if (spawnPoint == null)
                spawnPoint = transform;
            
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
            Transform spawn = useRoleSpawnPoints ? userPrefabs[prefabIndex].SpawnPoint : spawnPoint;

            // if useRoleSpawnPoints == true, but roleSpawnPoint == null --> Fallback to default spawn point
            if (spawn == null)
                spawn = spawnPoint;
            
            // Instantiate user prefab
            GameObject user = Instantiate(userPrefabs[prefabIndex].Prefab);

            user.transform.position = spawn.position;
            user.transform.rotation = spawn.rotation;
            
            // Spawn user prefab
            NetworkObject netObj = user.GetComponent<NetworkObject>();
            netObj.SpawnWithOwnership(serverRpcParams.Receive.SenderClientId);
            //user.GetComponent<NetworkObject>().SpawnAsPlayerObject(serverRpcParams.Receive.SenderClientId, destroyWithScene: false);
        }

        #endregion

        #region Data Structs

        [Serializable]
        public struct UserRolePrefab
        {
            [UserRoleSelector]
            public UserRole UserRole;
            public GameObject Prefab;
            public Transform SpawnPoint;
        }

        #endregion
    }
}