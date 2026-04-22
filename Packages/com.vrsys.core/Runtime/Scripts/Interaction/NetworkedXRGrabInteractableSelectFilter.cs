using Unity.Netcode;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Interaction
{
    public class NetworkedXRGrabInteractableSelectFilter : NetworkBehaviour, IXRSelectFilter
    {
        #region General Properties

        private NetworkVariable<bool> _isGrabbed = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private bool _isGrabbedLocally = false;

        #endregion

        #region IXRSelectFilter Properties

        public bool canProcess => isActiveAndEnabled;

        #endregion

        #region NetworkBehaviour Methods

        public override void OnNetworkSpawn()
        {
            XRBaseInteractable interactable = GetComponent<XRBaseInteractable>();

            interactable.selectExited.AddListener(OnRelease);
        }

        #endregion

        #region IXRSelectFilter Methods

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            bool canSelect;
            
            if (_isGrabbed.Value)
            {
                canSelect = _isGrabbedLocally;
            }
            else
            {
                _isGrabbedLocally = true;

                if (!IsOwner)
                    ChangeOwnerRpc(NetworkManager.LocalClientId);

                UpdateIsGrabbedRpc(true);

                canSelect = true;
            }
            
            ExtendedLogger.LogInfo(GetType().Name, $"Select access granted: {canSelect}", this);

            return canSelect;
        }

        #endregion

        #region Custom Methods

        private void OnRelease(SelectExitEventArgs arg0)
        {
            if (_isGrabbedLocally)
            {
                _isGrabbedLocally = false;
                UpdateIsGrabbedRpc(false);
            }
        }

        #endregion

        #region RPCs

        [Rpc(SendTo.Server)]
        private void ChangeOwnerRpc(ulong clientId)
        {
            NetworkObject.ChangeOwnership(clientId);
        }

        [Rpc(SendTo.Server)]
        private void UpdateIsGrabbedRpc(bool isGrabbed)
        {
            _isGrabbed.Value = isGrabbed;
        }

        #endregion

    }
}
