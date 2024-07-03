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
//   Authors:        Sebastian Muehlhaus. Lucky Chandrautama
//   Date:           2024
//-----------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace VRSYS.Core.Interaction
{
    public class BaseRayInteractor : BaseInteractor
    {
        [Header("Ray Interactor Variables")]
        public float rayLength;
        public Vector3 hitPoint;



        protected void EvaluateRaySelection(Ray ray, InputAction action)
        {
            if (Physics.Raycast(ray, out var hit, rayLength, LayersToInteractWith))
            {

                Transform target;
                target = hit.transform;
                hitPoint = hit.point;

                if (hoveredTransform is null || target == hoveredTransform)
                {
                    hoveredTransform = target;
                    if (action.WasPressedThisFrame())
                        selectedTransform = hoveredTransform;
                    else if (action.WasReleasedThisFrame())
                        selectedTransform = null;
                }
                else if (hoveredTransform is not null && target != hoveredTransform)
                {
                    if (action.WasReleasedThisFrame())
                    {
                        hoveredTransform = target;
                        selectedTransform = null;
                    }
                }
            }
            else
            {

                hitPoint = ray.origin + ray.direction * rayLength;

                if (!action.IsPressed())
                {
                    hoveredTransform = null;
                    selectedTransform = null;
                }
            }


        }
    }


}
