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

using System;
using UnityEngine;
using VRSYS.Core.Utility;

namespace VRSYS.ProjectionWall
{
    [ExecuteInEditMode]
    public class PortalScaleHelper : MonoBehaviour
    {
        public Transform entranceRoot;
        public ProjectionScreen exitScreen;
        public bool saveCurrentScale = false;
        public bool reset = false;

        private Transform previousEntranceRoot;
        private ProjectionScreen previousExitScreen;

        private Vector3 originalEntranceRootScale;
        private Vector3 originalExitScreenScale;

        // Update is called once per frame
        void Update()
        {
            EvaluatePortalComponentChange();
            EvaluateScaleChange();
        }

        private void EvaluatePortalComponentChange()
        {
            if (previousExitScreen != exitScreen)
            {
                if (exitScreen != null)
                    StoreOriginalExitScreenScale();
                previousExitScreen = exitScreen;
            }
            if (previousEntranceRoot != entranceRoot)
            {
                if (entranceRoot != null)
                    StoreOriginalEntranceRootScale();
                previousEntranceRoot = entranceRoot;
            }
        }

        private void EvaluateScaleChange()
        {
            if ((Vector3.one - transform.localScale).magnitude > 0.01f)
                PopulateScale();
            if (saveCurrentScale)
                Save();
            if (reset)
                Reset();
        }

        private void Save()
        {
            if (exitScreen != null)
                StoreOriginalExitScreenScale();
            if (entranceRoot != null)
                StoreOriginalEntranceRootScale();
            saveCurrentScale = false;
        }

        private void Reset()
        {
            if (entranceRoot != null)
            {
                entranceRoot.ResetScaledPosition(originalEntranceRootScale, Space.Self);
                entranceRoot.localScale = originalEntranceRootScale;
            }
            if (exitScreen != null)
            {
                exitScreen.transform.ResetScaledPosition(originalExitScreenScale, Space.Self);
                exitScreen.transform.localScale = originalExitScreenScale;
            }
            reset = false;
        }

        private void PopulateScale()
        {
            if (exitScreen == null || entranceRoot == null)
                return;
            
            var scaleDifference = transform.localScale - Vector3.one;            
            exitScreen.width = exitScreen.width + scaleDifference.x * exitScreen.width;
            exitScreen.height = exitScreen.height + scaleDifference.y * exitScreen.height;
            entranceRoot.localScale = exitScreen.transform.localScale;

            exitScreen.transform.ApplyScaleDifferenceToPosition(scaleDifference, Space.Self);
            entranceRoot.ApplyScaleDifferenceToPosition(scaleDifference, Space.Self);
            
            transform.localScale = Vector3.one;
        }

        void StoreOriginalEntranceRootScale()
        {
            originalEntranceRootScale = entranceRoot.localScale;
        }

        void StoreOriginalExitScreenScale()
        {
            originalExitScreenScale = exitScreen.transform.localScale;
        }
    }
}