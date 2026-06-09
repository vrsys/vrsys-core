using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Chat.Odin
{
    public class OdinMicrophoneDataAccessor : MonoBehaviour
    {
        #region Properties

        private bool _initialized = false;

        /// <summary>
        /// Capture sampling rate (Hz) of the ODIN microphone, as configured on
        /// <see cref="OdinHandler.Instance"/>.Microphone. Valid once initialized; 0 otherwise.
        /// </summary>
        public int SamplingRate { get; private set; }

        /// <summary>
        /// Channel count of the buffers delivered through <see cref="OnMicrophoneData"/>. ODIN capture is
        /// mono in practice, so this defaults to 1 and is refined from the input clip once available.
        /// </summary>
        public int Channels { get; private set; } = 1;

        #endregion

        #region Events

        public UnityEvent<float[], int> OnMicrophoneData = new ();

        #endregion
        
        #region MonoBehaviour Methods

        private void Start()
        {
            if (!GetComponentInParent<NetworkBehaviour>().IsOwner)
            {
                Destroy(this);
                return;
            }

            if (!_initialized)
                Initialize();
        }

        private void Update()
        {
            if(!_initialized)
                Initialize();
        }

        #endregion

        #region Private Methods

        private void Initialize()
        {
            if(OdinHandler.Instance != null)
                if (OdinHandler.Instance.Microphone != null)
                {
                    // OdinHandler.Instance.Microphone.OnMicrophoneData += (float[] buffer, int position) =>
                    //     OnMicrophoneData.Invoke(buffer, position);

                    var microphone = OdinHandler.Instance.Microphone;
                    SamplingRate = (int)microphone.SampleRate;
                    if (microphone.InputClip != null)
                        Channels = Mathf.Max(1, microphone.InputClip.channels);

                    microphone.OnMicrophoneData += ForwardAudioData;

                    ExtendedLogger.LogInfo(GetType().Name, "Initialized Odin microphone data accessor.", this);

                    _initialized = true;
                }
        }

        private void ForwardAudioData(float[] buffer, int position)
        {
            OnMicrophoneData.Invoke(buffer, position);
        }

        #endregion
    }
}