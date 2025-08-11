using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRSYS.Core.Networking
{
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
                    var results = Resources.LoadAll<UserRoleList>("");

                    if (results.Length > 0)
                        _instance = results[0];
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
    
    public class UserRoleSelectorAttribute : PropertyAttribute
    {
    }
}
