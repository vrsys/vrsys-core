using System;
using System.Runtime.InteropServices;
using System.Text;
using AOT;
using Unity.Collections;
using UnityEngine;

namespace VRSYS.Scripts.Recording
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        None = 4
    };
    
    public class PluginConfigurator : MonoBehaviour
    {
        [DllImport("RecordingPlugin")]
        private static extern bool SetRecordingMaxBufferSize(int recorderId, int maxBufferSize);

        [DllImport("RecordingPlugin")]
        private static extern bool SetReplayBufferNumber(int recorderId, int bufferNumber);

        [DllImport("RecordingPlugin")]
        private static extern bool SetReplayBufferStoredTimeInterval(int recorderId, float timeInterval);

        [DllImport("RecordingPlugin")]
        private static extern bool SetSoundRecordingMaxBufferSize(int recorderId, int maxBufferSize);

        [DllImport("RecordingPlugin")]
        private static extern bool SetRecordingDirectory(int recorderId, string directory, int directoryLength);

        [DllImport("RecordingPlugin")]
        private static extern int GetVersionNumber(StringBuilder textBuilder, int maxSize);
        
        [DllImport("RecordingPlugin", CallingConvention = CallingConvention.Cdecl)]
        static extern void RegisterDebugCallback(debugCallback cb);

        [DllImport("RecordingPlugin")]
        private static extern bool SetLogLevel(int level);

        public int recordingMaxBufferSize = 10000;
        public int replayBufferNumber = 3;
        public float replayBufferTimeInterval = 10.0f;
        public int recordingSoundMaxBufferSize = 100;
        public LogLevel logLevel = LogLevel.Info;
        public string versionInfo = "No information";

        private LogLevel lastAppliedLogLevel;
        [HideInInspector] public string recordingDirectory = "";

        // Rooted reference to the delegate handed to native code. The native side
        // keeps only the raw function pointer in a global that survives Unity domain
        // reloads (exit play mode / script recompile); the managed delegate does not.
        // Without rooting it here AND clearing it around reloads, that global dangles
        // and the next plugin log call crashes ("UNKNOWN while executing native code").
        private static debugCallback _debugCallback;

        // Registers the native log callback and roots the delegate. Idempotent;
        // re-registers with a fresh delegate after a domain reload.
        private static void RegisterLogging()
        {
            _debugCallback = OnDebugCallback;
            RegisterDebugCallback(_debugCallback);
        }

        // Clears the native callback so its function pointer can never outlive this
        // managed domain. The native log path guards a null callback (falls back to
        // stdout), so this is always safe.
        private static void UnregisterLogging()
        {
            RegisterDebugCallback(null);
            _debugCallback = null;
        }

#if UNITY_EDITOR
        // Runs on every editor load, including after each domain reload, so the
        // edit-mode "create/replay recordings in the editor" buttons always have a
        // valid callback even though MonoBehaviour.Start() does not run in edit mode.
        // beforeAssemblyReload clears the native pointer before this domain is torn
        // down, closing the stale-pointer window across reloads.
        [UnityEditor.InitializeOnLoadMethod]
        private static void InitializeEditorLogging()
        {
            RegisterLogging();
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= UnregisterLogging;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += UnregisterLogging;
        }
#endif

        public void Start()
        {
            RegisterLogging();
            SetRecordingMaxBufferSize(0, recordingMaxBufferSize);
            SetSoundRecordingMaxBufferSize(0, recordingSoundMaxBufferSize);
            SetReplayBufferNumber(0, replayBufferNumber);
            SetReplayBufferStoredTimeInterval(0, replayBufferTimeInterval);

            int maxSize = 300;
            StringBuilder buffer = new StringBuilder(maxSize);
            int len = GetVersionNumber(buffer, buffer.Capacity);
            if (len > 0)
            {
                versionInfo = buffer.ToString();
            }

            SetLogLevel((int)logLevel);
            lastAppliedLogLevel = logLevel;
        }

        public void Update()
        {
            if (logLevel != lastAppliedLogLevel)
            {
                lastAppliedLogLevel = logLevel;
                SetLogLevel((int)logLevel);
            }
        }

        delegate void debugCallback(IntPtr request, int level, int size);

        
        [MonoPInvokeCallback(typeof(debugCallback))]
        static void OnDebugCallback(IntPtr request, int level, int size)
        {
            //Ptr to string
            string debug_string = Marshal.PtrToStringAnsi(request, size);
            
            if((LogLevel)level == LogLevel.Debug)
                Debug.Log(debug_string);
            else if ((LogLevel)level == LogLevel.Info)
                Debug.Log(debug_string);
            else if ((LogLevel)level == LogLevel.Warning)
                Debug.LogWarning(debug_string);
            else if ((LogLevel)level == LogLevel.Error)
                Debug.LogError(debug_string);
            else 
                Debug.LogAssertion(debug_string);
        }
    }
}