using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.XR;
using VRSYS.Meta.Collocation;

public class DebugCollocationRealing : MonoBehaviour
{
    [FormerlySerializedAs("collocationManager")] public CollocationManagerOld collocationManagerOld;
    public InputAction realignAction;

    private List<XRInputSubsystem> _xrInputSubsystems = new();

    private void Start()
    {
        realignAction.Enable();
    }

    private void Update()
    {
        if(realignAction.WasPressedThisFrame())
            collocationManagerOld.Realign();
    }
}
