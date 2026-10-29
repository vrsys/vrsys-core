using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace VRSYS.Recording.Testing
{
    /// <summary>
    /// Gives a networked instance the same name on every peer. Netcode names client instances "&lt;prefab&gt;(Clone)",
    /// but recordings identify objects by their hierarchy path, so every peer has to use the name the server gave
    /// the instance (e.g. "Networked Spawned Sphere 3" from <see cref="RecordingTestSpawner"/>, or a replay copy's
    /// "...[Rec&lt;id&gt;]" name). The server publishes its object's name and keeps it current; clients apply it.
    ///
    /// The server's spawner tells its own recorder about a new instance. Clients record their copy as well, so this
    /// component tells the client's recorder once the instance has its name.
    /// </summary>
    [AddComponentMenu("VRSYS/Recording/Testing/Recording Test Network Name")]
    [RequireComponent(typeof(NetworkObject))]
    public class RecordingTestNetworkName : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString128Bytes> _networkName = new NetworkVariable<FixedString128Bytes>();
        private string _publishedName;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Publish();
                return;
            }

            ApplyName(default, _networkName.Value);
            _networkName.OnValueChanged += ApplyName;
            SignalRecorder();
        }

        public override void OnNetworkDespawn()
        {
            _networkName.OnValueChanged -= ApplyName;
        }

        private void LateUpdate()
        {
            // The server may rename the instance after spawning it (e.g. replay copies); pass that on.
            if (IsSpawned && IsServer && gameObject.name != _publishedName)
                Publish();
        }

        private void Publish()
        {
            _publishedName = gameObject.name;
            _networkName.Value = new FixedString128Bytes(_publishedName);
        }

        private void ApplyName(FixedString128Bytes previous, FixedString128Bytes current)
        {
            if (current.Length > 0)
                gameObject.name = current.ToString();
        }

        private void SignalRecorder()
        {
            RecorderController controller = FindFirstObjectByType<RecorderController>();
            if (controller != null && controller.recorderState != null &&
                controller.recorderState.currentState == State.Recording)
                controller.StartCoroutine(controller.SignalObjectInstantiation(gameObject));
        }
    }
}
