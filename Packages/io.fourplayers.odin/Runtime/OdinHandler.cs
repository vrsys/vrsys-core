using OdinNative.Odin;
using OdinNative.Odin.Media;
using OdinNative.Odin.Room;
using OdinNative.Unity;
using OdinNative.Unity.Audio;
using OdinNative.Unity.Events;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Audio;
using static OdinNative.Core.Imports.NativeBindings;

[AddComponentMenu("")]
[RequireComponent(typeof(OdinEditorConfig))]
[DisallowMultipleComponent, DefaultExecutionOrder(-100)]
public class OdinHandler : MonoBehaviour
{
    /// <summary>
    /// True if any <see cref="OdinNative.Odin.Room.Room"/> is joined
    /// </summary>
    public bool HasConnections => Client?.Rooms.Any(r => r.IsJoined) ?? false;

    /// <summary>
    /// Unity Component that handles one Microphone where data gets routed through (n) <see cref="OdinNative.Odin.Media.MediaStream"/>
    /// </summary>
    public MicrophoneReader Microphone;

    public RoomCollection Rooms => Client?.Rooms;
    /// <summary>
    /// Called before an actual room join
    /// </summary>
    public RoomJoinProxy OnRoomJoin;
    /// <summary>
    /// Called after a room is joined successfully
    /// </summary>
    /// <remarks>Invokes only on success</remarks>
    public RoomJoinedProxy OnRoomJoined;
    /// <summary>
    /// Called before a room leave
    /// </summary>
    public RoomLeaveProxy OnRoomLeave;
    /// <summary>
    /// Called after a room is destroyed
    /// </summary>
    public RoomLeftProxy OnRoomLeft;

    /// <summary>
    /// Called on every Peer that joins the room(s) we're connected to
    /// </summary>
    /// <remarks>Self is marked as Peer and this handler will trigger this invoke too</remarks>
    public PeerJoinedProxy OnPeerJoined;
    /// <summary>
    /// Called on every Peer that updates his UserData in the same room(s)
    /// </summary>
    public PeerUserDataChangedProxy OnPeerUserDataChanged;
    /// <summary>
    /// Called on every Peer that left in the same room(s)
    /// </summary>
    public PeerLeftProxy OnPeerLeft;
    /// <summary>
    /// Called on every Peer that created a media in the same room(s)
    /// </summary>
    public MediaAddedProxy OnMediaAdded;
    /// <summary>
    /// Called on every Peer that closed/destroyed one of his own media in the same room(s)
    /// </summary>
    /// <remarks>Invokes before <see cref="OnDeleteMediaObject"/></remarks>
    public MediaRemovedProxy OnMediaRemoved;
    /// <summary>
    /// Called on every activity change of a media in the same room(s)
    /// </summary>
    public MediaActiveStateChangedProxy OnMediaActiveStateChanged;
    /// <summary>
    /// Called on the Room that updates his UserData
    /// </summary>
    public RoomUserDataChangedProxy OnRoomUserDataChanged;
    /// <summary>
    /// Called on every Peer that received message from a peer by <see cref="OdinNative.Odin.Room.Room.SendMessage(ulong[], byte[])"/>
    /// </summary>
    public MessageReceivedProxy OnMessageReceived;
    /// <summary>
    /// Called on the Room that changed the connection state
    /// </summary>
    /// <remarks>If the client loses connection every connected room sends this event. Reconnect can fail and run into a timeout consequently additional events can be delayed.</remarks>
    public ConnectionStateChangedProxy OnConnectionStateChanged;

    /// <summary>
    /// Called if this OdinHandler created a MediaStream that was requested by the MediaQueue
    /// </summary>
    /// <remarks>Invokes after <see cref="OnMediaAdded"/></remarks>
    public UnityCreatedMediaObject OnCreatedMediaObject;
    /// <summary>
    /// Called if this OdinHandler destroyed a MediaStream that was closed by a remote peer and was requested by the MediaQueue
    /// </summary>
    /// <remarks>Invokes after <see cref="OnMediaRemoved"/></remarks>
    public UnityDeleteMediaObject OnDeleteMediaObject;

    internal static readonly ConcurrentQueue<Action> ActionQueue = new ConcurrentQueue<Action>();
    internal ConcurrentQueue<KeyValuePair<object, EventArgs>> EventQueue;

    /// <summary>
    /// Internal wrapper instance for the native ODIN runtime
    /// </summary>
    internal OdinClient Client;
    internal static bool Corrupted;
    private ConcurrentQueue<KeyValuePair<Room, MediaAddedEventArgs>> MediaAddedQueue;
    private ConcurrentQueue<KeyValuePair<Room, MediaRemovedEventArgs>> MediaRemovedQueue;

    private static readonly object Lock = new object();
    private static OdinEditorConfig _config;

    internal string ClientId { get; private set; }
    internal string Identifier { get; private set; }
    /// <summary>
    /// Static reference to the global <see cref="OdinNative.Unity.OdinEditorConfig"/>
    /// </summary>
    /// <remarks>Is a <see cref="UnityEngine.RequireComponent"/> <see href="https://docs.unity3d.com/ScriptReference/RequireComponent.html">(RequireComponent attribute)</see></remarks>
    public static OdinEditorConfig Config
    {
        get
        {
            lock (Lock)
            {
                if (_config != null)
                    return _config;

                var config = FindObjectsOfType<OdinEditorConfig>().FirstOrDefault();
                if (config == null)
                    config = Instance.gameObject.AddComponent<OdinEditorConfig>();

                return _config = config;
            }
        }
    }

    /// <summary>
    /// Singleton reference to this <see cref="OdinHandler"/>
    /// </summary>
    /// <remarks>Provides access to the client with a usual Unity singleton pattern and add a instance if the client is missing in the scene</remarks>
    public static OdinHandler Instance { get; private set; }

    /// <summary>
    /// Identify ODIN client
    /// </summary>
    [Header("OdinClient Settings")]
    [SerializeField]
    private bool _persistent = true;
    /// <summary>
    /// Identify <see cref="UnityEngine.GameObject"/> by Unity-Tag to attach a <see cref="OdinNative.Unity.Audio.PlaybackComponent"/>
    /// </summary>
    /// <remarks>Currently no effect</remarks>
    [Tooltip("Identify GameObject by Unity-Tag to attach a PlaybackComponent")]
    [SerializeField]
    public readonly string UnityAudioSourceTag = "Peer";
    /// <summary>
    /// Enable 3D Audio via preset <see cref="OdinNative.Odin.UserData"/>
    /// </summary>
    /// <remarks>Currently no effect</remarks>
    [SerializeField]
    public bool Use3DAudio = false;
    /// <summary>
    /// Creates <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> on <see cref="Room_OnMediaAdded"/> events
    /// </summary>
    [SerializeField]
    public bool CreatePlayback = false;
    [SerializeField]
    public AudioMixer PlaybackAudioMixer;
    [SerializeField]
    public AudioMixerGroup PlaybackAudioMixerGroup;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;

        if (_persistent)
            DontDestroyOnLoad(gameObject);

        Identifier = SystemInfo.deviceUniqueIdentifier;
        ClientId = string.Join(".", Application.companyName, Application.productName, Identifier);

        SetupEventProxy();
    }

    void Start()
    {
#if UNITY_EDITOR && (PLATFORM_ANDROID || UNITY_ANDROID)
        if(UnityEditor.PlayerSettings.Android.forceInternetPermission == false)
        {
            Debug.LogWarning($"UnityPlayerSettings InternetPermission is \"{(UnityEditor.PlayerSettings.Android.forceInternetPermission ? "Require" : "Auto")}\".");
            if (Config.Verbose) Debug.Log("If the application does not use any Unity network api calls and/or is not set in the AndroidManifest.xml, it's possible that Odin is missing necessary permissions.");
        }
#endif
        var reachability = Application.internetReachability;
        if (Config.VerboseDebug) Debug.Log($"InternetReachability {reachability}");

        if (string.IsNullOrEmpty(Config.ClientId))
            Config.ClientId = ClientId;

        UserData userData = new UserData(Config.UserDataText);

        try
        {
            if (string.IsNullOrEmpty(Config.AccessKey))
            {
                Debug.LogError("Access-Key was not set!");
                Config.AccessKey = OdinClient.CreateAccessKey();
                Debug.LogWarning("Using a generated test key!");
            }
            Client = new OdinClient(new System.Uri(Config.Server), Config.AccessKey, userData);
            if (!OdinNative.Core.OdinLibrary.IsInitialized)
                Client.ReloadLibrary(); // call the load from Unity main thread
        }
        catch (System.DllNotFoundException e)
        {
            Corrupted = true;
            Debug.LogError("Native Plugin libraries for Unity not found!");
            Debug.LogException(e);
            Destroy(this.gameObject);
            return;
        }

        if (Microphone == null && Corrupted == false)
            Microphone = FindObjectOfType<MicrophoneReader>();
        if(!Microphone)
            Debug.LogWarning("MicrophoneReader not found, ODIN will not automatically create MicrophoneMediaStreams.");
    }

    private void SetupEventProxy(bool customProxy = false)
    {
        EventQueue = new ConcurrentQueue<KeyValuePair<object, System.EventArgs>>();
        MediaAddedQueue = new ConcurrentQueue<KeyValuePair<Room, MediaAddedEventArgs>>();
        MediaRemovedQueue = new ConcurrentQueue<KeyValuePair<Room, MediaRemovedEventArgs>>();

        if (OnCreatedMediaObject == null) OnCreatedMediaObject = new UnityCreatedMediaObject();
        if (OnDeleteMediaObject == null) OnDeleteMediaObject = new UnityDeleteMediaObject();

        //Room
        if (OnRoomJoin == null) OnRoomJoin = new RoomJoinProxy();
        if (OnRoomJoined == null) OnRoomJoined = new RoomJoinedProxy();
        if (OnRoomLeave == null) OnRoomLeave = new RoomLeaveProxy();
        if (OnRoomLeft == null) OnRoomLeft = new RoomLeftProxy();
        //sub Room
        if (OnPeerJoined == null) OnPeerJoined = new PeerJoinedProxy();
        if (customProxy) OnPeerJoined.AddListener(new UnityEngine.Events.UnityAction<object, PeerJoinedEventArgs>(Room_OnPeerJoined));
        if (OnPeerUserDataChanged == null) OnPeerUserDataChanged = new PeerUserDataChangedProxy();
        if (customProxy) OnPeerUserDataChanged.AddListener(new UnityEngine.Events.UnityAction<object, PeerUserDataChangedEventArgs>(Room_OnPeerUserDataChanged));
        if (OnPeerLeft == null) OnPeerLeft = new PeerLeftProxy();
        if (customProxy) OnPeerLeft.AddListener(new UnityEngine.Events.UnityAction<object, PeerLeftEventArgs>(Room_OnPeerLeft));
        if (OnMediaAdded == null) OnMediaAdded = new MediaAddedProxy();
        if (customProxy) OnMediaAdded.AddListener(new UnityEngine.Events.UnityAction<object, MediaAddedEventArgs>(Room_OnMediaAdded));
        if (OnMediaRemoved == null) OnMediaRemoved = new MediaRemovedProxy();
        if (customProxy) OnMediaRemoved.AddListener(new UnityEngine.Events.UnityAction<object, MediaRemovedEventArgs>(Room_OnMediaRemoved));
        if (OnMediaActiveStateChanged == null) OnMediaActiveStateChanged = new MediaActiveStateChangedProxy();
        if (customProxy) OnMediaActiveStateChanged.AddListener(new UnityEngine.Events.UnityAction<object, MediaActiveStateChangedEventArgs>(Room_OnMediaActiveStateChanged));
        if (OnRoomUserDataChanged == null) OnRoomUserDataChanged = new RoomUserDataChangedProxy();
        if (customProxy) OnRoomUserDataChanged.AddListener(new UnityEngine.Events.UnityAction<object, RoomUserDataChangedEventArgs>(Room_OnRoomUserDataChanged));
        if (OnMessageReceived == null) OnMessageReceived = new MessageReceivedProxy();
        if (customProxy) OnMessageReceived.AddListener(new UnityEngine.Events.UnityAction<object, MessageReceivedEventArgs>(Room_OnMessageReceived));
    }

    public IUserData GetUserData()
    {
        return Client.UserData;
    }

    /// <summary>
    /// Join or create a room by name and attach a <see cref="OdinNative.Odin.Media.MicrophoneStream"/>
    /// </summary>
    /// <remarks>Configure Room-Apm i.e VadEnable, ... or Odin-Event-Listeners i.e PeerJoinedEvent, ... with <see cref="Config"/></remarks>
    /// <param name="roomName">Room name</param>
    /// <param name="userData">Override OdinClient default UserData</param>
    /// <param name="setup">Override default Room setup</param>
    public async void JoinRoom(string roomName, IUserData userData = null, System.Action<Room> setup = null)
    {
        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogError("Room name can not be empty!");
            return;
        }

        if (Client.Rooms[roomName] != null)
        {
            Debug.LogError($"Room {roomName} already joined!");
            return;
        }

        if (userData == null)
            userData = new UserData(Config.UserDataText);

        setup = CheckSetup(setup);

        Room room = await Client.JoinRoom(roomName, Config.ClientId, userData, setup);

        if (room == null || room.IsJoined == false)
        {
            Debug.LogError($"Odin {Config.ClientId}: Room {roomName} join failed!");
            return;
        }
        Debug.Log($"Odin {Config.ClientId}: Room {room.Config.Name} joined.");

        if (Microphone && room.CreateMicrophoneMedia(new OdinNative.Core.OdinMediaConfig(Microphone.SampleRate, Config.DeviceChannels)))
            Debug.Log($"MicrophoneStream added to room {roomName}.");

        await System.Threading.Tasks.Task.Yield();

        if (Config.VerboseDebug) Debug.Log($"## {nameof(JoinRoom)} Enqueue => {nameof(roomName)} \"{roomName}\"\n\t# {room}");
        EventQueue.Enqueue(new KeyValuePair<object, System.EventArgs>(
            this,
            new RoomJoinedEventArgs() { Room = room }));
    }

    /// <summary>
    /// Join or create a room by name and attach a <see cref="OdinNative.Odin.Media.MicrophoneStream"/>
    /// </summary>
    /// <remarks>Configure Room-Apm i.e VadEnable, ... or Odin-Event-Listeners i.e PeerJoinedEvent, ... with <see cref="Config"/></remarks>
    /// <param name="roomAlias">Room name</param>
    /// <param name="userData">Override OdinClient default UserData</param>
    /// <param name="setup">Override default Room setup</param>
    public async void JoinRoom(string roomAlias, string token, UserData userData = null, System.Action<Room> setup = null)
    {
        if (string.IsNullOrEmpty(roomAlias))
        {
            Debug.LogError("Room name can not be empty!");
            return;
        }

        if (Client.Rooms[roomAlias] != null)
        {
            Debug.LogError($"Room {roomAlias} already joined!");
            return;
        }

        if (userData == null)
            userData = new UserData(Config.UserDataText);

        setup = CheckSetup(setup);

        Room room = await Client.JoinNamedRoom(roomAlias, token, userData, setup);

        if (room == null || room.IsJoined == false)
        {
            Debug.LogError($"Odin {Config.ClientId}: Room {roomAlias} join failed!");
            return;
        }
        Debug.Log($"Odin {Config.ClientId}: Room {room.Config.Name} joined.");

        if (Microphone && room.CreateMicrophoneMedia(new OdinNative.Core.OdinMediaConfig(Microphone.SampleRate, Config.DeviceChannels)))
            Debug.Log($"MicrophoneStream added to room {roomAlias}.");

        await System.Threading.Tasks.Task.Yield();

        if (Config.VerboseDebug) Debug.Log($"## {nameof(JoinRoom)} Enqueue => {nameof(roomAlias)} \"{roomAlias}\" {nameof(token)} \"{token}\"\n\t# {room}");
        EventQueue.Enqueue(new KeyValuePair<object, System.EventArgs>(
            this,
            new RoomJoinedEventArgs() { Room = room }));
    }

    private Action<Room> CheckSetup(Action<Room> setup)
    {
        if (setup == null)
            setup = (r) =>
            {
                r.OnConnectionStateChanged += Room_OnConnectionStateChanged;
                var cfg = Config;
                if (cfg.PeerJoinedEvent) r.OnPeerJoined += Room_OnPeerJoined;
                if (cfg.PeerLeftEvent) r.OnPeerLeft += Room_OnPeerLeft;
                if (cfg.PeerUpdatedEvent) r.OnPeerUserDataChanged += Room_OnPeerUserDataChanged;
                if (cfg.MediaAddedEvent) r.OnMediaAdded += Room_OnMediaAdded;
                if (cfg.MediaRemovedEvent) r.OnMediaRemoved += Room_OnMediaRemoved;
                if (cfg.RoomUpdatedEvent) r.OnRoomUserDataChanged += Room_OnRoomUserDataChanged;
                if (cfg.MediaActiveStateChangedEvent) r.OnMediaActiveStateChanged += Room_OnMediaActiveStateChanged;
                if (cfg.MessageReceivedEvent) r.OnMessageReceived += Room_OnMessageReceived;

                r.SetApmConfig(new OdinNative.Core.OdinRoomConfig(cfg));

                EventQueue.Enqueue(new KeyValuePair<object, System.EventArgs>(
                    this,
                    new RoomJoinEventArgs() { Room = r }));
            };
        return setup;
    }

    /// <summary>
    /// Leave and free the <see cref="OdinNative.Odin.Room.Room"/> by name
    /// </summary>
    /// <param name="roomName">Room name</param>
    public async void LeaveRoom(string roomName)
    {
        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogError("Room name can not be empty!");
            return;
        }

        if (Config.VerboseDebug) Debug.Log($"## {nameof(LeaveRoom)} Enqueue => {nameof(roomName)} \"{roomName}\"\n\t# {Rooms[roomName]}");
        EventQueue.Enqueue(new KeyValuePair<object, System.EventArgs>(
            this,
            new RoomLeaveEventArgs() { Room = Rooms[roomName] }));

        if (CreatePlayback && Use3DAudio == false)
        {
            var playbacks = FindObjectsOfType<PlaybackComponent>()
                .Where(p => p.RoomName == roomName);

            foreach (PlaybackComponent playback in playbacks)
                DestroyImmediate(playback);
        }

        if (await Client.LeaveRoom(roomName) == false)
            Debug.LogWarning($"Room {roomName} not found!");
        else
        {
            EventQueue.Enqueue(new KeyValuePair<object, System.EventArgs>(
                this,
                new RoomLeftEventArgs() { RoomName = roomName }));
        }
    }

    /// <summary>
    /// Peer joined the room
    /// </summary>
    /// <param name="sender"><see cref="OdinNative.Odin.Room.Room"/> object</param>
    /// <param name="e">PeerJoined Args</param>
    protected virtual void Room_OnPeerJoined(object sender, PeerJoinedEventArgs e)
    {
        if (Config.Verbose) Debug.Log($"Odin Room \"{(sender as Room).Config.Name}\": User added {e.Peer} with {e.Peer.UserData}");
        if (Config.VerboseDebug) Debug.Log($"## {nameof(Room_OnPeerJoined)}\n\t# {(sender as Room)}\n\t# {e.Peer}");

        // Push for unity thread
        EventQueue.Enqueue(new KeyValuePair<object, System.EventArgs>(sender, e));
    }

    /// <summary>
    /// Peer left the room
    /// </summary>
    /// <param name="sender"><see cref="OdinNative.Odin.Room.Room"/> object</param>
    /// <param name="e">PeerLeft Args</param>
    protected virtual void Room_OnPeerLeft(object sender, PeerLeftEventArgs e)
    {
        if (Config.Verbose) Debug.Log($"Odin Room \"{(sender as Room).Config.Name}\": User left {e.PeerId}");
        if (Config.VerboseDebug) Debug.Log($"## {nameof(Room_OnPeerLeft)}\n\t# {(sender as Room)}");

        // Push for unity thread
        EventQueue.Enqueue(new KeyValuePair<object, System.EventArgs>(sender, e));
    }

    /// <summary>
    /// User data of a peer was updated
    /// </summary>
    /// <param name="sender"><see cref="OdinNative.Odin.Room.Room"/> object</param>
    /// <param name="e">PeerUpdated Args</param>
    protected virtual void Room_OnPeerUserDataChanged(object sender, PeerUserDataChangedEventArgs e)
    {
        if (Config.Verbose) Debug.Log($"Odin Room \"{(sender as Room).Config.Name}\": User {e.PeerId} updated {e.UserData}");
        if (Config.VerboseDebug) Debug.Log($"## {nameof(Room_OnPeerUserDataChanged)}\n\t# {(sender as Room)}\n\t# {e.Peer}\n\t# {e.UserData}");

        // Push for unity thread
        EventQueue.Enqueue(new KeyValuePair<object, System.EventArgs>(sender, e));
    }

    /// <summary>
    /// A new media (audio/video stream) was added to the room by a peer
    /// </summary>
    /// <remarks>The remote <see cref="OdinNative.Odin.Media.MediaStream"/> is always a <see cref="OdinNative.Odin.Media.PlaybackStream"/> and readonly.</remarks>
    /// <param name="sender"><see cref="OdinNative.Odin.Room.Room"/> object</param>
    /// <param name="e">MediaAdded Args</param>
    protected virtual void Room_OnMediaAdded(object sender, MediaAddedEventArgs e)
    {
        if (Config.Verbose) Debug.Log($"Odin Room \"{(sender as Room).Config.Name}\": add Media: {e.Media} PlaybackId: {e.Media.Id} to Peer: {e.Peer}");
        if (Config.VerboseDebug) Debug.Log($"## {nameof(Room_OnMediaAdded)}\n\t# {(sender as Room)}\n\t# {e.Peer}\n\t# {e.Media}");

        // Push for unity thread
        MediaAddedQueue.Enqueue(new KeyValuePair<Room, MediaAddedEventArgs>(sender as Room, e));
        EventQueue.Enqueue(new KeyValuePair<object, System.EventArgs>(sender, e));
    }

    /// <summary>
    /// A media (audio/video stream) was removed from the room by a peer
    /// </summary>
    /// <remarks>Peer and Media in <see cref="OdinNative.Odin.Room.MediaRemovedEventArgs"/> is null if the peer left before the owned Medias are removed</remarks>
    /// <param name="sender"><see cref="OdinNative.Odin.Room.Room"/> object</param>
    /// <param name="e">MediaRemoved Args with MediaId</param>
    protected virtual void Room_OnMediaRemoved(object sender, MediaRemovedEventArgs e)
    {
        if (Config.Verbose) Debug.Log($"Odin Room \"{(sender as Room).Config.Name}\": removed Media: {e.MediaStreamId} from Peer: {e.Peer}");
        if (Config.VerboseDebug) Debug.Log($"## {nameof(Room_OnMediaRemoved)}\n\t# {(sender as Room)}\n\t# {e.MediaStreamId}\n\t# {e.Peer}");


        // Push for unity thread
        MediaRemovedQueue.Enqueue(new KeyValuePair<Room, MediaRemovedEventArgs>(sender as Room, e));
        EventQueue.Enqueue(new KeyValuePair<object, System.EventArgs>(sender, e));
    }

    protected virtual void Room_OnRoomUserDataChanged(object sender, RoomUserDataChangedEventArgs e)
    {
        if (Config.Verbose) Debug.Log($"Odin Room \"{(sender as Room).Config.Name}\": changed {e.RoomName} data: {e.Data}");
        if (Config.VerboseDebug) Debug.Log($"## {nameof(Room_OnRoomUserDataChanged)}\n\t# {(sender as Room)}\n\t# {e.RoomName}\n\t# {e.Data}");

        // Push for unity thread
        EventQueue.Enqueue(new KeyValuePair<object, System.EventArgs>(sender, e));
    }

    protected virtual void Room_OnMediaActiveStateChanged(object sender, MediaActiveStateChangedEventArgs e)
    {
        if (Config.Verbose) Debug.Log($"Odin Room \"{(sender as Room).Config.Name}\": Media activity on {e.MediaStreamId} from Peer: {e.PeerId}");
        if (Config.VerboseDebug) Debug.Log($"## {nameof(Room_OnMediaActiveStateChanged)}\n\t# {(sender as Room)}\n\t# {e.MediaStreamId}\n\t# {e.PeerId}");

        // Push for unity thread
        EventQueue.Enqueue(new KeyValuePair<object, System.EventArgs>(sender, e));
    }

    /// <summary>
    /// Arbitrary data was sent by a peer
    /// </summary>
    /// <param name="sender"><see cref="OdinNative.Odin.Room.Room"/> object</param>
    /// <param name="e">MessageReceivedEventArgs with peer ID and data</param>
    protected virtual void Room_OnMessageReceived(object sender, MessageReceivedEventArgs e)
    {
        if (Config.Verbose) Debug.Log($"Odin Room \"{(sender as Room).Config.Name}\": received message from Peer: {e.PeerId}");
        if (Config.VerboseDebug) Debug.Log($"## {nameof(Room_OnMessageReceived)}\n\t# {(sender as Room)}\n\t# {e.PeerId}\n\t# {e.Data}");

        // Push for unity thread
        EventQueue.Enqueue(new KeyValuePair<object, System.EventArgs>(sender, e));
    }

    private void Room_OnConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
    {
        Room room = sender as Room;
        if (room.IsJoined && e.ConnectionState.HasFlag(OdinRoomConnectionState.Connecting))
            Debug.LogWarning($"Room \"{room.Config.Name}\" lost connection: {e.ChangeReason} retry: {e.Retry}");

        // Push for unity thread
        EventQueue.Enqueue(new KeyValuePair<object, System.EventArgs>(sender, e));
    }

    void FixedUpdate()
    {
        if (Corrupted) return;

        HandleNewMediaQueue();
        HandleRemoveMediaQueue();
        HandleEventQueue();

        foreach (Room room in Rooms)
        {
            if (room.IsJoined && room.Config.ApmConfig.EchoCanceller)
                RouteAudioProcess(room);
        }
    }

    void Update()
    {
        if (Corrupted) return;

        HandleActionQueue();
    }

    private bool RouteAudioProcess(Room room)
    {
        int readBufferSize = Mathf.FloorToInt(Time.fixedUnscaledDeltaTime * AudioSettings.outputSampleRate);
        if (Config.VerboseDebug)
            Debug.Log($"EC for {room.Config.Name} audio process size {readBufferSize}");
        float[] buffer = new float[readBufferSize];
        AudioListener.GetOutputData(buffer, 0); // only mono block
        return room.AudioProcessReverse(buffer);
    }

    private IEnumerator HandleActionQueue()
    {
        while (ActionQueue.TryDequeue(out Action uAction))
        {
            uAction.Invoke();
            yield return null;
        }
    }

    private void HandleEventQueue()
    {
        if (EventQueue == null) return;
        while (EventQueue.TryDequeue(out KeyValuePair<object, System.EventArgs> uEvent))
        {
            if (Config.VerboseDebug) Debug.Log($"## {nameof(HandleEventQueue)} TryDequeue <= {uEvent.Value.GetType()}\n\t# {uEvent.Key}");

            if (uEvent.Value is ConnectionStateChangedEventArgs)
                OnConnectionStateChanged?.Invoke(uEvent.Key, uEvent.Value as ConnectionStateChangedEventArgs);
            //Room
            else if (uEvent.Value is RoomJoinEventArgs)
                OnRoomJoin?.Invoke(uEvent.Value as RoomJoinEventArgs);
            else if (uEvent.Value is RoomJoinedEventArgs)
                OnRoomJoined?.Invoke(uEvent.Value as RoomJoinedEventArgs);
            else if (uEvent.Value is RoomLeaveEventArgs)
                OnRoomLeave?.Invoke(uEvent.Value as RoomLeaveEventArgs);
            else if (uEvent.Value is RoomLeftEventArgs)
                OnRoomLeft?.Invoke(uEvent.Value as RoomLeftEventArgs);
            //SubRoom
            else if (uEvent.Value is PeerJoinedEventArgs)
                OnPeerJoined?.Invoke(uEvent.Key, uEvent.Value as PeerJoinedEventArgs);
            else if (uEvent.Value is PeerUserDataChangedEventArgs)
                OnPeerUserDataChanged?.Invoke(uEvent.Key, uEvent.Value as PeerUserDataChangedEventArgs);
            else if (uEvent.Value is PeerLeftEventArgs)
                OnPeerLeft?.Invoke(uEvent.Key, uEvent.Value as PeerLeftEventArgs);
            else if (uEvent.Value is MediaAddedEventArgs)
                OnMediaAdded?.Invoke(uEvent.Key, uEvent.Value as MediaAddedEventArgs);
            else if (uEvent.Value is MediaRemovedEventArgs)
                OnMediaRemoved?.Invoke(uEvent.Key, uEvent.Value as MediaRemovedEventArgs);
            else if (uEvent.Value is RoomUserDataChangedEventArgs)
                OnRoomUserDataChanged?.Invoke(uEvent.Key, uEvent.Value as RoomUserDataChangedEventArgs);
            else if (uEvent.Value is MediaActiveStateChangedEventArgs)
                OnMediaActiveStateChanged?.Invoke(uEvent.Key, uEvent.Value as MediaActiveStateChangedEventArgs);
            else if (uEvent.Value is MessageReceivedEventArgs)
                OnMessageReceived?.Invoke(uEvent.Key, uEvent.Value as MessageReceivedEventArgs);
            else
                Debug.LogError($"Call to invoke unknown event skipped: {uEvent.Value.GetType()} from {nameof(uEvent.Key)} ({uEvent.Key.GetType()})");
        }
    }

    private void HandleNewMediaQueue()
    {
        if (MediaAddedQueue == null) return;
        if (MediaAddedQueue.TryDequeue(out KeyValuePair<Room, MediaAddedEventArgs> addedEvent))
        {
            if (Config.VerboseDebug) Debug.Log($"## {nameof(HandleNewMediaQueue)} TryDequeue <= {addedEvent.Value.GetType()}\n\t# {addedEvent.Key}");

            if (CreatePlayback)
            {
                if (Use3DAudio)
                {
                    Debug.LogWarning("Create Playback and 3D Audio enabled at the same time has currently limit support and uses FindGameObjectsWithTag with UnityAudioSourceTag tagged gameobjects.");
                    AddPlaybackComponent(UnityAudioSourceTag, addedEvent);
                }
                else if (addedEvent.Key.OwnId != addedEvent.Value.PeerId)
                {
                    GameObject pObject = new GameObject($"{addedEvent.Key.RoomId} {addedEvent.Value.PeerId}");
                    pObject.transform.parent = this.gameObject.transform;
                    AddPlaybackComponent(pObject, addedEvent);
                }
            }
            else if (Config.Verbose)
                Debug.LogWarning($"No available consumers for playback found.");

            OnCreatedMediaObject?.Invoke(addedEvent.Key.Config.Name, addedEvent.Value.Peer.Id, addedEvent.Value.Media.Id);
        }
    }

    private void HandleRemoveMediaQueue()
    {
        if (MediaRemovedQueue == null) return;
        if (MediaRemovedQueue.TryDequeue(out KeyValuePair<Room, MediaRemovedEventArgs> removedEvent))
        {
            if (Config.VerboseDebug) Debug.Log($"## {nameof(HandleRemoveMediaQueue)} TryDequeue <= {removedEvent.Value.GetType()}\n\t# {removedEvent.Key}");

            OnDeleteMediaObject?.Invoke(removedEvent.Value.MediaStreamId);

            if (CreatePlayback && Use3DAudio == false)
            {
                var playbacks = gameObject.GetComponentsInChildren<PlaybackComponent>();
                var playback = playbacks.FirstOrDefault(p => p.MediaStreamId == removedEvent.Value.MediaStreamId);

                if (playback == null)
                    Debug.LogWarning($"No Playback for stream id {removedEvent.Value.MediaStreamId} found to destroy!");
                else if (removedEvent.Value == null)
                    Debug.LogWarning($"No Media for stream id {removedEvent.Value.MediaStreamId} found!");
                else
                    Destroy(playback.gameObject);
            }
        }
    }

    private PlaybackComponent AddPlaybackComponent(string gameObjectTag, KeyValuePair<Room, MediaAddedEventArgs> addedEvent)
    {
        return AddPlaybackComponent(FindPeerContainer(gameObjectTag),
            addedEvent.Key.Config.Name,
            addedEvent.Value.Peer.Id,
            addedEvent.Value.Media.Id);
    }

    private PlaybackComponent AddPlaybackComponent(GameObject peerContainer, KeyValuePair<Room, MediaAddedEventArgs> addedEvent)
    {
        return AddPlaybackComponent(peerContainer,
            addedEvent.Key.Config.Name,
            addedEvent.Value.Peer.Id,
            addedEvent.Value.Media.Id);
    }

    /// <summary>
    /// Tries to identifiy a gameobject by tag to assign the PlaybackComponent
    /// </summary>
    /// <param name="gameObjectTag">Tag string to find with <see href="https://docs.unity3d.com/ScriptReference/GameObject.FindGameObjectsWithTag.html">FindGameObjectsWithTag</see></param>
    /// <param name="roomName">PlaybackComponent room</param>
    /// <param name="peerId">PlaybackComponent peer</param>
    /// <param name="mediaStreamId">PlaybackComponent media</param>
    /// <param name="autoDestroySource">optionally enable or disable on destroy of PlaybackComponent the destroy of the linked AudioSource</param>
    /// <returns>ScriptReference of <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> from the GameObject or null</returns>
    public PlaybackComponent AddPlaybackComponent(string gameObjectTag, string roomName, ulong peerId, long mediaStreamId, bool autoDestroySource = true)
    {
        return AddPlaybackComponent(FindPeerContainer(gameObjectTag),
            roomName,
            peerId,
            mediaStreamId,
            autoDestroySource);
    }

    private GameObject FindPeerContainer(string gameObjectTag)
    {
        GameObject[] gameObjects = GameObject.FindGameObjectsWithTag(gameObjectTag);
        if (gameObjects.Length == 0)
            Debug.LogWarning($"No game objects are tagged with '{gameObjectTag}'");
        var available = gameObjects.Where(g => g.GetComponent<PlaybackComponent>() == null);
        if (available.Count() == 0)
            Debug.LogWarning($"No game objects are free with '{gameObjectTag}' tag ({available.Count()}/{gameObjects.Length})");

        return available.FirstOrDefault();
    }

    /// <summary>
    /// Adds a new PlaybackComponent to the specified GameObject
    /// </summary>
    /// <param name="peerContainer">GameObject to attach to</param>
    /// <param name="roomName">PlaybackComponent room</param>
    /// <param name="peerId">PlaybackComponent peer</param>
    /// <param name="mediaStreamId">PlaybackComponent media</param>
    /// <param name="autoDestroySource">optionally enable or disable on destroy of PlaybackComponent the destroy of the linked AudioSource</param>
    /// <returns>ScriptReference of <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> from the GameObject or null</returns>
    public PlaybackComponent AddPlaybackComponent(GameObject peerContainer, string roomName, ulong peerId, long mediaStreamId, bool autoDestroySource = true)
    {
        if (peerContainer == null)
        {
            Debug.LogError("Can not add PlaybackComponent to null");
            return null;
        }

        var playback = peerContainer.AddComponent<PlaybackComponent>();
        playback.AutoDestroyAudioSource = autoDestroySource; // We create and destroy the audiosource
        playback.RoomName = roomName;
        playback.PeerId = peerId;
        playback.MediaStreamId = mediaStreamId;
        if (PlaybackAudioMixerGroup != null)
            playback.PlaybackSource.outputAudioMixerGroup = PlaybackAudioMixerGroup;
        Debug.Log($"Playback created on {peerContainer.name} for Room {playback.RoomName} Peer {playback.PeerId} Media {playback.MediaStreamId}");
        if (Config.VerboseDebug)
            Debug.Log($"## {nameof(AddPlaybackComponent)}:" +
                $" {nameof(peerContainer)} \"{peerContainer}\"" +
                $", {nameof(roomName)} \"{roomName}\"" +
                $", {nameof(peerId)} {peerId}" +
                $", {nameof(mediaStreamId)} {mediaStreamId}" +
                $", {nameof(autoDestroySource)} {autoDestroySource}\n\t" +
                $"- {nameof(playback.OdinMedia)} {playback.OdinMedia}");
        return playback;
    }

    /// <summary>
    /// Returns the attached <see cref="OdinNative.Odin.Media.MicrophoneStream"/> used by <see cref="OdinNative.Unity.Audio.MicrophoneReader"/>
    /// </summary>
    /// <param name="roomName">Room name</param>
    /// <param name="config"><see cref="OdinNative.Odin.Media.MicrophoneStream"/> config</param>
    /// <returns><see cref="OdinNative.Odin.Media.MicrophoneStream"/> or null if there is no room</returns>
    public MicrophoneStream GetOrCreateMicrophoneStream(string roomName, OdinNative.Core.OdinMediaConfig config = null)
    {
        if (string.IsNullOrEmpty(roomName)) return null;
        var room = Client.Rooms[roomName];
        if (room == null) return null;

        if (room.MicrophoneMedia == null)
            room.CreateMicrophoneMedia(config ?? new OdinNative.Core.OdinMediaConfig(Config.DeviceSampleRate, Config.DeviceChannels));

        return room.MicrophoneMedia;
    }

    #region convenience
    /// <summary>
    /// Configures the allowed 'view' distance for proximity calculation of peers in each room
    /// </summary>
    /// <remarks>Make sure that all of your ODIN clients configure the same `distance` value.</remarks>
    /// <param name="scale">Per default, the room will use a distance of `1.0` fo proximity calculation</param>
    public void RoomsSetPositionScale(float scale)
    {
        foreach (var room in Rooms)
            if (room.IsJoined)
                room.SetPositionScale(scale);
    }

    /// <summary>
    /// Updates the two-dimensional position of our own peer in each room
    /// </summary>
    /// <remarks>This should _only_ be used after configuring the room with <see cref="OdinNative.Core.Imports.NativeMethods.RoomSetPositionScale"/>.</remarks>
    /// <param name="x">x postition</param>
    /// <param name="y">y postition</param>
    public void RoomsUpdatePosition(float x, float y)
    {
        foreach (var room in Rooms)
            if (room.IsJoined)
                room.UpdatePosition(x, y);
    }

    /// <summary>
    /// Updates the <see cref="OdinNative.Odin.UserData"/> for all <see cref="Rooms"/>
    /// </summary>
    /// <param name="userData"><see cref="OdinNative.Odin.IUserData"/></param>
    public void UpdateUserData(IUserData userData)
    {
        Client.UpdateUserData(userData);
    }

    /// <summary>
    /// Gets the room object from <see cref="OdinNative.Odin.OdinClient"/>
    /// </summary>
    /// <param name="id">Room identifier e.g name or token</param>
    /// <returns>Room or null</returns>
    public Room GetRoom(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogError("id can not be empty!");
            return null;
        }
        var room = Client.Rooms[id];
        if (room == null)
        {
            Debug.LogWarning($"Room \"{id}\" not found!");
            return null;
        }
        return room;
    }

    /// <summary>
    /// Gets the a Peer object from a specified room matching a peer ID
    /// </summary>
    /// <remarks>Local seen Peers only, not </remarks>
    /// <param name="roomId">Room identifier e.g name or token</param>
    /// <param name="peerId">peer ID</param>
    /// <returns>Peer or null</returns>
    public OdinNative.Odin.Peer.Peer GetPeer(string roomId, ulong peerId)
    {
        Room room = GetRoom(roomId);
        if (room == null) return null;

        return room.RemotePeers.FirstOrDefault(p => p.Id == peerId);
    }

    /// <summary>
    /// Get the PlaybackStream of a peer in the room
    /// </summary>
    /// <param name="roomId">Room identifier e.g name or token</param>
    /// <param name="mediaStreamId">media handle ID</param>
    /// <returns>PlaybackStream or null</returns>
    public PlaybackStream GetMedia(string roomId, long mediaStreamId)
    {
        Room room = GetRoom(roomId);
        if (room == null) return null;

        return room.PlaybackMedias
            .SelectMany(c => c)
            .FirstOrDefault(m => m.Id == mediaStreamId)
            as PlaybackStream;
    }

    /// <summary>
    /// Get all remote peers inside a room
    /// </summary>
    /// <param name="roomId">Room identifier e.g name or token</param>
    /// <param name="includeSelf">optionally include Self in peers result</param>
    /// <returns>IEnumerable of RemotePeers</returns>
    public IEnumerable<OdinNative.Odin.Peer.Peer> GetPeers(string roomId, bool includeSelf = false)
    {
        Room room = GetRoom(roomId);
        if (room == null) return default;

        if (includeSelf)
            return room.RemotePeers
                .AsEnumerable();

        return room.RemotePeers
            .Where(p => p.Id != room.OwnId);
    }

    /// <summary>
    /// Sends arbitrary data to a all remote peers in all rooms
    /// </summary>
    /// <param name="data">arbitrary byte array</param>
    public void BroadcastMessage(byte[] data)
    {
        foreach (Room room in Rooms)
            room.BroadcastMessage(data);
    }

    /// <summary>
    /// Gets all <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> across all rooms
    /// </summary>
    /// <remarks>Simply uses <see href="https://docs.unity3d.com/ScriptReference/Object.FindObjectsOfType.html">FindObjectsOfType PlaybackComponent</see>
    /// A PlaybackComponent always have RoomName, PeerId and MediaId properties.</remarks>
    /// <returns>The array of objects found matching the type PlaybackComponent.</returns>
    public PlaybackComponent[] GetPlaybackComponents()
    {
        return FindObjectsOfType<PlaybackComponent>();
    }

    /// <summary>
    /// Gets all <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> filtered by room
    /// </summary>
    /// <remarks>A PlaybackComponent always have RoomName, PeerId and MediaId properties.</remarks>
    /// <param name="roomId">Room identifier e.g name or token</param>
    /// <returns>The filtered array of objects found matching the type PlaybackComponent.</returns>
    public PlaybackComponent[] GetPlaybackComponents(string roomId)
    {
        return GetPlaybackComponents()
            .Where(c => c.RoomName == roomId)
            .ToArray();
    }

    /// <summary>
    /// Gets all <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> across rooms filtered by peer
    /// </summary>
    /// <remarks>A PlaybackComponent always have RoomName, PeerId and MediaId properties.</remarks>
    /// <param name="peerId">peer ID</param>
    /// <returns>The filtered array of objects found matching the type PlaybackComponent.</returns>
    public PlaybackComponent[] GetPlaybackComponents(ulong peerId)
    {
        return GetPlaybackComponents()
            .Where(c => c.PeerId == peerId)
            .ToArray();
    }

    /// <summary>
    /// Gets all <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> across rooms filtered by media
    /// </summary>
    /// <remarks>A PlaybackComponent always have RoomName, PeerId and MediaId properties.</remarks>
    /// <param name="mediaStreamId">media handle ID</param>
    /// <returns>The filtered array of objects found matching the type PlaybackComponent.</returns>
    public PlaybackComponent[] GetPlaybackComponents(long mediaStreamId)
    {
        return GetPlaybackComponents()
            .Where(c => c.MediaStreamId == mediaStreamId)
            .ToArray();
    }

    /// <summary>
    /// Gets a <see cref="OdinNative.Unity.Audio.PlaybackComponent"/>
    /// </summary>
    /// <remarks>A PlaybackComponent always have RoomName, PeerId and MediaId properties.</remarks>
    /// <param name="roomId">Room identifier e.g name or token</param>
    /// <param name="peerId">peer ID</param>
    /// <param name="mediaStreamId">media handle ID</param>
    /// <returns>PlaybackComponent or null</returns>
    public PlaybackComponent GetPlaybackComponent(string roomId, ulong peerId, long mediaStreamId)
    {
        return GetPlaybackComponents()
            .FirstOrDefault(c => c.RoomName == roomId && c.PeerId == peerId && c.MediaStreamId == mediaStreamId);
    }

    /// <summary>
    /// Destroys all <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> instances
    /// </summary>
    /// <remarks>This will free all medias with a PlaybackComponent and
    /// removes the associated <see href="https://docs.unity3d.com/ScriptReference/AudioSource.html">AudioSource</see>,
    /// If <see cref="OdinNative.Unity.Audio.PlaybackComponent.AutoDestroyAudioSource"/> in <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> is set!</remarks>
    public void DestroyPlaybackComponents()
    {
        foreach (PlaybackComponent component in GetPlaybackComponents())
            Destroy(component);
    }

    /// <summary>
    /// Destroys all <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> filtered by room
    /// </summary>
    /// <remarks>This will free all medias with a PlaybackComponent by room and
    /// removes the associated <see href="https://docs.unity3d.com/ScriptReference/AudioSource.html">AudioSource</see>,
    /// If <see cref="OdinNative.Unity.Audio.PlaybackComponent.AutoDestroyAudioSource"/> in <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> is set!</remarks>
    /// <param name="roomId">Room identifier e.g name or token</param>
    public void DestroyPlaybackComponents(string roomId)
    {
        foreach (PlaybackComponent component in GetPlaybackComponents(roomId))
            Destroy(component);
    }

    /// <summary>
    /// Destroys all <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> filtered by peer
    /// </summary>
    /// <remarks>This will free all medias with a PlaybackComponent from a peer and 
    /// removes the associated <see href="https://docs.unity3d.com/ScriptReference/AudioSource.html">AudioSource</see>,
    /// If <see cref="OdinNative.Unity.Audio.PlaybackComponent.AutoDestroyAudioSource"/> in <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> is set!</remarks>
    /// <param name="peerId">peer ID</param>
    public void DestroyPlaybackComponents(ulong peerId)
    {
        foreach (PlaybackComponent component in GetPlaybackComponents(peerId))
            Destroy(component);
    }

    /// <summary>
    /// Destroys all <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> filtered by media
    /// </summary>
    /// <remarks>This will free the media with a PlaybackComponent and
    /// removes the associated <see href="https://docs.unity3d.com/ScriptReference/AudioSource.html">AudioSource</see>,
    /// If <see cref="OdinNative.Unity.Audio.PlaybackComponent.AutoDestroyAudioSource"/> in <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> is set!</remarks>
    /// <param name="mediaStreamId">media handle ID</param>
    public void DestroyPlaybackComponents(long mediaStreamId)
    {
        foreach (PlaybackComponent component in GetPlaybackComponents(mediaStreamId))
            Destroy(component);
    }
    #endregion convenience

    void OnEnable()
    {
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
#endif
    }

    private void OnBeforeAssemblyReload()
    {
        if (!HasConnections) return;

        if(Microphone)
            Destroy(Microphone);
        DestroyPlaybackComponents();
        Rooms.Clear();
        Debug.LogException(new NotSupportedException("ODIN SDK ReloadEvent is not supported!"));
    }

    void OnDestroy()
    {
        if (Corrupted) return;

        Client?.Dispose();
        Client = null;
    }
}
