#nullable enable

#if USING_XR_MANAGEMENT && USING_XR_SDK_OCULUS && !OVRPLUGIN_UNSUPPORTED_PLATFORM
#define USING_XR_SDK
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IUIControllerInterface
{
#if USING_XR_SDK
    public List<UIInputControllerButton> GetControlSchema();
#endif
}

// NOTE: intentionally not wrapped in `#if USING_XR_SDK`. Several sample scripts (e.g. UILogger)
// declare `GetControlSchema()` returning this type outside their own USING_XR_SDK guards, so the
// type must exist even when that symbol is undefined (this project uses OpenXR, not the Oculus
// XR plugin, so USING_XR_SDK_OCULUS is never defined). Only OVRInput types are referenced here,
// which come from Oculus.VR and are always available.
[System.Serializable]
public struct UIInputControllerButton
{
    public OVRInput.Button button;
    public OVRInput.Controller controller;
    public List<OVRInput.Button> combinationButtons;
    public string description;
    [HideInInspector] public string scope;
    [HideInInspector] public OVRInput.Axis2D axis2d;
}
