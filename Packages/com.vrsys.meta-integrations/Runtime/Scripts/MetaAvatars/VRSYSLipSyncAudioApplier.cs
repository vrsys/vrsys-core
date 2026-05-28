using Oculus.Avatar2;
using UnityEngine;

namespace VRSYS.Meta.Avatars
{
    public class VRSYSLipSyncAudioApplier : MonoBehaviour
    {
        #region Properties

        [SerializeField] private OvrAvatarLipSyncContext _lipSyncContext;
        [SerializeField, Tooltip("Factor applied on audio amplitude to exaggerate lip movements.")] private float _scaleFactor = 1.6f;

        #endregion

        #region Public Methods

        public void ForwardAudioToLipSync(float[] buffer, int position)
        {
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] *= _scaleFactor;
            
            _lipSyncContext.ProcessAudioSamples(buffer, 1);
        }

        #endregion
    }
}
