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
//   Authors:        Tony Zoeppig
//   Date:           2025
//-----------------------------------------------------------------

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VRSYS.Core.Editor
{
    public static class CreateVRSYSPrefabUtility
    {
        #region Prefab Creation

        public static void CreatePrefab(string path)
        {
            GameObject newObject = PrefabUtility.InstantiatePrefab(Resources.Load(path)) as GameObject;
            Place(newObject);
        }

        private static void Place(GameObject gameObject)
        {
            SceneView lastView = SceneView.lastActiveSceneView;
            gameObject.transform.position = Vector3.zero;
            gameObject.transform.rotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one;
            
            StageUtility.PlaceGameObjectInCurrentStage(gameObject);
            GameObjectUtility.EnsureUniqueNameForSibling(gameObject);
            
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create Object: {gameObject.name}");
            Selection.activeGameObject = gameObject;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        #endregion

        #region Menu Items

        [MenuItem("GameObject/VRSYS/Core/ConnectionManager")]
        public static void CreateConnectionManager(MenuCommand menuCommand)
        {
            CreatePrefab("Prefabs/Basic Prefabs/VRSYS-ConnectionManager");
        }
        
        [MenuItem("GameObject/VRSYS/Core/NetworkUserSpawner")]
        public static void CreateNetworkUserSpawner(MenuCommand menuCommand)
        {
            CreatePrefab("Prefabs/Basic Prefabs/VRSYS-NetworkUserSpawner");
        }
        
        [MenuItem("GameObject/VRSYS/Core/LobbyListUpdater")]
        public static void CreateLobbyListUpdater(MenuCommand menuCommand)
        {
            CreatePrefab("Prefabs/Basic Prefabs/VRSYS-LobbyListUpdater");
        }

        [MenuItem("GameObject/VRSYS/Core/XREnableHelper")]
        public static void CreateXREnableHelper(MenuCommand menuCommand)
        {
            CreatePrefab("Prefabs/Basic Prefabs/XR-Enable-Helper");
        }

        #endregion
    }
}
