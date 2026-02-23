using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VRSYS.Core.Logging;
using VRSYS.Core.Networking;

using Label = TMPro.TextMeshProUGUI;

namespace VRSYS.Meta.Collocation
{
    public class CollocationStateUI : MonoBehaviour
    {
        #region Structs

        [Serializable]
        private struct StatusColor
        {
            public CollocationStateStatus Status;
            public Color Color;
        }

        #endregion
        
        #region Properties

        [Header("Collocation Manager")]
        [SerializeField, Tooltip("If set to None, first CollocationManager found in scene will be used.")] private CollocationManager _collocationManager;

        [Header("State UI Elements & Configuration")] 
        [SerializeField] private GameObject _stateUi;
        [SerializeField] private Button _stateUiButton;
        [SerializeField] private TextMeshProUGUI _stateText;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Button _restartWithLocalAnchorButton;
        [SerializeField] private Button _restartWithSessionAnchorButton;
        [SerializeField] private Color _defaultTextColor = Color.white;
        [SerializeField] private List<StatusColor> _customStatusColors;

        [Header("Settings UI Elements & Configuration")] 
        [SerializeField] private GameObject _settingsUi;
        [SerializeField] private Button _settingsUiButton;
        [SerializeField] private Slider _discoveryTimeSlider;
        [SerializeField] private Label _discoveryTimeLabel;
        [SerializeField] private Slider _retryTimeSlider;
        [SerializeField] private Label _retryTimeLabel;
        [SerializeField] private Slider _maxRetriesSlider;
        [SerializeField] private Label _maxRetriesLabel;
        [SerializeField] private Toggle _useLocalAnchorToggle;
        [SerializeField] private Toggle _tryLoadLocalAnchorToggle;
        [SerializeField] private Toggle _useDefaultSessionAnchorToggle;
        [SerializeField] private TMP_InputField _xPosInputField;
        [SerializeField] private TMP_InputField _yPosInputField;
        [SerializeField] private TMP_InputField _zPosInputField;
        [SerializeField] private Button _saveValuesButton;
        

        [Header("Input Actions")] 
        [SerializeField] private InputActionProperty _toggleUIAction;

        [Header("Positioning")] 
        [SerializeField] private bool _positionAtHeadOnToggle = true;
        [SerializeField] private float _distanceToHead = .7f;

        [Header("Debugging")] 
        [SerializeField] private bool _verbose = false;

        private Color _defaultUiToggleButtonColor;

        #endregion

        #region MonoBehaviour Methods

        private void Start()
        {
            if (!GetComponentInParent<NetworkObject>().IsOwner)
            {
                Destroy(gameObject);
                return;
            }
            
            // Collocation Manager Setup
            if (_collocationManager == null)
            {
                _collocationManager = FindAnyObjectByType<CollocationManager>();

                if (_collocationManager == null)
                {
                    ExtendedLogger.LogError(GetType().Name, "No CollocationManager could be found.", this);
                    
                    gameObject.SetActive(false);
                    return;
                }
            }
            
            _collocationManager.OnStateChanged.AddListener(OnCollocationStateChanged);

            ColorUtility.TryParseHtmlString("006189", out _defaultUiToggleButtonColor);
            
            SetupStateUI();
            
            SetupSettingsUI();
            
            

            // Toggle Action Setup
            _toggleUIAction.action.Enable();
            _toggleUIAction.action.performed += ToggleUI;

            Invoke(nameof(PositionUI), 2f);
            
            ExtendedLogger.LogInfo(GetType().Name, "Finished collocation state UI setup.", this);
        }

        private void OnEnable()
        {
            if(_settingsUi.activeSelf)
                UpdateSettingUIElements();
        }

        #endregion

        #region Private Methods

        private void SetupStateUI()
        {
            _stateUiButton.onClick.AddListener(SetStateUIActive);
            
            _restartWithLocalAnchorButton.gameObject.SetActive(_collocationManager.CanRestart);
            _restartWithLocalAnchorButton.onClick.AddListener(TryRestartLocalAnchorCollocation);
            
            _restartWithSessionAnchorButton.gameObject.SetActive(_collocationManager.CanRestart);
            _restartWithSessionAnchorButton.onClick.AddListener(TryRestartSessionAnchorCollocation);
        }

        private void SetupSettingsUI()
        {
            _settingsUiButton.onClick.AddListener(SetSettingsUIActive);
            
            UpdateSettingUIElements();

            _discoveryTimeSlider.onValueChanged.AddListener((newValue) =>
            {
                _discoveryTimeLabel.text = newValue + " sec.";
            });
            
            _retryTimeSlider.onValueChanged.AddListener(newValue =>
            {
                _retryTimeLabel.text = newValue + " sec.";
            });
            
            _maxRetriesSlider.onValueChanged.AddListener(newValue =>
            {
                _maxRetriesLabel.text = newValue.ToString();
            });
            
            _saveValuesButton.onClick.AddListener(SaveSettingValues);
        }

        private void SetStateUIActive()
        {
            _stateUi.SetActive(true);
            _stateUiButton.image.color = Color.white;
            
            _settingsUi.SetActive(false);
            _settingsUiButton.image.color = _defaultUiToggleButtonColor;
        }

        private void SetSettingsUIActive()
        {
            UpdateSettingUIElements();
            
            _settingsUi.SetActive(true);
            _settingsUiButton.image.color = Color.white;
            
            _stateUi.SetActive(false);
            _stateUiButton.image.color = _defaultUiToggleButtonColor;
        }

        private void UpdateSettingUIElements()
        {
            _discoveryTimeSlider.value = _collocationManager.DiscoverTime;
            _retryTimeSlider.value = _collocationManager.RetryTime;
            _maxRetriesSlider.value = _collocationManager.MaxRetries;

            _useLocalAnchorToggle.isOn = _collocationManager.UseLocalAnchor;
            _tryLoadLocalAnchorToggle.isOn = _collocationManager.TryLoadLocalAnchor;
            _useDefaultSessionAnchorToggle.isOn = _collocationManager.UseDefaultSessionAnchor;

            _xPosInputField.text = _collocationManager.DefaultSessionAnchorWorldPosition.x.ToString();
            _yPosInputField.text = _collocationManager.DefaultSessionAnchorWorldPosition.y.ToString();
            _zPosInputField.text = _collocationManager.DefaultSessionAnchorWorldPosition.z.ToString();
        }
        
        private void SaveSettingValues()
        {
            _collocationManager.collocationSettings.DiscoverTime = _discoveryTimeSlider.value;
            _collocationManager.collocationSettings.RetryTime = _retryTimeSlider.value;
            _collocationManager.collocationSettings.MaxRetries = (int)_maxRetriesSlider.value;

            _collocationManager.collocationSettings.UseLocalAnchor = _useLocalAnchorToggle.isOn;
            _collocationManager.collocationSettings.TryLoadLocalAnchor = _tryLoadLocalAnchorToggle.isOn;
            _collocationManager.collocationSettings.UserDefaultSessionAnchor = _useDefaultSessionAnchorToggle.isOn;

            Vector3 pos = new Vector3(
                float.Parse(_xPosInputField.text),
                float.Parse(_yPosInputField.text),
                float.Parse(_zPosInputField.text));

            _collocationManager.collocationSettings.DefaultSessionAnchorWorldPos = pos;
        }
        
        private void ToggleUI()
        {
            gameObject.SetActive(!gameObject.activeSelf);

            if (gameObject.activeSelf && _positionAtHeadOnToggle)
                PositionUI();
        }

        private void OnCollocationStateChanged(CollocationStateMessage stateMessage)
        {
            if(_verbose)
                ExtendedLogger.LogInfo(GetType().Name, "Received state update message.", this);
            
            _stateText.text = stateMessage.State.ToString();
            
            _statusText.text = stateMessage.Status.ToString();

            int idx = _customStatusColors.FindIndex(c => c.Status == stateMessage.Status);
            _statusText.color = idx == -1 ? _defaultTextColor : _customStatusColors[idx].Color;

            _messageText.text = stateMessage.Message;
            
            _restartWithLocalAnchorButton.gameObject.SetActive(_collocationManager.CanRestart);
            _restartWithSessionAnchorButton.gameObject.SetActive(_collocationManager.CanRestart);
        }

        private void ToggleUI(InputAction.CallbackContext obj) => ToggleUI();

        private void PositionUI()
        {
            Transform head = NetworkUser.LocalInstance.head;

            transform.position = head.position + head.forward * _distanceToHead;
        }
        
        private void TryRestartLocalAnchorCollocation()
        {
            _collocationManager.RestartCollocationWithLocalAnchor(true);
        }

        private void TryRestartSessionAnchorCollocation()
        {
            _collocationManager.RestartCollocationWithSessionAnchor();
        }

        #endregion
    }
}
