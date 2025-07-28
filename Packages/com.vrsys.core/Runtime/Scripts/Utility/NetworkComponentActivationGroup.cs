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
//   Authors:        Sebastian Muehlhaus
//   Date:           2023
//-----------------------------------------------------------------

using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Utility
{
    public class NetworkComponentActivationGroup : NetworkBehaviour
    {
        [SerializeField]
        [Tooltip("The index of the currently activated Group Item. Only set this in the editor pre-runtime (subsequent changes in the editor are not networked). Use SetActiveObject() at runtime instead.")]
        private NetworkVariable<int> activeGroupIndex = new (0, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        
        [Serializable]
        public class GroupItem
        {
            public string name;
            public GameObject gameObject;
            public List<Behaviour> affectedBehaviours = new ();
            public List<Behaviour> affectedLocalBehaviours = new ();
            public List<Renderer> affectedRenderers = new ();
            public List<Collider> affectedColliders = new ();
            [HideInInspector] public string initialName;
        }
        
        [SerializeField]
        private List<GroupItem> groups = new ();
    
        [Header("State Change Feedback")]
        [Tooltip("If set to true the state of the mutex group will be appended to the name of the affected GameObjects.")]
        public bool appendStateToName = true;

        [Header("Active State Change (Evaluated only for Owner at Runtime)")]
        [Tooltip("If flipped to true the next object in the list will be activated.")]
        public bool cycleNext = false;
        [Tooltip("(Optional) Input Action to cycle to the next object in the list.")]
        public InputActionProperty cycleNextInput;
        
        [Header("State Feedback UI")]
        public GameObject stateLabel;
        public string stateLabelHint = "(click joystick to switch)";
        private TextMeshProUGUI stateLabelText;
        
        public override void OnNetworkSpawn()
        {
            foreach (var item in groups)
                item.initialName = item.gameObject.name;
            activeGroupIndex.OnValueChanged += OnActiveObjectIndexChanged;
            if(stateLabel != null)
                stateLabelText = stateLabel.GetComponentInChildren<TextMeshProUGUI>();
            if (!IsOwner)
            {
                foreach (var item in groups)
                    item.affectedLocalBehaviours.Clear();
                if(stateLabel != null)
                    stateLabel.SetActive(false);
            }
            UpdateEnabledStates();
            base.OnNetworkSpawn();
        }

        private void Update()
        {
            if (!IsOwner)
                return;

            if (cycleNextInput.action?.WasPressedThisFrame() == true)
                cycleNext = true;
            
            if (cycleNext)
            {
                SetActiveGroup((activeGroupIndex.Value + 1) % groups.Count);
                cycleNext = false;
            }
        }

        private void OnActiveObjectIndexChanged(int previousValue, int newValue)
        {
            if (previousValue == newValue)
                return;
            
            _SetActiveObject(newValue);
        }
    
        private void UpdateEnabledStates()
        {
            for (int idx = 0; idx < groups.Count; idx++)
            {
                if(idx != activeGroupIndex.Value)
                    SetEnabledStates(idx);
            }
            
            SetEnabledStates(activeGroupIndex.Value);
            
            if (stateLabel != null)
            {
                stateLabelText.text = groups[activeGroupIndex.Value].name + (stateLabelHint.Length > 0 ? " " + stateLabelHint: "");
            }
        }
        
        private void SetEnabledStates(int idx)
        {
            foreach (var b in groups[idx].affectedBehaviours)
                b.enabled = idx == activeGroupIndex.Value;
            foreach (var r in groups[idx].affectedRenderers)
                r.enabled = idx == activeGroupIndex.Value;
            foreach (var c in groups[idx].affectedColliders)
                c.enabled = idx == activeGroupIndex.Value;
            if (IsOwner)
            {
                foreach(var b in groups[idx].affectedLocalBehaviours)
                    b.enabled = idx == activeGroupIndex.Value;
            }
            if (appendStateToName)
            {
                var stateLabel = (idx == activeGroupIndex.Value ? " [active]" : " [inactive]");
                groups[idx].gameObject.name = groups[idx].initialName + stateLabel;
            }
        }
        
        public void SetActiveGroup(int index)
        {
            if(!IsOwner && !IsServer)
            {
                ExtendedLogger.LogError(GetType().Name, "only the owner & the server can set the active object!");
                return;
            }
            
            if (index < 0 || index >= groups.Count)
            {
                ExtendedLogger.LogError(GetType().Name, "index out of range!");
                return;
            }
    
            activeGroupIndex.Value = index;
            
            _SetActiveObject(index);
        }
    
        public void SetActiveGroup(string objectName)
        {
            SetActiveGroup(groups.FindIndex(item => item.initialName == objectName));
        }

        public void SetActiveGroup(GameObject go)
        {
            SetActiveGroup(groups.FindIndex(item => item.gameObject == go));
        }
    
        private void _SetActiveObject(int index)
        {
            UpdateEnabledStates();
        }
    }
}
