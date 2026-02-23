using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace VRSYS.Core.Samples
{
    public class SampleUIController : NetworkBehaviour
    {
        #region Properties

        [Header("UI Elements")] 
        [SerializeField] private Button _helloWorldButton;
        [SerializeField] private Button _spawnButton;

        [Header("Audio")] [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _helloWorldSound;
        [SerializeField] private AudioClip _clickSound;

        private InteractableObjectsSpawner _objectsSpawner;

        #endregion

        #region Mono- & NetworkBehaviour Methods

        private void Awake()
        {
            _helloWorldButton.onClick.AddListener(PlayHelloWorldRpc);
            
            _objectsSpawner = FindAnyObjectByType<InteractableObjectsSpawner>();
            _spawnButton.onClick.AddListener(PlayClickSoundRpc);
            _spawnButton.onClick.AddListener(_objectsSpawner.SpawnObjectRpc);
        }

        #endregion

        #region Private Methods

        private void PlaySound(AudioClip clip)
        {
            _audioSource.clip = clip;
            _audioSource.Play();
        }

        #endregion

        #region RPCs

        [Rpc(SendTo.Everyone)]
        private void PlayHelloWorldRpc() => PlaySound(_helloWorldSound);

        [Rpc(SendTo.Everyone)]
        private void PlayClickSoundRpc() => PlaySound(_clickSound);

        #endregion
    }
}
