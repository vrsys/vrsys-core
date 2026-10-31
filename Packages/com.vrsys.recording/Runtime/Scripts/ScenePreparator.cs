using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using Unity.XR.CoreUtils;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRSYS.Core.Avatar;
using VRSYS.Core.Networking;
using VRSYS.Core.Logging;

namespace VRSYS.Recording
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
        // The object exists but another recorder is already replaying it; this replay leaves it alone.
        public bool replayedByOtherRecorder = false;
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

        /// <summary>ID of this preparator's recorder, stored in the <see cref="ReplayInstance"/> of the objects it creates.</summary>
        internal int RecorderId => _controller != null ? _controller.RecorderID : -1;
        private List<GameObject> _instantiatedGameObjects = new List<GameObject>();
        private List<ReplayGameObjectInformation> replayGameObjectInformations = new List<ReplayGameObjectInformation>();
        private int[] _soundIDs = new int[MaxSoundSources];

        private float dissolveTime = 2.0f;

        // Networked prefabs the server spawns as replay copies (RecorderController.spawnNetworkedPrefabsForReplay).
        private class ExpectedNetworkedCopy
        {
            public ReplayGameObjectInformation information;
            public string parentName;
        }

        private readonly Dictionary<GameObject, ReplayPrefabHandler> _replayPrefabHandlers = new Dictionary<GameObject, ReplayPrefabHandler>();
        // Handlers for NetworkObjects nested in those prefabs, by prefab hash.
        private readonly List<uint> _nestedObjectHandlerHashes = new List<uint>();
        // Client: replay copies received from the server, by recorded object id (to resolve their nested NetworkObjects).
        private readonly Dictionary<int, GameObject> _networkedCopiesById = new Dictionary<int, GameObject>();
        // Client: copies this replay waits for, by recorded object id.
        private readonly Dictionary<int, ExpectedNetworkedCopy> _expectedNetworkedCopies = new Dictionary<int, ExpectedNetworkedCopy>();
        // Client: copies that arrived before this peer prepared its scene.
        private readonly Dictionary<int, GameObject> _arrivedNetworkedCopies = new Dictionary<int, GameObject>();

        /// <summary>True while a client still waits for networked replay copies spawned by the server.</summary>
        public bool IsWaitingForNetworkedCopies => _expectedNetworkedCopies.Count > 0;

        /// <summary>
        /// Raised while preparing an avatar playback object, with the instantiated GameObject and the
        /// recorded "Meta ID" node name. The optional Meta integration subscribes to this to wire up
        /// the Meta avatar replay data writer without ScenePreparator referencing any Meta type.
        /// </summary>
        public event System.Action<GameObject, string> AvatarPlaybackSetup;

        private void Awake()
        {
            _controller = GetComponent<RecorderController>();
            RegisterReplayPrefabHandlers();
        }

        public void Start()
        {
            _controller = GetComponent<RecorderController>();
            // Again in Start: in Awake, NetworkManager.Singleton may not be set yet (Awake order between objects is
            // undefined), and then no handler is registered. Prefabs that already have one are skipped.
            RegisterReplayPrefabHandlers();
        }

        private void OnDestroy()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.PrefabHandler != null)
            {
                foreach (GameObject prefab in _replayPrefabHandlers.Keys)
                    networkManager.PrefabHandler.RemoveHandler(prefab);
                foreach (uint hash in _nestedObjectHandlerHashes)
                    networkManager.PrefabHandler.RemoveHandler(hash);
            }
            _replayPrefabHandlers.Clear();
            _nestedObjectHandlerHashes.Clear();
        }

        // Registered on every peer before any replay, so a client builds a replay copy that arrives at any time (also
        // while it joins late) through the handler. Prefabs the application already handles itself are left alone.
        internal void RegisterReplayPrefabHandlers()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (_controller == null || !_controller.spawnNetworkedPrefabsForReplay || networkManager == null ||
                networkManager.NetworkConfig?.Prefabs == null)
                return;

            foreach (NetworkPrefab networkPrefab in networkManager.NetworkConfig.Prefabs.Prefabs)
            {
                GameObject prefab = networkPrefab.Prefab;
                if (prefab == null || prefab.GetComponent<NetworkObject>() == null ||
                    prefab.GetComponent<NetworkUser>() != null || _replayPrefabHandlers.ContainsKey(prefab))
                    continue;

                var handler = new ReplayPrefabHandler(prefab, this);
                if (!networkManager.PrefabHandler.AddHandler(prefab, handler))
                    continue;
                _replayPrefabHandlers.Add(prefab, handler);

                foreach (NetworkObject nested in prefab.GetComponentsInChildren<NetworkObject>(true))
                {
                    if (nested.gameObject == prefab)
                        continue;
                    if (networkManager.PrefabHandler.AddHandler(nested.PrefabIdHash, new ReplayNestedObjectHandler(nested.PrefabIdHash, this)))
                        _nestedObjectHandlerHashes.Add(nested.PrefabIdHash);
                }
            }
        }

        // Same decision on every peer: the server spawns the copy, clients wait for it instead of creating their own.
        // Avatars (NetworkUser) stay local copies. Clients only receive the server's replay of the copy when networked
        // transforms are replayed on the server only.
        private bool ShouldSpawnNetworked(GameObject prefab)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            return prefab != null && _controller.spawnNetworkedPrefabsForReplay &&
                   _replayPrefabHandlers.ContainsKey(prefab) && !_controller.localPlayback &&
                   _controller.replayNetworkedTransformOnServerOnly && networkManager != null &&
                   networkManager.IsListening;
        }

        private static GameObject LoadReplayPrefab(string prefabLocation) =>
            prefabLocation.Contains("Resources/")
                ? Resources.Load<GameObject>(prefabLocation.Split("Resources/").Last().Replace(".prefab", ""))
                : null;

        /// <summary>
        /// Builds an inactive replay copy of a networked prefab. The server and every client use this, so the copies
        /// keep the same NetworkBehaviours. The parent is set locally on each peer, not by Netcode.
        /// </summary>
        internal static GameObject CreateNetworkedCopy(GameObject prefab, Transform parent, int recorderId)
        {
            bool prefabWasActive = prefab.activeSelf;
            prefab.SetActive(false);
            GameObject copy = Instantiate(prefab, parent);
            prefab.SetActive(prefabWasActive);
            Utils.RemoveCustomComponents(copy.transform, keepNetworkComponents: true);
            ReplayInstance.Mark(copy.transform, recorderId);
            foreach (NetworkObject networkObject in copy.GetComponentsInChildren<NetworkObject>(true))
                networkObject.AutoObjectParentSync = false;
            return copy;
        }

        private void SpawnOrExpectNetworkedCopy(ReplayGameObjectInformation information, GameObject prefab,
            Transform parent, string parentName)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager.IsServer)
            {
                GameObject copy = CreateNetworkedCopy(prefab, parent, RecorderId);
                AttachNetworkedCopy(information, copy, parentName);
                var spawnData = new ReplaySpawnData { isReplayCopy = true, recordedObjectId = information.id };
                networkManager.PrefabHandler.SetInstantiationData(copy, spawnData);
                copy.SetActive(true);
                copy.GetComponent<NetworkObject>().Spawn(true);

                // Netcode does not spawn NetworkObjects nested in a prefab with its root; clients resolve them through
                // ReplayNestedObjectHandler from the copy they built for the root.
                foreach (NetworkObject nested in copy.GetComponentsInChildren<NetworkObject>(true))
                {
                    if (nested.gameObject == copy)
                        continue;
                    networkManager.PrefabHandler.SetInstantiationData(nested, spawnData);
                    nested.Spawn(true);
                }
                return;
            }

            if (_arrivedNetworkedCopies.TryGetValue(information.id, out GameObject arrived) && arrived != null)
            {
                _arrivedNetworkedCopies.Remove(information.id);
                AttachNetworkedCopy(information, arrived, parentName);
                return;
            }

            _expectedNetworkedCopies[information.id] = new ExpectedNetworkedCopy
                { information = information, parentName = parentName };
        }

        // Names and parents a copy like the recorded object and matches it and its recorded children.
        private void AttachNetworkedCopy(ReplayGameObjectInformation information, GameObject copy, string parentName)
        {
            copy.transform.SetParent(ResolveRecordedParent(parentName), true);
            copy.name = information.gameObjectName;
            information.foundObject = copy;
            information.instantiated = true;
            MarkAsPlaybackGO(copy);
            // Only the server removes a spawned copy (despawning it on every peer); clients never destroy it themselves.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                TrackInstantiatedHierarchy(copy);
            SceneGraphTraversalGameObjectExistenceCheck(copy, parentName);
        }

        private Transform ResolveRecordedParent(string parentName)
        {
            var parentInfo = replayGameObjectInformations.FirstOrDefault(i => i.hierarchyName == parentName);
            if (parentInfo != null && parentInfo.foundObject != null)
                return parentInfo.foundObject.transform;
            return string.IsNullOrEmpty(parentName) ? _controller.replayRoot : null;
        }

        /// <summary>Client: a replay copy spawned by the server arrived through the ReplayPrefabHandler.</summary>
        public void OnNetworkedCopyArrived(int recordedObjectId, GameObject copy)
        {
            _networkedCopiesById[recordedObjectId] = copy;
            if (_expectedNetworkedCopies.TryGetValue(recordedObjectId, out ExpectedNetworkedCopy expected))
            {
                _expectedNetworkedCopies.Remove(recordedObjectId);
                AttachNetworkedCopy(expected.information, copy, expected.parentName);
                return;
            }

            // Arrived before this peer prepared its scene (or after it stopped waiting): matched when the scene is prepared.
            _arrivedNetworkedCopies[recordedObjectId] = copy;
        }

        public void OnNetworkedCopyDestroyed(GameObject copy)
        {
            foreach (var entry in _arrivedNetworkedCopies.Where(e => e.Value == copy).ToList())
                _arrivedNetworkedCopies.Remove(entry.Key);
            foreach (var entry in _networkedCopiesById.Where(e => e.Value == copy).ToList())
                _networkedCopiesById.Remove(entry.Key);
        }

        /// <summary>Client: the not yet spawned NetworkObject with the given prefab hash inside a received replay copy.</summary>
        public NetworkObject FindNestedNetworkObject(int recordedObjectId, uint prefabIdHash)
        {
            if (!_networkedCopiesById.TryGetValue(recordedObjectId, out GameObject copy) || copy == null)
                return null;
            return copy.GetComponentsInChildren<NetworkObject>(true)
                .FirstOrDefault(n => n.gameObject != copy && !n.IsSpawned && n.PrefabIdHash == prefabIdHash);
        }

        /// <summary>Stops waiting for networked replay copies and returns the names of those that are still missing.</summary>
        public string StopWaitingForNetworkedCopies()
        {
            string missing = string.Join(", ", _expectedNetworkedCopies.Values.Select(e => e.information.hierarchyName));
            _expectedNetworkedCopies.Clear();
            return missing;
        }

        private void DebugScenePreparationLog(string message)
        {
            if (_controller == null || !RecordingLogger.IsEnabled(LogLevel.Debug))
                return;

            RecordingLogger.LogDebug(GetType().Name, "[ScenePreparationDebug][frame=" + Time.frameCount +
                      "][time=" + Time.realtimeSinceStartup.ToString("F3") + "] " + message, this);
        }

        private bool HandleMissingGameObject()
        {
            if (RecordingLogger.IsEnabled(LogLevel.Info))
                RecordingLogger.LogInfo(GetType().Name, "Trying to handle missing objects.", this);

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

            if (RecordingLogger.IsEnabled(LogLevel.Info))
                RecordingLogger.LogInfo(GetType().Name, "Scene Graph existence check finished.", this);

            bool error = false;
            int leftToOthers = 0;
            foreach (var info in replayGameObjectInformations)
            {
                if (info.replayedByOtherRecorder)
                    leftToOthers++;
                else if (info.foundObject == null)
                    error = true;
            }
            if (leftToOthers > 0)
                RecordingLogger.LogInfo(GetType().Name, leftToOthers +
                    " recorded objects are already replayed by another recorder and are left to it.", this);

            // Create parents before their children: a missing object is placed under its recorded parent only if that
            // parent already exists, otherwise it ends up at the scene root and its replay name no longer matches the
            // recording. Within a depth, prefabs come first, so their recorded children are found in the instance
            // instead of being created as empty objects. OrderBy is stable; List.Sort is not, and for more than 16
            // entries it shuffles entries with equal keys (most objects are first seen at the same time).
            replayGameObjectInformations = replayGameObjectInformations
                .OrderBy(i => HierarchyDepth(i.hierarchyName))
                .ThenByDescending(i => i.prefabLocation.Length > 0)
                .ThenBy(i => i.firstSeenTime)
                .ToList();

            // Only instantiate objects that are missing from the scene when explicitly enabled. When
            // disabled, objects already matched above still play back; missing ones are simply skipped.
            if (!_controller.instantiateMissingObjects)
                return error;

            // Recorded children of a prefab that the server spawns over the network arrive with that prefab.
            List<string> networkedCopyRoots = replayGameObjectInformations
                .Where(i => i.foundObject == null && i.prefabLocation.Length > 0 &&
                            ShouldSpawnNetworked(LoadReplayPrefab(i.prefabLocation)))
                .Select(i => i.hierarchyName + "/").ToList();

            foreach (var information in replayGameObjectInformations)
            {
                if (!information.instantiated)
                {
                    if (networkedCopyRoots.Any(root => information.hierarchyName.StartsWith(root, StringComparison.Ordinal)))
                        continue;

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

        private static int HierarchyDepth(string hierarchyName)
        {
            int depth = 0;
            foreach (char c in hierarchyName)
                if (c == '/')
                    depth++;
            return depth;
        }

        private void TryInstantiatePrefab(ReplayGameObjectInformation information, Transform parent, string parentName)
        {
            string prefabName = "";
            if (information.prefabLocation.Contains("Resources/")){
                prefabName = information.prefabLocation.Split("Resources/").Last().Replace(".prefab", "");
                if (RecordingLogger.IsEnabled(LogLevel.Info))
                    RecordingLogger.LogInfo(GetType().Name, ": " + information.gameObjectName + ", prefab location : " + prefabName +
                          ", original location: " + information.prefabLocation, this);
			} else {
				RecordingLogger.LogWarning(GetType().Name, "The prefab that would be instantiated for replay is not in a Resources folder!", this);
			}

            GameObject go = prefabName != "" ? Resources.Load<GameObject>(prefabName) : null;
            if (go != null && ShouldSpawnNetworked(go))
            {
                SpawnOrExpectNetworkedCopy(information, go, parent, parentName);
                return;
            }

            if (go != null)
            {
                if (RecordingLogger.IsEnabled(LogLevel.Info))
                    RecordingLogger.LogInfo(GetType().Name, "Prefab could be loaded.", this);
                // Instantiate INACTIVE: the recorded user prefab (e.g. REPLAY-HMDUser) contains an
                // OVRCameraRig + Camera. If instantiated active, their Awake/OnEnable register a second
                // XR camera rig with the renderer for a frame before RemoveCustomComponents strips them,
                // and the async render thread crashes in the Adreno Vulkan driver (vkCmdBeginRenderPass2).
                // Deactivating the source prefab first makes the clone instantiate inactive, so no Awake
                // runs until the camera/network components have been removed; it stays inactive afterwards.
                bool prefabWasActive = go.activeSelf;
                go.SetActive(false);
                GameObject newGo = Instantiate(go, parent);
                go.SetActive(prefabWasActive);
                if (RecordingLogger.IsEnabled(LogLevel.Info))
                    RecordingLogger.LogInfo(GetType().Name, "Removing custom components.", this);
                Utils.RemoveCustomComponents(newGo.transform);
                ReplayInstance.Mark(newGo.transform, RecorderId);
                if (RecordingLogger.IsEnabled(LogLevel.Info))
                    RecordingLogger.LogInfo(GetType().Name, "Removing custom components finished.", this);
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
                if (RecordingLogger.IsEnabled(LogLevel.Warning))
                    RecordingLogger.LogWarning(GetType().Name, "The gameobject at path: " + information.prefabLocation +
                                     " could not be loaded! Make sure it is in a resource folder!", this);
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
                // newGo is still inactive at this point (it is only activated at the end of this
                // method). GetComponentInChildren skips inactive objects by default and would
                // return null here -> NullReferenceException on userNameText.text. Include inactive
                // children and null-guard the result.
                TextMeshProUGUI userNameText = userNameTextGo.GetComponentInChildren<TextMeshProUGUI>(true);
                if (userNameText != null)
                {
                    string cleanName = information.gameObjectName.Replace(" [Local]", "").Replace(" [Remote]", "");
                    userNameText.text = "[Rec] " + cleanName;
                }
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
            ReplayInstance.Mark(newGo.transform, RecorderId);
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
                if (RecordingLogger.IsEnabled(LogLevel.Info))
                    RecordingLogger.LogInfo(GetType().Name, "Trying to get ids of all sound sources contained in recording.", this);
                DebugScenePreparationLog("HandleAudioSources before GetRecordingSoundSources. recorderId=" +
                                         _controller.RecorderID + ", maxSoundSources=" + MaxSoundSources);
                int count = GetRecordingSoundSources(_controller.RecorderID, (IntPtr)u, MaxSoundSources);
                string[] retrievedSoundIds = new string[Mathf.Max(0, Mathf.Min(count, MaxSoundSources))];
                for (int i = 0; i < retrievedSoundIds.Length; ++i)
                    retrievedSoundIds[i] = _soundIDs[i].ToString();
                DebugScenePreparationLog("HandleAudioSources after GetRecordingSoundSources. count=" + count +
                                         ", ids=[" + string.Join(", ", retrievedSoundIds) + "]");
                if (RecordingLogger.IsEnabled(LogLevel.Info))
                    RecordingLogger.LogInfo(GetType().Name, "Found: " + count + " sound sources.", this);

                if (count >= 0)
                {
                    for (int k = 0; k < count; ++k)
                    {
                        int soundSourceId = _soundIDs[k];
                        DebugScenePreparationLog("HandleAudioSources processing sound source index=" + k +
                                                 ", id=" + soundSourceId);
                        // Sound id 0 is the microphone, 1 the audio listener mix, 2 and up the recorded AudioSources.
                        bool replaySound = soundSourceId == 0 ? _controller.replayMicrophone
                            : soundSourceId == 1 ? _controller.replayAudioListener
                            : _controller.replayAudioSources;
                        if (!replaySound)
                        {
                            if (RecordingLogger.IsEnabled(LogLevel.Info))
                                RecordingLogger.LogInfo(GetType().Name, "Recorded sound " + soundSourceId +
                                    " is not played back (" + (soundSourceId == 0 ? "Replay microphone"
                                        : soundSourceId == 1 ? "Replay audio listener" : "Replay audio sources") +
                                    " is off).", this);
                            continue;
                        }

                        GameObject newGo = new GameObject();
                        newGo.name = "SoundSource:" + soundSourceId;
                        ReplayInstance.Mark(newGo.transform, RecorderId);
                        AudioSourceRecorder recorder = newGo.AddComponent<AudioSourceRecorder>();
                        recorder.SetId(soundSourceId);
                        // The listener mix was heard at the listener, so it is replayed without spatialization.
                        if (soundSourceId == 1)
                            recorder.replaySpatialBlend = 0f;
                        recorder.Controller = _controller;
                        _instantiatedGameObjects.Add(newGo);
                    }
                }
                else
                {
                    if (RecordingLogger.IsEnabled(LogLevel.Error))
                        RecordingLogger.LogError(GetType().Name, "Could not retrieve sound sources", this);
                    return true;
                }
            }

            if (RecordingLogger.IsEnabled(LogLevel.Info))
                RecordingLogger.LogInfo(GetType().Name, "Sound source retrieval and setup finished.", this);
            DebugScenePreparationLog("HandleAudioSources finished.");

            return false;
        }

        public Dictionary<string, GameObject> GetNamePresent()
        {
            return replayGameObjectInformations.ToDictionary(i => i.hierarchyName, i => i.foundObject);
        }

        #region Audio Source Control

        // Live audio within this recorder's recording scope is paused during its replay (everything when it records all
        // objects), so another recorder's objects keep sounding. Only the sources paused here are resumed.
        private readonly List<AudioSource> _pausedAudioSources = new List<AudioSource>();

        private void PauseAllAudioSources()
        {
            AudioSource[] audioSources = (AudioSource[])FindObjectsOfType(typeof(AudioSource));

            foreach (AudioSource audioSource in audioSources)
            {
                if (audioSource.gameObject.name != "UserSpeaker" &&
                    !audioSource.gameObject.name.Contains("Speaker for Player") &&
                    _controller.IsInRecordingScope(audioSource.gameObject))
                {
                    audioSource.Pause();
                    _pausedAudioSources.Add(audioSource);
                }
            }
        }

        private void ResumeAllAudioSources()
        {
            foreach (AudioSource audioSource in _pausedAudioSources)
                if (audioSource != null)
                    audioSource.UnPause();
            _pausedAudioSources.Clear();
        }

        #endregion
        
        public unsafe void PrepareReplayScene()
        {
            // Spawn network prefabs may have been switched on after Start (e.g. in the Inspector); registering skips
            // prefabs that already have a handler.
            RegisterReplayPrefabHandlers();
            PauseAllAudioSources();
            
            replayGameObjectInformations.Clear();
            _expectedNetworkedCopies.Clear();
            DebugScenePreparationLog("PrepareReplayScene entered. recorderId=" + _controller.RecorderID +
                                     ", replayRoot=" +
                                     (_controller.replayRoot == null ? "<null>" : _controller.replayRoot.name));
            if (RecordingLogger.IsEnabled(LogLevel.Info))
                RecordingLogger.LogInfo(GetType().Name, "Trying to get recording gameobjects", this);

            DebugScenePreparationLog("PrepareReplayScene before GetRecordingTransformCount.");
            int transformCount = GetRecordingTransformCount(_controller.RecorderID);
            DebugScenePreparationLog("PrepareReplayScene after GetRecordingTransformCount. transformCount=" +
                                     transformCount);
            if (transformCount <= 0)
            {
                RecordingLogger.LogWarning(GetType().Name,
                    "No transforms could be retrieved from the recording for scene preparation. Is the recording file broken?", this);
                return;
            }

            int[] transformIDs = new int[transformCount];
            int retrievedTransformIdCount;
            fixed (int* t = transformIDs)
            {
                DebugScenePreparationLog("PrepareReplayScene before GetRecordingTransformIDs. requestedMaxSize=" +
                                         transformCount);
                retrievedTransformIdCount = GetRecordingTransformIDs(_controller.RecorderID, (IntPtr)t, transformCount);
            }
            DebugScenePreparationLog("PrepareReplayScene after GetRecordingTransformIDs. returnedCount=" +
                                     retrievedTransformIdCount + ", ids=[" + string.Join(", ", transformIDs) + "]");

            int maxSize = 10000;
            for (int i = 0; i < transformCount; ++i)
            {
                int currentID = transformIDs[i];
                DebugScenePreparationLog("PrepareReplayScene reading metadata for transform index=" + i +
                                         ", id=" + currentID + ".");
                // DIAGNOSTIC: the last [PrepLoop] line before a crash names the exact object
                // (index + id), the hierarchy name read so far, and which getter ran next.
                if (RecordingLogger.IsEnabled(LogLevel.Debug)) RecordingLogger.LogDebug(GetType().Name, $"[PrepLoop] i={i}/{transformCount} id={currentID} step=hierarchyName", this);
                DebugScenePreparationLog("PrepareReplayScene before GetGameObjectHierarchyNameByID. id=" + currentID +
                                         ", maxSize=" + maxSize);
                StringBuilder buffer = new StringBuilder(maxSize);
                int length = GetGameObjectHierarchyNameByID(_controller.RecorderID, buffer, maxSize, currentID);
                string gameobjectHierarchyPath = length > 0 ? buffer.ToString() : "";
                DebugScenePreparationLog("PrepareReplayScene after GetGameObjectHierarchyNameByID. id=" + currentID +
                                         ", length=" + length + ", value=\"" + gameobjectHierarchyPath + "\"");

                // Mesh path and prefab location are only needed to instantiate objects that are missing
                // from the scene. When instantiation is disabled, skip these native queries entirely;
                // playback then only drives objects already present in the scene.
                string gameobjectMeshPath = "";
                string gameobjectPrefabLocation = "";
                string gameobjectComponents = "";
                if (_controller.instantiateMissingObjects)
                {
                    if (RecordingLogger.IsEnabled(LogLevel.Debug)) RecordingLogger.LogDebug(GetType().Name, $"[PrepLoop] i={i} id={currentID} step=meshPath name='{gameobjectHierarchyPath}'", this);
                    buffer = new StringBuilder(maxSize);
                    DebugScenePreparationLog("PrepareReplayScene before GetGameObjectMeshPathByID. id=" + currentID +
                                             ", maxSize=" + maxSize);
                    length = GetGameObjectMeshPathByID(_controller.RecorderID, buffer, maxSize, currentID);
                    gameobjectMeshPath = length > 0 ? buffer.ToString() : "";
                    DebugScenePreparationLog("PrepareReplayScene after GetGameObjectMeshPathByID. id=" + currentID +
                                             ", length=" + length + ", value=\"" + gameobjectMeshPath + "\"");

                    if (RecordingLogger.IsEnabled(LogLevel.Debug)) RecordingLogger.LogDebug(GetType().Name, $"[PrepLoop] i={i} id={currentID} step=prefab", this);
                    buffer = new StringBuilder(maxSize);
                    DebugScenePreparationLog("PrepareReplayScene before GetGameObjectPrefabByID. id=" + currentID +
                                             ", maxSize=" + maxSize);
                    length =  GetGameObjectPrefabByID(_controller.RecorderID, buffer, maxSize, currentID);
                    gameobjectPrefabLocation = length > 0 ? buffer.ToString() : "";
                    DebugScenePreparationLog("PrepareReplayScene after GetGameObjectPrefabByID. id=" + currentID +
                                             ", length=" + length + ", value=\"" + gameobjectPrefabLocation + "\"");
                    
                    if (RecordingLogger.IsEnabled(LogLevel.Debug)) RecordingLogger.LogDebug(GetType().Name, $"[PrepLoop] i={i} id={currentID} step=components", this);
                    buffer = new StringBuilder(maxSize);
                    DebugScenePreparationLog("PrepareReplayScene before GetGameObjectComponentsByID. id=" + currentID +
                                             ", maxSize=" + maxSize);
                    length =  GetGameObjectComponentsByID(_controller.RecorderID, buffer, maxSize, currentID);
                    gameobjectComponents = length > 0 ? buffer.ToString() : "";
                    DebugScenePreparationLog("PrepareReplayScene after GetGameObjectComponentsByID. id=" + currentID +
                                             ", length=" + length + ", value=\"" + gameobjectComponents + "\"");
                }

                if (RecordingLogger.IsEnabled(LogLevel.Debug)) RecordingLogger.LogDebug(GetType().Name, $"[PrepLoop] i={i} id={currentID} step=build comp='{gameobjectComponents}'", this);
                DebugScenePreparationLog("PrepareReplayScene before GetGameObjectFirstSeenTimeByID. id=" + currentID);
                float firstSeenTime = GetGameObjectFirstSeenTimeByID(_controller.RecorderID, currentID);
                DebugScenePreparationLog("PrepareReplayScene after GetGameObjectFirstSeenTimeByID. id=" + currentID +
                                         ", firstSeenTime=" + firstSeenTime);
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
                DebugScenePreparationLog("PrepareReplayScene stored metadata for transform index=" + i +
                                         ", id=" + currentID +
                                         ", gameObjectName=\"" + gameObjectInformation.gameObjectName +
                                         "\", componentCount=" + gameObjectInformation.components.Count);
            }
            DebugScenePreparationLog("PrepareReplayScene finished metadata retrieval. storedCount=" +
                                     replayGameObjectInformations.Count);

            DebugScenePreparationLog("PrepareReplayScene before HandleMissingGameObject.");
            bool error = HandleMissingGameObject();
            DebugScenePreparationLog("PrepareReplayScene after HandleMissingGameObject. error=" + error);

            if (RecordingLogger.IsEnabled(LogLevel.Error))
            {
                if (!error)
                    RecordingLogger.LogInfo(GetType().Name, "All gameObjects from the recording are present in the current scene graph!", this);
                else
                    RecordingLogger.LogError(GetType().Name,
                        "Not all gameObjects from the recording are present in the current scene graph!", this);
            }

            if (_controller.replayAudio)
            {
                if (RecordingLogger.IsEnabled(LogLevel.Info))
                    RecordingLogger.LogInfo(GetType().Name, "Trying to handle missing audio sources.", this);
                DebugScenePreparationLog("PrepareReplayScene before HandleAudioSources.");
                error = HandleAudioSources();
                DebugScenePreparationLog("PrepareReplayScene after HandleAudioSources. error=" + error);

                if (RecordingLogger.IsEnabled(LogLevel.Error))
                {
                    if (!error)
                        RecordingLogger.LogInfo(GetType().Name, "All sound sources from the recording are now present in the current scene graph!", this);
                    else
                        RecordingLogger.LogError(GetType().Name,
                            "Not all sound sources from the recording are present in the current scene graph!", this);
                }
            }
            else
            {
                DebugScenePreparationLog("PrepareReplayScene skipped HandleAudioSources because replayAudio=false.");
            }
        }

        public void CleanReplayScene()
        {
            RecordingLogger.LogInfo(GetType().Name, "Cleaning replay scene by destroying objects that were instantiated for playback.", this);

            // Snapshot the instantiated objects and reset the field immediately. Destruction is deferred
            // by dissolveTime, so a new PrepareReplayScene started within that window must not append into
            // the list that is about to be destroyed (otherwise the next replay's objects get killed too).
            List<GameObject> toDelete = _instantiatedGameObjects;
            _instantiatedGameObjects = new List<GameObject>();
            _expectedNetworkedCopies.Clear();

            foreach (var go in toDelete)
            {
                if (go != null)
                    GameObject.Destroy(go, dissolveTime);
            }

            // TODO: when playback changed the parents of already present objects then the cleanup does not reset to original parent!
            RecordingLogger.LogInfo(GetType().Name, "Cleaning replay scene done.", this);
            ResumeAllAudioSources();
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
                // Another recorder that is replaying this object keeps it: this replay neither drives it nor creates
                // a copy. A recorder that only records it (e.g. a recording of a running replay) does not block it.
                TransformRecorder[] transformRecorders = currentGameObj.GetComponents<TransformRecorder>();
                bool found = false;
                bool replayedByOther = false;

                foreach (var transformRecorder in transformRecorders)
                {
                    RecorderController owner = transformRecorder.controller;
                    if (owner == null)
                        continue;
                    if (owner.RecorderID == _controller.RecorderID)
                        found = true;
                    else if (owner.recorderState != null &&
                             (owner.recorderState.currentState == State.Replaying ||
                              owner.recorderState.currentState == State.PreparingReplay))
                        replayedByOther = true;
                }

                info.instantiated = true;
                if (found || !replayedByOther)
                    info.foundObject = currentGameObj;
                else
                    info.replayedByOtherRecorder = true;
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
