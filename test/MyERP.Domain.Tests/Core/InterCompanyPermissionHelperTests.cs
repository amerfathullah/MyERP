using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Core;

public class InterCompanyPermissionHelperTests
{
    [Fact]
    public async Task GetCounterpartPermissionsAsync_NullAuthService_DefaultsToAllowed()
    {
        var (canView, canCreate) = await InterCompanyPermissionHelper.GetCounterpartPermissionsAsync(
            null, "MyERP.SalesInvoices", "MyERP.SalesInvoices.Create");

        canView.ShouldBeTrue();
        canCreate.ShouldBeTrue();
    }

    [Fact]
    public async Task GetCounterpartPermissionsAsync_HonorsCounterpartViewAndCreateGrants()
    {
        var authService = Substitute.For<IAuthorizationService>();
        authService.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), "MyERP.SalesInvoices")
            .Returns(AuthorizationResult.Failed());
        authService.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), "MyERP.SalesInvoices.Create")
            .Returns(AuthorizationResult.Success());

        var (canView, canCreate) = await InterCompanyPermissionHelper.GetCounterpartPermissionsAsync(
            authService, "MyERP.SalesInvoices", "MyERP.SalesInvoices.Create");

        canView.ShouldBeFalse();
        canCreate.ShouldBeTrue();
    }
}
