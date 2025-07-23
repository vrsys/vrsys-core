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
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using VRSYS.Core.Logging;
using VRSYS.Core.Networking;

namespace VRSYS.Core.Navigation
{
    public class NavigationPlatformSeatAssignment : NetworkBehaviour, INavigationPlatformCallbacks
    {
        #region Internal Classes

        [Serializable]
        public class ServerSeat
        {
            public List<ulong> clients = new();
            public Transform transform;
        }

        [Serializable]
        public class ClientSeat : INetworkSerializable
        {
            public int id = -1;
            public Vector3 position = Vector3.zero;
            public Quaternion rotation = Quaternion.identity;
            
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref id);
                serializer.SerializeValue(ref position);
                serializer.SerializeValue(ref rotation);
            }
        }


        #endregion

        #region Member Variables

        [Header("Setup Configuration")]
        [SerializeField]
        private Transform seatsRoot;
        
        [Header("Server Data (DO NOT CHANGE)")]
        [SerializeField]
        private int nextSeatIndex = 0;

        [SerializeField]
        private List<ServerSeat> serverSeats = new();
        
        [Header("Client Data (DO NOT CHANGE)")]
        [SerializeField]
        private ClientSeat clientSeat;

        #endregion

        #region NetworkBehaviour Callbacks

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                if (seatsRoot == null)
                    seatsRoot = transform;
            
                if (seatsRoot != null)
                {
                    serverSeats.Clear();
                    foreach (Transform seatTransform in seatsRoot)
                    {
                        var seat = new ServerSeat();
                        seat.transform = seatTransform;
                        seat.clients = new ();
                        serverSeats.Add(seat);
                    }
                }
            }
            base.OnNetworkSpawn();
        }

        #endregion

        #region NavigationPlatformInterface Callbacks

        public void OnEnterPlatform(NavigationPlatformLink link)
        {
            if (!link.IsOwner)
                return;
            RequestSeatServerRpc(NetworkManager.LocalClientId);
        }

        public void OnLeavePlatform(NavigationPlatformLink link)
        {
            if (IsServer) 
                FindAndRemoveClient(link.OwnerClientId);
        }

        #endregion

        #region Server Methods

        private void UpdateNextSeatIndex()
        {
            int bestSeatIndex = 0;
            int numClientsOnBestSeat = serverSeats[bestSeatIndex].clients.Count;
            for(int i = 0; i < serverSeats.Count; ++i)
            {
                if(serverSeats[i].clients.Count < numClientsOnBestSeat)
                {
                    bestSeatIndex = i;
                    numClientsOnBestSeat = serverSeats[i].clients.Count;
                }
            }
            nextSeatIndex = bestSeatIndex;
        }
        
        private void FindAndRemoveClient(ulong clientId)
        {
            if (!IsServer)
                return;
            var seatIdx = serverSeats.FindIndex(seat => seat.clients.Contains(clientId));
            if (seatIdx >= 0)
                serverSeats[seatIdx].clients.Remove(clientId);
            UpdateNextSeatIndex();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestSeatServerRpc(ulong clientId)
        {
            serverSeats[nextSeatIndex].clients.Add(clientId);
            var seat = new ClientSeat();
            seat.id = nextSeatIndex;
            seat.position = serverSeats[nextSeatIndex].transform.localPosition;
            seat.rotation = serverSeats[nextSeatIndex].transform.localRotation;
            AssignToSeatClientRpc(seat, clientId);
            UpdateNextSeatIndex();
        }

        #endregion

        #region Client Methods

        [ClientRpc]
        private void AssignToSeatClientRpc(ClientSeat seat, ulong clientId)
        {
            if (NetworkManager.LocalClientId != clientId)
                return;
            clientSeat = seat;
            StartCoroutine(DelayAssignSeat(1f));
        }

        IEnumerator DelayAssignSeat(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            var headTracker = NetworkUser.LocalInstance.GetComponentInChildren<TrackedPoseDriver>();
            Vector3 headXZ = Vector3.zero;
            if (headTracker != null)
            {
                ExtendedLogger.LogInfo(GetType().Name, "read from tracker");
                headXZ = headTracker.positionAction.ReadValue<Vector3>();
                headXZ.y = 0;
            }
            else
            {
                ExtendedLogger.LogInfo(GetType().Name, "read from head");
                headXZ = NetworkUser.LocalInstance.head.localPosition;
                headXZ.y = 0;
            }
            
            var centerOffset = headXZ - clientSeat.position;
            ExtendedLogger.LogInfo(GetType().Name, "center offset it: " + centerOffset);
            
            NetworkUser.LocalInstance.transform.localPosition = clientSeat.position - centerOffset;
            NetworkUser.LocalInstance.transform.localRotation = clientSeat.rotation;
        }

        #endregion
    }
}
