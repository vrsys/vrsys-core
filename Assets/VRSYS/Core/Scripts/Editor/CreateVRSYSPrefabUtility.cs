using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VRSYS.Core.Editor
{
    public static class CreateVRSYSPrefabUtility
    {
        #region Prefab Creation

        private static void CreatePrefab(string path)
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

        #region MenuItems

        [MenuItem("GameObject/VRSYS/Core/ConnectionManager")]
        public static void CreateConnectionManager(MenuCommand menuCommand)
        {
            CreatePrefab("VRSYS-ConnectionManager");
        }
        
        [MenuItem("GameObject/VRSYS/Core/NetworkUserSpawner")]
        public static void CreateNetworkUserSpawner(MenuCommand menuCommand)
        {
            CreatePrefab("VRSYS-NetworkUserSpawner");
        }
        
        [MenuItem("GameObject/VRSYS/Core/LobbyListUpdater")]
        public static void CreateLobbyListUpdater(MenuCommand menuCommand)
        {
            CreatePrefab("VRSYS-LobbyListUpdater");
        }

        #endregion
    }
}
