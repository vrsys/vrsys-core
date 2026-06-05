using System;
using UnityEngine;
namespace VRSYS.Recording.Scripts
{
    // https://forum.unity.com/threads/editor-script-how-to-access-objects-under-dontdestroyonload-while-in-play-mode.442014/
    public class DontDestroySceneAccessor : MonoBehaviour
    {
        private static DontDestroySceneAccessor _instance;
        
        public static DontDestroySceneAccessor Instance {
            get {
                return _instance;
            }
        }
 
        void Awake()
        {
            if (_instance != null) Destroy(this);
            this.gameObject.name = this.GetType().ToString();
            _instance = this;
            DontDestroyOnLoad(this);
        }
 
        public GameObject[] GetAllRootsOfDontDestroyOnLoad() {
            return this.gameObject.scene.GetRootGameObjects();
        }

        private void OnDestroy()
        {
            
        }
    }
}