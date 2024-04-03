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

using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRSYS.Core.Logging;
using VRSYS.Core.ScriptableObjects;

namespace VRSYS.Core.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkSceneSwitch : NetworkBehaviour
    {
        [Header("Cross-Scene Configuration")]

        [Tooltip("Settings that are potentially shared across scenes. Note, any adjustments you make on this property may affect scene load behaviour in other scenes.")]
        public SceneLoadSettings sceneLoadSettings;

        [Header("In-Scene Configuration")]

        [Tooltip("If this field contains a non-empty string, its value will be the scene load target used OnNetworkSpawn. sceneLoadSettings.sceneToLoadOnSpawn will be ignored, but remains unchanged. Use this for scene load adjustments that should not persist across scenes.")]
        public string sceneToLoadOnSpawnOnce = "";
        public void SetSceneToLoadOnSpawnOnce(string name) => sceneToLoadOnSpawnOnce = name;

        [Tooltip("If this field contains a non-empty string, its value will be the scene load target used OnNetworkDespawn. sceneLoadSettings.sceneToLoadOnDespawn will be ignored, but remains unchanged. Use this for scne load adjustments that should not persist across scenes.")]
        public string sceneToLoadOnDespawnOnce = "";
        public void SetSceneToLoadOnDespawnOnce(string name) => sceneToLoadOnDespawnOnce = name;

        [Header("Debugging")]

        public bool verbose = false;

        private static bool sceneIsLoading = false;

        private bool hasSceneToLoadOnSpawn => !string.IsNullOrEmpty(sceneToLoadOnSpawn);

        private bool hasSceneToLoadOnDespawn => !string.IsNullOrEmpty(sceneToLoadOnDespawn);

        private string sceneToLoadOnSpawn => sceneToLoadOnSpawnOnce.Length > 0 ? sceneToLoadOnSpawnOnce : sceneLoadSettings?.sceneToLoadOnSpawn;

        private string sceneToLoadOnDespawn => sceneToLoadOnDespawnOnce.Length > 0 ? sceneToLoadOnDespawnOnce : sceneLoadSettings?.sceneToLoadOnDespawn;

        private void Start()
        {
            sceneIsLoading = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer && hasSceneToLoadOnSpawn)
                LoadScene(sceneToLoadOnSpawn, isNetworkScene: true);
        }

        private void OnNetworkSceneLoad(ulong clientId, string sceneName, LoadSceneMode loadSceneMode, AsyncOperation asyncOperation)
        {
            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, "loading scene " + sceneName);
            sceneIsLoading = true;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, "despawning current scene");
            if (hasSceneToLoadOnDespawn)
                LoadScene(sceneToLoadOnDespawn, isNetworkScene: false);
        }

        public void LoadScene(string targetScene)
        {
            if (string.IsNullOrEmpty(targetScene))
            {
                ExtendedLogger.LogError(GetType().Name, "no '" + nameof(targetScene) + "' set for call to '" + nameof(LoadScene) + "'.");
                return;
            }
            LoadScene(targetScene, isNetworkScene: true);
        }

        private void LoadScene(string name, bool isNetworkScene)
        {
            if (sceneIsLoading)
                return;
            sceneIsLoading = true;
            if (isNetworkScene)
            {
                if(IsServer && !IsHost)
                    DistributeSceneLoad(name);
                else
                    LoadSceneServerRpc(name);
            }
            else
                SceneManager.LoadScene(name, LoadSceneMode.Single);
        }

        [ServerRpc(RequireOwnership = false)]
        private void LoadSceneServerRpc(FixedString64Bytes name)
        {
            if(verbose)
                ExtendedLogger.LogInfo(GetType().Name, "Load Scene Server RPC called.");
            DistributeSceneLoad(name.ToString());
        }

        private void DistributeSceneLoad(string name)
        {
            SceneAboutToChangeClientRpc();
            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
                if (client.PlayerObject != null)
                {
                    if (verbose)
                        ExtendedLogger.LogInfo(GetType().Name, "despawning client named " + client.PlayerObject.name);
                    client.PlayerObject.Despawn();
                }
            }
            NetworkManager.SceneManager.LoadScene(name, LoadSceneMode.Single);
        }

        [ClientRpc]
        private void SceneAboutToChangeClientRpc()
        {
            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, "scene is about to change");
            sceneIsLoading = true;
        }
    }
}
