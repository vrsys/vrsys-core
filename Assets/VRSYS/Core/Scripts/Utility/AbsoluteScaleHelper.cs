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
    [ExecuteInEditMode]
    public class AbsoluteScaleHelper : MonoBehaviour
    {
        public enum Space
        {
            ParentLocal,
            ParentLossy
        }

        public Vector3 localBaseScale;
        public Vector3 additionalDistance = Vector3.zero;
        public Space space = Space.ParentLossy;

        private Vector3 previousParentScale = Vector3.zero;
        private Vector3 previousLocalBaseScale = Vector3.zero;
        private Vector3 previousAdditionalDistance = Vector3.zero;

        private bool parentScaleChanged => !previousParentScale.EpsilonEquals(space == Space.ParentLossy ? transform.parent.lossyScale : transform.parent.localScale);
        private bool localBaseScaleChanged => !previousLocalBaseScale.EpsilonEquals(localBaseScale);
        private bool additionalDistanceChanged => !previousAdditionalDistance.EpsilonEquals(additionalDistance);

        private void Awake()
        {
            localBaseScale = transform.localScale;
            Apply();
        }

        // Update is called once per frame
        void Update()
        {
            if (localBaseScaleChanged || additionalDistanceChanged || parentScaleChanged)
                Apply();
    }

        void Apply()
        {
            previousParentScale = space == Space.ParentLossy ? transform.parent.lossyScale : transform.parent.localScale;
            previousLocalBaseScale = localBaseScale;
            previousAdditionalDistance = additionalDistance;
            transform.localScale = localBaseScale + additionalDistance.DivideComponents(transform.parent.lossyScale);
        }
    }
}
