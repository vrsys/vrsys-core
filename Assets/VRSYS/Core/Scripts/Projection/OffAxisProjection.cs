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
//   Authors:        Sebastian Muehlhaus, Andre Kunert, Lucky Chandrautama
//   Date:           2023
//-----------------------------------------------------------------

using UnityEngine;
using UnityEngine.Serialization;

namespace VRSYS.Core.Projection {
    
    [RequireComponent(typeof(Camera))]
    public class OffAxisProjection : MonoBehaviour {
        public ProjectionScreen screen;
        public bool autoUpdateProjection = false;
        [FormerlySerializedAs("autoUpdateNearClipPlane")] public bool screenIsNearClipPlane = false;
        public bool flipProjection = false;
        
        private Camera cam;
        public new Camera camera => cam;
        
        private float originalNearClipPlane;
        private bool lastScreenIsNearClipPlane;
        
        private void Awake() {
            cam = GetComponent<Camera>();
            originalNearClipPlane = cam.nearClipPlane;
            lastScreenIsNearClipPlane = screenIsNearClipPlane;
        }

        // Update is called once per frame
        void LateUpdate()
        {
            if (autoUpdateProjection)
                CalculateProjection();
        }

        private void UpdateNearClipPlaneSetting()
        {
            if (screenIsNearClipPlane)
                originalNearClipPlane = cam.nearClipPlane;
            else
                cam.nearClipPlane = originalNearClipPlane;
            lastScreenIsNearClipPlane = screenIsNearClipPlane;
        }

        public void CalculateProjection() {
            if (lastScreenIsNearClipPlane != screenIsNearClipPlane)
                UpdateNearClipPlaneSetting();
            
            transform.localRotation = Quaternion.Inverse(transform.parent.localRotation);

            var screenMatNoScale = Matrix4x4.TRS(screen.transform.position, screen.transform.rotation, Vector3.one);

            var eyePos = transform.position;
            var eyePosSP = screenMatNoScale.inverse * new Vector4(eyePos.x, eyePos.y, eyePos.z, 1f);
            if (flipProjection)
                eyePosSP *= -1;
            
            var near = cam.nearClipPlane;
            if (screenIsNearClipPlane) {
                var s1 = screen.transform.position;
                var s2 = screen.transform.position - screen.transform.forward;
                var camOnScreenForward = Vector3.Project((transform.position - s1), (s2 - s1)) + s1;
                near = Vector3.Distance(screen.transform.position, camOnScreenForward);
                cam.nearClipPlane = near;
            }
            var far = cam.farClipPlane;

            var factor = near / eyePosSP.z;
            var l = (eyePosSP.x - screen.width * 0.5f) * factor;
            var r = (eyePosSP.x + screen.width * 0.5f) * factor;
            var b = (eyePosSP.y - screen.height * 0.5f) * factor;
            var t = (eyePosSP.y + screen.height * 0.5f) * factor;

            cam.projectionMatrix = Matrix4x4.Frustum(l, r, b, t, near, far);
        }
    }
}
