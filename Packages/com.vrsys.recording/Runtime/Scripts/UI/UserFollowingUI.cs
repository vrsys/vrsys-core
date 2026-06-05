using UnityEngine;
using VRSYS.Core.Avatar;
using VRSYS.Core.Networking;

public enum BodyPart
{
    HandLeft, HandRight, Head
}

[RequireComponent(typeof(Canvas))]
public class UserFollowingUI : MonoBehaviour
{

    public BodyPart attachedTo;
    public Vector3 offset;
    [SerializeField]
    public float UIScale;

    private Canvas _canvas;
    private GameObject parent = null;
    
    // Start is called before the first frame update
    void Start()
    {
        _canvas = GetComponent<Canvas>();
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if(_canvas.renderMode != RenderMode.WorldSpace)
            _canvas.renderMode = RenderMode.WorldSpace;
        
        if (parent == null && NetworkUser.LocalInstance != null)
        {
            if (attachedTo == BodyPart.HandLeft)
                parent = ((AvatarHMDAnatomy)NetworkUser.LocalInstance.avatarAnatomy).leftHand.gameObject;
            else if (attachedTo == BodyPart.HandRight)
                parent = ((AvatarHMDAnatomy)NetworkUser.LocalInstance.avatarAnatomy).rightHand.gameObject;
            else if (attachedTo == BodyPart.Head)
                parent = NetworkUser.LocalInstance.head.gameObject;

            if (parent != null)
                _canvas.worldCamera = NetworkUser.LocalInstance.GetComponentInChildren<Camera>();
        }

        if (parent != null)
        {
            Transform parentTransform = parent.transform;
            Transform uiParentTransform = transform;

            uiParentTransform.position = parentTransform.position;
            uiParentTransform.rotation = parentTransform.rotation;
            uiParentTransform.position += offset.x * transform.TransformDirection(Vector3.right) + offset.y * transform.TransformDirection(Vector3.up) + 
            offset.z * transform.TransformDirection(Vector3.forward);
            uiParentTransform.localScale = Vector3.one * UIScale;
            //uiParentTransform.LookAt(NetworkUser.localHead.transform);
            
        }

    }
}
