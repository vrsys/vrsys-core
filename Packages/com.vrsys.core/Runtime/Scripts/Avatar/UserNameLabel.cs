using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using VRSYS.Core.Logging;
using VRSYS.Core.Networking;

namespace VRSYS.Core.Avatar
{
    public class UserNameLabel : MonoBehaviour
    {
        #region Member Variables

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI _labelText;
        [SerializeField] private Image _labelBackground;

        [Header("Behaviour Configuration")] 
        [SerializeField] private bool _applyUserColor = false;

        private NetworkUser _networkUser;

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            Initialize();
        }

        #endregion

        #region Private Methods

        private void Initialize()
        {
            _networkUser = GetComponentInParent<NetworkUser>();

            if (_networkUser == null)
            {
                ExtendedLogger.LogError(GetType().Name, $"No {typeof(NetworkUser)} could be found.", this);
                return;
            }

            // register value changed events
            _networkUser.userName.OnValueChanged += OnUserNameChanged;
            _networkUser.userColor.OnValueChanged += OnUserColorChanged;
            
            // initialize label components
            UpdateUserName();
            UpdateBackground();
        }

        private void UpdateUserName() => _labelText.text = _networkUser.userName.Value.ToString();

        private void UpdateBackground()
        {
            if (_applyUserColor)
                _labelBackground.color = _networkUser.userColor.Value;
        }

        #endregion

        #region Event Callbacks

        private void OnUserNameChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue) =>
            UpdateUserName();

        private void OnUserColorChanged(Color previousValue, Color newValue) => UpdateBackground();

        #endregion
    }
}
