using System;
using System.Linq;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class SalesPersonTests
{
    [Fact]
    public void SalesPerson_DefaultState()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Ahmad");
        Assert.Equal("Ahmad", sp.Name);
        Assert.Null(sp.ParentSalesPersonId);
        Assert.False(sp.IsGroup);
        Assert.True(sp.IsEnabled);
        Assert.Equal(0m, sp.CommissionRate);
        Assert.Null(sp.EmployeeId);
        Assert.Empty(sp.Targets);
    }

    [Fact]
    public void SalesPerson_WithParent()
    {
        var parentId = Guid.NewGuid();
        var sp = new SalesPerson(Guid.NewGuid(), "Junior Rep", parentId: parentId);
        Assert.Equal(parentId, sp.ParentSalesPersonId);
    }

    [Fact]
    public void SalesPerson_SetCommissionRate_Valid()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Rep");
        sp.SetCommissionRate(5m);
        Assert.Equal(5m, sp.CommissionRate);
    }

    [Fact]
    public void SalesPerson_SetCommissionRate_Boundary()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Rep");
        sp.SetCommissionRate(0m);   // Minimum valid
        Assert.Equal(0m, sp.CommissionRate);

        sp.SetCommissionRate(100m); // Maximum valid
        Assert.Equal(100m, sp.CommissionRate);
    }

    [Fact]
    public void SalesPerson_SetCommissionRate_Invalid_Throws()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Rep");
        Assert.Throws<BusinessException>(() => sp.SetCommissionRate(-1m));
        Assert.Throws<BusinessException>(() => sp.SetCommissionRate(101m));
    }

    [Fact]
    public void SalesPerson_CalculateCommission()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Rep");
        sp.SetCommissionRate(5m);

        // 5% of RM 10,000 = RM 500
        Assert.Equal(500m, sp.CalculateCommission(10_000m));
    }

    [Fact]
    public void SalesPerson_CalculateCommission_ZeroRate()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Rep");
        Assert.Equal(0m, sp.CalculateCommission(10_000m));
    }

    [Fact]
    public void SalesPerson_Disable()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Rep");
        sp.Disable();
        Assert.False(sp.IsEnabled);
    }

    [Fact]
    public void SalesPerson_AddTarget_Valid()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Rep");
        sp.AddTarget(Guid.NewGuid(), 0m, 100_000m); // Amount target

        Assert.Single(sp.Targets);
        Assert.Equal(100_000m, sp.Targets.First().TargetAmount);
    }

    [Fact]
    public void SalesPerson_AddTarget_QtyTarget()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Rep");
        sp.AddTarget(Guid.NewGuid(), 500m, 0m); // Qty target

        Assert.Equal(500m, sp.Targets.First().TargetQty);
    }

    [Fact]
    public void SalesPerson_AddTarget_BothZero_Throws()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Rep");
        var ex = Assert.Throws<BusinessException>(() =>
            sp.AddTarget(Guid.NewGuid(), 0m, 0m));
        Assert.Equal("MyERP:03013", ex.Code);
    }

    [Fact]
    public void SalesPerson_NameRequired()
    {
        Assert.Throws<ArgumentException>(() => new SalesPerson(Guid.NewGuid(), ""));
    }

    [Fact]
    public void SalesTeamEntry_CommissionCalculation()
    {
        // Sales person with 5% commission, allocated 60% of RM 50,000 eligible
        var entry = new SalesTeamEntry(
            Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            allocatedPercentage: 60m, eligibleAmount: 50_000m, commissionRate: 5m);

        // allocated_amount = 50000 × 60/100 = 30000
        Assert.Equal(30_000m, entry.AllocatedAmount);
        // incentives = 30000 × 5/100 = 1500
        Assert.Equal(1_500m, entry.Incentives);
    }

    [Fact]
    public void SalesTeamEntry_100Percent()
    {
        var entry = new SalesTeamEntry(
            Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(),
            allocatedPercentage: 100m, eligibleAmount: 10_000m, commissionRate: 3m);

        Assert.Equal(10_000m, entry.AllocatedAmount);
        Assert.Equal(300m, entry.Incentives);
    }

    [Fact]
    public void SalesTeamEntry_ZeroCommission()
    {
        var entry = new SalesTeamEntry(
            Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            allocatedPercentage: 50m, eligibleAmount: 20_000m, commissionRate: 0m);

        Assert.Equal(10_000m, entry.AllocatedAmount);
        Assert.Equal(0m, entry.Incentives);
    }

    [Fact]
    public void SalesTeamEntry_Properties()
    {
        var personId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var entry = new SalesTeamEntry(
            Guid.NewGuid(), personId, "SalesInvoice", parentId,
            40m, 100_000m, 7.5m);

        Assert.Equal(personId, entry.SalesPersonId);
        Assert.Equal("SalesInvoice", entry.ParentType);
        Assert.Equal(parentId, entry.ParentId);
        Assert.Equal(40m, entry.AllocatedPercentage);
        Assert.Equal(7.5m, entry.CommissionRate);
    }
}
