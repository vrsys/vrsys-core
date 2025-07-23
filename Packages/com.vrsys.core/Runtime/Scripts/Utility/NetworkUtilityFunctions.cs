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
//   Date:           2023
//-----------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using VRSYS.Core.Networking;

namespace VRSYS.Core.Utility
{
    public static class NetworkUtilityFunctions
    {
        #region Client RPC Params

        public static ClientRpcParams ClientRpcParamsWithTargetUser(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[]{clientId}
                }
            };
        }
    
        public static ClientRpcParams ClientRpcParamsWithTargetUser(ulong[] clientIds)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = clientIds
                }
            };
        }
    
        public static ClientRpcParams ClientRpcParamsWithTargetUser(List<ulong> clientIds)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = clientIds.ToArray()
                }
            };
        }
    
        public static ClientRpcParams ClientRpcParamsWithTargetUser(NetworkList<ulong> clientIds)
        {
            List<ulong> ids = new List<ulong>();

            foreach (var id in clientIds)
            {
                ids.Add(id);
            }
        
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = ids.ToArray()
                }
            };
        }

        #endregion
        
        #region NetworkUser

        /// <summary>
        /// Returns NetworkUser connected with given clientId.
        /// </summary>
        public static NetworkUser GetNetworkUser(ulong clientId)
        {
            List<NetworkUser> networkUsers = UnityEngine.Object.FindObjectsOfType<NetworkUser>().ToList();
            int index =
                networkUsers.FindIndex(user => user.GetComponent<NetworkObject>().OwnerClientId == clientId);

            if (index != -1)
                return networkUsers[index];
            
            return null;
        }

        /// <summary>
        /// Returns NetworkUser connected with given userName.
        /// If there are multiple users with the same userName, the first one is returned.
        /// </summary>
        public static NetworkUser GetNetworkUser(string userName)
        {
            List<NetworkUser> networkUsers = UnityEngine.Object.FindObjectsOfType<NetworkUser>().ToList();
            int index =
                networkUsers.FindIndex(user => user.userName.Value.ToString() == userName);

            if (index != -1)
                return networkUsers[index];
            
            return null;
        }
        
        /// <summary>
        /// Returns all NetworkUsers connected with given userName.
        /// </summary>
        public static List<NetworkUser> GetNetworkUsers(string userName)
        {
            List<NetworkUser> networkUsers = UnityEngine.Object.FindObjectsOfType<NetworkUser>().ToList();
            return networkUsers.FindAll(user => user.userName.Value.ToString() == userName);
        }
        
        /// <summary>
        /// Returns NetworkUser component for every client id.
        /// </summary>
        public static Dictionary<ulong, NetworkUser> GetNetworkUsers()
        {
            List<NetworkUser> networkUsers = UnityEngine.Object.FindObjectsOfType<NetworkUser>().ToList();
            Dictionary<ulong, NetworkUser> networkUsersDict = new Dictionary<ulong, NetworkUser>();

            foreach (var user in networkUsers)
            {
                networkUsersDict.Add(user.GetComponent<NetworkObject>().OwnerClientId, user);
            }

            return networkUsersDict;
        }

        #endregion
    }
}
