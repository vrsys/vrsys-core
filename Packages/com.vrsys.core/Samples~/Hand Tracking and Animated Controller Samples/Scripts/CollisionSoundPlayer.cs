using Unity.Netcode;
using UnityEngine;

namespace VRSYS.Core.Samples
{
    [RequireComponent(typeof(AudioSource))]
    public class CollisionSoundPlayer : NetworkBehaviour
    {
        #region Properties

        private AudioSource _audioSource;
        [SerializeField] private AudioClip _collisionSound;

        #endregion

        #region Mono- & NetworkBehaviour Methods

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        private void OnCollisionEnter(Collision other)
        {
            if (IsServer)
                PlaySoundRpc();
        }

        #endregion

        #region RPCs

        [Rpc(SendTo.Everyone)]
        private void PlaySoundRpc()
        {
            _audioSource.clip = _collisionSound;
            _audioSource.Play();
        }

        #endregion
    }
}
