using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Unity.Netcode.Components;
using UnityEngine;
using VRSYS.Core.Interaction;
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
            public float time;

            public TransformCapture(Transform transform, bool active, float time)
            {
                localPosition = transform.localPosition;
                localRotation = transform.localRotation;
                localScale = transform.localScale;
                globalPosition = transform.position;
                globalRotation = transform.rotation;
                globalScale = transform.lossyScale;
                this.active = active;
                parentId = transform.parent != null ? transform.parent.gameObject.GetInstanceID() : 0;
                this.time = time;
            }

            public TransformCapture(Vector3 localPosition, Quaternion localRotation, Vector3 localScale,
                Vector3 globalPosition, Quaternion globalRotation, Vector3 globalScale, bool active, int parentId,
                float time)
            {
                this.localPosition = localPosition;
                this.localRotation = localRotation;
                this.localScale = localScale;
                this.globalPosition = globalPosition;
                this.globalRotation = globalRotation;
                this.globalScale = globalScale;
                this.active = active;
                this.parentId = parentId;
                this.time = time;
            }

            public TransformCapture(float[] matrixDTO, int[] infoDTO, float time)
            {
                this.localPosition = new Vector3(matrixDTO[0], matrixDTO[1], matrixDTO[2]);
                this.localRotation = new Quaternion(matrixDTO[4], matrixDTO[5], matrixDTO[6], matrixDTO[3]);
                this.localScale = new Vector3(matrixDTO[7], matrixDTO[8], matrixDTO[9]);
                this.globalPosition = new Vector3(matrixDTO[10], matrixDTO[11], matrixDTO[12]);
                this.globalRotation = new Quaternion(matrixDTO[14], matrixDTO[15], matrixDTO[16], matrixDTO[13]);
                this.globalScale = new Vector3(matrixDTO[17], matrixDTO[18], matrixDTO[19]);
                this.active = infoDTO[0] > 0;
                this.parentId = infoDTO[1];
                this.time = time;
            }
        }

        private List<RerecordSample> _rerecBuffer = new List<RerecordSample>();
        private float _rerecLastPushTime = -1.0f;
        private TransformCapture _rerecLastCapture;
        private TransformCapture _rerecPreviousFrameCapture;
        private bool _rerecHasLastCapture = false;

        [DllImport("RecordingPlugin")]
        private static extern bool RegisterObjectMeshPath(int recorderId, int uuid, string path, int pathLength,
            float time);

        [DllImport("RecordingPlugin")]
        private static extern bool RegisterObjectComponents(int recorderId, int uuid, string componentString,
            int componentStringLength);

        [DllImport("RecordingPlugin")]
        private static extern bool RegisterObjectPrefab(int recorderId, int uuid, string prefab, int prefabLength,
            float time);

        [DllImport("RecordingPlugin")]
        private static extern bool RecordObjectAtTimestamp(int recorderId, string objectName, int objectNameLength,
            int uuid, float[] localMatrix, float timeStamp, int[] objectInformation);

        [DllImport("RecordingPlugin")]
        private static extern bool GetTransformAndInformationAtTime(int recorderId, string objectName,
            int objectNameLength, int uuid, float currentTime, IntPtr data, IntPtr objectInformation);

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

        private TransformCapture? _lastRecordedCapture = null;
        private TransformCapture? _lastReplayedCapture = null;

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

        // Offset of a hold keyframe before a jump, gap or parent change (see Record and TickRerecordCapture).
        private const float HoldKeyframeOffsetSeconds = 0.001f;
        private float lastErrorLog = 0.0f;
        private NetworkTransform _networkTransform;

        public override void Start()
        {
            base.Start();
            _networkTransform = GetComponent<NetworkTransform>();
            isNetworkedRecorder = _networkTransform != null;
        }

        public override bool HasNetworkAuthority() =>
            _networkTransform == null || !_networkTransform.IsSpawned || _networkTransform.CanCommitToTransform;

        // Physics must not move an object while the replay drives it: the replay sets its pose every frame but not
        // its velocity, and a NetworkTransform sends the pose after the physics step. The rigidbody is kept kinematic
        // while the replay applies poses and restored when the object is taken over or the replay ends.
        private Rigidbody _rigidbody;
        private InteractableNetworkState _grabState;
        private bool _physicsComponentsLookedUp;
        private bool _holdsRigidbody;
        private bool _wasKinematic;

        private void SetDrivenByReplay(bool driven)
        {
            if (!_physicsComponentsLookedUp)
            {
                _rigidbody = GetComponent<Rigidbody>();
                _grabState = GetComponent<InteractableNetworkState>();
                _physicsComponentsLookedUp = true;
            }

            // While a user holds the object, the XR Interaction Toolkit owns its physics state and restores it on release.
            if (_rigidbody == null || (_grabState != null && _grabState.IsSpawned && _grabState.isGrabbed.Value))
                return;

            if (driven)
            {
                if (!_holdsRigidbody)
                {
                    _wasKinematic = _rigidbody.isKinematic;
                    _holdsRigidbody = true;
                }

                // Re-applied every frame: NetworkRigidbody makes the body dynamic when this peer gains authority.
                if (!_rigidbody.isKinematic)
                    _rigidbody.isKinematic = true;
            }
            else if (_holdsRigidbody)
            {
                _rigidbody.isKinematic = _wasKinematic;
                _holdsRigidbody = false;
            }
        }

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

            if (id == 99999)
                id = gameObject.GetInstanceID();

            bool firstSeen = false;

            if (!_isRecorded)
            {
                _isRecorded = true;
                firstSeen = true;

                // TODO: change to recording only prefabs!
                string meshPath = " ";
                MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                    meshPath = meshFilter.sharedMesh.name;

                RegisterObjectMeshPath(controller.RecorderID, id, meshPath, meshPath.Length, recordTime);

                Component[] components = gameObject.GetComponents(typeof(Component));
                string componentString = "";
                string pattern = @"\(([^)]+)\)";
                Regex rg = new Regex(pattern);
                foreach (Component component in components)
                {
                    if (component == null)
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
                            ExtendedLogger.LogInfo(GetType().Name,
                                "Prefab path: " + prefabInfo.correspondingPrefab.assetPath + ", for object: " +
                                gameObject.name, this);
                        if (prefabInfo.correspondingPrefab != null &&
                            prefabInfo.correspondingPrefab.assetPath.Length > 0)
                        {
                            RegisterObjectPrefab(controller.RecorderID, id, prefabInfo.correspondingPrefab.assetPath,
                                prefabInfo.correspondingPrefab.assetPath.Length, _lastRecordTime);
                            break;
                        }
                    }
                }
            }

            TransformCapture currentCapture = new TransformCapture(transform, gameObject.activeSelf, recordTime);
            bool objectChanged = HasObjectChanged(_lastRecordedCapture, currentCapture,
                controller.recordOnLocalTransformChangesOnly);

            if (objectChanged)
            {
                bool wasTeleported = CheckIfWasTeleported(currentCapture);
                if (wasTeleported && controller.debugLogs)
                    ExtendedLogger.LogInfo(GetType().Name,
                        "Gap/teleport keyframe inserted. Time: " + recordTime + ", uuid: " + id, this);

                bool parentChanged = CheckIfParentChanged(currentCapture);
                if (parentChanged && controller.debugLogs)
                    ExtendedLogger.LogInfo(GetType().Name,
                        "Parent change keyframe inserted. Time: " + recordTime + ", uuid: " + id, this);

                if ((parentChanged || wasTeleported) && _lastRecordedCapture.HasValue)
                {
                    fillDTOData(_lastRecordedCapture.Value);

                    float insertedRecordingTime = recordTime - HoldKeyframeOffsetSeconds;
                    if (insertedRecordingTime > 0.0f)
                    {
                        result = RecordObjectAtTimestamp(controller.RecorderID, _name, _name.Length, id, _matrixDTO,
                            insertedRecordingTime, _infoDTO);

                        if (!result && controller.debugLogs)
                            ExtendedLogger.LogError(GetType().Name,
                                "Inserting prior teleport/parent change keyframe: Failed, " + gameObject.name, this);
                    }
                }

                fillDTOData(currentCapture);

                result = RecordObjectAtTimestamp(controller.RecorderID, _name, _name.Length, id, _matrixDTO, recordTime,
                    _infoDTO);

                if (!result && controller.debugLogs)
                {
                    ExtendedLogger.LogError(GetType().Name, "Recording object: " + _name + " Failed", this);
                }
                else
                {
                    _transform.hasChanged = false;

                    _lastRecordTime = recordTime;
                    _lastRecordedCapture = currentCapture;
                }
            }

            return result;
        }

        private bool CheckIfWasTeleported(TransformCapture currentCapture)
        {
            if (!_lastRecordedCapture.HasValue)
                return false;

            // This check is being done to avoid problems during jumps/teleportations when the object does not move
            // often and then suddenly does a huge "jump". The reason for this is that it can produce artifacts during
            // the replay when the interpolation is being done between two transforms which have a huge time stamp difference
            float posDif = (_lastRecordedCapture.Value.localPosition - currentCapture.localPosition).magnitude;
            float scaDif = (_lastRecordedCapture.Value.localScale - currentCapture.localScale).magnitude;

            // Elapsed time since the last *recorded* sample. While the object is static no
            // samples are written, so a value larger than one sample step means we skipped a
            // static period and must insert a hold keyframe to stop the plugin from smearing
            // the first motion delta across the whole gap.
            float timeDif = currentCapture.time - _lastRecordedCapture.Value.time;
            bool relevantChange = (posDif > 1f || scaDif > 0.3f);
            return ((timeDif > 1.5f / (float)controller.transformRecordingStepsPerSecond) || relevantChange) &&
                   currentCapture.time > 0.001f;
        }

        private bool CheckIfParentChanged(TransformCapture currentCapture)
        {
            if (!_lastRecordedCapture.HasValue)
                return false;
            return _lastRecordedCapture.Value.parentId != currentCapture.parentId;
        }

        unsafe public override bool Replay(float replayTime)
        {
            if (inRerecordingMode)
            {
                SetDrivenByReplay(false);
                return true;
            }

            replayTime = ClampReplayTimeForNativeBuffer(replayTime);

            // The next playback step was not reached -> applying the most recent data to the transform
            if (Mathf.Abs(replayTime - _lastReplayTime) < 1.0f / playbackFramerate && _transform != null)
            {
                SetDrivenByReplay(true);
                if (_lastReplayedCapture.HasValue)
                    ApplyReplayCapture(_lastReplayedCapture.Value, transform);

                return true;
            }

            if (_transform == null)
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

            SetDrivenByReplay(true);

            if (id == 99999)
                id = gameObject.GetInstanceID();

            if (recorderId == 99999)
                recorderId = controller.RecorderID;

            _currentParentTransform = _transform.parent;

            if (controller.replayHierarchyChanges)
            {
                if (_currentParentTransform != null && _currentParentTransform != _parentTransform &&
                    controller.recorderState.newIdOriginalId.ContainsKey(_currentParentTransform.gameObject
                        .GetInstanceID()))
                {
                    _parent = controller.recorderState.newIdOriginalId[
                        _currentParentTransform.gameObject.GetInstanceID()];
                    _parentTransform = _currentParentTransform;
                }
                else if (_currentParentTransform == null)
                    _parent = 0;
            }

            fixed (float* p = _matrixDTO)
            {
                fixed (int* u = _infoDTO)
                {
                    bool result = GetTransformAndInformationAtTime(recorderId, _name, _name.Length, id, replayTime,
                        (IntPtr)p, (IntPtr)u);

                    if (!result)
                    {
                        if (Time.time - lastErrorLog > 10.0f)
                        {
                            lastErrorLog = Time.time;
                            if (controller.debugLogs)
                                ExtendedLogger.LogError(GetType().Name, "Could not get replay transform for: " + _name,
                                    this);
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

                        var replayCapture = new TransformCapture(_matrixDTO, _infoDTO, replayTime);
                        ApplyReplayCapture(replayCapture, _transform);
                        _lastReplayedCapture = replayCapture;
                    }

                    _lastReplayTime = replayTime;
                    return result;
                }
            }
        }

        private void ApplyReplayCapture(TransformCapture capture, Transform tr)
        {
            // Apply a parent change before the local transform: the replayed values are relative to the new
            // parent. Setting them first and reparenting afterwards (keeping the world pose) showed the object
            // at a wrong pose for the frame of the change.
            if (controller.replayHierarchyChanges)
            {
                if (capture.parentId != -99999 && _parent != capture.parentId)
                {
                    if (controller.recorderState.originalIdGameObjects.ContainsKey(capture.parentId))
                    {
                        Transform parentTransform = controller.recorderState.originalIdGameObjects[capture.parentId].transform;
                        if (capture.parentId != 0 && parentTransform != null)
                        {
                            tr.SetParent(parentTransform, false);
                            _parentTransform = parentTransform;
                            _parent = capture.parentId;
                        }
                        else
                        {
                            // A reparent-to-root during recording maps to the replay anchor
                            // during playback: the recording's root is the configured replayRoot
                            // (cf. ScenePreparator, which parents top-level recorded objects under
                            // it). SetParent(null) when no anchor is set preserves scene-root behaviour.
                            tr.SetParent(controller.replayRoot, false);
                            _parentTransform = controller.replayRoot;
                            _parent = 0;
                        }
                    }

                    if (capture.parentId == 0)
                    {
                        tr.SetParent(controller.replayRoot, false);
                        _parentTransform = controller.replayRoot;
                        _parent = 0;
                    }
                }
            }

            if (controller.replayLocalTransformChangesOnly)
            {
                tr.localScale = capture.localScale;
                tr.localRotation = capture.localRotation;
                tr.localPosition = capture.localPosition;
            }
            else
            {
                tr.localScale = LocalScaleForGlobalScale(tr.parent, capture.globalScale);
                tr.rotation = capture.globalRotation;
                tr.position = capture.globalPosition;
            }

            if (gameObject.activeSelf != capture.active)
                gameObject.SetActive(capture.active);
        }

        // Unity's lossyScale is the component-wise product of the local scales along the hierarchy, so the local scale
        // for a target global scale divides by the parent's lossyScale. Exact for parents without rotation or with
        // uniform scale; under a rotated, non-uniformly scaled parent the object is skewed and no local scale matches
        // the global scale exactly. A zero parent scale axis keeps the target value (any local value gives zero).
        private static Vector3 LocalScaleForGlobalScale(Transform parent, Vector3 globalScale)
        {
            if (parent == null)
                return globalScale;

            Vector3 parentScale = parent.lossyScale;
            return new Vector3(DivideOrKeep(globalScale.x, parentScale.x),
                DivideOrKeep(globalScale.y, parentScale.y),
                DivideOrKeep(globalScale.z, parentScale.z));
        }

        private static float DivideOrKeep(float value, float divisor) =>
            Mathf.Abs(divisor) > 1e-6f ? value / divisor : value;

        private float ClampReplayTimeForNativeBuffer(float replayTime)
        {
            float duration = controller != null && controller.recorderState != null
                ? controller.recorderState.recordingDuration
                : -1.0f;

            if (duration > ReplayBoundaryPaddingSeconds * 2.0f)
                return Mathf.Clamp(replayTime, ReplayBoundaryPaddingSeconds, duration - ReplayBoundaryPaddingSeconds);

            return Mathf.Max(0.0f, replayTime);
        }

        bool fillDTOData(TransformCapture capture)
        {
            PackTransform(capture, _matrixDTO, _infoDTO);
            return true;
        }

        private static bool HasObjectChanged(TransformCapture? previous, TransformCapture current,
            bool useLocalChangeCheck)
        {
            // No previous data has been recorded so the new data should be treated as a "change"
            if (!previous.HasValue)
                return true;

            bool changeInActiveStatus = previous.Value.active != current.active;
            bool changeInParent = previous.Value.parentId != current.parentId;
            bool changeInTransform = false;

            // Detect transform changes from either the local or the global (world) transform, selectable
            // via RecorderController.recordOnLocalTransformChangesOnly.
            if (useLocalChangeCheck)
                changeInTransform = !previous.Value.localPosition.Equals(current.localPosition) ||
                                    !previous.Value.localScale.Equals(current.localScale) ||
                                    !previous.Value.localRotation.Equals(current.localRotation);
            else
                changeInTransform = !previous.Value.globalPosition.Equals(current.globalPosition) ||
                                    !previous.Value.globalScale.Equals(current.globalScale) ||
                                    !previous.Value.globalRotation.Equals(current.globalRotation);

            return changeInActiveStatus || changeInParent || changeInTransform;
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

            // Hand physics back after the replay. Without network authority, NetworkRigidbody keeps the body kinematic.
            if (_holdsRigidbody && _rigidbody != null && HasNetworkAuthority())
                _rigidbody.isKinematic = _wasKinematic;
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
            _rerecLastCapture = new TransformCapture(transform, gameObject.activeSelf, 0.0f);
            _rerecPreviousFrameCapture = _rerecLastCapture;
            _rerecHasLastCapture = true;
        }

        public override void TickRerecordCapture(float currentReplayTime)
        {
            TransformCapture currentCapture = new TransformCapture(transform, gameObject.activeSelf, currentReplayTime);
            TransformCapture previousFrameCapture = _rerecPreviousFrameCapture;
            _rerecPreviousFrameCapture = currentCapture;

            if (_rerecHasLastCapture &&
                !HasObjectChanged(_rerecLastCapture, currentCapture, false))
                return;

            // As in Record: across a parent change the local transforms of two samples refer to different parents.
            // Hold the pose of the previous frame under the old parent up to the change, and never skip the change.
            bool parentChanged = _rerecHasLastCapture && previousFrameCapture.parentId != currentCapture.parentId;

            float minStep = 1.0f / Mathf.Max(1, controller.transformRecordingStepsPerSecond);
            if (!parentChanged && _rerecLastPushTime >= 0.0f && currentReplayTime - _rerecLastPushTime < minStep)
                return;

            if (parentChanged)
            {
                float holdTime = currentReplayTime - HoldKeyframeOffsetSeconds;
                if (holdTime > _rerecLastPushTime)
                    AddRerecordSample(previousFrameCapture, holdTime);
            }

            AddRerecordSample(currentCapture, currentReplayTime);
            _rerecLastPushTime = currentReplayTime;
            _rerecLastCapture = currentCapture;
            _rerecHasLastCapture = true;
        }

        private void AddRerecordSample(TransformCapture capture, float time)
        {
            float[] matrix = new float[20];
            int[] info = new int[2];
            PackTransform(capture, matrix, info);
            _rerecBuffer.Add(new RerecordSample { time = time, matrix = matrix, info = info });
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