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

using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class MouseInteractor : BaseInteractor
{
    [SerializeField] [Tooltip("User prefab camera.")]
    protected Camera userCamera;
   
    [Header("Input Actions")] 
    public InputActionProperty mousePosition;
    public InputActionProperty leftMouseClick;
    public InputActionProperty rightMouseClick;
    public InputActionProperty middleMouseClick;
    

    private void Start()
    {
        if (!isOfflineOrOwner)
            Destroy(this);
        else
        {
            userCamera ??= Camera.main;

        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!isOfflineOrOwner)
            return;

        EvaluateInteraction();
    }

    private void EvaluateInteraction()
    {
        Vector2 mouseVec2 = mousePosition.action.ReadValue<Vector2>();
        Ray ray = userCamera.ScreenPointToRay(mouseVec2);
        var prevHoveredTransform = hoveredTransform;
        var prevSelectedTransform = selectedTransform;
        if (Physics.Raycast(ray, out var hit, 100, layersToInteractWith)){
            if (hoveredTransform is null || hit.transform == hoveredTransform)
            {
                hoveredTransform = hit.transform;
                if (leftMouseClick.action.WasPressedThisFrame())
                    selectedTransform = hoveredTransform;
                else if(leftMouseClick.action.WasReleasedThisFrame())
                    selectedTransform = null;
            }
            else if (hoveredTransform is not null && hit.transform != hoveredTransform)
            {
                if (leftMouseClick.action.WasReleasedThisFrame())
                {
                    hoveredTransform = hit.transform;
                    selectedTransform = null;
                }
            }
        }
        else if (selectedTransform is null || selectedTransform is not null && !leftMouseClick.action.IsPressed())
        {
            hoveredTransform = null;
            selectedTransform = null;
        }
        
        EvaluateHoverStateChange(prevHoveredTransform);
        EvaluateSelectStateChange(prevSelectedTransform);
    }

    private void EvaluateHoverStateChange(Transform prevHoveredTransform)
    {
        if (prevHoveredTransform == hoveredTransform) return;
        if (prevHoveredTransform is not null)
        {
            if (prevHoveredTransform.TryGetComponent<IBaseInteractable>(out var interactable))
            {
                interactable.OnHoverExited(this);
                hoverExited.Invoke();
            }
        }

        if (hoveredTransform is null) return;
        {
            if (!hoveredTransform.TryGetComponent<SimpleMouseInteractable>(out var interactable)) return;
            
            interactable.OnHoverEntered(this);
            hoverEntered.Invoke();
        }
    }

    private void EvaluateSelectStateChange(Transform prevSelectTransform)
    {
        if(selectedTransform is null)
            return;
        
        var isInteractable = selectedTransform.TryGetComponent<SimpleMouseInteractable>(out var interactableComponent);

        if (prevSelectTransform == selectedTransform || !isInteractable) return;
        if (prevSelectTransform is null)
        {
            interactableComponent.OnSelectEntered(this);
            selectEntered.Invoke();

        }
        else
        { 
            interactableComponent.OnSelectExited(this);
            selectExited.Invoke();
        }
    }
    
}
