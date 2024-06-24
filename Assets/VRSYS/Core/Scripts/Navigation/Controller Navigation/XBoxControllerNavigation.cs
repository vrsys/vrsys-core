using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Logging;



public class XBoxControllerNavigation : ControllerNavigation
{
    [Header("Navigation Options")]
    public NavigationTechnique currentNavigationTechnique = NavigationTechnique.Steering;

    [Header("Controller Specific Input Actions")]
    public InputActionProperty translationVelocityAction;
    public InputActionProperty headRotationAction;
    public InputActionProperty headRotationVelocityAction;
    public InputActionProperty techniqueSelectionAction;
    public InputActionProperty resetOrientationAction;

    public enum NavigationTechnique
    {
        Steering,
        Jumping

    }

    private void Start()
    {
        Init();

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
        ResetOrientation();

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

    public void ResetOrientation()
    {
        var resetPressed = resetOrientationAction.action.WasPressedThisFrame();

        if (resetPressed)
            target.rotation = Quaternion.Euler(0, target.rotation.eulerAngles.y, target.rotation.eulerAngles.z);

        if (verbose)
        {
            ExtendedLogger.LogInfo(resetOrientationAction.action.ToString(), "Reset orientation");
        }
    }

    protected override Vector3 CalcTranslationInput()
    {
        Vector3 xzInput = new Vector3(moveAction.action.ReadValue<Vector2>().x, 0f,
            moveAction.action.ReadValue<Vector2>().y);


        float acceleration = translationVelocity * (translationVelocityAction.action.ReadValue<float>() + 1);

        if (verbose)
        {
            ExtendedLogger.LogInfo(GetType().Name, "acceleration " + acceleration);
            ExtendedLogger.LogInfo(GetType().Name, "xzInput " + xzInput);

        }

        Vector3 transInput = xzInput * (acceleration * Time.deltaTime);


        return transInput;
    }

    protected override Vector3 CalcRotationInput()
    {
        float headAcceleration = rotationVelocity * (headRotationVelocityAction.action.ReadValue<float>() + 1);


        Vector2 headRotation = headRotationAction.action.ReadValue<Vector2>();
        Vector3 rotInput = new Vector3(headRotation.y, headRotation.x, 0.0f);
        rotInput *= (headAcceleration * Time.deltaTime);
        return rotInput;
    }

    public void Jumping()
    {
        if (verbose)
        {

            ExtendedLogger.LogInfo(GetType().Name, "Jump!");

        }
    }
}
