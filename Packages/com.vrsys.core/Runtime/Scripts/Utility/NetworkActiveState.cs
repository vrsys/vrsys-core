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

using Unity.Netcode;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Utility
{
    public class NetworkActiveState : NetworkBehaviour
    {
        public NetworkVariable<bool> activeState = new();

        public UnityEvent<bool> activeStateChanged = new();

        public UnityEvent<bool> inactiveStateChanged = new();

        public InputActionProperty toggleBinding;

        public bool enforceOwnership = false;

        public bool inputBlocked = false;

        public bool hasStateAuthority => !inputBlocked && (!enforceOwnership || IsOwner);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            activeState.OnValueChanged += (value, newValue) => ApplyActiveState(newValue);
            ApplyActiveState(activeState.Value);
        }

        private void Update()
        {
            if (hasStateAuthority && toggleBinding.action != null && toggleBinding.action.WasPressedThisFrame())
                SetActiveState(!activeState.Value);
        }

        private void ApplyActiveState(bool state)
        {
            activeStateChanged.Invoke(state);
            inactiveStateChanged.Invoke(!state);
        }

        public void SetActiveState(bool newState)
        {
            if (!hasStateAuthority)
            {
                ExtendedLogger.LogError(
                    GetType().Name,
                    "Only owners may request active state change " +
                    "on this component (check 'enforceOwnership' property)."
                );
                return;
            }

            SetActiveStateServerRpc(newState);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetActiveStateServerRpc(bool newState)
        {
            activeState.Value = newState;
            if (IsHost)
                ApplyActiveState(newState);
        }
    }
}