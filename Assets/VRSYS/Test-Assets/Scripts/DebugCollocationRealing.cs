using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using VRSYS.Meta.Collocation;

public class DebugCollocationRealing : MonoBehaviour
{
    public CollocationManager collocationManager;
    public InputAction realignAction;

    private List<XRInputSubsystem> _xrInputSubsystems = new();

    private void Start()
    {
        realignAction.Enable();
    }

    private void Update()
    {
        if(realignAction.WasPressedThisFrame())
            collocationManager.Realign();
    }
}
