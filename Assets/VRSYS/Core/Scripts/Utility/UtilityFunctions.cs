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
//   Authors:        Sebastian Muehlhaus, Tony Zoeppig
//   Date:           2023
//-----------------------------------------------------------------

using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRSYS.Core.Utility
{
    public static class UtilityFunctions
    {
        public static Matrix4x4 GetTransformationMatrix(Transform t, bool world = true)
        {
            if (world)
            {
                return Matrix4x4.TRS(t.position, t.rotation, t.lossyScale);
            }
            else
            {
                return Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
            }
        }
        
        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach(Transform transform in go.transform)
            {
                SetLayerRecursively(transform.gameObject, layer);
            }
        }
        
        public static GameObject FindRecursiveInScene(string name, Scene? scn = null)
        {
            Scene scene;

            if (scn == null)
                scene = SceneManager.GetActiveScene();
            else
                scene = (Scene)scn;

            var sceneRoots = scene.GetRootGameObjects();

            GameObject result = null;
            foreach (var root in sceneRoots)
            {
                if (root.name.Equals(name)) return root;

                result = FindRecursive(root, name);

                if (result) break;
            }

            return result;
        }

        public static GameObject FindRecursive(GameObject entryGO, string name)
        {
            GameObject result = null;            
            foreach (Transform child in entryGO.transform)
            {
                if (child.name.Equals(name))
                    return child.gameObject;

                result = FindRecursive(child.gameObject, name);

                if (result != null)
                    break;
            }
            return result;
        }

        public static Vector3 ClosestPointOnPlane(Vector3 planeOffset, Vector3 planeNormal, Vector3 point)
            => point + DistanceFromPlane(planeOffset, planeNormal, point) * planeNormal;

        public static float DistanceFromPlane(Vector3 planeOffset, Vector3 planeNormal, Vector3 point)
            => Vector3.Dot(planeOffset - point, planeNormal);

        public static bool Equals(this Quaternion quatA, Quaternion value, float epsilon)
            => 1 - Mathf.Abs(Quaternion.Dot(quatA, value)) < epsilon;

        public static bool Equals(this Vector3 v1, Vector3 v2, float epsilon)
            => (v1 - v2).magnitude < epsilon;

        public static bool Equals(this float a, float b, float epsilon)
            => Mathf.Abs(a - b) < epsilon;
    }
}