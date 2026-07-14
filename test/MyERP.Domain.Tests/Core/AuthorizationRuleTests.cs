using System;
using MyERP.Core;
using MyERP.Core.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Core;

public class AuthorizationRuleTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    private AuthorizationRule CreateRule(
        AuthorizationBasedOn basedOn = AuthorizationBasedOn.GrandTotal,
        decimal threshold = 50_000m,
        string transactionType = "SalesInvoice")
    {
        return new AuthorizationRule(Guid.NewGuid(), transactionType, basedOn, threshold, _companyId);
    }

    [Fact]
    public void Rule_DefaultState()
    {
        var rule = CreateRule();
        Assert.Equal("SalesInvoice", rule.TransactionType);
        Assert.Equal(AuthorizationBasedOn.GrandTotal, rule.BasedOn);
        Assert.Equal(50_000m, rule.ThresholdValue);
        Assert.Null(rule.SystemUserId);
        Assert.Null(rule.SystemRole);
        Assert.Null(rule.ApprovingRole);
        Assert.Null(rule.ApprovingUserId);
    }

    [Fact]
    public void Rule_IsExceeded_AboveThreshold_True()
    {
        var rule = CreateRule(threshold: 50_000m);
        Assert.True(rule.IsExceeded(50_001m));
    }

    [Fact]
    public void Rule_IsExceeded_AtThreshold_False()
    {
        var rule = CreateRule(threshold: 50_000m);
        Assert.False(rule.IsExceeded(50_000m)); // Not exceeding, equal
    }

    [Fact]
    public void Rule_IsExceeded_BelowThreshold_False()
    {
        var rule = CreateRule(threshold: 50_000m);
        Assert.False(rule.IsExceeded(49_999m));
    }

    [Fact]
    public void Rule_IsAuthorizedApprover_ByUserId()
    {
        var rule = CreateRule();
        var approverId = Guid.NewGuid();
        rule.ApprovingUserId = approverId;

        Assert.True(rule.IsAuthorizedApprover(approverId, Array.Empty<string>()));
        Assert.False(rule.IsAuthorizedApprover(Guid.NewGuid(), Array.Empty<string>()));
    }

    [Fact]
    public void Rule_IsAuthorizedApprover_ByRole()
    {
        var rule = CreateRule();
        rule.ApprovingRole = "Finance Manager";

        Assert.True(rule.IsAuthorizedApprover(Guid.NewGuid(), new[] { "Finance Manager" }));
        Assert.False(rule.IsAuthorizedApprover(Guid.NewGuid(), new[] { "Sales User" }));
    }

    [Fact]
    public void Rule_IsAuthorizedApprover_RoleCaseInsensitive()
    {
        var rule = CreateRule();
        rule.ApprovingRole = "Finance Manager";

        Assert.True(rule.IsAuthorizedApprover(Guid.NewGuid(), new[] { "finance manager" }));
    }

    [Fact]
    public void Rule_GetTier_UserSpecific()
    {
        var rule = CreateRule();
        rule.SystemUserId = Guid.NewGuid();
        Assert.Equal(1, rule.GetTier());
    }

    [Fact]
    public void Rule_GetTier_RoleSpecific()
    {
        var rule = CreateRule();
        rule.SystemRole = "Sales User";
        Assert.Equal(2, rule.GetTier());
    }

    [Fact]
    public void Rule_GetTier_Global()
    {
        var rule = CreateRule();
        Assert.Equal(3, rule.GetTier());
    }

    [Fact]
    public void Rule_Validate_NoApprover_Throws()
    {
        var rule = CreateRule();
        // No ApprovingRole and no ApprovingUserId

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:01013", ex.Code);
    }

    [Fact]
    public void Rule_Validate_WithRole_Succeeds()
    {
        var rule = CreateRule();
        rule.ApprovingRole = "Finance Manager";
        rule.Validate(); // Should not throw
    }

    [Fact]
    public void Rule_Validate_WithUser_Succeeds()
    {
        var rule = CreateRule();
        rule.ApprovingUserId = Guid.NewGuid();
        rule.Validate(); // Should not throw
    }

    [Fact]
    public void Rule_Validate_SelfApproval_Throws()
    {
        var rule = CreateRule();
        var userId = Guid.NewGuid();
        rule.SystemUserId = userId;
        rule.ApprovingUserId = userId; // Same user!

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:01014", ex.Code);
    }

    [Fact]
    public void Rule_Validate_SameRoleSystem_And_Approving_Throws()
    {
        var rule = CreateRule();
        rule.SystemRole = "Sales User";
        rule.ApprovingRole = "Sales User"; // Same role!

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:01014", ex.Code);
    }

    [Fact]
    public void Rule_Validate_DiscountExceeds100_Throws()
    {
        var rule = CreateRule(AuthorizationBasedOn.AverageDiscount, 101m);
        rule.ApprovingRole = "Manager";

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:01015", ex.Code);
    }

    [Fact]
    public void Rule_Validate_Discount100_Succeeds()
    {
        var rule = CreateRule(AuthorizationBasedOn.AverageDiscount, 100m);
        rule.ApprovingRole = "Manager";
        rule.Validate(); // 100% is valid
    }

    [Fact]
    public void Rule_Validate_CustomerwiseDiscount_NoCustomer_Throws()
    {
        var rule = CreateRule(AuthorizationBasedOn.CustomerwiseDiscount, 30m);
        rule.ApprovingRole = "Manager";
        rule.CustomerId = null;

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:01016", ex.Code);
    }

    [Fact]
    public void Rule_Validate_CustomerwiseDiscount_WithCustomer_Succeeds()
    {
        var rule = CreateRule(AuthorizationBasedOn.CustomerwiseDiscount, 30m);
        rule.ApprovingRole = "Manager";
        rule.CustomerId = Guid.NewGuid();
        rule.Validate();
    }

    [Fact]
    public void Rule_Validate_DifferentSystemAndApprovingUsers_Succeeds()
    {
        var rule = CreateRule();
        rule.SystemUserId = Guid.NewGuid();
        rule.ApprovingUserId = Guid.NewGuid(); // Different user
        rule.Validate();
    }

    [Fact]
    public void Rule_Validate_DifferentRoles_Succeeds()
    {
        var rule = CreateRule();
        rule.SystemRole = "Sales User";
        rule.ApprovingRole = "Finance Manager"; // Different role
        rule.Validate();
    }

    [Fact]
    public void Rule_CompanyScope_NullIsGlobal()
    {
        var rule = new AuthorizationRule(Guid.NewGuid(), "SalesInvoice",
            AuthorizationBasedOn.GrandTotal, 100_000m, companyId: null);
        Assert.Null(rule.CompanyId);
    }

    [Fact]
    public void Rule_TransactionType_Required()
    {
        Assert.Throws<ArgumentException>(() =>
            new AuthorizationRule(Guid.NewGuid(), "", AuthorizationBasedOn.GrandTotal, 50_000m));
    }
}
