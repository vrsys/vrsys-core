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
//   Date:           2025
//-----------------------------------------------------------------

#if UNITY_EDITOR

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRSYS.Core.Networking;

namespace VRSYS.Core.Editor
{
    [CustomPropertyDrawer(typeof(UserRoleSelectorAttribute))]
    public class UserRoleSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty roleNameProperty = property.FindPropertyRelative("Name");

            UserRoleList roleList = UserRoleList.Instance;

            if (roleList == null)
            {
                EditorGUI.LabelField(position, "No UserRoleList created in project.");
                return;
            }

            var roles = roleList.RoleEntries;
            if (roles == null)
            {
                EditorGUI.LabelField(position, "No user roles defined.");
                return;
            }

            string currentValue = roleNameProperty.stringValue;
            string[] roleNames = roles.Select(r => r.Name).ToArray();
            int index = Array.IndexOf(roleNames, currentValue);

            if (index < 0)
                index = 0;

            index = EditorGUI.Popup(position, label.text, index, roleNames);

            if (index >= 0 && index < roleNames.Length)
                roleNameProperty.stringValue = roleNames[index];


        }
    }
}

#endif