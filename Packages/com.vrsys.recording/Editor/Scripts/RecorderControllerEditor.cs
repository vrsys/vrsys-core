using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace VRSYS.Recording
{
#if UNITY_EDITOR
    /// <summary>
    /// UI Toolkit inspector for <see cref="RecorderController"/>, laid out like the COZY weather inspector:
    /// a status header, state-dependent record/replay buttons, a search bar that finds any setting by name,
    /// tooltip or keyword, and a grid of section tiles that open a page per section.
    ///
    /// Sections and their fields are declared in <see cref="BuildCategories"/>. Every serialized field that
    /// is not assigned to a section shows up automatically under "Other", so new fields are never hidden.
    /// Fields are drawn with bound PropertyFields, so Undo and prefab overrides are handled by Unity.
    /// Styling lives in Editor/UI/RecorderControllerEditor.uss.
    /// </summary>
    [CustomEditor(typeof(RecorderController))]
    public class RecorderControllerEditor : Editor
    {
        private const string StyleSheetPath = "Packages/com.vrsys.recording/Editor/UI/RecorderControllerEditor.uss";
        private const string BannerImagePath = "Packages/com.vrsys.recording/Editor/UI/Images/recorder-banner.png";
        private const string LogoImagePath = "Packages/com.vrsys.recording/Editor/UI/Images/vr-logo.png";
        // internal: RecorderStateEditor uses these to open the "Files & Session" section.
        internal const string PageSessionKey = "VRSYS.Recording.RecorderControllerEditor.Page";
        internal const string FilesPageId = "files";
        private const string DocumentationUrl = "https://vrsys.gitbook.io/vrsys";
        private const string HomePageId = "home";
        private const string AlignedFieldClass = "unity-base-field__aligned";
        private const string RequiresServerOnlyClass = "rc-requires-server-only";
        private const string DownloadButtonClass = "rc-download-button";
        private const string DownloadHintClass = "rc-download-hint";
        private const string ServerOnlyHintClass = "rc-server-only-hint";
        private const float MinTileWidth = 165f;

        // Public fields the controller sets itself at runtime. They are state, not settings, so they are
        // shown in the header instead of as editable fields (and are not listed under "Other").
        private static readonly HashSet<string> RuntimeStatePaths = new HashSet<string> { "localPlayback" };

        #region Section model

        private sealed class Entry
        {
            public Category Category;
            public string Label;
            public string PropertyPath;     // RecorderController property path, null for actions/headings
            public bool IsHeading;
            public bool RequiresServerOnly; // enabled only with "Replay networked transforms on server only"
            public Func<VisualElement> Build;
            public string SearchText;
        }

        private sealed class Category
        {
            public string Id;
            public string Title;
            public string Description;
            public string AccentClass;
            public string[] IconNames;
            public Func<string> Status;
            public readonly List<Entry> Entries = new List<Entry>();
            public Label StatusLabel;
        }

        private enum Page { Home, Category, Search }

        #endregion

        private RecorderController _controller;
        private NetworkController _networkController;
        private SerializedObject _networkSerializedObject;
        private SerializedObject _stateSerializedObject;
        private readonly List<System.Action> _playbackFileReloaders = new List<System.Action>();
        private readonly List<Category> _categories = new List<Category>();
        private string _downloadPassword = "";

        private VisualElement _root;
        private VisualElement _header;
        private VisualElement _timeline;
        private VisualElement _timelineFill;
        private VisualElement _controls;
        private VisualElement _homePage;
        private VisualElement _categoryPage;
        private VisualElement _searchPage;
        private Label _stateLabel;
        private Label _subtitleLabel;
        private Label _timeLabel;
        private Slider _scrubber;
        private VisualElement _scrubberFill;
        private bool _scrubbing;
        private bool _pausedBeforeScrub;
        private ToolbarSearchField _searchField;
        private VisualElement _tiles;
        private Category _openCategory;
        private string _controlsKey;
        private List<Entry> _lastSearchMatches = new List<Entry>();

        private static readonly Dictionary<string, Texture> IconCache = new Dictionary<string, Texture>();

        #region Lifecycle

        private void OnEnable()
        {
            _controller = target as RecorderController;
            if (_controller == null)
                return;

            // The previous inspector forced this reference to the sibling RecorderState; keep doing that.
            if (_controller.recorderState == null)
            {
                RecorderState state = _controller.GetComponent<RecorderState>();
                if (state != null)
                {
                    serializedObject.Update();
                    serializedObject.FindProperty("recorderState").objectReferenceValue = state;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            _networkController = _controller.GetComponent<NetworkController>();
            _networkSerializedObject = _networkController != null ? new SerializedObject(_networkController) : null;

            // The RecorderState settings are edited here; its own inspector only shows a pointer to this one.
            RecorderState recorderState = _controller.recorderState != null
                ? _controller.recorderState
                : _controller.GetComponent<RecorderState>();
            _stateSerializedObject = recorderState != null ? new SerializedObject(recorderState) : null;
            CollapseRecorderStateOnce(recorderState);

            BuildCategories();
        }

        // Fold the RecorderState component in the Inspector the first time per editor session, since
        // everything it offers is shown here. The user can still expand it; we don't fight that.
        private static void CollapseRecorderStateOnce(RecorderState state)
        {
            if (state == null)
                return;
            string key = "VRSYS.Recording.RecorderStateCollapsed." + state.GetInstanceID();
            if (SessionState.GetBool(key, false))
                return;
            SessionState.SetBool(key, true);
            UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(state, false);
        }

        public override VisualElement CreateInspectorGUI()
        {
            _playbackFileReloaders.Clear();
            _root = new VisualElement();
            _root.AddToClassList("rc-root");
            _root.AddToClassList(EditorGUIUtility.isProSkin ? "rc-dark" : "rc-light");

            StyleSheet sheet = LoadStyleSheet();
            if (sheet != null)
                _root.styleSheets.Add(sheet);

            if (_controller == null)
            {
                _root.Add(new HelpBox("No RecorderController selected.", HelpBoxMessageType.Info));
                return _root;
            }

            _root.Add(BuildHeader());

            _controls = new VisualElement();
            _controls.AddToClassList("rc-controls");
            _root.Add(_controls);

            _root.Add(BuildToolbar());

            _homePage = BuildHomePage();
            _categoryPage = new VisualElement();
            _categoryPage.AddToClassList("rc-page-container");
            _searchPage = new VisualElement();
            _searchPage.AddToClassList("rc-page-container");
            _root.Add(_homePage);
            _root.Add(_categoryPage);
            _root.Add(_searchPage);

            // Reopen the section that was open last time (survives selection changes and domain reloads).
            string lastPage = SessionState.GetString(PageSessionKey, HomePageId);
            Category last = _categories.FirstOrDefault(c => c.Id == lastPage);
            if (last != null)
                ShowCategory(last);
            else
                ShowHome();

            // Play-mode state (record/replay state, times, tile summaries) is polled, not bound.
            _root.schedule.Execute(Refresh).Every(100);
            Refresh();

            return _root;
        }

        #endregion

        #region Sections

        private void BuildCategories()
        {
            _categories.Clear();

            // --- Files & Session (stored on the RecorderState) ---
            Category files = AddCategory(FilesPageId, "Files & Session",
                "Which file is recorded and which recording is played back. Stored on the RecorderState component.",
                "rc-accent-files", FilesStatus, "TextAsset Icon", "Folder Icon");
            StateProp(files, "recordingFile", "Recording name",
                "Name of the recording file that will be created when recording.",
                "recording file name output record");
            StateProp(files, "fileNameDateSuffix", "File name date suffix",
                "None: no date information added to the file name. Date: date added to the file name. DateTime: date and timestamp added to the file name.",
                "date time timestamp suffix file name");
            StateProp(files, "recorderID", "Recorder ID",
                "The ID of a recorder needs to be unique.",
                "id identifier recorder unique");
            Heading(files, "Playback");
            StateProp(files, "useLocalReplayFiles", "Use local replay files",
                "If enabled, the playback list shows recordings found in the local recording directory and the server is not queried for the replay list. If disabled, it lists recordings retrieved from the server.",
                "local server replay list playback source files");
            ActionEntry(files, "Selected Replay File",
                "The recording that is played back by the Replay buttons (RecorderState.fixedPlaybackRecordingName).",
                "playback replay file recording select choose selected fixedPlaybackRecordingName",
                () => BuildPlaybackFileField("Selected Replay File"));

            // --- Scene Setup ---
            Category setup = AddCategory("setup", "Scene Setup",
                "One-time setup: adds the prefab information that playback needs to re-instantiate recorded objects.",
                "rc-accent-setup", SetupStatus, "Prefab Icon");
            Prop(setup, "recorderState", "Recorder State",
                "The RecorderState on this GameObject. It stores the recording file, server and replay information. Assigned automatically.",
                "state file replay information",
                f => f.SetEnabled(false));
            ActionEntry(setup, "Set up networked prefabs",
                "Adds or updates RecordingPrefabInformation on every prefab in all NetworkPrefabsLists, so playback can instantiate them.",
                "prefab information network NetworkPrefabsList addressable setup instantiate requirements",
                () => InlineButton("Set up networked prefabs",
                    "Adds or updates RecordingPrefabInformation on every prefab in all NetworkPrefabsLists.",
                    AddPrefabInformationToAllNetworkPrefabs));
            ActionEntry(setup, "Set up all prefabs under Assets",
                "Adds or updates RecordingPrefabInformation on every .prefab under Assets, so playback can instantiate them.",
                "prefab information assets addressable setup instantiate requirements",
                () => InlineButton("Set up all prefabs under Assets",
                    "Adds or updates RecordingPrefabInformation on every .prefab under Assets.",
                    AddPrefabInformationToAllPrefabs));

            // --- Transform Recording ---
            Category transform = AddCategory("transform", "Transform Recording",
                "What is captured from transforms and how often.",
                "rc-accent-transform", TransformStatus, "Transform Icon");
            Prop(transform, "attachTransformRecorderToAll", "Attach to all transforms",
                "Attach a transform recorder to every object in the scene when recording.",
                "transform recorder every object scene");
            Prop(transform, "transformRecordingStepsPerSecond", "Recording steps per second",
                "How many transform samples are captured per second while recording.",
                "rate frequency hz sample fps steps");
            Prop(transform, "recordOnLocalTransformChangesOnly", "Record on local changes only",
                "When enabled, a transform is recorded only when its local position/rotation/scale changes. When disabled, changes are detected from the global (world) transform instead.",
                "local world global position rotation scale change detection");

            // --- Audio Recording ---
            Category audio = AddCategory("audio", "Audio Recording",
                "Which audio is captured into the recording.",
                "rc-accent-audio", AudioStatus, "AudioSource Icon");
            Prop(audio, "recordMicro", "Record microphone",
                "Capture the local microphone into the recording.",
                "mic microphone voice sound audio");
            Prop(audio, "recordAudioListener", "Record audio listener",
                "Capture the scene's AudioListener output into the recording.",
                "listener mix output sound audio");
            Prop(audio, "recordAllSoundSources", "Record all sound sources",
                "Capture every AudioSource in the scene into the recording.",
                "audiosource sources sound audio");
            Prop(audio, "audioRecordingStepsPerSecond", "Audio steps per second",
                "How many audio recording steps are captured per second.",
                "rate frequency hz sample steps sound audio");

            // --- Playback ---
            Category playback = AddCategory("playback", "Playback",
                "How a recording is applied to the scene when it is replayed.",
                "rc-accent-playback", PlaybackStatus, "Animation.Play", "PlayButton");
            Prop(playback, "replayRoot", "Replay Root",
                "Optional anchor for playback. When set, recorded objects are matched/placed relative to this transform: pre-existing duplicate objects beneath it are matched to the recording, and objects that have to be instantiated for playback are created under it. Its position/rotation/scale thus offsets the whole replay. Leave empty to match/place objects at the scene root.",
                "anchor offset parent origin root");
            Prop(playback, "replayHierarchyChanges", "Replay hierarchy changes",
                "Reapply recorded reparenting (hierarchy changes) during playback. Disable to keep objects under their initial parent.",
                "parent reparent hierarchy");
            Prop(playback, "replayLocalTransformChangesOnly", "Replay on local changes only",
                "When enabled, a replayed transform is applied from its recorded local position/rotation/scale. When disabled, the recorded global (world) transform is used instead.",
                "local world global position rotation scale");
            Prop(playback, "playbackTransform", "Playback transform",
                "Attach TransformRecorder components during replay so recorded transforms are applied.",
                "transform recorder apply");
            Prop(playback, "instantiateMissingObjects", "Instantiate missing objects",
                "When enabled, recorded objects that are not present in the scene are instantiated for playback (querying their mesh/prefab information). When disabled, only objects already present in the scene are played back.",
                "spawn create missing prefab mesh");
            Prop(playback, "replayAudio", "Playback audio",
                "Handle recorded audio sources during replay.",
                "sound audio");
            Heading(playback, "End of playback");
            Prop(playback, "endPlaybackOnDurationReached", "Stop at recording end",
                "Once playback time has reached the duration, the playback will be stopped.",
                "stop end duration finish reached");
            Prop(playback, "endPlaybackOnReplayError", "Stop on replay error",
                "End the playback as soon as a transform, audio or generic recorder fails to replay a frame. When disabled, failing recorders are skipped and the playback continues.",
                "stop error fail");
            Prop(playback, "indefinitePlayback", "Indefinite playback",
                "Playback time will advance beyond the recording duration. Relevant for rerecording.",
                "rerecording endless loop beyond duration");

            // --- Network & Sync ---
            Category network = AddCategory("network", "Network & Sync",
                "Multi-user replay: who drives networked objects and how peers stay in sync.",
                "rc-accent-network", NetworkStatus, "NetworkAnimator Icon", "CloudConnect", "BuildSettings.Web.Small");
            Prop(network, "replayNetworkedTransformOnServerOnly", "Replay on server only",
                "Replay networked transforms on the server only. Objects with a NetworkTransform are only replayed on the server (or host); clients receive their poses through the NetworkTransform. Objects without a NetworkTransform are replayed on every peer.",
                "server host client networktransform netcode networked transforms");
            Prop(network, "spawnNetworkedPrefabsForReplay", "Spawn network prefabs",
                "Spawn networked prefabs for replay. Missing objects whose prefab is a registered network prefab are spawned by the server (keeping their network components) instead of being created as local copies on every peer. The server replays them; clients receive their poses through the NetworkTransform. Requires 'Replay on server only'. Avatars stay local copies.",
                "spawn network prefab server netcode",
                requiresServerOnly: true);
            Prop(network, "networkedSpawnTimeout", "Spawn timeout (s)",
                "How long a client waits for the spawned copies before it starts the replay without them.",
                "timeout wait spawn seconds networked",
                f => f.RegisterValueChangeCallback(evt =>
                {
                    if (evt.changedProperty.floatValue < 0f)
                    {
                        evt.changedProperty.floatValue = 0f;
                        evt.changedProperty.serializedObject.ApplyModifiedProperties();
                    }
                }),
                requiresServerOnly: true);
            Hint(network, "Enable 'Replay on server only' to use network prefab spawning.", ServerOnlyHintClass);
            Prop(network, "synchronizedPlayback", "Synchronised playback",
                "Keep playback time synchronised across all networked clients.",
                "sync synchronized time clients");
            Prop(network, "lateJoinPlayback", "Late join playback",
                "Start clients that join after playback began at the current playback time.",
                "late join client connect");
            ActionEntry(network, "Max sync time (ms)",
                "Delay added to distributed recording and replay commands so clients can schedule them against synchronized network time. Stored on the NetworkController.",
                "delay latency schedule network time ms NetworkController",
                BuildMaxSyncTimeField);

            // --- Server & Files ---
            Category server = AddCategory("server", "Server & Files",
                "Uploading and downloading recording files and opening the local recording folder.",
                "rc-accent-server", ServerStatus, "CloudConnect", "Folder Icon");
            StateProp(server, "projectName", "Project name",
                "Name of the project for which recordings are created. Used for upload and download.",
                "project name upload download zip");
            StateProp(server, "selectedServer", "Selected server",
                "URL of the server to which files are uploaded and from which files are downloaded.",
                "server url address host upload download");
            Prop(server, "uploadFilesToServer", "Upload to server",
                "Upload the produced recording files to the configured server.",
                "upload server transfer recording files");
            Prop(server, "downloadFilesFromServer", "Download from server",
                "Download recording files from the configured server before playback.",
                "download server transfer recording files");
            ActionEntry(server, "Open recording directory",
                "Opens the recording directory (or the persistent data path if none is set) in the file browser.",
                "folder directory explorer finder path files",
                () => InlineButton("Open recording directory",
                    "Opens the recording directory in the file browser.", OpenRecordingDirectoryFromState));
            ActionEntry(server, "Download project zip",
                "Downloads all recordings of the project from the selected server as a zip file.",
                "download zip project server password",
                BuildDownloadZipBlock);

            // --- Output & Debug ---
            Category output = AddCategory("output", "Output & Debug",
                "Extra export files, logging and native plugin buffers.",
                "rc-accent-output", OutputStatus, "console.infoicon", "UnityEditor.ConsoleWindow");
            Prop(output, "createWAV", "Create WAV",
                "Export recorded audio to a .wav file alongside the recording.",
                "wav export audio file");
            Prop(output, "createCSV", "Create CSV",
                "Export recorded data to a .csv file alongside the recording.",
                "csv export data file");
            Prop(output, "debugLogs", "Print debug logs",
                "Print verbose recording/playback debug logs to the console.",
                "log debug verbose console");
            Heading(output, "Native plugin");
            Prop(output, "pluginSettings.recordingMaxBufferSize", "Recording max buffer size",
                "Maximum native buffer size for transform recording data.",
                "buffer memory native plugin");
            Prop(output, "pluginSettings.replayBufferNumber", "Replay buffer number",
                "Number of native replay buffers to keep available.",
                "buffer memory native plugin");
            Prop(output, "pluginSettings.replayBufferTimeInterval", "Replay buffer time interval",
                "Time span covered by each native replay buffer.",
                "buffer time native plugin");
            Prop(output, "pluginSettings.recordingSoundMaxBufferSize", "Sound max buffer size",
                "Maximum native buffer size for recorded sound data.",
                "buffer audio sound native plugin");
            Prop(output, "pluginSettings.logLevel", "Plugin log level",
                "Minimum native recording plugin log level forwarded to Unity.",
                "log level native plugin verbosity");
            Prop(output, "pluginSettings.versionInfo", "Plugin version",
                "Version reported by the native recording plugin.",
                "version native plugin",
                f => f.SetEnabled(false));

            // --- Events ---
            Category events = AddCategory("events", "Events",
                "UnityEvents raised when recording or replay starts and ends.",
                "rc-accent-events", EventsStatus, "EventSystem Icon", "EventTrigger Icon");
            System.Action<PropertyField> spaced = f => f.AddToClassList("rc-entry-spaced");
            Prop(events, "onRecordingHasStarted", "On recording has started", "Raised when a recording starts.", "event callback start record", spaced);
            Prop(events, "onRecordingHasEnded", "On recording has ended", "Raised when a recording ends.", "event callback end stop record", spaced);
            Prop(events, "onReplayIsPreparing", "On replay is preparing", "Raised while a replay is being prepared (files loaded, objects matched).", "event callback prepare replay", spaced);
            Prop(events, "onReplayHasStarted", "On replay has started", "Raised when a replay starts.", "event callback start replay playback", spaced);
            Prop(events, "onReplayHasEnded", "On replay has ended", "Raised when a replay ends.", "event callback end stop replay playback", spaced);

            AddUncoveredFieldsCategory();
        }

        // Any serialized field that no section claims ends up here, so new fields stay visible and searchable.
        private void AddUncoveredFieldsCategory()
        {
            HashSet<string> covered = new HashSet<string>(_categories
                .SelectMany(c => c.Entries)
                .Where(e => e.PropertyPath != null)
                .Select(e => e.PropertyPath.Split('.')[0]));

            List<(string path, string label, string tooltip)> uncovered = new List<(string, string, string)>();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script" || covered.Contains(iterator.propertyPath) ||
                    RuntimeStatePaths.Contains(iterator.propertyPath))
                    continue;
                uncovered.Add((iterator.propertyPath, iterator.displayName, iterator.tooltip));
            }

            if (uncovered.Count == 0)
                return;

            Category other = AddCategory("other", "Other",
                "Serialized fields that are not assigned to a section yet. Add them to a section in RecorderControllerEditor.BuildCategories().",
                "rc-accent-other", () => uncovered.Count == 1 ? "1 field" : uncovered.Count + " fields",
                "_Popup", "Settings");
            foreach ((string path, string label, string tooltip) in uncovered)
                Prop(other, path, label, tooltip, "");
        }

        private Category AddCategory(string id, string title, string description, string accentClass,
            Func<string> status, params string[] iconNames)
        {
            Category category = new Category
            {
                Id = id,
                Title = title,
                Description = description,
                AccentClass = accentClass,
                Status = status,
                IconNames = iconNames
            };
            _categories.Add(category);
            return category;
        }

        private void Prop(Category category, string path, string label, string tooltip, string keywords,
            System.Action<PropertyField> configure = null, bool requiresServerOnly = false)
        {
            AddEntry(category, label, tooltip, keywords, path, requiresServerOnly, () =>
            {
                SerializedProperty property = serializedObject.FindProperty(path);
                if (property == null)
                    return new HelpBox($"Property '{path}' was not found on RecorderController.", HelpBoxMessageType.Warning);

                PropertyField field = new PropertyField(property, label) { tooltip = tooltip };
                field.BindProperty(property);
                configure?.Invoke(field);
                return field;
            });
        }

        // Like Prop, but for a field of the sibling RecorderState component.
        private void StateProp(Category category, string path, string label, string tooltip, string keywords,
            System.Action<PropertyField> configure = null)
        {
            AddEntry(category, label, tooltip, keywords + " recorderstate " + path, null, false, () =>
            {
                if (_stateSerializedObject == null || _stateSerializedObject.targetObject == null)
                    return new HelpBox("No RecorderState on this GameObject.", HelpBoxMessageType.Warning);

                SerializedProperty property = _stateSerializedObject.FindProperty(path);
                if (property == null)
                    return new HelpBox($"Property '{path}' was not found on RecorderState.", HelpBoxMessageType.Warning);

                PropertyField field = new PropertyField(property, label) { tooltip = tooltip };
                field.BindProperty(property);
                configure?.Invoke(field);
                return field;
            });
        }

        private void ActionEntry(Category category, string label, string tooltip, string keywords, Func<VisualElement> build)
        {
            AddEntry(category, label, tooltip, keywords, null, false, build);
        }

        private void Heading(Category category, string text)
        {
            category.Entries.Add(new Entry
            {
                Category = category,
                Label = text,
                IsHeading = true,
                Build = () =>
                {
                    Label heading = new Label(text);
                    heading.AddToClassList("rc-subheading");
                    return heading;
                }
            });
        }

        // Small explanatory line inside a section; not searchable. Its visibility is driven by Refresh().
        private static void Hint(Category category, string text, string styleClass)
        {
            category.Entries.Add(new Entry
            {
                Category = category,
                Label = text,
                IsHeading = true,
                Build = () =>
                {
                    Label hint = new Label(text);
                    hint.AddToClassList("rc-hint");
                    hint.AddToClassList("rc-dependent");
                    hint.AddToClassList(styleClass);
                    return hint;
                }
            });
        }

        private static void AddEntry(Category category, string label, string tooltip, string keywords, string path,
            bool requiresServerOnly, Func<VisualElement> build)
        {
            category.Entries.Add(new Entry
            {
                Category = category,
                Label = label,
                PropertyPath = path,
                RequiresServerOnly = requiresServerOnly,
                Build = build,
                SearchText = string.Join(" ", label, tooltip, keywords, path, category.Title).ToLowerInvariant()
            });
        }

        #endregion

        #region Tile status lines

        private string FilesStatus()
        {
            RecorderState state = _controller.recorderState;
            if (state == null)
                return "RecorderState missing";
            string playback = string.IsNullOrEmpty(state.fixedPlaybackRecordingName)
                ? "no playback file"
                : "plays " + state.fixedPlaybackRecordingName;
            return Join(string.IsNullOrEmpty(state.recordingFile) ? "unnamed" : state.recordingFile, playback);
        }

        private string SetupStatus()
        {
            return _controller.recorderState != null ? "RecorderState linked" : "RecorderState missing";
        }

        private string TransformStatus()
        {
            return Join(
                _controller.transformRecordingStepsPerSecond + " Hz",
                _controller.attachTransformRecorderToAll ? "all objects" : "selected",
                _controller.recordOnLocalTransformChangesOnly ? "local" : "world");
        }

        private string AudioStatus()
        {
            string sources = Join(
                _controller.recordMicro ? "Mic" : null,
                _controller.recordAudioListener ? "Listener" : null,
                _controller.recordAllSoundSources ? "Sources" : null);
            return string.IsNullOrEmpty(sources) ? "No audio captured" : sources;
        }

        private string PlaybackStatus()
        {
            return Join(
                _controller.replayRoot != null ? "Root: " + _controller.replayRoot.name : "Scene root",
                _controller.replayAudio ? "audio" : null,
                _controller.indefinitePlayback ? "indefinite" : _controller.endPlaybackOnDurationReached ? "stops at end" : null);
        }

        private string NetworkStatus()
        {
            return Join(
                _controller.replayNetworkedTransformOnServerOnly ? "Server-driven" : "All peers",
                _controller.synchronizedPlayback ? "synced" : "unsynced",
                _controller.lateJoinPlayback ? "late join" : null,
                _networkController != null ? _networkController.maxSynchronizationTimeMS.ToString("0") + " ms" : null);
        }

        private string ServerStatus()
        {
            return Join("Upload " + OnOff(_controller.uploadFilesToServer),
                "Download " + OnOff(_controller.downloadFilesFromServer));
        }

        private string OutputStatus()
        {
            string exports = Join(_controller.createWAV ? "WAV" : null, _controller.createCSV ? "CSV" : null);
            return Join(
                string.IsNullOrEmpty(exports) ? "No exports" : exports,
                _controller.debugLogs ? "debug logs" : null,
                _controller.pluginSettings != null ? "log " + _controller.pluginSettings.logLevel : null);
        }

        private string EventsStatus()
        {
            UnityEvent[] events =
            {
                _controller.onRecordingHasStarted, _controller.onRecordingHasEnded, _controller.onReplayIsPreparing,
                _controller.onReplayHasStarted, _controller.onReplayHasEnded
            };
            int count = events.Where(e => e != null).Sum(e => e.GetPersistentEventCount());
            return count == 0 ? "No persistent listeners" : count == 1 ? "1 persistent listener" : count + " persistent listeners";
        }

        private static string OnOff(bool value) => value ? "on" : "off";

        private static string Join(params string[] parts)
        {
            return string.Join("  ·  ", parts.Where(p => !string.IsNullOrEmpty(p)));
        }

        #endregion

        #region Layout

        private VisualElement BuildHeader()
        {
            _header = new VisualElement();
            _header.AddToClassList("rc-header");

            // Banner image (sphere moving along a dotted trajectory) behind the title, like COZY's banner.
            // Cropped to fill the header and anchored right, so the trajectory stays visible in narrow inspectors.
            Texture2D banner = LoadEditorImage(BannerImagePath, "recorder-banner");
            if (banner != null)
            {
                _header.AddToClassList("rc-banner");
                _header.style.backgroundImage = banner;
                _header.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                _header.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Right);
                _header.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            }

            VisualElement row = new VisualElement();
            row.AddToClassList("rc-row");

            // Group logo in the top-left corner, next to the title.
            Texture2D logo = LoadEditorImage(LogoImagePath, "vr-logo");
            if (logo != null)
            {
                Image logoImage = new Image
                {
                    image = logo,
                    scaleMode = ScaleMode.ScaleToFit,
                    tooltip = "Virtual Reality and Visualization Research, Bauhaus-Universität Weimar"
                };
                logoImage.AddToClassList("rc-logo");
                row.Add(logoImage);
            }

            VisualElement titles = new VisualElement();
            titles.AddToClassList("rc-header-titles");
            Label title = new Label("VRSYS Recorder");
            title.AddToClassList("rc-title");
            _subtitleLabel = new Label();
            _subtitleLabel.AddToClassList("rc-subtitle");
            titles.Add(title);
            titles.Add(_subtitleLabel);

            _stateLabel = new Label();
            _stateLabel.AddToClassList("rc-pill");

            row.Add(titles);
            row.Add(Spacer());
            row.Add(_stateLabel);
            _header.Add(row);

            _timeLabel = new Label();
            _timeLabel.AddToClassList("rc-time");
            _header.Add(_timeLabel);

            _timeline = new VisualElement();
            _timeline.AddToClassList("rc-timeline");
            _timelineFill = new VisualElement();
            _timelineFill.AddToClassList("rc-timeline-fill");
            _timeline.Add(_timelineFill);
            _header.Add(_timeline);

            // Interactive replay timeline: drag to jump to a time (replaces the static bar while replaying).
            _scrubber = new Slider(0f, 1f) { tooltip = "Drag to jump to a time in the replay" };
            _scrubber.AddToClassList("rc-scrubber");
            _scrubber.RegisterValueChangedCallback(evt => OnScrub(evt.newValue));
            _scrubber.RegisterCallback<PointerDownEvent>(_ => BeginScrub(), TrickleDown.TrickleDown);
            _scrubber.RegisterCallback<PointerUpEvent>(_ => EndScrub(), TrickleDown.TrickleDown);
            _scrubber.RegisterCallback<PointerCaptureOutEvent>(_ => EndScrub(), TrickleDown.TrickleDown);
            VisualElement tracker = _scrubber.Q("unity-tracker");
            if (tracker != null)
            {
                _scrubberFill = new VisualElement { pickingMode = PickingMode.Ignore };
                _scrubberFill.AddToClassList("rc-scrubber-fill");
                tracker.Add(_scrubberFill);
            }
            _header.Add(_scrubber);

            return _header;
        }

        private VisualElement BuildToolbar()
        {
            Toolbar toolbar = new Toolbar();
            toolbar.AddToClassList("rc-toolbar");

            _searchField = new ToolbarSearchField { tooltip = "Search all settings by name, description or keyword" };
            _searchField.AddToClassList("rc-search");
            _searchField.RegisterValueChangedCallback(evt => OnSearchChanged(evt.newValue));
            // Enter jumps to the section of the first result, with the matches highlighted.
            _searchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && _lastSearchMatches.Count > 0)
                {
                    Category first = _lastSearchMatches[0].Category;
                    HashSet<Entry> highlight = new HashSet<Entry>(_lastSearchMatches.Where(m => m.Category == first));
                    _searchField.SetValueWithoutNotify("");
                    ShowCategory(first, highlight);
                    evt.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
            toolbar.Add(_searchField);
            toolbar.Add(Spacer());

            ToolbarButton overview = new ToolbarButton(() =>
            {
                _searchField.SetValueWithoutNotify("");
                ShowHome();
            })
            {
                text = "Overview",
                tooltip = "Show all sections"
            };
            toolbar.Add(overview);

            ToolbarButton docs = new ToolbarButton(() => Application.OpenURL(DocumentationUrl)) { tooltip = "Open VRSYS documentation" };
            docs.AddToClassList("rc-toolbar-icon");
            AddIconOrText(docs, "?", "_Help");
            toolbar.Add(docs);

            return toolbar;
        }

        private VisualElement BuildHomePage()
        {
            VisualElement page = new VisualElement();
            page.AddToClassList("rc-page-container");

            VisualElement tiles = new VisualElement();
            tiles.AddToClassList("rc-tiles");
            _tiles = tiles;
            // Equal-width columns that adapt to the inspector width (flex-wrap alone stretches the last row).
            tiles.RegisterCallback<GeometryChangedEvent>(_ => LayoutTiles());

            foreach (Category category in _categories)
            {
                Category captured = category;
                Button tile = new Button(() => ShowCategory(captured)) { tooltip = category.Description };
                tile.AddToClassList("rc-tile");
                tile.AddToClassList(category.AccentClass);

                tile.Add(Badge(category, "rc-badge"));

                VisualElement text = new VisualElement();
                text.AddToClassList("rc-tile-text");
                Label title = new Label(category.Title);
                title.AddToClassList("rc-tile-title");
                category.StatusLabel = new Label();
                category.StatusLabel.AddToClassList("rc-tile-status");
                text.Add(title);
                text.Add(category.StatusLabel);
                tile.Add(text);

                tiles.Add(tile);
            }

            page.Add(tiles);
            return page;
        }

        private void LayoutTiles()
        {
            if (_tiles == null)
                return;

            float width = _tiles.contentRect.width;
            int count = _tiles.childCount;
            if (width <= 0f || count == 0)
                return;

            int columns = Mathf.Clamp(Mathf.FloorToInt(width / MinTileWidth), 1, count);
            // Use the resolved margins: on scaled displays they are pixel-snapped (e.g. 3px -> 3.33pt),
            // and a hard-coded value makes the last column wrap.
            IResolvedStyle first = _tiles[0].resolvedStyle;
            float margins = first.marginLeft + first.marginRight;
            float tileWidth = Mathf.Floor(width / columns - margins - 0.5f);
            _tiles.EnableInClassList("rc-tiles-compact", columns == 1);

            foreach (VisualElement tile in _tiles.Children())
            {
                if (Mathf.Abs(tile.resolvedStyle.width - tileWidth) > 0.5f)
                    tile.style.width = tileWidth;
            }
        }

        private void ShowHome()
        {
            _openCategory = null;
            SessionState.SetString(PageSessionKey, HomePageId);
            SetPage(Page.Home);
        }

        private void ShowCategory(Category category, HashSet<Entry> highlight = null)
        {
            _openCategory = category;
            SessionState.SetString(PageSessionKey, category.Id);
            _categoryPage.Clear();

            VisualElement page = new VisualElement();
            page.AddToClassList("rc-page");
            page.AddToClassList(category.AccentClass);

            VisualElement header = new VisualElement();
            header.AddToClassList("rc-page-header");

            Button back = new Button(ShowHome) { tooltip = "Back to overview" };
            back.AddToClassList("rc-icon-button");
            AddIconOrText(back, "<", "back");
            header.Add(back);
            header.Add(Badge(category, "rc-badge-small"));

            Label title = new Label(category.Title);
            title.AddToClassList("rc-page-title");
            header.Add(title);
            header.Add(Spacer());

            int index = _categories.IndexOf(category);
            Button previous = new Button(() => ShowCategory(_categories[(index - 1 + _categories.Count) % _categories.Count]))
                { tooltip = "Previous section" };
            previous.AddToClassList("rc-icon-button");
            AddIconOrText(previous, "<", "Animation.PrevKey");
            Button next = new Button(() => ShowCategory(_categories[(index + 1) % _categories.Count]))
                { tooltip = "Next section" };
            next.AddToClassList("rc-icon-button");
            AddIconOrText(next, ">", "Animation.NextKey");
            header.Add(previous);
            header.Add(next);
            page.Add(header);

            Label description = new Label(category.Description);
            description.AddToClassList("rc-page-desc");
            page.Add(description);

            VisualElement body = new VisualElement();
            body.AddToClassList("rc-page-body");
            foreach (Entry entry in category.Entries)
                body.Add(BuildEntry(entry, highlight != null && highlight.Contains(entry)));
            page.Add(body);

            _categoryPage.Add(page);
            SetPage(Page.Category);
            Refresh();
        }

        private void OnSearchChanged(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                if (_openCategory != null)
                    ShowCategory(_openCategory);
                else
                    ShowHome();
                return;
            }

            ShowSearch(query);
        }

        private void ShowSearch(string query)
        {
            _searchPage.Clear();

            string[] tokens = query.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            // Settings whose label matches come first, so their sections are listed first as well.
            List<Entry> matches = _categories
                .SelectMany(c => c.Entries)
                .Where(e => !e.IsHeading && tokens.All(t => e.SearchText.Contains(t)))
                .OrderBy(e => tokens.All(t => e.Label.ToLowerInvariant().Contains(t)) ? 0 : 1)
                .ToList();
            _lastSearchMatches = matches;

            if (matches.Count == 0)
            {
                Label empty = new Label($"No settings match \"{query.Trim()}\".");
                empty.AddToClassList("rc-empty");
                _searchPage.Add(empty);
                SetPage(Page.Search);
                return;
            }

            int sectionCount = matches.Select(m => m.Category).Distinct().Count();
            Label summary = new Label($"{matches.Count} setting{(matches.Count == 1 ? "" : "s")} in {sectionCount} section{(sectionCount == 1 ? "" : "s")}  ·  Enter opens the first section");
            summary.AddToClassList("rc-search-summary");
            _searchPage.Add(summary);

            foreach (IGrouping<Category, Entry> group in matches.GroupBy(m => m.Category))
            {
                Category category = group.Key;
                HashSet<Entry> groupEntries = new HashSet<Entry>(group);

                VisualElement container = new VisualElement();
                container.AddToClassList("rc-result-group");
                container.AddToClassList(category.AccentClass);

                Button header = new Button(() =>
                {
                    _searchField.SetValueWithoutNotify("");
                    ShowCategory(category, groupEntries);
                }) { tooltip = "Open the " + category.Title + " section with these settings highlighted" };
                header.AddToClassList("rc-result-header");
                header.Add(Badge(category, "rc-badge-small"));
                Label title = new Label(category.Title);
                title.AddToClassList("rc-result-title");
                header.Add(title);
                header.Add(Spacer());
                Label open = new Label("Open section");
                open.AddToClassList("rc-result-open");
                header.Add(open);
                container.Add(header);

                foreach (Entry entry in group)
                    container.Add(BuildEntry(entry, false));

                _searchPage.Add(container);
            }

            SetPage(Page.Search);
            Refresh();
        }

        private VisualElement BuildEntry(Entry entry, bool highlight)
        {
            VisualElement element = entry.Build();
            element.AddToClassList("rc-entry");
            if (highlight)
                element.AddToClassList("rc-highlight");
            if (entry.RequiresServerOnly)
            {
                element.AddToClassList(RequiresServerOnlyClass);
                element.AddToClassList("rc-dependent");
            }
            return element;
        }

        private void SetPage(Page page)
        {
            _homePage.style.display = page == Page.Home ? DisplayStyle.Flex : DisplayStyle.None;
            _categoryPage.style.display = page == Page.Category ? DisplayStyle.Flex : DisplayStyle.None;
            _searchPage.style.display = page == Page.Search ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #endregion

        #region Live refresh (state, controls, tile summaries)

        private void Refresh()
        {
            if (_controller == null || _root == null)
                return;

            RecorderState state = _controller.recorderState;
            bool hasState = state != null;
            State current = hasState ? state.currentState : State.Idle;

            // Header
            _stateLabel.text = hasState ? StateDisplayName(current) : "No state";
            foreach (State s in Enum.GetValues(typeof(State)))
                _header.EnableInClassList(StateClass(s), hasState && s == current);
            _header.EnableInClassList("rc-state-missing", !hasState);

            string mode = null;
            if (hasState && current == State.Recording)
                mode = state.localRecording ? "local recording" : "distributed recording";
            else if (hasState && (current == State.Replaying || current == State.PreparingReplay))
                mode = _controller.localPlayback ? "local replay" : "distributed replay";

            _subtitleLabel.text = hasState
                ? Join("Recorder ID " + state.recorderID,
                    string.IsNullOrEmpty(state.recordingFile) ? null : state.recordingFile,
                    mode)
                : "Add a RecorderState component to this GameObject";

            UpdateTimeline(state, current, hasState);
            RebuildControls(state, current, hasState);

            // The server replay list arrives at runtime; keep the playback-file dropdowns current.
            if (hasState && !state.useLocalReplayFiles)
                foreach (System.Action reload in _playbackFileReloaders)
                    reload();

            // Tile summaries
            foreach (Category category in _categories)
            {
                if (category.StatusLabel == null)
                    continue;
                try
                {
                    category.StatusLabel.text = category.Status != null ? category.Status() : "";
                }
                catch (Exception)
                {
                    category.StatusLabel.text = "";
                }
            }

            // Fields that only apply when networked transforms are replayed on the server
            bool serverOnly = _controller.replayNetworkedTransformOnServerOnly;
            _root.Query(className: RequiresServerOnlyClass).ForEach(e => e.SetEnabled(serverOnly));
            _root.Query(className: ServerOnlyHintClass).ForEach(e =>
                e.style.display = serverOnly ? DisplayStyle.None : DisplayStyle.Flex);

            // Download button needs project name, server and password; say what is missing.
            List<string> missing = new List<string>();
            if (!hasState || string.IsNullOrEmpty(state.projectName)) missing.Add("a project name");
            if (!hasState || string.IsNullOrEmpty(state.selectedServer)) missing.Add("a selected server");
            if (string.IsNullOrEmpty(_downloadPassword)) missing.Add("the password");
            bool canDownload = missing.Count == 0;
            _root.Query(className: DownloadButtonClass).ForEach(e => e.SetEnabled(canDownload));
            _root.Query<Label>(className: DownloadHintClass).ForEach(l =>
            {
                l.style.display = canDownload ? DisplayStyle.None : DisplayStyle.Flex;
                l.text = canDownload ? "" : "Needs " + string.Join(", ", missing) + ".";
            });
        }

        private void UpdateTimeline(RecorderState state, State current, bool hasState)
        {
            bool recording = hasState && current == State.Recording && state.currentRecordingTime >= 0f;
            bool replaying = hasState && current == State.Replaying && state.currentReplayTime >= 0f;

            _timeLabel.style.display = recording || replaying ? DisplayStyle.Flex : DisplayStyle.None;
            _timeline.style.display = recording ? DisplayStyle.Flex : DisplayStyle.None;
            _scrubber.style.display = replaying ? DisplayStyle.Flex : DisplayStyle.None;
            if (!replaying)
                _scrubbing = false;

            if (recording)
            {
                _timeLabel.text = FormatTime(state.currentRecordingTime);
                _timelineFill.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
                return;
            }

            if (!replaying)
                return;

            float time = state.currentReplayTime;
            float duration = state.recordingDuration;
            string text = duration > 0f ? FormatTime(time) + " / " + FormatTime(duration) : FormatTime(time);
            if (state.replayPaused && !_scrubbing)
                text += "   (paused)";
            _timeLabel.text = text;

            float max = Mathf.Max(duration, time, 0.01f);
            if (!Mathf.Approximately(_scrubber.highValue, max))
                _scrubber.highValue = max;
            if (!_scrubbing)
                _scrubber.SetValueWithoutNotify(time);
            if (_scrubberFill != null)
                _scrubberFill.style.width = new StyleLength(new Length(Mathf.Clamp01(_scrubber.value / max) * 100f, LengthUnit.Percent));

            bool canSeek = CanSeek(out string reason);
            _scrubber.SetEnabled(canSeek);
            _scrubber.tooltip = canSeek ? "Drag to jump to a time in the replay" : reason;
        }

        // Seeking sets RecorderState.currentReplayTime, as the TimeInteractor does for rewind/forward.
        // In synchronised distributed playback the host's time is authoritative, so only the host can seek.
        private bool CanSeek(out string reason)
        {
            RecorderState state = _controller.recorderState;
            reason = null;
            if (!EditorApplication.isPlaying || state == null || state.currentState != State.Replaying)
            {
                reason = "Available while a replay is running.";
                return false;
            }

            NetworkManager network = NetworkManager.Singleton;
            if (!_controller.localPlayback && _controller.synchronizedPlayback &&
                network != null && network.IsListening && !network.IsServer)
            {
                reason = "Synchronised playback follows the host. Seek on the host instead.";
                return false;
            }

            return true;
        }

        private void BeginScrub()
        {
            if (_scrubbing || !CanSeek(out _))
                return;
            _scrubbing = true;
            _pausedBeforeScrub = _controller.recorderState.replayPaused;
            _controller.PlaybackPause(); // hold the playhead while dragging (this peer only)
        }

        private void EndScrub()
        {
            if (!_scrubbing)
                return;
            _scrubbing = false;
            if (!_pausedBeforeScrub)
                _controller.PlaybackResume();
        }

        private void OnScrub(float value)
        {
            if (!CanSeek(out _))
                return;

            RecorderState state = _controller.recorderState;
            // Same bounds as the TimeInteractor: never before the start, stay just short of the end.
            float upper = state.recordingDuration > 0.2f && !_controller.indefinitePlayback
                ? state.recordingDuration - 0.1f
                : Mathf.Max(value, 0f);
            state.currentReplayTime = Mathf.Clamp(value, 0f, upper);
            UpdateTimeline(state, state.currentState, true);
        }

        private void RebuildControls(RecorderState state, State current, bool hasState)
        {
            bool isPlaying = EditorApplication.isPlaying;
            string key = string.Join("|", hasState, isPlaying, current, _controller.localPlayback,
                hasState && state.localRecording, hasState && state.replayPaused);
            if (key == _controlsKey)
                return;
            _controlsKey = key;
            _controls.Clear();

            if (!hasState)
            {
                _controls.Add(new HelpBox("RecorderController needs a RecorderState component on the same GameObject.",
                    HelpBoxMessageType.Warning));
                return;
            }

            // One row per action group with a short caption, so button labels stay short and never clip.
            // Icons: record dot / play triangle for the action, one person (local) or a group (distributed).
            switch (current)
            {
                case State.Idle:
                    _controls.Add(ActionRow("Record",
                        ActionButton("Distributed", "rc-action-record", "Start a recording on all connected peers.",
                            isPlaying, () => _controller.SendStartRecordingEvents(), GlyphKind.Record, GlyphKind.People),
                        ActionButton("Local", "rc-action-record", "Start a recording on this instance only.",
                            isPlaying, () => _controller.StartLocalRecording(), GlyphKind.Record, GlyphKind.Person)));
                    _controls.Add(ActionRow("Replay",
                        ActionButton("Distributed", "rc-action-replay", "Replay the selected recording on all connected peers.",
                            isPlaying, () => _controller.PrepareAndStartDistributedReplay(), GlyphKind.Play, GlyphKind.People),
                        ActionButton("Local", "rc-action-replay", "Replay the selected recording on this instance only.",
                            isPlaying, () => _controller.PrepareAndStartLocalReplay(), GlyphKind.Play, GlyphKind.Person)));
                    VisualElement replayFile = BuildPlaybackFileField("Selected Replay File");
                    replayFile.AddToClassList("rc-controls-replay-file");
                    _controls.Add(replayFile);
                    break;

                case State.Recording:
                    bool localRecording = state.localRecording;
                    _controls.Add(ActionRow("Record",
                        ActionButton("Stop recording", "rc-action-stop", "End the current recording.", true, () =>
                        {
                            if (_controller.recorderState != null && _controller.recorderState.localRecording)
                                _controller.EndRecording();
                            else
                                _controller.SendEndRecordingEvent();
                        }, GlyphKind.Stop, localRecording ? GlyphKind.Person : GlyphKind.People)));
                    break;

                case State.Replaying:
                    GlyphKind audience = _controller.localPlayback ? GlyphKind.Person : GlyphKind.People;
                    bool paused = state.replayPaused;
                    _controls.Add(ActionRow("Replay",
                        PlayPauseButton(paused, _controller.localPlayback, audience),
                        ActionButton("Stop", "rc-action-stop", "End the current replay.", true, () =>
                        {
                            if (_controller.localPlayback)
                                _controller.EndReplay();
                            else
                                _controller.SendEndReplayEvent();
                        }, GlyphKind.Stop, audience)));
                    break;

                case State.PreparingReplay:
                    _controls.Add(ActionRow("Replay",
                        ActionButton("Cancel", "rc-action-stop", "Cancelling download & playback is not implemented yet.", false, null,
                            GlyphKind.Stop)));
                    break;

                default:
                    Label preparing = new Label(StateDisplayName(current) + "...");
                    preparing.AddToClassList("rc-hint");
                    _controls.Add(preparing);
                    break;
            }

            if (!isPlaying)
            {
                Label hint = new Label("Enter Play Mode to start a recording or replay.");
                hint.AddToClassList("rc-hint");
                _controls.Add(hint);
            }
        }

        // Local replay: RecorderController.PlaybackPause/PlaybackResume (this peer only).
        // Distributed replay: RecorderController.GlobalPlaybackPause/GlobalPlaybackResume (all peers).
        private Button PlayPauseButton(bool paused, bool local, GlyphKind audience)
        {
            if (paused)
                return ActionButton("Play", "rc-action-replay",
                    local ? "Resume the replay." : "Resume the replay on all peers.", true,
                    () =>
                    {
                        if (local) _controller.PlaybackResume();
                        else _controller.GlobalPlaybackResume();
                    }, GlyphKind.Play, audience);

            return ActionButton("Pause", "rc-action-replay",
                local ? "Pause the replay." : "Pause the replay on all peers.", true,
                () =>
                {
                    if (local) _controller.PlaybackPause();
                    else _controller.GlobalPlaybackPause();
                }, GlyphKind.Pause, audience);
        }

        private static VisualElement ActionRow(string caption, params Button[] buttons)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("rc-action-row");
            Label label = new Label(caption);
            label.AddToClassList("rc-action-caption");
            row.Add(label);
            foreach (Button button in buttons)
                row.Add(button);
            return row;
        }

        private static Button ActionButton(string text, string styleClass, string tooltip, bool enabled,
            System.Action onClick, params GlyphKind[] glyphs)
        {
            Button button = new Button(() => onClick?.Invoke()) { tooltip = tooltip };
            button.AddToClassList("rc-action");
            button.AddToClassList(styleClass);

            VisualElement icons = new VisualElement();
            icons.AddToClassList("rc-action-icons");
            foreach (GlyphKind glyph in glyphs)
                icons.Add(new Glyph(glyph));
            button.Add(icons);

            Label label = new Label(text);
            label.AddToClassList("rc-action-label");
            button.Add(label);

            button.SetEnabled(enabled && onClick != null);
            return button;
        }

        #region Vector icons

        private enum GlyphKind { Record, Play, Pause, Stop, Person, People }

        /// <summary>
        /// Small vector icon drawn with Painter2D, so it stays crisp at any display scale and takes its
        /// colour from USS (the element's `color`).
        /// </summary>
        private sealed class Glyph : VisualElement
        {
            private readonly GlyphKind _kind;

            public Glyph(GlyphKind kind)
            {
                _kind = kind;
                pickingMode = PickingMode.Ignore;
                AddToClassList("rc-glyph");
                AddToClassList("rc-glyph-" + kind.ToString().ToLowerInvariant());
                generateVisualContent += Draw;
                RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 0f || rect.height <= 0f)
                    return;

                Painter2D painter = context.painter2D;
                Color color = resolvedStyle.color;
                Vector2 center = rect.center;
                float size = Mathf.Min(rect.width, rect.height);

                switch (_kind)
                {
                    case GlyphKind.Record:
                        Circle(painter, color, center, size * 0.42f);
                        break;

                    case GlyphKind.Play:
                        painter.fillColor = color;
                        painter.BeginPath();
                        painter.MoveTo(center + new Vector2(-size * 0.32f, -size * 0.42f));
                        painter.LineTo(center + new Vector2(size * 0.42f, 0f));
                        painter.LineTo(center + new Vector2(-size * 0.32f, size * 0.42f));
                        painter.ClosePath();
                        painter.Fill();
                        break;

                    case GlyphKind.Pause:
                        Box(painter, color, new Rect(center.x - size * 0.36f, center.y - size * 0.4f, size * 0.26f, size * 0.8f));
                        Box(painter, color, new Rect(center.x + size * 0.1f, center.y - size * 0.4f, size * 0.26f, size * 0.8f));
                        break;

                    case GlyphKind.Stop:
                        Box(painter, color, new Rect(center.x - size * 0.36f, center.y - size * 0.36f, size * 0.72f, size * 0.72f));
                        break;

                    case GlyphKind.Person:
                        Person(painter, color, center.x, rect.yMin, rect.height);
                        break;

                    case GlyphKind.People:
                        // Two people behind (dimmed), one in front.
                        Color back = new Color(color.r, color.g, color.b, color.a * 0.55f);
                        float backHeight = rect.height * 0.82f;
                        float offset = rect.height * 0.42f;
                        float backTop = rect.yMax - backHeight;
                        Person(painter, back, center.x - offset, backTop, backHeight);
                        Person(painter, back, center.x + offset, backTop, backHeight);
                        Person(painter, color, center.x, rect.yMin, rect.height);
                        break;
                }
            }

            private static void Circle(Painter2D painter, Color color, Vector2 center, float radius)
            {
                painter.fillColor = color;
                painter.BeginPath();
                painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
                painter.ClosePath();
                painter.Fill();
            }

            private static void Box(Painter2D painter, Color color, Rect rect)
            {
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Fill();
            }

            // Head circle + rounded shoulders, fitted into a box of the given height.
            private static void Person(Painter2D painter, Color color, float centerX, float top, float height)
            {
                float headRadius = height * 0.22f;
                Circle(painter, color, new Vector2(centerX, top + headRadius), headRadius);

                float shoulderRadius = height * 0.4f;
                Vector2 shoulderCenter = new Vector2(centerX, top + height);
                painter.fillColor = color;
                painter.BeginPath();
                painter.Arc(shoulderCenter, shoulderRadius, Angle.Degrees(180f), Angle.Degrees(360f));
                painter.ClosePath();
                painter.Fill();
            }
        }

        #endregion

        private static string StateDisplayName(State state)
        {
            switch (state)
            {
                case State.Idle: return "Idle";
                case State.Recording: return "Recording";
                case State.Replaying: return "Replaying";
                case State.PreparingReplay: return "Preparing replay";
                case State.PrepareRecording: return "Preparing recording";
                default: return state.ToString();
            }
        }

        private static string StateClass(State state) => "rc-state-" + state.ToString().ToLowerInvariant();

        private static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int minutes = (int)(seconds / 60f);
            return minutes.ToString("00") + ":" + (seconds - minutes * 60).ToString("00.0");
        }

        #endregion

        #region Custom entry builders

        // Dropdown for RecorderState.fixedPlaybackRecordingName, listing local .recordmeta files or the
        // server's replay list (RecorderState.useLocalReplayFiles), with a refresh button and a hint line.
        private VisualElement BuildPlaybackFileField(string label)
        {
            if (_stateSerializedObject == null || _stateSerializedObject.targetObject == null)
                return new HelpBox("No RecorderState on this GameObject.", HelpBoxMessageType.Warning);

            SerializedProperty property = _stateSerializedObject.FindProperty("fixedPlaybackRecordingName");
            SerializedProperty useLocal = _stateSerializedObject.FindProperty("useLocalReplayFiles");

            VisualElement container = new VisualElement();
            container.AddToClassList("rc-playback-file");

            VisualElement row = new VisualElement();
            row.AddToClassList("rc-row");
            DropdownField dropdown = label != null ? new DropdownField(label) : new DropdownField();
            dropdown.tooltip = "The recording that is played back by the Replay buttons.";
            dropdown.AddToClassList("rc-playback-dropdown");
            if (label != null)
                dropdown.AddToClassList(AlignedFieldClass);
            row.Add(dropdown);

            Button refresh = new Button { tooltip = "Rescan the recordings" };
            refresh.AddToClassList("rc-icon-button");
            AddIconOrText(refresh, "R", "Refresh");
            row.Add(refresh);
            container.Add(row);

            Label hint = new Label();
            hint.AddToClassList("rc-hint");
            hint.AddToClassList("rc-playback-hint");
            container.Add(hint);

            string lastSignature = null;
            void Reload()
            {
                if (_stateSerializedObject.targetObject == null)
                    return;
                _stateSerializedObject.Update();
                List<string> options = GetReplayFileOptions(out string source);
                string current = property.stringValue;

                // Same rule as the old RecorderState inspector: pick the first recording if none is set.
                if (string.IsNullOrEmpty(current) && options.Count > 0)
                {
                    property.stringValue = options[0];
                    _stateSerializedObject.ApplyModifiedProperties();
                    current = options[0];
                }

                string signature = current + "|" + source + "|" + string.Join("|", options);
                if (signature == lastSignature)
                    return;
                lastSignature = signature;

                List<string> choices = new List<string>(options);
                bool missing = !string.IsNullOrEmpty(current) && !options.Contains(current);
                if (missing)
                    choices.Insert(0, current);
                dropdown.choices = choices;
                dropdown.SetValueWithoutNotify(string.IsNullOrEmpty(current) ? "(none)" : current);

                if (options.Count == 0)
                    hint.text = "No recordings found " + source + ".";
                else if (missing)
                    hint.text = "'" + current + "' is not among the recordings " + source + ".";
                else
                    hint.text = "";
                hint.style.display = string.IsNullOrEmpty(hint.text) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (string.IsNullOrEmpty(evt.newValue) || evt.newValue == "(none)")
                    return;
                _stateSerializedObject.Update();
                property.stringValue = evt.newValue;
                _stateSerializedObject.ApplyModifiedProperties();
                Reload();
            });
            refresh.clicked += () =>
            {
                lastSignature = null;
                Reload();
            };

            // Follow undo/redo and changes made elsewhere (e.g. the playback source toggle).
            container.TrackPropertyValue(property, _ => Reload());
            container.TrackPropertyValue(useLocal, _ =>
            {
                lastSignature = null;
                Reload();
            });

            container.RegisterCallback<AttachToPanelEvent>(_ => _playbackFileReloaders.Add(Reload));
            container.RegisterCallback<DetachFromPanelEvent>(_ => _playbackFileReloaders.Remove(Reload));
            Reload();
            return container;
        }

        // Local: *.recordmeta files in the recording directory (persistent data path if none is set).
        // Server: the replay list the RecorderState received at runtime.
        private List<string> GetReplayFileOptions(out string source)
        {
            RecorderState state = _controller.recorderState;
            if (state == null)
            {
                source = "(no RecorderState)";
                return new List<string>();
            }

            if (state.useLocalReplayFiles)
            {
                string directory = !string.IsNullOrEmpty(state.recordingDirectory) && Directory.Exists(state.recordingDirectory)
                    ? state.recordingDirectory
                    : Application.persistentDataPath;
                source = "in " + directory;
                if (!Directory.Exists(directory))
                    return new List<string>();
                return Directory.GetFiles(directory, "*.recordmeta")
                    .Select(Path.GetFileNameWithoutExtension)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            source = "in the server's replay list (filled at runtime)";
            return state.replayList?.replayNames?.Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>();
        }

        private VisualElement BuildMaxSyncTimeField()
        {
            if (_networkSerializedObject == null)
                return new HelpBox("No NetworkController on this GameObject.", HelpBoxMessageType.Info);

            SerializedProperty property = _networkSerializedObject.FindProperty("maxSynchronizationTimeMS");
            if (property == null)
                return new HelpBox("NetworkController.maxSynchronizationTimeMS was not found.", HelpBoxMessageType.Warning);

            FloatField field = new FloatField("Max synchronisation time (ms)")
            {
                tooltip = "Delay added to distributed recording and replay commands so clients can schedule them against synchronized network time. Stored on the NetworkController."
            };
            field.AddToClassList(AlignedFieldClass);
            field.BindProperty(property);
            return field;
        }

        private VisualElement BuildDownloadZipBlock()
        {
            VisualElement block = new VisualElement();
            block.AddToClassList("rc-block");

            TextField password = new TextField("Download password") { isPasswordField = true, value = _downloadPassword };
            password.AddToClassList(AlignedFieldClass);
            password.RegisterValueChangedCallback(evt =>
            {
                _downloadPassword = evt.newValue;
                Refresh();
            });
            block.Add(password);

            Button download = InlineButton("Download project zip",
                "Downloads all recordings of the project (RecorderState project name) from the selected server.",
                DownloadProjectZip);
            download.AddToClassList(DownloadButtonClass);
            block.Add(download);

            Label hint = new Label();
            hint.AddToClassList("rc-hint");
            hint.AddToClassList(DownloadHintClass);
            block.Add(hint);

            return block;
        }

        private static Button InlineButton(string text, string tooltip, System.Action onClick)
        {
            Button button = new Button(onClick) { text = text, tooltip = tooltip };
            button.AddToClassList("rc-inline-button");
            return button;
        }

        #endregion

        #region Small UI helpers

        private static VisualElement Spacer()
        {
            VisualElement spacer = new VisualElement();
            spacer.AddToClassList("rc-spacer");
            return spacer;
        }

        private static VisualElement Badge(Category category, string sizeClass)
        {
            VisualElement badge = new VisualElement();
            badge.AddToClassList("rc-badge-base");
            badge.AddToClassList(sizeClass);

            Texture icon = Icon(category.IconNames);
            if (icon != null)
            {
                Image image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("rc-badge-icon");
                badge.Add(image);
            }
            else
            {
                Label letter = new Label(category.Title.Substring(0, 1));
                letter.AddToClassList("rc-badge-letter");
                badge.Add(letter);
            }

            return badge;
        }

        private static void AddIconOrText(VisualElement target, string fallbackText, params string[] iconNames)
        {
            Texture icon = Icon(iconNames);
            if (icon != null)
            {
                Image image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("rc-button-icon");
                target.Add(image);
            }
            else if (target is TextElement textElement)
            {
                textElement.text = fallbackText;
            }
        }

        // Loads a built-in editor icon, trying the dark-skin variant first. Missing icon names are skipped
        // silently (IconContent would otherwise log a warning per missing name).
        private static Texture Icon(params string[] names)
        {
            if (names == null || names.Length == 0)
                return null;

            string key = string.Join("|", names) + (EditorGUIUtility.isProSkin ? "|dark" : "|light");
            if (IconCache.TryGetValue(key, out Texture cached))
                return cached;

            Texture found = null;
            bool logWasEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            try
            {
                foreach (string name in names)
                {
                    if (EditorGUIUtility.isProSkin && !name.StartsWith("d_"))
                    {
                        found = EditorGUIUtility.IconContent("d_" + name)?.image;
                        if (found != null)
                            break;
                    }

                    found = EditorGUIUtility.IconContent(name)?.image;
                    if (found != null)
                        break;
                }
            }
            finally
            {
                Debug.unityLogger.logEnabled = logWasEnabled;
            }

            IconCache[key] = found;
            return found;
        }

        private static Texture2D LoadEditorImage(string path, string searchName)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
                return texture;

            // Fallback in case the package is referenced under a different path.
            string guid = AssetDatabase.FindAssets(searchName + " t:Texture2D").FirstOrDefault();
            return guid != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        }

        private static StyleSheet LoadStyleSheet()
        {
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (sheet != null)
                return sheet;

            // Fallback in case the package is referenced under a different path.
            string guid = AssetDatabase.FindAssets("RecorderControllerEditor t:StyleSheet").FirstOrDefault();
            return guid != null ? AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        }

        #endregion

        #region Actions (unchanged behaviour from the previous IMGUI inspector)

        private void OpenRecordingDirectoryFromState()
        {
            RecorderState state = _controller.recorderState;
            if (state == null || string.IsNullOrEmpty(state.recordingDirectory))
                OpenRecordingDirectory(Application.persistentDataPath);
            else
                OpenRecordingDirectory(state.recordingDirectory);
        }

        private void DownloadProjectZip()
        {
            RecorderState state = _controller.recorderState;
            if (state == null)
                return;

            string projectName = state.projectName;
            string serverAddress = state.selectedServer;
            string saveDir = string.IsNullOrEmpty(state.recordingDirectory)
                ? Application.persistentDataPath
                : state.recordingDirectory;
            string savePath = Path.Combine(saveDir, projectName + ".zip");

            _controller.StartCoroutine(NetworkUtils.DownloadProjectZip(
                projectName,
                serverAddress,
                _downloadPassword,
                savePath,
                (ok, msg) =>
                {
                    if (ok) Debug.Log("Download saved to: " + msg);
                    else Debug.LogError("Download failed: " + msg);
                }));
        }

        private void OpenRecordingDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (Directory.Exists(path))
                {
                    EditorUtility.RevealInFinder(path);
                }
                else
                {
                    Debug.LogError("The specified recording directory does not exist: " + path);
                }
            }
            else
            {
                Debug.LogError("No recording directory is specified.");
            }
        }

        private void AddPrefabInformationToAllNetworkPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:NetworkPrefabsList");

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                NetworkPrefabsList prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);

                if (prefabsList == null)
                    continue;

                foreach (var networkPrefab in prefabsList.PrefabList)
                {
                    if (networkPrefab.Prefab == null)
                        continue;

                    GameObject go = networkPrefab.Prefab;
                    string assetPath = AssetDatabase.GetAssetPath(go);
                    if (IsInReadOnlyPackage(assetPath))
                    {
                        Debug.LogWarning("Skipped '" + assetPath + "': prefabs in an installed package are read-only, " +
                                         "so RecordingPrefabInformation cannot be added. Create a prefab variant in a " +
                                         "Resources folder under Assets, use the variant instead (spawner and " +
                                         "NetworkPrefabsList), and run this again.", go);
                        continue;
                    }

                    string addressableKey = ResolveAddressableKey(assetPath);

                    RecordingPrefabInformation[] oldPrefabInformation = go.GetComponents<RecordingPrefabInformation>();
                    if (oldPrefabInformation.Length > 0)
                    {
                        foreach (var information in oldPrefabInformation)
                        {
                            information.Setup(assetPath, addressableKey);
                        }
                    }
                    else
                    {
                        RecordingPrefabInformation recordingPrefabInformation = go.AddComponent<RecordingPrefabInformation>();
                        if (recordingPrefabInformation != null)
                            recordingPrefabInformation.Setup(assetPath, addressableKey);
                    }

                    PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                }
            }
        }

        private void AddPrefabInformationToAllPrefabs()
        {
            // Restrict the search to the Assets directory (not Packages) and to actual prefab files.
            // "t:GameObject" also matches imported models (e.g. .fbx), which are read-only and cannot
            // take components, so filter by the .prefab extension.
            string[] assetGUIDs = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" });
            Debug.Log("searching in " + assetGUIDs.Length + " gameobjects...");

            foreach (var guid in assetGUIDs)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (go == null)
                    continue;

                string addressableKey = ResolveAddressableKey(assetPath);

                RecordingPrefabInformation[] oldPrefabInformation = go.GetComponents<RecordingPrefabInformation>();
                if (oldPrefabInformation.Length > 0)
                {
                    foreach (var information in oldPrefabInformation)
                    {
                        information.Setup(assetPath, addressableKey);
                    }
                }
                else
                {
                    RecordingPrefabInformation recordingPrefabInformation = go.AddComponent<RecordingPrefabInformation>();
                    if (recordingPrefabInformation != null)
                    {
                        recordingPrefabInformation.Setup(assetPath, addressableKey);
                    }
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            }
        }

        // Prefabs of packages installed from a registry, Git or a tarball live in the read-only package cache;
        // a component added to them is never saved. Embedded and local packages can be edited.
        private static bool IsInReadOnlyPackage(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Packages/", StringComparison.Ordinal))
                return false;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
            return package != null && package.source != UnityEditor.PackageManager.PackageSource.Embedded &&
                   package.source != UnityEditor.PackageManager.PackageSource.Local;
        }

        // Returns the Addressables address of the asset at the given path, or "" if the asset is not
        // marked Addressable or the Addressables package is not installed. Resolved via reflection so the
        // recording package keeps zero hard dependency on com.unity.addressables; it activates
        // automatically once Addressables is present in the project.
        private static string ResolveAddressableKey(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return "";

            Type defaultObjectType = Type.GetType(
                "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
            if (defaultObjectType == null)
                return ""; // Addressables editor assembly not present

            object settings = defaultObjectType
                .GetProperty("Settings", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                ?.GetValue(null);
            if (settings == null)
                return ""; // Addressables not initialised in this project

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                return "";

            object entry = settings.GetType()
                .GetMethod("FindAssetEntry", new[] { typeof(string) })
                ?.Invoke(settings, new object[] { guid });
            if (entry == null)
                return ""; // asset is not marked Addressable

            return entry.GetType().GetProperty("address")?.GetValue(entry) as string ?? "";
        }

        #endregion
    }
#endif
}
