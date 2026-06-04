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

                    OdinHandler.Instance.Microphone.OnMicrophoneData += ForwardAudioData;
                    
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