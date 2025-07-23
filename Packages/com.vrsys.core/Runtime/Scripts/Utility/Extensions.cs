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
// Copyright (c) 2022 Virtual Reality and Visualization Research Group
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

using UnityEngine;

namespace VRSYS.Core.Utility
{
    public static class Extensions
    {
        public static void ApplyScaleDifferenceToPosition(this Transform t, Vector3 scaleDifference, Space space = Space.World)
        {
            if(space == Space.Self)
                t.localPosition = t.localPosition + t.localPosition.MultiplyComponents(scaleDifference);
            else
                t.position = t.position + t.position.MultiplyComponents(scaleDifference);
        }

        public static void ResetScaledPosition(this Transform t, Vector3 originalScale, Space space = Space.World)
        {
            var scaleRatio = originalScale.DivideComponents(space == Space.Self ? t.localScale : t.position);
            if (space == Space.Self)
                t.localPosition = t.localPosition.MultiplyComponents(scaleRatio);
            else
                t.position = t.position.MultiplyComponents(scaleRatio);
        }

        public static Transform SetLocalScaleX(this Transform t, float x)
        {
            t.localScale = t.localScale.SetX(x);
            return t;
        }

        public static Transform SetLocalScaleY(this Transform t, float y)
        {
            t.localScale = t.localScale.SetY(y);
            return t;
        }

        public static Transform SetLocalScaleZ(this Transform t, float z)
        {
            t.localScale = t.localScale.SetZ(z);
            return t;
        }

        public static Vector3 SetX(this Vector3 v, float x)
        {
            v = new Vector3(x, v.y, v.z);
            return v;
        }
        
        public static Vector3 SetY(this Vector3 v, float y)
        {
            v = new Vector3(v.x, y, v.z);
            return v;
        }
        
        public static Vector3 SetZ(this Vector3 v, float z)
        {
            v = new Vector3(v.x, v.y, z);
            return v;
        }

        public static Vector3 MultiplyComponents(this Vector3 v, Vector3 factors)
        {
            v = new Vector3(v.x * factors.x, v.y * factors.y, v.z * factors.z);
            return v;
        }

        public static Vector3 DivideComponents(this Vector3 v, Vector3 divisiors)
        {
            v = new Vector3(v.x / divisiors.x, v.y / divisiors.y, v.z / divisiors.z);
            return v;
        }

        public static bool EpsilonEquals(this Vector3 a, Vector3 b, float epsilon = 0.001f) 
        {
            return (a - b).magnitude < epsilon;
        }
    }
}