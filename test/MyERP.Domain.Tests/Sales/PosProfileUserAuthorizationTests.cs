using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Tests for POS Profile User authorization and default profile resolution.
/// Per ERPNext PR #58508 (commit 9018573179) and PR #58591 (commit 4355f8e60e).
/// </summary>
public class PosProfileUserAuthorizationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _cashierUser1 = Guid.NewGuid();
    private readonly Guid _cashierUser2 = Guid.NewGuid();
    private readonly Guid _unauthorizedUser = Guid.NewGuid();

    [Fact]
    public void AddUser_AddsCashierWithDefaultFlag()
    {
        var profile = new PosProfile(Guid.NewGuid(), _companyId, "Main POS Register", _warehouseId);
        profile.AddUser(_cashierUser1, isDefault: true);

        profile.Users.Count.ShouldBe(1);
        profile.Users[0].UserId.ShouldBe(_cashierUser1);
        profile.Users[0].IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void AddUser_DuplicateCashier_ThrowsBusinessException()
    {
        var profile = new PosProfile(Guid.NewGuid(), _companyId, "Main POS Register", _warehouseId);
        profile.AddUser(_cashierUser1, isDefault: true);

        var ex = Should.Throw<BusinessException>(() => profile.AddUser(_cashierUser1, isDefault: false));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void RemoveUser_RemovesSpecifiedUser()
    {
        var profile = new PosProfile(Guid.NewGuid(), _companyId, "Main POS Register", _warehouseId);
        profile.AddUser(_cashierUser1, isDefault: true);
        profile.AddUser(_cashierUser2, isDefault: false);

        profile.RemoveUser(_cashierUser1);

        profile.Users.Count.ShouldBe(1);
        profile.Users[0].UserId.ShouldBe(_cashierUser2);
    }

    [Fact]
    public void ClearUsers_RemovesAllUsers()
    {
        var profile = new PosProfile(Guid.NewGuid(), _companyId, "Main POS Register", _warehouseId);
        profile.AddUser(_cashierUser1, isDefault: true);
        profile.AddUser(_cashierUser2, isDefault: false);

        profile.ClearUsers();

        profile.Users.ShouldBeEmpty();
    }

    [Fact]
    public void CashierAuthorization_AssignedCashier_IsAuthorized()
    {
        var profile = new PosProfile(Guid.NewGuid(), _companyId, "Kiosk 1", _warehouseId);
        profile.AddUser(_cashierUser1);

        var isAuthorized = profile.Users.Count == 0 || profile.Users.Any(u => u.UserId == _cashierUser1);
        isAuthorized.ShouldBeTrue();
    }

    [Fact]
    public void CashierAuthorization_UnassignedCashier_IsBlocked()
    {
        var profile = new PosProfile(Guid.NewGuid(), _companyId, "Kiosk 1", _warehouseId);
        profile.AddUser(_cashierUser1);

        var isAuthorized = profile.Users.Count == 0 || profile.Users.Any(u => u.UserId == _unauthorizedUser);
        isAuthorized.ShouldBeFalse();
    }

    [Fact]
    public void CashierAuthorization_NoUsersConfigured_AllowsAllCashiers()
    {
        var profile = new PosProfile(Guid.NewGuid(), _companyId, "Open Register", _warehouseId);
        // No users added -> legacy open behavior

        var isAuthorized = profile.Users.Count == 0 || profile.Users.Any(u => u.UserId == _unauthorizedUser);
        isAuthorized.ShouldBeTrue();
    }

    [Fact]
    public void DefaultProfileResolution_PrioritizesDefaultProfileForUser()
    {
        var p1 = new PosProfile(Guid.NewGuid(), _companyId, "Register 1", _warehouseId);
        p1.AddUser(_cashierUser1, isDefault: false);

        var p2 = new PosProfile(Guid.NewGuid(), _companyId, "Register 2 (Default)", _warehouseId);
        p2.AddUser(_cashierUser1, isDefault: true);

        var p3 = new PosProfile(Guid.NewGuid(), _companyId, "Open Register", _warehouseId);

        var profiles = new List<PosProfile> { p1, p3, p2 };

        var resolved = profiles
            .Where(p => p.Users.Count == 0 || p.Users.Any(u => u.UserId == _cashierUser1))
            .OrderByDescending(p => p.Users.Any(u => u.UserId == _cashierUser1 && u.IsDefault))
            .ThenByDescending(p => p.Users.Any(u => u.UserId == _cashierUser1))
            .First();

        resolved.Id.ShouldBe(p2.Id);
        resolved.ProfileName.ShouldBe("Register 2 (Default)");
    }
}
