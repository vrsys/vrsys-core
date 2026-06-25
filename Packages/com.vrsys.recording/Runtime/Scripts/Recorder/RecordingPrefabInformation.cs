using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VRSYS.Recording
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class RecordingPrefabInformation : MonoBehaviour
    {
        public Prefab correspondingPrefab;
        
#if UNITY_EDITOR
        // Project-side folder where the generated Prefab mapping assets are stored. These assets only
        // hold the prefab's asset path (referenced directly via correspondingPrefab), so they do not
        // need to live under a Resources folder.
        private const string PrefabInformationFolder = "Assets/Recording/PrefabInformation";

        public void Setup(string assetPath, string addressableKey = "")
        {
            EnsureFolderExists(PrefabInformationFolder);
            string fullPath = PrefabInformationFolder + "/" + gameObject.name + ".asset";

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
            correspondingPrefab.addressableKey = addressableKey;

            EditorUtility.SetDirty(correspondingPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Creates each missing folder along the given project-relative path (e.g. "Assets/A/B"),
        // because AssetDatabase.CreateAsset requires the parent folder to already exist.
        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
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