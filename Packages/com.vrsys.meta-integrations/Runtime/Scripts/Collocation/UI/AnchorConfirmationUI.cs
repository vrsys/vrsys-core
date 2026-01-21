using UnityEngine;
using UnityEngine.UI;
using VRSYS.Core.Networking;

using UnityEngine.Events;

namespace VRSYS.Meta.Collocation
{
    public class AnchorConfirmationUI : MonoBehaviour
    {
        #region Properties

        [Header("UI Elements")] 
        [SerializeField] private Button _confirm;
        [SerializeField] private Button _tryAgain;

        #endregion

        #region Public Methods

        public void Initialize(UnityAction OnConfirm, UnityAction OnTryAgain)
        {
            _confirm.onClick.AddListener(OnConfirm);
            _tryAgain.onClick.AddListener(OnTryAgain);
            Hide();
        }
        
        public void Show()
        {
            gameObject.SetActive(true);
            // Set position & rotation
            Transform userHead = NetworkUser.LocalInstance.head;
            
            transform.position = userHead.position + userHead.forward * 0.3f;

            Vector3 rotationAngles = userHead.rotation.eulerAngles;
            rotationAngles = new Vector3(0, rotationAngles.y + 180, 0);
            transform.rotation = Quaternion.Euler(rotationAngles);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        #endregion
    }
}