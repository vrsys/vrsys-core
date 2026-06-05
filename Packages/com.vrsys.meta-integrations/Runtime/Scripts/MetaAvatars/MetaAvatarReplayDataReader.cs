using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using VRSYS.Core.Logging;
using VRSYS.Meta.Avatars;
using VRSYS.Meta.General;

public class MetaAvatarReplayDataReader : NetworkBehaviour
{
    #region Struct

    public struct AvatarData
    {
        public ulong UserID;
        public byte[] Data;

        public AvatarData(ulong userID, byte[] data)
        {
            UserID = userID;
            Data = data;
        }
    }

    #endregion

    #region Properties
    
    private bool _initialized = false;

    private NetworkVariable<ulong> _userId = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private MetaAvatarHandler _metaAvatarHandler;
    private VRSYSMetaAvatarEntity _observedAvatar;

    [Header("Reading Configuration")] 
    [SerializeField] private float _readingInterval = 0.08f;
    [SerializeField] private bool _autoStartReading = false;

    [Header("Events")] 
    public UnityEvent<AvatarData> OnAvatarDataRead = new ();

    private bool _isReading;
    private Coroutine _readingCoroutine;

    [Header("Debugging")] 
    [SerializeField] private bool _verbose = false;

    #endregion

    #region Mono- & NetworkBehaviour Methods

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            if (VrsysOvrPlatformInitializer.Instance.Initialized)
            {
                _userId.Value = VrsysOvrPlatformInitializer.Instance.LocalUserId;
            }
            else
            {
                VrsysOvrPlatformInitializer.Instance.OnLocalUserIdRetrieved.AddListener(SetUserId);
            }
        }
        
        _metaAvatarHandler = GetComponent<MetaAvatarHandler>();
        
        if (_metaAvatarHandler == null)
        {
            ExtendedLogger.LogError(GetType().Name, "Not attached to a MetaAvatarHandler!", this);
            return;
        }
        
        Initialize();
    }

    #endregion

    #region Private Methods

    private void SetUserId(ulong userId) => _userId.Value = userId;

    private void Initialize()
    {
        if(_verbose)
            ExtendedLogger.LogInfo(GetType().Name, "Trying to initialize...", this);
        
        if (_userId.Value == 0)
        {
            Invoke(nameof(Initialize), 1f);
            return;
        }

        gameObject.name = "Meta ID: " + _userId.Value;

        _observedAvatar = IsOwner ? _metaAvatarHandler.LocalAvatar() : _metaAvatarHandler.RemoteAvatar();
        
        if (_observedAvatar == null)
        {
            Invoke(nameof(Initialize), 1f);
            return;
        }
        
        if(_verbose)
            ExtendedLogger.LogInfo(GetType().Name, "Initialized!", this);
        
        _initialized = true;

        if (_autoStartReading)
            StartReadingData();
    }

    #endregion

    #region Public Methods

    public bool StartReadingData()
    {
        if (!_initialized)
        {
            ExtendedLogger.LogWarning(GetType().Name, $"Cannot start reading process for user with ID {_userId.Value}. Reader isn't fully initialized yet.", this);
            return false;
        }

        if (!_isReading)
        {
            if(_verbose)
                ExtendedLogger.LogInfo(GetType().Name, $"Starting reading avatar data for user with ID {_userId.Value}...", this);

            _isReading = true;
            _readingCoroutine = StartCoroutine(ReadAvatarData());
        }
        else
        {
            ExtendedLogger.LogWarning(GetType().Name, $"Reader is already reading avatar data for user with ID {_userId.Value}...", this);
        }

        return true;
    }

    public void StopReadingData()
    {
        if (_isReading)
        {
            if(_verbose)
                ExtendedLogger.LogInfo(GetType().Name, "Stopping reading avatar data...");
            
            if(_readingCoroutine != null)
                StopCoroutine(_readingCoroutine);
            _readingCoroutine = null;

            _isReading = false;
        }
    }
    
    public ulong GetUserId() => _userId.Value;

    #endregion

    #region Coroutines

    private IEnumerator ReadAvatarData()
    {
        while (true)
        {
            if (_initialized)
            {
                if (_observedAvatar != null)
                {
                    byte[] data = _observedAvatar.RecordStreamData(_observedAvatar.activeStreamLod);

                    OnAvatarDataRead.Invoke(new AvatarData(_userId.Value, data));

                    if (_verbose)
                        ExtendedLogger.LogInfo(GetType().Name, $"Read avatar data. Data size: {data.Length}, ID: {_userId.Value}, Network ID: {NetworkObject.NetworkObjectId}", this);
                }
                else
                {
                    ExtendedLogger.LogWarning(GetType().Name, $"Observed avatar for user with ID {_userId.Value} is null! Cannot read data!", this);
                }
            }

            yield return new WaitForSeconds(_readingInterval);
        }
    }

    #endregion
}
