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
// Copyright (c) 2023 Virtual Reality and Visualization Group
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
//   Authors:        Sebastian Muehlhaus, Tony Jan Zoeppig
//   Date:           2023
//-----------------------------------------------------------------

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Avatar
{
    public class AvatarHMDAnatomy : AvatarAnatomy
    {
        #region Properties

        public Transform body;
        public Transform leftHand { get; private set; }

        public Transform rightHand { get; private set; }

        [SerializeField, Tooltip("Transform that receives tracking for left hand. Used when no XRInputModalityManager is attached.")] 
        private Transform _defaultLeftHand;
        
        [SerializeField, Tooltip("Transform that receives tracking for right hand. Used when no XRInputModalityManager is attached.")] 
        private Transform _defaultRightHand;

        [SerializeField, Tooltip("Transform that receives tracking for left controller. Used when XRInputModalityManager is attached and controllers are active.")] 
        private Transform _leftController;
        
        [SerializeField, Tooltip("Transform that receives tracking for right controller. Used when XRInputModalityManager is attached and controllers are active.")] 
        private Transform _rightController;

        [SerializeField, Tooltip("Transform that receives tracking for left tracked hand. Used when XRInputModalityManager is attached and tracked hands are active.")] 
        private Transform _leftTrackedHand;
        
        [SerializeField, Tooltip("Transform that receives tracking for right tracked hand. Used when XRInputModalityManager is attached and tracked hands are active.")] 
        private Transform _rightTrackedHand;

        private XRInputModalityManager _modalityManager;

        #endregion

        #region MonoBehaviour Methods

        private void Awake()
        {
            _modalityManager = GetComponent<XRInputModalityManager>();

            if (_modalityManager == null)
            {
                ExtendedLogger.LogWarning(GetType().Name, "No XRInputModalityManager attached. LeftHand and rightHand fallback to assigned default transforms.");

                leftHand = _defaultLeftHand;
                rightHand = _defaultRightHand;
                
                return;
            }
            
            _modalityManager.motionControllerModeStarted.AddListener(MotionControllersActivated);
            _modalityManager.motionControllerModeEnded.AddListener(MotionControllersDeactivated);
            
            _modalityManager.trackedHandModeStarted.AddListener(TrackedHandsActivated);
            _modalityManager.trackedHandModeEnded.AddListener(TrackedHandsDeactivated);
        }

        #endregion

        #region Private Methods

        private void MotionControllersActivated()
        {
            leftHand = _leftController;
            rightHand = _rightController;
        }

        private void MotionControllersDeactivated()
        {
            leftHand = null;
            rightHand = null;
        }

        private void TrackedHandsActivated()
        {
            leftHand = _leftTrackedHand;
            rightHand = _rightTrackedHand;
        }

        private void TrackedHandsDeactivated()
        {
            leftHand = null;
            rightHand = null;
        }

        #endregion
    }
}

