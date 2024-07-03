// VRSYS plugin of Virtual Reality and Visualization Group (Bauhaus-University Weimar)
//  _    ______  _______  _______
// | |  / / __ \/ ___/\ \/ / ___/
// | | / / /_/ /\__ \  \  /\__ \ 
// | |/ / _, _/___/ /  / /___/ / 
// |___/_/ |_|/____/  /_//____/  
//
//  __                            __                       __   __   __    ___ .  . ___
// |__)  /\  |  | |__|  /\  |  | /__`    |  | |\ | | \  / |__  |__) /__` |  |   /\   |  
// |__) /~~\ \__/ |  | /~~\ \__/ .__/    \__/ | \| |  \/  |___ |  \ .__/ |  |  /~~\  |  
//
//       ___               __                                                           
// |  | |__  |  |\/|  /\  |__)                                                          
// |/\| |___ |  |  | /~~\ |  \                                                                                                                                                                                     
//
// Copyright (c) 2024 Virtual Reality and Visualization Group
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//-----------------------------------------------------------------
//   Authors:        Lucky Chandrautama
//   Date:           2024
//-----------------------------------------------------------------


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
