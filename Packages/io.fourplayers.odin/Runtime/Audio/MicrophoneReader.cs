using OdinNative.Core;
using OdinNative.Odin;
using OdinNative.Odin.Media;
using OdinNative.Odin.Room;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if PLATFORM_ANDROID || UNITY_ANDROID
using UnityEngine.Android;
#endif
#if PLATFORM_IOS || UNITY_IOS
using UnityEngine.iOS;
#endif

namespace OdinNative.Unity.Audio
{
    /// <summary>
    /// Handles microphone input data and sends input to ODIN
    /// </summary>
    [DisallowMultipleComponent]
    public class MicrophoneReader : MonoBehaviour
    {
#if PLATFORM_ANDROID || UNITY_ANDROID
        /// <summary>
        /// Check if the user has authorized use of the microphone
        /// </summary>
        /// <remarks>Andriod 6+ with <see cref="UnityEngine.Android.Permission.Microphone"/> <see href="https://docs.unity3d.com/ScriptReference/Android.Permission.Microphone.html">(Permission)</see></remarks>
        public bool HasPermission => Permission.HasUserAuthorizedPermission(Permission.Microphone);
#else
        /// <summary>
        /// Check if the user has authorized use of the microphone
        /// </summary>
        /// <remarks>In other build targets except for Web Player, this function will always return true.</remarks>
        public bool HasPermission => Application.HasUserAuthorization(UserAuthorization.Microphone);
#endif
        private bool InitialPermission;

        /// <summary>
        /// Skips registered PushAudio for the OnMicrophoneData event
        /// </summary>
        /// <remarks>This will stop progress of time in the audio pipeline and can result in missing event updates like OnMediaActiveStateChanged. Should only be disabled if handled.</remarks>
        [Tooltip("Redirect the captured audio to all rooms.")]
        [SerializeField]
        public bool RedirectCapturedAudio = true;
        /// <summary>
        /// Zero out the event audio buffer for PushAudio.
        /// </summary>
        [Tooltip("Silence the captured audio to all rooms.")]
        [SerializeField]
        public bool SilenceCapturedAudio = false;

        [Tooltip("Indicates whether the recording should continue recording if AudioClipLength is reached, and wrap around and record from the beginning of the AudioClip.")]
        [SerializeField]
        public bool ContinueRecording = true;
        [Header("AudioClip Settings")]
        [Tooltip("Is the length of the AudioClip produced by the recording.")]
        [Range(1, 60)]
        [SerializeField]
        public int AudioClipLength = 1;
        /// <summary>
        /// Use set <see cref="SampleRate"/> on true, <see cref="OdinEditorConfig.DeviceSampleRate"/> on false
        /// </summary>
        [SerializeField]
        public bool OverrideSampleRate;
        /// <summary>
        /// The recording <see cref="OdinNative.Core.MediaSampleRate"/>
        /// </summary>
        /// <remarks>Set value by <see cref="OdinEditorConfig.DeviceSampleRate"/> on false. 
        /// For <see cref="UnityEngine.Microphone.Start"/> <see href="https://docs.unity3d.com/ScriptReference/Microphone.Start.html">(Microphone.Start)</see>
        /// creation of <see cref="UnityEngine.AudioClip"/> <see href="https://docs.unity3d.com/ScriptReference/AudioClip.html">(AudioClip)</see> if <see cref="OverrideSampleRate"/></remarks>
        [SerializeField]
        public MediaSampleRate SampleRate;

        private bool IsInputDeviceConnected;
        private int InputMinFreq;
        private int InputMaxFreq;
        /// <summary>
        /// Enable/Disable the use of <see cref="OdinNative.Unity.Audio.MicrophoneReader.InputDevice"/> as a new/fixed device name
        /// </summary>
        public bool CustomInputDevice;
        /// <summary>
        /// The device name to use as microphone in Unity. (i.e <see href="https://docs.unity3d.com/ScriptReference/Microphone.Start.html">Microphone.Start</see>)
        /// </summary>
        /// <remarks>If you pass a null or empty string for the device name then the default microphone will be used. You can get a list of available microphone devices from the devices property. (see <see href="https://docs.unity3d.com/ScriptReference/Microphone-devices.html">Microphone.devices</see>)</remarks>
        public string InputDevice;
        private AudioClip InputClip;
        private bool IsFirstStartGlobal;
        internal bool IsStreaming;

        /// <summary>
        /// Use <see cref="UnityEngine.Microphone.Start"/> <see href="https://docs.unity3d.com/ScriptReference/Microphone.Start.html">(Microphone.Start)</see> in <see cref="Start"/>
        /// </summary>
        [SerializeField]
        [Tooltip("Automatical microphone start on Start()")]
        public bool AutostartListen = true;

        public bool CustomMicVolumeScale = false;
        [SerializeField]
        [Tooltip("Automatical microphone volume boost")]
        public float MicVolumeScale = 1f;

        /// <summary>
        /// Create and play <see cref="UnityEngine.AudioSource"/> <see href="https://docs.unity3d.com/ScriptReference/AudioSource.html">(AudioSource)</see>
        /// with a Microphone <see cref="UnityEngine.AudioClip"/> <see href="https://docs.unity3d.com/ScriptReference/AudioClip.html">(AudioClip)</see> on loop.
        /// </summary>
        /// <remarks>Needs <see href="https://docs.unity3d.com/Manual/PlatformSpecific.html">Platform specific Permissions</see> i.e Microphone to work.</remarks>
        [Header("Microphone Test")]
        [Tooltip("Start/Stop Audio-Loopback")]
        [SerializeField]
        public bool Loopback = false;
        private AudioSource LoopSource;

        void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged += AudioSettings_OnAudioConfigurationChanged;
            if (InputClip != null && Microphone.IsRecording(InputDevice)) IsStreaming = true;

            OnMicrophoneData += PushAudio;
        }

        void Reset()
        {
            RedirectCapturedAudio = true;
            SilenceCapturedAudio = false;

            ContinueRecording = true;
            AudioClipLength = 3;

            OverrideSampleRate = false;
            SampleRate = OdinHandler.Config.DeviceSampleRate;

            CustomMicVolumeScale = false;
            MicVolumeScale = 1f;

            Loopback = false;
            AutostartListen = true;
        }

        void Start()
        {
            SetupMicrophoneReader();
        }

        private void SetupMicrophoneReader()
        {
            IsStreaming = false;
            IsFirstStartGlobal = true;
            InputClip = null;
            IsInputDeviceConnected = false;
            SetupBuffers();
            SetupMicrophone(InputDevice);
            if (Microphone.IsRecording(InputDevice)) IsFirstStartGlobal = false;
            if (HasPermission)
            {
                InitialPermission = HasPermission; // Override because we should not need the check this lifetime anymore. 
                if (AutostartListen) StartListen();
            }
        }

        private string SetupMicrophone(string customDevice = "")
        {
            if (OdinHandler.Config.Verbose)
                Debug.Log($"User has authorization of Microphone: {HasPermission}");

            InputDevice = CustomInputDevice ? customDevice : Microphone.devices.FirstOrDefault();

            if (string.IsNullOrEmpty(InputDevice) && CustomInputDevice == false || Microphone.devices.Length <= 0)
            {
                IsInputDeviceConnected = false;
                Debug.LogWarning($"{nameof(MicrophoneReader)} no Microphone.devices found.");
            }
            else
            {
                IsInputDeviceConnected = true;
                if (string.IsNullOrEmpty(InputDevice))
                    Debug.LogWarning($"{nameof(MicrophoneReader)} setup unknown system default device.");
                else
                    Debug.Log($"{nameof(MicrophoneReader)} setup device \"{InputDevice}\".");
            }

            if (IsInputDeviceConnected == false) return string.Empty;

            Microphone.GetDeviceCaps(InputDevice, out InputMinFreq, out InputMaxFreq);

            if (OverrideSampleRate == false)
            {
                // There is no real reason to switch sample rate based on device capability. The Sample Rate chosen here
                // will be used for the sample rate of the AudioClip produced by the recording. If this is chosen
                // to be the required sampling rate, we don't need to do resampling. It's done automatically by Unity then.
                SampleRate = OdinHandler.Config.RemoteSampleRate;
            }

            return InputDevice;
        }

        /// <summary>
        /// Start Unity microphone capture
        /// </summary>
        /// <remarks>if "Autostart Listen" in Editor component is true, the capture will be called in Unity-Start(void).</remarks>
        public bool StartListen()
        {
            if (IsInputDeviceConnected == false) return false;

            if (OdinHandler.Config.Verbose)
                Debug.Log($"Microphone start \"{InputDevice}\", Loop:{ContinueRecording}, {AudioClipLength}s, {(int)SampleRate}Hz");

            InputClip = Microphone.Start(InputDevice, ContinueRecording, AudioClipLength, ((int)SampleRate));
            RBuffer.MicPosition = Microphone.GetPosition(InputDevice);
            return IsStreaming = InputClip != null;
        }

        private void PushAudio(float[] buffer, int position)
        {
            if (RedirectCapturedAudio == false) return;

            if (CustomMicVolumeScale)
            {
                float bufferScale = GetVolumeScale(MicVolumeScale);
                SetVolume(ref buffer, bufferScale);
            }

            if (OdinHandler.Instance?.Client?.Rooms == null)
            {
                Debug.LogError("Odin hot-reload needs new MicrophoneStream for Microphone Reader component to push audio.");
                this.enabled = false;
                return; 
            }

            if(SilenceCapturedAudio)
                for(int i = 0; i < buffer.Length; i++)
                    buffer[i] = 0f;

            foreach (Room room in OdinHandler.Instance.Client.Rooms)
            {
                if (room?.MicrophoneMedia != null)
                    room.MicrophoneMedia.AudioPushData(buffer);
                else if (room.IsJoined && OdinHandler.Config.VerboseDebug)
                    Debug.LogWarning($"Room {room.Config.Name} is missing a microphone stream. See Room.CreateMicrophoneMedia");
            }
        }

        float GetVolumeScale(float value)
        {
            return Mathf.Pow(value, 3);
        }

        void SetVolume(ref float[] buffer, float scale)
        {
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] *= scale;
        }

        float GetAveragedVolume(float[] buffer)
        {
            float avg = 0;
            foreach (float s in buffer)
            {
                avg += Mathf.Abs(s);
            }
            return avg / buffer.Length * 100f;
        }

        void Update()
        {
            if (IsInputDeviceConnected == false) return;

            if (InitialPermission == false)
            {
                /* If the app targets Android 11 or higher and isn't used for a few months,
                 * the system protects user data by automatically resetting the sensitive runtime permissions
                 * that the user had granted.*/
                if (HasPermission)
                {
                    InitialPermission = HasPermission; // Override because we should not need the check this lifetime anymore. 
                    ResetDevice(InputDevice);
                    return;
                }
                /* if the user taps Deny for a specific permission more than once during the app's lifetime on a device,
                 * the user doesn't see the system permissions dialog even if the app requests that permission again.
                 * The user's action implies "don't ask again."*/
                else return;
            }

            Flush();
            //TestLoopback();
        }

        private void AudioSettings_OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (deviceWasChanged && isActiveAndEnabled)
                ResetDevice(InputDevice);
        }

        internal void ResetDevice(string deviceName)
        {
            if (IsStreaming && Microphone.IsRecording(deviceName))
                Microphone.End(deviceName);
            
            foreach (Room room in OdinHandler.Instance.Rooms)
            {
                MicrophoneStream currentStream = room.MicrophoneMedia;
                if (null != currentStream)
                {
                    OdinMediaConfig currentStreamMediaConfig = currentStream.MediaConfig;
                    currentStream.Dispose();
                    room.CreateMicrophoneMedia(currentStreamMediaConfig);
                }
            }
            
            SetupMicrophoneReader();
        }
        

        /// <summary>
        /// Stop Unity Microphone capture if this AudioSender created the recording
        /// </summary>
        public void StopListen()
        {
            // Stops the device only if this Sender started the recording
            if (IsFirstStartGlobal && Microphone.IsRecording(InputDevice))
                Microphone.End(InputDevice);

            IsStreaming = false;
        }

        void OnDisable()
        {
            IsStreaming = false;
            OnMicrophoneData -= PushAudio;
            AudioSettings.OnAudioConfigurationChanged -= AudioSettings_OnAudioConfigurationChanged;
        }

        void OnDestroy()
        {
            StopListen();
        }

        #region Buffer
        public delegate void MicrophoneCallbackDelegate(float[] buffer, int position);
        /// <summary>
        /// Event is fired if raw microphone data is available
        /// </summary>
        public MicrophoneCallbackDelegate OnMicrophoneData;

        private class RBuffer
        {
            public static int MicPosition = 0;

            public const int sizesMin = 10;
            public const int sizesMax = 11;

            const int redundancy = 8; // times 8 ea buffer size to cycle
            int index = 0;

            float[][] internalBuffers = new float[redundancy][];

            public float[] buffer
            {
                get
                {
                    return internalBuffers[index];
                }
            }

            public void Cycle()
            {
                index = (index + 1) % redundancy;
            }

            public RBuffer(int size)
            {
                for (int i = 0; i < redundancy; i++)
                {
                    internalBuffers[i] = new float[1 << size];
                }
            }
        }

        RBuffer[] MicBuffers = new RBuffer[RBuffer.sizesMax + 1];

        void SetupBuffers()
        {
            RBuffer.MicPosition = 0;
            MicBuffers = new RBuffer[RBuffer.sizesMax + 1];
            for (int i = RBuffer.sizesMin; i <= RBuffer.sizesMax; i++)
                MicBuffers[i] = new RBuffer(i);
        }

        void Flush()
        {
            // initialization failure
            if (MicBuffers == null || MicBuffers.All(b => b == null))
            {
                Debug.LogError("Odin MicBuffer corrupted. Try restart!");
                SetupMicrophoneReader();
                return;
            }
            // no running devices or manually disabled
            if (IsStreaming == false || isActiveAndEnabled == false) return;
            // on failure, Microphone.Start should return null not an AudioClip without samples
            if (InputClip == null || InputClip.samples == 0) return;

            int newPosition = Microphone.GetPosition(InputDevice);
            // device is not recording or buffer got collected
            if (RBuffer.MicPosition == newPosition || MicBuffers == null) return;

            // give a sample on start ( S + 1 - 0 ) % S = 1 and give a sample at the end ( S + 0 - 99 ) % S = 1
            int dataToRead = (InputClip.samples + newPosition - RBuffer.MicPosition) % InputClip.samples;
            for (int i = RBuffer.sizesMax; i >= RBuffer.sizesMin; i--)
            {
                RBuffer mic = MicBuffers[i];
                int n = mic.buffer.Length; // 1 << i;

                while (dataToRead >= n)
                {
                    // If the read length from the offset is longer than the clip length,
                    // the read will wrap around and read the remaining samples from the start of the clip.
                    InputClip.GetData(mic.buffer, RBuffer.MicPosition);
                    RBuffer.MicPosition = (RBuffer.MicPosition + n) % InputClip.samples;
                    OnMicrophoneData?.Invoke(mic.buffer, RBuffer.MicPosition);

                    mic.Cycle();
                    dataToRead -= n;
                }
            }
        }
        #endregion Buffer

        #region Test
        private void TestLoopback()
        {
            if (LoopSource == null && Loopback)
                LoopSource = gameObject.AddComponent<AudioSource>();
            else if (Loopback == false)
            {
                if (LoopSource != null)
                {
                    if (IsStreaming) StopListen();
                    Destroy(LoopSource);
                    LoopSource = null;
                }
                return;
            }
            else if (LoopSource && LoopSource.isPlaying && IsStreaming)
                return;

            if (IsStreaming == false && StartListen() == false) return;

            Debug.Log("Set LoopSource...");
            LoopSource.clip = InputClip;
            LoopSource.loop = ContinueRecording;
            LoopSource.Play();
        }
        #endregion Test
    }
}
