using System.Collections.Generic;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Meta.Bodytracking
{
    public class MetaAvatarConnector : MonoBehaviour
    {
        #region Member Variables

        [SerializeField] private SkinnedMeshRenderer proxyMesh;
        
        private MetaAvatarHandler metaAvatarHandler;
        
        private Transform bonesRoot;
        private Transform bindShapesRoot;

        #endregion

        #region MonoBehaviour Callbacks

        private void Awake()
        {
            Initialize();
        }

        #endregion

        #region Custom Methods

        private void Initialize()
        {
            metaAvatarHandler = GetComponent<MetaAvatarHandler>();

            if (metaAvatarHandler == null)
            {
                ExtendedLogger.LogError(GetType().Name, "No MetaAvatarHandler attached.", this);
                return;
            }
            
            metaAvatarHandler.onAvatarLoaded.AddListener(ConnectAvatar);
        }

        private void ConnectAvatar()
        {
            SkinnedMeshRenderer[] renderers = metaAvatarHandler.GetLocalAvatarEntity()
                .GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);

            proxyMesh.sharedMesh = renderers[0].sharedMesh;
        }

        #endregion
    }
}
