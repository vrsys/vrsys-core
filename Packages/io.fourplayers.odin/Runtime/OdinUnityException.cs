using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Exception type for ODIN Unity
/// </summary>
class OdinUnityException : Exception
{
    public OdinUnityException(string message)
        : base(message)
    { }

    public OdinUnityException(string message, Exception innerException)
        : base(message, innerException)
    { }
}
