// VRSYS plugin of Virtual Reality and Visualization Group (Bauhaus-University Weimar)
//  _    ______  _______  _______
// | |  / / __ \/ ___/\ \/ / ___/
// | | / / /_/ /\__ \  \  /\__ \ 
// | |/ / _, _/___/ /  / /___/ / 
// |___/_/ |_|/____/  /_//____/  
//
//  __                            __                       __   __   __    ___ .  . ___
// |__)  /\  |  | |__|  /\  |  | /__`    |  | |\ | | \  / |__  |__) /__` |  |   /\   |  
// |__) /~~\ \__/ |  | /~~\ \__/ .__/    \__/ | \| |  \/  |___ |  \ .__/ |  |  /~~\  |  
//
//       ___               __                                                           
// |  | |__  |  |\/|  /\  |__)                                                          
// |/\| |___ |  |  | /~~\ |  \                                                                                                                                                                                     
//
// Copyright (c) 2023 Virtual Reality and Visualization Group
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//-----------------------------------------------------------------
//   Authors:        Tony Zoeppig
//   Date:           2025
//-----------------------------------------------------------------

using System.Collections;
using Oculus.Avatar2;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using VRSYS.Core.Logging;
using VRSYS.Meta.General;

namespace VRSYS.Meta.Avatars
{
    public class MetaAvatarHandler : NetworkBehaviour
    {
        #region Member Variables

        [Header("Avatar Prefabs")] 
        [SerializeField] private VRSYSMetaAvatarEntity _localAvatarPrefab;
        [SerializeField] private VRSYSMetaAvatarEntity _remoteAvatarPrefab;

        [Header("AvatarComponents")] 
        [SerializeField] private OvrAvatarInputManager _bodyTracking;
        [SerializeField] private OvrAvatarLipSyncBehavior _lipSync;
        [SerializeField] private OvrAvatarFacePoseBehavior _facePose;
        [SerializeField] private OvrAvatarEyePoseBehavior _eyePose;
        
        [Header("Avatar Events")]
        public UnityEvent<VRSYSMetaAvatarEntity> onLocalAvatarEntitySpawned = new UnityEvent<VRSYSMetaAvatarEntity>();
        public UnityEvent onSkeletonLoaded = new UnityEvent();
        public UnityEvent onAvatarLoaded = new UnityEvent();

        private ulong _userId
        {
            get
            {
                return VrsysOvrPlatformInitializer.Instance.LocalUserId;
            }
        }
        
        private VRSYSMetaAvatarEntity _localAvatar;
        private VRSYSMetaAvatarEntity _remoteAvatar;

        private bool _remoteAvatarIsLoaded = false;
        private bool _skeletonIsLoaded = false;

        [Header("Networking")] 
        [SerializeField] private float _remoteAvatarUpdateInterval = 0.08f;

        [Header("Debug")] 
        [SerializeField] private bool _verbose = false;

        #endregion

        #region Mono- & NEtworkBehaviour Callbacks

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                _remoteAvatar = Instantiate(_remoteAvatarPrefab, transform);

                ClearComponentsOnRemote();

                return;
            }

            _localAvatar = GetComponentInChildren<VRSYSMetaAvatarEntity>();

            if (_localAvatar == null)
                _localAvatar = Instantiate(_localAvatarPrefab, transform);
            
            onLocalAvatarEntitySpawned.Invoke(_localAvatar);

            SetupLocalAvatarEntity();
            
            StartCoroutine(StreamAvatarData());
        }

        #endregion

        #region Private Methods

        private void ClearComponentsOnRemote()
        {
            Destroy(_bodyTracking.gameObject);
            Destroy(_lipSync.gameObject);
            Destroy(_facePose.gameObject);
            Destroy(_eyePose.gameObject);
        }
        
        private void SetupLocalAvatarEntity()
        {
            _localAvatar.OnSkeletonLoadedEvent.AddListener(OnSkeletonLoaded);
            _localAvatar.OnDefaultAvatarLoadedEvent.AddListener(OnAvatarLoaded);
            _localAvatar.OnUserAvatarLoadedEvent.AddListener(OnAvatarLoaded);

            if (_bodyTracking != null)
                _localAvatar.SetBodyTracking(_bodyTracking);

            if (_lipSync != null)
                _localAvatar.SetLipSync(_lipSync);

            if (_facePose != null)
                _localAvatar.SetFacePoseProvider(_facePose);

            if (_eyePose != null)
                _localAvatar.SetEyePoseProvider(_eyePose);
        }

        #endregion

        #region Event Callbacks

        private void OnSkeletonLoaded(OvrAvatarEntity arg0)
        {
            _skeletonIsLoaded = true;
            onSkeletonLoaded.Invoke();
        }
        
        private void OnAvatarLoaded(OvrAvatarEntity arg0) => onAvatarLoaded.Invoke();

        #endregion

        #region Coroutines

        private IEnumerator StreamAvatarData()
        {
            while (true)
            {
                if (VrsysOvrPlatformInitializer.Instance.Initialized && _skeletonIsLoaded)
                {
                    var data = _localAvatar.RecordStreamData(_localAvatar.activeStreamLod);
                    SendAvatarDataRpc(data, _userId);
                }

                yield return new WaitForSeconds(_remoteAvatarUpdateInterval);
            }
        }

        #endregion

        #region RPCs

        [Rpc(SendTo.NotMe)]
        private void SendAvatarDataRpc(byte[] data, ulong userId)
        {
            if(_verbose)
                ExtendedLogger.LogInfo(GetType().Name, $"Received data for user cdn: {userId}", this);
            
            if (!_remoteAvatarIsLoaded && userId != 0)
            {
                _remoteAvatar.LoadAvatarByCdn(userId);
                _remoteAvatarIsLoaded = true;
            }

            if (_remoteAvatarIsLoaded)
                _remoteAvatar.ApplyStreamData(data);
        }

        #endregion
    }
}
