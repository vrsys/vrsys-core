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
using UnityEngine;


namespace VRSYS.Core.Interaction
{

    [RequireComponent(typeof(RayControllerInteractor))]
    [RequireComponent(typeof(LineRenderer))]
    public class RayControllerInteractorLineVisual : MonoBehaviour
    {
        private LineRenderer lineRenderer;
        private RayControllerInteractor interactor;

        [SerializeField]
        [Range(0.0001f, 0.05f)]
        float lineWidth = 0.005f;

        [SerializeField]
        AnimationCurve widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [SerializeField]
        Gradient validHoverColorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.blue, 0f), new GradientColorKey(Color.blue, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) },
        };

        [SerializeField]
        Gradient unhitColorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) },
        };

        [SerializeField]
        Gradient selectedColorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.green, 0f), new GradientColorKey(Color.green, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) },
        };


        private void Awake()
        {
            interactor = GetComponent<RayControllerInteractor>();

            Setup();
        }

        // Start is called before the first frame update
        void Start()
        {
        }

        // Update is called once per frame
        void Update()
        {
            UpdateLineGeometry();
        }

        #region Custom Methods
        private void UpdateLineGeometry()
        {
            lineRenderer.SetPosition(0, interactor.RayStartPoint);
            lineRenderer.SetPosition(1, interactor.hitPoint);

        }



        private void Setup()
        {
            InteractorEventSetup();
            LineRendererSetup();
        }
        private void InteractorEventSetup()
        {

            interactor.hoverEntered.AddListener(HoverEnteredVisualFeedback);
            interactor.hoverExited.AddListener(HoverExitedVisualFeedback);
            interactor.selectEntered.AddListener(SelectionEnteredVisualFeedback);
            interactor.selectExited.AddListener(SelectionExitedVisualFeedback);

        }

        private void LineRendererSetup()
        {


            lineRenderer = GetComponent<LineRenderer>();

            lineRenderer.useWorldSpace = true;

            lineRenderer.SetPosition(0, interactor.RayStartPoint);
            lineRenderer.SetPosition(1, interactor.RayEndPoint);

            lineRenderer.widthMultiplier = lineWidth;
            lineRenderer.widthCurve = widthCurve;

        }

        private void HoverEnteredVisualFeedback()
        {

            lineRenderer.colorGradient = validHoverColorGradient;

        }
        private void HoverExitedVisualFeedback()
        {
            lineRenderer.colorGradient = unhitColorGradient;

        }
        private void SelectionEnteredVisualFeedback()
        {
            lineRenderer.colorGradient = selectedColorGradient;

        }
        private void SelectionExitedVisualFeedback()
        {
            lineRenderer.colorGradient = validHoverColorGradient;

        }

        #endregion
    }

}

