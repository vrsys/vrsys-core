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
//   Authors:        Tony Jan Zoeppig
//   Date:           2024
//-----------------------------------------------------------------

using Unity.Netcode;
using VRSYS.Core.Logging;
using VRSYS.Core.Projection;

namespace VRSYS.Core.Navigation
{
    public class NavigationPlatformJoystickConnector : NetworkBehaviour, INavigationPlatformCallbacks
    {
        private JoystickNavigation joystickNavigation;
    
        public void OnEnterPlatform(NavigationPlatformLink link)
        {
            if (IsOwner)
            {
                joystickNavigation = GetComponent<JoystickNavigation>();
        
                if (GetComponent<ProjectionWallRuntimeSetup>().isProjectionMaster)
                {
                    joystickNavigation.enabled = true;
                    joystickNavigation.target = link.platformTransform;
            
                    GetPlatformOwnershipServerRpc(link.platformTransform.GetComponent<NetworkObject>());
                }
                else
                {
                    joystickNavigation.enabled = false;
                }
            }
        }

        public void OnLeavePlatform(NavigationPlatformLink link)
        {
            if (IsOwner)
            {
                joystickNavigation.enabled = false;
                joystickNavigation.target = null;
            }
        }

        [ServerRpc]
        public void GetPlatformOwnershipServerRpc(NetworkObjectReference platformRef, ServerRpcParams serverRpcParams = default)
        {
            if (platformRef.TryGet(out NetworkObject networkObject))
            {
                ExtendedLogger.LogInfo(GetType().Name, "Transfering Ownership of: " + networkObject.name + " to " + transform.name);
            
                networkObject.ChangeOwnership(serverRpcParams.Receive.SenderClientId);
            }
        }
    }
}