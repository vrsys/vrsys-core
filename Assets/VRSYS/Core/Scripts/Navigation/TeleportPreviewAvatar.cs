using Unity.Netcode;
using UnityEngine;

namespace VRSYS.Core.Navigation
{
    public class TeleportPreviewAvatar : NetworkBehaviour
    {
        #region Member Variables

        [Header("Preview Avatar Components")]
        [Tooltip("This component represent the visuals to show selected position.")]
        [SerializeField] private GameObject placementIndicatorVisuals;
        [Tooltip("This component indicates progress of position locking.")]
        [SerializeField] private Transform progressIndicator;
        [Tooltip("This component indicates future rotation and height of the user.")]
        [SerializeField] private Transform previewAvatar;
        [Tooltip("This component is toggled to show or hide preview avatar when position is locked.")] 
        [SerializeField] private GameObject previewAvatarVisuals;
        
        // Network Variables
        private NetworkVariable<bool> placementIndicatorActive = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<bool> avatarActive = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        #endregion

        #region Mono- & NetworkBehaviour Callbacks

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                placementIndicatorActive.OnValueChanged += OnIndicatorActiveChanged;
                placementIndicatorVisuals.SetActive(placementIndicatorActive.Value);

                avatarActive.OnValueChanged += OnAvatarActiveChanged;
                previewAvatarVisuals.SetActive(avatarActive.Value);
            }

            Initialize();
        }

        #endregion

        #region Custom Methods


        private void Initialize()
        {
            placementIndicatorVisuals.SetActive(placementIndicatorActive.Value);
            previewAvatarVisuals.SetActive(avatarActive.Value);
        }

        /// <summary>
        /// Activates placement & progress indicator.
        /// </summary>
        public void ActivateIndicator()
        {
            placementIndicatorVisuals.SetActive(true);
            placementIndicatorActive.Value = true;
        }
        
        /// <summary>
        /// Updates  position of placement & scale of progress indicator.
        /// </summary>
        public void UpdateIndicator(Vector3 targetPos, float progress)
        {
            transform.position = targetPos;
            progressIndicator.localScale = new Vector3(progress, progressIndicator.localScale.y,
                progress);
        }

        /// <summary>
        /// Activates full preview avatar.
        /// </summary>
        public void ActivateAvatar()
        {
            previewAvatarVisuals.SetActive(true);
            avatarActive.Value = true;
        }

        /// <summary>
        /// Updates Avatar rotation and height.
        /// </summary>
        public void UpdateAvatar(Vector3 hitPos, float height)
        {
            transform.rotation = Quaternion.LookRotation(hitPos - transform.position);

            Vector3 newPos = previewAvatar.localPosition;
            newPos.y = height;
            previewAvatar.localPosition = newPos;
        }

        /// <summary>
        /// Deactivates all currently active preview avatar visuals
        /// </summary>
        public void Deactivate()
        {
            if (placementIndicatorVisuals.activeSelf)
            {
                placementIndicatorVisuals.SetActive(false);
                placementIndicatorActive.Value = false;
            }

            if (previewAvatarVisuals.activeSelf)
            {
                previewAvatarVisuals.SetActive(false);
                avatarActive.Value = false;
            }
        }

        #endregion

        #region OnValueChangedEvents

        private void OnIndicatorActiveChanged(bool previousValue, bool newValue) =>
            placementIndicatorVisuals.SetActive(newValue);

        private void OnAvatarActiveChanged(bool previousValue, bool newValue) =>
            previewAvatarVisuals.SetActive(newValue);

        #endregion
    }
}
