using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRSYS.Core.Networking;
using WebSocketSharp;

namespace VRSYS.Meta.Collocation
{
    public class CreateSessionUi : MonoBehaviour
    {
        #region Properties

        private CreateSessionStateHandler _stateHandler;
        private string _sessionName;

        [Header("UI Elements")] 
        [SerializeField] private TMP_InputField _sessionNameInputField;
        [SerializeField] private Button _createButton;
        [SerializeField] private GameObject _warningText;
        [SerializeField] private Button _searchAgainButton;

        #endregion

        #region Public Methods

        public void Initialize(CreateSessionStateHandler stateHandler)
        {
            _stateHandler = stateHandler;
            
            // Set position & rotation
            Transform userHead = NetworkUser.LocalInstance.head;
            
            transform.position = userHead.position + userHead.forward * 0.5f;

            Vector3 rotationAngles = userHead.rotation.eulerAngles;
            rotationAngles = new Vector3(0, rotationAngles.y, 0);
            transform.rotation = Quaternion.Euler(rotationAngles);
            
            _sessionNameInputField.onValueChanged.AddListener(OnSessionNameInputChanged);
            _createButton.onClick.AddListener(OnClickCreateSession);
            _searchAgainButton.onClick.AddListener(_stateHandler.SearchSessions);
        }

        #endregion

        #region Private Methods
        
        private void OnSessionNameInputChanged(string arg0)
        {
            if(_warningText.activeSelf)
                _warningText.SetActive(false);

            _sessionName = _sessionNameInputField.text;
        }

        private void OnClickCreateSession()
        {
            if (_sessionNameInputField.text.IsNullOrEmpty())
            {
                _warningText.SetActive(true);
                return;
            }
            
            _stateHandler.CreateSession(_sessionName);
        }

        #endregion
    }
}
