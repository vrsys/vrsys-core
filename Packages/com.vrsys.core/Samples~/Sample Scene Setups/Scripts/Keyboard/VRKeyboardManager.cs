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

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VRSYS.Core.Keyboard
{
    public class VRKeyboardManager : MonoBehaviour
    {
        #region Member Variables
        
        [Header("Keyboard Properties")]
        private string inputText = "";

        public TMP_InputField inputTextField;
        public List<TextMeshProUGUI> inputTextUis;

        [Header("Auto Complete Properties")] 
        public List<string> predefinedWords;
        [HideInInspector] public int maxSuggestions = 5;
        public List<AutoCompleteSuggestion> autoCompleteSuggestions;

        #endregion

        #region Custom Methods

        public void Activate(TMP_InputField inputTextField)
        {
            this.inputTextField = inputTextField;
            inputText = this.inputTextField.text;
            gameObject.SetActive(true);
        }

        public void Activate(TextMeshProUGUI inputTextUi)
        {
            inputTextUis.Add(inputTextUi);
            inputText = inputTextUi.text;
            gameObject.SetActive(true);
        }

        public void Activate(string inputText = "")
        {
            this.inputText = inputText;
            ApplyInputText();
            gameObject.SetActive(true);
        }

        public void GetInput(string character)
        {
            inputText += character;
            ApplyInputText();
            AutoComplete();
        }

        public void GetAction(VRKeyboardActionType actionType)
        {
            switch (actionType)
            {
                case VRKeyboardActionType.Backspace:
                    inputText = inputText.Remove(inputText.Length - 1);
                    ApplyInputText();
                    if (inputText.Length > 0)
                        AutoComplete();
                    break;
                case VRKeyboardActionType.Clear:
                    inputText = "";
                    ApplyInputText();
                    ResetAutoComplete();
                    break;
                case VRKeyboardActionType.Enter:
                    EventSystem.current.SetSelectedGameObject(null);
                    inputText = "";
                    gameObject.SetActive(false);
                    break;
                case VRKeyboardActionType.Shift:
                    VRKeyboardInputKey[] keys = FindObjectsOfType<VRKeyboardInputKey>();
                    foreach (VRKeyboardInputKey key in keys)
                    {
                        key.TriggerCase();
                    }
                    break;
            }
        }

        public void AutoComplete()
        {
            ResetAutoComplete();
            
            List<string> matchingWords = new List<string>();

            foreach (string word in predefinedWords)
            {
                if (word.ToLower().Contains(inputText.ToLower()))
                {
                    matchingWords.Add(word);
                }
            }

            if (matchingWords.Count <= maxSuggestions)
            {
                for (int i = 0; i < matchingWords.Count; i++)
                {
                    autoCompleteSuggestions[i].Setup(matchingWords[i]);
                }
            }
        }

        public void ApplyAutoComplete(string word)
        {
            inputText = word;
            ApplyInputText();
            ResetAutoComplete();
        }

        private void ApplyInputText()
        {
            if (inputTextField != null)
                inputTextField.text = inputText;
            if (inputTextUis != null)
                foreach (var textUi in inputTextUis)
                {
                    textUi.text = inputText;
                }
        }

        public void ResetAutoComplete()
        {
            foreach (AutoCompleteSuggestion s in autoCompleteSuggestions)
            {
                s.ResetSuggestion();
            }
        }
        
        #endregion
    }
}


