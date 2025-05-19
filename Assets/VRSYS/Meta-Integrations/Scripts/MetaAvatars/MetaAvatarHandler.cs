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

public class MetaAvatarHandler : NetworkBehaviour
{
    #region Member Variables

    [Header("Avatar Prefabs")]
    [SerializeField] private SampleAvatarEntity localAvatarPrefab;
    [SerializeField] private SampleAvatarEntity remoteAvatarPrefab;

    [Header("Avatar Components")]
    [SerializeField] private SampleInputManager bodyTracking;
    [SerializeField] private OvrAvatarLipSyncBehavior lipSync;
    [SerializeField] private OvrAvatarFacePoseBehavior facePose;
    [SerializeField] private OvrAvatarEyePoseBehavior eyePose;

    [Header("Avatar Events")]
    public UnityEvent<SampleAvatarEntity> onLocalAvatarEntitySpawned = new UnityEvent<SampleAvatarEntity>();
    public UnityEvent onSkeletonLoaded = new UnityEvent();
    public UnityEvent onAvatarLoaded = new UnityEvent();

    private SampleAvatarEntity localAvatar;
    private SampleAvatarEntity remoteAvatar;
    private bool remoteAvatarIsLoaded = false;
    private bool skeletonLoaded = false;

    private ulong userId; // Oculus user id
    private WaitForSeconds waitTime = new WaitForSeconds(.08f); // avatar update rate

    #endregion

    #region Mono- & NetworkBehaviour Callbacks

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // Instantiate remote avatar for remote clients
            remoteAvatar = Instantiate(remoteAvatarPrefab, transform);

            ClearComponentsOnRemote();
            return;
        }

        localAvatar = GetComponentInChildren<SampleAvatarEntity>();
        
        if(localAvatar == null)
            localAvatar = Instantiate(localAvatarPrefab, transform);
        
        onLocalAvatarEntitySpawned.Invoke(localAvatar);
        
        SetupLocalAvatarEntity();
        
        // Get oculus user id
        OvrPlatformInit.InitializeOvrPlatform();
        Oculus.Platform.Users.GetLoggedInUser().OnComplete(message =>
        {
            if (!message.IsError)
                userId = message.Data.ID;
            else
            {
                var e = message.GetError();
                OvrAvatarLog.LogError($"Error loading CDN avatar: {e.Message}. Falling back to local avatar");
            }
        });
        
        // Start streaming avatar data
        StartCoroutine(StreamAvatarData());
    }

    #endregion

    #region Custom Methods

    private void ClearComponentsOnRemote()
    {
        Destroy(bodyTracking.gameObject);
        Destroy(lipSync.gameObject);
        Destroy(facePose.gameObject);
        Destroy(eyePose.gameObject);
    }

    private void SetupLocalAvatarEntity()
    {
        localAvatar.OnSkeletonLoadedEvent.AddListener(OnSkeletonLoaded);
        localAvatar.OnDefaultAvatarLoadedEvent.AddListener(OnAvatarLoaded);
        localAvatar.OnUserAvatarLoadedEvent.AddListener(OnAvatarLoaded);
        
        if(bodyTracking != null)
            localAvatar.SetBodyTracking(bodyTracking);
        
        if(lipSync != null)
            localAvatar.SetLipSync(lipSync);
        
        if(facePose != null)
            localAvatar.SetFacePoseProvider(facePose);
        
        if(eyePose != null)
            localAvatar.SetEyePoseProvider(eyePose);
    }
    
    private void OnSkeletonLoaded(OvrAvatarEntity arg0)
    {
        skeletonLoaded = true;
        onSkeletonLoaded.Invoke();
    }
    
    private void OnAvatarLoaded(OvrAvatarEntity arg0)
    {
        onAvatarLoaded.Invoke();
    }

    public SampleAvatarEntity GetLocalAvatarEntity()
    {
        return localAvatar;
    }

    #endregion

    #region RPCs

    [Rpc(SendTo.NotMe)]
    private void SendAvatarDataRpc(byte[] data, ulong userId)
    {
        if (!remoteAvatarIsLoaded && userId != 0)
        {
            remoteAvatar.LoadRemoteUserCdnAvatar(userId);
            remoteAvatarIsLoaded = true;
        }

        if(remoteAvatarIsLoaded)
            remoteAvatar.ApplyStreamData(data);
    }

    #endregion

    #region Coroutines

    private IEnumerator StreamAvatarData()
    {
        while (true)
        {
            if (skeletonLoaded)
            {
                var data = localAvatar.RecordStreamData(localAvatar.activeStreamLod);
                SendAvatarDataRpc(data, userId);
            }
            
            yield return waitTime;
        }
    }

    #endregion
}
