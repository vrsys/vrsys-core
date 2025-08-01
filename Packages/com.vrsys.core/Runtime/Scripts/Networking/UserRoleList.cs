using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace VRSYS.Core.Networking
{
    [CreateAssetMenu(menuName = "VRSYS/Core/Scriptable Objects/UserRoleList")]

    public class UserRoleList : ScriptableObject
    {
        #region Singleton

        private static UserRoleList _instance;

        public static UserRoleList Instance
        {
            get
            {
                if (_instance == null)
                {
                    var assets = AssetDatabase.FindAssets($"t: {typeof(UserRoleList)}");
                    if (assets.Length == 0) 
                        return null;
                    string path = AssetDatabase.GUIDToAssetPath(assets[0]);
                    _instance = AssetDatabase.LoadAssetAtPath<UserRoleList>(path);
                }

                return _instance;
            }
        }

        #endregion
        
        #region Public Members

        public List<UserRoleEntry> RoleEntries = new List<UserRoleEntry>();

        public List<UserRole> UserRoles
        {
            get
            {
                List<UserRole> roles = new List<UserRole>();

                foreach (var entry in RoleEntries)
                {
                    roles.Add(new UserRole(entry.Name));
                }

                return roles;
            }
        }

        #endregion
    }
    
    public class UserRoleSelectorAttribute : PropertyAttribute { }
    
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
