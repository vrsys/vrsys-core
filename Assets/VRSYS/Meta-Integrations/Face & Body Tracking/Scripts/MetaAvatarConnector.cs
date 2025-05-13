using System.Collections.Generic;
using UnityEngine;
using VRSYS.Core.Logging;

public class MetaAvatarConnector : MonoBehaviour
{
    #region Member Variables

    [Header("Meta Avatar Components")]
    [SerializeField] private MetaAvatarHandler avatarHandler;

    [Header("Rig Components")] 
    [SerializeField] private Transform rootBone;
    private List<Transform> bones = new List<Transform>();

    [Header("Debug")] 
    public bool connectAvatarTrigger = false;

    #endregion

    #region MonoBehaviour Callbacks

    private void Awake()
    {
        if (avatarHandler == null)
        {
            ExtendedLogger.LogError(GetType().Name, "No MetaAvatarHandler configured!", this);
            return;
        }

        RetrieveBones();
        
        avatarHandler.onAvatarLoaded.AddListener(ConnectAvatar);
    }

    private void Update()
    {
        if (connectAvatarTrigger)
        {
            ConnectAvatar();
            connectAvatarTrigger = false;
        }
    }

    #endregion

    #region Custom Methods

    private void RetrieveBones()
    {
        bones.Add(rootBone);

        foreach (Transform child in rootBone)
        {
            bones.Add(child);
        }
    }

    private void ConnectAvatar()
    {
        SkinnedMeshRenderer[] renderers = avatarHandler.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        foreach (var renderer in renderers)
        {
            ExtendedLogger.LogError(GetType().Name, renderer.name + " adjusted.", this);
            
            renderer.rootBone = rootBone;
            renderer.bones = bones.ToArray();
        }
    }

    #endregion
}
