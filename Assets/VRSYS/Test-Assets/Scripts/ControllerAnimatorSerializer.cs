using Unity.Netcode;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Avatar
{
    public class ControllerAnimatorSerializer : NetworkBehaviour
    {
        #region Properties

        [SerializeField, Tooltip("ControllerAnimator that is being serialized.")]
        private ControllerAnimator _controllerAnimator;

        private bool _initialized => _controllerAnimator != null;

        #endregion

        #region Network Properties

        private NetworkVariable<ControllerAnimator.ControllerValueData> _controllerValueData =
            new NetworkVariable<ControllerAnimator.ControllerValueData>(default, NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);

        #endregion

        #region Mono- & NetworkBehaviour Methods

        public override void OnNetworkSpawn()
        {
            if (!_initialized)
            {
                ExtendedLogger.LogError(GetType().Name, "Missing ControllerAnimator reference.", this);
                return;
            }
            
            if(IsOwner)
                _controllerAnimator.EnableReadControllerData();
            else
            {
                _controllerAnimator.DisableReadControllerData();
            }
        }

        private void Update()
        {
            if(!_initialized)
                return;

            if (IsOwner)
            {
                WriteControllerValues();
            }
            else
            {
                ReadControllerValues();
            }
        }

        #endregion

        #region Private Methods

        private void WriteControllerValues()
        {
            _controllerValueData.Value = _controllerAnimator.controllerValueData;
        }

        private void ReadControllerValues()
        {
            _controllerAnimator.SetControllerValues(_controllerValueData.Value);
        }

        #endregion
    }
}
