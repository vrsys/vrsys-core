using Unity.Netcode;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Utility
{
    public class RendererActivationSerializer : NetworkBehaviour
    {
        #region Member Variables

        private Renderer renderer;
        
        private NetworkVariable<bool> isActive = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        #endregion

        #region Mono- & NetworkBehaviour Callbacks

        public override void OnNetworkSpawn()
        {
            renderer = GetComponent<Renderer>();

            if (renderer == null)
            {
                ExtendedLogger.LogError(GetType().Name, "No renderer attached!", this);
                return;
            }

            if (IsOwner)
                isActive.Value = renderer.enabled;
            else
            {
                renderer.enabled = isActive.Value;
                isActive.OnValueChanged += OnIsActiveChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
                isActive.OnValueChanged -= OnIsActiveChanged;
        }

        private void Update()
        {
            if (!IsOwner)
                return;

            if (isActive.Value != renderer.enabled)
                isActive.Value = renderer.enabled;
        }

        #endregion

        #region Custom Methods

        private void OnIsActiveChanged(bool previousValue, bool newValue)
        {
            renderer.enabled = newValue;
        }

        #endregion
    }
}
