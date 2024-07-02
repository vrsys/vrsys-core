using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using VRSYS.Core.Navigation;
using VRSYS.Core.Logging;

public class RayControllerInteractor : BaseRayInteractor
{
    
    public XBoxControllerNavigation xBoxControllerNavigation;
    private bool AllowNavigation => xBoxControllerNavigation.allowNavigation;

    [Header("Input Actions")]
    public InputActionProperty selectActionProperty;
    public InputActionProperty railingActionProperty;
    public InputActionProperty railingToggleProperty;



    private void Start()
    {
        if (!isOfflineOrOwner)
            Destroy(this);

        rayOrigin = transform.position + transform.TransformDirection(Vector3.forward) * rayOriginOffset;

    }


    private void Update()
    {
        if (!isOfflineOrOwner)
            return;

        EvaluateInteraction();
        NavigationRailingToggle();
    }

    protected void EvaluateInteraction()
    {

        var prevHoveredTransform = hoveredTransform;
        var prevSelectedTransform = selectedTransform;

        Ray ray = new(rayOrigin, transform.TransformDirection(Vector3.forward));

        EvaluateRaySelection(ray, selectActionProperty.action);

        EvaluateHoverStateChange(prevHoveredTransform);
        EvaluateSelectStateChange(prevSelectedTransform);
    }

    private void NavigationRailingToggle()
    {
        if(railingToggleProperty.action.WasPressedThisFrame())
            xBoxControllerNavigation.allowNavigation = !AllowNavigation;
    }
}
