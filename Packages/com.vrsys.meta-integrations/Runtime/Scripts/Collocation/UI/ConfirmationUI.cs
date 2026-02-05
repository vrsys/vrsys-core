using UnityEngine;
using UnityEngine.UI;
using VRSYS.Core.Networking;

using UnityEngine.Events;
using UnityEngine.Serialization;

namespace VRSYS.Meta.Collocation
{
    public class ConfirmationUI : MonoBehaviour
    {
        #region Properties

        [Header("UI Elements")] 
        [SerializeField] private Button _confirm;
        [SerializeField] private Button _reject;

        #endregion

        #region Public Methods

        public void Initialize(UnityAction OnConfirm, UnityAction OnReject)
        {
            _confirm.onClick.AddListener(OnConfirm);
            _reject.onClick.AddListener(OnReject);
            Hide();
        }
        
        public void Show()
        {
            gameObject.SetActive(true);
            // Set position & rotation
            Transform userHead = NetworkUser.LocalInstance.head;
            
            transform.position = userHead.position + userHead.forward * 0.3f;

            Vector3 rotationAngles = userHead.rotation.eulerAngles;
            rotationAngles = new Vector3(0, rotationAngles.y, 0);
            transform.rotation = Quaternion.Euler(rotationAngles);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        #endregion
    }
}