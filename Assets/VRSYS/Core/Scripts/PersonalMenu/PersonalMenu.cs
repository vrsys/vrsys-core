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
//   Authors:        Tony Jan Zoeppig, Lucky Chandrautama
//   Date:           2023
//-----------------------------------------------------------------

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VRSYS.Core.PersonalMenu
{
    public class PersonalMenu : NetworkBehaviour
    {
        #region Member Variables

        [Header("Personal Menu Properties")] 
        
        [Tooltip("Used to distinguish Canvas UI type of PersonalMenu-Canvas between Desktop (Screen Space-Overlay) and HMD (World Space) user")] 
        [SerializeField] private bool desktopMode = false;
        
        [Tooltip("Only used when isNetworked set to false")]  
        [SerializeField]  InputActionProperty toggleMenuAction;

        public GameObject personalMenu;
        private NetworkVariable<bool> isNetworkActivated = new (false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [Header("Submenu Properties")] 
        public Transform submenuParent;
        public Transform submenuIconParent;
        public List<PersonalSubmenu> submenuPrefabs;
        public int defaultSubmenu = -1;
        private PersonalSubmenu currentSubmenu;
        
        private List<PersonalSubmenu> submenus = new List<PersonalSubmenu>();

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            if (!desktopMode)
            {
                if (IsOwner)
                {
                    InitializeSubMenus();
                }
                else
                {
                    isNetworkActivated.OnValueChanged += OnIsActivatedChanged;
                }
            
                SetIsActive(isNetworkActivated.Value);
            }
            else
            {
                if (IsOwner)
                {
                    InitializeSubMenus();
                    toggleMenuAction.action.performed += ToggleIsActivated;
                }
                
                SetIsActive(false);
            }
        }

        #endregion

        #region Custom Methods

        private void InitializeSubMenus()
        {
            if (submenuPrefabs.Count == 0)
                return;

            for (int i = 0; i < submenuPrefabs.Count; i++)
            {
                PersonalSubmenu newSubmenu = Instantiate(submenuPrefabs[i], submenuParent);
                submenus.Add(newSubmenu);
                newSubmenu.Activate();

                GameObject submenuIcon = Instantiate(newSubmenu.iconPrefab, submenuIconParent);
                submenuIcon.GetComponent<Button>().onClick.AddListener(newSubmenu.Select);

                newSubmenu.icon = submenuIcon.GetComponent<PersonalSubmenuIcon>();
            }

            if (defaultSubmenu < submenus.Count && defaultSubmenu >= 0)
            {
                currentSubmenu = submenus[defaultSubmenu];
                submenus[defaultSubmenu].Select();
            }
            else
            {
                currentSubmenu = submenus[0];
                submenus[0].Select();
            }
        }

        public void ToggleIsActivated()
        {
            if (IsOwner)
            {
                isNetworkActivated.Value = !isNetworkActivated.Value;
                personalMenu.SetActive(isNetworkActivated.Value);
            }
        }
        
        private void ToggleIsActivated(InputAction.CallbackContext obj)
        {
            personalMenu.SetActive(!personalMenu.activeSelf);
        }
        
        private void SetIsActive(bool isActive)
        {
            personalMenu.SetActive(isActive);
        }

        public void SwitchSubmenu(PersonalSubmenu submenu)
        {
            if (submenu != currentSubmenu)
            {
                currentSubmenu.Deselect();
                currentSubmenu = submenu;
            }
        }

        #endregion

        #region NetworkVariable Callbacks

        private void OnIsActivatedChanged(bool previousvalue, bool newvalue)
        {
            SetIsActive(newvalue);
        }

        #endregion
    }
}