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
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using VRSYS.Core.Logging;

/// <summary>
/// Base class for all non-XR Interactor classes
/// </summary>
public abstract class BaseInteractor : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The layers that the this interactor will interact with")]
    protected LayerMask layersToInteractWith;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("The transform that the mouse is currently hovering over")]
    protected Transform hoveredTransform;
    public bool isHovering => hoveredTransform is not null;

    [SerializeField]
    [Tooltip("The transform that the mouse is currently selecting")]
    protected Transform selectedTransform;
    public bool isSelecting => selectedTransform is not null;
    public bool verbose = false;

    [Header("Base Interactor Unity Events")]

    public UnityEvent hoverEntered = new();

    public UnityEvent hoverExited = new();

    public UnityEvent selectEntered = new();

    public UnityEvent selectExited = new();


    protected bool? isOfflineOrOwner_;
    protected bool isOfflineOrOwner
    {
        get
        {
            if (!isOfflineOrOwner_.HasValue)
            {
                if (GetComponent<NetworkObject>() is not null)
                    isOfflineOrOwner_ = GetComponent<NetworkObject>().IsOwner;
                else
                    isOfflineOrOwner_ = true;
            }
            return isOfflineOrOwner_.Value;
        }
    }

    public LayerMask LayersToInteractWith { get => layersToInteractWith; set => layersToInteractWith = value; }

    protected void EvaluateHoverStateChange(Transform prevHoveredTransform)
    {
        if (prevHoveredTransform == hoveredTransform) return;
        if (prevHoveredTransform is not null)
        {
            if (prevHoveredTransform.TryGetComponent<BaseInteractable>(out var interactable))
            {
                interactable.OnHoverExited(this);
                hoverExited.Invoke();
            }
        }

        if (hoveredTransform is null) return;
        {
            if (!hoveredTransform.TryGetComponent<BaseInteractable>(out var interactable)) return;

            interactable.OnHoverEntered(this);
            hoverEntered.Invoke();
        }
    }

    protected void EvaluateSelectStateChange(Transform prevSelectTransform)
    {
        if (selectedTransform is null)
            return;

        var isInteractable = selectedTransform.TryGetComponent<BaseInteractable>(out var interactableComponent);

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
