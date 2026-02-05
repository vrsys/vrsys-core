using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VRSYS.Core.Logging;
using VRSYS.Core.Networking;

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
        [Tooltip("If set to None, first CollocationManager found in scene will be used.")] [SerializeField] private CollocationManager _collocationManager;

        [Header("UI Elements")] 
        [SerializeField] private TextMeshProUGUI _stateText;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Button _closeButton;
        
        [Header("UI Configuration")]
        [SerializeField] private Color _defaultTextColor = Color.white;
        [SerializeField] private List<StatusColor> _customStatusColors;

        [Header("Input Actions")] 
        [SerializeField] private InputActionProperty _toggleUIAction;

        [Header("Positioning")] 
        [SerializeField] private bool _positionAtHeadOnToggle = true;
        [SerializeField] private float _distanceToHead = .7f;

        [Header("Debugging")] 
        [SerializeField] private bool _verbose = false;

        #endregion

        #region Mono- & NetworkBehaviour Methods

        private void Start()
        {
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
            _closeButton.onClick.AddListener(CloseMenu);

            _toggleUIAction.action.Enable();
            _toggleUIAction.action.performed += ToggleUI;

            Invoke(nameof(PositionUI), 2f);
            
            ExtendedLogger.LogInfo(GetType().Name, "Finished collocation state UI setup.", this);
        }

        #endregion

        #region Public Methods

        public void ToggleUI()
        {
            gameObject.SetActive(!gameObject.activeSelf);

            if (gameObject.activeSelf && _positionAtHeadOnToggle)
                PositionUI();
        }

        #endregion

        #region Private Methods

        private void OnCollocationStateChanged(CollocationStateMessage stateMessage)
        {
            if(_verbose)
                ExtendedLogger.LogInfo(GetType().Name, "Received state update message.", this);
            
            _stateText.text = stateMessage.State.ToString();
            
            _statusText.text = stateMessage.Status.ToString();

            int idx = _customStatusColors.FindIndex(c => c.Status == stateMessage.Status);
            _statusText.color = idx == -1 ? _defaultTextColor : _customStatusColors[idx].Color;

            _messageText.text = stateMessage.Message;
        }
        
        private void CloseMenu()
        {
            gameObject.SetActive(false);
        }

        private void ToggleUI(InputAction.CallbackContext obj) => ToggleUI();

        private void PositionUI()
        {
            Transform head = NetworkUser.LocalInstance.head;

            transform.position = head.position + head.forward * _distanceToHead;
        }

        #endregion
    }
}
