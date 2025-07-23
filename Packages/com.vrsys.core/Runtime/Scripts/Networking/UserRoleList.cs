using System.Collections.Generic;
using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Networking
{
    [CreateAssetMenu(menuName = "VRSYS/Core/Scriptable Objects/UserRoleList")]

    public class UserRoleList : ScriptableObject
    {
        #region Member Variables

        [SerializeField] private List<UserRole> userRoles;

        #endregion

        #region Custom Methods

        public List<UserRole> GetUserRoles()
        {
            return userRoles;
        }

        public int GetUserRoleIdx(UserRole userRole)
        {
            int idx = userRoles.FindIndex(a => a == userRole);

            if (idx == -1)
                ExtendedLogger.LogError(GetType().Name, $"User role {userRole.name} not configured in UserRoleList!",
                    this);

            return idx;
        }

        public UserRole GetUserRole(int idx)
        {
            return userRoles[idx];
        }

        #endregion
    }
}
