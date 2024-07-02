using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BaseRayInteractor : BaseInteractor
{
    [Header("Ray Interactor Variables")]
    public float rayOriginOffset;
    public float rayLength;
    public Vector3 rayOrigin;

    protected void EvaluateRaySelection(Ray ray, InputAction action)
    {

        if (Physics.Raycast(ray, out var hit, rayLength, layersToInteractWith))
        {
            if (hoveredTransform is null || hit.transform == hoveredTransform)
            {
                hoveredTransform = hit.transform;
                if (action.WasPressedThisFrame())
                    selectedTransform = hoveredTransform;
                else if (action.WasReleasedThisFrame())
                    selectedTransform = null;
            }
            else if (hoveredTransform is not null && hit.transform != hoveredTransform)
            {
                if (action.WasReleasedThisFrame())
                {
                    hoveredTransform = hit.transform;
                    selectedTransform = null;
                }
            }
        }
        else if (selectedTransform is null || selectedTransform is not null && !action.IsPressed())
        {
            hoveredTransform = null;
            selectedTransform = null;
        }


    }
}
