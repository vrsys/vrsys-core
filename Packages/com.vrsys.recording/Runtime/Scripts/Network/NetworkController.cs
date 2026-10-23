using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using VRSYS.Core.Avatar;
using VRSYS.Core.Networking;
using VRSYS.Core.Logging;

namespace VRSYS.Recording
{

    /// <remarks>
    /// RPC conventions: commands that change shared replay/recording state (download,
    /// recording/replay start/end, play/pause) are sent to the server, which relays them to every peer.
    /// The relay gives all peers the same order when several users issue commands at once; a single
    /// SendTo.Everyone RPC would run the sender's own call first and let peers diverge.
    /// Per-user reports and idempotent commands use a single SendTo.Everyone RPC.
    /// Download status is reported to the server only; the server starts the synchronized replay once every
    /// participating client has its files.
    /// </remarks>
    [RequireComponent(typeof(RecorderState))]
    [RequireComponent(typeof(RecorderController))]
    public class NetworkController : NetworkBehaviour
    {
        [DllImport("RecordingPlugin")]
        private static extern bool RegisterRecordingStartGlobalTimeOffset(int recorderId, float globalTimeOffset);

        [HideInInspector] public float maxSynchronizationTimeMS;
        [HideInInspector] public Dictionary<string, float> _userReplayTimes = new Dictionary<string, float>();
        [HideInInspector] public Dictionary<string, float> _userReplayTimesUpdateTime = new Dictionary<string, float>();
        [HideInInspector] public float currentLatency;

        /// <summary>
        /// The host's (Netcode server's) current replay time, broadcast to every client. Used as the
        /// synchronization target for synchronized playback, replacing the previous app-level singleton.
        /// </summary>
        public float HostReplayTime { get; private set; }

        private RecorderState _state;
        private RecorderController _controller;

        private bool _replayStarted = false;
        private bool _transformsDownloaded = false;
        private bool _soundsDownloaded = false;
        private bool _metaInformationDownloaded = false;
        private bool _genericDownloaded = false;
        private bool _transformsDownloadFailed = false;
        private bool _soundsDownloadFailed = false;
        private bool _metaInformationDownloadFailed = false;
        private bool _genericDownloadFailed = false;
        // Client side: the download status last reported to the server during the current replay preparation.
        private bool _downloadStatusReported = false;
        private bool _lastReportedDownloading = false;

        // Server side: clients taking part in the current replay preparation, and whether each is still downloading.
        private readonly Dictionary<ulong, bool> _clientDownloading = new Dictionary<ulong, bool>();
        private bool _awaitingDownloads = false;
        private Coroutine _startRecordingCoroutine;
        private Coroutine _startReplayCoroutine;
        private Coroutine _endRecordingCoroutine;
        private Coroutine _endReplayCoroutine;
        private string _lastDistributedReplayStatusLog;
        
        private DateTime _globalSynchronizationTime;
        private TimeSpan _globalRecordStartDifference;
        private float _internalSynchronizationTime;
        private float _currentPhotonPing;

        private int _selectedServerId = 0;
        private TextMeshProUGUI _serverText;
        
        public void Start()
        {
            _state = GetComponent<RecorderState>();
            _controller = GetComponent<RecorderController>();

            // Establish and periodically refresh the NTP clock offset in the background so the
            // (blocking) UDP query never stalls the main thread, and so we never flood the NTP
            // servers. All synchronized-time reads below use the cached offset.
            StartCoroutine(KeepClockSynchronized());
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        
        #region RecordingFileDownload
        private string LocalDownloadStatusSummary()
        {
            return "sounds=" + _soundsDownloaded +
                   ", transforms=" + _transformsDownloaded +
                   ", meta=" + _metaInformationDownloaded +
                   ", generic=" + _genericDownloaded +
                   ", failed(sounds=" + _soundsDownloadFailed +
                   ", transforms=" + _transformsDownloadFailed +
                   ", meta=" + _metaInformationDownloadFailed +
                   ", generic=" + _genericDownloadFailed + ")";
        }
        
        private string UserDownloadStatusSummary()
        {
            if (_clientDownloading.Count == 0)
                return "<none>";

            string result = "";
            foreach (var entry in _clientDownloading)
            {
                if (result.Length > 0)
                    result += ", ";

                result += entry.Key + (entry.Value ? "=downloading" : "=finished");
            }

            return result;
        }
        
        public void StartDownloadOnAllClientsEvent()
        {
            
            if (_state.selectedReplayFile == "")
            {
                DebugDistributedReplayLog("StartDownloadOnAllClientsEvent aborted: no replay file selected.");
                ExtendedLogger.LogError(GetType().Name, "No replay file selected!", this);
                return;
            }

            if (_controller != null && _controller.debugLogs)
                ExtendedLogger.LogInfo(GetType().Name, "Started download on all clients", this);
            DebugDistributedReplayLog("StartDownloadOnAllClientsEvent: replayFile=" + _state.selectedReplayFile +
                                      ", recorderId=" + _state.recorderID +
                                      ", state=" + _state.currentState +
                                      ", projectName=" + _state.projectName +
                                      ", selectedServer=" + _state.selectedServer +
                                      ", recordingDirectory=" + _state.recordingDirectory);
            DebugDistributedReplayStatusIfChanged("StartDownloadOnAllClientsEvent");
            StartDownloadsServerRpc(_state.selectedReplayFile, _state.recorderID);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void StartDownloadsServerRpc(string replayFile, int recorderId)
        {
            DebugDistributedReplayLog("StartDownloadsServerRpc: replayFile=" + replayFile +
                                      ", recorderId=" + recorderId +
                                      ", isServer=" + IsServer +
                                      ", isClient=" + IsClient);

            if (_awaitingDownloads)
            {
                DebugDistributedReplayLog("StartDownloadsServerRpc ignored: a replay preparation is already waiting for downloads.");
                return;
            }

            _clientDownloading.Clear();
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                _clientDownloading[clientId] = true;
            _awaitingDownloads = _clientDownloading.Count > 0;
            DebugDistributedReplayStatusIfChanged("StartDownloadsServerRpc");

            StartDownloadsClientRpc(replayFile, recorderId);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void StartDownloadsClientRpc(string replayFile, int recorderId)
        {
            DebugDistributedReplayLog("StartDownloadsClientRpc received: replayFile=" + replayFile +
                                      ", incomingRecorderId=" + recorderId +
                                      ", localRecorderId=" + _state.recorderID +
                                      ", state=" + _state.currentState);
            if (_state.currentState == State.Idle)
            {
                _state.selectedReplayFile = replayFile;
                _state.fixedPlaybackRecordingName = replayFile;
                
                if(recorderId != _state.recorderID)
                {
                    DebugDistributedReplayLog("StartDownloadsClientRpc ignored after idle check: recorder id mismatch.");
                    return;
                }
                
                if (_controller != null && _controller.debugLogs)
                    ExtendedLogger.LogInfo(GetType().Name, "Download started for recorder id: " + _state.recorderID, this);
                if (_controller != null && _controller.debugLogs)
                    ExtendedLogger.LogInfo(GetType().Name, "Selected replay file: " + _state.selectedReplayFile, this);

                _soundsDownloaded = false;
                _transformsDownloaded = false;
                _metaInformationDownloaded = false;
                _genericDownloaded = false;
                _replayStarted = false;
                _downloadStatusReported = false;

                if (_state.recordingDirectory == "" || true)
                {
                    _state.recordingDirectory = Application.persistentDataPath;
                }

                StartDownloadCoroutines();
                _state.currentState = State.PreparingReplay;

                DebugDistributedReplayStatusIfChanged("StartDownloadsClientRpc after state change");
            }
            else
            {
                DebugDistributedReplayLog("StartDownloadsClientRpc ignored: state is " + _state.currentState +
                                          " instead of Idle.");
                // Tell the server not to wait for this client.
                ReportDownloadStatusRpc(_state.recorderID, false, false);
            }
        }
        
        public void StartDownloads()
        {
            if (_state.currentState == State.PreparingReplay)
            {
                if (_controller != null && _controller.debugLogs)
                    ExtendedLogger.LogInfo(GetType().Name, "Local download started", this);
                if (_controller != null && _controller.debugLogs)
                    ExtendedLogger.LogInfo(GetType().Name, "Selected replay file: " + _state.selectedReplayFile, this);
                DebugDistributedReplayLog("StartDownloads: local download path. replayFile=" +
                                          _state.selectedReplayFile +
                                          ", recordingDirectory=" + _state.recordingDirectory +
                                          ", projectName=" + _state.projectName);

                _soundsDownloaded = false;
                _transformsDownloaded = false;
                _metaInformationDownloaded = false;
                _genericDownloaded = false;
                _replayStarted = false;
                _downloadStatusReported = false;

                if (_state.recordingDirectory == "")
                {
                    _state.recordingDirectory = Application.persistentDataPath;
                }

                StartDownloadCoroutines();         
                DebugDistributedReplayStatusIfChanged("StartDownloads local");
            }
        }
        
        /// <summary>
        /// Called every FixedUpdate while a replay is being prepared. Keeps failed downloads retrying
        /// (IsDownloading) and reports the local download status to the server whenever it changes.
        /// </summary>
        public void UpdateDownloadStatusEvent()
        {
            bool downloadState = IsDownloading();
            DebugDistributedReplayStatusIfChanged("UpdateDownloadStatusEvent");

            if (_state.currentState != State.PreparingReplay)
                return;

            if (_controller.localPlayback)
            {
                if (!downloadState)
                {
                    if (_controller != null && _controller.debugLogs)
                        ExtendedLogger.LogInfo(GetType().Name, "Download of files finished. Starting local playback.", this);
                    _controller.StartReplay();
                }
                return;
            }

            if (_downloadStatusReported && _lastReportedDownloading == downloadState)
                return;

            _downloadStatusReported = true;
            _lastReportedDownloading = downloadState;
            DebugDistributedReplayLog("UpdateDownloadStatusEvent: reporting downloadState=" + downloadState +
                                      ", recorderId=" + _state.recorderID);
            ReportDownloadStatusRpc(_state.recorderID, true, downloadState);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReportDownloadStatusRpc(int recorderId, bool participating, bool downloading, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (recorderId != _state.recorderID || !_awaitingDownloads || !_clientDownloading.ContainsKey(clientId))
            {
                DebugDistributedReplayLog("ReportDownloadStatusRpc ignored: client=" + clientId +
                                          ", incomingRecorderId=" + recorderId +
                                          ", localRecorderId=" + _state.recorderID +
                                          ", awaitingDownloads=" + _awaitingDownloads);
                return;
            }

            if (participating)
                _clientDownloading[clientId] = downloading;
            else
                _clientDownloading.Remove(clientId);

            DebugDistributedReplayLog("ReportDownloadStatusRpc: client=" + clientId +
                                      ", participating=" + participating +
                                      ", downloading=" + downloading);
            TryStartReplayWhenAllDownloaded();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!_clientDownloading.Remove(clientId))
                return;

            DebugDistributedReplayLog("Client " + clientId + " disconnected during replay preparation.");
            TryStartReplayWhenAllDownloaded();
        }

        // Server only: starts the synchronized replay once every participating client has its files.
        private void TryStartReplayWhenAllDownloaded()
        {
            if (!_awaitingDownloads)
                return;

            if (_clientDownloading.Count == 0)
            {
                _awaitingDownloads = false;
                DebugDistributedReplayLog("Replay preparation cancelled: no participating clients left.");
                return;
            }

            foreach (bool downloading in _clientDownloading.Values)
            {
                if (downloading)
                {
                    DebugDistributedReplayStatusIfChanged("TryStartReplayWhenAllDownloaded waiting");
                    return;
                }
            }

            _awaitingDownloads = false;
            DebugDistributedReplayLog("All clients finished downloading, starting synchronized replay.");
            StartReplayOnAllClients();
        }
        
        public bool IsDownloading()
        {
            if (!_soundsDownloaded || !_transformsDownloaded || !_metaInformationDownloaded || !_genericDownloaded)
            {

                if (_transformsDownloadFailed)
                {
                    _transformsDownloadFailed = false;
                    StartCoroutine(DownloadFileFromServer(_state.projectName, _state.recordingDirectory, "get_transform_recording", _state.selectedReplayFile));
                }

                if (_soundsDownloadFailed)
                {
                    _soundsDownloadFailed = false;
                    StartCoroutine(DownloadFileFromServer(_state.projectName, _state.recordingDirectory, "get_sound_recording", _state.selectedReplayFile));
                }

                if (_metaInformationDownloadFailed)
                {
                    _metaInformationDownloadFailed = false;
                    StartCoroutine(DownloadFileFromServer(_state.projectName, _state.recordingDirectory, "get_meta_recording", _state.selectedReplayFile));
                }

                if (_genericDownloadFailed)
                {
                    _genericDownloadFailed = false;
                    StartCoroutine(DownloadFileFromServer(_state.projectName, _state.recordingDirectory, "get_generic_recording", _state.selectedReplayFile));
                }

                return true;
            }
            
            //Text downloadStatus = Utils.GetChildByName(gameObject,"DownloadStatus").GetComponent<Text>();
            //downloadStatus.text = "Waiting";

            return false;
        }
        
        private IEnumerator DownloadFileFromServer(string projectName, string directory, string url, string fileName)
        {
            string completeURL = _state.selectedServer + "/" + url + "/" + projectName + "/"+ fileName;
            if (_controller != null && _controller.debugLogs)
                ExtendedLogger.LogInfo(GetType().Name, "Started download of file from: " + completeURL, this);

            string fileType = ".None";
            
            if (url.Contains("get_meta_recording"))
            {
                fileType = ".recordmeta";
            }
            else if (url.Contains("get_sound_recording"))
            {
                fileType = ".sound";
            }
            else if (url.Contains("get_generic_recording"))
            {
                fileType = ".generic";
            } 
            else if (url.Contains("get_transform_recording"))
            {
               fileType = ".transform";
            }
            
            using (var uwr = new UnityWebRequest(completeURL, UnityWebRequest.kHttpVerbGET))
            {
                string file = directory + "/" + fileName + fileType;
                DebugDistributedReplayLog("DownloadFileFromServer: url=" + completeURL +
                                          ", targetFile=" + file +
                                          ", fileType=" + fileType +
                                          ", exists=" + File.Exists(file));
                if(!File.Exists(file)){
                    DownloadHandlerFile dH = new DownloadHandlerFile(file);
                    dH.removeFileOnAbort = true;
                    uwr.downloadHandler = dH;
                    uwr.timeout = 0;
                    //uwr.useHttpContinue = false;
                    //uwr.SetRequestHeader("Accept-Encoding", "gzip, deflate, sdch");
                    //uwr.SetRequestHeader("Connection","Keep-Alive");
                    //uwr.SetRequestHeader("Keep-Alive","timeout=15, max=1000");
                    //uwr.SetRequestHeader("Cache-Control", "no-cache");
                    
                    uwr.SendWebRequest();
                    DebugDistributedReplayLog("DownloadFileFromServer: request sent for " + fileType +
                                              ", targetFile=" + file);
                    
                    while (!uwr.isDone)
                    {
                        //Text downloadStatus = Utils.GetChildByName(gameObject,"DownloadStatus").GetComponent<Text>();
                        //downloadStatus.text = (uwr.downloadProgress * 100.0f).ToString("F0") + "%";

                        yield return null;
                    }

                    if (uwr.result != UnityWebRequest.Result.Success)
                    {
                        ExtendedLogger.LogError(GetType().Name, uwr.error + ", url: " + completeURL, this);
                        DebugDistributedReplayLog("DownloadFileFromServer failed: fileType=" + fileType +
                                                  ", error=" + uwr.error +
                                                  ", responseCode=" + uwr.responseCode);
                        
                        if (url.Contains("get_meta_recording"))
                        {
                            _metaInformationDownloadFailed = true;
                        }
                        else if (url.Contains("get_sound_recording"))
                        {
                            _soundsDownloadFailed = true;
                        }
                        else if (url.Contains("get_generic_recording"))
                        {
                            _genericDownloadFailed = true;
                        }
                        else if (url.Contains("get_transform_recording"))
                        {
                            _transformsDownloadFailed = true;
                        }
                    }
                    else
                    {
                        DebugDistributedReplayLog("DownloadFileFromServer finished: fileType=" + fileType +
                                                  ", downloadedBytes=" + uwr.downloadedBytes +
                                                  ", targetFile=" + file);
                        if (url.Contains("get_meta_recording"))
                        {
                            _metaInformationDownloaded = true;
                        }
                        else if (url.Contains("get_sound_recording"))
                        {
                            _soundsDownloaded = true;
                        }
                        else if (url.Contains("get_generic_recording"))
                        {
                            _genericDownloaded = true;
                        }
                        else if (url.Contains("get_transform_recording"))
                        {
                            _transformsDownloaded = true;
                        }
                    }
                    DebugDistributedReplayStatusIfChanged("DownloadFileFromServer completion " + fileType);
                }
                else
                {
                    if (_controller != null && _controller.debugLogs)
                        ExtendedLogger.LogInfo(GetType().Name, "File already exists: " + file + ". Skipping download.", this);
                    DebugDistributedReplayLog("DownloadFileFromServer skipped existing file: fileType=" + fileType +
                                              ", targetFile=" + file);
                    if (url.Contains("get_meta_recording"))
                    {
                        _metaInformationDownloaded = true;
                    }
                    else if (url.Contains("get_sound_recording"))
                    {
                        _soundsDownloaded = true;
                    }
                    else if (url.Contains("get_generic_recording"))
                    {
                        _genericDownloaded = true;
                    }
                    else if (url.Contains("get_transform_recording"))
                    {
                        _transformsDownloaded = true;
                    }
                    DebugDistributedReplayStatusIfChanged("DownloadFileFromServer skipped " + fileType);
                }
            }
        }

        private void StartDownloadCoroutines()
        {
            string replayFile = _state.selectedReplayFile;
            string projectName = _state.projectName;
            DebugDistributedReplayLog("StartDownloadCoroutines: replayFile=" + replayFile +
                                      ", projectName=" + projectName +
                                      ", recordingDirectory=" + _state.recordingDirectory +
                                      ", selectedServer=" + _state.selectedServer);
            StartCoroutine(DownloadFileFromServer(projectName, _state.recordingDirectory, "get_transform_recording", replayFile));
            StartCoroutine(DownloadFileFromServer(projectName, _state.recordingDirectory, "get_sound_recording", replayFile));
            StartCoroutine(DownloadFileFromServer(projectName, _state.recordingDirectory, "get_meta_recording", replayFile));
            StartCoroutine(DownloadFileFromServer(projectName, _state.recordingDirectory, "get_generic_recording", replayFile));
        }
        #endregion
        
        #region RecordingFileServerManagement
        public void SwitchSelectedServerEvent()
        {
            int newSelectedServerId = (_selectedServerId + 1) % _state.serverList.Count;
            SwitchSelectedServerRpc(newSelectedServerId, _state.recorderID);
        }
        
        // Carries the absolute server index, so concurrent switches converge without a server relay.
        [Rpc(SendTo.Everyone)]
        private void SwitchSelectedServerRpc(int selectedServerId, int recorderId)
        {
            
            if(_state.recorderID != recorderId)
                return;
            
            if (_controller != null && _controller.debugLogs)
                ExtendedLogger.LogInfo(GetType().Name, "Switch selected server event received. Recorder id: " + _state.recorderID, this);

            _selectedServerId = selectedServerId;
            _state.selectedServer = _state.serverList[_selectedServerId];
            _state.replayList = new ReplayList();
            if(_serverText != null)
                _serverText.text = "Server: " + _state.selectedServer;
            
            UpdateReplayList();
        }
        
        public void UpdateSelectedServerText(TextMeshProUGUI text)
        {
            if(text == null)
                return;

            if (_serverText == null)
                _serverText = text;

            _serverText.text = "Server: " + _state.selectedServer;
        }
        #endregion
        
        #region RecordingStart
        public void PrepareRecordingOnAllClientsEvent()
        {
            PrepareRecordingRpc(_state.recorderID);
        }

        [Rpc(SendTo.Everyone)]
        private void PrepareRecordingRpc(int recorderId)
        {
            if(_state.recorderID != recorderId)
                return;
            
            if (_controller != null && _controller.debugLogs)
                ExtendedLogger.LogInfo(GetType().Name, "Preparing for recording. Recorder id: " + _state.recorderID, this);
            
            _controller.PrepareRecording();
        }
        
        public void StartRecordingOnAllClientsEvent()
        {
            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            if (_controller != null && _controller.debugLogs)
                ExtendedLogger.LogInfo(GetType().Name, "Global time before offset: " + _globalSynchronizationTime, this);
            DateTime startRecordingTime = _globalSynchronizationTime.AddMilliseconds(maxSynchronizationTimeMS);
            if (_controller != null && _controller.debugLogs)
                ExtendedLogger.LogInfo(GetType().Name, "Start recording time: " + _globalSynchronizationTime, this);
            StartRecordingOnServerRpc(startRecordingTime.ToFileTime(), _state.recorderID);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void StartRecordingOnServerRpc(long startTime, int recorderId)
        {
            StartRecordingOnClientRpc(startTime, recorderId);
        }
        
        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void StartRecordingOnClientRpc(long startTime, int recorderId)
        {
            DateTime startRecordingTime = DateTime.FromFileTime(startTime);
            
            if (_state.recorderID != recorderId)
                return;
            
            if (_controller != null && _controller.debugLogs)
                ExtendedLogger.LogInfo(GetType().Name, "Received start recording time: " + startRecordingTime + ". Recorder id: " + _state.recorderID, this);

            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            if (_startRecordingCoroutine != null)
            {
                StopCoroutine(_startRecordingCoroutine);
                _startRecordingCoroutine = null;
            }

            if (_globalSynchronizationTime > startRecordingTime)
            {
                TimeSpan difference = _globalSynchronizationTime - startRecordingTime;
                ExtendedLogger.LogError(GetType().Name, "The recording should have started already! Time difference: " + difference.TotalMilliseconds + " ms.  Potential fix: increase the  maxSynchronizationTime!", this);
                StartRecordingIfStateAllows(startRecordingTime);
            }
            else
            {
                _startRecordingCoroutine = StartCoroutine(StartRecordingWhenReady(startRecordingTime));
            }
        }

        private IEnumerator StartRecordingWhenReady(DateTime startRecordingTime)
        {
            yield return WaitUntilSynchronizedTime(startRecordingTime);
            _startRecordingCoroutine = null;
            StartRecordingIfStateAllows(startRecordingTime);
        }

        private void StartRecordingIfStateAllows(DateTime startRecordingTime)
        {
            if (_state.currentState == State.PrepareRecording)
            {
                _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
                TimeSpan diff = _globalSynchronizationTime - startRecordingTime;
                RegisterRecordingStartGlobalTimeOffset(_state.recorderID, (float)diff.TotalMilliseconds);
                _controller.StartRecording();
            }
            else
            {
                ExtendedLogger.LogWarning(GetType().Name, "A request to start a recording was sent but the current state does not allow starting new recording.", this);
            }
        }
        #endregion

        #region RecordingStop
        private IEnumerator WaitUntilSynchronizedTime(DateTime time)
        {
            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();

            while (_globalSynchronizationTime < time)
            {
                TimeSpan difference = _globalSynchronizationTime - time;
                _globalRecordStartDifference = difference;
                yield return null;
                _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            }

            if (_controller != null && _controller.debugLogs)
                ExtendedLogger.LogInfo(GetType().Name, "Target time passed.", this);
        }

        public void EndRecordingOnAllClientsEvent()
        {
            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            DateTime endRecordingTime = _globalSynchronizationTime.AddMilliseconds(maxSynchronizationTimeMS);
            EndRecordingOnServerRpc(endRecordingTime.ToFileTime(), _state.recorderID);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void EndRecordingOnServerRpc(long stopTime, int recorderId)
        {
            EndRecordingOnClientRpc(stopTime, recorderId);
        }
        
        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void EndRecordingOnClientRpc(long stopTime, int recorderId)
        {
            DateTime stopRecordingTime = DateTime.FromFileTime(stopTime);


            if (recorderId != _state.recorderID)
                return;
            
            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            if (_endRecordingCoroutine != null)
            {
                StopCoroutine(_endRecordingCoroutine);
                _endRecordingCoroutine = null;
            }

            if (_globalSynchronizationTime > stopRecordingTime)
            {
                TimeSpan difference = _globalSynchronizationTime - stopRecordingTime;
                ExtendedLogger.LogError(GetType().Name, "The recording should have stopped already! Time difference: " +
                               difference.TotalMilliseconds +
                               " ms.  Potential fix: increase the  maxSynchronizationTime!", this);
                StopRecording();
            }
            else
            {
                _endRecordingCoroutine = StartCoroutine(StopRecordingWhenReady(stopRecordingTime));
            }
        }

        private IEnumerator StopRecordingWhenReady(DateTime stopRecordingTime)
        {
            yield return WaitUntilSynchronizedTime(stopRecordingTime);
            _endRecordingCoroutine = null;
            StopRecording();
        }

        private void StopRecording()
        {
            if (_startRecordingCoroutine != null)
            {
                StopCoroutine(_startRecordingCoroutine);
                _startRecordingCoroutine = null;
            }

            _controller.EndRecording();
        }
        
        #endregion

        #region ReplayStart
        // Server only: schedules the replay start on all peers.
        private void StartReplayOnAllClients()
        {
            if (!IsServer)
            {
                ExtendedLogger.LogWarning(GetType().Name, "Only the server can start a synchronized replay.", this);
                return;
            }

            if (_controller != null && _controller.debugLogs)
                ExtendedLogger.LogInfo(GetType().Name, "Sending event to start replay on all clients.", this);

            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            DateTime startReplayTime = _globalSynchronizationTime.AddMilliseconds(maxSynchronizationTimeMS);
            DebugDistributedReplayLog("StartReplayOnAllClients: now=" + _globalSynchronizationTime +
                                      ", target=" + startReplayTime +
                                      ", maxSynchronizationTimeMS=" + maxSynchronizationTimeMS +
                                      ", recorderId=" + _state.recorderID +
                                      ", selectedReplayFile=" + _state.selectedReplayFile +
                                      ", recordingDirectory=" + _state.recordingDirectory +
                                      ", selectedServer=" + _state.selectedServer);
            DebugDistributedReplayStatusIfChanged("StartReplayOnAllClients");
            StartReplayRpc(startReplayTime.ToFileTime(), _state.recorderID);
        }
        
        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void StartReplayRpc(long startTime, int recorderId)
        {
            DateTime startReplayTime = DateTime.FromFileTime(startTime);

            if (recorderId != _state.recorderID)
            {
                DebugDistributedReplayLog("StartReplayRpc ignored: incomingRecorderId=" + recorderId +
                                          ", localRecorderId=" + _state.recorderID);
                return;
            }
            
            if (_controller != null && _controller.debugLogs)
                ExtendedLogger.LogInfo(GetType().Name, "Start replay event received for recorder id: " + _state.recorderID, this);
            
            if (!_replayStarted)
                if (_controller != null && _controller.debugLogs)
                    ExtendedLogger.LogInfo(GetType().Name, "Replay not yet started.", this);

            bool isDownloading = IsDownloading();
            DebugDistributedReplayLog("StartReplayRpc: target=" + startReplayTime +
                                      ", now=" + NetworkUtils.GetSynchronizedTime() +
                                      ", state=" + _state.currentState +
                                      ", isDownloading=" + isDownloading +
                                      ", replayStarted=" + _replayStarted +
                                      ", localDownloads=" + LocalDownloadStatusSummary());
            DebugDistributedReplayStatusIfChanged("StartReplayRpc");

            if (!isDownloading)
                if (_controller != null && _controller.debugLogs)
                    ExtendedLogger.LogInfo(GetType().Name, "Not downloading files.", this);

            if (!_replayStarted && !isDownloading && _state.currentState == State.PreparingReplay)
            {
                _replayStarted = true;

                _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
                if (_globalSynchronizationTime > startReplayTime)
                {
                    TimeSpan difference = _globalSynchronizationTime - startReplayTime;
                    ExtendedLogger.LogError(GetType().Name, "The replay should have started already! Time difference: " +
                                   difference.TotalMilliseconds +
                                   " ms.  Potential fix: increase the  maxSynchronizationTime!", this);
                    StartReplayIfStateAllows();
                }
                else
                {
                    if (_startReplayCoroutine != null)
                    {
                        DebugDistributedReplayLog("StartReplayRpc: stopping previous start replay coroutine before scheduling a new one.");
                        StopCoroutine(_startReplayCoroutine);
                    }

                    DebugDistributedReplayLog("StartReplayRpc: scheduling StartReplayWhenReady for target=" + startReplayTime);
                    _startReplayCoroutine = StartCoroutine(StartReplayWhenReady(startReplayTime));
                }
            }
            else
            {
                DebugDistributedReplayLog("StartReplayRpc: replay start conditions not met. state=" +
                                          _state.currentState +
                                          ", isDownloading=" + isDownloading +
                                          ", replayStarted=" + _replayStarted);
            }
        }
        
        private IEnumerator StartReplayWhenReady(DateTime startReplayTime)
        {
            DebugDistributedReplayLog("StartReplayWhenReady: waiting for target=" + startReplayTime);
            yield return WaitUntilSynchronizedTime(startReplayTime);
            _startReplayCoroutine = null;
            DebugDistributedReplayLog("StartReplayWhenReady: target reached, evaluating start conditions.");
            StartReplayIfStateAllows();
        }

        private void StartReplayIfStateAllows()
        {
            if (_state.currentState != State.PreparingReplay)
            {
                _replayStarted = false;
                DebugDistributedReplayStatusIfChanged("StartReplayIfStateAllows blocked by state");
                ExtendedLogger.LogWarning(GetType().Name, "A request to start a replay was sent but the current state does not allow starting replay.", this);
                return;
            }

            if (IsDownloading())
            {
                _replayStarted = false;
                DebugDistributedReplayStatusIfChanged("StartReplayIfStateAllows blocked by download");
                ExtendedLogger.LogWarning(GetType().Name, "A request to start a replay was sent but downloads are still running.", this);
                return;
            }

            if (_controller != null && _controller.debugLogs)
                ExtendedLogger.LogInfo(GetType().Name, "Target time passed. Starting replay.", this);
            DebugDistributedReplayLog("StartReplayIfStateAllows: calling RecorderController.StartReplay.");
            _controller.StartReplay();
            DebugDistributedReplayLog("StartReplayIfStateAllows: RecorderController.StartReplay returned. state=" +
                                      _state.currentState);
        }

        public void EndReplayOnAllClientsEvent()
        {
            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            DateTime stopReplayTime = _globalSynchronizationTime.AddMilliseconds(maxSynchronizationTimeMS);
            EndReplayOnServerRpc(stopReplayTime.ToFileTime(), _state.recorderID);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void EndReplayOnServerRpc(long stopTime, int recorderId)
        {
            _awaitingDownloads = false;
            _clientDownloading.Clear();
            EndReplayOnClientRpc(stopTime, recorderId);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void EndReplayOnClientRpc(long stopTime, int recorderId)
        {
            DateTime stopReplayTime = DateTime.FromFileTime(stopTime);

            if (recorderId != _state.recorderID)
                return;
            
            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            if (_endReplayCoroutine != null)
            {
                StopCoroutine(_endReplayCoroutine);
                _endReplayCoroutine = null;
            }

            if (_globalSynchronizationTime > stopReplayTime)
            {
                TimeSpan difference = _globalSynchronizationTime - stopReplayTime;
                ExtendedLogger.LogError(GetType().Name, "The replay should have stopped already! Time difference: " +
                                        difference.TotalMilliseconds +
                                        " ms.  Potential fix: increase the  maxSynchronizationTime!", this);
                StopReplay();
            }
            else
            {
                _endReplayCoroutine = StartCoroutine(StopReplayWhenReady(stopReplayTime));
            }
        }

        private IEnumerator StopReplayWhenReady(DateTime stopReplayTime)
        {
            yield return WaitUntilSynchronizedTime(stopReplayTime);
            _endReplayCoroutine = null;
            StopReplay();
        }

        private void StopReplay()
        {
            if (_startReplayCoroutine != null)
            {
                StopCoroutine(_startReplayCoroutine);
                _startReplayCoroutine = null;
            }

            _replayStarted = false;
            _controller.EndReplay();
        }
        #endregion

        #region ReplayTimeControl
        public void TogglePlayPauseReplayOnAllClients(bool paused)
        {
            TogglePlayPauseReplayServerRpc(paused, _state.recorderID);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void TogglePlayPauseReplayServerRpc(bool isPaused, int recorderId)
        {
            //_state.replayPaused = isPaused;
            TogglePlayPauseReplayClientRpc(isPaused, recorderId);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void TogglePlayPauseReplayClientRpc(bool isPaused, int recorderId)
        {
            if(recorderId != _state.recorderID)
                return;
            
            _state.replayPaused = isPaused;
        }

        public void UpdateUserReplayTimeOnAllClientsEvent(float currentUserTime)
        {
            if(NetworkUser.LocalInstance != null)
                UpdateUserReplayTimeRpc(currentUserTime, _state.recorderID, NetworkUser.LocalInstance.name);
        }

        [Rpc(SendTo.Everyone)]
        private void UpdateUserReplayTimeRpc(float userReplayTime, int recorderId, string userName)
        {
            if(recorderId != _state.recorderID)
                return;
            
            if (!_userReplayTimes.ContainsKey(userName))
            {
                _userReplayTimes.Add(userName, userReplayTime);
                _userReplayTimesUpdateTime.Add(userName, Time.time);
            }
            else
            {
                _userReplayTimes[userName] = userReplayTime;
                _userReplayTimesUpdateTime[userName] = Time.time;
            }

            // TODO: Fix this
            /*
            foreach (var key in _userReplayTimes.Keys.ToList())
            {
                if (!PhotonNetwork.CurrentRoom.Players.ContainsKey(key))
                {
                    _userReplayTimes.Remove(key);
                    _userReplayTimesUpdateTime.Remove(key);
                }
            }*/
        }
        
        /// <summary>
        /// Called on the host each replay tick to broadcast the authoritative replay time to all
        /// clients, which use it as the synchronization target in synchronized playback.
        /// </summary>
        public void PublishHostReplayTime(float replayTime)
        {
            if (!IsServer)
                return;

            HostReplayTime = replayTime;
            PublishHostReplayTimeRpc(replayTime, _state.recorderID);
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        private void PublishHostReplayTimeRpc(float replayTime, int recorderId)
        {
            if (recorderId != _state.recorderID)
                return;

            HostReplayTime = replayTime;
        }
        #endregion
        
        #region ReplayListManagement
        public void UpdateReplayList()
        {
            // When using local replay files, populate the list from the local recording directory
            // and do not query the server (which would otherwise log connection errors when offline).
            if (_state.useLocalReplayFiles)
            {
                UpdateLocalReplayList();
                return;
            }

            StartCoroutine(GetReplayList(_state.selectedServer, _state.projectName));
        }

        private void UpdateLocalReplayList()
        {
            string directory = string.IsNullOrEmpty(_state.recordingDirectory)
                ? Application.persistentDataPath
                : _state.recordingDirectory;

            if (!Directory.Exists(directory))
                return;

            string[] filesWithPaths = Directory.GetFiles(directory, "*.recordmeta");
            string[] replayNames = new string[filesWithPaths.Length];
            for (int i = 0; i < filesWithPaths.Length; i++)
                replayNames[i] = Path.GetFileNameWithoutExtension(filesWithPaths[i]);

            if (_state.replayList == null || _state.replayList.replayNames == null ||
                replayNames.Length != _state.replayList.replayNames.Length)
            {
                _state.replayList = new ReplayList { replayNames = replayNames };
            }
        }
        
        private IEnumerator GetReplayList(string serverAddress, string projectName)
        {
            string completeURL = serverAddress + "/all_recording_names/project/" + projectName;

            using (var uwr = new UnityWebRequest(completeURL, UnityWebRequest.kHttpVerbGET))
            {
                DownloadHandlerBuffer dH = new DownloadHandlerBuffer();
                uwr.downloadHandler = dH;

                yield return uwr.SendWebRequest();
                if (uwr.result != UnityWebRequest.Result.Success)
                    ExtendedLogger.LogError(GetType().Name, uwr.error + ", url: " + completeURL, this);
                else
                {
                    string response = uwr.downloadHandler.text;
                    ReplayList newReplayList = JsonUtility.FromJson<ReplayList>(response);
          
                    if (_state.replayList == null || _state.replayList.replayNames == null || newReplayList.replayNames.Length != _state.replayList.replayNames.Length)
                        _state.replayList = newReplayList;
                }
            }
        }
        #endregion
        
        #region  Helper
        private IEnumerator KeepClockSynchronized()
        {
            while (true)
            {
                var syncTask = Task.Run(() => NetworkUtils.SynchronizeClock(true));
                yield return new WaitUntil(() => syncTask.IsCompleted);
                yield return new WaitForSeconds(30f);
            }
        }
        
        public void Upload(string projectName, string filePath, string fileName, string serverAddress)
        {

            StartCoroutine(NetworkUtils.UploadToServer(projectName, filePath, fileName, serverAddress));
        }

        public bool IsServerOrHost()
        {
            return IsHost || IsServer;
        }
        
        private void DebugDistributedReplayLog(string message)
        {
            if (_controller == null || !_controller.debugLogs)
                return;

            ExtendedLogger.LogInfo(GetType().Name, "[DistributedReplayDebug][frame=" + Time.frameCount +
                                                   "][time=" + Time.realtimeSinceStartup.ToString("F3") + "] " + message, this);
        }

        private void DebugDistributedReplayStatusIfChanged(string context)
        {
            if (_controller == null || !_controller.debugLogs)
                return;

            string status = "state=" + _state.currentState +
                            ", replayStarted=" + _replayStarted +
                            ", awaitingDownloads=" + _awaitingDownloads +
                            ", localDownloads=" + LocalDownloadStatusSummary() +
                            ", clients=" + UserDownloadStatusSummary();

            if (_lastDistributedReplayStatusLog == status)
                return;

            _lastDistributedReplayStatusLog = status;
            DebugDistributedReplayLog(context + ": " + status);
        }
        #endregion
    }
}
