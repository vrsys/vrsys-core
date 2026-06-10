using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRSYS.Core.Avatar;
using VRSYS.Core.Networking;
using VRSYS.Recording.Scripts;
using Vrsys.Scripts.Recording;

namespace VRSYS.Scripts.Recording
{
    public class ReplayGameObjectInformation
    {
        public int id = -1;
        public string hierarchyName = "";
        public string gameObjectName = "";
        public List<string> components = new List<string>();
        public string meshName = "";
        public string prefabLocation = "";
        public float firstSeenTime = -1;
        public bool instantiated = false;
        public GameObject foundObject = null;
    }

    public class ScenePreparator : MonoBehaviour
    {
        [DllImport("RecordingPlugin")]
        private static extern int GetRecordingTransformCount(int recorderId);

        [DllImport("RecordingPlugin")]
        private static extern int GetRecordingTransformIDs(int recorderId, IntPtr ids, int maxSize);

        [DllImport("RecordingPlugin")]
        private static extern int GetGameObjectHierarchyNameByID(int recorderId, StringBuilder textBuilder, int maxSize, int id);

        [DllImport("RecordingPlugin")]
        private static extern int GetGameObjectMeshPathByID(int recorderId, StringBuilder textBuilder, int maxSize, int id);

        [DllImport("RecordingPlugin")]
        private static extern int GetGameObjectPrefabByID(int recorderId, StringBuilder textBuilder, int maxSize,
            int id);

        [DllImport("RecordingPlugin")]
        private static extern int GetGameObjectComponentsByID(int recorderId, StringBuilder textBuilder, int maxSize, int id);

        [DllImport("RecordingPlugin")]
        private static extern int GetRecordingSoundSources(int recorderId, IntPtr ids, int maxSize);

        [DllImport("RecordingPlugin")]
        private static extern float GetGameObjectFirstSeenTimeByID(int recorderId, int id);

        private const int MaxSoundSources = 100;

        private RecorderController _controller;
        private List<GameObject> _instantiatedGameObjects = new List<GameObject>();
        private List<ReplayGameObjectInformation> replayGameObjectInformations = new List<ReplayGameObjectInformation>();
        private int[] _soundIDs = new int[MaxSoundSources];

        private float dissolveTime = 2.0f;

        /// <summary>
        /// Raised while preparing an avatar playback object, with the instantiated GameObject and the
        /// recorded "Meta ID" node name. The optional Meta integration subscribes to this to wire up
        /// the Meta avatar replay data writer without ScenePreparator referencing any Meta type.
        /// </summary>
        public event System.Action<GameObject, string> AvatarPlaybackSetup;

        public void Start()
        {
            _controller = GetComponent<RecorderController>();
        }

        private bool HandleMissingGameObject()
        {
            Debug.Log("Trying to handle missing objects.");

            // With a configured replay root, only objects beneath that root may be matched to the
            // recording, so restrict the existence check to its subtree. This ensures duplicates that
            // live outside the anchor (e.g. an original "/Cube") are not matched and played back.
            if (_controller.replayRoot != null)
            {
                SceneGraphTraversalGameObjectExistenceCheck(_controller.replayRoot.gameObject, "");
            }
            else
            {
                GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var rootObject in rootObjects)
                    SceneGraphTraversalGameObjectExistenceCheck(rootObject, "");

                if (DontDestroySceneAccessor.Instance != null)
                {
                    rootObjects = DontDestroySceneAccessor.Instance.GetAllRootsOfDontDestroyOnLoad();
                    foreach (var rootObject in rootObjects)
                        SceneGraphTraversalGameObjectExistenceCheck(rootObject, "");
                }
            }

            if (_controller.debugLogs)
                Debug.Log("Scene Graph existence check finished.");

            bool error = false;
            foreach (var info in replayGameObjectInformations)
            {
                if (info.foundObject == null)
                    error = true;
            }

            // this might not be necessary, only in cases where gameobjects changed their parents before the recording was started
            replayGameObjectInformations.Sort((s1, s2) => s1.firstSeenTime.CompareTo(s2.firstSeenTime));
            // this is important, as we first want to instantiate all elements that have prefabs before creating empty objects for the rest.
            replayGameObjectInformations.Sort((a, b) =>
                (b.prefabLocation.Length > 0).CompareTo(a.prefabLocation.Length > 0));

            foreach (var information in replayGameObjectInformations)
            {
                if (!information.instantiated)
                {
                    int lastSlash = information.hierarchyName.LastIndexOf('/');
                    string parentName = lastSlash >= 0 ? information.hierarchyName.Substring(0, lastSlash) : "";
                    Transform parent = null;
                    var parentInfo = replayGameObjectInformations.FirstOrDefault(i => i.hierarchyName == parentName);
                    if (parentInfo?.foundObject != null)
                        parent = parentInfo.foundObject.transform;
                    // Top-level recorded objects (no recorded parent) are placed under the configured
                    // replay root so the instantiated playback objects sit correctly under the anchor.
                    else if (string.IsNullOrEmpty(parentName) && _controller.replayRoot != null)
                        parent = _controller.replayRoot;

                    if (information.prefabLocation.Length > 0 && information.foundObject == null)
                        TryInstantiatePrefab(information, parent, parentName);
                    else if (information.foundObject == null)
                        CreateEmptyGameObject(information, parent, parentName);

                    information.instantiated = true;
                }
            }

            return error;
        }

        private void TryInstantiatePrefab(ReplayGameObjectInformation information, Transform parent, string parentName)
        {
            string prefabName = "";
            if (information.prefabLocation.Contains("Resources/")){
                prefabName = information.prefabLocation.Split("Resources/").Last().Replace(".prefab", "");
                Debug.Log(": " + information.gameObjectName + ", prefab location : " + prefabName +
                          ", original location: " + information.prefabLocation);
			} else {
				Debug.LogWarning("The prefab that would be instantiated for replay is not in a Resources folder!");
			}

            GameObject go = prefabName != "" ? Resources.Load<GameObject>(prefabName) : null;

            if (go != null)
            {
                if (_controller.debugLogs)
                    Debug.Log("Prefab could be loaded.");
                GameObject newGo = Instantiate(go, parent);
                if (_controller.debugLogs)
                    Debug.Log("Removing custom components.");
                Utils.RemoveCustomComponents(newGo.transform);
                Utils.ModifyComponents(newGo.transform);
                if (_controller.debugLogs)
                    Debug.Log("Removing custom components finished.");
                newGo.name = information.gameObjectName;
                information.foundObject = newGo;
                newGo.SetActive(false);

                if (newGo.GetComponent<AvatarHMDAnatomy>() != null)
                    SetupAvatarPlaybackGO(newGo, information);

                MarkAsPlaybackGO(newGo);
                // Track the whole instantiated subtree, not just the root. A recorded child may be
                // reparented out of this prefab during playback; if only the root were tracked it would
                // survive when the root is destroyed and leak. Tracking every node guarantees cleanup.
                TrackInstantiatedHierarchy(newGo);
                SceneGraphTraversalGameObjectExistenceCheck(newGo, parentName);
            }
            else
            {
                if (_controller.debugLogs)
                    Debug.LogWarning("The gameobject at path: " + information.prefabLocation +
                                     " could not be loaded! Make sure it is in a resource folder!");
                CreateEmptyGameObject(information, parent, parentName);
            }
        }

        private void SetupAvatarPlaybackGO(GameObject newGo, ReplayGameObjectInformation information)
        {
            string metaIDNodeString = "";
            foreach (var replayInfo in replayGameObjectInformations)
            {
                if (replayInfo.hierarchyName.Contains(newGo.name) &&
                    replayInfo.hierarchyName.Contains("Meta ID:"))
                {
                    string toBeSearched = newGo.name + "/";
                    string stringRest = replayInfo.hierarchyName
                        .Substring(replayInfo.hierarchyName.IndexOf(toBeSearched) + toBeSearched.Length);
                    metaIDNodeString = stringRest.Split('/')[0];
                    break;
                }
            }

            // Meta-specific avatar wiring (renaming the replay-data writer node to the recorded Meta ID)
            // is handled by the optional Meta integration through this hook, keeping ScenePreparator Meta-free.
            if (metaIDNodeString != "")
                AvatarPlaybackSetup?.Invoke(newGo, metaIDNodeString);

            GameObject userNameLabelGo = Utils.GetChildByName(newGo, "UserNameLabel");
            if (userNameLabelGo != null)
                userNameLabelGo.GetComponent<Canvas>().enabled = false;

            GameObject userNameTextGo = Utils.GetChildByName(newGo, "UserName-Text");
            if (userNameTextGo != null)
            {
                TextMeshProUGUI userNameText = userNameTextGo.GetComponentInChildren<TextMeshProUGUI>();
                string cleanName = information.gameObjectName.Replace(" [Local]", "").Replace(" [Remote]", "");
                userNameText.text = "[Rec] " + cleanName;
            }

            GameObject recordingIndication = Utils.GetChildByName(newGo, "Indicator-Image");
            if (recordingIndication != null)
                recordingIndication.GetComponent<Image>().enabled = false;

            newGo.SetActive(true);
            
            // TODO: note, that the replay avatar will only be instantiated once playback has started, meaning that there
            // will be empty nodes being created for the LOD transforms which are children of the replay avatar that were recorded
            // as they are not present during scene setup they cannot be found and are thus created as empty gameobjects
        }

        private void CreateEmptyGameObject(ReplayGameObjectInformation information, Transform parent, string parentName)
        {
            GameObject newGo = new GameObject(information.gameObjectName);
            newGo.transform.parent = parent;
            MarkAsPlaybackGO(newGo);
            _instantiatedGameObjects.Add(newGo);
            information.foundObject = newGo;
            SceneGraphTraversalGameObjectExistenceCheck(newGo, parentName);
            newGo.SetActive(false);
        }

        private unsafe bool HandleAudioSources()
        {
            fixed (int* u = _soundIDs)
            {
                if (_controller.debugLogs)
                    Debug.Log("Trying to get ids of all sound sources contained in recording.");
                int count = GetRecordingSoundSources(_controller.RecorderID, (IntPtr)u, MaxSoundSources);
                if (_controller.debugLogs)
                    Debug.Log("Found: " + count + " sound sources.");

                if (count >= 0)
                {
                    for (int k = 0; k < count; ++k)
                    {
                        int soundSourceId = _soundIDs[k];
                        if (soundSourceId == 1)
                        {
                            Debug.LogWarning("The recorded audiolistener data is not played back!");
                            continue;
                        }

                        GameObject newGo = new GameObject();
                        newGo.name = "SoundSource:" + soundSourceId;
                        AudioSourceRecorder recorder = newGo.AddComponent<AudioSourceRecorder>();
                        recorder.SetId(soundSourceId);
                        recorder.Controller = _controller;
                        _instantiatedGameObjects.Add(newGo);
                    }
                }
                else
                {
                    if (_controller.debugLogs)
                        Debug.LogError("Could not retrieve sound sources");
                    return true;
                }
            }

            if (_controller.debugLogs)
                Debug.Log("Sound source retrieval and setup finished.");

            return false;
        }

        public Dictionary<string, GameObject> GetNamePresent()
        {
            return replayGameObjectInformations.ToDictionary(i => i.hierarchyName, i => i.foundObject);
        }

        public unsafe void PrepareReplayScene()
        {
            replayGameObjectInformations.Clear();
            if (_controller.debugLogs)
                Debug.Log("Trying to get recording gameobjects");

            int transformCount = GetRecordingTransformCount(_controller.RecorderID);
            if (transformCount <= 0)
            {
                Debug.LogWarning(
                    "No transforms could be retrieved from the recording for scene preparation. Is the recording file broken?");
                return;
            }

            int[] transformIDs = new int[transformCount];
            fixed (int* t = transformIDs)
            {
                GetRecordingTransformIDs(_controller.RecorderID, (IntPtr)t, transformCount);
            }

            int maxSize = 10000;
            StringBuilder buffer = new StringBuilder(maxSize);

            for (int i = 0; i < transformCount; ++i)
            {
                int currentID = transformIDs[i];
                GetGameObjectHierarchyNameByID(_controller.RecorderID, buffer, maxSize, currentID);
                string gameobjectHierarchyPath = buffer.ToString();

                GetGameObjectMeshPathByID(_controller.RecorderID, buffer, maxSize, currentID);
                string gameobjectMeshPath = buffer.ToString();

                GetGameObjectPrefabByID(_controller.RecorderID, buffer, maxSize, currentID);
                string gameobjectPrefabLocation = buffer.ToString();

                GetGameObjectComponentsByID(_controller.RecorderID, buffer, maxSize, currentID);
                string gameobjectComponents = buffer.ToString();

                float firstSeenTime = GetGameObjectFirstSeenTimeByID(_controller.RecorderID, currentID);
                string[] pathParts = gameobjectHierarchyPath.Split('/');
                ReplayGameObjectInformation gameObjectInformation = new ReplayGameObjectInformation();
                gameObjectInformation.id = currentID;
                gameObjectInformation.hierarchyName = gameobjectHierarchyPath;
                gameObjectInformation.meshName = gameobjectMeshPath;
                gameObjectInformation.gameObjectName = pathParts[pathParts.Length - 1];
                gameObjectInformation.prefabLocation = gameobjectPrefabLocation.Trim();
                gameObjectInformation.components = gameobjectComponents.Split(",").ToList();
                gameObjectInformation.firstSeenTime = firstSeenTime;
                replayGameObjectInformations.Add(gameObjectInformation);
            }

            bool error = HandleMissingGameObject();

            if (_controller.debugLogs)
            {
                if (!error)
                    Debug.Log("All gameObjects from the recording are present in the current scene graph!");
                else
                    Debug.LogError(
                        "Not all gameObjects from the recording are present in the current scene graph!");
            }

            if (_controller.debugLogs)
                Debug.Log("Trying to handle missing audio sources.");
            error = HandleAudioSources();

            if (_controller.debugLogs)
            {
                if (!error)
                    Debug.Log("All sound sources from the recording are now present in the current scene graph!");
                else
                    Debug.LogError(
                        "Not all sound sources from the recording are present in the current scene graph!");
            }
        }

        public void CleanReplayScene()
        {
            Debug.Log("Cleaning replay scene by destroying objects that were instantiated for playback.");

            // Snapshot the instantiated objects and reset the field immediately. Destruction is deferred
            // by dissolveTime, so a new PrepareReplayScene started within that window must not append into
            // the list that is about to be destroyed (otherwise the next replay's objects get killed too).
            List<GameObject> toDelete = _instantiatedGameObjects;
            _instantiatedGameObjects = new List<GameObject>();

            foreach (var go in toDelete)
            {
                if (go != null)
                    GameObject.Destroy(go, dissolveTime);
            }

            Debug.Log("Cleaning replay scene done.");
        }

        private void TrackInstantiatedHierarchy(GameObject go)
        {
            _instantiatedGameObjects.Add(go);
            foreach (Transform child in go.transform)
                TrackInstantiatedHierarchy(child.gameObject);
        }

        private void MarkAsPlaybackGO(GameObject currentGameObj)
        {
            currentGameObj.name += "[Rec" + _controller.RecorderID + "]";
            //foreach (Transform childTransform in currentGameObj.transform)
            //{
            //    MarkAsPlaybackGO(childTransform.gameObject);
            //}
        }

        private void SceneGraphTraversalGameObjectExistenceCheck(GameObject currentGameObj, string name)
        {
            // if (currentGameObj.tag == "IgnoreForPlayback")
            //     return;

            // Note: this is being done because the name of a local user can change during sessions
            string objectName = currentGameObj.name;
            string pattern = "\\[Rec" + _controller.RecorderID + "\\]$";
            objectName = Regex.Replace(objectName, pattern, "");
            string pattern2 = "\\[Rec" + _controller.RecorderID + "\\]\\/";
            objectName = Regex.Replace(objectName, pattern2, "/");

            // When a replay root is configured, treat it as the root of the recorded hierarchy: objects
            // duplicated beneath it are matched against the recorded (anchor-relative) names, while the
            // replay root itself and its ancestors are not part of those names.
            if (_controller.replayRoot != null && currentGameObj.transform == _controller.replayRoot)
                name = "";
            else
                name += "/" + objectName;

            if (currentGameObj.GetComponent<NetworkUser>() != null)
                return;

            if (currentGameObj.GetComponent<XROrigin>() != null)
                return;

            // if(currentGameObj.tag == "IgnoreForPlayback")
            //     return;

            foreach (Transform childTransform in currentGameObj.transform)
            {
                SceneGraphTraversalGameObjectExistenceCheck(childTransform.gameObject, name);
            }

            var info = replayGameObjectInformations.FirstOrDefault(i => i.hierarchyName == name);
            if (info != null)
            {
                TransformRecorder[] transformRecorders = currentGameObj.GetComponents<TransformRecorder>();
                bool found = false;
                bool recorderAttached = false;

                foreach (var transformRecorder in transformRecorders)
                {
                    recorderAttached = true;
                    if (transformRecorder.controller.RecorderID == _controller.RecorderID)
                        found = true;
                }

                if (!recorderAttached || found)
                {
                    info.foundObject = currentGameObj;
                    info.instantiated = true;
                }
            }
            else
            {
                if (!name.Contains("RecordingSetup"))
                {
                    //Debug.Log("GameObject: " + name + " not existent in recording! Thus it will not be animated...");
                }
            }
        }
    }
}
