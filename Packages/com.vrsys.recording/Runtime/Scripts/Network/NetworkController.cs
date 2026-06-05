using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using VRSYS.Core.Avatar;
using VRSYS.Core.Networking;

namespace VRSYS.Scripts.Recording
{

    [RequireComponent(typeof(RecorderState))]
    [RequireComponent(typeof(RecorderController))]
    public class NetworkController : NetworkBehaviour
    {
        [DllImport("RecordingPlugin")]
        private static extern bool RegisterRecordingStartGlobalTimeOffset(int recorderId, float globalTimeOffset);

        public float maxSynchronizationTimeMS;
        [HideInInspector] public List<String> _userNames = new List<string>();
        [HideInInspector] public Dictionary<string, float> _userReplayTimes = new Dictionary<string, float>();
        [HideInInspector] public Dictionary<string, float> _userReplayTimesUpdateTime = new Dictionary<string, float>();
        [HideInInspector] public Dictionary<string, bool> _userDownloadStatus = new Dictionary<string, bool>();
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
        private bool _allUsersFinishedLoading = false;
        private bool _startReplayEventSent = false;
        
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

        private IEnumerator KeepClockSynchronized()
        {
            while (true)
            {
                var syncTask = Task.Run(() => NetworkUtils.SynchronizeClock(true));
                yield return new WaitUntil(() => syncTask.IsCompleted);
                yield return new WaitForSeconds(30f);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
        }
        
        public void SwitchSelectedServerEvent()
        {
            int newSelectedServerId = (_selectedServerId + 1) % _state.serverList.Count;
            SwitchSelectedServerServerRpc(newSelectedServerId, _state.recorderID);
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void SwitchSelectedServerServerRpc(int selectedServerId, int recorderId)
        {
           SwitchSelectedServerClientRPC(selectedServerId,recorderId);
        }
        
        [ClientRpc]
        private void SwitchSelectedServerClientRPC(int selectedServerId, int recorderId)
        {
            
            if(_state.recorderID != recorderId)
                return;
            
            Debug.Log("Switch selected server event received. Recorder id: " + _state.recorderID);

            _selectedServerId = selectedServerId;
            _state.selectedServer = _state.serverList[_selectedServerId];
            _state.replayList = new ReplayList();
            if(_serverText != null)
                _serverText.text = "Server: " + _state.selectedServer;
            
            UpdateReplayList();
        }
        
        public void PrepareRecordingOnAllClientsEvent()
        {
            PrepareRecordingOnServerRpc(_state.recorderID);
        }

        [ServerRpc(RequireOwnership = false)]
        private void PrepareRecordingOnServerRpc(int recorderId)
        {
            PrepareRecordingOnClientRpc(recorderId);
        }
        
        
        [ClientRpc]
        private void PrepareRecordingOnClientRpc(int recorderId)
        {
            if(_state.recorderID != recorderId)
                return;
            
            Debug.Log("Preparing for recording. Recorder id: " + _state.recorderID);
            
            _controller.PrepareRecording();
        }
        
        public void StartRecordingOnAllClientsEvent()
        {
            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            Debug.Log("Global time before offset: " + _globalSynchronizationTime);
            DateTime startRecordingTime = _globalSynchronizationTime.AddMilliseconds(maxSynchronizationTimeMS);
            Debug.Log("Start recording time: " + _globalSynchronizationTime);
            StartRecordingOnServerRpc(startRecordingTime.ToFileTime(), _state.recorderID);
        }

        [ServerRpc(RequireOwnership = false)]
        private void StartRecordingOnServerRpc(long startTime, int recorderId)
        {
            StartRecordingOnClientRpc(startTime, recorderId);
        }
        
        [ClientRpc]
        private void StartRecordingOnClientRpc(long startTime, int recorderId)
        {
            DateTime startRecordingTime = DateTime.FromFileTime(startTime);
            
            if (_state.recorderID != recorderId)
                return;
            
            Debug.Log("Received start recording time: " + startRecordingTime + ". Recorder id: " + _state.recorderID);

            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            if (_globalSynchronizationTime > startRecordingTime)
            {
                TimeSpan difference = _globalSynchronizationTime - startRecordingTime;
                Debug.LogError("The recording should have started already! Time difference: " + difference.TotalMilliseconds + " ms.  Potential fix: increase the  maxSynchronizationTime!");
                if (_state.currentState == State.PrepareRecording)
                {
                    _controller.StartRecording();
                }
            }
            else
            {
                Task.Run(() => WaitForRecordingAsync(startRecordingTime));
            }
        }

        private async void WaitForRecordingAsync(DateTime startRecordingTime)
        {
            await Task.Run(() => WaitUntilTime(startRecordingTime));
            
            if (_state.currentState == State.PrepareRecording)
            {
                TimeSpan diff = _globalSynchronizationTime - startRecordingTime;
                RegisterRecordingStartGlobalTimeOffset(_state.recorderID, (float)diff.TotalMilliseconds);
                _controller.StartRecording();
            }
            else
            {
                Debug.LogWarning("A request to start a recording was sent but the current state does not allow starting new recording.");
            }
        }

        private bool WaitUntilTime(DateTime time)
        {
            while (_globalSynchronizationTime < time)
            {
                TimeSpan difference = _globalSynchronizationTime - time;
                _globalRecordStartDifference = difference;
                _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
                Thread.Sleep(1);
            }
            Debug.Log("Target time passed.");
            return true;
        }

        public void EndRecordingOnAllClientsEvent()
        {
            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            DateTime endRecordingTime = _globalSynchronizationTime.AddMilliseconds(maxSynchronizationTimeMS);
            EndRecordingOnServerRpc(endRecordingTime.ToFileTime(), _state.recorderID);
        }

        [ServerRpc(RequireOwnership = false)]
        private void EndRecordingOnServerRpc(long stopTime, int recorderId)
        {
            EndRecordingOnClientRpc(stopTime, recorderId);
        }
        
        [ClientRpc]
        private void EndRecordingOnClientRpc(long stopTime, int recorderId)
        {
            DateTime stopRecordingTime = DateTime.FromFileTime(stopTime);


            if (recorderId != _state.recorderID)
                return;
            
            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            if (_globalSynchronizationTime > stopRecordingTime)
            {
                TimeSpan difference = _globalSynchronizationTime - stopRecordingTime;
                Debug.LogError("The recording should have stopped already! Time difference: " +
                               difference.TotalMilliseconds +
                               " ms.  Potential fix: increase the  maxSynchronizationTime!");
            }
            else
            {
                while (_globalSynchronizationTime < stopRecordingTime)
                {
                    _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
                    Thread.Sleep(1);
                }
            }

            _controller.EndRecording();
            
            _allUsersFinishedLoading = false;
        }
        
        public void StartReplayOnAllClientsEvent()
        {
            
            Debug.Log("Sending event to start replay on all clients.");
            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            DateTime startReplayTime = _globalSynchronizationTime.AddMilliseconds(maxSynchronizationTimeMS);
            StartReplayOnServerRpc(startReplayTime.ToFileTime(), _state.recorderID);
        }

        [ServerRpc(RequireOwnership = false)]
        private void StartReplayOnServerRpc(long startTime, int recorderId)
        {
            StartReplayOnClientRpc(startTime, recorderId);
        }
        
        [ClientRpc]
        private void StartReplayOnClientRpc(long startTime, int recorderId)
        {
            DateTime startReplayTime = DateTime.FromFileTime(startTime);

            if (recorderId != _state.recorderID)
                return;
            
            Debug.Log("Start replay event received for recorder id: " + _state.recorderID);
            
            if (!_replayStarted)
                Debug.Log("Replay not yet started.");

            if (!IsDownloading())
                Debug.Log("Not downloading files.");

            if (!_replayStarted && !IsDownloading() && _state.currentState == State.PreparingReplay)
            {
                _replayStarted = true;

                _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
                if (_globalSynchronizationTime > startReplayTime)
                {
                    TimeSpan difference = _globalSynchronizationTime - startReplayTime;
                    Debug.LogError("The replay should have started already! Time difference: " +
                                   difference.TotalMilliseconds +
                                   " ms.  Potential fix: increase the  maxSynchronizationTime!");
                    _controller.StartReplay();
                }
                else
                {
                    Task.Run(() => WaitForReplayAsync(startReplayTime));
                    Debug.Log("Target time passed. Starting replay.");
                    _controller.StartReplay();
                }
            }
        }
        
        private async void WaitForReplayAsync(DateTime startReplayTime)
        {
            bool finished = await Task.Run(() => WaitUntilTime(startReplayTime));
        }

        public void EndReplayOnAllClientsEvent()
        {
            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            DateTime stopReplayTime = _globalSynchronizationTime.AddMilliseconds(maxSynchronizationTimeMS);
            EndReplayOnServerRpc(stopReplayTime.ToFileTime(), _state.recorderID);
        }

        [ServerRpc(RequireOwnership = false)]
        private void EndReplayOnServerRpc(long stopTime, int recorderId)
        {
            EndReplayOnClientRpc(stopTime, recorderId);
        }

        [ClientRpc]
        private void EndReplayOnClientRpc(long stopTime, int recorderId)
        {
            DateTime stopReplayTime = DateTime.FromFileTime(stopTime);

            if (recorderId != _state.recorderID)
                return;
            
            _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
            if (_globalSynchronizationTime > stopReplayTime)
            {
                TimeSpan difference = _globalSynchronizationTime - stopReplayTime;
                Debug.LogError("The replay should have stopped already! Time difference: " +
                               difference.TotalMilliseconds +
                               " ms.  Potential fix: increase the  maxSynchronizationTime!");
            }
            else
            {
                while (_globalSynchronizationTime < stopReplayTime)
                {
                    _globalSynchronizationTime = NetworkUtils.GetSynchronizedTime();
                    Thread.Sleep(1);
                }
            }
            
            _controller.EndReplay();
        }

        public void StartDownloadOnAllClientsEvent()
        {
            
            if (_state.selectedReplayFile == "")
            {
                Debug.LogError("No replay file selected!");
                return;
            }

            Debug.Log("Started download on all clients");
            StartDownloadsServerRpc(_state.selectedReplayFile, _state.recorderID);
        }

        [ServerRpc(RequireOwnership = false)]
        private void StartDownloadsServerRpc(string replayFile, int recorderId)
        {
            StartDownloadsClientRpc(replayFile, recorderId);
        }

        [ClientRpc]
        private void StartDownloadsClientRpc(string replayFile, int recorderId)
        {
            Debug.Log("Test");
            if (_state.currentState == State.Idle)
            {
                _state.selectedReplayFile = replayFile;
                _state.fixedPlaybackRecordingName = replayFile;
                
                if(recorderId != _state.recorderID)
                    return;
                
                Debug.Log("Download started for recorder id: " + _state.recorderID);
                Debug.Log("Selected replay file: " + _state.selectedReplayFile);

                _soundsDownloaded = false;
                _transformsDownloaded = false;
                _metaInformationDownloaded = false;
                _genericDownloaded = false;
                _replayStarted = false;
                _allUsersFinishedLoading = false;
                _startReplayEventSent = false;

                if (_state.recordingDirectory == "" || true)
                {
                    _state.recordingDirectory = Application.persistentDataPath;
                }

                StartDownloadCoroutines();
                _userDownloadStatus.Clear();

                _state.currentState = State.PreparingReplay;
                
                foreach (var player in _userNames)
                {
                    _userDownloadStatus.Add(player, false);
                }
            }
        }
        
        public void StartDownloads()
        {
            if (_state.currentState == State.PreparingReplay)
            {
                Debug.Log("Local download started");
                Debug.Log("Selected replay file: " + _state.selectedReplayFile);

                _soundsDownloaded = false;
                _transformsDownloaded = false;
                _metaInformationDownloaded = false;
                _genericDownloaded = false;
                _replayStarted = false;
                _allUsersFinishedLoading = false;
                _startReplayEventSent = false;

                if (_state.recordingDirectory == "")
                {
                    _state.recordingDirectory = Application.persistentDataPath;
                }

                StartDownloadCoroutines();         
            }
        }
        
        public void UpdateDownloadStatusEvent()
        {
            
            bool downloadState = IsDownloading();
            object[] recordingData = new object[] { downloadState, _state.recorderID };
            string userName = "Local User";
            if(NetworkUser.LocalInstance != null)
                userName = NetworkUser.LocalInstance.name;

            if (_state.currentState == State.PreparingReplay)
            {
                if (_controller.localPlayback)
                {
                    if (!downloadState)
                    {
                        Debug.Log("Download of files finished. Starting local playback.");
                        _controller.StartReplay();
                    }
                }
                else
                {
                    UpdateDownloadStatusServerRpc(downloadState, _state.recorderID, userName);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void UpdateDownloadStatusServerRpc(bool downloadStatus, int recorderId, string userName)
        {
            UpdateDownloadStatusClientRpc(downloadStatus, recorderId, userName);
        }
        

        [ClientRpc]
        private void UpdateDownloadStatusClientRpc(bool downloadStatus, int recorderId, string userName)
        {
            if(recorderId != _state.recorderID)
                return;
            
            if (!_allUsersFinishedLoading)
            {
                //Debug.Log("Player: " + sender + ", Download state: " + downloadStatus);

                if (!_userNames.Contains(userName))
                {
                    _userNames.Add(userName);
                }
                foreach (var player in _userNames)
                {
                    if (player == NetworkUser.LocalInstance.name)
                    {
                        if (_userDownloadStatus.ContainsKey(player))
                        {
                            _userDownloadStatus[player] = downloadStatus;
                        }
                        else
                        {
                            _userDownloadStatus.Add(player, downloadStatus);
                        }
                    }
                }

                // TODO: Fix this!
                /*
                foreach (var player in _userDownloadStatus.Keys.ToList())
                {
                    if (!PhotonNetwork.CurrentRoom.Players.ContainsKey(player))
                    {
                        _userDownloadStatus.Remove(player);
                    }
                }*/
                
                _allUsersFinishedLoading = true;
                foreach (var player in _userNames)
                {
                    if (!_userDownloadStatus.ContainsKey(player))
                    {
                        //Debug.Log("User: " + player.ActorNumber + " not finished downloading.");
                        _userDownloadStatus[player] = false;
                        _allUsersFinishedLoading = false;
                    }
                    else if (_userDownloadStatus[player])
                    {
                        //Debug.Log("User: " + player.ActorNumber + " not finished downloading.");
                        _allUsersFinishedLoading = false;
                    }
                }
                
                _allUsersFinishedLoading = _allUsersFinishedLoading && !IsDownloading();

                //if (_allUsersFinishedLoading)
                //    Debug.Log("All user finished downloading the recording file.");

                //if (!_replayStarted)
                //    Debug.Log("Replay has not started yet.");

                //if (!IsDownloading())
                //    Debug.Log("Not downloading anymore.");
                //else 
                //    Debug.Log("Still downloading.");
            }

            if (_allUsersFinishedLoading && !_replayStarted && !_startReplayEventSent)
            {
                StartReplayOnAllClientsEvent();
            }
        }

        public void TogglePlayPauseReplayOnAllClients(bool paused)
        {
            TogglePlayPauseReplayServerRpc(paused, _state.recorderID);
        }

        [ServerRpc(RequireOwnership = false)]
        private void TogglePlayPauseReplayServerRpc(bool isPaused, int recorderId)
        {
            //_state.replayPaused = isPaused;
            TogglePlayPauseReplayClientRpc(isPaused, recorderId);
        }
        

        [ClientRpc]
        private void TogglePlayPauseReplayClientRpc(bool isPaused, int recorderId)
        {
            if(recorderId != _state.recorderID)
                return;
            
            _state.replayPaused = isPaused;
        }

        public void UpdateUserReplayTimeOnAllClientsEvent(float currentUserTime)
        {
            if(NetworkUser.LocalInstance != null)
                UpdateUserReplayTimeServerRpc(currentUserTime, _state.recorderID, NetworkUser.LocalInstance.name);
        }

        [ServerRpc(RequireOwnership = false)]
        private void UpdateUserReplayTimeServerRpc(float userReplayTime, int recorderId, string userName)
        {
            UpdateUserReplayTimeClientRpc(userReplayTime, recorderId, userName);
        }

        [ClientRpc]
        private void UpdateUserReplayTimeClientRpc(float userReplayTime, int recorderId, string userName)
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
            PublishHostReplayTimeClientRpc(replayTime, _state.recorderID);
        }

        [ClientRpc]
        private void PublishHostReplayTimeClientRpc(float replayTime, int recorderId)
        {
            if (recorderId != _state.recorderID)
                return;

            HostReplayTime = replayTime;
        }

        public void Upload(string projectName, string filePath, string fileName, string serverAddress)
        {

            StartCoroutine(NetworkUtils.UploadToServer(projectName, filePath, fileName, serverAddress));
        }

        public void UpdateReplayList()
        {
            
            // if (_state.selectedServer.Length == 0 && _state.serverList.Count > 0)
            // {
            //     _selectedServerId = 0;
            //     _state.selectedServer = _state.serverList[_selectedServerId];
            // }
            //
            // if (!_state.serverList.Contains(_state.selectedServer))
            // {
            //     _selectedServerId = 0;
            //     _state.selectedServer = _state.serverList[_selectedServerId];
            // }
            //
            // if (!_state.selectedServer.Contains("http"))
            //     _state.selectedServer = "http://" + _state.selectedServer;

            StartCoroutine(GetReplayList(_state.selectedServer, _state.projectName));
        }

        public void UpdateSelectedServerText(TextMeshProUGUI text)
        {
            if(text == null)
                return;

            if (_serverText == null)
                _serverText = text;

            _serverText.text = "Server: " + _state.selectedServer;
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
                    Debug.LogError(uwr.error + ", url: " + completeURL);
                else
                {
                    string response = uwr.downloadHandler.text;
                    ReplayList newReplayList = JsonUtility.FromJson<ReplayList>(response);
          
                    if (_state.replayList == null || _state.replayList.replayNames == null || newReplayList.replayNames.Length != _state.replayList.replayNames.Length)
                        _state.replayList = newReplayList;
                }
            }
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
            Debug.Log("Started download of file from: " + completeURL);

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
                    
                    while (!uwr.isDone)
                    {
                        //Text downloadStatus = Utils.GetChildByName(gameObject,"DownloadStatus").GetComponent<Text>();
                        //downloadStatus.text = (uwr.downloadProgress * 100.0f).ToString("F0") + "%";

                        yield return null;
                    }

                    if (uwr.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError(uwr.error + ", url: " + completeURL);
                        
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
                }
                else
                {
                    Debug.Log("File already exists: " + file + ". Skipping download.");
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
            }
        }

        private void StartDownloadCoroutines()
        {
            string replayFile = _state.selectedReplayFile;
            string projectName = _state.projectName;
            StartCoroutine(DownloadFileFromServer(projectName, _state.recordingDirectory, "get_transform_recording", replayFile));
            StartCoroutine(DownloadFileFromServer(projectName, _state.recordingDirectory, "get_sound_recording", replayFile));
            StartCoroutine(DownloadFileFromServer(projectName, _state.recordingDirectory, "get_meta_recording", replayFile));
            StartCoroutine(DownloadFileFromServer(projectName, _state.recordingDirectory, "get_generic_recording", replayFile));
        }
    }
}