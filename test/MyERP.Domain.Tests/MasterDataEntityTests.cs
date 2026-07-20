using System;
using MyERP.Assets;
using MyERP.Assets.Entities;
using MyERP.HumanResources.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.MasterDataEntities;

public class MasterDataEntityTests
{
    // ========== AssetCategory Tests ==========

    [Fact]
    public void AssetCategory_Create_SetsDefaults()
    {
        var cat = new AssetCategory(Guid.NewGuid(), "Computer Equipment");
        cat.CategoryName.ShouldBe("Computer Equipment");
        cat.IsDepreciable.ShouldBeTrue();
        cat.DefaultDepreciationMethod.ShouldBe(DepreciationMethod.StraightLine);
        cat.DefaultUsefulLifeMonths.ShouldBe(60);
        cat.DefaultDepreciationRate.ShouldBeNull();
    }

    [Fact]
    public void AssetCategory_GLAccounts_DefaultNull()
    {
        var cat = new AssetCategory(Guid.NewGuid(), "Vehicles");
        cat.AssetAccountId.ShouldBeNull();
        cat.DepreciationAccountId.ShouldBeNull();
        cat.AccumulatedDepreciationAccountId.ShouldBeNull();
    }

    [Fact]
    public void AssetCategory_GLAccounts_CanBeSet()
    {
        var cat = new AssetCategory(Guid.NewGuid(), "Furniture");
        var assetAcct = Guid.NewGuid();
        var deprAcct = Guid.NewGuid();
        var accumAcct = Guid.NewGuid();

        cat.AssetAccountId = assetAcct;
        cat.DepreciationAccountId = deprAcct;
        cat.AccumulatedDepreciationAccountId = accumAcct;

        cat.AssetAccountId.ShouldBe(assetAcct);
        cat.DepreciationAccountId.ShouldBe(deprAcct);
        cat.AccumulatedDepreciationAccountId.ShouldBe(accumAcct);
    }

    [Fact]
    public void AssetCategory_DepreciationSettings_CanBeConfigured()
    {
        var cat = new AssetCategory(Guid.NewGuid(), "Machinery")
        {
            IsDepreciable = true,
            DefaultDepreciationMethod = DepreciationMethod.DoubleDecliningBalance,
            DefaultUsefulLifeMonths = 120,
            DefaultDepreciationRate = 20m,
        };

        cat.DefaultDepreciationMethod.ShouldBe(DepreciationMethod.DoubleDecliningBalance);
        cat.DefaultUsefulLifeMonths.ShouldBe(120);
        cat.DefaultDepreciationRate.ShouldBe(20m);
    }

    [Fact]
    public void AssetCategory_NonDepreciable_CanBeSet()
    {
        var cat = new AssetCategory(Guid.NewGuid(), "Land") { IsDepreciable = false };
        cat.IsDepreciable.ShouldBeFalse();
    }

    [Fact]
    public void AssetCategory_TenantId_SetOnConstruction()
    {
        var tenantId = Guid.NewGuid();
        var cat = new AssetCategory(Guid.NewGuid(), "Equipment", tenantId);
        cat.TenantId.ShouldBe(tenantId);
    }

    // ========== LeaveType Tests ==========

    [Fact]
    public void LeaveType_Create_SetsDefaults()
    {
        var lt = new LeaveType(Guid.NewGuid(), "Annual Leave", 14);
        lt.Name.ShouldBe("Annual Leave");
        lt.MaxDaysAllowed.ShouldBe(14m);
        lt.IsActive.ShouldBeTrue();
        lt.RequiresApproval.ShouldBeTrue();
        lt.IsPaidLeave.ShouldBeTrue();
        lt.AllowCarryForward.ShouldBeFalse();
        lt.IncludeHolidays.ShouldBeFalse();
        lt.AllowNegativeBalance.ShouldBeFalse();
    }

    [Fact]
    public void LeaveType_EmptyName_Throws()
    {
        Should.Throw<ArgumentException>(() => new LeaveType(Guid.NewGuid(), "", 10));
    }

    [Fact]
    public void LeaveType_CarryForward_CanBeConfigured()
    {
        var lt = new LeaveType(Guid.NewGuid(), "Sick Leave", 14)
        {
            AllowCarryForward = true,
            MaxCarryForwardDays = 5,
            CarryForwardExpiryMonths = 3,
        };

        lt.AllowCarryForward.ShouldBeTrue();
        lt.MaxCarryForwardDays.ShouldBe(5m);
        lt.CarryForwardExpiryMonths.ShouldBe(3);
    }

    [Fact]
    public void LeaveType_UnpaidLeave_IsPaidFalse()
    {
        var lt = new LeaveType(Guid.NewGuid(), "Unpaid Leave", 30) { IsPaidLeave = false };
        lt.IsPaidLeave.ShouldBeFalse();
    }

    [Fact]
    public void LeaveType_NoApproval_CanBeSet()
    {
        var lt = new LeaveType(Guid.NewGuid(), "Compensatory Off", 5)
        {
            RequiresApproval = false,
        };
        lt.RequiresApproval.ShouldBeFalse();
    }

    [Fact]
    public void LeaveType_NegativeBalance_CanBeEnabled()
    {
        var lt = new LeaveType(Guid.NewGuid(), "Emergency Leave", 3)
        {
            AllowNegativeBalance = true,
        };
        lt.AllowNegativeBalance.ShouldBeTrue();
    }

    [Fact]
    public void LeaveType_IncludeHolidays_CanBeSet()
    {
        var lt = new LeaveType(Guid.NewGuid(), "Maternity Leave", 98)
        {
            IncludeHolidays = true,
        };
        lt.IncludeHolidays.ShouldBeTrue();
    }

    // ========== SalaryComponent Tests ==========

    [Fact]
    public void SalaryComponent_Create_EarningDefaults()
    {
        var comp = new SalaryComponent(Guid.NewGuid(), "Basic Salary", SalaryComponentType.Earning);
        comp.Name.ShouldBe("Basic Salary");
        comp.ComponentType.ShouldBe(SalaryComponentType.Earning);
        comp.IsStatutory.ShouldBeFalse();
        comp.IsTaxApplicable.ShouldBeTrue();
        comp.DependsOnPaymentDays.ShouldBeTrue();
        comp.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void SalaryComponent_Create_DeductionType()
    {
        var comp = new SalaryComponent(Guid.NewGuid(), "EPF Employee", SalaryComponentType.Deduction);
        comp.ComponentType.ShouldBe(SalaryComponentType.Deduction);
    }

    [Fact]
    public void SalaryComponent_Statutory_CanBeSet()
    {
        var comp = new SalaryComponent(Guid.NewGuid(), "EPF Employer", SalaryComponentType.Deduction)
        {
            IsStatutory = true,
        };
        comp.IsStatutory.ShouldBeTrue();
    }

    [Fact]
    public void SalaryComponent_NonTaxable_CanBeSet()
    {
        var comp = new SalaryComponent(Guid.NewGuid(), "Travel Allowance", SalaryComponentType.Earning)
        {
            IsTaxApplicable = false,
        };
        comp.IsTaxApplicable.ShouldBeFalse();
    }

    [Fact]
    public void SalaryComponent_EmptyName_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new SalaryComponent(Guid.NewGuid(), "", SalaryComponentType.Earning));
    }

    [Fact]
    public void SalaryComponent_WhitespaceName_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new SalaryComponent(Guid.NewGuid(), "   ", SalaryComponentType.Earning));
    }

    [Fact]
    public void SalaryComponent_Abbreviation_CanBeSet()
    {
        var comp = new SalaryComponent(Guid.NewGuid(), "Basic Salary", SalaryComponentType.Earning)
        {
            Abbreviation = "B",
        };
        comp.Abbreviation.ShouldBe("B");
    }

    [Fact]
    public void SalaryComponent_DefaultAccount_CanBeLinked()
    {
        var acctId = Guid.NewGuid();
        var comp = new SalaryComponent(Guid.NewGuid(), "HRA", SalaryComponentType.Earning)
        {
            DefaultAccountId = acctId,
        };
        comp.DefaultAccountId.ShouldBe(acctId);
    }

    [Fact]
    public void SalaryComponent_DoesNotDependOnPaymentDays()
    {
        var comp = new SalaryComponent(Guid.NewGuid(), "Fixed Bonus", SalaryComponentType.Earning)
        {
            DependsOnPaymentDays = false,
        };
        comp.DependsOnPaymentDays.ShouldBeFalse();
    }

    [Fact]
    public void SalaryComponent_TenantId_SetOnConstruction()
    {
        var tenantId = Guid.NewGuid();
        var comp = new SalaryComponent(Guid.NewGuid(), "Tax", SalaryComponentType.Deduction, tenantId);
        comp.TenantId.ShouldBe(tenantId);
    }
}
