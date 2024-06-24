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
//   Authors:        Sebastian Muehlhaus, Andre Kunert
//   Date:           2022
//-----------------------------------------------------------------

using UnityEngine;
using VRSYS.Core.Utility;

namespace VRSYS.ProjectionWall
{
    [ExecuteInEditMode]
    public class ProjectionScreen : MonoBehaviour
    {
        public float width
        {
            get => transform.localScale.x;
            set => transform.SetLocalScaleX(value);
        }
        
        public float height
        {
            get => transform.localScale.y;
            set => transform.SetLocalScaleY(value);
        }
        
        public bool drawGizmoFlag = true;
        
        public Vector3 topLeftCorner => transform.TransformPoint(new Vector3(-0.5f, 0.5f, 0f));
        public Vector3 topRightCorner => transform.TransformPoint(new Vector3(0.5f, 0.5f, 0f));
        public Vector3 bottomRightCorner => transform.TransformPoint(new Vector3(0.5f, -0.5f, 0f));
        public Vector3 bottomLeftCorner => transform.TransformPoint(new Vector3(-0.5f, -0.5f, 0f));

        private void OnDrawGizmos()
        {
            if (drawGizmoFlag)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(bottomLeftCorner, topLeftCorner);
                Gizmos.DrawLine(topLeftCorner, topRightCorner);
                Gizmos.DrawLine(topRightCorner, bottomRightCorner);
                Gizmos.DrawLine(bottomRightCorner, bottomLeftCorner);
            }
        }
    }
}
