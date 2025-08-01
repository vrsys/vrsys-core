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
//   Authors:        Tony Jan Zoeppig, Sebastian Muehlhaus, Ephraim Schott
//   Date:           2025
//-----------------------------------------------------------------

using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace VRSYS.Core.Interaction
{
    public class InteractableNetworkState : NetworkBehaviour
    {
        #region Member Variables

        [HideInInspector] public NetworkVariable<bool> isGrabbed = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Header("Debugging")]
        [SerializeField] private bool _getOwnership = false;

        #endregion

        #region MonoBehaviour Callbacks

        private void Update()
        {
            if (_getOwnership)
            {
                SwitchOwnerRpc(NetworkManager.LocalClientId);
                _getOwnership = false;
            }
        }

        #endregion

        #region Custom Methods

        public void UpdateIsGrabbed(bool isGrabbed)
        {
            if(!IsOwner)
                SwitchOwnerRpc(NetworkManager.LocalClientId);
            
            UpdateIsGrabbedRpc(isGrabbed);
        }

        #endregion

        #region RPCs

        [Rpc(SendTo.Server)]
        private void UpdateIsGrabbedRpc(bool isGrabbed)
        {
            this.isGrabbed.Value = isGrabbed;
        }
        
        [Rpc(SendTo.Server)]
        private void SwitchOwnerRpc(ulong newOwner)
        {
            NetworkObject.ChangeOwnership(newOwner);
        }

        #endregion
    }
}