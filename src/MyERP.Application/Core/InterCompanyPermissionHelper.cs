using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Authorization;

namespace MyERP.Core;

public static class InterCompanyPermissionHelper
{
    public static async Task<(bool CanViewCounterpart, bool CanCreateCounterpart)> GetCounterpartPermissionsAsync(
        IAuthorizationService? authorizationService,
        string viewPermission,
        string createPermission)
    {
        if (authorizationService == null)
        {
            return (true, true);
        }

        if (authorizationService is IAbpAuthorizationService abpAuth)
        {
            var canView = await abpAuth.IsGrantedAsync(viewPermission);
            var canCreate = await abpAuth.IsGrantedAsync(createPermission);
            return (canView, canCreate);
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var viewResult = await authorizationService.AuthorizeAsync(principal, null, viewPermission);
        var createResult = await authorizationService.AuthorizeAsync(principal, null, createPermission);
        return (viewResult.Succeeded, createResult.Succeeded);
    }
}
