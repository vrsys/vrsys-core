using System;
using UnityEngine;
using VRSYS.Core.Logging;
using VRSYS.Meta.Avatars;

public class MetaAvatarReplayDataWriter : MonoBehaviour
{
    #region Properties

    
    private bool _initialized = false;
    private ulong _userId = 0;
    
    [Header("Replay Avatar")]
    [SerializeField] private VRSYSMetaAvatarEntity _replayEntityPrefab;
    private VRSYSMetaAvatarEntity _replayEntity;
    
    private bool _isReplaying = false;

    [Header("Debugging")] 
    [SerializeField] private bool _verbose = false;

    #endregion

    #region Private Methods

    public bool Initialize()
    {
        string preText = "Meta ID: ";
        int index = gameObject.name.IndexOf(preText, StringComparison.Ordinal);
        string userIdString= (index < 0) ? gameObject.name : gameObject.name.Remove(index, preText.Length);
        Debug.Log("Trying to parse Meta ID of GO: "  + gameObject.name + " : " + userIdString);
        ulong userId = Convert.ToUInt64(userIdString);
        Debug.Log("Parsed ID of GO: "  + gameObject.name + " : " + userId);
        _initialized = Initialize(userId);
        return _initialized;
    }
    
    public bool Initialize(ulong userId)
    {
        if (_initialized)
        {
            ExtendedLogger.LogWarning(GetType().Name, "Writer is already initialized.", this);
            return true;
        }
        
        _replayEntity = Instantiate(_replayEntityPrefab, transform);
        _replayEntity.Hidden = true;

        _userId = userId;
        _replayEntity.LoadAvatarByCdn(_userId);
        
        if(_verbose)
            ExtendedLogger.LogInfo(GetType().Name, "Writer is initialized!", this);
        return true;
    }

    public void StartReplay()
    {
        if (!_initialized)
        {
            ExtendedLogger.LogError(GetType().Name, "Writer is not initialized. Call Initialize() once first.", this);
            return;
        }
        
        if(_verbose)
            ExtendedLogger.LogInfo(GetType().Name, "Starting avatar replay...", this);

        _replayEntity.Hidden = false;
        _isReplaying = true;
    }

    public bool ApplyData(byte[] data)
    {
        if (!_initialized)
        {
            ExtendedLogger.LogError(GetType().Name, "Writer is not initialized. Call Initialize() once first.", this);
            return false;
        }
        
        if (!_isReplaying)
        {
            ExtendedLogger.LogWarning(GetType().Name, "Replaying data isn't started. Call StartReplay() first.", this);
            return false;
        }
        
        if(_verbose)
            ExtendedLogger.LogInfo(GetType().Name, $"Applying replay data. Data size: {data.Length}", this);
        
        if(_replayEntity.Hidden)
            _replayEntity.Hidden = false;
        _replayEntity.SetPlaybackTimeDelay(0.2f);
        return _replayEntity.ApplyStreamData(data);
    }

    public void StopReplay()
    {
        if(!_initialized || !_isReplaying)
            return;

        _replayEntity.Hidden = true;

        _isReplaying = false;
    }

    public void DestroyReplayEntity()
    {
        if(!_isReplaying)
            Destroy(_replayEntity);    
    }
    
    public void UpdateUserId(ulong userId)
    {
        if (!_initialized)
        {
            ExtendedLogger.LogError(GetType().Name, "Writer is not initialized. Call Initialize() once first.", this);
            return;
        }

        _userId = userId;
        _replayEntity.LoadAvatarByCdn(_userId);
    }
    
    public ulong GetUserId() => _userId;

    #endregion
}
