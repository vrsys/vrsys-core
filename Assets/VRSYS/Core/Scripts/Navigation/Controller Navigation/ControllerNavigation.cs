using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class ControllerNavigation: MonoBehaviour
{

    public Transform target;

    [Header("Base Steering Input Actions")]
    public InputActionProperty moveAction;

    [Header("Base Steering Velocities")]
    [Tooltip("Translation Velocity [m/sec]")] [Range(0, 10)]
    public float translationVelocity = 3.0f;

    [Tooltip("Rotation Velocity [degree/sec]")] [Range(0, 30)]
    public float rotationVelocity = 5.0f;

    [Header("Misc.")][SerializeField]
    protected bool verbose;

    protected bool? isOfflineOrOwner_;
    protected bool isOfflineOrOwner
    {
        get
        {
            if (!isOfflineOrOwner_.HasValue)
            {
                if (GetComponent<NetworkObject>() is not null)
                    isOfflineOrOwner_ = GetComponent<NetworkObject>().IsOwner;
                else
                    isOfflineOrOwner_ = true;
            }
            return isOfflineOrOwner_.Value;
        }
    }


    // Start is called before the first frame update
    protected void Init()
    {
        if (!isOfflineOrOwner)
            Destroy(this);
        else if (target == null)
            target = transform;
    }

    // Update is called once per frame
    protected virtual void MapInput(Vector3 transInput, Vector3 rotInput)
    {
        // map translation input
        if (transInput.magnitude > 0.0f)
            target.Translate(transInput);

        // map rotation input
        if (rotInput.magnitude > 0.0f)
            target.localRotation *= Quaternion.Euler(rotInput);
    }


    protected abstract Vector3 CalcTranslationInput();
    protected abstract Vector3 CalcRotationInput();




}
