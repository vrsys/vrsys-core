// VRSYS plugin of Virtual Reality and Visualization Research Group (Bauhaus University Weimar)
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
// Copyright (c) 2023 Virtual Reality and Visualization Research Group
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
//   Authors:        Tony Jan Zoeppig, Sebastian Muehlhaus
//   Date:           2023
//-----------------------------------------------------------------

using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRSYS.Core.Keyboard
{
    public class VRKeyboardInputKey : VRKeyboardKey
    {
        #region Member Variables

        [Header("Key Properties")]
        public string character;
        public bool uppercase;

        public UnityEvent<string> pressed = new UnityEvent<string>();

        [Header("Key UI Elements")] 
        public TextMeshProUGUI lowerCaseText;
        public TextMeshProUGUI upperCaseText;

        #endregion

        #region MonoBehaviour Callbacks

        protected override void Awake()
        {
            base.Awake();

            if (character == "space")
            {
                character = " ";
                lowerCaseText.text = "_";
                upperCaseText.text = "_";
            }
            else
            {
                lowerCaseText.text = character.ToLower();
                upperCaseText.text = character.ToUpper();
            }
        }

        #endregion

        #region Interactable Callbacks

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);

            renderer.material.SetColor("_Color", selectedColor);
            transform.localPosition = new Vector3(transform.localPosition.x, -0.01f, transform.localPosition.z);
            
            if (uppercase)
            {
                pressed.Invoke(character.ToUpper());
            }
            else
            {
                pressed.Invoke(character.ToLower());
            }
            
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            renderer.material.SetColor("_Color", defaultColor);
            transform.localPosition = new Vector3(transform.localPosition.x, 0, transform.localPosition.z);
        }

        #endregion

        #region Custom Methods

        public void TriggerCase()
        {
            uppercase = !uppercase;

            if (uppercase)
            {
                lowerCaseText.gameObject.SetActive(false);
                upperCaseText.gameObject.SetActive(true);
            }
            else
            {
                lowerCaseText.gameObject.SetActive(true);
                upperCaseText.gameObject.SetActive(false);
            }
        }

        #endregion
    }

}

