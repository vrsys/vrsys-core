using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Recording
{
    public class TransformRecorder : Recorder
    {
        public struct RerecordSample
        {
            public float time;
            public float[] matrix;
            public int[] info;
        }

        private struct TransformCapture
        {
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
            public Vector3 globalPosition;
            public Quaternion globalRotation;
            public Vector3 globalScale;
            public bool active;
            public int parentId;
        }

        private List<RerecordSample> _rerecBuffer = new List<RerecordSample>();
        private float _rerecLastPushTime = -1.0f;
        private TransformCapture _rerecLastCapture;
        private bool _rerecHasLastCapture = false;

        [DllImport("RecordingPlugin")]
        private static extern bool RegisterObjectMeshPath(int recorderId, int uuid, string path, int pathLength, float time);
        
        [DllImport("RecordingPlugin")]
        private static extern bool RegisterObjectComponents(int recorderId, int uuid, string componentString, int componentStringLength);
        
        [DllImport("RecordingPlugin")]
        private static extern bool RegisterObjectPrefab(int recorderId, int uuid, string prefab, int prefabLength, float time);
        
        [DllImport("RecordingPlugin")]
        private static extern bool RecordObjectAtTimestamp(int recorderId, string objectName, int objectNameLength, int uuid, float[] localMatrix, float timeStamp, int[] objectInformation);
        
        [DllImport("RecordingPlugin")]
        private static extern bool GetTransformAndInformationAtTime(int recorderId, string objectName, int objectNameLength, int uuid, float currentTime, IntPtr data, IntPtr objectInformation);
        
        private bool _isRecorded = false;
        private int playbackFramerate = 60;
        private int _parent;
        private Transform _parentTransform;
        private Transform _currentParentTransform;
        private Transform _transform;
        private bool _active = false;
        private float _lastRecordTime;
        private bool _firstPreview = true;
        private int _isMesh = -1;

        private int _presentInRecording = -1;
        
        private Vector3 _originalLocalPos;
        private Vector3 _originalLocalSca;
        private Quaternion _originalLocalRot;
        private Transform _originalParent;
        private bool _originalStateCaptured;
        
        private Vector3 _lastLocalPos;
        private Vector3 _lastLocalSca;
        private Quaternion _lastLocalRot;
        
        private Vector3 _lastGlobalPos;
        private Vector3 _lastGlobalSca;
        private Quaternion _lastGlobalRot;
        
        private Vector3 _initalPreviewPos;
        private Vector3 _initialPreviewSca;
        private Quaternion _initialPreviewRot;
        
        private float[] _matrixDTO = new float[20];
        private int[] _infoDTO = new int[2];
        private float[] _positionsDTO = new float[4 * 300];

        private string _name = "";
        
        private AudioSource _source;
        private TrailRenderer _renderer;

        private bool originalIdFound;
        private int originalId;

        private bool anonymiseName;
        private string trueName;
        private string anonymisedName;

        private const float ReplayBoundaryPaddingSeconds = 0.001f;
        private float lastErrorLog = 0.0f;
        
        public override bool Record(float recordTime)
        {
            if (gameObject == null)
                return false;
            
            _transform = gameObject.transform;
            
            if (_name == "")
            {
                _name = Utils.GetObjectName(gameObject);
                _name = Utils.RemoveDiacritics(_name);
            }
            
            bool result = true;

            if(id == 99999)
                id = gameObject.GetInstanceID();

            int parentID = 0;
            if (_transform.parent != null)
                parentID = _transform.parent.gameObject.GetInstanceID();

            bool firstSeen = false;
            
            if (!_isRecorded)
            {
                _isRecorded = true;
                firstSeen = true;
                
                // TODO: change to recording only prefabs!
                string meshPath = " ";
                MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
                if (meshFilter != null &&  meshFilter.sharedMesh != null)
                    meshPath = meshFilter.sharedMesh.name;

                RegisterObjectMeshPath(controller.RecorderID, id, meshPath, meshPath.Length, recordTime);

                Component[] components = gameObject.GetComponents(typeof(Component));
                string componentString = "";
                string pattern = @"\(([^)]+)\)";
                Regex rg = new Regex(pattern);
                foreach (Component component in components)
                {
                    if(component == null)
                        continue;
                    
                    string c = component.ToString();
                    MatchCollection componentNames = rg.Matches(c);
                    for (int i = 0; i < componentNames.Count; i++)
                    {
                        string cs = componentNames[i].Value;
                        cs = cs.Replace("(", "");
                        cs = cs.Replace(")", "");
                        componentString += cs + ",";
                    }
                }

                RegisterObjectComponents(controller.RecorderID, id, componentString, componentString.Length);

                RecordingPrefabInformation[] information = GetComponents<RecordingPrefabInformation>();
                if (information.Length > 0)
                {
                    foreach (var prefabInfo in information)
                    {
                        // TODO: For some reason the prefab path is empty for the local player. Why?
                        if (controller.debugLogs)
                            ExtendedLogger.LogInfo(GetType().Name, "Prefab path: " + prefabInfo.correspondingPrefab.assetPath + ", for object: " + gameObject.name, this);
                        if (prefabInfo.correspondingPrefab != null && prefabInfo.correspondingPrefab.assetPath.Length > 0)
                        {
                            RegisterObjectPrefab(controller.RecorderID, id, prefabInfo.correspondingPrefab.assetPath,
                                prefabInfo.correspondingPrefab.assetPath.Length, _lastRecordTime);
                            break;
                        }
                    }
                }
                  
            }
            
            TransformCapture currentCapture = CaptureCurrentTransform(parentID);
            bool changeInTransform;
            bool objectChanged = HasObjectChanged(LastRecordedCapture(), currentCapture, firstSeen, controller.recordOnLocalTransformChangesOnly, out changeInTransform);

            if (objectChanged)
            {
                bool teleportation = false;
                // This check is being done to avoid problems during jumps/teleportations when the object does not move
                // often and then suddenly does a huge "jump". The reason for this is that it can produce artifacts during
                // the replay when the interpolation is being done between two transforms which have a huge time stamp difference
                if (changeInTransform && !firstSeen)
                {
                    float posDif = (_lastLocalPos - currentCapture.localPosition).magnitude;
                    float scaDif = (_lastLocalSca - currentCapture.localScale).magnitude;

                    // Elapsed time since the last *recorded* sample. While the object is static no
                    // samples are written, so a value larger than one sample step means we skipped a
                    // static period and must insert a hold keyframe to stop the plugin from smearing
                    // the first motion delta across the whole gap.
                    float timeDif = recordTime - _lastRecordTime;

                    if ((timeDif > 1.5f / (float) controller.transformRecordingStepsPerSecond || (posDif > 1f || scaDif > 0.3f)) && recordTime > 0.001f)
                    {
                        teleportation = true;
                        if (controller.debugLogs)
                            ExtendedLogger.LogInfo(GetType().Name, "Gap/teleport keyframe inserted. Pos Dif: " + posDif + ", Scale Dif: " + scaDif + ", Time Dif: " + timeDif + ", Time: " + recordTime + ", uuid: " + id, this);
                    }
                }
             
                if (teleportation)
                {
                    fillDTOLastData();

                    float teleportTime = recordTime - (1.0f / 1000.0f);
                    if (teleportTime > 0.0f)
                    {
                        result = RecordObjectAtTimestamp(controller.RecorderID, _name, _name.Length, id, _matrixDTO, teleportTime, _infoDTO);
                        
                        if (!result && controller.debugLogs)
                            ExtendedLogger.LogError(GetType().Name, "Recording teleportation object: Failed, " + gameObject.name, this);
                    }
                }

                fillDTOCurrentData(currentCapture);
                
                result = RecordObjectAtTimestamp(controller.RecorderID, _name, _name.Length, id, _matrixDTO, recordTime, _infoDTO);
                
                if (!result && controller.debugLogs)
                {
                    ExtendedLogger.LogError(GetType().Name, "Recording object: " + _name + " Failed", this);
                }
                else
                {
                    _transform.hasChanged = false;
                    
                    _lastRecordTime = recordTime;
                }
            }

            return result;
        }

        unsafe public override bool Replay(float replayTime)
        {
            if (inRerecordingMode)
                return true;

            replayTime = ClampReplayTimeForNativeBuffer(replayTime);
            
            if (Mathf.Abs(replayTime - _lastReplayTime) < 1.0f/playbackFramerate && _transform != null)
            {
                _transform.localScale = _lastLocalSca;
                _transform.localRotation = _lastLocalRot;
                _transform.localPosition = _lastLocalPos; 
                
                return true;
            }

            if(_transform == null)
                _transform = gameObject.transform;
            
            if (_name == "")
            {
                _name = Utils.GetObjectName(gameObject, controller.replayRoot);
                string pattern = "\\[Rec" + controller.RecorderID + "\\]$";
                _name = Regex.Replace(_name, pattern, "");
                string pattern2 = "\\[Rec" + controller.RecorderID + "\\]\\/";
                _name = Regex.Replace(_name, pattern2, "/");
                
                _originalLocalPos = _transform.localPosition;
                _originalLocalRot = _transform.localRotation;
                _originalLocalSca = _transform.localScale;
                // Capture the parent before any reparenting is replayed below. Playback may grab/reparent
                // this pre-existing object (TransformRecorder.SetParent), so the original parent must be
                // remembered here to restore the hierarchy position when the replay stops (see OnDestroy).
                _originalParent = _transform.parent;
                _originalStateCaptured = true;
            }

            if (_presentInRecording == -1)
                _presentInRecording = controller.RecordedObjectPresent.ContainsKey(_name) ? 1 : 0;

            if (_presentInRecording == 0)
                return true;

            if(id == 99999)
                id = gameObject.GetInstanceID();

            if (recorderId == 99999)
                recorderId = controller.RecorderID;

            _currentParentTransform = _transform.parent;

            if (controller.replayHierarchyChanges)
            {
                if (_currentParentTransform != null && _currentParentTransform != _parentTransform && controller.recorderState.newIdOriginalId.ContainsKey(_currentParentTransform.gameObject.GetInstanceID()))
                {
                    _parent = controller.recorderState.newIdOriginalId[_currentParentTransform.gameObject.GetInstanceID()];
                    _parentTransform = _currentParentTransform;
                }
                else if (_currentParentTransform == null)
                    _parent = 0;
            }

            fixed (float* p = _matrixDTO)
            {
                fixed (int* u = _infoDTO)
                {
                    bool result = GetTransformAndInformationAtTime(recorderId, _name, _name.Length, id, replayTime, (IntPtr) p, (IntPtr) u);

                    if (!result)
                    {
                        if (Time.time - lastErrorLog > 10.0f)
                        {
                            lastErrorLog = Time.time;
                            if(controller.debugLogs)
                                ExtendedLogger.LogError(GetType().Name, "Could not get replay transform for: " + _name, this);
                        }
                    }
                    else
                    {
                        if (!originalIdFound)
                        {
                            originalId = GetOriginalID(controller.RecorderID, _name, _name.Length, id);
                            originalIdFound = true;
                            controller.AddOriginalIdGameobject(originalId, id, gameObject);
                        }

                        _lastLocalPos.x = _matrixDTO[0]; _lastLocalPos.y = _matrixDTO[1]; _lastLocalPos.z = _matrixDTO[2];
                        _lastLocalRot.x = _matrixDTO[4]; _lastLocalRot.y = _matrixDTO[5]; _lastLocalRot.z = _matrixDTO[6]; _lastLocalRot.w = _matrixDTO[3];
                        _lastLocalSca.x = _matrixDTO[7]; _lastLocalSca.y = _matrixDTO[8]; _lastLocalSca.z = _matrixDTO[9];

                        bool active = _infoDTO[0] > 0;
                        int newParentUUID = _infoDTO[1];

                        if(!_transform.localScale.Equals(_lastLocalSca))
                            _transform.localScale = _lastLocalSca;
                        if(!_transform.localRotation.Equals(_lastLocalRot))
                            _transform.localRotation = _lastLocalRot;
                        if(!_transform.localPosition.Equals(_lastLocalPos))
                            _transform.localPosition = _lastLocalPos;

                        if (gameObject.activeSelf != active)
                            gameObject.SetActive(active);
                        
                        if (controller.replayHierarchyChanges)
                        {
                            if (newParentUUID != -99999 && _parent != newParentUUID)
                            {
                                if (controller.recorderState.originalIdGameObjects.ContainsKey(newParentUUID))
                                {
                                    Transform parentTransform = controller.recorderState.originalIdGameObjects[newParentUUID].transform;
                                    if (newParentUUID != 0 && parentTransform != null)
                                    {
                                        _transform.SetParent(parentTransform);
                                        _parentTransform = parentTransform;
                                        _parent = newParentUUID;
                                    }
                                    else
                                    {
                                        // A reparent-to-root during recording maps to the replay anchor
                                        // during playback: the recording's root is the configured replayRoot
                                        // (cf. ScenePreparator, which parents top-level recorded objects under
                                        // it). SetParent(null) when no anchor is set preserves scene-root behaviour.
                                        _transform.SetParent(controller.replayRoot);
                                        _parentTransform = controller.replayRoot;
                                        _parent = 0;
                                    }
                                }

                                if (newParentUUID == 0)
                                {
                                    _transform.SetParent(controller.replayRoot);
                                    _parentTransform = controller.replayRoot;
                                    _parent = 0;
                                }
                            }
                        }
                    }

                    _lastReplayTime = replayTime;
                    return result;
                }
            }
        }

        private float ClampReplayTimeForNativeBuffer(float replayTime)
        {
            float duration = controller != null && controller.recorderState != null
                ? controller.recorderState.recordingDuration
                : -1.0f;

            if (duration > ReplayBoundaryPaddingSeconds * 2.0f)
                return Mathf.Clamp(replayTime, ReplayBoundaryPaddingSeconds, duration - ReplayBoundaryPaddingSeconds);

            return Mathf.Max(0.0f, replayTime);
        }

        bool fillDTOCurrentData()
        {
            int parentId = transform.parent != null ? transform.parent.gameObject.GetInstanceID() : 0;
            fillDTOCurrentData(CaptureCurrentTransform(parentId));
            return true;
        }

        bool fillDTOCurrentData(TransformCapture capture)
        {
            StoreLastRecordedCapture(capture);
            fillDTOLastData();
            return true;
        }
        
        bool fillDTOLastData()
        {
            PackTransform(LastRecordedCapture(), _matrixDTO, _infoDTO);
            return true;
        }

        private TransformCapture CaptureCurrentTransform(int parentId)
        {
            _transform = gameObject.transform;

            return new TransformCapture
            {
                localPosition = _transform.localPosition,
                localRotation = _transform.localRotation,
                localScale = _transform.localScale,
                globalPosition = _transform.position,
                globalRotation = _transform.rotation,
                globalScale = _transform.lossyScale,
                active = gameObject.activeSelf,
                parentId = parentId
            };
        }

        private TransformCapture LastRecordedCapture()
        {
            return new TransformCapture
            {
                localPosition = _lastLocalPos,
                localRotation = _lastLocalRot,
                localScale = _lastLocalSca,
                globalPosition = _lastGlobalPos,
                globalRotation = _lastGlobalRot,
                globalScale = _lastGlobalSca,
                active = _active,
                parentId = _parent
            };
        }

        private void StoreLastRecordedCapture(TransformCapture capture)
        {
            _lastLocalPos = capture.localPosition;
            _lastLocalRot = capture.localRotation;
            _lastLocalSca = capture.localScale;
            _lastGlobalPos = capture.globalPosition;
            _lastGlobalRot = capture.globalRotation;
            _lastGlobalSca = capture.globalScale;
            _active = capture.active;
            _parent = capture.parentId;
        }

        private static bool HasObjectChanged(TransformCapture previous, TransformCapture current,
            bool firstSeen, bool useLocalChangeCheck, out bool changeInTransform)
        {
            bool changeInActiveStatus = previous.active != current.active;
            bool changeInParent = previous.parentId != current.parentId;

            // Detect transform changes from either the local or the global (world) transform, selectable
            // via RecorderController.recordOnLocalTransformChangesOnly.
            if (useLocalChangeCheck)
                changeInTransform = !previous.localPosition.Equals(current.localPosition) ||
                                    !previous.localScale.Equals(current.localScale) ||
                                    !previous.localRotation.Equals(current.localRotation);
            else
                changeInTransform = !previous.globalPosition.Equals(current.globalPosition) ||
                                    !previous.globalScale.Equals(current.globalScale) ||
                                    !previous.globalRotation.Equals(current.globalRotation);

            return changeInActiveStatus || firstSeen || changeInParent || changeInTransform;
        }

        private static bool HasObjectChanged(TransformCapture previous, TransformCapture current,
            bool firstSeen, bool useLocalChangeCheck)
        {
            bool changeInTransform;
            return HasObjectChanged(previous, current, firstSeen, useLocalChangeCheck, out changeInTransform);
        }

        private static void PackTransform(TransformCapture capture, float[] matrixDTO, int[] infoDTO)
        {
            PackTransform(capture.localPosition, capture.localRotation, capture.localScale,
                capture.globalPosition, capture.globalRotation, capture.globalScale,
                capture.active, capture.parentId, matrixDTO, infoDTO);
        }

        public static void PackTransform(Vector3 localPosition, Quaternion localRotation, Vector3 localScale,
            Vector3 globalPosition, Quaternion globalRotation, Vector3 globalScale,
            bool active, int parentId, float[] matrixDTO, int[] infoDTO)
        {
            matrixDTO[0] = localPosition.x;
            matrixDTO[1] = localPosition.y;
            matrixDTO[2] = localPosition.z;

            matrixDTO[3] = localRotation.w;
            matrixDTO[4] = localRotation.x;
            matrixDTO[5] = localRotation.y;
            matrixDTO[6] = localRotation.z;

            matrixDTO[7] = localScale.x;
            matrixDTO[8] = localScale.y;
            matrixDTO[9] = localScale.z;

            matrixDTO[10] = globalPosition.x;
            matrixDTO[11] = globalPosition.y;
            matrixDTO[12] = globalPosition.z;

            matrixDTO[13] = globalRotation.w;
            matrixDTO[14] = globalRotation.x;
            matrixDTO[15] = globalRotation.y;
            matrixDTO[16] = globalRotation.z;

            matrixDTO[17] = globalScale.x;
            matrixDTO[18] = globalScale.y;
            matrixDTO[19] = globalScale.z;

            infoDTO[0] = active ? 1 : -1;
            infoDTO[1] = parentId;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (_presentInRecording == 1 && tag != "InstantiatedForPlayback")
            {
                // Restore the original parent first: playback may have reparented this object, and the
                // cached local TRS below is only meaningful relative to the original parent. Restoring the
                // parent before the local TRS therefore fixes both the hierarchy position and the world
                // pose. worldPositionStays is irrelevant here because the local values are overwritten next.
                if (_originalStateCaptured && _originalParent != gameObject.transform.parent)
                    gameObject.transform.SetParent(_originalParent);

                gameObject.transform.localPosition = _originalLocalPos;
                gameObject.transform.localRotation = _originalLocalRot;
                gameObject.transform.localScale = _originalLocalSca;
            }
        }

        public override void BeginRerecordCapture()
        {
            base.BeginRerecordCapture();
            _rerecBuffer.Clear();
            _rerecLastPushTime = -1.0f;
            _rerecLastCapture = CaptureCurrentTransform(GetRerecordParentId(transform));
            _rerecHasLastCapture = true;
        }

        public override void TickRerecordCapture(float currentReplayTime)
        {
            TransformCapture currentCapture = CaptureCurrentTransform(GetRerecordParentId(transform));
            if (_rerecHasLastCapture &&
                !HasObjectChanged(_rerecLastCapture, currentCapture, false, controller.recordOnLocalTransformChangesOnly))
                return;

            float minStep = 1.0f / Mathf.Max(1, controller.transformRecordingStepsPerSecond);
            if (_rerecLastPushTime >= 0.0f && currentReplayTime - _rerecLastPushTime < minStep)
                return;
            _rerecLastPushTime = currentReplayTime;

            float[] matrix = new float[20];
            int[] info = new int[2];
            PackTransform(currentCapture, matrix, info);

            _rerecBuffer.Add(new RerecordSample { time = currentReplayTime, matrix = matrix, info = info });
            _rerecLastCapture = currentCapture;
            _rerecHasLastCapture = true;
        }

        private int GetRerecordParentId(Transform t)
        {
            if (t.parent == null)
                return 0;
            return controller.recorderState.ResolveOriginalId(t.parent.gameObject);
        }

        public List<RerecordSample> DrainRerecordSamples()
        {
            List<RerecordSample> drained = _rerecBuffer;
            _rerecBuffer = new List<RerecordSample>();
            return drained;
        }
    }
}
