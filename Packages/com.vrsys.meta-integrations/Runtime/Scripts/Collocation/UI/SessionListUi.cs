using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VRSYS.Core.Networking;

namespace VRSYS.Meta.Collocation
{
    public class SessionListUi : MonoBehaviour
    {
        #region Properties

        private DisplaySessionsStateHandler _stateHandler;

        [Header("UI Components")] 
        [SerializeField] private Transform _tileRoot;
        [SerializeField] private GameObject _sessionTilePrefab;
        [SerializeField] private Button _createSessionButton;

        #endregion

        #region Public Methods

        public void Initialize(List<OVRColocationSession.Data> sessionDatas, DisplaySessionsStateHandler stateHandler)
        {
            _stateHandler = stateHandler;
            
            // Set position & rotation
            Transform userHead = NetworkUser.LocalInstance.head;
            
            transform.position = userHead.position + userHead.forward * 0.3f;

            Vector3 rotationAngles = userHead.rotation.eulerAngles;
            rotationAngles = new Vector3(0, rotationAngles.y + 180, 0);
            transform.rotation = Quaternion.Euler(rotationAngles);

            foreach (var data in sessionDatas)
            {
                GameObject tileGameObject = Instantiate(_sessionTilePrefab, _tileRoot);

                tileGameObject.GetComponent<CollocationSessionTile>().Initialize(_stateHandler, data);
            }
            
            _createSessionButton.onClick.AddListener(_stateHandler.CreateNewSession);
        }

        #endregion
    }
}
