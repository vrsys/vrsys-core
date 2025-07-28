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
//   Authors:        Tony Zoeppig
//   Date:           2025
//-----------------------------------------------------------------

using Oculus.Avatar2;
using Button = OVRInput.Button;
using Touch = OVRInput.Touch;

namespace VRSYS.Meta.Avatars
{
    public class VRSYSMetaInputControlDelegate : OvrAvatarInputControlDelegate
    {
        public override bool GetInputControlState(out OvrAvatarInputControlState inputControlState)
        {
            inputControlState = new OvrAvatarInputControlState();
            inputControlState.type = GetControllerType();
            UpdateControllerInput(ref inputControlState.leftControllerState, OVRInput.Controller.LTouch);
            UpdateControllerInput(ref inputControlState.rightControllerState, OVRInput.Controller.RTouch);

            return true;
        }

        private void UpdateControllerInput(ref OvrAvatarControllerState controllerState, OVRInput.Controller controller)
        {
            controllerState.buttonMask = 0;
            controllerState.touchMask = 0;

            // Button Press
            if (OVRInput.Get(Button.One, controller))
            {
                controllerState.buttonMask |= CAPI.ovrAvatar2Button.One;
            }

            if (OVRInput.Get(Button.Two, controller))
            {
                controllerState.buttonMask |= CAPI.ovrAvatar2Button.Two;
            }

            if (OVRInput.Get(Button.Three, controller))
            {
                controllerState.buttonMask |= CAPI.ovrAvatar2Button.Three;
            }

            if (OVRInput.Get(Button.PrimaryThumbstick, controller))
            {
                controllerState.buttonMask |= CAPI.ovrAvatar2Button.Joystick;
            }

            // Button Touch
            if (OVRInput.Get(Touch.One, controller))
            {
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.One;
            }

            if (OVRInput.Get(Touch.Two, controller))
            {
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.Two;
            }

            if (OVRInput.Get(Touch.PrimaryThumbstick, controller))
            {
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.Joystick;
            }

            if (OVRInput.Get(Touch.PrimaryThumbRest, controller))
            {
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.ThumbRest;
            }

            // Trigger
            controllerState.indexTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller);
            if (OVRInput.Get(Touch.PrimaryIndexTrigger, controller))
            {
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.Index;
            }
            else if (controllerState.indexTrigger <= 0f)
            {
                // TODO: Not sure if this is the correct way to do this
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.Pointing;
            }

            // Grip
            controllerState.handTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, controller);

            // Set ThumbUp if no other thumb-touch is set.
            // TODO: Not sure if this is the correct way to do this
            if ((controllerState.touchMask & (CAPI.ovrAvatar2Touch.One | CAPI.ovrAvatar2Touch.Two |
                                              CAPI.ovrAvatar2Touch.Joystick | CAPI.ovrAvatar2Touch.ThumbRest)) == 0)
            {
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.ThumbUp;
            }
        }
    }
}
