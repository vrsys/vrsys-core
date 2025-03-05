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
//   Date:           2023
//-----------------------------------------------------------------

using System;
using Unity.Netcode;
using UnityEngine;
using VRSYS.Core.Utility;

namespace VRSYS.Core.Interaction
{
    [RequireComponent(typeof(LineRenderer))]
    public class RaySerializer : NetworkBehaviour
    {
        #region Serialized Data Type

        public struct RayData : INetworkSerializable, System.IEquatable<RayData>
        {
            public bool enabled;
            public bool useEndPointsOnly;
            public Vector3 startPosition;
            public Vector3 endPosition;
            public Vector3[] rayPositions;
            public Color color;

            public bool Equals(RayData other)
            {
                return enabled == other.enabled &&
                       startPosition.Equals(other.startPosition, epsilon: 0.001f) &&
                       endPosition.Equals(other.endPosition, epsilon: 0.001f) &&
                       color == other.color;
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                if (serializer.IsReader)
                {
                    var reader = serializer.GetFastBufferReader();
                    reader.ReadValueSafe(out enabled);
                    reader.ReadValueSafe(out useEndPointsOnly);
                    reader.ReadValueSafe(out startPosition);
                    reader.ReadValueSafe(out endPosition);
                    reader.ReadValueSafe(out color);
                }
                else
                {
                    var writer = serializer.GetFastBufferWriter();
                    writer.WriteValueSafe(enabled);
                    writer.WriteValueSafe(useEndPointsOnly);
                    writer.WriteValueSafe(startPosition);
                    writer.WriteValueSafe(endPosition);
                    writer.WriteValueSafe(color);
                }
                
                // Array serialization according to: https://docs-multiplayer.unity3d.com/netcode/1.4.0/advanced-topics/serialization/inetworkserializable/
                if (!useEndPointsOnly)
                {
                    int length = 0;
                    if (!serializer.IsReader && rayPositions != null)
                        length = rayPositions.Length;
                
                    serializer.SerializeValue(ref length);

                    if (serializer.IsReader)
                        rayPositions = new Vector3[length];
                
                    for(int i = 0; i < length; i++)
                        serializer.SerializeValue(ref rayPositions[i]);
                }
            }
        }

        #endregion

        #region Member Variables

        public bool useEndPointsOnly = false;

        private LineRenderer ray;
        private NetworkVariable<RayData> rayData = new NetworkVariable<RayData>(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
        #endregion

        #region NetworkBehaviour Callbacks

        public override void OnNetworkSpawn()
        {
            ray = GetComponent<LineRenderer>();
            ray.positionCount = 2;
            
            if (IsOwner)
                return;
            
            ApplyRayUpdates();
        }

        #endregion
    
        #region MonoBehaviour Callbacks
        
        private bool SerializeRayUpdates(out RayData data)
        {
            data = new RayData();
            if (ray.enabled && ray.positionCount >= 2)
            {
                data.enabled = true;
                data.useEndPointsOnly = useEndPointsOnly;
                data.startPosition = ray.GetPosition(0);
                data.endPosition = ray.GetPosition(ray.positionCount - 1);
                if (useEndPointsOnly)
                    data.rayPositions = Array.Empty<Vector3>();
                else
                {
                    Vector3[] rayPositions = new Vector3[ray.positionCount];
                    
                    for (int i = 0; i < ray.positionCount; i++)
                        rayPositions[i] = ray.GetPosition(i);

                    data.rayPositions = rayPositions;
                }
                data.color = ray.startColor;
                return true;
            }
            else if (!ray.enabled && rayData.Value.enabled)
            {
                data.enabled = false;
                return true;
            }
            return false;
        }

        private void ApplyRayUpdates()
        {
            ray.enabled = rayData.Value.enabled;
            if (ray.enabled)
            {
                if (rayData.Value.useEndPointsOnly)
                {
                    ray.positionCount = 2;
                    ray.SetPosition(0, rayData.Value.startPosition);
                    ray.SetPosition(1, rayData.Value.endPosition);
                }
                else
                {
                    ray.positionCount = rayData.Value.rayPositions.Length;

                    for (int i = 0; i < ray.positionCount; i++)
                    {
                        ray.SetPosition(i, rayData.Value.rayPositions[i]);
                    }
                }
                
                ray.startColor = rayData.Value.color;
                ray.endColor = rayData.Value.color;
            }
        }

        private void Update()
        {
            if (IsOwner)
            {
                RayData newData;
                if(SerializeRayUpdates(out newData))
                    rayData.Value = newData;
            }
            else
            {
                ApplyRayUpdates();
            }
        }
    
        #endregion
    }
}