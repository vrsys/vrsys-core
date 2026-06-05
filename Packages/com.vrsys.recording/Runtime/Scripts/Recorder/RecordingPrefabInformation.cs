using UnityEngine;
using VRSYS.Recording.Scripts;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VRSYS.Scripts.Recording
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class RecordingPrefabInformation : MonoBehaviour
    {
        public Prefab correspondingPrefab;
        
#if UNITY_EDITOR
        public void Setup(string assetPath)
        {
            string fullPath = "Assets/VRSYS/Recording/Resources/Prefabs/" + gameObject.name + ".asset";
   
            // Try to load the existing asset
            var existingPrefab = AssetDatabase.LoadAssetAtPath<Prefab>(fullPath);

            if (existingPrefab != null)
            {
                correspondingPrefab = existingPrefab; // Modify existing asset
            }
            else
            {
                correspondingPrefab = ScriptableObject.CreateInstance<Prefab>(); // Create new one
                AssetDatabase.CreateAsset(correspondingPrefab, fullPath);
            }

            correspondingPrefab.assetPath = assetPath;
            
            EditorUtility.SetDirty(correspondingPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
#endif

        public void Start()
        {
            // Here we handle late-joining content for recording
            RecorderController[] recorderControllers = FindObjectsOfType<RecorderController>();
            foreach (RecorderController recorderController in recorderControllers)
            {
                if (recorderController.CurrentState == State.Recording)
                {
                    recorderController.AttachTransformRecorderRecursively(gameObject);
                    
                    AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    for (int i = 0; i < sources.Length; ++i)
                    {
                        AudioSourceRecorder recorder = sources[i].GetComponent<AudioSourceRecorder>();
                        if (recorder == null)
                        {
                            recorder = sources[i].gameObject.AddComponent<AudioSourceRecorder>();
                            recorder.SetId(recorderController.GetNextAvailableSoundID());
                            recorder.Controller = recorderController;
                        }
                    }
                }
            }
        }
    }
}