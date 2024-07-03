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
//   Authors:        Sebastian Muehlhaus, Lucky Chandrautama
//   Date:           2024
//-----------------------------------------------------------------

using UnityEngine;
using UnityEngine.Events;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Interaction
{
    public class BaseInteractable : MonoBehaviour
    {
        public UnityEvent hoverEntered = new();

        public UnityEvent hoverExited = new();

        public UnityEvent selectEntered = new();

        public UnityEvent selectExited = new();

        public bool isHovered { get; private set; }
        public bool isSelected { get; private set; }

        public bool verbose = false;

        // Start is called before the first frame update
        void Start()
        {
            isHovered = false;
            isSelected = false;
        }

        public void OnHoverEntered(BaseInteractor interactor)
        {
            hoverEntered.Invoke();
            isHovered = true;

            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, interactor.gameObject.name + " hover entering " + gameObject.name);

        }

        public void OnHoverExited(BaseInteractor interactor)
        {
            hoverExited.Invoke();
            isHovered = false;

            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, interactor.gameObject.name + " hover exiting " + gameObject.name);


        }

        public void OnSelectEntered(BaseInteractor interactor)
        {
            selectEntered.Invoke();
            isSelected = true;

            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, interactor.gameObject.name + " select entering " + gameObject.name);

        }

        public void OnSelectExited(BaseInteractor interactor)
        {
            selectExited.Invoke();
            isSelected = false;

            if (verbose)
                ExtendedLogger.LogInfo(GetType().Name, interactor.gameObject.name + " select entering " + gameObject.name);
        }
    }


}