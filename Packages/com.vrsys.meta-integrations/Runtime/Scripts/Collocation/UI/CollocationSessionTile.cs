using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRSYS.Meta.Collocation
{
    public class CollocationSessionTile : MonoBehaviour
    {
        #region Properties

        private DisplaySessionsStateHandler _stateHandler;
        private OVRColocationSession.Data _data;

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI _sessionNameText;
        [SerializeField] private TextMeshProUGUI _sessionUuidText;
        [SerializeField] private Button _joinSessionButton;

        #endregion

        #region Public Methods

        public void Initialize(DisplaySessionsStateHandler stateHandler, OVRColocationSession.Data data)
        {
            _stateHandler = stateHandler;
            _data = data;

            _sessionNameText.text = Encoding.UTF8.GetString(data.Metadata);
            _sessionUuidText.text = data.AdvertisementUuid.ToString();
            _joinSessionButton.onClick.AddListener(SessionSelected);
        }

        #endregion

        #region Private Methods

        private void SessionSelected()
        {
            _stateHandler.JoinSession(_data);
        }

        #endregion
    }
}
