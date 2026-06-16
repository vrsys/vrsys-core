using Oculus.Avatar2;
using UnityEngine;

namespace VRSYS.Meta.Avatars
{
    public class VRSYSLipSyncAudioApplier : MonoBehaviour
    {
        #region Properties

        [SerializeField] private OvrAvatarLipSyncContext _lipSyncContext;
        [SerializeField, Tooltip("Factor applied on audio amplitude to exaggerate lip movements.")] private float _scaleFactor = 1.6f;

        private float[] _scratch;

        #endregion

        #region Public Methods

        public void ForwardAudioToLipSync(float[] buffer, int position)
        {
            if (_lipSyncContext == null || !_lipSyncContext.isActiveAndEnabled)
                return;

            if (buffer == null || buffer.Length == 0)
                return;

            // Work on a copy: ProcessAudioSamples clears the buffer in place (CaptureAudio), and this is
            // ODIN's shared microphone buffer that other subscribers (e.g. the recorder) read afterwards.
            if (_scratch == null || _scratch.Length != buffer.Length)
                _scratch = new float[buffer.Length];

            for (int i = 0; i < buffer.Length; i++)
                _scratch[i] = buffer[i] * _scaleFactor;

            _lipSyncContext.ProcessAudioSamples(_scratch, 1);
        }

        #endregion
    }
}
