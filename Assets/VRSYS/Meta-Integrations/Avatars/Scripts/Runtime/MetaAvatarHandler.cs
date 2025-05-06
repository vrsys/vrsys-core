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
    public UnityEvent onSkeletonLoaded = new UnityEvent();

    [Header("Debug")] 
    public bool verbose = true;

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

        localAvatar = Instantiate(localAvatarPrefab, transform);
        localAvatar.OnSkeletonLoadedEvent.AddListener(OnSkeletonLoaded);
        
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
    
    private void OnSkeletonLoaded(OvrAvatarEntity arg0)
    {
        if(verbose)
            ExtendedLogger.LogInfo(GetType().Name, "Skeleton was loaded.");
        
        skeletonLoaded = true;
        onSkeletonLoaded.Invoke();
    }

    private void SetupLocalAvatarEntity()
    {
        if(bodyTracking != null)
            localAvatar.SetBodyTracking(bodyTracking);
        
        if(lipSync != null)
            localAvatar.SetLipSync(lipSync);
        
        if(facePose != null)
            localAvatar.SetFacePoseProvider(facePose);
        
        if(eyePose != null)
            localAvatar.SetEyePoseProvider(eyePose);
    }

    #endregion

    #region RPCs

    [Rpc(SendTo.NotMe)]
    private void SendAvatarDataRpc(byte[] data, ulong userId)
    {
        if(verbose)
            ExtendedLogger.LogInfo(GetType().Name, "Received avatar data with byte array size of: " + data.Length + ". From user id: " + userId, this);
        if (!remoteAvatarIsLoaded && userId != 0)
        {
            if(verbose)
                ExtendedLogger.LogInfo(GetType().Name, "Load avatar for user id: " + userId, this); 
            
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
            if(verbose)
                ExtendedLogger.LogInfo(GetType().Name, "Trying to stream avatar data...", this);
            
            if (skeletonLoaded)
            {
                if(verbose)
                    ExtendedLogger.LogInfo(GetType().Name, "Streaming avatar data...", this);
                
                var data = localAvatar.RecordStreamData(localAvatar.activeStreamLod);
                SendAvatarDataRpc(data, userId);
            }
            
            yield return waitTime;
        }
    }

    #endregion
}
