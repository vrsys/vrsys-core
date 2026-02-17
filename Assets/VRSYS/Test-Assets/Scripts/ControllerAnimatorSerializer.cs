using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Avatar
{
    public class ControllerAnimatorSerializer : NetworkBehaviour
    {
        #region Properties

        [SerializeField, Tooltip("Root GameObject of controller visuals.")]
        private GameObject _controllerVisuals;

        private ControllerAnimator _controllerAnimator;

        private bool _initialized => _controllerAnimator != null;

        private XRInputModalityManager _modalityManager;

        #endregion

        #region Network Properties

        private NetworkVariable<bool> _isActive = new(true, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private NetworkVariable<ControllerAnimator.ControllerValueData> _controllerValueData =
            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        #endregion

        #region Mono- & NetworkBehaviour Methods

        public override void OnNetworkSpawn()
        {
            _controllerAnimator = _controllerVisuals.GetComponent<ControllerAnimator>();
            
            if (!_initialized)
            {
                ExtendedLogger.LogError(GetType().Name, "Missing ControllerAnimator on controller visuals node.", this);
                return;
            }

            if (IsOwner)
            {
                _controllerAnimator.EnableReadControllerData();
                
                _modalityManager = GetComponentInParent<XRInputModalityManager>();

                if (_modalityManager != null)
                {
                    _modalityManager.motionControllerModeStarted.AddListener(OnControllerModeStarted);
                    _modalityManager.motionControllerModeEnded.AddListener(OnControllerModeEnded);
                }
            }
            else
            {
                _controllerAnimator.DisableReadControllerData();
                _isActive.OnValueChanged += OnisActiveChanged;
                
                UpdateControllerVisualsActive();
            }
        }

        private void Update()
        {
            if(!_initialized || !_isActive.Value)
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

        private void UpdateControllerVisualsActive() => _controllerVisuals.SetActive(_isActive.Value);

        #endregion

        #region Event Callbacks

        private void OnControllerModeStarted() => _isActive.Value = true;

        private void OnControllerModeEnded() => _isActive.Value = false;

        private void OnisActiveChanged(bool previousValue, bool newValue) => UpdateControllerVisualsActive();

        #endregion
    }
}
