using System;
using System.Collections.Generic;
using System.Linq;

namespace MyERP.HumanResources.DomainServices;

/// <summary>
/// Validates user access when linking, unlinking, or updating User accounts on Employee records.
/// Enforces parity with ERPNext PR #61894 / commit c80bd8912b (validate_user_access):
/// Only administrators (System Manager, Administrator, admin) may link, unlink, or update a User holding an admin role.
/// Non-admin users (e.g. HR User, HR Manager) are permitted to link/unlink operational users (Employees, Sales, Accounts)
/// but are prevented from modifying user links associated with administrative accounts.
/// </summary>
public static class UserAccessValidator
{
    private static readonly HashSet<string> AdminRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Administrator",
        "System Manager",
        "admin"
    };

    /// <summary>
    /// Checks whether the role set has administrator privileges.
    /// </summary>
    public static bool IsAdminUser(IEnumerable<string>? roles)
    {
        if (roles == null) return false;
        return roles.Any(r => AdminRoles.Contains(r));
    }

    /// <summary>
    /// Evaluates if an editor can link, unlink, or update an Employee record associated with target user.
    /// Returns true if permitted; false if editor lacks privileges to modify the target user's link.
    /// </summary>
    public static bool CanModifyUserLink(IEnumerable<string>? editorRoles, IEnumerable<string>? targetUserRoles)
    {
        var editorRoleSet = editorRoles != null
            ? new HashSet<string>(editorRoles, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Administrator / System Manager / admin can link/unlink any user
        if (editorRoleSet.Overlaps(AdminRoles))
        {
            return true;
        }

        if (targetUserRoles == null)
        {
            return true;
        }

        // Check if target user holds any admin role that editor lacks
        var targetRoleSet = new HashSet<string>(targetUserRoles, StringComparer.OrdinalIgnoreCase);
        targetRoleSet.IntersectWith(AdminRoles);
        targetRoleSet.ExceptWith(editorRoleSet);

        return targetRoleSet.Count == 0;
    }
}
