using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace VRSYS.Core.Networking
{
    public class UserRoleList : ScriptableObject
    {
        #region Singleton

        public static UserRoleList Instance;

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

        #region Scriptable Object Callbacks

        private void Awake()
        {
            Instance = this;
        }

        #endregion
    }
    
    public class UserRoleSelectorAttribute : PropertyAttribute
    {
    }
}
