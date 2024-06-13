using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Logging;



public class XBoxControllerNavigation : MonoBehaviour
{

    public Transform target;
    public NavigationTechnique currentNavigationTechnique = NavigationTechnique.Steering;

    [Header("Input Actions")]
    public InputActionProperty moveAction;
    public InputActionProperty translationVelocityAction;
    public InputActionProperty headRotationAction;
    public InputActionProperty headRotationVelocityAction;
    public InputActionProperty techniqueSelectionAction;

    [Range(0, 10)] public float translationVelocity = 3.0f;
    [Range(0, 30)] public float rotationVelocity = 5.0f;

    public enum NavigationTechnique
    {
        Steering,
        Jumping

    }

    private bool? isOfflineOrOwner_;
    private bool isOfflineOrOwner
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


    private void Start()
    {
        if (!isOfflineOrOwner)
            Destroy(this);
        else if (target == null)
            target = transform;
        currentNavigationTechnique = NavigationTechnique.Steering;
    }


    // Update is called once per frame
    void Update()
    {
        TechniqueSelection();
    }


    private void Steering()
    {
        if (!isOfflineOrOwner)
            return;
        MapInput(CalcTranslationInput(), CalcRotationInput());


    }

    private void TechniqueSelection()
    {
        float input = techniqueSelectionAction.action.ReadValue<float>();
        int selection = input < 0 ? -1: input > 0 ? 1:  0;

        int current = (((int)currentNavigationTechnique) + selection) % (Enum.GetNames(typeof(NavigationTechnique)).Length);
        currentNavigationTechnique = (NavigationTechnique)current;

        switch (currentNavigationTechnique)
        {

            case NavigationTechnique.Jumping:
                Jumping();
                break;
            
            default:
                Steering();
                break;
        }
    }

    public void MapInput(Vector3 transInput, Vector3 rotInput)
    {
        // map translation input
        if (transInput.magnitude > 0.0f)
            target.Translate(transInput);

        // map rotation input
        if (rotInput.magnitude > 0.0f)
            target.localRotation *= Quaternion.Euler(rotInput);
    }

    public Vector3 CalcTranslationInput()
    {
        Vector3 xzInput = new Vector3(moveAction.action.ReadValue<Vector2>().x, 0f,
            moveAction.action.ReadValue<Vector2>().y);

        ExtendedLogger.LogInfo(GetType().Name, "xzInput " + xzInput);

        float acceleration = translationVelocity * (translationVelocityAction.action.ReadValue<float>() + 1);

        ExtendedLogger.LogInfo(GetType().Name, "acceleration " + acceleration);


        Vector3 transInput = xzInput * (acceleration * Time.deltaTime);


        return transInput;
    }

    public Vector3 CalcRotationInput()
    {
        float headAcceleration = rotationVelocity * (headRotationVelocityAction.action.ReadValue<float>() + 1);


        Vector2 headRotation = headRotationAction.action.ReadValue<Vector2>();
        Vector3 rotInput = new Vector3(headRotation.y, headRotation.x, 0.0f);
        rotInput *= (headAcceleration * Time.deltaTime);
        return rotInput;
    }

    public void Jumping()
    {
        ExtendedLogger.LogInfo(GetType().Name, "Jump!");
    }
}
