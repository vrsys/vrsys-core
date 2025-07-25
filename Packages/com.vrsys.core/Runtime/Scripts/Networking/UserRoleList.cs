using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VRSYS.Core.Networking
{
    [CreateAssetMenu(menuName = "VRSYS/Core/Scriptable Objects/UserRoleList")]

    public class UserRoleList : ScriptableObject
    {
        #region Singleton

        public static UserRoleList Instance;

        #endregion
        
        #region Public Members

        public List<UserRoleEntry> Roles = new List<UserRoleEntry>();

        #endregion

        #region Scriptable Object Callbacks

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError($"UserRoleList already has been created under: {AssetDatabase.GetAssetPath(Instance)}");
                return;
            }

            Instance = this;
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

            var roles = roleList.Roles;
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
